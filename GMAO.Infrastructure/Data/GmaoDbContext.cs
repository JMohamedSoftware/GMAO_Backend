using GMAO.Application.Interfaces;
using GMAO.Domain.Entities;
using GMAO.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace GMAO.Infrastructure.Data;

public class GmaoDbContext : DbContext
{
    private readonly ITenantContextService? _tenantContext;
    private readonly int? _currentSocieteId;
    private readonly bool _isSuperAdmin;
    private readonly bool _isImpersonating;

    public GmaoDbContext(
        DbContextOptions<GmaoDbContext> options,
        ITenantContextService? tenantContext = null) : base(options)
    {
        _tenantContext = tenantContext;
        _isSuperAdmin = tenantContext?.IsSuperAdmin ?? false;
        _isImpersonating = tenantContext?.IsImpersonating ?? false;
        _currentSocieteId = tenantContext?.CurrentSocieteId;
    }

    // ── Auth & Utilisateurs ──────────────────────────────────────────────────
    public DbSet<Role> Roles { get; set; }
    public DbSet<RolePermission> RolePermissions { get; set; }
    public DbSet<Societe> Societes { get; set; }
    public DbSet<User> Users { get; set; }
    public DbSet<Equipe> Equipes { get; set; }
    public DbSet<RefreshToken> RefreshTokens { get; set; }
    public DbSet<Competence> Competences { get; set; }
    public DbSet<TechnicienCompetence> TechnicienCompetences { get; set; }

    // ── Référentiel ──────────────────────────────────────────────────────────
    public DbSet<Famille> Familles { get; set; }
    public DbSet<Localisation> Localisations { get; set; }
    public DbSet<Fournisseur> Fournisseurs { get; set; }

    // ── Equipements ──────────────────────────────────────────────────────────
    public DbSet<Equipement> Equipements { get; set; }

    // ── Interventions ────────────────────────────────────────────────────────
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

    // ── Documents ────────────────────────────────────────────────────────────
    public DbSet<Document> Documents { get; set; }

    // ── Campagnes ────────────────────────────────────────────────────────────
    public DbSet<Campagne> Campagnes { get; set; }

    // ── Notifications & Audit / CDC ──────────────────────────────────────────
    public DbSet<Notification> Notifications { get; set; }
    public DbSet<Historique> Historiques { get; set; }
    public DbSet<AuditTrailEntry> AuditTrailEntries { get; set; }
    public DbSet<CdcEvent> CdcEvents { get; set; }
    public DbSet<CdcWorkerState> CdcWorkerStates { get; set; }

    // ── SuperAdmin SaaS ──────────────────────────────────────────────────────
    public DbSet<SiteUsine> SitesUsines { get; set; }
    public DbSet<PlanOffre> PlansOffres { get; set; }
    public DbSet<Facture> Factures { get; set; }
    public DbSet<JournalSuperAdmin> JournauxSuperAdmin { get; set; }
    public DbSet<PlatformSettings> PlatformSettings { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Appliquer toutes les configurations depuis le dossier Configurations/
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(GmaoDbContext).Assembly);

        // ── Global Query Filters (Multi-Tenant Fail-Closed) ───────────────────
        modelBuilder.Entity<Equipement>()
            .HasQueryFilter(e => (_isSuperAdmin && !_isImpersonating) || e.SocieteId == (_currentSocieteId ?? -1));

        modelBuilder.Entity<DemandeIntervention>()
            .HasQueryFilter(d => (_isSuperAdmin && !_isImpersonating) || d.SocieteId == (_currentSocieteId ?? -1));

        modelBuilder.Entity<OrdresTravail>()
            .HasQueryFilter(ot => (_isSuperAdmin && !_isImpersonating) || ot.SocieteId == (_currentSocieteId ?? -1));

        modelBuilder.Entity<Piece>()
            .HasQueryFilter(p => (_isSuperAdmin && !_isImpersonating) || p.SocieteId == (_currentSocieteId ?? -1));

        modelBuilder.Entity<Fournisseur>()
            .HasQueryFilter(f => (_isSuperAdmin && !_isImpersonating) || f.SocieteId == (_currentSocieteId ?? -1));

        modelBuilder.Entity<Campagne>()
            .HasQueryFilter(c => (_isSuperAdmin && !_isImpersonating) || c.SocieteId == (_currentSocieteId ?? -1));

        modelBuilder.Entity<SiteUsine>()
            .HasQueryFilter(s => (_isSuperAdmin && !_isImpersonating) || s.SocieteId == (_currentSocieteId ?? -1));

