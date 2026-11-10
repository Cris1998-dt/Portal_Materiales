using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PortalMateriales.Infrastructure.Migraciones
{
    /// <inheritdoc />
    public partial class MaestroProveedores : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Activo",
                table: "Proveedores",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Direccion",
                table: "Proveedores",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Idioma",
                table: "Proveedores",
                type: "nchar(2)",
                fixedLength: true,
                maxLength: 2,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Moneda",
                table: "Proveedores",
                type: "nchar(3)",
                fixedLength: true,
                maxLength: 3,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Rtn",
                table: "Proveedores",
                type: "nchar(14)",
                fixedLength: true,
                maxLength: 14,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Tipo",
                table: "Proveedores",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            // Proveedores existentes antes del maestro: quedan activos y válidos para el dominio
            // (internacional no exige RTN). El coordinador completa sus datos después.
            migrationBuilder.Sql("""
                UPDATE Proveedores
                SET Activo = 1, Tipo = 'Internacional', Moneda = 'USD', Idioma = 'es',
                    Direccion = 'Pendiente de completar'
                """);

            migrationBuilder.CreateTable(
                name: "ContactosProveedor",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Puesto = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Telefono = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Correo = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    ProveedorId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContactosProveedor", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContactosProveedor_Proveedores_ProveedorId",
                        column: x => x.ProveedorId,
                        principalTable: "Proveedores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Proveedores_Rtn",
                table: "Proveedores",
                column: "Rtn",
                unique: true,
                filter: "[Rtn] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ContactosProveedor_ProveedorId",
                table: "ContactosProveedor",
                column: "ProveedorId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ContactosProveedor");

            migrationBuilder.DropIndex(
                name: "IX_Proveedores_Rtn",
                table: "Proveedores");

            migrationBuilder.DropColumn(
                name: "Activo",
                table: "Proveedores");

            migrationBuilder.DropColumn(
                name: "Direccion",
                table: "Proveedores");

            migrationBuilder.DropColumn(
                name: "Idioma",
                table: "Proveedores");

            migrationBuilder.DropColumn(
                name: "Moneda",
                table: "Proveedores");

            migrationBuilder.DropColumn(
                name: "Rtn",
                table: "Proveedores");

            migrationBuilder.DropColumn(
                name: "Tipo",
                table: "Proveedores");
        }
    }
}
