using PortalMateriales.Domain;

namespace PortalMateriales.Application;

/// <summary>Mantenimiento del maestro de proveedores y sus contactos.</summary>
public class ProveedoresService(IProveedorRepository repo, IConsultas consultas)
{
    public Task<IReadOnlyList<ProveedorDto>> ListarAsync(bool? activo, CancellationToken ct) => consultas.ListarProveedoresAsync(activo, ct);

    public async Task<ProveedorDetalleDto> DetalleAsync(int id, CancellationToken ct) => Detalle(await CargarAsync(id, ct));

    public async Task<ProveedorDetalleDto> CrearAsync(CrearProveedorRequest r, CancellationToken ct)
    {
        var p = new Proveedor(r.Nombre, r.Tipo, r.Rtn, r.Moneda, r.Idioma, r.Correo, r.Direccion);
        foreach (var c in r.Contactos ?? []) p.AgregarContacto(c.Nombre, c.Puesto, c.Telefono, c.Correo);
        await ExigirRtnUnicoAsync(p, ct);
        repo.Agregar(p);
        await repo.GuardarCambiosAsync(ct);
        return Detalle(p);
    }

    public async Task<ProveedorDetalleDto> ActualizarAsync(int id, ProveedorRequest r, CancellationToken ct)
    {
        var p = await CargarAsync(id, ct);
        p.Actualizar(r.Nombre, r.Tipo, r.Rtn, r.Moneda, r.Idioma, r.Correo, r.Direccion);
        await ExigirRtnUnicoAsync(p, ct);
        await repo.GuardarCambiosAsync(ct);
        return Detalle(p);
    }

    public Task CambiarEstadoAsync(int id, bool activo, CancellationToken ct) =>
        EjecutarAsync(id, p => { if (activo) p.Activar(); else p.Desactivar(); }, ct);

    public async Task<ContactoDto> AgregarContactoAsync(int id, ContactoRequest r, CancellationToken ct)
    {
        var p = await CargarAsync(id, ct);
        var c = p.AgregarContacto(r.Nombre, r.Puesto, r.Telefono, r.Correo);
        await repo.GuardarCambiosAsync(ct);
        return new ContactoDto(c.Id, c.Nombre, c.Puesto, c.Telefono, c.Correo);
    }

    public Task ActualizarContactoAsync(int id, int contactoId, ContactoRequest r, CancellationToken ct) =>
        EjecutarAsync(id, p => p.ActualizarContacto(contactoId, r.Nombre, r.Puesto, r.Telefono, r.Correo), ct);

    public Task EliminarContactoAsync(int id, int contactoId, CancellationToken ct) =>
        EjecutarAsync(id, p => p.EliminarContacto(contactoId), ct);

    private async Task EjecutarAsync(int id, Action<Proveedor> accion, CancellationToken ct)
    {
        var p = await CargarAsync(id, ct);
        accion(p);
        await repo.GuardarCambiosAsync(ct);
    }

    private async Task<Proveedor> CargarAsync(int id, CancellationToken ct) =>
        await repo.ObtenerAsync(id, ct) ?? throw new NoEncontradoException($"Proveedor {id} no encontrado.");

    // El índice único de la base también lo garantiza; aquí se da un mensaje claro en lugar de un error SQL.
    private async Task ExigirRtnUnicoAsync(Proveedor p, CancellationToken ct)
    {
        if (p.Rtn is not null && await repo.ExisteRtnAsync(p.Rtn, p.Id == 0 ? null : p.Id, ct))
            throw new ValidacionException($"Ya existe un proveedor con RTN {p.Rtn}.");
    }

    private static ProveedorDetalleDto Detalle(Proveedor p) => new(ProveedorDto.De(p),
        p.Contactos.OrderBy(c => c.Nombre).Select(c => new ContactoDto(c.Id, c.Nombre, c.Puesto, c.Telefono, c.Correo)).ToList());
}
