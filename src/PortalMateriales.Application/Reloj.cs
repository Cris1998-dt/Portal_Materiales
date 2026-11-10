namespace PortalMateriales.Application;

public static class Reloj
{
    // ponytail: zona fija del negocio; pasarla a configuración si hay plantas en otros husos.
    private static readonly TimeZoneInfo Zona = TimeZoneInfo.FindSystemTimeZoneById("America/Tegucigalpa");

    public static DateTime AhoraLocal(this TimeProvider t) => TimeZoneInfo.ConvertTime(t.GetUtcNow(), Zona).DateTime;
    public static DateOnly Hoy(this TimeProvider t) => DateOnly.FromDateTime(t.AhoraLocal());
}
