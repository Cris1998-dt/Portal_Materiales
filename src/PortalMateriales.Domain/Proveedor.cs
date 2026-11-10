namespace PortalMateriales.Domain;

public enum TipoProveedor { Nacional, Internacional }

/// <summary>Maestro de proveedores. Raíz de su propio agregado: los contactos solo se tocan a través de él.</summary>
public class Proveedor
{
    public int Id { get; private set; }
    public string Nombre { get; private set; } = "";
    public TipoProveedor Tipo { get; private set; }
    /// <summary>Obligatorio si es nacional; opcional si es internacional.</summary>
    public string? Rtn { get; private set; }
    /// <summary>Código ISO 4217 en que factura (HNL, USD...).</summary>
    public string Moneda { get; private set; } = "";
    /// <summary>Código ISO 639-1 para comunicarse (es, en...).</summary>
    public string Idioma { get; private set; } = "";
    public string Correo { get; private set; } = "";
    public string Direccion { get; private set; } = "";
    /// <summary>Un proveedor inactivo no recibe asignaciones nuevas; lo ya asignado sigue su curso.</summary>
    public bool Activo { get; private set; }

    private readonly List<ContactoProveedor> _contactos = [];
    public IReadOnlyList<ContactoProveedor> Contactos => _contactos;

    private Proveedor() { }

    public Proveedor(string nombre, TipoProveedor tipo, string? rtn, string moneda, string idioma, string correo, string direccion)
    {
        Actualizar(nombre, tipo, rtn, moneda, idioma, correo, direccion);
        Activo = true;
    }

    public void Actualizar(string nombre, TipoProveedor tipo, string? rtn, string moneda, string idioma, string correo, string direccion)
    {
        if (!Enum.IsDefined(tipo)) throw new ValidacionException("Tipo de proveedor inválido.");
        Nombre = Validar.Texto(nombre, "Nombre", 150);
        Tipo = tipo;
        Rtn = Validar.Rtn(rtn, obligatorio: tipo == TipoProveedor.Nacional);
        Moneda = Validar.Moneda(moneda);
        Idioma = Validar.Idioma(idioma);
        Correo = Validar.Correo(correo, "Correo");
        Direccion = Validar.Texto(direccion, "Dirección", 300);
    }

    public void Activar() => Activo = true;
    public void Desactivar() => Activo = false;

    public ContactoProveedor AgregarContacto(string nombre, string puesto, string telefono, string correo)
    {
        var c = new ContactoProveedor();
        c.Actualizar(nombre, puesto, telefono, correo);
        ExigirCorreoUnico(c.Correo, null);
        _contactos.Add(c);
        return c;
    }

    public void ActualizarContacto(int contactoId, string nombre, string puesto, string telefono, string correo)
    {
        var c = Contacto(contactoId);
        ExigirCorreoUnico(Validar.Correo(correo, "Correo del contacto"), c);
        c.Actualizar(nombre, puesto, telefono, correo);
    }

    public void EliminarContacto(int contactoId) => _contactos.Remove(Contacto(contactoId));

    private ContactoProveedor Contacto(int id) =>
        _contactos.FirstOrDefault(c => c.Id == id) ?? throw new ValidacionException($"El contacto {id} no pertenece a este proveedor.");

    private void ExigirCorreoUnico(string correo, ContactoProveedor? excepto)
    {
        if (_contactos.Any(c => c != excepto && c.Correo == correo))
            throw new ValidacionException($"Ya existe un contacto con el correo {correo}.");
    }
}

public class ContactoProveedor
{
    public int Id { get; private set; }
    public string Nombre { get; private set; } = "";
    public string Puesto { get; private set; } = "";
    public string Telefono { get; private set; } = "";
    public string Correo { get; private set; } = "";

    internal ContactoProveedor() { }

    internal void Actualizar(string nombre, string puesto, string telefono, string correo)
    {
        Nombre = Validar.Texto(nombre, "Nombre del contacto", 150);
        Puesto = Validar.Texto(puesto, "Puesto", 100);
        Telefono = Validar.Telefono(telefono);
        Correo = Validar.Correo(correo, "Correo del contacto");
    }
}
