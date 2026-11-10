using System.Text;
using System.Text.Json;
using Microsoft.JSInterop;
using PortalMateriales.Domain;

namespace PortalMateriales.Web.Servicios;

/// <summary>
/// Sesión del usuario: el JWT y lo que trae dentro. Se guarda en localStorage para sobrevivir recargas.
/// El rol aquí solo decide qué se muestra; las reglas reales las aplica la API.
/// </summary>
public class Sesion(IJSRuntime js)
{
    private const string Clave = "portal-sesion";

    public string? Token { get; private set; }
    public int UsuarioId { get; private set; }
    public string Nombre { get; private set; } = "";
    public Rol Rol { get; private set; }
    public DateTimeOffset Expira { get; private set; }

    public bool Activa => Token is not null && Expira > DateTimeOffset.UtcNow;
    public bool Es(params Rol[] roles) => Activa && roles.Contains(Rol);

    public event Action? Cambio;

    public async Task CargarAsync()
    {
        var token = await js.InvokeAsync<string?>("portal.leer", Clave);
        if (token is not null && !Aplicar(token)) await CerrarAsync();
    }

    public async Task IniciarAsync(string token)
    {
        if (!Aplicar(token)) throw new InvalidOperationException("Token inválido.");
        await js.InvokeVoidAsync("portal.guardar", Clave, token);
        Cambio?.Invoke();
    }

    public async Task CerrarAsync()
    {
        Token = null;
        await js.InvokeVoidAsync("portal.guardar", Clave, null);
        Cambio?.Invoke();
    }

    // Lee el payload del JWT (sin validar firma: eso lo hace la API en cada llamada).
    private bool Aplicar(string token)
    {
        try
        {
            var partes = token.Split('.');
            var b64 = partes[1].Replace('-', '+').Replace('_', '/');
            b64 = b64.PadRight(b64.Length + (4 - b64.Length % 4) % 4, '=');
            using var doc = JsonDocument.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(b64)));
            var c = doc.RootElement;
            UsuarioId = int.Parse(c.GetProperty("sub").GetString()!);
            Nombre = c.GetProperty("name").GetString()!;
            Rol = Enum.Parse<Rol>(c.GetProperty("role").GetString()!);
            Expira = DateTimeOffset.FromUnixTimeSeconds(c.GetProperty("exp").GetInt64());
            Token = token;
            return Activa;
        }
        catch (Exception e) when (e is FormatException or JsonException or KeyNotFoundException or IndexOutOfRangeException or ArgumentException)
        {
            return false;
        }
    }
}
