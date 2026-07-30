using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GMAO.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSocieteIdToAllTenantTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SocieteId",
                table: "Pieces",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "SocieteId",
                table: "OrdresTravail",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "SocieteId",
                table: "Fournisseurs",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "SocieteId",
                table: "DemandesIntervention",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "SocieteId",
                table: "Campagnes",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateIndex(
                name: "IX_DemandesIntervention_SocieteId",
                table: "DemandesIntervention",
                column: "SocieteId");

            migrationBuilder.AddForeignKey(
                name: "FK_DemandesIntervention_Societes_SocieteId",
                table: "DemandesIntervention",
                column: "SocieteId",
                principalTable: "Societes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DemandesIntervention_Societes_SocieteId",
                table: "DemandesIntervention");

            migrationBuilder.DropIndex(
                name: "IX_DemandesIntervention_SocieteId",
                table: "DemandesIntervention");

            migrationBuilder.DropColumn(
                name: "SocieteId",
                table: "Pieces");

            migrationBuilder.DropColumn(
                name: "SocieteId",
                table: "OrdresTravail");

            migrationBuilder.DropColumn(
                name: "SocieteId",
                table: "Fournisseurs");

            migrationBuilder.DropColumn(
                name: "SocieteId",
                table: "DemandesIntervention");

            migrationBuilder.DropColumn(
                name: "SocieteId",
                table: "Campagnes");
        }
    }
}
