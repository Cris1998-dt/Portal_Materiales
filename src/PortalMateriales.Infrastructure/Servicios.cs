using System.Security.Cryptography;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using PortalMateriales.Application;

namespace PortalMateriales.Infrastructure;

/// <summary>Fotos en Azure Blob Storage (contenedor privado; la API las sirve tras validar acceso).</summary>
public class BlobAlmacenArchivos(BlobContainerClient contenedor) : IAlmacenArchivos
{
    public Task GuardarAsync(string nombre, Stream contenido, string contentType, CancellationToken ct) =>
        contenedor.GetBlobClient(nombre).UploadAsync(contenido,
            new BlobUploadOptions { HttpHeaders = new BlobHttpHeaders { ContentType = contentType } }, ct);

    public async Task<Stream?> AbrirAsync(string nombre, CancellationToken ct)
    {
        try { return await contenedor.GetBlobClient(nombre).OpenReadAsync(cancellationToken: ct); }
        catch (RequestFailedException e) when (e.Status == 404) { return null; }
    }

    public Task EliminarAsync(string nombre, CancellationToken ct) =>
        contenedor.GetBlobClient(nombre).DeleteIfExistsAsync(cancellationToken: ct);
}

/// <summary>PBKDF2-SHA256 de la librería estándar. Formato: iteraciones.sal.hash (base64).</summary>
public class Pbkdf2PasswordHasher : IPasswordHasher
{
    private const int Iteraciones = 100_000;

    public string Hash(string password)
    {
        var sal = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, sal, Iteraciones, HashAlgorithmName.SHA256, 32);
        return $"{Iteraciones}.{Convert.ToBase64String(sal)}.{Convert.ToBase64String(hash)}";
    }

    public bool Verificar(string password, string almacenado)
    {
        var partes = almacenado.Split('.');
        if (partes.Length != 3 || !int.TryParse(partes[0], out var iter)) return false;
        var esperado = Convert.FromBase64String(partes[2]);
        var calculado = Rfc2898DeriveBytes.Pbkdf2(password, Convert.FromBase64String(partes[1]), iter, HashAlgorithmName.SHA256, esperado.Length);
        return CryptographicOperations.FixedTimeEquals(esperado, calculado);
    }
}
