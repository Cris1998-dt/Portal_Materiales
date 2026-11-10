# Portal de Desarrollo de Materiales

API para dar seguimiento a las solicitudes de desarrollo de materiales (telas, mallas, etiquetas, hilos)
desde que se piden hasta que se aprueba o rechaza la muestra.

- **Backend:** .NET 10 (ASP.NET Core Minimal APIs), expuesto como API REST.
- **Base de datos:** SQL Server (EF Core 10, migraciones).
- **Fotos:** Azure Blob Storage (Azurite en local).
- **Interfaz web:** Blazor WebAssembly (.NET 10), responsiva para celular, tablet y escritorio, con tema claro/oscuro.
- **Swagger:** la API completa también se puede usar desde `/swagger`.
- **Aplicación desplegada:** <https://portal-materiales-7afae9.azurewebsites.net> (interfaz) ·
  <https://portal-materiales-7afae9.azurewebsites.net/swagger> (Swagger)

## Probarlo en 1 minuto

**Interfaz web:** abra la URL y toque uno de los usuarios de ejemplo en la pantalla de inicio de sesión.
Cada rol ve solo sus opciones y, en cada solicitud, solo los botones de las acciones que puede hacer en ese momento.

**Swagger:**

1. Abra `/swagger`.
2. `POST /api/auth/login` → **Try it out** con `{"usuario":"carla","password":"Demo123!"}`.
3. Copie el `token` de la respuesta, pulse **Authorize** y péguelo.
4. Ya puede usar el resto de endpoints con el rol de ese usuario.

| Usuario | Rol | Notas |
|---|---|---|
| `ana`, `luis` | Solicitante | Cada uno decide solo sobre sus solicitudes |
| `carla` | Coordinador | |
| `valle`, `mallas`, `etiquetas`, `hilos` | Proveedor | Uno por proveedor; solo ven lo suyo |

Contraseña de todos: `Demo123!` (datos ficticios).

## Flujo

```
Solicitado ──asignar──▶ Asignado ──aceptar──▶ En progreso ──envio──▶ Enviado ──evaluacion──▶ En evaluación ──aprobar──▶ Aprobado
 (Solicitante)          (Coordinador)        (Proveedor)            (Proveedor)            (Coordinador)            (Solicitante)
                                                  ▲                                                     │
                                                  └───────────────────────── rechazar ──────────────────┘
```

| Endpoint | Rol | Transición |
|---|---|---|
| `POST /api/solicitudes` | Solicitante | crea en **Solicitado** |
| `POST /api/solicitudes/{id}/asignar` | Coordinador | Solicitado → **Asignado** |
| `POST /api/solicitudes/{id}/aceptar` | Proveedor asignado | Asignado → **En progreso** (con fecha compromiso) |
| `POST /api/solicitudes/{id}/envio` | Proveedor asignado | En progreso → **Enviado** |
| `POST /api/solicitudes/{id}/evaluacion` | Coordinador | Enviado → **En evaluación** (multipart: resultado, comentarios, 1–5 fotos) |
| `POST /api/solicitudes/{id}/aprobar` | Solicitante que creó | En evaluación → **Aprobado** |
| `POST /api/solicitudes/{id}/rechazar` | Solicitante que creó | En evaluación → **En progreso** (comentario obligatorio) |
| `GET /api/solicitudes?estado=` | Todos | listado con filtro; el proveedor solo ve las suyas |
| `GET /api/solicitudes/{id}` | Todos | detalle con envíos, evaluaciones, fotos e historial |
| `GET /api/solicitudes/{id}/fotos/{fotoId}` | Todos | descarga la foto desde Blob Storage |
| `GET /api/indicadores` | Solicitante, Coordinador | por estado, vencidas y % a tiempo por proveedor |

### Maestro de proveedores

| Endpoint | Rol | Qué hace |
|---|---|---|
| `GET /api/proveedores?activo=` | Solicitante, Coordinador | lista, con filtro opcional por estado |
| `GET /api/proveedores/{id}` | Solicitante, Coordinador | detalle con contactos |
| `POST /api/proveedores` | Coordinador | crea (activo), con contactos opcionales |
| `PUT /api/proveedores/{id}` | Coordinador | actualiza datos generales |
| `POST /api/proveedores/{id}/activar` · `/desactivar` | Coordinador | cambia el estado |
| `POST /api/proveedores/{id}/contactos` | Coordinador | agrega contacto |
| `PUT` · `DELETE /api/proveedores/{id}/contactos/{contactoId}` | Coordinador | edita o elimina contacto |

Datos del proveedor: nombre, **tipo** (Nacional / Internacional), **RTN** (14 dígitos; obligatorio si es nacional,
opcional si es internacional, único), **moneda** (código ISO 4217: `HNL`, `USD`, `GTQ`…), **idioma**
(código ISO 639-1: `es`, `en`, `zh`…), correo, dirección y estado **activo/inactivo**.
Cada proveedor tiene sus **contactos** (tabla `ContactosProveedor`): nombre, puesto, teléfono y correo,
sin correos repetidos dentro del mismo proveedor.

