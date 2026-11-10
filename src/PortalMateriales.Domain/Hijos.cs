namespace PortalMateriales.Domain;

// Entidades internas del agregado Solicitud: solo se crean desde sus métodos.

public class Envio
{
    public int Id { get; private set; }
    public DateOnly FechaEnvio { get; private set; }
    public string Transportista { get; private set; } = "";
    public string NumeroGuia { get; private set; } = "";
    /// <summary>Copia del compromiso vigente al enviar: el indicador "a tiempo" no cambia si luego se recompromete.</summary>
    public DateOnly FechaCompromiso { get; private set; }
    public bool ATiempo => FechaEnvio <= FechaCompromiso;

    private Envio() { }
    internal Envio(DateOnly fechaEnvio, string transportista, string numeroGuia, DateOnly fechaCompromiso)
    {
        FechaEnvio = fechaEnvio; Transportista = transportista; NumeroGuia = numeroGuia; FechaCompromiso = fechaCompromiso;
    }
}

public class Evaluacion
{
    public int Id { get; private set; }
    public ResultadoEvaluacion Resultado { get; private set; }
    public string Comentarios { get; private set; } = "";
    public int CoordinadorId { get; private set; }
    public DateTime RegistradaEn { get; private set; }
    private readonly List<FotoMuestra> _fotos = [];
    public IReadOnlyList<FotoMuestra> Fotos => _fotos;

    private Evaluacion() { }
    internal Evaluacion(ResultadoEvaluacion resultado, string comentarios, int coordinadorId, DateTime registradaEn, IEnumerable<FotoMuestra> fotos)
    {
        Resultado = resultado; Comentarios = comentarios; CoordinadorId = coordinadorId; RegistradaEn = registradaEn;
        _fotos.AddRange(fotos);
    }
}

/// <summary>Referencia a una foto guardada en el almacenamiento de archivos (no guarda los bytes).</summary>
public class FotoMuestra
{
    public int Id { get; private set; }
    public string NombreArchivo { get; private set; } = "";
    public string ContentType { get; private set; } = "";
    public string NombreOriginal { get; private set; } = "";

    private FotoMuestra() { }
    public FotoMuestra(string nombreArchivo, string contentType, string nombreOriginal)
    {
        NombreArchivo = nombreArchivo; ContentType = contentType; NombreOriginal = nombreOriginal;
    }
}

public class HistorialCambio
{
    public int Id { get; private set; }
    public DateTime Fecha { get; private set; }
    public int UsuarioId { get; private set; }
    public string UsuarioNombre { get; private set; } = "";
    public string Accion { get; private set; } = "";
    public EstadoSolicitud? EstadoAnterior { get; private set; }
    public EstadoSolicitud EstadoNuevo { get; private set; }
    public string? Detalle { get; private set; }

    private HistorialCambio() { }
    internal HistorialCambio(DateTime fecha, int usuarioId, string usuarioNombre, string accion,
        EstadoSolicitud? anterior, EstadoSolicitud nuevo, string? detalle)
    {
        Fecha = fecha; UsuarioId = usuarioId; UsuarioNombre = usuarioNombre; Accion = accion;
        EstadoAnterior = anterior; EstadoNuevo = nuevo; Detalle = detalle;
    }
}
