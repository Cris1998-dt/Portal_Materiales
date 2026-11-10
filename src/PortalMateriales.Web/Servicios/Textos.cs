using System.Globalization;
using PortalMateriales.Domain;

namespace PortalMateriales.Web.Servicios;

/// <summary>Etiquetas en español y formatos de fecha (independientes de la cultura del navegador).</summary>
public static class Textos
{
    public static string De(EstadoSolicitud e) => e switch
    {
        EstadoSolicitud.EnProgreso => "En progreso",
        EstadoSolicitud.EnEvaluacion => "En evaluación",
        _ => e.ToString(),
    };

    public static string De(ResultadoEvaluacion r) => r == ResultadoEvaluacion.NoCumple ? "No cumple" : "Cumple";

    public static string De(AccionSolicitud a) => a switch
    {
        AccionSolicitud.Asignar => "Asignar proveedor",
        AccionSolicitud.Aceptar => "Aceptar y comprometer fecha",
        AccionSolicitud.RegistrarEnvio => "Registrar envío",
        AccionSolicitud.Evaluar => "Registrar evaluación",
        AccionSolicitud.Aprobar => "Aprobar muestra",
        AccionSolicitud.Rechazar => "Rechazar muestra",
        _ => a.ToString(),
    };

    public static string Fecha(DateOnly d) => d.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
    public static string Fecha(DateOnly? d) => d is { } v ? Fecha(v) : "—";
    public static string FechaHora(DateTime d) => d.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);

    public static readonly (string Codigo, string Nombre)[] Monedas =
        [("HNL", "Lempira"), ("USD", "Dólar estadounidense"), ("EUR", "Euro"), ("GTQ", "Quetzal"), ("MXN", "Peso mexicano"), ("CNY", "Yuan")];

    public static readonly (string Codigo, string Nombre)[] Idiomas =
        [("es", "Español"), ("en", "Inglés"), ("pt", "Portugués"), ("zh", "Chino"), ("fr", "Francés")];

    public static string Idioma(string codigo) => Idiomas.FirstOrDefault(i => i.Codigo == codigo).Nombre ?? codigo;
}
