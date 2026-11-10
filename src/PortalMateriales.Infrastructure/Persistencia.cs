using Microsoft.EntityFrameworkCore;
using PortalMateriales.Application;
using PortalMateriales.Domain;

namespace PortalMateriales.Infrastructure;

public class SolicitudRepository(AppDbContext db) : ISolicitudRepository
{
    public Task<Solicitud?> ObtenerAsync(int id, CancellationToken ct) =>
        db.Solicitudes
            .Include(s => s.Envios)
            .Include(s => s.Evaluaciones).ThenInclude(e => e.Fotos)
            .Include(s => s.Historial)
            .AsSplitQuery()
            .FirstOrDefaultAsync(s => s.Id == id, ct);

    public void Agregar(Solicitud solicitud) => db.Solicitudes.Add(solicitud);
    public Task GuardarCambiosAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}

public class ProveedorRepository(AppDbContext db) : IProveedorRepository
{
    public Task<Proveedor?> ObtenerAsync(int id, CancellationToken ct) =>
        db.Proveedores.Include(p => p.Contactos).FirstOrDefaultAsync(p => p.Id == id, ct);

    public Task<bool> ExisteRtnAsync(string rtn, int? exceptoId, CancellationToken ct) =>
        db.Proveedores.AnyAsync(p => p.Rtn == rtn && p.Id != exceptoId, ct);

    public void Agregar(Proveedor proveedor) => db.Proveedores.Add(proveedor);
    public Task GuardarCambiosAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}

public class UsuarioRepository(AppDbContext db) : IUsuarioRepository
{
    public Task<Usuario?> ObtenerPorNombreUsuarioAsync(string nombreUsuario, CancellationToken ct) =>
        db.Usuarios.AsNoTracking().FirstOrDefaultAsync(u => u.NombreUsuario == nombreUsuario, ct);
}

public class Consultas(AppDbContext db) : IConsultas
{
    public async Task<IReadOnlyList<SolicitudResumenDto>> ListarSolicitudesAsync(EstadoSolicitud? estado, int? proveedorId, DateOnly hoy, CancellationToken ct)
    {
        var q = db.Solicitudes.AsNoTracking();
        if (estado is not null) q = q.Where(s => s.Estado == estado);
        if (proveedorId is not null) q = q.Where(s => s.ProveedorId == proveedorId);
        return await ResumenAsync(q, hoy, ct);
    }

    public async Task<IndicadoresDto> IndicadoresAsync(DateOnly hoy, CancellationToken ct)
    {
        var conteo = await db.Solicitudes.GroupBy(s => s.Estado)
            .Select(g => new { g.Key, Total = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Total, ct);
        var porEstado = Enum.GetValues<EstadoSolicitud>().ToDictionary(e => e, e => conteo.GetValueOrDefault(e));

        var vencidas = await ResumenAsync(db.Solicitudes.AsNoTracking().Where(Solicitud.EsVencida(hoy)), hoy, ct);

        // Cada envío cuenta una vez, comparado con el compromiso vigente cuando se envió.
        var envios = await db.Solicitudes.Where(s => s.ProveedorId != null)
            .SelectMany(s => s.Envios, (s, e) => new { ProveedorId = s.ProveedorId!.Value, e.FechaEnvio, e.FechaCompromiso })
            .GroupBy(x => x.ProveedorId)
            .Select(g => new { g.Key, Total = g.Count(), ATiempo = g.Count(x => x.FechaEnvio <= x.FechaCompromiso) })
            .ToDictionaryAsync(x => x.Key, ct);

        var cumplimiento = (await db.Proveedores.AsNoTracking().OrderBy(p => p.Nombre).ToListAsync(ct))
            .Select(p => envios.TryGetValue(p.Id, out var e)
                ? new CumplimientoProveedorDto(p.Id, p.Nombre, e.Total, e.ATiempo, Math.Round(100m * e.ATiempo / e.Total, 1))
                : new CumplimientoProveedorDto(p.Id, p.Nombre, 0, 0, null))
            .ToList();

        return new IndicadoresDto(porEstado, vencidas.Count, vencidas, cumplimiento);
    }

    public async Task<IReadOnlyList<ProveedorDto>> ListarProveedoresAsync(bool? activo, CancellationToken ct)
    {
        var q = db.Proveedores.AsNoTracking();
        if (activo is not null) q = q.Where(p => p.Activo == activo);
        return (await q.OrderBy(p => p.Nombre).ToListAsync(ct)).Select(ProveedorDto.De).ToList();
    }

    public Task<Proveedor?> ObtenerProveedorAsync(int id, CancellationToken ct) =>
        db.Proveedores.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task<IReadOnlyDictionary<int, string>> NombresUsuariosAsync(IEnumerable<int> ids, CancellationToken ct)
    {
        var lista = ids.Distinct().ToList();
        return await db.Usuarios.Where(u => lista.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Nombre, ct);
    }

    private async Task<IReadOnlyList<SolicitudResumenDto>> ResumenAsync(IQueryable<Solicitud> q, DateOnly hoy, CancellationToken ct)
    {
        var filas = await q.OrderByDescending(s => s.Id)
            .Select(s => new { S = s, Proveedor = db.Proveedores.Where(p => p.Id == s.ProveedorId).Select(p => p.Nombre).FirstOrDefault() })
            .ToListAsync(ct);
        return filas.Select(f => new SolicitudResumenDto(f.S.Id, f.S.Material, f.S.Tipo, f.S.Prioridad, f.S.Estado,
            f.S.FechaObjetivo, f.Proveedor, f.S.FechaCompromiso, f.S.EstaVencida(hoy))).ToList();
    }
}
