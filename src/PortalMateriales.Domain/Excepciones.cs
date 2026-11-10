namespace PortalMateriales.Domain;

/// <summary>Dato inválido (campo vacío, fecha fuera de rango...).</summary>
public class ValidacionException(string mensaje) : Exception(mensaje);

/// <summary>La acción no es válida en el estado actual de la solicitud.</summary>
public class TransicionInvalidaException(string mensaje) : Exception(mensaje);

/// <summary>El usuario no tiene el rol o no es el dueño de la solicitud.</summary>
public class AccesoDenegadoException(string mensaje) : Exception(mensaje);
