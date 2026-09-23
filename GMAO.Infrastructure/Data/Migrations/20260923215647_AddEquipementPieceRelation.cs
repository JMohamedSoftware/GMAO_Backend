using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GMAO.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEquipementPieceRelation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EquipementPiece",
                columns: table => new
                {
                    EquipementsId = table.Column<int>(type: "integer", nullable: false),
                    PiecesId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EquipementPiece", x => new { x.EquipementsId, x.PiecesId });
                    table.ForeignKey(
                        name: "FK_EquipementPiece_Equipements_EquipementsId",
                        column: x => x.EquipementsId,
                        principalTable: "Equipements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EquipementPiece_Pieces_PiecesId",
                        column: x => x.PiecesId,
                        principalTable: "Pieces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EquipementPiece_PiecesId",
                table: "EquipementPiece",
                column: "PiecesId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EquipementPiece");
        }
    }
}
