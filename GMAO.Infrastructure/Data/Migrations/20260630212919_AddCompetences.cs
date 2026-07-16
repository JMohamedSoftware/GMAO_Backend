using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace GMAO.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCompetences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Competences",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Nom = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Competences", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TechnicienCompetences",
                columns: table => new
                {
                    TechnicienId = table.Column<int>(type: "integer", nullable: false),
                    CompetenceId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TechnicienCompetences", x => new { x.TechnicienId, x.CompetenceId });
                    table.ForeignKey(
                        name: "FK_TechnicienCompetences_Competences_CompetenceId",
                        column: x => x.CompetenceId,
                        principalTable: "Competences",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TechnicienCompetences_Users_TechnicienId",
                        column: x => x.TechnicienId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TechnicienCompetences_CompetenceId",
                table: "TechnicienCompetences",
                column: "CompetenceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TechnicienCompetences");

            migrationBuilder.DropTable(
                name: "Competences");
        }
    }
}