        modelBuilder.Entity<Facture>()
            .HasQueryFilter(f => (_isSuperAdmin && !_isImpersonating) || f.SocieteId == (_currentSocieteId ?? -1));

        modelBuilder.Entity<Localisation>()
            .HasQueryFilter(l => (_isSuperAdmin && !_isImpersonating) || l.SocieteId == (_currentSocieteId ?? -1));

        modelBuilder.Entity<Equipe>()
            .HasQueryFilter(eq => (_isSuperAdmin && !_isImpersonating) || eq.SocieteId == (_currentSocieteId ?? -1));

        modelBuilder.Entity<MouvementStock>()
            .HasQueryFilter(m => (_isSuperAdmin && !_isImpersonating) || (m.Piece != null && m.Piece.SocieteId == (_currentSocieteId ?? -1)));

        modelBuilder.Entity<PlanPreventif>()
            .HasQueryFilter(p => (_isSuperAdmin && !_isImpersonating) || (p.Equipement != null && p.Equipement.SocieteId == (_currentSocieteId ?? -1)));

        modelBuilder.Entity<TachePreventive>()
            .HasQueryFilter(t => (_isSuperAdmin && !_isImpersonating) || (t.Plan != null && t.Plan.Equipement != null && t.Plan.Equipement.SocieteId == (_currentSocieteId ?? -1)));

        modelBuilder.Entity<Document>()
            .HasQueryFilter(d => (_isSuperAdmin && !_isImpersonating)
                || (d.SocieteId != null && d.SocieteId == (_currentSocieteId ?? -1))
                || (d.SocieteId == null && (
                    (d.Equipement != null && d.Equipement.SocieteId == (_currentSocieteId ?? -1)) ||
                    (d.OT != null && d.OT.SocieteId == (_currentSocieteId ?? -1)) ||
                    (d.Incident != null && d.Incident.SocieteId == (_currentSocieteId ?? -1)) ||
                    (d.PlanPreventif != null && d.PlanPreventif.Equipement != null && d.PlanPreventif.Equipement.SocieteId == (_currentSocieteId ?? -1)))));

        // ── Relations Equipe ─────────────────────────────────────────────────
        modelBuilder.Entity<Equipe>()
            .HasOne(e => e.ChefEquipe)
            .WithMany()
            .HasForeignKey(e => e.ChefEquipeId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Equipe>()
            .HasMany(e => e.Membres)
            .WithOne(u => u.Equipe)
            .HasForeignKey(u => u.EquipeId)
            .OnDelete(DeleteBehavior.SetNull);

        // ── Seed Data ────────────────────────────────────────────────────────
        SeedRoles(modelBuilder);
        SeedFamilles(modelBuilder);
        SeedFamillesPieces(modelBuilder);
        SeedCompetences(modelBuilder);
        SeedLocalisations(modelBuilder);
    }

    // ── Contrôle et Protection d'Intégrité Multi-Tenant sur SaveChanges ─────
    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ApplyMultiTenantWriteGuards();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ApplyMultiTenantWriteGuards();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void ApplyMultiTenantWriteGuards()
    {
        var isSuperAdmin = _isSuperAdmin;
        var currentTenantId = _currentSocieteId;

        foreach (var entry in ChangeTracker.Entries())
        {
            // Exclure les journaux/pistes d'audit système de la vérification forcée d'appartenance locataire
            if (entry.Entity is AuditTrailEntry || entry.Entity is CdcEvent || entry.Entity is CdcWorkerState)
                continue;

            var prop = entry.Properties.FirstOrDefault(p => p.Metadata.Name == "SocieteId");
            if (prop == null) continue;

            if (entry.State == EntityState.Added)
            {
                if (!isSuperAdmin)
                {
                    if (!currentTenantId.HasValue || currentTenantId.Value <= 0)
                    {
                        // RefreshToken / User login peuvent sauvegarder sans SocieteId modifié sur entités sans SocieteId obligatoire
                        if (prop.Metadata.IsNullable) continue;
                        throw new InvalidOperationException("Action interdite : contexte locataire (SocieteId) manquant ou non autorisé pour la création d'enregistrements.");
                    }
                    // Forcer systématiquement le SocieteId vérifié de l'utilisateur standard
                    prop.CurrentValue = currentTenantId.Value;
                }
                else
                {
                    // SuperAdmin : si SocieteId non explicité dans l'entité, appliquer le tenant impersonné si actif
                    int? existingVal = prop.CurrentValue != null ? Convert.ToInt32(prop.CurrentValue) : null;
                    if (!existingVal.HasValue || existingVal.Value <= 0)
                    {
                        if (currentTenantId.HasValue && currentTenantId.Value > 0)
                        {
                            prop.CurrentValue = currentTenantId.Value;
                        }
                    }
                }
            }
            else if (entry.State == EntityState.Modified)
            {
                if (!isSuperAdmin && prop.IsModified && !Equals(prop.OriginalValue, prop.CurrentValue))
                {
                    throw new System.Security.SecurityException("Violation de sécurité : tentative illicite de transfert d'enregistrement inter-tenant.");
                }
            }
        }
    }

