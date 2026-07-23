using GMAO.Domain.Entities;
using GMAO.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace GMAO.Infrastructure.Data;

public class GmaoDbContext : DbContext
{
    public GmaoDbContext(DbContextOptions<GmaoDbContext> options) : base(options) { }

    // ── Auth & Utilisateurs ─────────────────────────────────────────────────
    public DbSet<Role> Roles { get; set; }
    public DbSet<RolePermission> RolePermissions { get; set; }
    public DbSet<Societe> Societes { get; set; }
    public DbSet<User> Users { get; set; }
    public DbSet<RefreshToken> RefreshTokens { get; set; }
    public DbSet<Competence> Competences { get; set; }
    public DbSet<TechnicienCompetence> TechnicienCompetences { get; set; }

    // ── Référentiel ─────────────────────────────────────────────────────────
    public DbSet<Famille> Familles { get; set; }
    public DbSet<Localisation> Localisations { get; set; }
    public DbSet<Fournisseur> Fournisseurs { get; set; }

    // ── Equipements ─────────────────────────────────────────────────────────
    public DbSet<Equipement> Equipements { get; set; }

    // ── Interventions ───────────────────────────────────────────────────────
    public DbSet<DemandeIntervention> DemandesIntervention { get; set; }
    public DbSet<OrdresTravail> OrdresTravail { get; set; }
    public DbSet<Intervention> Interventions { get; set; }

    // ── Maintenance Préventive ───────────────────────────────────────────────
    public DbSet<PlanPreventif> PlansPreventifs { get; set; }
    public DbSet<TachePreventive> TachesPreventives { get; set; }

    // ── Stock ────────────────────────────────────────────────────────────────
    public DbSet<FamillePiece> FamillesPieces { get; set; }
    public DbSet<Piece> Pieces { get; set; }
    public DbSet<MouvementStock> MouvementsStock { get; set; }

    // ── Documents & Médias ───────────────────────────────────────────────────
    public DbSet<Document> Documents { get; set; }

    // ── Campagnes ────────────────────────────────────────────────────────────
    public DbSet<Campagne> Campagnes { get; set; }

    // ── Notifications & Audit ────────────────────────────────────────────────
    public DbSet<Notification> Notifications { get; set; }
    public DbSet<Historique> Historiques { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Appliquer toutes les configurations depuis le dossier Configurations/
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(GmaoDbContext).Assembly);

        // ── Seed Data ────────────────────────────────────────────────────────
        SeedRoles(modelBuilder);
        SeedFamilles(modelBuilder);
        SeedFamillesPieces(modelBuilder);
        SeedCompetences(modelBuilder);
        SeedSocietes(modelBuilder);
        SeedUsers(modelBuilder);
    }

    private static void SeedRoles(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Role>().HasData(
            new Role { Id = 1, Nom = "Admin",                Description = "Administrateur d'une société avec accès complet sur son périmètre" },
            new Role { Id = 2, Nom = "Responsable Maintenance", Description = "Gestion des OT, validation des demandes et rapports" },
            new Role { Id = 3, Nom = "Chef Equipe",          Description = "Supervision des techniciens et suivi des OT" },
            new Role { Id = 4, Nom = "Technicien",           Description = "Exécution des interventions" },
            new Role { Id = 5, Nom = "Production",           Description = "Déclaration des pannes" },
            new Role { Id = 6, Nom = "Magasinier",           Description = "Gestion du stock de pièces de rechange" },
            new Role { Id = 7, Nom = "SuperAdmin",           Description = "Administrateur plateforme - gère toutes les sociétés" }
        );
    }

    private static void SeedFamilles(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Famille>().HasData(
            new Famille { Id = 1, Nom = "Pompes", Description = "Pompes hydrauliques et centrifuges" },
            new Famille { Id = 2, Nom = "Chaudières", Description = "Chaudières et générateurs de vapeur" },
            new Famille { Id = 3, Nom = "Convoyeurs", Description = "Convoyeurs et tapis transporteurs" },
            new Famille { Id = 4, Nom = "Compresseurs", Description = "Compresseurs d'air et de gaz" },
            new Famille { Id = 5, Nom = "Moteurs Electriques", Description = "Moteurs électriques AC/DC" },
            new Famille { Id = 6, Nom = "Réfrigération", Description = "Groupes frigorifiques et chambres froides" },
            new Famille { Id = 7, Nom = "Stérilisateurs", Description = "Autoclaves et tunnels de stérilisation" },
            new Famille { Id = 8, Nom = "Remplisseuses", Description = "Machines de remplissage et dosage" }
        );
    }

    private static void SeedFamillesPieces(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<FamillePiece>().HasData(
            new FamillePiece { Id = 1, Nom = "Roulements & Paliers" },
            new FamillePiece { Id = 2, Nom = "Joints & Garnitures" },
            new FamillePiece { Id = 3, Nom = "Courroies & Chaînes" },
            new FamillePiece { Id = 4, Nom = "Filtres" },
            new FamillePiece { Id = 5, Nom = "Lubrifiants & Huiles" },
            new FamillePiece { Id = 6, Nom = "Composants Electriques" },
            new FamillePiece { Id = 7, Nom = "Visserie & Boulonnerie" }
        );
    }

    private static void SeedCompetences(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Competence>().HasData(
            new Competence { Id = 1, Nom = "Électromécanicien", Description = "Maintenance mécanique et électrique combinée" },
            new Competence { Id = 2, Nom = "Électricien industriel", Description = "Réseaux électriques, câblage et moteurs" },
            new Competence { Id = 3, Nom = "Automaticien", Description = "Programmation API Siemens, Schneider, automates et capteurs" },
            new Competence { Id = 4, Nom = "Soudeur", Description = "Soudure tuyauterie inox et structures métalliques" },
            new Competence { Id = 5, Nom = "Mécanicien", Description = "Maintenance mécanique lourde, réducteurs, convoyeurs" }
        );
    }

    private static void SeedSocietes(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Societe>().HasData(
            new Societe
            {
                Id = 1,
                CodeTenant = "tenant-midi",
                Nom = "cicam",
                EmailContact = "cicam@midi.com",
                IsActive = true,
                CreatedAt = new DateTime(2026, 1, 10, 12, 0, 0, DateTimeKind.Utc)
            }
        );
    }

    private static void SeedUsers(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>().HasData(
            new User
            {
                Id = 1,
                Nom = "Super",
                Prenom = "Admin",
                Email = "superadmin@gmao.com",
                PasswordHash = "8c6976e5b5410415bde908bd4dee15dfb167a9c873fc4bb8a81f6f2ab448a918",
                Telephone = "+21699999999",
                RoleId = 7,
                SocieteId = null,
                IsActive = true,
                CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            }
        );
    }
}
