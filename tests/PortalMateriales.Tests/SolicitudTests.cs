using PortalMateriales.Domain;

namespace PortalMateriales.Tests;

/// <summary>Máquina de estados del agregado Solicitud: transiciones válidas, roles y propiedad.</summary>
public class SolicitudTests
{
    private static readonly DateTime Ahora = new(2026, 11, 9, 10, 0, 0);
    private static readonly DateOnly Hoy = DateOnly.FromDateTime(Ahora);
    private static readonly Actor Ana = new(1, "Ana", Rol.Solicitante);
    private static readonly Actor Luis = new(2, "Luis", Rol.Solicitante);
    private static readonly Actor Carla = new(3, "Carla", Rol.Coordinador);
    private static readonly Actor ProvA = new(4, "Prov A", Rol.Proveedor, ProveedorId: 0); // Proveedor sin persistir tiene Id 0
    private static readonly Actor ProvB = new(5, "Prov B", Rol.Proveedor, ProveedorId: 99);
    private static readonly FotoMuestra[] UnaFoto = [new("f.png", "image/png", "f.png")];

    private static Solicitud Nueva() =>
        Solicitud.Crear("Jersey", TipoMaterial.Tela, "desc", Hoy.AddDays(10), Prioridad.Alta, Ana, Ahora);

    private static Solicitud Hasta(EstadoSolicitud estado)
    {
        var s = Nueva();
        if (estado == EstadoSolicitud.Solicitado) return s;
        s.Asignar(ProveedorTests.Nuevo(), Carla, Ahora);
        if (estado == EstadoSolicitud.Asignado) return s;
        s.Aceptar(Hoy.AddDays(5), ProvA, Ahora);
        if (estado == EstadoSolicitud.EnProgreso) return s;
        s.RegistrarEnvio(Hoy, "DHL", "G1", ProvA, Ahora);
        if (estado == EstadoSolicitud.Enviado) return s;
        s.RegistrarEvaluacion(ResultadoEvaluacion.Cumple, "ok", UnaFoto, Carla, Ahora);
        if (estado == EstadoSolicitud.EnEvaluacion) return s;
        s.Aprobar(null, Ana, Ahora);
        return s;
    }

    [Fact]
    public void Flujo_feliz_llega_a_aprobado_con_historial_completo()
    {
        var s = Hasta(EstadoSolicitud.Aprobado);
        Assert.Equal(EstadoSolicitud.Aprobado, s.Estado);
        Assert.Equal(6, s.Historial.Count);
        Assert.All(s.Historial.Skip(1), h => Assert.NotNull(h.EstadoAnterior));
        Assert.Equal(["Ana", "Carla", "Prov A", "Prov A", "Carla", "Ana"], s.Historial.Select(h => h.UsuarioNombre));
    }

    [Theory]
    [InlineData(Rol.Coordinador)]
    [InlineData(Rol.Proveedor)]
    public void Solo_el_solicitante_crea(Rol rol) =>
        Assert.Throws<AccesoDenegadoException>(() =>
            Solicitud.Crear("x", TipoMaterial.Tela, "d", Hoy, Prioridad.Baja, new Actor(9, "X", rol, 1), Ahora));

    [Fact]
    public void Fecha_objetivo_en_el_pasado_no_se_acepta() =>
        Assert.Throws<ValidacionException>(() =>
            Solicitud.Crear("x", TipoMaterial.Tela, "d", Hoy.AddDays(-1), Prioridad.Baja, Ana, Ahora));

    [Fact]
    public void No_se_puede_saltar_estados()
    {
        Assert.Throws<TransicionInvalidaException>(() => Nueva().Aprobar(null, Ana, Ahora));
        Assert.Throws<TransicionInvalidaException>(() => Hasta(EstadoSolicitud.Asignado).RegistrarEnvio(Hoy, "DHL", "G", ProvA, Ahora));
        Assert.Throws<TransicionInvalidaException>(() => Hasta(EstadoSolicitud.EnProgreso).RegistrarEvaluacion(ResultadoEvaluacion.Cumple, "c", UnaFoto, Carla, Ahora));
        Assert.Throws<TransicionInvalidaException>(() => Hasta(EstadoSolicitud.Aprobado).Rechazar("no", Ana, Ahora));
    }

