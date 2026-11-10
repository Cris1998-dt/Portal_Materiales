using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using PortalMateriales.Domain;

namespace PortalMateriales.Api;

/// <summary>Emite y lee el JWT. El token lleva lo que el dominio necesita para armar el <see cref="Actor"/>.</summary>
public class Tokens(string clave)
{
    private readonly SigningCredentials _firma = new(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(clave)), SecurityAlgorithms.HmacSha256);

    public (string Token, DateTime Expira) Emitir(Actor a)
    {
        var expira = DateTime.UtcNow.AddHours(8);
        var claims = new Dictionary<string, object>
        {
            ["sub"] = a.UsuarioId.ToString(), ["name"] = a.Nombre, ["role"] = a.Rol.ToString(),
        };
        if (a.ProveedorId is int p) claims["proveedor_id"] = p.ToString();
        var token = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Claims = claims, Expires = expira, SigningCredentials = _firma,
        });
        return (token, expira);
    }

    public static Actor Actor(ClaimsPrincipal u) => new(
        int.Parse(u.FindFirstValue("sub")!),
        u.FindFirstValue("name")!,
        Enum.Parse<Rol>(u.FindFirstValue("role")!),
        int.TryParse(u.FindFirstValue("proveedor_id"), out var p) ? p : null);
}
