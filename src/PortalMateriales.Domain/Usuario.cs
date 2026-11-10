namespace PortalMateriales.Domain;

public class Usuario
{
    public int Id { get; private set; }
    public string NombreUsuario { get; private set; } = "";
    public string Nombre { get; private set; } = "";
    public Rol Rol { get; private set; }
    public string PasswordHash { get; private set; } = "";
    /// <summary>Solo para usuarios con rol Proveedor: a qué proveedor representan.</summary>
    public int? ProveedorId { get; private set; }

    private Usuario() { }

    public Usuario(string nombreUsuario, string nombre, Rol rol, string passwordHash, int? proveedorId = null)
    {
        if (rol == Rol.Proveedor && proveedorId is null)
            throw new ValidacionException("Un usuario proveedor debe estar ligado a un proveedor.");
        NombreUsuario = nombreUsuario; Nombre = nombre; Rol = rol; PasswordHash = passwordHash; ProveedorId = proveedorId;
    }

    public Actor ComoActor() => new(Id, Nombre, Rol, ProveedorId);
}