    [Fact]
    public void Solo_el_proveedor_asignado_acepta_y_envia()
    {
        Assert.Throws<AccesoDenegadoException>(() => Hasta(EstadoSolicitud.Asignado).Aceptar(Hoy, ProvB, Ahora));
        Assert.Throws<AccesoDenegadoException>(() => Hasta(EstadoSolicitud.Asignado).Aceptar(Hoy, Carla, Ahora));
        Assert.Throws<AccesoDenegadoException>(() => Hasta(EstadoSolicitud.EnProgreso).RegistrarEnvio(Hoy, "DHL", "G", ProvB, Ahora));
    }

    [Fact]
    public void Solo_el_solicitante_que_creo_decide()
    {
        Assert.Throws<AccesoDenegadoException>(() => Hasta(EstadoSolicitud.EnEvaluacion).Aprobar(null, Luis, Ahora));
        Assert.Throws<AccesoDenegadoException>(() => Hasta(EstadoSolicitud.EnEvaluacion).Rechazar("no", Carla, Ahora));
    }

    [Fact]
    public void Evaluacion_exige_entre_una_y_cinco_fotos()
    {
        Assert.Throws<ValidacionException>(() => Hasta(EstadoSolicitud.Enviado).RegistrarEvaluacion(ResultadoEvaluacion.Cumple, "c", [], Carla, Ahora));
        var seis = Enumerable.Range(0, 6).Select(i => new FotoMuestra($"{i}", "image/png", "x")).ToArray();
        Assert.Throws<ValidacionException>(() => Hasta(EstadoSolicitud.Enviado).RegistrarEvaluacion(ResultadoEvaluacion.Cumple, "c", seis, Carla, Ahora));
    }

    [Fact]
    public void Rechazo_regresa_a_en_progreso_y_exige_nuevo_compromiso()
    {
        var s = Hasta(EstadoSolicitud.EnEvaluacion);
        s.Rechazar("tono distinto", Ana, Ahora);

        Assert.Equal(EstadoSolicitud.EnProgreso, s.Estado);
        Assert.Null(s.FechaCompromiso);
        Assert.Throws<TransicionInvalidaException>(() => s.RegistrarEnvio(Hoy, "DHL", "G2", ProvA, Ahora));

        s.Aceptar(Hoy.AddDays(3), ProvA, Ahora);
        Assert.Throws<TransicionInvalidaException>(() => s.Aceptar(Hoy.AddDays(4), ProvA, Ahora)); // ya hay compromiso vigente
        s.RegistrarEnvio(Hoy, "DHL", "G2", ProvA, Ahora);
        Assert.Equal(2, s.Envios.Count);
    }

    [Fact]
    public void Rechazo_exige_comentario() =>
        Assert.Throws<ValidacionException>(() => Hasta(EstadoSolicitud.EnEvaluacion).Rechazar("  ", Ana, Ahora));

    [Fact]
    public void Vencida_solo_en_progreso_con_compromiso_pasado()
    {
        var s = Hasta(EstadoSolicitud.EnProgreso); // compromiso = Hoy + 5
        Assert.False(s.EstaVencida(Hoy.AddDays(5)));
        Assert.True(s.EstaVencida(Hoy.AddDays(6)));

        s.RegistrarEnvio(Hoy, "DHL", "G", ProvA, Ahora);
        Assert.False(s.EstaVencida(Hoy.AddDays(30))); // ya enviada: no está vencida
    }

    [Fact]
    public void Envio_guarda_si_fue_a_tiempo_contra_el_compromiso_vigente()
    {
        var tarde = Nueva();
        tarde.Asignar(ProveedorTests.Nuevo(), Carla, Ahora.AddDays(-10));
        tarde.Aceptar(Hoy.AddDays(-5), ProvA, Ahora.AddDays(-10));
        tarde.RegistrarEnvio(Hoy, "DHL", "G", ProvA, Ahora);
        Assert.False(tarde.Envios.Single().ATiempo);

        var puntual = Hasta(EstadoSolicitud.Enviado);
        Assert.True(puntual.Envios.Single().ATiempo);
    }

    [Fact]
    public void Envio_no_puede_tener_fecha_futura() =>
        Assert.Throws<ValidacionException>(() => Hasta(EstadoSolicitud.EnProgreso).RegistrarEnvio(Hoy.AddDays(1), "DHL", "G", ProvA, Ahora));
}

