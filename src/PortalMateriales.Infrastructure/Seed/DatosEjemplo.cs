using Microsoft.EntityFrameworkCore;
using PortalMateriales.Application;
using PortalMateriales.Domain;

namespace PortalMateriales.Infrastructure;

/// <summary>
/// Datos ficticios. Las solicitudes se construyen reproduciendo el flujo real con fechas pasadas,
/// así pasan por las mismas reglas del dominio y el historial queda completo y coherente.
/// Las fechas son relativas al día de la carga para que siempre haya vencidas y pendientes.
/// </summary>
public class DatosEjemplo(AppDbContext db, IPasswordHasher hasher, IAlmacenArchivos archivos, TimeProvider reloj)
{
    public const string PasswordDemo = "Demo123!";

    public async Task CargarAsync()
    {
        if (await db.Usuarios.AnyAsync()) return;

        Proveedor valle = new("Textiles del Valle", TipoProveedor.Nacional, "0501-1999-123456", "HNL", "es",
                      "ventas@textilesvalle.example", "Bo. Guamilito, 3 calle 5 ave. NO, San Pedro Sula, Cortés"),
                  mallas = new("Mallas Centroamericanas", TipoProveedor.Nacional, "0502-2005-004321", "USD", "es",
                      "contacto@mallasca.example", "Parque Industrial ZIP Choloma, nave 12, Choloma, Cortés"),
                  etiquetas = new("Etiquetas Rápidas", TipoProveedor.Internacional, null, "GTQ", "es",
                      "pedidos@etiquetasrapidas.example", "Km 16.5 Carretera a El Salvador, Ciudad de Guatemala, Guatemala"),
                  hilos = new("Hilos Premium", TipoProveedor.Internacional, null, "USD", "en",
                      "info@hilospremium.example", "1200 Mill Road, Greensboro, NC 27401, Estados Unidos"),
                  asia = new("Tejidos Asia Pacífico", TipoProveedor.Internacional, null, "USD", "zh",
                      "sales@tejidosasia.example", "88 Keyuan Road, Ningbo, Zhejiang, China");
        valle.AgregarContacto("Pedro Mejía", "Gerente de ventas", "+504 2550-1122", "pedro.mejia@textilesvalle.example");
        valle.AgregarContacto("Karla Rivera", "Jefa de laboratorio", "+504 2550-1130", "karla.rivera@textilesvalle.example");
        mallas.AgregarContacto("Sofía Banegas", "Ejecutiva de cuenta", "+504 2669-4400", "sofia.banegas@mallasca.example");
        etiquetas.AgregarContacto("Jorge Castillo", "Servicio al cliente", "+502 2422-8800", "jorge.castillo@etiquetasrapidas.example");
        hilos.AgregarContacto("Marta Johnson", "Account Manager", "+1 (336) 555-0142", "marta.johnson@hilospremium.example");
        hilos.AgregarContacto("David Lee", "Logistics Coordinator", "+1 (336) 555-0177", "david.lee@hilospremium.example");
        asia.AgregarContacto("Li Wei", "Export Sales", "+86 574 8765 4321", "li.wei@tejidosasia.example");
        asia.Desactivar(); // ejemplo de proveedor inactivo: aparece en el maestro pero no se le puede asignar
        db.Proveedores.AddRange(valle, mallas, etiquetas, hilos, asia);
        await db.SaveChangesAsync();

        var hash = hasher.Hash(PasswordDemo); // misma contraseña demo para todos
        Usuario ana = new("ana", "Ana Solicitante", Rol.Solicitante, hash),
                luis = new("luis", "Luis Solicitante", Rol.Solicitante, hash),
                carla = new("carla", "Carla Coordinadora", Rol.Coordinador, hash),
                uValle = new("valle", "Pedro (Textiles del Valle)", Rol.Proveedor, hash, valle.Id),
                uMallas = new("mallas", "Sofía (Mallas Centroamericanas)", Rol.Proveedor, hash, mallas.Id),
                uEtiquetas = new("etiquetas", "Jorge (Etiquetas Rápidas)", Rol.Proveedor, hash, etiquetas.Id),
                uHilos = new("hilos", "Marta (Hilos Premium)", Rol.Proveedor, hash, hilos.Id);
        db.Usuarios.AddRange(ana, luis, carla, uValle, uMallas, uEtiquetas, uHilos);
        await db.SaveChangesAsync();

        var hoy = reloj.Hoy();
        DateOnly D(int dias) => hoy.AddDays(dias);
        DateTime T(int dias, int hora = 9) => D(dias).ToDateTime(new TimeOnly(hora, 0));
        var coord = carla.ComoActor();
        var foto = await SubirFotoAsync();
        var proveedorDe = new Dictionary<Proveedor, Actor>
        {
            [valle] = uValle.ComoActor(), [mallas] = uMallas.ComoActor(), [etiquetas] = uEtiquetas.ComoActor(), [hilos] = uHilos.ComoActor(),
        };

        // Atajo para avanzar una solicitud por el flujo. diaEnvio null = se queda En progreso.
        Solicitud Flujo(Usuario quien, string material, TipoMaterial tipo, string desc, Prioridad prio, int creada, int objetivo,
            Proveedor? prov = null, int? compromiso = null, int? envio = null, ResultadoEvaluacion? eval = null, bool? aprobar = null)
        {
            var sol = quien.ComoActor();
            var s = Solicitud.Crear(material, tipo, desc, D(objetivo), prio, sol, T(creada));
            if (prov is null) return s;
            s.Asignar(prov, coord, T(creada, 11));
            if (compromiso is null) return s;
            var p = proveedorDe[prov];
            s.Aceptar(D(compromiso.Value), p, T(creada + 1));
            if (envio is null) return s;
            s.RegistrarEnvio(D(envio.Value), "DHL", $"GU-{Random.Shared.Next(100000, 999999)}", p, T(envio.Value, 16));
            if (eval is null) return s;
            s.RegistrarEvaluacion(eval.Value, eval == ResultadoEvaluacion.Cumple
                ? "Color y tacto conformes al estándar." : "Tono fuera de tolerancia y encogimiento mayor al 5%.",
                [new FotoMuestra(foto, "image/png", "muestra.png")], coord, T(envio.Value + 2, 10));
            if (aprobar is null) return s;
            if (aprobar.Value) s.Aprobar("Aprobado para producción.", sol, T(envio.Value + 3));
            else s.Rechazar("No cumple el tono solicitado; repetir muestra.", sol, T(envio.Value + 3));
            return s;
        }

        var lista = new List<Solicitud>
        {
            Flujo(ana, "Jersey algodón orgánico 180 g", TipoMaterial.Tela, "Jersey 100% algodón orgánico, color crudo, para línea básica.", Prioridad.Media, -1, 30),
            Flujo(luis, "Malla deportiva transpirable", TipoMaterial.Malla, "Malla poliéster con microperforación para paneles laterales.", Prioridad.Alta, -3, 25, mallas),
            Flujo(ana, "Etiqueta tejida logo", TipoMaterial.Etiqueta, "Etiqueta tejida 4x2 cm, fondo negro, logo blanco.", Prioridad.Baja, -6, 20, etiquetas, 5),
            Flujo(luis, "Hilo poliéster reciclado", TipoMaterial.Hilo, "Hilo 40/2 de PET reciclado, color negro.", Prioridad.Alta, -12, 10, hilos, -3),
            Flujo(ana, "Malla mesh hexagonal", TipoMaterial.Malla, "Mesh hexagonal para forros de chaqueta.", Prioridad.Media, -9, 15, mallas, -1),
            Flujo(ana, "French terry 280 g", TipoMaterial.Tela, "French terry perchado, color gris jaspe.", Prioridad.Alta, -15, 10, valle, -5, -6),
            Flujo(luis, "Malla de forro 120 g", TipoMaterial.Malla, "Malla de forro para shorts de baño.", Prioridad.Media, -20, 5, mallas, -10, -8, ResultadoEvaluacion.Cumple),
            Flujo(ana, "Etiqueta termotransferible", TipoMaterial.Etiqueta, "Etiqueta sin costura para cuello.", Prioridad.Media, -30, -5, etiquetas, -22, -23, ResultadoEvaluacion.Cumple, true),
            Flujo(luis, "Etiqueta de composición satinada", TipoMaterial.Etiqueta, "Etiqueta satinada impresa, 3 idiomas.", Prioridad.Baja, -40, -20, etiquetas, -30, -27, ResultadoEvaluacion.Cumple, true),
            Flujo(ana, "Hilo algodón peinado", TipoMaterial.Hilo, "Hilo 30/1 algodón peinado para bordado.", Prioridad.Media, -35, -15, hilos, -25, -22, ResultadoEvaluacion.Cumple, true),
            Flujo(luis, "Denim stretch 10 oz", TipoMaterial.Tela, "Denim con 2% elastano, lavado medio.", Prioridad.Alta, -45, -25, valle, -35, -36, ResultadoEvaluacion.Cumple, true),
            Flujo(ana, "Malla elástica power mesh", TipoMaterial.Malla, "Power mesh para ropa de compresión.", Prioridad.Alta, -50, -30, mallas, -40, -41, ResultadoEvaluacion.Cumple, true),
        };

        // Una muestra rechazada que volvió a En progreso y el proveedor recomprometió fecha.
        var rib = Flujo(ana, "Rib 1x1 para puños", TipoMaterial.Tela, "Rib algodón-elastano color vino.", Prioridad.Media, -28, 10, valle, -20, -21, ResultadoEvaluacion.NoCumple, false);
        rib.Aceptar(D(3), proveedorDe[valle], T(-17));
        lista.Add(rib);

        db.Solicitudes.AddRange(lista);
        await db.SaveChangesAsync();
    }

    private async Task<string> SubirFotoAsync()
    {
        const string nombre = "ejemplo/muestra.png";
        await using var png = typeof(DatosEjemplo).Assembly.GetManifestResourceStream("PortalMateriales.Infrastructure.Seed.muestra.png")!;
        await archivos.GuardarAsync(nombre, png, "image/png", CancellationToken.None);
        return nombre;
    }
}
