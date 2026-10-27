using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GMAO.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSocieteIdToLocalisations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SocieteId",
                table: "Localisations",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.UpdateData(
                table: "Localisations",
                keyColumn: "Id",
                keyValue: 1,
                column: "SocieteId",
                value: 1);

            migrationBuilder.UpdateData(
                table: "Localisations",
                keyColumn: "Id",
                keyValue: 2,
                column: "SocieteId",
                value: 1);

            migrationBuilder.UpdateData(
                table: "Localisations",
                keyColumn: "Id",
                keyValue: 3,
                column: "SocieteId",
                value: 1);

            migrationBuilder.UpdateData(
                table: "Localisations",
                keyColumn: "Id",
                keyValue: 4,
                column: "SocieteId",
                value: 1);

            migrationBuilder.UpdateData(
                table: "Localisations",
                keyColumn: "Id",
                keyValue: 5,
                column: "SocieteId",
                value: 1);

            migrationBuilder.UpdateData(
                table: "Localisations",
                keyColumn: "Id",
                keyValue: 6,
                column: "SocieteId",
                value: 1);

            migrationBuilder.UpdateData(
                table: "Localisations",
                keyColumn: "Id",
                keyValue: 7,
                column: "SocieteId",
                value: 1);

            migrationBuilder.UpdateData(
                table: "Localisations",
                keyColumn: "Id",
                keyValue: 8,
                column: "SocieteId",
                value: 1);

            migrationBuilder.CreateIndex(
                name: "IX_Localisations_SocieteId",
                table: "Localisations",
                column: "SocieteId");

            migrationBuilder.AddForeignKey(
                name: "FK_Localisations_Societes_SocieteId",
                table: "Localisations",
                column: "SocieteId",
                principalTable: "Societes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Localisations_Societes_SocieteId",
                table: "Localisations");

            migrationBuilder.DropIndex(
                name: "IX_Localisations_SocieteId",
                table: "Localisations");

            migrationBuilder.DropColumn(
                name: "SocieteId",
                table: "Localisations");
        }
    }
}