    private static void SeedLocalisations(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Localisation>().HasData(
            new Localisation { Id = 1, Nom = "Usine", Description = "Site principal" },
            new Localisation { Id = 2, Nom = "Réception", ParentId = 1 },
            new Localisation { Id = 3, Nom = "Lavage", ParentId = 1 },
            new Localisation { Id = 4, Nom = "Tri", ParentId = 1 },
            new Localisation { Id = 5, Nom = "Concentration", ParentId = 1 },
            new Localisation { Id = 6, Nom = "Conditionnement", ParentId = 1 },
            new Localisation { Id = 7, Nom = "Stockage", ParentId = 1 },
            new Localisation { Id = 8, Nom = "Utilités", ParentId = 1 }
        );
    }

    private static void SeedRoles(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Role>().HasData(
            new Role { Id = 1, Nom = "Administrateur", Description = "Administrateur de la plateforme et de l'usine avec accès complet" },
            new Role { Id = 2, Nom = "Responsable Maintenance", Description = "Gestion des OT, validation des demandes et rapports" },
            new Role { Id = 3, Nom = "Chef d'équipe", Description = "Supervision des techniciens et suivi des OT" },
            new Role { Id = 4, Nom = "Technicien", Description = "Exécution des interventions" },
            new Role { Id = 5, Nom = "Responsable Production", Description = "Déclaration des pannes et validation" },
            new Role { Id = 7, Nom = "SuperAdmin", Description = "Administrateur global de la plateforme" }
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
            new Famille { Id = 6, Nom = "Réfrigération", Description = "Groupes frigorifiques et chambres froides" }
        );
    }

    private static void SeedFamillesPieces(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<FamillePiece>().HasData(
            new FamillePiece { Id = 1, Nom = "Roulements", Description = "Roulements à billes, rouleaux, paliers" },
            new FamillePiece { Id = 2, Nom = "Vannes", Description = "Vannes papillon, vannes sphériques, clapets" },
            new FamillePiece { Id = 3, Nom = "Joints", Description = "Joints toriques, joints d'étanchéité, tresses" },
            new FamillePiece { Id = 4, Nom = "Filtres", Description = "Filtres à huile, filtres à air, tamis" },
            new FamillePiece { Id = 5, Nom = "Courroies", Description = "Courroies trapézoïdales, crantées" },
            new FamillePiece { Id = 6, Nom = "Moteurs & Pompes", Description = "Moteurs de rechange, pompes de dosage" },
            new FamillePiece { Id = 7, Nom = "Capteurs", Description = "Capteurs de température, pression, débit" },
            new FamillePiece { Id = 8, Nom = "Électricité", Description = "Fusibles, contacteurs, disjoncteurs, relais" }
        );
    }

    private static void SeedCompetences(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Competence>().HasData(
            new Competence { Id = 1, Nom = "Mécanique", Description = "Maintenance mécanique, démontage/remontage, alignement" },
            new Competence { Id = 2, Nom = "Électricité", Description = "Dépannage électrique, armoires, moteurs, câblage" },
            new Competence { Id = 3, Nom = "Automatisme", Description = "Automates programmables (PLC), capteurs, variateurs" },
            new Competence { Id = 4, Nom = "Pneumatique", Description = "Circuits pneumatiques, vérins, distributeurs" },
            new Competence { Id = 5, Nom = "Hydraulique", Description = "Centrales hydrauliques, pompes HP, électrovannes" },
            new Competence { Id = 6, Nom = "Soudure", Description = "Soudure TIG, MIG, arc pour réparation tuyauteries et châssis" },
            new Competence { Id = 7, Nom = "Froid Industriel", Description = "Groupes frigorifiques, compresseurs à ammoniac/fréon" },
            new Competence { Id = 8, Nom = "Chaud & Vapeur", Description = "Chaudières vapeur, échangeurs thermiques, réseaux vapeur" }
        );
    }
}
