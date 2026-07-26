using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace GMAO.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEquipes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "EquipeId",
                table: "Users",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Equipes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Nom = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    SocieteId = table.Column<int>(type: "integer", nullable: false),
                    ChefEquipeId = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Equipes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Equipes_Societes_SocieteId",
                        column: x => x.SocieteId,
                        principalTable: "Societes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Equipes_Users_ChefEquipeId",
                        column: x => x.ChefEquipeId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Users_EquipeId",
                table: "Users",
                column: "EquipeId");

            migrationBuilder.CreateIndex(
                name: "IX_Equipes_ChefEquipeId",
                table: "Equipes",
                column: "ChefEquipeId");

            migrationBuilder.CreateIndex(
                name: "IX_Equipes_SocieteId",
                table: "Equipes",
                column: "SocieteId");

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Equipes_EquipeId",
                table: "Users",
                column: "EquipeId",
                principalTable: "Equipes",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Users_Equipes_EquipeId",
                table: "Users");

            migrationBuilder.DropTable(
                name: "Equipes");

            migrationBuilder.DropIndex(
                name: "IX_Users_EquipeId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "EquipeId",
                table: "Users");
        }
    }
}
