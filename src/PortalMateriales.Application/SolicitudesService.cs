using PortalMateriales.Domain;

namespace PortalMateriales.Application;

/// <summary>
/// Casos de uso del flujo. Cada uno: cargar agregado → invocar el método de dominio → guardar.
/// Las reglas viven en <see cref="Solicitud"/>; aquí solo se orquesta.
/// </summary>
public class SolicitudesService(ISolicitudRepository repo, IConsultas consultas, IAlmacenArchivos archivos, TimeProvider reloj)
{
    public const long MaxBytesFoto = 5 * 1024 * 1024;
    private static readonly Dictionary<string, string> TiposFoto = new(StringComparer.OrdinalIgnoreCase)
    {
        ["image/jpeg"] = ".jpg", ["image/png"] = ".png", ["image/webp"] = ".webp",
    };

    public async Task<int> CrearAsync(CrearSolicitudRequest r, Actor actor, CancellationToken ct)
    {
        var s = Solicitud.Crear(r.Material, r.Tipo, r.Descripcion, r.FechaObjetivo, r.Prioridad, actor, reloj.AhoraLocal());
        repo.Agregar(s);
        await repo.GuardarCambiosAsync(ct);
        return s.Id;
    }

    public Task<IReadOnlyList<SolicitudResumenDto>> ListarAsync(EstadoSolicitud? estado, Actor actor, CancellationToken ct) =>
        consultas.ListarSolicitudesAsync(estado, ProveedorVisible(actor), reloj.Hoy(), ct);

    public async Task<SolicitudDetalleDto> DetalleAsync(int id, Actor actor, CancellationToken ct)
    {
        var s = await CargarVisibleAsync(id, actor, ct);
        var proveedor = s.ProveedorId is int pid ? await consultas.ObtenerProveedorAsync(pid, ct) : null;
        var nombres = await consultas.NombresUsuariosAsync(s.Evaluaciones.Select(e => e.CoordinadorId).Append(s.SolicitanteId), ct);
        return new SolicitudDetalleDto(s.Id, s.Material, s.Tipo, s.Descripcion, s.Prioridad, s.Estado, s.EstaVencida(reloj.Hoy()),
            s.FechaObjetivo, s.CreadaEn, nombres.GetValueOrDefault(s.SolicitanteId, "?"),
            proveedor is null ? null : ProveedorDto.De(proveedor),
            s.FechaCompromiso,
            s.Envios.Select(e => new EnvioDto(e.FechaEnvio, e.Transportista, e.NumeroGuia, e.FechaCompromiso, e.ATiempo)).ToList(),
            s.Evaluaciones.Select(e => new EvaluacionDto(e.Resultado, e.Comentarios, nombres.GetValueOrDefault(e.CoordinadorId, "?"), e.RegistradaEn,
                e.Fotos.Select(f => new FotoDto(f.Id, f.NombreOriginal, $"/api/solicitudes/{s.Id}/fotos/{f.Id}")).ToList())).ToList(),
            s.Historial.OrderBy(h => h.Fecha).ThenBy(h => h.Id)
                .Select(h => new HistorialDto(h.Fecha, h.UsuarioNombre, h.Accion, h.EstadoAnterior, h.EstadoNuevo, h.Detalle)).ToList(),
            s.AccionesPara(actor));
    }

    public async Task AsignarAsync(int id, AsignarRequest r, Actor actor, CancellationToken ct)
    {
        var s = await CargarVisibleAsync(id, actor, ct);
        var proveedor = await consultas.ObtenerProveedorAsync(r.ProveedorId, ct)
            ?? throw new ValidacionException($"El proveedor {r.ProveedorId} no existe.");
        s.Asignar(proveedor, actor, reloj.AhoraLocal());
        await repo.GuardarCambiosAsync(ct);
    }

    public Task AceptarAsync(int id, AceptarRequest r, Actor actor, CancellationToken ct) =>
        EjecutarAsync(id, actor, s => s.Aceptar(r.FechaCompromiso, actor, reloj.AhoraLocal()), ct);

