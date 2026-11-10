using Microsoft.AspNetCore.Diagnostics;
using PortalMateriales.Application;
using PortalMateriales.Domain;

namespace PortalMateriales.Api;

/// <summary>Traduce las excepciones de dominio/aplicación a ProblemDetails con el código HTTP adecuado.</summary>
public class ManejadorErrores(IProblemDetailsService problemas) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext ctx, Exception ex, CancellationToken ct)
    {
        (int status, string titulo)? r = ex switch
        {
            ValidacionException => (400, "Datos inválidos"),
            BadHttpRequestException => (400, "Solicitud mal formada"),
            AccesoDenegadoException => (403, "Acción no permitida para su rol"),
            NoEncontradoException => (404, "No encontrado"),
            TransicionInvalidaException => (409, "Transición no válida"),
            _ => null,
        };
        if (r is null) return false; // 500 genérico, sin exponer detalles

        ctx.Response.StatusCode = r.Value.status;
        return await problemas.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = ctx,
            ProblemDetails = { Status = r.Value.status, Title = r.Value.titulo, Detail = ex.Message },
        });
    }
}
