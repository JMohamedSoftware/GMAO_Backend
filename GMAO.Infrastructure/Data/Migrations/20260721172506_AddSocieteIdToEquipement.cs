using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GMAO.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSocieteIdToEquipement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SocieteId",
                table: "Equipements",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Equipements_SocieteId",
                table: "Equipements",
                column: "SocieteId");

            migrationBuilder.AddForeignKey(
                name: "FK_Equipements_Societes_SocieteId",
                table: "Equipements",
                column: "SocieteId",
                principalTable: "Societes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Equipements_Societes_SocieteId",
                table: "Equipements");

            migrationBuilder.DropIndex(
                name: "IX_Equipements_SocieteId",
                table: "Equipements");

            migrationBuilder.DropColumn(
                name: "SocieteId",
                table: "Equipements");
        }
    }
}
