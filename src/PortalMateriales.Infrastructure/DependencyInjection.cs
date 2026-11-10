using Azure.Storage.Blobs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PortalMateriales.Application;

namespace PortalMateriales.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        services.AddDbContext<AppDbContext>(o => o.UseSqlServer(
            config.GetConnectionString("Sql") ?? throw new InvalidOperationException("Falta ConnectionStrings:Sql"),
            // Azure SQL gratuito se pausa solo: el primer intento tras la pausa falla de forma transitoria.
            sql => sql.EnableRetryOnFailure(6, TimeSpan.FromSeconds(10), null)));

        services.AddSingleton(new BlobContainerClient(
            config.GetConnectionString("Blob") ?? throw new InvalidOperationException("Falta ConnectionStrings:Blob"),
            config["Almacenamiento:Contenedor"] ?? "fotos-muestras"));

        services.AddScoped<ISolicitudRepository, SolicitudRepository>();
        services.AddScoped<IProveedorRepository, ProveedorRepository>();
        services.AddScoped<IUsuarioRepository, UsuarioRepository>();
        services.AddScoped<IConsultas, Consultas>();
        services.AddSingleton<IAlmacenArchivos, BlobAlmacenArchivos>();
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddScoped<DatosEjemplo>();
        return services;
    }

    /// <summary>Aplica migraciones, crea el contenedor de fotos y carga datos de ejemplo si la base está vacía.</summary>
    public static async Task InicializarAsync(this IServiceProvider sp)
    {
        using var scope = sp.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
        await scope.ServiceProvider.GetRequiredService<BlobContainerClient>().CreateIfNotExistsAsync();
        await scope.ServiceProvider.GetRequiredService<DatosEjemplo>().CargarAsync();
    }
}
