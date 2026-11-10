using Microsoft.EntityFrameworkCore;
using PortalMateriales.Domain;

namespace PortalMateriales.Infrastructure;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Solicitud> Solicitudes => Set<Solicitud>();
    public DbSet<Proveedor> Proveedores => Set<Proveedor>();
    public DbSet<Usuario> Usuarios => Set<Usuario>();

    // Enums como texto: la base se lee sin tabla de códigos y no se rompe si se reordena el enum.
    protected override void ConfigureConventions(ModelConfigurationBuilder b) =>
        b.Properties<Enum>().HaveConversion<string>().HaveMaxLength(20);

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Proveedor>(e =>
        {
            e.Property(p => p.Nombre).HasMaxLength(150);
            e.Property(p => p.Rtn).HasMaxLength(14).IsFixedLength();
            e.HasIndex(p => p.Rtn).IsUnique(); // EF lo filtra a "Rtn IS NOT NULL": varios internacionales sin RTN
            e.Property(p => p.Moneda).HasMaxLength(3).IsFixedLength();
            e.Property(p => p.Idioma).HasMaxLength(2).IsFixedLength();
            e.Property(p => p.Correo).HasMaxLength(150);
            e.Property(p => p.Direccion).HasMaxLength(300);
            e.HasMany(p => p.Contactos).WithOne().HasForeignKey("ProveedorId").IsRequired();
        });

        b.Entity<ContactoProveedor>(e =>
        {
            e.ToTable("ContactosProveedor");
            e.Property(c => c.Nombre).HasMaxLength(150);
            e.Property(c => c.Puesto).HasMaxLength(100);
            e.Property(c => c.Telefono).HasMaxLength(30);
            e.Property(c => c.Correo).HasMaxLength(150);
        });

        b.Entity<Usuario>(e =>
        {
            e.HasIndex(u => u.NombreUsuario).IsUnique();
            e.Property(u => u.NombreUsuario).HasMaxLength(50);
            e.Property(u => u.Nombre).HasMaxLength(150);
            e.Property(u => u.PasswordHash).HasMaxLength(200);
            e.HasOne<Proveedor>().WithMany().HasForeignKey(u => u.ProveedorId);
        });

        b.Entity<Solicitud>(e =>
        {
            e.Property(s => s.Material).HasMaxLength(150);
            e.Property(s => s.Descripcion).HasMaxLength(2000);
            e.HasIndex(s => s.Estado);
            e.HasIndex(s => s.ProveedorId);
            e.HasOne<Usuario>().WithMany().HasForeignKey(s => s.SolicitanteId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Proveedor>().WithMany().HasForeignKey(s => s.ProveedorId).OnDelete(DeleteBehavior.Restrict);

            // Hijos del agregado: solo accesibles a través de Solicitud (FK sombra SolicitudId).
            e.HasMany(s => s.Envios).WithOne().HasForeignKey("SolicitudId").IsRequired();
            e.HasMany(s => s.Evaluaciones).WithOne().HasForeignKey("SolicitudId").IsRequired();
            e.HasMany(s => s.Historial).WithOne().HasForeignKey("SolicitudId").IsRequired();
        });

        b.Entity<Envio>(e =>
        {
            e.Property(x => x.Transportista).HasMaxLength(100);
            e.Property(x => x.NumeroGuia).HasMaxLength(100);
        });

        b.Entity<Evaluacion>(e =>
        {
            e.Property(x => x.Comentarios).HasMaxLength(2000);
            e.HasOne<Usuario>().WithMany().HasForeignKey(x => x.CoordinadorId).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(x => x.Fotos).WithOne().HasForeignKey("EvaluacionId").IsRequired();
        });

        b.Entity<FotoMuestra>(e =>
        {
            e.Property(x => x.NombreArchivo).HasMaxLength(300);
            e.Property(x => x.ContentType).HasMaxLength(50);
            e.Property(x => x.NombreOriginal).HasMaxLength(255);
        });

        b.Entity<HistorialCambio>(e =>
        {
            e.Property(x => x.UsuarioNombre).HasMaxLength(150);
            e.Property(x => x.Accion).HasMaxLength(100);
            e.Property(x => x.Detalle).HasMaxLength(1000);
        });
    }
}
