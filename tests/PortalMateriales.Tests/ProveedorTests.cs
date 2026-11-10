using PortalMateriales.Domain;

namespace PortalMateriales.Tests;

/// <summary>Maestro de proveedores: validaciones, estado y contactos.</summary>
public class ProveedorTests
{
    public static Proveedor Nuevo(TipoProveedor tipo = TipoProveedor.Nacional, string? rtn = "08011999123456",
        string moneda = "HNL", string idioma = "es") =>
        new("Textiles", tipo, rtn, moneda, idioma, "ventas@textiles.example", "San Pedro Sula");

    [Fact]
    public void Nuevo_proveedor_queda_activo_y_normaliza_datos()
    {
        var p = Nuevo(rtn: "0801-1999-123456", moneda: "usd", idioma: "EN");
        Assert.True(p.Activo);
        Assert.Equal("08011999123456", p.Rtn);
        Assert.Equal("USD", p.Moneda);
        Assert.Equal("en", p.Idioma);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("123")]
    [InlineData("0801199912345X")]
    public void Nacional_exige_rtn_de_14_digitos(string? rtn) =>
        Assert.Throws<ValidacionException>(() => Nuevo(TipoProveedor.Nacional, rtn));

    [Fact]
    public void Internacional_no_exige_rtn() =>
        Assert.Null(Nuevo(TipoProveedor.Internacional, rtn: null).Rtn);

    [Theory]
    [InlineData("XXX", "es")]
    [InlineData("HNL", "xx")]
    [InlineData("", "es")]
    public void Moneda_e_idioma_deben_ser_codigos_iso(string moneda, string idioma) =>
        Assert.Throws<ValidacionException>(() => Nuevo(moneda: moneda, idioma: idioma));

    [Fact]
    public void Correo_invalido_se_rechaza() =>
        Assert.Throws<ValidacionException>(() =>
            new Proveedor("X", TipoProveedor.Internacional, null, "USD", "en", "no-es-correo", "dir"));

    [Fact]
    public void Proveedor_inactivo_no_recibe_asignaciones()
    {
        var p = Nuevo();
        p.Desactivar();
        var s = Solicitud.Crear("Jersey", TipoMaterial.Tela, "d", new DateOnly(2030, 1, 1), Prioridad.Media,
            new Actor(1, "Ana", Rol.Solicitante), new DateTime(2026, 11, 9));

        Assert.Throws<ValidacionException>(() => s.Asignar(p, new Actor(2, "Carla", Rol.Coordinador), new DateTime(2026, 11, 9)));

        p.Activar();
        s.Asignar(p, new Actor(2, "Carla", Rol.Coordinador), new DateTime(2026, 11, 9));
        Assert.Equal(EstadoSolicitud.Asignado, s.Estado);
    }

    [Fact]
    public void Contactos_validan_datos_y_no_repiten_correo()
    {
        var p = Nuevo();
        p.AgregarContacto("Pedro", "Ventas", "+504 2550-1122", "Pedro@Textiles.example");
        Assert.Equal("pedro@textiles.example", p.Contactos[0].Correo);

        Assert.Throws<ValidacionException>(() => p.AgregarContacto("Otro", "Ventas", "+504 2550-0000", "pedro@textiles.example"));
        Assert.Throws<ValidacionException>(() => p.AgregarContacto("Ana", "Lab", "abc", "ana@textiles.example"));
        Assert.Throws<ValidacionException>(() => p.AgregarContacto("Ana", " ", "+504 2550-0000", "ana@textiles.example"));
        Assert.Single(p.Contactos);
    }

    [Fact]
    public void Eliminar_contacto_inexistente_falla() =>
        Assert.Throws<ValidacionException>(() => Nuevo().EliminarContacto(42));
}
