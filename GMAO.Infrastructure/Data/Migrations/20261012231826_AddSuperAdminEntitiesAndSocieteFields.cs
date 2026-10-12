using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace GMAO.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSuperAdminEntitiesAndSocieteFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Category",
                table: "Societes",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Gouvernorat",
                table: "Societes",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LogoUrl",
                table: "Societes",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ManagerNom",
                table: "Societes",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxEquipements",
                table: "Societes",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MaxSites",
                table: "Societes",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<double>(
                name: "MaxStorageGb",
                table: "Societes",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<int>(
                name: "MaxUsers",
                table: "Societes",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<double>(
                name: "StorageUsedGb",
                table: "Societes",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<decimal>(
                name: "SubscriptionAmount",
                table: "Societes",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTime>(
                name: "SubscriptionEndDate",
                table: "Societes",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SubscriptionStartDate",
                table: "Societes",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SubscriptionStatus",
                table: "Societes",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Telephone",
                table: "Societes",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Ville",
                table: "Societes",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Factures",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    NumeroFacture = table.Column<string>(type: "text", nullable: false),
                    SocieteId = table.Column<int>(type: "integer", nullable: false),
                    SocieteNom = table.Column<string>(type: "text", nullable: false),
                    MontantHT = table.Column<decimal>(type: "numeric", nullable: false),
                    TauxTVA = table.Column<decimal>(type: "numeric", nullable: false),
                    MontantTTC = table.Column<decimal>(type: "numeric", nullable: false),
                    Statut = table.Column<string>(type: "text", nullable: false),
                    MethodePaiement = table.Column<string>(type: "text", nullable: false),
                    DateEmission = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DateEcheance = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DatePaiement = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Periode = table.Column<string>(type: "text", nullable: false),
                    Plan = table.Column<string>(type: "text", nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Factures", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Factures_Societes_SocieteId",
                        column: x => x.SocieteId,
                        principalTable: "Societes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "JournauxSuperAdmin",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DateHeure = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Niveau = table.Column<string>(type: "text", nullable: false),
                    Action = table.Column<string>(type: "text", nullable: false),
                    Categorie = table.Column<string>(type: "text", nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: true),
                    UserNom = table.Column<string>(type: "text", nullable: false),
                    UserRole = table.Column<string>(type: "text", nullable: false),
                    SocieteNom = table.Column<string>(type: "text", nullable: false),
                    AdresseIp = table.Column<string>(type: "text", nullable: true),
                    Navigateur = table.Column<string>(type: "text", nullable: true),
                    Details = table.Column<string>(type: "text", nullable: false),
                    Statut = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JournauxSuperAdmin", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PlansOffres",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "text", nullable: false),
                    Nom = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    PrixMensuel = table.Column<decimal>(type: "numeric", nullable: false),
                    PrixAnnuel = table.Column<decimal>(type: "numeric", nullable: false),
                    MaxSites = table.Column<int>(type: "integer", nullable: false),
                    MaxUsers = table.Column<int>(type: "integer", nullable: false),
                    MaxEquipements = table.Column<int>(type: "integer", nullable: false),
                    MaxStorageGb = table.Column<int>(type: "integer", nullable: false),
                    SupportLevel = table.Column<string>(type: "text", nullable: false),
                    FonctionnalitesJson = table.Column<string>(type: "text", nullable: false),
                    IsPopular = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlansOffres", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PlatformSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    NomPlateforme = table.Column<string>(type: "text", nullable: false),
                    VersionPlateforme = table.Column<string>(type: "text", nullable: false),
                    UrlPlateforme = table.Column<string>(type: "text", nullable: false),
                    SupportEmail = table.Column<string>(type: "text", nullable: false),
                    SupportTelephone = table.Column<string>(type: "text", nullable: false),
                    Devise = table.Column<string>(type: "text", nullable: false),
                    TauxTvaParDefaut = table.Column<decimal>(type: "numeric", nullable: false),
                    MaintenanceMode = table.Column<bool>(type: "boolean", nullable: false),
                    AllowNewRegistrations = table.Column<bool>(type: "boolean", nullable: false),
                    DefaultTrialDays = table.Column<int>(type: "integer", nullable: false),
                    SmtpHost = table.Column<string>(type: "text", nullable: false),
                    SmtpPort = table.Column<int>(type: "integer", nullable: false),
                    SmtpUser = table.Column<string>(type: "text", nullable: false),
                    SmtpSsl = table.Column<bool>(type: "boolean", nullable: false),
                    StorageProvider = table.Column<string>(type: "text", nullable: false),
                    BackupAuto = table.Column<bool>(type: "boolean", nullable: false),
                    BackupFrequence = table.Column<string>(type: "text", nullable: false),
                    RetentionJours = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlatformSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SitesUsines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SocieteId = table.Column<int>(type: "integer", nullable: false),
                    Nom = table.Column<string>(type: "text", nullable: false),
                    CodeSite = table.Column<string>(type: "text", nullable: false),
                    Ville = table.Column<string>(type: "text", nullable: false),
                    Gouvernorat = table.Column<string>(type: "text", nullable: false),
                    Adresse = table.Column<string>(type: "text", nullable: true),
                    Type = table.Column<string>(type: "text", nullable: false),
                    Statut = table.Column<string>(type: "text", nullable: false),
                    CapaciteProduction = table.Column<string>(type: "text", nullable: false),
                    NombreEquipements = table.Column<int>(type: "integer", nullable: false),
                    NombreUtilisateurs = table.Column<int>(type: "integer", nullable: false),
                    Responsable = table.Column<string>(type: "text", nullable: true),
                    Telephone = table.Column<string>(type: "text", nullable: true),
                    Email = table.Column<string>(type: "text", nullable: true),
                    PhotoUrl = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SitesUsines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SitesUsines_Societes_SocieteId",
                        column: x => x.SocieteId,
                        principalTable: "Societes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Factures_SocieteId",
                table: "Factures",
                column: "SocieteId");

            migrationBuilder.CreateIndex(
                name: "IX_SitesUsines_SocieteId",
                table: "SitesUsines",
                column: "SocieteId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Factures");

            migrationBuilder.DropTable(
                name: "JournauxSuperAdmin");

            migrationBuilder.DropTable(
                name: "PlansOffres");

            migrationBuilder.DropTable(
                name: "PlatformSettings");

            migrationBuilder.DropTable(
                name: "SitesUsines");

            migrationBuilder.DropColumn(
                name: "Category",
                table: "Societes");

            migrationBuilder.DropColumn(
                name: "Gouvernorat",
                table: "Societes");

            migrationBuilder.DropColumn(
                name: "LogoUrl",
                table: "Societes");

            migrationBuilder.DropColumn(
                name: "ManagerNom",
                table: "Societes");

            migrationBuilder.DropColumn(
                name: "MaxEquipements",
                table: "Societes");

            migrationBuilder.DropColumn(
                name: "MaxSites",
                table: "Societes");

            migrationBuilder.DropColumn(
                name: "MaxStorageGb",
                table: "Societes");

            migrationBuilder.DropColumn(
                name: "MaxUsers",
                table: "Societes");

            migrationBuilder.DropColumn(
                name: "StorageUsedGb",
                table: "Societes");

            migrationBuilder.DropColumn(
                name: "SubscriptionAmount",
                table: "Societes");

            migrationBuilder.DropColumn(
                name: "SubscriptionEndDate",
                table: "Societes");

            migrationBuilder.DropColumn(
                name: "SubscriptionStartDate",
                table: "Societes");

            migrationBuilder.DropColumn(
                name: "SubscriptionStatus",
                table: "Societes");

            migrationBuilder.DropColumn(
                name: "Telephone",
                table: "Societes");

            migrationBuilder.DropColumn(
                name: "Ville",
                table: "Societes");
        }
    }
}
