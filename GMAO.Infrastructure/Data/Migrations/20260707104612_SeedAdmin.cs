using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace GMAO.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class SeedAdmin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DemandesIntervention_Equipements_EquipementId",
                table: "DemandesIntervention");

            migrationBuilder.DropForeignKey(
                name: "FK_DemandesIntervention_Users_DemandeurId",
                table: "DemandesIntervention");

            migrationBuilder.DropForeignKey(
                name: "FK_Documents_Equipements_EquipementId",
                table: "Documents");

            migrationBuilder.DropForeignKey(
                name: "FK_Documents_OrdresTravail_OTId",
                table: "Documents");

            migrationBuilder.DropForeignKey(
                name: "FK_Documents_PlansPreventifs_PlanPreventifId",
                table: "Documents");

            migrationBuilder.DropForeignKey(
                name: "FK_Documents_Users_UploadedById",
                table: "Documents");

            migrationBuilder.DropForeignKey(
                name: "FK_Equipements_Familles_FamilleId",
                table: "Equipements");

            migrationBuilder.DropForeignKey(
                name: "FK_Equipements_Fournisseurs_FournisseurId",
                table: "Equipements");

            migrationBuilder.DropForeignKey(
                name: "FK_Equipements_Localisations_LocalisationId",
                table: "Equipements");

            migrationBuilder.DropForeignKey(
                name: "FK_Historiques_Users_UserId",
                table: "Historiques");

            migrationBuilder.DropForeignKey(
                name: "FK_MouvementsStock_OrdresTravail_OTId",
                table: "MouvementsStock");

            migrationBuilder.DropForeignKey(
                name: "FK_MouvementsStock_Pieces_PieceId",
                table: "MouvementsStock");

            migrationBuilder.DropForeignKey(
                name: "FK_MouvementsStock_Users_UserId",
                table: "MouvementsStock");

            migrationBuilder.DropForeignKey(
                name: "FK_OrdresTravail_Campagnes_CampagneId",
                table: "OrdresTravail");

            migrationBuilder.DropForeignKey(
                name: "FK_OrdresTravail_Equipements_EquipementId",
                table: "OrdresTravail");

            migrationBuilder.DropForeignKey(
                name: "FK_Pieces_FamillesPieces_FamillePieceId",
                table: "Pieces");

            migrationBuilder.DropForeignKey(
                name: "FK_Pieces_Fournisseurs_FournisseurId",
                table: "Pieces");

            migrationBuilder.DropForeignKey(
                name: "FK_Users_Roles_RoleId",
                table: "Users");

            migrationBuilder.AlterColumn<string>(
                name: "Telephone",
                table: "Users",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Prenom",
                table: "Users",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Nom",
                table: "Users",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "Users",
                type: "character varying(150)",
                maxLength: 150,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<int>(
                name: "RoleId1",
                table: "Users",
                type: "integer",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "TachesPreventives",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Nom",
                table: "Roles",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "Roles",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Token",
                table: "RefreshTokens",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "UniteMesure",
                table: "PlansPreventifs",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Titre",
                table: "PlansPreventifs",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "PlansPreventifs",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Unite",
                table: "Pieces",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Reference",
                table: "Pieces",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Emplacement",
                table: "Pieces",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Designation",
                table: "Pieces",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "NumeroOT",
                table: "OrdresTravail",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Instructions",
                table: "OrdresTravail",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "OrdresTravail",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Titre",
                table: "Notifications",
                type: "character varying(150)",
                maxLength: 150,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Message",
                table: "Notifications",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "LienUrl",
                table: "Notifications",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Reference",
                table: "MouvementsStock",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Motif",
                table: "MouvementsStock",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Nom",
                table: "Localisations",
                type: "character varying(150)",
                maxLength: 150,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "Localisations",
                type: "character varying(400)",
                maxLength: 400,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "TempsPasse",
                table: "Interventions",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Solution",
                table: "Interventions",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Observation",
                table: "Interventions",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Diagnostic",
                table: "Interventions",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "CausePanne",
                table: "Interventions",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "TableConcernee",
                table: "Historiques",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "EntityId",
                table: "Historiques",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "AdresseIp",
                table: "Historiques",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Action",
                table: "Historiques",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Telephone",
                table: "Fournisseurs",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Nom",
                table: "Fournisseurs",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "Fournisseurs",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Contact",
                table: "Fournisseurs",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Code",
                table: "Fournisseurs",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Adresse",
                table: "Fournisseurs",
                type: "character varying(400)",
                maxLength: 400,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Nom",
                table: "FamillesPieces",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Nom",
                table: "Familles",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "IconeUrl",
                table: "Familles",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "Familles",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "PhotoUrl",
                table: "Equipements",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "NumeroSerie",
                table: "Equipements",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Notes",
                table: "Equipements",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Modele",
                table: "Equipements",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Marque",
                table: "Equipements",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Designation",
                table: "Equipements",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Code",
                table: "Equipements",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Nom",
                table: "Documents",
                type: "character varying(255)",
                maxLength: 255,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "MimeType",
                table: "Documents",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Chemin",
                table: "Documents",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "PhotoUrl",
                table: "DemandesIntervention",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "DemandesIntervention",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "CommentaireRejet",
                table: "DemandesIntervention",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Nom",
                table: "Competences",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "Competences",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Nom",
                table: "Campagnes",
                type: "character varying(150)",
                maxLength: 150,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "Campagnes",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.InsertData(
                table: "Competences",
                columns: new[] { "Id", "Description", "Nom" },
                values: new object[,]
                {
                    { 1, "Maintenance mécanique et électrique combinée", "Électromécanicien" },
                    { 2, "Réseaux électriques, câblage et moteurs", "Électricien industriel" },
                    { 3, "Programmation API Siemens, Schneider, automates et capteurs", "Automaticien" },
                    { 4, "Soudure tuyauterie inox et structures métalliques", "Soudeur" },
                    { 5, "Maintenance mécanique lourde, réducteurs, convoyeurs", "Mécanicien" }
                });

            migrationBuilder.InsertData(
                table: "Users",
                columns: new[] { "Id", "CreatedAt", "Email", "IsActive", "Nom", "PasswordHash", "Prenom", "RoleId", "RoleId1", "Telephone" },
                values: new object[] { 1, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "admin@tomate.com", true, "Admin", "8c6976e5b5410415bde908bd4dee15dfb167a9c873fc4bb8a81f6f2ab448a918", "GMAO", 1, null, "+21699999999" });

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_RoleId1",
                table: "Users",
                column: "RoleId1");

            migrationBuilder.CreateIndex(
                name: "IX_Roles_Nom",
                table: "Roles",
                column: "Nom",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_Token",
                table: "RefreshTokens",
                column: "Token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Pieces_Reference",
                table: "Pieces",
                column: "Reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Fournisseurs_Code",
                table: "Fournisseurs",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FamillesPieces_Nom",
                table: "FamillesPieces",
                column: "Nom",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Familles_Nom",
                table: "Familles",
                column: "Nom",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Equipements_Code",
                table: "Equipements",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Competences_Nom",
                table: "Competences",
                column: "Nom",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_DemandesIntervention_Equipements_EquipementId",
                table: "DemandesIntervention",
                column: "EquipementId",
                principalTable: "Equipements",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DemandesIntervention_Users_DemandeurId",
                table: "DemandesIntervention",
                column: "DemandeurId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Documents_Equipements_EquipementId",
                table: "Documents",
                column: "EquipementId",
                principalTable: "Equipements",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Documents_OrdresTravail_OTId",
                table: "Documents",
                column: "OTId",
                principalTable: "OrdresTravail",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Documents_PlansPreventifs_PlanPreventifId",
                table: "Documents",
                column: "PlanPreventifId",
                principalTable: "PlansPreventifs",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Documents_Users_UploadedById",
                table: "Documents",
                column: "UploadedById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Equipements_Familles_FamilleId",
                table: "Equipements",
                column: "FamilleId",
                principalTable: "Familles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Equipements_Fournisseurs_FournisseurId",
                table: "Equipements",
                column: "FournisseurId",
                principalTable: "Fournisseurs",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Equipements_Localisations_LocalisationId",
                table: "Equipements",
                column: "LocalisationId",
                principalTable: "Localisations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Historiques_Users_UserId",
                table: "Historiques",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MouvementsStock_OrdresTravail_OTId",
                table: "MouvementsStock",
                column: "OTId",
                principalTable: "OrdresTravail",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_MouvementsStock_Pieces_PieceId",
                table: "MouvementsStock",
                column: "PieceId",
                principalTable: "Pieces",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MouvementsStock_Users_UserId",
                table: "MouvementsStock",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_OrdresTravail_Campagnes_CampagneId",
                table: "OrdresTravail",
                column: "CampagneId",
                principalTable: "Campagnes",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_OrdresTravail_Equipements_EquipementId",
                table: "OrdresTravail",
                column: "EquipementId",
                principalTable: "Equipements",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Pieces_FamillesPieces_FamillePieceId",
                table: "Pieces",
                column: "FamillePieceId",
                principalTable: "FamillesPieces",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Pieces_Fournisseurs_FournisseurId",
                table: "Pieces",
                column: "FournisseurId",
                principalTable: "Fournisseurs",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Roles_RoleId",
                table: "Users",
                column: "RoleId",
                principalTable: "Roles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Roles_RoleId1",
                table: "Users",
                column: "RoleId1",
                principalTable: "Roles",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DemandesIntervention_Equipements_EquipementId",
                table: "DemandesIntervention");

            migrationBuilder.DropForeignKey(
                name: "FK_DemandesIntervention_Users_DemandeurId",
                table: "DemandesIntervention");

            migrationBuilder.DropForeignKey(
                name: "FK_Documents_Equipements_EquipementId",
                table: "Documents");

            migrationBuilder.DropForeignKey(
                name: "FK_Documents_OrdresTravail_OTId",
                table: "Documents");

            migrationBuilder.DropForeignKey(
                name: "FK_Documents_PlansPreventifs_PlanPreventifId",
                table: "Documents");

            migrationBuilder.DropForeignKey(
                name: "FK_Documents_Users_UploadedById",
                table: "Documents");

            migrationBuilder.DropForeignKey(
                name: "FK_Equipements_Familles_FamilleId",
                table: "Equipements");

            migrationBuilder.DropForeignKey(
                name: "FK_Equipements_Fournisseurs_FournisseurId",
                table: "Equipements");

            migrationBuilder.DropForeignKey(
                name: "FK_Equipements_Localisations_LocalisationId",
                table: "Equipements");

            migrationBuilder.DropForeignKey(
                name: "FK_Historiques_Users_UserId",
                table: "Historiques");

            migrationBuilder.DropForeignKey(
                name: "FK_MouvementsStock_OrdresTravail_OTId",
                table: "MouvementsStock");

            migrationBuilder.DropForeignKey(
                name: "FK_MouvementsStock_Pieces_PieceId",
                table: "MouvementsStock");

            migrationBuilder.DropForeignKey(
                name: "FK_MouvementsStock_Users_UserId",
                table: "MouvementsStock");

            migrationBuilder.DropForeignKey(
                name: "FK_OrdresTravail_Campagnes_CampagneId",
                table: "OrdresTravail");

            migrationBuilder.DropForeignKey(
                name: "FK_OrdresTravail_Equipements_EquipementId",
                table: "OrdresTravail");

            migrationBuilder.DropForeignKey(
                name: "FK_Pieces_FamillesPieces_FamillePieceId",
                table: "Pieces");

            migrationBuilder.DropForeignKey(
                name: "FK_Pieces_Fournisseurs_FournisseurId",
                table: "Pieces");

            migrationBuilder.DropForeignKey(
                name: "FK_Users_Roles_RoleId",
                table: "Users");

            migrationBuilder.DropForeignKey(
                name: "FK_Users_Roles_RoleId1",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_Email",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_RoleId1",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Roles_Nom",
                table: "Roles");

            migrationBuilder.DropIndex(
                name: "IX_RefreshTokens_Token",
                table: "RefreshTokens");

            migrationBuilder.DropIndex(
                name: "IX_Pieces_Reference",
                table: "Pieces");

            migrationBuilder.DropIndex(
                name: "IX_Fournisseurs_Code",
                table: "Fournisseurs");

            migrationBuilder.DropIndex(
                name: "IX_FamillesPieces_Nom",
                table: "FamillesPieces");

            migrationBuilder.DropIndex(
                name: "IX_Familles_Nom",
                table: "Familles");

            migrationBuilder.DropIndex(
                name: "IX_Equipements_Code",
                table: "Equipements");

            migrationBuilder.DropIndex(
                name: "IX_Competences_Nom",
                table: "Competences");

            migrationBuilder.DeleteData(
                table: "Competences",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "Competences",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "Competences",
                keyColumn: "Id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "Competences",
                keyColumn: "Id",
                keyValue: 4);

            migrationBuilder.DeleteData(
                table: "Competences",
                keyColumn: "Id",
                keyValue: 5);

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DropColumn(
                name: "RoleId1",
                table: "Users");

            migrationBuilder.AlterColumn<string>(
                name: "Telephone",
                table: "Users",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Prenom",
                table: "Users",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "Nom",
                table: "Users",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "Users",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(150)",
                oldMaxLength: 150);

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "TachesPreventives",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500);

            migrationBuilder.AlterColumn<string>(
                name: "Nom",
                table: "Roles",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "Roles",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Token",
                table: "RefreshTokens",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500);

            migrationBuilder.AlterColumn<string>(
                name: "UniteMesure",
                table: "PlansPreventifs",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Titre",
                table: "PlansPreventifs",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "PlansPreventifs",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(1000)",
                oldMaxLength: 1000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Unite",
                table: "Pieces",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Reference",
                table: "Pieces",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "Emplacement",
                table: "Pieces",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Designation",
                table: "Pieces",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "NumeroOT",
                table: "OrdresTravail",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(30)",
                oldMaxLength: 30);

            migrationBuilder.AlterColumn<string>(
                name: "Instructions",
                table: "OrdresTravail",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(2000)",
                oldMaxLength: 2000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "OrdresTravail",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(1000)",
                oldMaxLength: 1000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Titre",
                table: "Notifications",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(150)",
                oldMaxLength: 150);

            migrationBuilder.AlterColumn<string>(
                name: "Message",
                table: "Notifications",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500);

            migrationBuilder.AlterColumn<string>(
                name: "LienUrl",
                table: "Notifications",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(255)",
                oldMaxLength: 255,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Reference",
                table: "MouvementsStock",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Motif",
                table: "MouvementsStock",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Nom",
                table: "Localisations",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(150)",
                oldMaxLength: 150);

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "Localisations",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(400)",
                oldMaxLength: 400,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "TempsPasse",
                table: "Interventions",
                type: "numeric",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Solution",
                table: "Interventions",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(1000)",
                oldMaxLength: 1000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Observation",
                table: "Interventions",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(1000)",
                oldMaxLength: 1000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Diagnostic",
                table: "Interventions",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(1000)",
                oldMaxLength: 1000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "CausePanne",
                table: "Interventions",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(1000)",
                oldMaxLength: 1000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "TableConcernee",
                table: "Historiques",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "EntityId",
                table: "Historiques",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "AdresseIp",
                table: "Historiques",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Action",
                table: "Historiques",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "Telephone",
                table: "Fournisseurs",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Nom",
                table: "Fournisseurs",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "Fournisseurs",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(150)",
                oldMaxLength: 150,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Contact",
                table: "Fournisseurs",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(150)",
                oldMaxLength: 150,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Code",
                table: "Fournisseurs",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<string>(
                name: "Adresse",
                table: "Fournisseurs",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(400)",
                oldMaxLength: 400,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Nom",
                table: "FamillesPieces",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "Nom",
                table: "Familles",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "IconeUrl",
                table: "Familles",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "Familles",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(300)",
                oldMaxLength: 300,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "PhotoUrl",
                table: "Equipements",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "NumeroSerie",
                table: "Equipements",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Notes",
                table: "Equipements",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(1000)",
                oldMaxLength: 1000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Modele",
                table: "Equipements",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Marque",
                table: "Equipements",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Designation",
                table: "Equipements",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "Code",
                table: "Equipements",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(30)",
                oldMaxLength: 30);

            migrationBuilder.AlterColumn<string>(
                name: "Nom",
                table: "Documents",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(255)",
                oldMaxLength: 255);

            migrationBuilder.AlterColumn<string>(
                name: "MimeType",
                table: "Documents",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Chemin",
                table: "Documents",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500);

            migrationBuilder.AlterColumn<string>(
                name: "PhotoUrl",
                table: "DemandesIntervention",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "DemandesIntervention",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(1000)",
                oldMaxLength: 1000);

            migrationBuilder.AlterColumn<string>(
                name: "CommentaireRejet",
                table: "DemandesIntervention",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Nom",
                table: "Competences",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "Competences",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(300)",
                oldMaxLength: 300,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Nom",
                table: "Campagnes",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(150)",
                oldMaxLength: 150);

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "Campagnes",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_DemandesIntervention_Equipements_EquipementId",
                table: "DemandesIntervention",
                column: "EquipementId",
                principalTable: "Equipements",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_DemandesIntervention_Users_DemandeurId",
                table: "DemandesIntervention",
                column: "DemandeurId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Documents_Equipements_EquipementId",
                table: "Documents",
                column: "EquipementId",
                principalTable: "Equipements",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Documents_OrdresTravail_OTId",
                table: "Documents",
                column: "OTId",
                principalTable: "OrdresTravail",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Documents_PlansPreventifs_PlanPreventifId",
                table: "Documents",
                column: "PlanPreventifId",
                principalTable: "PlansPreventifs",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Documents_Users_UploadedById",
                table: "Documents",
                column: "UploadedById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Equipements_Familles_FamilleId",
                table: "Equipements",
                column: "FamilleId",
                principalTable: "Familles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Equipements_Fournisseurs_FournisseurId",
                table: "Equipements",
                column: "FournisseurId",
                principalTable: "Fournisseurs",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Equipements_Localisations_LocalisationId",
                table: "Equipements",
                column: "LocalisationId",
                principalTable: "Localisations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Historiques_Users_UserId",
                table: "Historiques",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_MouvementsStock_OrdresTravail_OTId",
                table: "MouvementsStock",
                column: "OTId",
                principalTable: "OrdresTravail",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_MouvementsStock_Pieces_PieceId",
                table: "MouvementsStock",
                column: "PieceId",
                principalTable: "Pieces",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_MouvementsStock_Users_UserId",
                table: "MouvementsStock",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_OrdresTravail_Campagnes_CampagneId",
                table: "OrdresTravail",
                column: "CampagneId",
                principalTable: "Campagnes",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_OrdresTravail_Equipements_EquipementId",
                table: "OrdresTravail",
                column: "EquipementId",
                principalTable: "Equipements",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Pieces_FamillesPieces_FamillePieceId",
                table: "Pieces",
                column: "FamillePieceId",
                principalTable: "FamillesPieces",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Pieces_Fournisseurs_FournisseurId",
                table: "Pieces",
                column: "FournisseurId",
                principalTable: "Fournisseurs",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Roles_RoleId",
                table: "Users",
                column: "RoleId",
                principalTable: "Roles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
