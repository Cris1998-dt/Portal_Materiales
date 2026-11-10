using System.Linq.Expressions;

namespace PortalMateriales.Domain;

/// <summary>
/// Raíz del agregado. Toda transición del flujo pasa por un método de esta clase,
/// que valida estado, rol y propiedad, y deja el registro en el historial.
/// </summary>
public class Solicitud
{
    public const int MaxFotosPorEvaluacion = 5;

    public int Id { get; private set; }
    public string Material { get; private set; } = "";
    public TipoMaterial Tipo { get; private set; }
    public string Descripcion { get; private set; } = "";
    public DateOnly FechaObjetivo { get; private set; }
    public Prioridad Prioridad { get; private set; }
    public EstadoSolicitud Estado { get; private set; }
    public int SolicitanteId { get; private set; }
    public int? ProveedorId { get; private set; }
    /// <summary>Compromiso vigente del proveedor. Se limpia al rechazar la muestra.</summary>
    public DateOnly? FechaCompromiso { get; private set; }
    public DateTime CreadaEn { get; private set; }

    private readonly List<Envio> _envios = [];
    private readonly List<Evaluacion> _evaluaciones = [];
    private readonly List<HistorialCambio> _historial = [];
    public IReadOnlyList<Envio> Envios => _envios;
    public IReadOnlyList<Evaluacion> Evaluaciones => _evaluaciones;
    public IReadOnlyList<HistorialCambio> Historial => _historial;

    private Solicitud() { }

    /// <summary>Única definición de "vencida", usable en memoria y traducible a SQL por EF.</summary>
    public static Expression<Func<Solicitud, bool>> EsVencida(DateOnly hoy) =>
        s => s.Estado == EstadoSolicitud.EnProgreso && s.FechaCompromiso != null && s.FechaCompromiso < hoy;

    public bool EstaVencida(DateOnly hoy) => EsVencida(hoy).Compile()(this);

    public static Solicitud Crear(string material, TipoMaterial tipo, string descripcion,
        DateOnly fechaObjetivo, Prioridad prioridad, Actor actor, DateTime ahora)
    {
        ExigirRol(actor, Rol.Solicitante);
        var s = new Solicitud
        {
            Material = Texto(material, nameof(Material), 150),
            Tipo = tipo,
            Descripcion = Texto(descripcion, nameof(Descripcion), 2000),
            FechaObjetivo = fechaObjetivo >= Hoy(ahora) ? fechaObjetivo
                : throw new ValidacionException("La fecha objetivo no puede estar en el pasado."),
            Prioridad = prioridad,
            Estado = EstadoSolicitud.Solicitado,
            SolicitanteId = actor.UsuarioId,
            CreadaEn = ahora,
        };
        s.Registrar(actor, ahora, "Creó la solicitud", null, $"Prioridad {prioridad}, objetivo {fechaObjetivo:yyyy-MM-dd}");
        return s;
    }

    public void Asignar(Proveedor proveedor, Actor actor, DateTime ahora)
    {
        ExigirRol(actor, Rol.Coordinador);
        ExigirEstado(EstadoSolicitud.Solicitado);
        if (!proveedor.Activo) throw new ValidacionException($"El proveedor {proveedor.Nombre} está inactivo.");
        ProveedorId = proveedor.Id;
        Cambiar(EstadoSolicitud.Asignado, actor, ahora, "Asignó proveedor", proveedor.Nombre);
    }

    /// <summary>
    /// El proveedor se compromete a una fecha. Válido al recibir la asignación
    /// o después de un rechazo (En progreso sin compromiso vigente).
    /// </summary>
    public void Aceptar(DateOnly fechaCompromiso, Actor actor, DateTime ahora)
    {
        ExigirProveedorAsignado(actor);
        if (!EsperaCompromiso)
            throw new TransicionInvalidaException($"No se puede aceptar una solicitud en estado {Estado} con compromiso vigente.");
        if (fechaCompromiso < Hoy(ahora))
            throw new ValidacionException("La fecha compromiso no puede estar en el pasado.");

        FechaCompromiso = fechaCompromiso;
        Cambiar(EstadoSolicitud.EnProgreso, actor, ahora, "Aceptó con fecha compromiso", $"{fechaCompromiso:yyyy-MM-dd}");
    }

    public void RegistrarEnvio(DateOnly fechaEnvio, string transportista, string numeroGuia, Actor actor, DateTime ahora)
    {
        ExigirProveedorAsignado(actor);
        ExigirEstado(EstadoSolicitud.EnProgreso);
        if (FechaCompromiso is not { } compromiso)
            throw new TransicionInvalidaException("Debe registrar una fecha compromiso antes de enviar.");
        if (fechaEnvio > Hoy(ahora))
            throw new ValidacionException("La fecha de envío no puede estar en el futuro.");
        if (fechaEnvio < DateOnly.FromDateTime(CreadaEn))
            throw new ValidacionException("La fecha de envío no puede ser anterior a la solicitud.");

        var envio = new Envio(fechaEnvio, Texto(transportista, "Transportista", 100), Texto(numeroGuia, "NumeroGuia", 100), compromiso);
        _envios.Add(envio);
        Cambiar(EstadoSolicitud.Enviado, actor, ahora, "Registró envío",
            $"{envio.Transportista}, guía {envio.NumeroGuia}, enviado {fechaEnvio:yyyy-MM-dd}" + (envio.ATiempo ? "" : " (fuera de compromiso)"));
    }

