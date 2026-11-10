using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using PortalMateriales.Api;
using PortalMateriales.Application;
using PortalMateriales.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

var jwtClave = builder.Configuration["Jwt:Clave"];
if (string.IsNullOrEmpty(jwtClave) || jwtClave.Length < 32)
    throw new InvalidOperationException("Jwt:Clave es obligatoria y debe tener al menos 32 caracteres.");

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<SolicitudesService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<ProveedoresService>();
builder.Services.AddSingleton(new Tokens(jwtClave));

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.MapInboundClaims = false; // conserva "sub" y "role" tal cual vienen en el token
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtClave)),
            RoleClaimType = "role",
            NameClaimType = "name",
        };
    });
builder.Services.AddAuthorization();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ManejadorErrores>();
builder.Services.ConfigureHttpJsonOptions(o =>
    o.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));

builder.Services.AddOpenApi(o => o.AddDocumentTransformer((doc, _, _) =>
{
    doc.Info.Title = "Portal de Desarrollo de Materiales";
    doc.Info.Description = """
        Seguimiento de solicitudes de desarrollo de materiales. Interfaz web en [/](/).

        **Cómo probar:** ejecute `POST /api/auth/login` con un usuario de ejemplo, copie el `token`
        y péguelo en **Authorize** (arriba a la derecha).

        | Usuario | Rol |
        |---|---|
        | ana, luis | Solicitante |
        | carla | Coordinador |
        | valle, mallas, etiquetas, hilos | Proveedor |

        Contraseña de todos: `Demo123!` (datos ficticios).
        """;
    doc.Servers = []; // URLs relativas: detrás del proxy HTTPS de la nube no queda "http://" en Swagger
    doc.Components ??= new OpenApiComponents();
    doc.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
    doc.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT",
    };
    doc.Security = [new OpenApiSecurityRequirement { [new OpenApiSecuritySchemeReference("Bearer", doc)] = [] }];
    return Task.CompletedTask;
}));

var app = builder.Build();

await app.Services.InicializarAsync();

app.UseExceptionHandler();
app.UseAuthentication();
app.UseAuthorization();

// Swagger habilitado también en producción: es la interfaz de la aplicación.
app.MapOpenApi();
app.UseSwaggerUI(o =>
{
    o.SwaggerEndpoint("/openapi/v1.json", "Portal Materiales");
    o.EnablePersistAuthorization();
});
app.MapGet("/salud", () => Results.Ok("ok")).ExcludeFromDescription();

app.MapEndpoints();

// Interfaz web (Blazor WebAssembly) servida por la misma API: misma URL, sin CORS.
app.MapStaticAssets();
app.MapFallback("/api/{**resto}", () => Results.NotFound()).ExcludeFromDescription(); // una ruta de API inexistente no devuelve la página
app.MapFallbackToFile("index.html");

app.Run();

public partial class Program;
