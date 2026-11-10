namespace PortalMateriales.Domain;

public enum EstadoSolicitud { Solicitado, Asignado, EnProgreso, Enviado, EnEvaluacion, Aprobado }

public enum TipoMaterial { Tela, Malla, Etiqueta, Hilo, Otro }

public enum Prioridad { Baja, Media, Alta }

public enum Rol { Solicitante, Coordinador, Proveedor }

public enum ResultadoEvaluacion { Cumple, NoCumple }

public enum AccionSolicitud { Asignar, Aceptar, RegistrarEnvio, Evaluar, Aprobar, Rechazar }
