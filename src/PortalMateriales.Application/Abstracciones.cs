using PortalMateriales.Domain;

namespace PortalMateriales.Application;

// Puertos que la capa de infraestructura implementa.

public interface ISolicitudRepository
{
    /// <summary>Carga el agregado completo (envíos, evaluaciones con fotos, historial).</summary>
    Task<Solicitud?> ObtenerAsync(int id, CancellationToken ct);
    void Agregar(Solicitud solicitud);
    Task GuardarCambiosAsync(CancellationToken ct);
}

/// <summary>Lecturas que no necesitan el agregado: se resuelven con proyecciones directas.</summary>
public interface IConsultas
{
    Task<IReadOnlyList<SolicitudResumenDto>> ListarSolicitudesAsync(EstadoSolicitud? estado, int? proveedorId, DateOnly hoy, CancellationToken ct);
    Task<IndicadoresDto> IndicadoresAsync(DateOnly hoy, CancellationToken ct);
    Task<IReadOnlyList<ProveedorDto>> ListarProveedoresAsync(bool? activo, CancellationToken ct);
    Task<Proveedor?> ObtenerProveedorAsync(int id, CancellationToken ct);
    Task<IReadOnlyDictionary<int, string>> NombresUsuariosAsync(IEnumerable<int> ids, CancellationToken ct);
}

public interface IProveedorRepository
{
    /// <summary>Carga el proveedor con sus contactos, con seguimiento de cambios.</summary>
    Task<Proveedor?> ObtenerAsync(int id, CancellationToken ct);
    Task<bool> ExisteRtnAsync(string rtn, int? exceptoId, CancellationToken ct);
    void Agregar(Proveedor proveedor);
    Task GuardarCambiosAsync(CancellationToken ct);
}

public interface IUsuarioRepository
{
    Task<Usuario?> ObtenerPorNombreUsuarioAsync(string nombreUsuario, CancellationToken ct);
}

public interface IAlmacenArchivos
{
    Task GuardarAsync(string nombre, Stream contenido, string contentType, CancellationToken ct);
    Task<Stream?> AbrirAsync(string nombre, CancellationToken ct);
    Task EliminarAsync(string nombre, CancellationToken ct);
}

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verificar(string password, string hash);
}

public class NoEncontradoException(string mensaje) : Exception(mensaje);
