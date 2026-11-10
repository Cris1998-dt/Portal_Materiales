namespace PortalMateriales.Domain;

/// <summary>Quién ejecuta una acción. El dominio lo recibe para validar rol y propiedad.</summary>
public sealed record Actor(int UsuarioId, string Nombre, Rol Rol, int? ProveedorId = null);