/// <summary>AccionesPara debe coincidir exactamente con lo que los métodos permiten, en cada estado y para cada actor.</summary>
public class AccionesTests
{
    private static readonly DateTime Ahora = new(2026, 11, 9, 10, 0, 0);
    private static readonly DateOnly Hoy = DateOnly.FromDateTime(Ahora);
    private static readonly Actor[] Actores =
    [
        new(1, "Ana", Rol.Solicitante), new(2, "Luis", Rol.Solicitante), new(3, "Carla", Rol.Coordinador),
        new(4, "Prov A", Rol.Proveedor, 0), new(5, "Prov B", Rol.Proveedor, 99),
    ];

    private static IEnumerable<Solicitud> TodosLosEstados()
    {
        var ana = Actores[0]; var carla = Actores[2]; var prov = Actores[3];
        var s = Solicitud.Crear("Jersey", TipoMaterial.Tela, "d", Hoy.AddDays(10), Prioridad.Alta, ana, Ahora);
        yield return Clonar(s);
        s.Asignar(ProveedorTests.Nuevo(), carla, Ahora); yield return Clonar(s);
        s.Aceptar(Hoy.AddDays(5), prov, Ahora); yield return Clonar(s);
        s.RegistrarEnvio(Hoy, "DHL", "G", prov, Ahora); yield return Clonar(s);
        s.RegistrarEvaluacion(ResultadoEvaluacion.NoCumple, "c", [new("f", "image/png", "f")], carla, Ahora); yield return Clonar(s);
        var rechazada = Clonar(s); rechazada.Rechazar("no", ana, Ahora); yield return rechazada; // En progreso sin compromiso
        s.Aprobar(null, ana, Ahora); yield return s;

        // Reproduce el mismo camino para obtener una copia independiente en ese punto.
        Solicitud Clonar(Solicitud origen)
        {
            var c = Solicitud.Crear("Jersey", TipoMaterial.Tela, "d", Hoy.AddDays(10), Prioridad.Alta, ana, Ahora);
            foreach (var h in origen.Historial.Skip(1))
                switch (h.Accion)
                {
                    case "Asignó proveedor": c.Asignar(ProveedorTests.Nuevo(), carla, Ahora); break;
                    case "Aceptó con fecha compromiso": c.Aceptar(Hoy.AddDays(5), prov, Ahora); break;
                    case "Registró envío": c.RegistrarEnvio(Hoy, "DHL", "G", prov, Ahora); break;
                    case "Registró evaluación": c.RegistrarEvaluacion(ResultadoEvaluacion.NoCumple, "c", [new("f", "image/png", "f")], carla, Ahora); break;
                    case "Rechazó la muestra": c.Rechazar("no", ana, Ahora); break;
                }
            return c;
        }
    }

    private static bool Permite(Solicitud s, AccionSolicitud accion, Actor a)
    {
        try
        {
            switch (accion)
            {
                case AccionSolicitud.Asignar: s.Asignar(ProveedorTests.Nuevo(), a, Ahora); break;
                case AccionSolicitud.Aceptar: s.Aceptar(Hoy.AddDays(5), a, Ahora); break;
                case AccionSolicitud.RegistrarEnvio: s.RegistrarEnvio(Hoy, "DHL", "G", a, Ahora); break;
                case AccionSolicitud.Evaluar: s.ValidarEvaluacion(a, 1); break;
                case AccionSolicitud.Aprobar: s.Aprobar(null, a, Ahora); break;
                case AccionSolicitud.Rechazar: s.Rechazar("no", a, Ahora); break;
            }
            return true;
        }
        catch (Exception e) when (e is TransicionInvalidaException or AccesoDenegadoException) { return false; }
    }

    [Fact]
    public void Acciones_disponibles_coinciden_con_lo_que_el_dominio_permite()
    {
        var revisados = 0;
        foreach (var estadoIndice in Enumerable.Range(0, 7))
            foreach (var actor in Actores)
                foreach (var accion in Enum.GetValues<AccionSolicitud>())
                {
                    var anunciadas = TodosLosEstados().ElementAt(estadoIndice).AccionesPara(actor);
                    var permitida = Permite(TodosLosEstados().ElementAt(estadoIndice), accion, actor); // copia nueva: probar no la altera
                    Assert.True(anunciadas.Contains(accion) == permitida,
                        $"Estado #{estadoIndice}, {actor.Nombre}, {accion}: anunciada={anunciadas.Contains(accion)} permitida={permitida}");
                    revisados++;
                }
        Assert.Equal(7 * 5 * 6, revisados);
    }
}
