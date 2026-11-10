using PortalMateriales.Domain;

namespace PortalMateriales.Application;

public class AuthService(IUsuarioRepository usuarios, IPasswordHasher hasher)
{
    /// <summary>Devuelve el actor si las credenciales son válidas; null si no (sin decir cuál campo falló).</summary>
    public async Task<Actor?> LoginAsync(LoginRequest r, CancellationToken ct)
    {
        var u = await usuarios.ObtenerPorNombreUsuarioAsync(r.Usuario?.Trim() ?? "", ct);
        return u is not null && hasher.Verificar(r.Password ?? "", u.PasswordHash) ? u.ComoActor() : null;
    }
}