Errores como `ProblemDetails`: 400 dato inválido, 401 sin sesión, 403 rol incorrecto,
404 no existe (o no es visible para ese proveedor), 409 transición no válida en el estado actual.

## Correr en local

Requisitos: [.NET SDK 10](https://dotnet.microsoft.com/download/dotnet/10.0) y Docker.

```bash
git clone <repo> && cd PortalMateriales
docker compose up -d                       # SQL Server (puerto 14334) + Azurite (10000)
dotnet run --project src/PortalMateriales.Api --launch-profile http
```

Abra <http://localhost:5047> (interfaz) o <http://localhost:5047/swagger>. Al arrancar, la API aplica las migraciones, crea el contenedor de fotos
y carga los datos de ejemplo si la base está vacía (no hace falta ningún paso manual).
SQL Server tarda unos 20 s en aceptar conexiones la primera vez; la API reintenta sola.

Pruebas:

```bash
dotnet test                                # pruebas unitarias del dominio (máquina de estados)
scripts/prueba-flujo.sh                    # prueba de punta a punta contra la API en marcha (49 verificaciones)
scripts/prueba-flujo.sh https://<url>      # la misma contra la versión desplegada
```

Para empezar de cero: `docker compose down -v`.

### Configuración

| Clave | Local (`appsettings.Development.json`) | Nube (variables de entorno) |
|---|---|---|
| `ConnectionStrings:Sql` | SQL Server del compose | `ConnectionStrings__Sql` |
| `ConnectionStrings:Blob` | `UseDevelopmentStorage=true` (Azurite) | `ConnectionStrings__Blob` |
| `Jwt:Clave` | clave de desarrollo | `Jwt__Clave` (≥ 32 caracteres, secreta) |
| `Almacenamiento:Contenedor` | `fotos-muestras` | opcional |

## Despliegue en Azure (capas gratuitas)

| Recurso | Servicio | Plan |
|---|---|---|
| API | App Service Linux, runtime `DOTNETCORE:10.0` | F1 (gratis) |
| Base de datos | Azure SQL Database serverless | oferta gratuita (32 GB, 100 000 vCore-s/mes, se pausa al agotarse) |
| Fotos | Storage Account, Blob privado `fotos-muestras` | Standard LRS |

Todo en `westus3`, grupo `rg-portal-materiales`. El comando de inicio está fijado en
`dotnet PortalMateriales.Api.dll`: la publicación incluye también el `runtimeconfig` de la interfaz Blazor y,
sin ese comando, App Service no sabe cuál ejecutar. La configuración secreta va en *App settings*
(`ConnectionStrings__Sql`, `ConnectionStrings__Blob`, `Jwt__Clave`), no en el repositorio.

> **Primera petición lenta:** el plan F1 se duerme y la base gratuita se pausa sin uso.
> La primera llamada puede tardar hasta ~1 minuto; la API reintenta la conexión sola.

Publicar una versión nueva:

```bash
dotnet publish src/PortalMateriales.Api -c Release -o publicar
rm publicar/appsettings.Development.json && (cd publicar && zip -qr ../app.zip .)
az webapp deploy -g rg-portal-materiales -n <app> --src-path app.zip --type zip
```

`dotnet publish` del proyecto Api incluye la interfaz web compilada. Al arrancar, la app aplica migraciones pendientes y carga datos de ejemplo solo si la base está vacía.
El firewall de SQL permite solo servicios de Azure (el plan F1 no admite red privada).

## Arquitectura

Clean Architecture; las dependencias apuntan hacia adentro:

```
Api ──▶ Infrastructure ──▶ Application ──▶ Domain
 └──▶ Web (Blazor WASM) ──▶ Application (solo DTOs y enums)
```

| Proyecto | Contiene | Depende de |
|---|---|---|
| `Domain` | Agregados `Solicitud` (con `Envio`, `Evaluacion`, `FotoMuestra`, `HistorialCambio`) y `Proveedor` (con `ContactoProveedor`), `Usuario`, `Actor`, excepciones de dominio | nada |
| `Application` | Casos de uso (`SolicitudesService`, `ProveedoresService`, `AuthService`), DTOs y puertos (`ISolicitudRepository`, `IProveedorRepository`, `IConsultas`, `IAlmacenArchivos`, `IPasswordHasher`) | Domain |
| `Infrastructure` | EF Core + SQL Server, migraciones, Blob Storage, hash de contraseñas, datos de ejemplo | Application |
| `Api` | Endpoints, JWT, Swagger, traducción de errores a HTTP; sirve la interfaz web | Infrastructure, Web |
| `Web` | Interfaz Blazor WebAssembly: páginas, componentes y cliente de la API | Application (contratos) |

### Decisiones

- **Las reglas del flujo viven en el agregado.** Cada transición es un método de `Solicitud`
  (`Asignar`, `Aceptar`, `RegistrarEnvio`, `RegistrarEvaluacion`, `Aprobar`, `Rechazar`) que valida estado,
  rol y propiedad, y escribe el historial en el mismo paso. No hay forma de cambiar el estado sin dejar rastro.
  Por eso la API solo exige sesión: no repite las reglas de rol.
- **El dominio recibe un `Actor`** (usuario, rol, proveedor) en lugar de leer la sesión: queda puro y se prueba sin infraestructura.
- **Rechazo:** regresa a *En progreso* y anula el compromiso; el proveedor vuelve a `aceptar` con una fecha nueva
  antes de reenviar. Así "vencida" siempre se mide contra un compromiso que el proveedor sí asumió.
- **Vencida** = *En progreso* con fecha compromiso pasada. Se calcula, no se guarda: una sola expresión
  (`Solicitud.EsVencida`) se usa en memoria y EF la traduce a SQL.
- **% a tiempo por proveedor** = envíos con fecha de envío ≤ compromiso vigente al enviar, sobre todos sus envíos
  (cada reenvío tras un rechazo cuenta). El envío guarda copia del compromiso para que el indicador no cambie después.
- **Fotos:** contenedor privado en Blob Storage; la base guarda solo la referencia. La API las sirve tras validar
  que el usuario puede ver la solicitud. Se validan antes de subir (estado, rol, 1–5 fotos, JPEG/PNG/WebP ≤ 5 MB);
  si algo falla después, se borran las ya subidas.
- **Visibilidad del proveedor:** una solicitud ajena devuelve 404, no 403, para no revelar que existe.
- **Lecturas separadas de escrituras:** las escrituras cargan el agregado por `ISolicitudRepository`;
  listados e indicadores usan `IConsultas` con proyecciones (sin cargar agregados completos).
- **Fechas en hora de Honduras** (`America/Tegucigalpa`), para que "hoy" y "vencida" no dependan de la zona del servidor.
- **Datos de ejemplo reproduciendo el flujo:** se crean llamando a los mismos métodos del dominio con fechas pasadas,
  así cumplen las reglas y tienen historial real. Las fechas son relativas al día de carga.
- **Sin MediatR, AutoMapper ni repositorio genérico:** con un solo agregado no aportan; servicios de aplicación simples.
- **Proveedor como agregado propio:** sus contactos solo se modifican a través de él. Moneda e idioma son propios
  de cada proveedor y no se derivan del tipo (un proveedor nacional puede facturar en USD). Los códigos ISO se
  validan contra la librería estándar de .NET, sin tablas de catálogo que mantener.
- **Inactivar no borra:** un proveedor inactivo no recibe asignaciones nuevas, pero lo ya asignado sigue su curso
  y su historial e indicadores se conservan. El mantenimiento del maestro lo hace el Coordinador (regla de rol en la API,
  porque no es parte del flujo de la solicitud).
- **Interfaz en Blazor WebAssembly servida por la misma API:** todo el sistema en .NET 10, una sola URL y un solo
  despliegue, sin CORS. La interfaz reutiliza los DTOs y enums de `Application`, así que un cambio de contrato
  rompe la compilación en lugar de fallar en producción.
- **El servidor dice qué botones mostrar:** el detalle de una solicitud trae `acciones`, calculadas por
  `Solicitud.AccionesPara(actor)` con los mismos predicados que validan cada transición. Una prueba recorre
  todos los estados × actores × acciones (210 casos) y verifica que lo anunciado coincide con lo permitido.
  La interfaz no repite reglas de negocio.
- **Responsiva sin librerías de UI:** CSS propio con tokens de color (claro/oscuro con contraste AA). En celular
  las tablas pasan a tarjetas, los diálogos se abren desde abajo y los campos usan 16 px (iOS no hace zoom).
  Estados con ícono + texto + color, foco visible, `aria-*` en diálogos y avisos.
- **Contraseñas** con PBKDF2-SHA256 de la librería estándar; **sesión** con JWT de 8 h.
- **Enums como texto en SQL**: la base se lee sin tablas de códigos.

## Qué quedó fuera y por qué

- **Reasignar proveedor o cancelar una solicitud:** no está en el flujo pedido.
- **Gestión de usuarios:** se cargan como datos de ejemplo; no hay altas por API (los proveedores sí tienen mantenimiento).
- **Paginación del listado:** con el volumen esperado no hace falta; se agrega con `skip/take` si crece.
- **Notificaciones (correo) y refresh tokens:** fuera de alcance.
- **Pruebas automáticas de la interfaz en el repositorio:** la interfaz se probó de punta a punta en WebKit (motor de Safari)
  en tamaños de iPhone, iPad y escritorio, pero ese script depende de Playwright/Node y no se incluyó para no sumar otro
  entorno de ejecución.
- **Pruebas de integración contra SQL Server con WebApplicationFactory:** cubiertas por `scripts/prueba-flujo.sh`
  contra la API real; las unitarias cubren la máquina de estados.
