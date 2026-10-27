using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GMAO.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class MakeEquipementLocalisationNullable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Equipements_Localisations_LocalisationId",
                table: "Equipements");

            migrationBuilder.AlterColumn<int>(
                name: "LocalisationId",
                table: "Equipements",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddForeignKey(
                name: "FK_Equipements_Localisations_LocalisationId",
                table: "Equipements",
                column: "LocalisationId",
                principalTable: "Localisations",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Equipements_Localisations_LocalisationId",
                table: "Equipements");

            migrationBuilder.AlterColumn<int>(
                name: "LocalisationId",
                table: "Equipements",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Equipements_Localisations_LocalisationId",
                table: "Equipements",
                column: "LocalisationId",
                principalTable: "Localisations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
