using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace GMAO.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Campagnes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Nom = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    DateDebut = table.Column<DateOnly>(type: "date", nullable: false),
                    DateFin = table.Column<DateOnly>(type: "date", nullable: false),
                    Etat = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Campagnes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Familles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Nom = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    IconeUrl = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Familles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FamillesPieces",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Nom = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FamillesPieces", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Fournisseurs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "text", nullable: false),
                    Nom = table.Column<string>(type: "text", nullable: false),
                    Adresse = table.Column<string>(type: "text", nullable: true),
                    Telephone = table.Column<string>(type: "text", nullable: true),
                    Email = table.Column<string>(type: "text", nullable: true),
                    Contact = table.Column<string>(type: "text", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Fournisseurs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Localisations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Nom = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    ParentId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Localisations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Localisations_Localisations_ParentId",
                        column: x => x.ParentId,
                        principalTable: "Localisations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Roles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Nom = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Roles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Pieces",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Reference = table.Column<string>(type: "text", nullable: false),
                    Designation = table.Column<string>(type: "text", nullable: false),
                    FamillePieceId = table.Column<int>(type: "integer", nullable: true),
                    StockActuel = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    StockMinimum = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    StockMaximum = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    Unite = table.Column<string>(type: "text", nullable: true),
                    PrixUnitaire = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    FournisseurId = table.Column<int>(type: "integer", nullable: true),
                    Emplacement = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pieces", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Pieces_FamillesPieces_FamillePieceId",
                        column: x => x.FamillePieceId,
                        principalTable: "FamillesPieces",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Pieces_Fournisseurs_FournisseurId",
                        column: x => x.FournisseurId,
                        principalTable: "Fournisseurs",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Equipements",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "text", nullable: false),
                    Designation = table.Column<string>(type: "text", nullable: false),
                    FamilleId = table.Column<int>(type: "integer", nullable: false),
                    LocalisationId = table.Column<int>(type: "integer", nullable: false),
                    Marque = table.Column<string>(type: "text", nullable: true),
                    Modele = table.Column<string>(type: "text", nullable: true),
                    NumeroSerie = table.Column<string>(type: "text", nullable: true),
                    DateAchat = table.Column<DateOnly>(type: "date", nullable: true),
                    DateMiseEnService = table.Column<DateOnly>(type: "date", nullable: true),
                    DateFinGarantie = table.Column<DateOnly>(type: "date", nullable: true),
                    Criticite = table.Column<int>(type: "integer", nullable: false),
                    Etat = table.Column<int>(type: "integer", nullable: false),
                    FournisseurId = table.Column<int>(type: "integer", nullable: true),
                    PhotoUrl = table.Column<string>(type: "text", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Equipements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Equipements_Familles_FamilleId",
                        column: x => x.FamilleId,
                        principalTable: "Familles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Equipements_Fournisseurs_FournisseurId",
                        column: x => x.FournisseurId,
                        principalTable: "Fournisseurs",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Equipements_Localisations_LocalisationId",
                        column: x => x.LocalisationId,
                        principalTable: "Localisations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Nom = table.Column<string>(type: "text", nullable: false),
                    Prenom = table.Column<string>(type: "text", nullable: false),
                    Email = table.Column<string>(type: "text", nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: false),
                    Telephone = table.Column<string>(type: "text", nullable: true),
                    RoleId = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Users_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlansPreventifs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EquipementId = table.Column<int>(type: "integer", nullable: false),
                    Titre = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    TypeDeclenchement = table.Column<int>(type: "integer", nullable: false),
                    Frequence = table.Column<int>(type: "integer", nullable: false),
                    UniteMesure = table.Column<string>(type: "text", nullable: true),
                    DerniereDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ProchaineDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Actif = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlansPreventifs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlansPreventifs_Equipements_EquipementId",
                        column: x => x.EquipementId,
                        principalTable: "Equipements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DemandesIntervention",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EquipementId = table.Column<int>(type: "integer", nullable: false),
                    DemandeurId = table.Column<int>(type: "integer", nullable: false),
                    DatePanne = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    Priorite = table.Column<int>(type: "integer", nullable: false),
                    Statut = table.Column<int>(type: "integer", nullable: false),
                    PhotoUrl = table.Column<string>(type: "text", nullable: true),
                    CommentaireRejet = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DemandesIntervention", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DemandesIntervention_Equipements_EquipementId",
                        column: x => x.EquipementId,
                        principalTable: "Equipements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DemandesIntervention_Users_DemandeurId",
                        column: x => x.DemandeurId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Historiques",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    Action = table.Column<string>(type: "text", nullable: false),
                    TableConcernee = table.Column<string>(type: "text", nullable: false),
                    EntityId = table.Column<string>(type: "text", nullable: true),
                    AnciennesValeurs = table.Column<string>(type: "text", nullable: true),
                    NouvellesValeurs = table.Column<string>(type: "text", nullable: true),
                    AdresseIp = table.Column<string>(type: "text", nullable: true),
                    Date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Historiques", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Historiques_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Notifications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    Titre = table.Column<string>(type: "text", nullable: false),
                    Message = table.Column<string>(type: "text", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Lu = table.Column<bool>(type: "boolean", nullable: false),
                    LuAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LienUrl = table.Column<string>(type: "text", nullable: true),
                    Date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Notifications_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RefreshTokens",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    Token = table.Column<string>(type: "text", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsRevoked = table.Column<bool>(type: "boolean", nullable: false),
                    ReplacedByToken = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RefreshTokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RefreshTokens_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TachesPreventives",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PlanId = table.Column<int>(type: "integer", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    Ordre = table.Column<int>(type: "integer", nullable: false),
                    DureeEstimeeMinutes = table.Column<int>(type: "integer", nullable: true),
                    EstObligatoire = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TachesPreventives", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TachesPreventives_PlansPreventifs_PlanId",
                        column: x => x.PlanId,
                        principalTable: "PlansPreventifs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OrdresTravail",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    NumeroOT = table.Column<string>(type: "text", nullable: false),
                    DemandeId = table.Column<int>(type: "integer", nullable: true),
                    EquipementId = table.Column<int>(type: "integer", nullable: false),
                    ResponsableId = table.Column<int>(type: "integer", nullable: false),
                    TechnicienId = table.Column<int>(type: "integer", nullable: true),
                    Priorite = table.Column<int>(type: "integer", nullable: false),
                    TypeMaintenance = table.Column<int>(type: "integer", nullable: false),
                    Statut = table.Column<int>(type: "integer", nullable: false),
                    DateCreation = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DateDebutPrevue = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DateFinPrevue = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DateDebutReelle = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DateFinReelle = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Instructions = table.Column<string>(type: "text", nullable: true),
                    CoutMainOeuvre = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    CoutPieces = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    CoutSousTraitance = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    CampagneId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrdresTravail", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrdresTravail_Campagnes_CampagneId",
                        column: x => x.CampagneId,
                        principalTable: "Campagnes",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_OrdresTravail_DemandesIntervention_DemandeId",
                        column: x => x.DemandeId,
                        principalTable: "DemandesIntervention",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_OrdresTravail_Equipements_EquipementId",
                        column: x => x.EquipementId,
                        principalTable: "Equipements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_OrdresTravail_Users_ResponsableId",
                        column: x => x.ResponsableId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OrdresTravail_Users_TechnicienId",
                        column: x => x.TechnicienId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Documents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Nom = table.Column<string>(type: "text", nullable: false),
                    Chemin = table.Column<string>(type: "text", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    TailleFichierOctets = table.Column<long>(type: "bigint", nullable: false),
                    MimeType = table.Column<string>(type: "text", nullable: true),
                    EquipementId = table.Column<int>(type: "integer", nullable: true),
                    OTId = table.Column<int>(type: "integer", nullable: true),
                    PlanPreventifId = table.Column<int>(type: "integer", nullable: true),
                    UploadedById = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Documents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Documents_Equipements_EquipementId",
                        column: x => x.EquipementId,
                        principalTable: "Equipements",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Documents_OrdresTravail_OTId",
                        column: x => x.OTId,
                        principalTable: "OrdresTravail",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Documents_PlansPreventifs_PlanPreventifId",
                        column: x => x.PlanPreventifId,
                        principalTable: "PlansPreventifs",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Documents_Users_UploadedById",
                        column: x => x.UploadedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Interventions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OTId = table.Column<int>(type: "integer", nullable: false),
                    Diagnostic = table.Column<string>(type: "text", nullable: true),
                    CausePanne = table.Column<string>(type: "text", nullable: true),
                    Solution = table.Column<string>(type: "text", nullable: true),
                    TempsPasse = table.Column<decimal>(type: "numeric", nullable: true),
                    Observation = table.Column<string>(type: "text", nullable: true),
                    DateDebut = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DateFin = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Statut = table.Column<int>(type: "integer", nullable: false),
                    DateIntervention = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Interventions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Interventions_OrdresTravail_OTId",
                        column: x => x.OTId,
                        principalTable: "OrdresTravail",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MouvementsStock",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PieceId = table.Column<int>(type: "integer", nullable: false),
                    OTId = table.Column<int>(type: "integer", nullable: true),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Quantite = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    PrixUnitaire = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    PrixTotal = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    Reference = table.Column<string>(type: "text", nullable: true),
                    Motif = table.Column<string>(type: "text", nullable: true),
                    Date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MouvementsStock", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MouvementsStock_OrdresTravail_OTId",
                        column: x => x.OTId,
                        principalTable: "OrdresTravail",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_MouvementsStock_Pieces_PieceId",
                        column: x => x.PieceId,
                        principalTable: "Pieces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MouvementsStock_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Familles",
                columns: new[] { "Id", "Description", "IconeUrl", "Nom" },
                values: new object[,]
                {
                    { 1, "Pompes hydrauliques et centrifuges", null, "Pompes" },
                    { 2, "Chaudières et générateurs de vapeur", null, "Chaudières" },
                    { 3, "Convoyeurs et tapis transporteurs", null, "Convoyeurs" },
                    { 4, "Compresseurs d'air et de gaz", null, "Compresseurs" },
                    { 5, "Moteurs électriques AC/DC", null, "Moteurs Electriques" },
                    { 6, "Groupes frigorifiques et chambres froides", null, "Réfrigération" },
                    { 7, "Autoclaves et tunnels de stérilisation", null, "Stérilisateurs" },
                    { 8, "Machines de remplissage et dosage", null, "Remplisseuses" }
                });

            migrationBuilder.InsertData(
                table: "FamillesPieces",
                columns: new[] { "Id", "Description", "Nom" },
                values: new object[,]
                {
                    { 1, null, "Roulements & Paliers" },
                    { 2, null, "Joints & Garnitures" },
                    { 3, null, "Courroies & Chaînes" },
                    { 4, null, "Filtres" },
                    { 5, null, "Lubrifiants & Huiles" },
                    { 6, null, "Composants Electriques" },
                    { 7, null, "Visserie & Boulonnerie" }
                });

            migrationBuilder.InsertData(
                table: "Roles",
                columns: new[] { "Id", "Description", "Nom" },
                values: new object[,]
                {
                    { 1, "Administrateur système avec accès total", "Admin" },
                    { 2, "Gestion des OT, validation des demandes et rapports", "Responsable Maintenance" },
                    { 3, "Supervision des techniciens et suivi des OT", "Chef Equipe" },
                    { 4, "Exécution des interventions", "Technicien" },
                    { 5, "Déclaration des pannes", "Production" },
                    { 6, "Gestion du stock de pièces de rechange", "Magasinier" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_DemandesIntervention_DemandeurId",
                table: "DemandesIntervention",
                column: "DemandeurId");

            migrationBuilder.CreateIndex(
                name: "IX_DemandesIntervention_EquipementId",
                table: "DemandesIntervention",
                column: "EquipementId");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_EquipementId",
                table: "Documents",
                column: "EquipementId");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_OTId",
                table: "Documents",
                column: "OTId");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_PlanPreventifId",
                table: "Documents",
                column: "PlanPreventifId");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_UploadedById",
                table: "Documents",
                column: "UploadedById");

            migrationBuilder.CreateIndex(
                name: "IX_Equipements_FamilleId",
                table: "Equipements",
                column: "FamilleId");

            migrationBuilder.CreateIndex(
                name: "IX_Equipements_FournisseurId",
                table: "Equipements",
                column: "FournisseurId");

            migrationBuilder.CreateIndex(
                name: "IX_Equipements_LocalisationId",
                table: "Equipements",
                column: "LocalisationId");

            migrationBuilder.CreateIndex(
                name: "IX_Historiques_UserId",
                table: "Historiques",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Interventions_OTId",
                table: "Interventions",
                column: "OTId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Localisations_ParentId",
                table: "Localisations",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_MouvementsStock_OTId",
                table: "MouvementsStock",
                column: "OTId");

            migrationBuilder.CreateIndex(
                name: "IX_MouvementsStock_PieceId",
                table: "MouvementsStock",
                column: "PieceId");

            migrationBuilder.CreateIndex(
                name: "IX_MouvementsStock_UserId",
                table: "MouvementsStock",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_UserId",
                table: "Notifications",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_OrdresTravail_CampagneId",
                table: "OrdresTravail",
                column: "CampagneId");

            migrationBuilder.CreateIndex(
                name: "IX_OrdresTravail_DemandeId",
                table: "OrdresTravail",
                column: "DemandeId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrdresTravail_EquipementId",
                table: "OrdresTravail",
                column: "EquipementId");

            migrationBuilder.CreateIndex(
                name: "IX_OrdresTravail_NumeroOT",
                table: "OrdresTravail",
                column: "NumeroOT",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrdresTravail_ResponsableId",
                table: "OrdresTravail",
                column: "ResponsableId");

            migrationBuilder.CreateIndex(
                name: "IX_OrdresTravail_TechnicienId",
                table: "OrdresTravail",
                column: "TechnicienId");

            migrationBuilder.CreateIndex(
                name: "IX_Pieces_FamillePieceId",
                table: "Pieces",
                column: "FamillePieceId");

            migrationBuilder.CreateIndex(
                name: "IX_Pieces_FournisseurId",
                table: "Pieces",
                column: "FournisseurId");

            migrationBuilder.CreateIndex(
                name: "IX_PlansPreventifs_EquipementId",
                table: "PlansPreventifs",
                column: "EquipementId");

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_UserId",
                table: "RefreshTokens",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_TachesPreventives_PlanId",
                table: "TachesPreventives",
                column: "PlanId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_RoleId",
                table: "Users",
                column: "RoleId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Documents");

            migrationBuilder.DropTable(
                name: "Historiques");

            migrationBuilder.DropTable(
                name: "Interventions");

            migrationBuilder.DropTable(
                name: "MouvementsStock");

            migrationBuilder.DropTable(
                name: "Notifications");

            migrationBuilder.DropTable(
                name: "RefreshTokens");

            migrationBuilder.DropTable(
                name: "TachesPreventives");

            migrationBuilder.DropTable(
                name: "OrdresTravail");

            migrationBuilder.DropTable(
                name: "Pieces");

            migrationBuilder.DropTable(
                name: "PlansPreventifs");

            migrationBuilder.DropTable(
                name: "Campagnes");

            migrationBuilder.DropTable(
                name: "DemandesIntervention");

            migrationBuilder.DropTable(
                name: "FamillesPieces");

            migrationBuilder.DropTable(
                name: "Equipements");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "Familles");

            migrationBuilder.DropTable(
                name: "Fournisseurs");

            migrationBuilder.DropTable(
                name: "Localisations");

            migrationBuilder.DropTable(
                name: "Roles");
        }
    }
}
