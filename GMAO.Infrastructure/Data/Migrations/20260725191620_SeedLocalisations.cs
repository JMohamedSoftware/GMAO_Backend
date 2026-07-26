using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace GMAO.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class SeedLocalisations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Localisations",
                columns: new[] { "Id", "Description", "Nom", "ParentId" },
                values: new object[] { 1, "Site principal", "Usine", null });

            migrationBuilder.InsertData(
                table: "Roles",
                columns: new[] { "Id", "Description", "Nom" },
                values: new object[] { 7, "Administrateur global de la plateforme", "SuperAdmin" });

            migrationBuilder.InsertData(
                table: "Localisations",
                columns: new[] { "Id", "Description", "Nom", "ParentId" },
                values: new object[,]
                {
                    { 2, null, "Réception", 1 },
                    { 3, null, "Lavage", 1 },
                    { 4, null, "Tri", 1 },
                    { 5, null, "Concentration", 1 },
                    { 6, null, "Conditionnement", 1 },
                    { 7, null, "Stockage", 1 },
                    { 8, null, "Utilités", 1 }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Localisations",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "Localisations",
                keyColumn: "Id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "Localisations",
                keyColumn: "Id",
                keyValue: 4);

            migrationBuilder.DeleteData(
                table: "Localisations",
                keyColumn: "Id",
                keyValue: 5);

            migrationBuilder.DeleteData(
                table: "Localisations",
                keyColumn: "Id",
                keyValue: 6);

            migrationBuilder.DeleteData(
                table: "Localisations",
                keyColumn: "Id",
                keyValue: 7);

            migrationBuilder.DeleteData(
                table: "Localisations",
                keyColumn: "Id",
                keyValue: 8);

            migrationBuilder.DeleteData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 7);

            migrationBuilder.DeleteData(
                table: "Localisations",
                keyColumn: "Id",
                keyValue: 1);
        }
    }
}
