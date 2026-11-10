using PortalMateriales.Domain;

namespace PortalMateriales.Application;

public record CrearSolicitudRequest(string Material, TipoMaterial Tipo, string Descripcion, DateOnly FechaObjetivo, Prioridad Prioridad);
public record AsignarRequest(int ProveedorId);
public record AceptarRequest(DateOnly FechaCompromiso);
public record EnvioRequest(DateOnly FechaEnvio, string Transportista, string NumeroGuia);
public record DecisionRequest(string? Comentario);

/// <summary>Archivo subido, desacoplado de IFormFile para que Application no dependa de ASP.NET.</summary>
public record ArchivoSubido(string NombreOriginal, string ContentType, long Tamano, Func<Stream> Abrir);

public record SolicitudResumenDto(int Id, string Material, TipoMaterial Tipo, Prioridad Prioridad, EstadoSolicitud Estado,
    DateOnly FechaObjetivo, string? Proveedor, DateOnly? FechaCompromiso, bool Vencida);

public record SolicitudDetalleDto(int Id, string Material, TipoMaterial Tipo, string Descripcion, Prioridad Prioridad,
    EstadoSolicitud Estado, bool Vencida, DateOnly FechaObjetivo, DateTime CreadaEn, string Solicitante,
    ProveedorDto? Proveedor, DateOnly? FechaCompromiso,
    IReadOnlyList<EnvioDto> Envios, IReadOnlyList<EvaluacionDto> Evaluaciones, IReadOnlyList<HistorialDto> Historial,
    IReadOnlyList<AccionSolicitud> Acciones);

public record ProveedorDto(int Id, string Nombre, TipoProveedor Tipo, string? Rtn, string Moneda, string Idioma,
    string Correo, string Direccion, bool Activo)
{
    public static ProveedorDto De(Proveedor p) => new(p.Id, p.Nombre, p.Tipo, p.Rtn, p.Moneda, p.Idioma, p.Correo, p.Direccion, p.Activo);
}

public record ProveedorDetalleDto(ProveedorDto Proveedor, IReadOnlyList<ContactoDto> Contactos);
public record ContactoDto(int Id, string Nombre, string Puesto, string Telefono, string Correo);

/// <summary>Moneda: ISO 4217 (HNL, USD). Idioma: ISO 639-1 (es, en). RTN: 14 dígitos, obligatorio si es Nacional.</summary>
public record ProveedorRequest(string Nombre, TipoProveedor Tipo, string? Rtn, string Moneda, string Idioma, string Correo, string Direccion);
public record CrearProveedorRequest(string Nombre, TipoProveedor Tipo, string? Rtn, string Moneda, string Idioma, string Correo, string Direccion,
    IReadOnlyList<ContactoRequest>? Contactos);
public record ContactoRequest(string Nombre, string Puesto, string Telefono, string Correo);
public record EnvioDto(DateOnly FechaEnvio, string Transportista, string NumeroGuia, DateOnly FechaCompromiso, bool ATiempo);
public record EvaluacionDto(ResultadoEvaluacion Resultado, string Comentarios, string Coordinador, DateTime RegistradaEn, IReadOnlyList<FotoDto> Fotos);
public record FotoDto(int Id, string NombreOriginal, string Url);
public record HistorialDto(DateTime Fecha, string Usuario, string Accion, EstadoSolicitud? EstadoAnterior, EstadoSolicitud EstadoNuevo, string? Detalle);

public record IndicadoresDto(
    IReadOnlyDictionary<EstadoSolicitud, int> PorEstado,
    int Vencidas,
    IReadOnlyList<SolicitudResumenDto> DetalleVencidas,
    IReadOnlyList<CumplimientoProveedorDto> EntregasATiempo);

/// <summary>Porcentaje es null si el proveedor aún no tiene envíos.</summary>
public record CumplimientoProveedorDto(int ProveedorId, string Proveedor, int Envios, int ATiempo, decimal? Porcentaje);

public record LoginRequest(string Usuario, string Password);