    public void RegistrarEvaluacion(ResultadoEvaluacion resultado, string comentarios,
        IReadOnlyCollection<FotoMuestra> fotos, Actor actor, DateTime ahora)
    {
        ValidarEvaluacion(actor, fotos.Count);
        _evaluaciones.Add(new Evaluacion(resultado, Texto(comentarios, "Comentarios", 2000), actor.UsuarioId, ahora, fotos));
        Cambiar(EstadoSolicitud.EnEvaluacion, actor, ahora, "Registró evaluación", $"{resultado}, {fotos.Count} foto(s)");
    }

    /// <summary>Permite validar antes de subir las fotos al almacenamiento.</summary>
    public void ValidarEvaluacion(Actor actor, int cantidadFotos)
    {
        ExigirRol(actor, Rol.Coordinador);
        ExigirEstado(EstadoSolicitud.Enviado);
        if (cantidadFotos is 0 or > MaxFotosPorEvaluacion)
            throw new ValidacionException($"La evaluación lleva entre 1 y {MaxFotosPorEvaluacion} fotos de la muestra.");
    }

    public void Aprobar(string? comentario, Actor actor, DateTime ahora)
    {
        ExigirSolicitanteDueno(actor);
        ExigirEstado(EstadoSolicitud.EnEvaluacion);
        Cambiar(EstadoSolicitud.Aprobado, actor, ahora, "Aprobó la muestra", comentario);
    }

    /// <summary>Rechazar regresa a En progreso y anula el compromiso: el proveedor debe comprometer una nueva fecha.</summary>
    public void Rechazar(string comentario, Actor actor, DateTime ahora)
    {
        ExigirSolicitanteDueno(actor);
        ExigirEstado(EstadoSolicitud.EnEvaluacion);
        var motivo = Texto(comentario, "Comentario", 1000);
        FechaCompromiso = null;
        Cambiar(EstadoSolicitud.EnProgreso, actor, ahora, "Rechazó la muestra", motivo);
    }

    /// <summary>
    /// Qué puede hacer este actor ahora mismo. La UI lo usa para mostrar solo los botones válidos;
    /// se arma con los mismos predicados que validan cada método (una prueba lo garantiza).
    /// </summary>
    public IReadOnlyList<AccionSolicitud> AccionesPara(Actor actor)
    {
        var acciones = new List<AccionSolicitud>();
        if (Estado == EstadoSolicitud.Solicitado && actor.Rol == Rol.Coordinador) acciones.Add(AccionSolicitud.Asignar);
        if (EsProveedorAsignado(actor) && EsperaCompromiso) acciones.Add(AccionSolicitud.Aceptar);
        if (EsProveedorAsignado(actor) && Estado == EstadoSolicitud.EnProgreso && FechaCompromiso is not null) acciones.Add(AccionSolicitud.RegistrarEnvio);
        if (Estado == EstadoSolicitud.Enviado && actor.Rol == Rol.Coordinador) acciones.Add(AccionSolicitud.Evaluar);
        if (Estado == EstadoSolicitud.EnEvaluacion && EsSolicitanteDueno(actor)) acciones.AddRange([AccionSolicitud.Aprobar, AccionSolicitud.Rechazar]);
        return acciones;
    }

    // ---- reglas auxiliares ----

    /// <summary>Recién asignada, o de vuelta en progreso tras un rechazo sin compromiso vigente.</summary>
    private bool EsperaCompromiso =>
        Estado == EstadoSolicitud.Asignado || (Estado == EstadoSolicitud.EnProgreso && FechaCompromiso is null);

    private bool EsProveedorAsignado(Actor a) => a.Rol == Rol.Proveedor && ProveedorId is not null && a.ProveedorId == ProveedorId;
    private bool EsSolicitanteDueno(Actor a) => a.Rol == Rol.Solicitante && a.UsuarioId == SolicitanteId;

    private void Cambiar(EstadoSolicitud nuevo, Actor actor, DateTime ahora, string accion, string? detalle)
    {
        var anterior = Estado;
        Estado = nuevo;
        Registrar(actor, ahora, accion, anterior, detalle);
    }

    private void Registrar(Actor actor, DateTime ahora, string accion, EstadoSolicitud? anterior, string? detalle) =>
        _historial.Add(new HistorialCambio(ahora, actor.UsuarioId, actor.Nombre, accion, anterior, Estado, detalle));

    private void ExigirEstado(EstadoSolicitud esperado)
    {
        if (Estado != esperado)
            throw new TransicionInvalidaException($"La acción requiere estado {esperado}; la solicitud está en {Estado}.");
    }

    private static void ExigirRol(Actor actor, Rol rol)
    {
        if (actor.Rol != rol) throw new AccesoDenegadoException($"Esta acción la realiza el rol {rol}.");
    }

    private void ExigirProveedorAsignado(Actor actor)
    {
        ExigirRol(actor, Rol.Proveedor);
        if (!EsProveedorAsignado(actor))
            throw new AccesoDenegadoException("La solicitud no está asignada a su proveedor.");
    }

    private void ExigirSolicitanteDueno(Actor actor)
    {
        ExigirRol(actor, Rol.Solicitante);
        if (!EsSolicitanteDueno(actor))
            throw new AccesoDenegadoException("Solo quien creó la solicitud puede decidir sobre la muestra.");
    }

    private static DateOnly Hoy(DateTime ahora) => DateOnly.FromDateTime(ahora);

    private static string Texto(string? valor, string campo, int max) => Validar.Texto(valor, campo, max);
}
