using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Components;

namespace PortalMateriales.Web.Servicios;

/// <summary>Error de la API ya traducido a un mensaje para el usuario (viene de ProblemDetails).</summary>
public class ApiException(string mensaje, HttpStatusCode estado) : Exception(mensaje)
{
    public HttpStatusCode Estado { get; } = estado;
}

/// <summary>Cliente de la API: agrega el token, convierte errores en <see cref="ApiException"/> y saca al login si la sesión expiró.</summary>
public class Api(HttpClient http, Sesion sesion, NavigationManager nav)
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    public async Task<T> GetAsync<T>(string url) =>
        (await (await EnviarAsync(new HttpRequestMessage(HttpMethod.Get, url))).Content.ReadFromJsonAsync<T>(Json))!;

    public async Task<T> PostAsync<T>(string url, object? cuerpo) =>
        (await (await EnviarAsync(Con(HttpMethod.Post, url, cuerpo))).Content.ReadFromJsonAsync<T>(Json))!;

    public async Task PostAsync(string url, object? cuerpo = null) => await EnviarAsync(Con(HttpMethod.Post, url, cuerpo));
    public async Task PutAsync(string url, object cuerpo) => await EnviarAsync(Con(HttpMethod.Put, url, cuerpo));
    public async Task DeleteAsync(string url) => await EnviarAsync(new HttpRequestMessage(HttpMethod.Delete, url));

    public async Task PostFormAsync(string url, MultipartFormDataContent form) =>
        await EnviarAsync(new HttpRequestMessage(HttpMethod.Post, url) { Content = form });

    /// <summary>Descarga una imagen protegida y la devuelve como data URL para usarla en &lt;img&gt;.</summary>
    public async Task<string> ImagenAsync(string url)
    {
        var r = await EnviarAsync(new HttpRequestMessage(HttpMethod.Get, url));
        var tipo = r.Content.Headers.ContentType?.MediaType ?? "image/png";
        return $"data:{tipo};base64,{Convert.ToBase64String(await r.Content.ReadAsByteArrayAsync())}";
    }

    private static HttpRequestMessage Con(HttpMethod m, string url, object? cuerpo) =>
        new(m, url) { Content = cuerpo is null ? null : JsonContent.Create(cuerpo, cuerpo.GetType(), options: Json) };

    private async Task<HttpResponseMessage> EnviarAsync(HttpRequestMessage req)
    {
        if (sesion.Token is not null) req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", sesion.Token);
        HttpResponseMessage r;
        try { r = await http.SendAsync(req); }
        catch (HttpRequestException) { throw new ApiException("No se pudo conectar con el servidor. Revise su conexión e intente de nuevo.", 0); }

        if (r.IsSuccessStatusCode) return r;
        if (r.StatusCode == HttpStatusCode.Unauthorized && sesion.Activa)
        {
            await sesion.CerrarAsync();
            nav.NavigateTo("login");
        }
        string? mensaje = null;
        try
        {
            var p = await r.Content.ReadFromJsonAsync<JsonElement>(Json);
            mensaje = p.TryGetProperty("detail", out var d) && d.ValueKind == JsonValueKind.String ? d.GetString()
                : p.TryGetProperty("title", out var t) ? t.GetString() : null;
        }
        catch (JsonException) { }
        throw new ApiException(mensaje ?? $"Error {(int)r.StatusCode} del servidor.", r.StatusCode);
    }
}
