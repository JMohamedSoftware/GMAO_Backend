using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace GMAO.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class UpdateRolesToMatchCahierDesCharges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Re-assign users with dropped roles (6: Magasinier, 7: SuperAdmin) to 1 (Administrateur)
            migrationBuilder.Sql("UPDATE \"Users\" SET \"RoleId\" = 1 WHERE \"RoleId\" IN (6, 7);");

            migrationBuilder.DeleteData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 6);

            migrationBuilder.DeleteData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 7);

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "Description", "Nom" },
                values: new object[] { "Administrateur de la plateforme et de l'usine avec accès complet", "Administrateur" });

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 3,
                column: "Nom",
                value: "Chef d'équipe");

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 5,
                columns: new[] { "Description", "Nom" },
                values: new object[] { "Déclaration des pannes et validation", "Responsable Production" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "Description", "Nom" },
                values: new object[] { "Administrateur d'une société avec accès complet sur son périmètre", "Admin" });

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 3,
                column: "Nom",
                value: "Chef Equipe");

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 5,
                columns: new[] { "Description", "Nom" },
                values: new object[] { "Déclaration des pannes", "Production" });

            migrationBuilder.InsertData(
                table: "Roles",
                columns: new[] { "Id", "Description", "Nom" },
                values: new object[,]
                {
                    { 6, "Gestion du stock de pièces de rechange", "Magasinier" },
                    { 7, "Administrateur plateforme - gère toutes les sociétés", "SuperAdmin" }
                });
        }
    }
}
