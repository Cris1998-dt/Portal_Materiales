namespace PortalMateriales.Web.Servicios;

public record Aviso(Guid Id, string Texto, bool EsError);

/// <summary>Mensajes breves ("toast") que se cierran solos.</summary>
public class Avisos
{
    public List<Aviso> Lista { get; } = [];
    public event Action? Cambio;

    public void Exito(string texto) => Agregar(texto, false);
    public void Error(string texto) => Agregar(texto, true);

    public void Quitar(Guid id) { Lista.RemoveAll(a => a.Id == id); Cambio?.Invoke(); }

    private async void Agregar(string texto, bool error)
    {
        var a = new Aviso(Guid.NewGuid(), texto, error);
        Lista.Add(a);
        Cambio?.Invoke();
        await Task.Delay(error ? 7000 : 4000);
        Quitar(a.Id);
    }
}
