using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace GMAO.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMultiTenant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SocieteId",
                table: "Users",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Societes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CodeTenant = table.Column<string>(type: "text", nullable: false),
                    Nom = table.Column<string>(type: "text", nullable: false),
                    Adresse = table.Column<string>(type: "text", nullable: true),
                    EmailContact = table.Column<string>(type: "text", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Societes", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "Societes",
                columns: new[] { "Id", "Adresse", "CodeTenant", "CreatedAt", "EmailContact", "IsActive", "Nom" },
                values: new object[] { 1, null, "tenant-midi", new DateTime(2026, 1, 10, 12, 0, 0, 0, DateTimeKind.Utc), "admin@midi.com", true, "Conserverie du Midi S.A." });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 1,
                column: "SocieteId",
                value: 1);

            migrationBuilder.CreateIndex(
                name: "IX_Users_SocieteId",
                table: "Users",
                column: "SocieteId");

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Societes_SocieteId",
                table: "Users",
                column: "SocieteId",
                principalTable: "Societes",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Users_Societes_SocieteId",
                table: "Users");

            migrationBuilder.DropTable(
                name: "Societes");

            migrationBuilder.DropIndex(
                name: "IX_Users_SocieteId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "SocieteId",
                table: "Users");
        }
    }
}
