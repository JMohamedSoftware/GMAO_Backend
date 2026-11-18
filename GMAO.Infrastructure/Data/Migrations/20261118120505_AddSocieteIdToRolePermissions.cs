using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GMAO.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSocieteIdToRolePermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SocieteId",
                table: "RolePermissions",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_RolePermissions_SocieteId",
                table: "RolePermissions",
                column: "SocieteId");

            migrationBuilder.AddForeignKey(
                name: "FK_RolePermissions_Societes_SocieteId",
                table: "RolePermissions",
                column: "SocieteId",
                principalTable: "Societes",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RolePermissions_Societes_SocieteId",
                table: "RolePermissions");

            migrationBuilder.DropIndex(
                name: "IX_RolePermissions_SocieteId",
                table: "RolePermissions");

            migrationBuilder.DropColumn(
                name: "SocieteId",
                table: "RolePermissions");
        }
    }
}
