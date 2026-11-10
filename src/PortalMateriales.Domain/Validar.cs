using System.Globalization;
using System.Net.Mail;
using System.Text.RegularExpressions;

namespace PortalMateriales.Domain;

/// <summary>Validaciones de formato compartidas por las entidades del dominio.</summary>
internal static partial class Validar
{
    // Catálogos ISO tomados de la librería estándar: no hay tabla que mantener.
    private static readonly HashSet<string> Monedas = CultureInfo.GetCultures(CultureTypes.SpecificCultures)
        .Select(c => { try { return new RegionInfo(c.Name).ISOCurrencySymbol; } catch (ArgumentException) { return null; } })
        .OfType<string>().ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> Idiomas = CultureInfo.GetCultures(CultureTypes.NeutralCultures)
        .Select(c => c.TwoLetterISOLanguageName).Where(c => c.Length == 2).ToHashSet(StringComparer.OrdinalIgnoreCase);

    public static string Texto(string? valor, string campo, int max)
    {
        var v = valor?.Trim();
        if (string.IsNullOrEmpty(v)) throw new ValidacionException($"{campo} es obligatorio.");
        if (v.Length > max) throw new ValidacionException($"{campo} admite máximo {max} caracteres.");
        return v;
    }

    public static string Correo(string? valor, string campo)
    {
        var v = Texto(valor, campo, 150);
        return MailAddress.TryCreate(v, out var m) && m.Address == v
            ? v.ToLowerInvariant()
            : throw new ValidacionException($"{campo} no es un correo válido.");
    }

    public static string Telefono(string? valor)
    {
        var v = Texto(valor, "Teléfono", 30);
        return TelefonoRegex().IsMatch(v) ? v : throw new ValidacionException("Teléfono solo admite dígitos, espacios, +, - y paréntesis (7 a 30 caracteres).");
    }

    public static string Moneda(string? valor)
    {
        var v = Texto(valor, "Moneda", 3).ToUpperInvariant();
        return Monedas.Contains(v) ? v : throw new ValidacionException($"Moneda '{v}' no es un código ISO 4217 válido (ej. HNL, USD, EUR).");
    }

    public static string Idioma(string? valor)
    {
        var v = Texto(valor, "Idioma", 2).ToLowerInvariant();
        return Idiomas.Contains(v) ? v : throw new ValidacionException($"Idioma '{v}' no es un código ISO 639-1 válido (ej. es, en, zh).");
    }

    /// <summary>RTN hondureño: 14 dígitos. Acepta guiones o espacios y los quita.</summary>
    public static string? Rtn(string? valor, bool obligatorio)
    {
        var v = new string((valor ?? "").Where(c => !char.IsWhiteSpace(c) && c != '-').ToArray());
        if (v.Length == 0)
            return obligatorio ? throw new ValidacionException("El RTN es obligatorio para proveedores nacionales.") : null;
        return v.Length == 14 && v.All(char.IsAsciiDigit) ? v : throw new ValidacionException("El RTN debe tener 14 dígitos.");
    }

    [GeneratedRegex(@"^\+?[0-9 ()\-]{7,30}$")]
    private static partial Regex TelefonoRegex();
}
