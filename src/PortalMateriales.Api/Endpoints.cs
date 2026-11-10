using Microsoft.AspNetCore.Mvc;
using PortalMateriales.Application;
using PortalMateriales.Domain;

namespace PortalMateriales.Api;

public static class Endpoints
{
    public static void MapEndpoints(this WebApplication app)
    {
        app.MapPost("/api/auth/login", async (LoginRequest r, AuthService auth, Tokens tokens, CancellationToken ct) =>
        {
            var actor = await auth.LoginAsync(r, ct);
            if (actor is null) return Results.Problem(title: "Usuario o contraseña incorrectos", statusCode: 401);
            var (token, expira) = tokens.Emitir(actor);
            return Results.Ok(new { token, expira, usuario = actor.Nombre, rol = actor.Rol.ToString() });
        }).WithTags("Auth").AllowAnonymous().WithSummary("Inicia sesión y devuelve el JWT");

        // Las reglas de rol de cada transición las aplica el dominio (403 si no corresponde);
        // aquí solo se exige sesión, para no duplicarlas.
        var s = app.MapGroup("/api/solicitudes").WithTags("Solicitudes").RequireAuthorization();

        s.MapGet("/", (EstadoSolicitud? estado, HttpContext h, SolicitudesService svc, CancellationToken ct) =>
            svc.ListarAsync(estado, Tokens.Actor(h.User), ct))
            .WithSummary("Lista solicitudes, opcionalmente filtradas por estado. El proveedor solo ve las suyas.");

        s.MapGet("/{id:int}", (int id, HttpContext h, SolicitudesService svc, CancellationToken ct) =>
            svc.DetalleAsync(id, Tokens.Actor(h.User), ct))
            .WithSummary("Detalle con envíos, evaluaciones (fotos) e historial");

        s.MapPost("/", async (CrearSolicitudRequest r, HttpContext h, SolicitudesService svc, CancellationToken ct) =>
        {
            var id = await svc.CrearAsync(r, Tokens.Actor(h.User), ct);
            return Results.Created($"/api/solicitudes/{id}", new { id });
        }).WithSummary("Solicitante: crea una solicitud (estado Solicitado)");

        s.MapPost("/{id:int}/asignar", (int id, AsignarRequest r, HttpContext h, SolicitudesService svc, CancellationToken ct) =>
            Sin(svc.AsignarAsync(id, r, Tokens.Actor(h.User), ct)))
            .WithSummary("Coordinador: asigna a un proveedor (Solicitado → Asignado)");

        s.MapPost("/{id:int}/aceptar", (int id, AceptarRequest r, HttpContext h, SolicitudesService svc, CancellationToken ct) =>
            Sin(svc.AceptarAsync(id, r, Tokens.Actor(h.User), ct)))
            .WithSummary("Proveedor: acepta con fecha compromiso (Asignado → En progreso, o recompromiso tras un rechazo)");

        s.MapPost("/{id:int}/envio", (int id, EnvioRequest r, HttpContext h, SolicitudesService svc, CancellationToken ct) =>
            Sin(svc.RegistrarEnvioAsync(id, r, Tokens.Actor(h.User), ct)))
            .WithSummary("Proveedor: registra el envío (En progreso → Enviado)");

        s.MapPost("/{id:int}/evaluacion", (int id, [FromForm] EvaluacionForm f, HttpContext h, SolicitudesService svc, CancellationToken ct) =>
            Sin(svc.RegistrarEvaluacionAsync(id, f.Resultado, f.Comentarios,
                f.Fotos.Select(f => new ArchivoSubido(f.FileName, f.ContentType, f.Length, f.OpenReadStream)).ToList(),
                Tokens.Actor(h.User), ct)))
            .DisableAntiforgery() // API con JWT, sin cookies: no aplica CSRF
            .WithSummary($"Coordinador: registra resultado y fotos, 1 a {Solicitud.MaxFotosPorEvaluacion} JPEG/PNG/WebP de hasta 5 MB (Enviado → En evaluación)");

        s.MapPost("/{id:int}/aprobar", (int id, DecisionRequest r, HttpContext h, SolicitudesService svc, CancellationToken ct) =>
            Sin(svc.AprobarAsync(id, r, Tokens.Actor(h.User), ct)))
            .WithSummary("Solicitante que creó la solicitud: aprueba la muestra (En evaluación → Aprobado)");

        s.MapPost("/{id:int}/rechazar", (int id, DecisionRequest r, HttpContext h, SolicitudesService svc, CancellationToken ct) =>
            Sin(svc.RechazarAsync(id, r, Tokens.Actor(h.User), ct)))
            .WithSummary("Solicitante que creó la solicitud: rechaza con comentario (En evaluación → En progreso; el proveedor debe recomprometer fecha)");

        s.MapGet("/{id:int}/fotos/{fotoId:int}", async (int id, int fotoId, HttpContext h, SolicitudesService svc, CancellationToken ct) =>
        {
            var (contenido, tipo) = await svc.AbrirFotoAsync(id, fotoId, Tokens.Actor(h.User), ct);
            return Results.Stream(contenido, tipo);
        }).WithSummary("Descarga una foto de la evaluación (desde Blob Storage)");

        // Maestro de proveedores: lo consultan Solicitante y Coordinador; solo el Coordinador lo modifica.
        var lectura = new[] { nameof(Rol.Coordinador), nameof(Rol.Solicitante) };
        var pr = app.MapGroup("/api/proveedores").WithTags("Proveedores")
            .RequireAuthorization(p => p.RequireRole(lectura));
        var prEscritura = pr.MapGroup("").RequireAuthorization(p => p.RequireRole(nameof(Rol.Coordinador)));

        pr.MapGet("/", (bool? activo, ProveedoresService svc, CancellationToken ct) => svc.ListarAsync(activo, ct))
            .WithSummary("Lista el maestro de proveedores; filtro opcional por activo");

        pr.MapGet("/{id:int}", (int id, ProveedoresService svc, CancellationToken ct) => svc.DetalleAsync(id, ct))
            .WithSummary("Detalle del proveedor con sus contactos");

        prEscritura.MapPost("/", async (CrearProveedorRequest r, ProveedoresService svc, CancellationToken ct) =>
        {
            var d = await svc.CrearAsync(r, ct);
            return Results.Created($"/api/proveedores/{d.Proveedor.Id}", d);
        }).WithSummary("Coordinador: crea un proveedor (activo) con sus contactos. Moneda ISO 4217, idioma ISO 639-1, RTN obligatorio si es Nacional");

        prEscritura.MapPut("/{id:int}", (int id, ProveedorRequest r, ProveedoresService svc, CancellationToken ct) =>
            svc.ActualizarAsync(id, r, ct))
            .WithSummary("Coordinador: actualiza los datos generales del proveedor");

        prEscritura.MapPost("/{id:int}/activar", (int id, ProveedoresService svc, CancellationToken ct) =>
            Sin(svc.CambiarEstadoAsync(id, true, ct))).WithSummary("Coordinador: activa el proveedor");

        prEscritura.MapPost("/{id:int}/desactivar", (int id, ProveedoresService svc, CancellationToken ct) =>
            Sin(svc.CambiarEstadoAsync(id, false, ct)))
            .WithSummary("Coordinador: desactiva el proveedor (no recibe asignaciones nuevas; lo ya asignado sigue)");

        prEscritura.MapPost("/{id:int}/contactos", async (int id, ContactoRequest r, ProveedoresService svc, CancellationToken ct) =>
        {
            var c = await svc.AgregarContactoAsync(id, r, ct);
            return Results.Created($"/api/proveedores/{id}", c);
        }).WithSummary("Coordinador: agrega un contacto al proveedor");

        prEscritura.MapPut("/{id:int}/contactos/{contactoId:int}", (int id, int contactoId, ContactoRequest r, ProveedoresService svc, CancellationToken ct) =>
            Sin(svc.ActualizarContactoAsync(id, contactoId, r, ct))).WithSummary("Coordinador: actualiza un contacto");

        prEscritura.MapDelete("/{id:int}/contactos/{contactoId:int}", (int id, int contactoId, ProveedoresService svc, CancellationToken ct) =>
            Sin(svc.EliminarContactoAsync(id, contactoId, ct))).WithSummary("Coordinador: elimina un contacto");

        app.MapGet("/api/indicadores", (IConsultas q, TimeProvider reloj, CancellationToken ct) => q.IndicadoresAsync(reloj.Hoy(), ct))
            .WithTags("Indicadores")
            .WithSummary("Solicitudes por estado, vencidas y % de entregas a tiempo por proveedor")
            // Muestra datos de todos los proveedores: no se expone al rol Proveedor.
            .RequireAuthorization(p => p.RequireRole(nameof(Rol.Coordinador), nameof(Rol.Solicitante)));
    }

    /// <summary>Formulario multipart de la evaluación: un solo objeto para que Swagger muestre los campos y el selector de archivos.</summary>
    public record EvaluacionForm(ResultadoEvaluacion Resultado, string Comentarios, IFormFileCollection Fotos);

    private static async Task<IResult> Sin(Task t) { await t; return Results.NoContent(); }
}