    public Task RegistrarEnvioAsync(int id, EnvioRequest r, Actor actor, CancellationToken ct) =>
        EjecutarAsync(id, actor, s => s.RegistrarEnvio(r.FechaEnvio, r.Transportista, r.NumeroGuia, actor, reloj.AhoraLocal()), ct);

    public Task AprobarAsync(int id, DecisionRequest r, Actor actor, CancellationToken ct) =>
        EjecutarAsync(id, actor, s => s.Aprobar(r.Comentario, actor, reloj.AhoraLocal()), ct);

    public Task RechazarAsync(int id, DecisionRequest r, Actor actor, CancellationToken ct) =>
        EjecutarAsync(id, actor, s => s.Rechazar(r.Comentario ?? "", actor, reloj.AhoraLocal()), ct);

    /// <summary>
    /// Sube las fotos y luego registra la evaluación. Si el dominio la rechaza o falla el guardado,
    /// borra las fotos ya subidas para no dejar archivos huérfanos.
    /// </summary>
    public async Task RegistrarEvaluacionAsync(int id, ResultadoEvaluacion resultado, string comentarios,
        IReadOnlyList<ArchivoSubido> fotos, Actor actor, CancellationToken ct)
    {
        var s = await CargarVisibleAsync(id, actor, ct);
        s.ValidarEvaluacion(actor, fotos.Count); // antes de subir: no deja archivos de evaluaciones imposibles
        foreach (var f in fotos)
        {
            if (!TiposFoto.ContainsKey(f.ContentType))
                throw new ValidacionException($"'{f.NombreOriginal}': solo se aceptan fotos JPEG, PNG o WebP.");
            if (f.Tamano is 0 or > MaxBytesFoto)
                throw new ValidacionException($"'{f.NombreOriginal}': cada foto debe pesar entre 1 byte y 5 MB.");
        }

        var subidas = new List<FotoMuestra>();
        try
        {
            foreach (var f in fotos)
            {
                var nombre = $"solicitudes/{id}/{Guid.NewGuid():N}{TiposFoto[f.ContentType]}";
                await using var stream = f.Abrir();
                await archivos.GuardarAsync(nombre, stream, f.ContentType, ct);
                subidas.Add(new FotoMuestra(nombre, f.ContentType, Path.GetFileName(f.NombreOriginal)));
            }
            s.RegistrarEvaluacion(resultado, comentarios, subidas, actor, reloj.AhoraLocal());
            await repo.GuardarCambiosAsync(ct);
        }
        catch
        {
            foreach (var f in subidas) await archivos.EliminarAsync(f.NombreArchivo, CancellationToken.None);
            throw;
        }
    }

    public async Task<(Stream Contenido, string ContentType)> AbrirFotoAsync(int id, int fotoId, Actor actor, CancellationToken ct)
    {
        var s = await CargarVisibleAsync(id, actor, ct);
        var foto = s.Evaluaciones.SelectMany(e => e.Fotos).FirstOrDefault(f => f.Id == fotoId)
            ?? throw new NoEncontradoException("Foto no encontrada.");
        var stream = await archivos.AbrirAsync(foto.NombreArchivo, ct)
            ?? throw new NoEncontradoException("El archivo de la foto no está en el almacenamiento.");
        return (stream, foto.ContentType);
    }

    private async Task EjecutarAsync(int id, Actor actor, Action<Solicitud> accion, CancellationToken ct)
    {
        var s = await CargarVisibleAsync(id, actor, ct);
        accion(s);
        await repo.GuardarCambiosAsync(ct);
    }

    /// <summary>Un proveedor solo ve lo asignado a él; para el resto la solicitud "no existe" (no se filtra su existencia).</summary>
    private async Task<Solicitud> CargarVisibleAsync(int id, Actor actor, CancellationToken ct)
    {
        var s = await repo.ObtenerAsync(id, ct);
        if (s is null || (actor.Rol == Rol.Proveedor && s.ProveedorId != actor.ProveedorId))
            throw new NoEncontradoException($"Solicitud {id} no encontrada.");
        return s;
    }

    private static int? ProveedorVisible(Actor actor) => actor.Rol == Rol.Proveedor ? actor.ProveedorId ?? -1 : null;
}
