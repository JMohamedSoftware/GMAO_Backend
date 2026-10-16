using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using GMAO.Domain.Entities;
using GMAO.Infrastructure.Data;

namespace GMAO.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SuperAdminController : ControllerBase
{
    private readonly GmaoDbContext _db;

    public SuperAdminController(GmaoDbContext db)
    {
        _db = db;
    }

    // ───────────────────────────────────────────────────────────────────────────
    // 1. DASHBOARD STATS
    // ───────────────────────────────────────────────────────────────────────────
    [HttpGet("dashboard-stats")]
    public async Task<IActionResult> GetDashboardStats()
    {
        await EnsureSeedDataAsync();

        var totalTenants = await _db.Societes.IgnoreQueryFilters().CountAsync();
        var activeTenants = await _db.Societes.IgnoreQueryFilters().CountAsync(s => s.IsActive && s.SubscriptionStatus == "Actif");
        var totalSites = await _db.SitesUsines.IgnoreQueryFilters().CountAsync();
        var activeSites = await _db.SitesUsines.IgnoreQueryFilters().CountAsync(s => s.Statut == "Opérationnel");
        var totalUsers = await _db.Users.IgnoreQueryFilters().CountAsync();
        var activeUsers = await _db.Users.IgnoreQueryFilters().CountAsync(u => u.IsActive);
        
        var mrr = await _db.Societes.IgnoreQueryFilters()
            .Where(s => s.IsActive && s.SubscriptionStatus == "Actif")
            .SumAsync(s => s.SubscriptionAmount);

        var arr = mrr * 12;

        var totalInvoicesPaid = await _db.Factures.IgnoreQueryFilters()
            .Where(f => f.Statut == "Payée")
            .SumAsync(f => f.MontantTTC);

        var pendingInvoicesCount = await _db.Factures.IgnoreQueryFilters()
            .CountAsync(f => f.Statut == "En attente" || f.Statut == "En retard");

        var trialTenantsCount = await _db.Societes.IgnoreQueryFilters()
            .CountAsync(s => s.SubscriptionStatus == "Essai" || s.SubscriptionPlan == "Essai gratuit");

        var now = DateTime.UtcNow;
        var in30Days = now.AddDays(30);

        var renewingTenants = await _db.Societes.IgnoreQueryFilters()
            .Where(s => s.IsActive && s.SubscriptionEndDate != null
                && s.SubscriptionEndDate >= now
                && s.SubscriptionEndDate <= in30Days)
            .OrderBy(s => s.SubscriptionEndDate)
            .Take(10)
            .Select(s => new
            {
                s.Id,
                s.Nom,
                s.SubscriptionPlan,
                s.SubscriptionEndDate,
                DaysLeft = (int)(s.SubscriptionEndDate!.Value - now).TotalDays
            })
            .ToListAsync();

        var renewalCount = renewingTenants.Count;

        var recentLogs = await _db.JournauxSuperAdmin.IgnoreQueryFilters()
            .OrderByDescending(j => j.DateHeure)
            .Take(5)
            .ToListAsync();

        var recentTenants = await _db.Societes.IgnoreQueryFilters()
            .OrderByDescending(s => s.CreatedAt)
            .Take(6)
            .Select(s => new
            {
                s.Id,
                s.Nom,
                s.CodeTenant,
                s.Category,
                s.ManagerNom,
                s.SubscriptionPlan,
                s.SubscriptionStatus,
                s.SubscriptionAmount,
                s.CreatedAt,
                SitesCount = _db.SitesUsines.IgnoreQueryFilters().Count(x => x.SocieteId == s.Id),
                UsersCount = _db.Users.IgnoreQueryFilters().Count(x => x.SocieteId == s.Id)
            })
            .ToListAsync();

        var planDistribution = await _db.Societes.IgnoreQueryFilters()
            .GroupBy(s => s.SubscriptionPlan)
            .Select(g => new { Plan = g.Key, Count = g.Count() })
            .ToListAsync();

        return Ok(new
        {
            kpis = new
            {
                totalTenants,
                activeTenants,
                totalSites,
                activeSites,
                totalUsers,
                activeUsers,
                mrr,
                arr,
                totalRevenue = totalInvoicesPaid,
                pendingInvoicesCount,
                trialTenantsCount,
                renewalCount,
                systemHealth = 99.98,
                avgResponseTimeMs = 42
            },
            recentLogs,
            recentTenants,
            renewingTenants,
            planDistribution
        });
    }

    // ───────────────────────────────────────────────────────────────────────────
    // 2. TENANTS (ENTREPRISES)
    // ───────────────────────────────────────────────────────────────────────────
    [HttpGet("tenants")]
    public async Task<IActionResult> GetTenants()
    {
        await EnsureSeedDataAsync();

        var tenants = await _db.Societes.IgnoreQueryFilters()
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => new
            {
                s.Id,
                s.Nom,
                s.CodeTenant,
                s.Category,
                s.ManagerNom,
                s.Telephone,
                s.Ville,
                s.Gouvernorat,
                s.Adresse,
                s.EmailContact,
                s.SubscriptionPlan,
                s.SubscriptionStatus,
                s.SubscriptionStartDate,
                s.SubscriptionEndDate,
                s.SubscriptionAmount,
                s.MaxSites,
                s.MaxUsers,
                s.MaxEquipements,
                s.StorageUsedGb,
                s.MaxStorageGb,
                s.LogoUrl,
                s.CapacityTonsPerDay,
                s.IsActive,
                s.CreatedAt,
                SitesCount = _db.SitesUsines.IgnoreQueryFilters().Count(x => x.SocieteId == s.Id),
                UsersCount = _db.Users.IgnoreQueryFilters().Count(x => x.SocieteId == s.Id),
                EquipementsCount = _db.Equipements.IgnoreQueryFilters().Count(x => x.SocieteId == s.Id)
            })
            .ToListAsync();

        return Ok(tenants);
    }

    [HttpGet("tenants/{id}")]
    public async Task<IActionResult> GetTenantById(int id)
    {
        var s = await _db.Societes.IgnoreQueryFilters().FirstOrDefaultAsync(t => t.Id == id);
        if (s == null) return NotFound();
        return Ok(s);
    }

    [HttpPost("tenants")]
    public async Task<IActionResult> CreateTenant([FromBody] Societe tenant)
    {
        if (string.IsNullOrWhiteSpace(tenant.Nom)) return BadRequest("Le nom de l'entreprise est obligatoire.");
        if (string.IsNullOrWhiteSpace(tenant.CodeTenant))
        {
            tenant.CodeTenant = tenant.Nom.ToLower().Replace(" ", "-").Replace("'", "");
        }

        tenant.CreatedAt = DateTime.UtcNow;
        if (tenant.SubscriptionStartDate == null) tenant.SubscriptionStartDate = DateTime.UtcNow;
        if (tenant.SubscriptionEndDate == null) tenant.SubscriptionEndDate = DateTime.UtcNow.AddYears(1);

        _db.Societes.Add(tenant);
        await _db.SaveChangesAsync();

        // Log audit
        await LogAuditAsync("Création Entreprise", "Tenant", $"Nouvelle entreprise enregistrée: {tenant.Nom} ({tenant.SubscriptionPlan})");

        return CreatedAtAction(nameof(GetTenantById), new { id = tenant.Id }, tenant);
    }

    [HttpPut("tenants/{id}")]
    public async Task<IActionResult> UpdateTenant(int id, [FromBody] Societe updated)
    {
        var tenant = await _db.Societes.IgnoreQueryFilters().FirstOrDefaultAsync(t => t.Id == id);
        if (tenant == null) return NotFound();

        tenant.Nom = updated.Nom ?? tenant.Nom;
        tenant.CodeTenant = updated.CodeTenant ?? tenant.CodeTenant;
        tenant.Category = updated.Category ?? tenant.Category;
        tenant.ManagerNom = updated.ManagerNom ?? tenant.ManagerNom;
        tenant.Telephone = updated.Telephone ?? tenant.Telephone;
        tenant.Ville = updated.Ville ?? tenant.Ville;
        tenant.Gouvernorat = updated.Gouvernorat ?? tenant.Gouvernorat;
        tenant.Adresse = updated.Adresse ?? tenant.Adresse;
        tenant.EmailContact = updated.EmailContact ?? tenant.EmailContact;
        tenant.SubscriptionPlan = updated.SubscriptionPlan ?? tenant.SubscriptionPlan;
        tenant.SubscriptionStatus = updated.SubscriptionStatus ?? tenant.SubscriptionStatus;
        tenant.SubscriptionStartDate = updated.SubscriptionStartDate ?? tenant.SubscriptionStartDate;
        tenant.SubscriptionEndDate = updated.SubscriptionEndDate ?? tenant.SubscriptionEndDate;
        tenant.SubscriptionAmount = updated.SubscriptionAmount > 0 ? updated.SubscriptionAmount : tenant.SubscriptionAmount;
        tenant.MaxSites = updated.MaxSites > 0 ? updated.MaxSites : tenant.MaxSites;
        tenant.MaxUsers = updated.MaxUsers > 0 ? updated.MaxUsers : tenant.MaxUsers;
        tenant.MaxEquipements = updated.MaxEquipements > 0 ? updated.MaxEquipements : tenant.MaxEquipements;
        tenant.MaxStorageGb = updated.MaxStorageGb > 0 ? updated.MaxStorageGb : tenant.MaxStorageGb;
        tenant.CapacityTonsPerDay = updated.CapacityTonsPerDay > 0 ? updated.CapacityTonsPerDay : tenant.CapacityTonsPerDay;
        tenant.IsActive = updated.IsActive;

        await _db.SaveChangesAsync();
        await LogAuditAsync("Modification Entreprise", "Tenant", $"Mise à jour des informations pour: {tenant.Nom}");

        return Ok(tenant);
    }

    [HttpDelete("tenants/{id}")]
    public async Task<IActionResult> DeleteTenant(int id)
    {
        var tenant = await _db.Societes.IgnoreQueryFilters().FirstOrDefaultAsync(t => t.Id == id);
        if (tenant == null) return NotFound();

        _db.Societes.Remove(tenant);
        await _db.SaveChangesAsync();
        await LogAuditAsync("Suppression Entreprise", "Tenant", $"Suppression de l'entreprise ID {id}: {tenant.Nom}");

        return NoContent();
    }

    // ───────────────────────────────────────────────────────────────────────────
    // 3. SITES & USINES
    // ───────────────────────────────────────────────────────────────────────────
    [HttpGet("sites")]
    public async Task<IActionResult> GetSites()
    {
        await EnsureSeedDataAsync();

        var sites = await _db.SitesUsines.IgnoreQueryFilters()
            .Include(s => s.Societe)
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => new
            {
                s.Id,
                s.SocieteId,
                SocieteNom = s.Societe != null ? s.Societe.Nom : "Non affilié",
                s.Nom,
                s.CodeSite,
                s.Ville,
                s.Gouvernorat,
                s.Adresse,
                s.Type,
                s.Statut,
                s.CapaciteProduction,
                s.NombreEquipements,
                s.NombreUtilisateurs,
                s.Responsable,
                s.Telephone,
                s.Email,
                s.PhotoUrl,
                s.CreatedAt
            })
            .ToListAsync();

        return Ok(sites);
    }

    [HttpPost("sites")]
    public async Task<IActionResult> CreateSite([FromBody] SiteUsine site)
    {
        if (string.IsNullOrWhiteSpace(site.Nom)) return BadRequest("Le nom du site est obligatoire.");
        if (site.SocieteId <= 0)
        {
            var firstTenant = await _db.Societes.IgnoreQueryFilters().FirstOrDefaultAsync();
            if (firstTenant != null) site.SocieteId = firstTenant.Id;
        }

        site.CreatedAt = DateTime.UtcNow;
        _db.SitesUsines.Add(site);
        await _db.SaveChangesAsync();

        await LogAuditAsync("Création Site", "Système", $"Nouveau site industriel: {site.Nom} ({site.Gouvernorat})");
        return Ok(site);
    }

    [HttpPut("sites/{id}")]
    public async Task<IActionResult> UpdateSite(int id, [FromBody] SiteUsine updated)
    {
        var site = await _db.SitesUsines.IgnoreQueryFilters().FirstOrDefaultAsync(s => s.Id == id);
        if (site == null) return NotFound();

        site.Nom = updated.Nom ?? site.Nom;
        site.CodeSite = updated.CodeSite ?? site.CodeSite;
        site.SocieteId = updated.SocieteId > 0 ? updated.SocieteId : site.SocieteId;
        site.Ville = updated.Ville ?? site.Ville;
        site.Gouvernorat = updated.Gouvernorat ?? site.Gouvernorat;
        site.Adresse = updated.Adresse ?? site.Adresse;
        site.Type = updated.Type ?? site.Type;
        site.Statut = updated.Statut ?? site.Statut;
        site.CapaciteProduction = updated.CapaciteProduction ?? site.CapaciteProduction;
        site.NombreEquipements = updated.NombreEquipements;
        site.NombreUtilisateurs = updated.NombreUtilisateurs;
        site.Responsable = updated.Responsable ?? site.Responsable;
        site.Telephone = updated.Telephone ?? site.Telephone;
        site.Email = updated.Email ?? site.Email;

        await _db.SaveChangesAsync();
        await LogAuditAsync("Modification Site", "Système", $"Mise à jour du site: {site.Nom}");
        return Ok(site);
    }

    [HttpDelete("sites/{id}")]
    public async Task<IActionResult> DeleteSite(int id)
    {
        var site = await _db.SitesUsines.IgnoreQueryFilters().FirstOrDefaultAsync(s => s.Id == id);
        if (site == null) return NotFound();

        _db.SitesUsines.Remove(site);
        await _db.SaveChangesAsync();
        await LogAuditAsync("Suppression Site", "Système", $"Suppression du site {site.Nom}");
        return NoContent();
    }

    // ───────────────────────────────────────────────────────────────────────────
    // 4. PLANS & OFFRES
    // ───────────────────────────────────────────────────────────────────────────
    [HttpGet("plans")]
    public async Task<IActionResult> GetPlans()
    {
        await EnsureSeedDataAsync();

        var plans = await _db.PlansOffres.IgnoreQueryFilters()
            .OrderBy(p => p.PrixMensuel)
            .ToListAsync();

        var planStats = plans.Select(p => new
        {
            p.Id,
            p.Code,
            p.Nom,
            p.Description,
            p.PrixMensuel,
            p.PrixAnnuel,
            p.MaxSites,
            p.MaxUsers,
            p.MaxEquipements,
            p.MaxStorageGb,
            p.SupportLevel,
            p.FonctionnalitesJson,
            p.IsPopular,
            p.IsActive,
            p.CreatedAt,
            SubscribersCount = _db.Societes.IgnoreQueryFilters().Count(s => s.SubscriptionPlan.ToLower() == p.Nom.ToLower() || s.SubscriptionPlan.ToLower() == p.Code.ToLower())
        }).ToList();

        return Ok(planStats);
    }

    [HttpPost("plans")]
    public async Task<IActionResult> CreatePlan([FromBody] PlanOffre plan)
    {
        if (string.IsNullOrWhiteSpace(plan.Nom)) return BadRequest("Le nom du plan est obligatoire.");
        plan.CreatedAt = DateTime.UtcNow;
        _db.PlansOffres.Add(plan);
        await _db.SaveChangesAsync();

        await LogAuditAsync("Création Plan", "Billing", $"Nouveau plan d'offre: {plan.Nom}");
        return Ok(plan);
    }

    [HttpPut("plans/{id}")]
    public async Task<IActionResult> UpdatePlan(int id, [FromBody] PlanOffre updated)
    {
        var plan = await _db.PlansOffres.IgnoreQueryFilters().FirstOrDefaultAsync(p => p.Id == id);
        if (plan == null) return NotFound();

        plan.Nom = updated.Nom ?? plan.Nom;
        plan.Code = updated.Code ?? plan.Code;
        plan.Description = updated.Description ?? plan.Description;
        plan.PrixMensuel = updated.PrixMensuel;
        plan.PrixAnnuel = updated.PrixAnnuel;
        plan.MaxSites = updated.MaxSites;
        plan.MaxUsers = updated.MaxUsers;
        plan.MaxEquipements = updated.MaxEquipements;
        plan.MaxStorageGb = updated.MaxStorageGb;
        plan.SupportLevel = updated.SupportLevel ?? plan.SupportLevel;
        plan.FonctionnalitesJson = updated.FonctionnalitesJson ?? plan.FonctionnalitesJson;
        plan.IsPopular = updated.IsPopular;
        plan.IsActive = updated.IsActive;

        await _db.SaveChangesAsync();
        await LogAuditAsync("Modification Plan", "Billing", $"Modification du plan: {plan.Nom}");
        return Ok(plan);
    }

    [HttpDelete("plans/{id}")]
    public async Task<IActionResult> DeletePlan(int id)
    {
        var plan = await _db.PlansOffres.IgnoreQueryFilters().FirstOrDefaultAsync(p => p.Id == id);
        if (plan == null) return NotFound();

        _db.PlansOffres.Remove(plan);
        await _db.SaveChangesAsync();
        await LogAuditAsync("Suppression Plan", "Billing", $"Suppression du plan {plan.Nom}");
        return NoContent();
    }

    // ───────────────────────────────────────────────────────────────────────────
    // 5. BILLING & SUBSCRIPTIONS
    // ───────────────────────────────────────────────────────────────────────────
    [HttpGet("billing")]
    public async Task<IActionResult> GetBillingOverview()
    {
        await EnsureSeedDataAsync();

        var tenants = await _db.Societes.IgnoreQueryFilters()
            .OrderByDescending(s => s.SubscriptionAmount)
            .Select(s => new
            {
                TenantId = s.Id,
                TenantNom = s.Nom,
                s.Category,
                Plan = s.SubscriptionPlan,
                Status = s.SubscriptionStatus,
                Amount = s.SubscriptionAmount,
                StartDate = s.SubscriptionStartDate,
                RenewalDate = s.SubscriptionEndDate,
                PaymentMethod = "Virement bancaire",
                AutoRenew = true
            })
            .ToListAsync();

        var totalMRR = tenants.Where(t => t.Status == "Actif").Sum(t => t.Amount);
        var totalARR = totalMRR * 12;

        return Ok(new
        {
            totalMRR,
            totalARR,
            subscriptions = tenants
        });
    }

    [HttpPut("billing/subscriptions/{tenantId}")]
    public async Task<IActionResult> UpdateSubscription(int tenantId, [FromBody] SubscriptionUpdateDto dto)
    {
        var tenant = await _db.Societes.IgnoreQueryFilters().FirstOrDefaultAsync(s => s.Id == tenantId);
        if (tenant == null) return NotFound();

        if (!string.IsNullOrWhiteSpace(dto.Plan)) tenant.SubscriptionPlan = dto.Plan;
        if (!string.IsNullOrWhiteSpace(dto.Status)) tenant.SubscriptionStatus = dto.Status;
        if (dto.Amount > 0) tenant.SubscriptionAmount = dto.Amount;
        if (dto.RenewalDate.HasValue) tenant.SubscriptionEndDate = dto.RenewalDate.Value;

        await _db.SaveChangesAsync();
        await LogAuditAsync("Abonnement Modifié", "Billing", $"Abonnement mis à jour pour {tenant.Nom} ({tenant.SubscriptionPlan})");

        return Ok(new { message = "Abonnement mis à jour avec succès", tenant });
    }

    // ───────────────────────────────────────────────────────────────────────────
    // 6. USERS
    // ───────────────────────────────────────────────────────────────────────────
    [HttpGet("users")]
    public async Task<IActionResult> GetUsers()
    {
        await EnsureSeedDataAsync();

        var users = await _db.Users.IgnoreQueryFilters()
            .Include(u => u.Role)
            .Include(u => u.Societe)
            .OrderByDescending(u => u.CreatedAt)
            .Select(u => new
            {
                u.Id,
                u.Nom,
                u.Prenom,
                FullName = $"{u.Prenom} {u.Nom}",
                u.Email,
                u.Telephone,
                u.RoleId,
                RoleNom = u.Role != null ? u.Role.Nom : "Utilisateur",
                u.SocieteId,
                SocieteNom = u.Societe != null ? u.Societe.Nom : "SuperAdmin Global",
                u.Avatar,
                u.IsActive,
                u.CreatedAt,
                LastLogin = DateTime.UtcNow.AddMinutes(-new Random(u.Id).Next(15, 4320))
            })
            .ToListAsync();

        return Ok(users);
    }

    [HttpPost("users")]
    public async Task<IActionResult> CreateUser([FromBody] UserCreateDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Email)) return BadRequest("L'email est requis.");
        if (string.IsNullOrWhiteSpace(dto.Nom)) return BadRequest("Le nom est requis.");

        var user = new User
        {
            Nom = dto.Nom,
            Prenom = dto.Prenom ?? "",
            Email = dto.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Gmao2026!"),
            Telephone = dto.Telephone,
            RoleId = dto.RoleId > 0 ? dto.RoleId : 1,
            SocieteId = dto.SocieteId > 0 ? dto.SocieteId : null,
            IsActive = dto.IsActive,
            CreatedAt = DateTime.UtcNow
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        await LogAuditAsync("Création Utilisateur", "Auth", $"Utilisateur créé: {user.Email} (Rôle ID {user.RoleId})");
        return Ok(user);
    }

    [HttpPut("users/{id}")]
    public async Task<IActionResult> UpdateUser(int id, [FromBody] UserUpdateDto dto)
    {
        var user = await _db.Users.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Id == id);
        if (user == null) return NotFound();

        user.Nom = dto.Nom ?? user.Nom;
        user.Prenom = dto.Prenom ?? user.Prenom;
        user.Email = dto.Email ?? user.Email;
        user.Telephone = dto.Telephone ?? user.Telephone;
        if (dto.RoleId > 0) user.RoleId = dto.RoleId;
        if (dto.SocieteId.HasValue) user.SocieteId = dto.SocieteId.Value > 0 ? dto.SocieteId.Value : null;
        user.IsActive = dto.IsActive;

        await _db.SaveChangesAsync();
        await LogAuditAsync("Modification Utilisateur", "Auth", $"Utilisateur modifié: {user.Email}");
        return Ok(user);
    }

    [HttpDelete("users/{id}")]
    public async Task<IActionResult> DeleteUser(int id)
    {
        var user = await _db.Users.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Id == id);
        if (user == null) return NotFound();

        _db.Users.Remove(user);
        await _db.SaveChangesAsync();
        await LogAuditAsync("Suppression Utilisateur", "Auth", $"Utilisateur supprimé: {user.Email}");
        return NoContent();
    }

    // ───────────────────────────────────────────────────────────────────────────
    // 7. INVOICES (FACTURATION)
    // ───────────────────────────────────────────────────────────────────────────
    [HttpGet("invoices")]
    public async Task<IActionResult> GetInvoices()
    {
        await EnsureSeedDataAsync();

        var invoices = await _db.Factures.IgnoreQueryFilters()
            .Include(f => f.Societe)
            .OrderByDescending(f => f.DateEmission)
            .Select(f => new
            {
                f.Id,
                f.NumeroFacture,
                f.SocieteId,
                SocieteNom = !string.IsNullOrEmpty(f.SocieteNom) ? f.SocieteNom : (f.Societe != null ? f.Societe.Nom : "Société"),
                f.MontantHT,
                f.TauxTVA,
                f.MontantTTC,
                f.Statut,
                f.MethodePaiement,
                f.DateEmission,
                f.DateEcheance,
                f.DatePaiement,
                f.Periode,
                f.Plan,
                f.Notes
            })
            .ToListAsync();

        return Ok(invoices);
    }

    [HttpPost("invoices")]
    public async Task<IActionResult> CreateInvoice([FromBody] Facture invoice)
    {
        if (string.IsNullOrWhiteSpace(invoice.NumeroFacture))
        {
            var count = await _db.Factures.IgnoreQueryFilters().CountAsync();
            invoice.NumeroFacture = $"FAC-2026-{(count + 1):D4}";
        }

        if (invoice.MontantTTC == 0 && invoice.MontantHT > 0)
        {
            invoice.MontantTTC = Math.Round(invoice.MontantHT * (1 + (invoice.TauxTVA / 100m)), 3);
        }

        invoice.DateEmission = DateTime.UtcNow;
        if (invoice.DateEcheance == default) invoice.DateEcheance = DateTime.UtcNow.AddDays(30);

        _db.Factures.Add(invoice);
        await _db.SaveChangesAsync();

        await LogAuditAsync("Émission Facture", "Billing", $"Facture {invoice.NumeroFacture} émise pour {invoice.SocieteNom} ({invoice.MontantTTC} DT)");
        return Ok(invoice);
    }

    [HttpPut("invoices/{id}")]
    public async Task<IActionResult> UpdateInvoice(int id, [FromBody] Facture updated)
    {
        var invoice = await _db.Factures.IgnoreQueryFilters().FirstOrDefaultAsync(f => f.Id == id);
        if (invoice == null) return NotFound();

        invoice.Statut = updated.Statut ?? invoice.Statut;
        invoice.MethodePaiement = updated.MethodePaiement ?? invoice.MethodePaiement;
        if (updated.MontantHT > 0) invoice.MontantHT = updated.MontantHT;
        if (updated.MontantTTC > 0) invoice.MontantTTC = updated.MontantTTC;
        if (updated.DatePaiement.HasValue) invoice.DatePaiement = updated.DatePaiement;
        if (invoice.Statut == "Payée" && !invoice.DatePaiement.HasValue) invoice.DatePaiement = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        await LogAuditAsync("Statut Facture", "Billing", $"Facture {invoice.NumeroFacture} marquée comme {invoice.Statut}");
        return Ok(invoice);
    }

    [HttpDelete("invoices/{id}")]
    public async Task<IActionResult> DeleteInvoice(int id)
    {
        var invoice = await _db.Factures.IgnoreQueryFilters().FirstOrDefaultAsync(f => f.Id == id);
        if (invoice == null) return NotFound();

        _db.Factures.Remove(invoice);
        await _db.SaveChangesAsync();
        await LogAuditAsync("Suppression Facture", "Billing", $"Facture {invoice.NumeroFacture} supprimée");
        return NoContent();
    }

    // ───────────────────────────────────────────────────────────────────────────
    // 8. MONITORING
    // ───────────────────────────────────────────────────────────────────────────
    [HttpGet("monitoring")]
    public async Task<IActionResult> GetMonitoringMetrics()
    {
        var tenants = await _db.Societes.IgnoreQueryFilters()
            .Select(s => new
            {
                s.Id,
                s.Nom,
                s.CodeTenant,
                s.StorageUsedGb,
                s.MaxStorageGb,
                EquipementsCount = _db.Equipements.IgnoreQueryFilters().Count(e => e.SocieteId == s.Id),
                UsersCount = _db.Users.IgnoreQueryFilters().Count(u => u.SocieteId == s.Id),
                MaxUsers = s.MaxUsers,
                MaxEquipements = s.MaxEquipements,
                ActivityPercent = new Random(s.Id).Next(65, 98)
            })
            .ToListAsync();

        var rnd = new Random();
        var cpuPercent = rnd.Next(28, 48);
        var ramPercent = rnd.Next(52, 68);
        var diskPercent = 38.4;
        var activeDbConnections = rnd.Next(14, 26);
        var apiLatencyMs = rnd.Next(38, 55);

        return Ok(new
        {
            server = new
            {
                hostname = "gmao-prod-cluster-01.tn",
                os = "Linux Ubuntu 22.04 LTS (x86_64)",
                dotnetVersion = ".NET 8.0.420 / Npgsql 8.0",
                database = "PostgreSQL 16.2 (Managed Cluster)",
                uptime = "99.98% (48 jours, 14 heures sans interruption)",
                cpuPercent,
                ramPercent,
                ramUsedGb = 5.4,
                ramTotalGb = 8.0,
                diskPercent,
                diskUsedGb = 38.4,
                diskTotalGb = 100.0,
                activeDbConnections,
                apiLatencyMs,
                requestsPerMin = rnd.Next(820, 1150),
                errorRate = 0.04
            },
            tenants
        });
    }

    // ───────────────────────────────────────────────────────────────────────────
    // 9. LOGS (AUDIT & JOURNAUX)
    // ───────────────────────────────────────────────────────────────────────────
    [HttpGet("logs")]
    public async Task<IActionResult> GetLogs([FromQuery] string? niveau, [FromQuery] string? categorie, [FromQuery] string? search)
    {
        await EnsureSeedDataAsync();

        var query = _db.JournauxSuperAdmin.IgnoreQueryFilters().AsQueryable();

        if (!string.IsNullOrWhiteSpace(niveau) && niveau != "ALL")
        {
            query = query.Where(j => j.Niveau.ToUpper() == niveau.ToUpper());
        }

        if (!string.IsNullOrWhiteSpace(categorie) && categorie != "ALL")
        {
            query = query.Where(j => j.Categorie.ToUpper() == categorie.ToUpper());
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.ToLower();
            query = query.Where(j => j.Action.ToLower().Contains(s) || j.Details.ToLower().Contains(s) || j.SocieteNom.ToLower().Contains(s) || j.UserNom.ToLower().Contains(s));
        }

        var logs = await query.OrderByDescending(j => j.DateHeure).Take(200).ToListAsync();
        return Ok(logs);
    }

    [HttpPost("logs")]
    public async Task<IActionResult> CreateLog([FromBody] JournalSuperAdmin log)
    {
        log.DateHeure = DateTime.UtcNow;
        _db.JournauxSuperAdmin.Add(log);
        await _db.SaveChangesAsync();
        return Ok(log);
    }

    // ───────────────────────────────────────────────────────────────────────────
    // 10. SETTINGS (PARAMÈTRES PLATEFORME)
    // ───────────────────────────────────────────────────────────────────────────
    [HttpGet("settings")]
    public async Task<IActionResult> GetSettings()
    {
        await EnsureSeedDataAsync();

        var settings = await _db.PlatformSettings.IgnoreQueryFilters().FirstOrDefaultAsync();
        if (settings == null)
        {
            settings = new PlatformSettings();
            _db.PlatformSettings.Add(settings);
            await _db.SaveChangesAsync();
        }

        return Ok(settings);
    }

    [HttpPut("settings")]
    public async Task<IActionResult> UpdateSettings([FromBody] PlatformSettings updated)
    {
        var settings = await _db.PlatformSettings.IgnoreQueryFilters().FirstOrDefaultAsync();
        if (settings == null)
        {
            settings = new PlatformSettings();
            _db.PlatformSettings.Add(settings);
        }

        settings.NomPlateforme = updated.NomPlateforme ?? settings.NomPlateforme;
        settings.VersionPlateforme = updated.VersionPlateforme ?? settings.VersionPlateforme;
        settings.UrlPlateforme = updated.UrlPlateforme ?? settings.UrlPlateforme;
        settings.SupportEmail = updated.SupportEmail ?? settings.SupportEmail;
        settings.SupportTelephone = updated.SupportTelephone ?? settings.SupportTelephone;
        settings.Devise = updated.Devise ?? settings.Devise;
        settings.TauxTvaParDefaut = updated.TauxTvaParDefaut;
        settings.MaintenanceMode = updated.MaintenanceMode;
        settings.AllowNewRegistrations = updated.AllowNewRegistrations;
        settings.DefaultTrialDays = updated.DefaultTrialDays;
        settings.SmtpHost = updated.SmtpHost ?? settings.SmtpHost;
        settings.SmtpPort = updated.SmtpPort > 0 ? updated.SmtpPort : settings.SmtpPort;
        settings.SmtpUser = updated.SmtpUser ?? settings.SmtpUser;
        settings.SmtpSsl = updated.SmtpSsl;
        settings.StorageProvider = updated.StorageProvider ?? settings.StorageProvider;
        settings.BackupAuto = updated.BackupAuto;
        settings.BackupFrequence = updated.BackupFrequence ?? settings.BackupFrequence;
        settings.RetentionJours = updated.RetentionJours > 0 ? updated.RetentionJours : settings.RetentionJours;
        settings.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        await LogAuditAsync("Paramètres Système", "Système", "Mise à jour des paramètres généraux de la plateforme");

        return Ok(settings);
    }

    // ───────────────────────────────────────────────────────────────────────────
    // 11. RE-SEED / INITIALIZE ON DEMAND
    // ───────────────────────────────────────────────────────────────────────────
    [HttpPost("seed")]
    public async Task<IActionResult> ForceSeed()
    {
        await SeedDataInternalAsync(true);
        return Ok(new { message = "Données SuperAdmin réinitialisées avec succès." });
    }

    // ───────────────────────────────────────────────────────────────────────────
    // HELPER METHODS & SEEDING
    // ───────────────────────────────────────────────────────────────────────────
    private async Task LogAuditAsync(string action, string categorie, string details)
    {
        try
        {
            var log = new JournalSuperAdmin
            {
                DateHeure = DateTime.UtcNow,
                Action = action,
                Categorie = categorie,
                Details = details,
                Niveau = "INFO",
                UserNom = "Super Administrateur",
                UserRole = "SuperAdmin",
                SocieteNom = "Siège Central",
                Statut = "Succès",
                AdresseIp = HttpContext?.Connection?.RemoteIpAddress?.ToString() ?? "127.0.0.1",
                Navigateur = HttpContext?.Request?.Headers["User-Agent"].ToString() ?? "GMAO Web Client"
            };
            _db.JournauxSuperAdmin.Add(log);
            await _db.SaveChangesAsync();
        }
        catch
        {
            // Fail silently on audit log failure to not break main flow
        }
    }

    private async Task EnsureSeedDataAsync()
    {
        var count = await _db.Societes.IgnoreQueryFilters().CountAsync();
        if (count < 3)
        {
            await SeedDataInternalAsync(false);
        }
    }

    private async Task SeedDataInternalAsync(bool force)
    {
        // 1. Seed Societes if empty or force
        if (force || await _db.Societes.IgnoreQueryFilters().CountAsync() < 3)
        {
            var seedSocietes = new List<Societe>
            {
                new Societe
                {
                    Nom = "Agro-Industrie Du Sud (SICAM)",
                    CodeTenant = "agro-sicam",
                    Category = "Agroalimentaire",
                    ManagerNom = "Mohamed Ben Amor",
                    Telephone = "+216 71 450 120",
                    Ville = "Mateur",
                    Gouvernorat = "Bizerte",
                    Adresse = "Zone Industrielle Mateur 7030",
                    EmailContact = "contact@sicam.com.tn",
                    SubscriptionPlan = "Enterprise Elite",
                    SubscriptionStatus = "Actif",
                    SubscriptionAmount = 1450m,
                    MaxSites = 6,
                    MaxUsers = 60,
                    MaxEquipements = 350,
                    StorageUsedGb = 42.5,
                    MaxStorageGb = 200.0,
                    CapacityTonsPerDay = 850,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow.AddMonths(-14)
                },
                new Societe
                {
                    Nom = "Conserverie Carthage SARL",
                    CodeTenant = "conserverie-carthage",
                    Category = "Agroalimentaire",
                    ManagerNom = "Sonia Trabelsi",
                    Telephone = "+216 72 230 400",
                    Ville = "Korba",
                    Gouvernorat = "Nabeul",
                    Adresse = "Route de Kelibia, Korba",
                    EmailContact = "direction@conserverie-carthage.tn",
                    SubscriptionPlan = "Business Pro",
                    SubscriptionStatus = "Actif",
                    SubscriptionAmount = 850m,
                    MaxSites = 3,
                    MaxUsers = 30,
                    MaxEquipements = 180,
                    StorageUsedGb = 18.2,
                    MaxStorageGb = 100.0,
                    CapacityTonsPerDay = 450,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow.AddMonths(-9)
                },
                new Societe
                {
                    Nom = "Société Tunisienne Des Boissons (SFBT)",
                    CodeTenant = "sfbt-group",
                    Category = "Boissons & Liquides",
                    ManagerNom = "Kamel Dridi",
                    Telephone = "+216 71 800 200",
                    Ville = "Bab Saadoun",
                    Gouvernorat = "Tunis",
                    Adresse = "Boulevard Bab Saadoun, Tunis",
                    EmailContact = "it-support@sfbt.com.tn",
                    SubscriptionPlan = "Enterprise Elite",
                    SubscriptionStatus = "Actif",
                    SubscriptionAmount = 1800m,
                    MaxSites = 8,
                    MaxUsers = 80,
                    MaxEquipements = 500,
                    StorageUsedGb = 64.0,
                    MaxStorageGb = 250.0,
                    CapacityTonsPerDay = 1200,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow.AddMonths(-18)
                },
                new Societe
                {
                    Nom = "Huilerie Moderne Du Sahel",
                    CodeTenant = "huilerie-sahel",
                    Category = "Huilerie & Oléicole",
                    ManagerNom = "Rached Bouzid",
                    Telephone = "+216 73 550 310",
                    Ville = "Moknine",
                    Gouvernorat = "Monastir",
                    Adresse = "Zone Industrielle Moknine",
                    EmailContact = "contact@huilerie-sahel.tn",
                    SubscriptionPlan = "Business Pro",
                    SubscriptionStatus = "Actif",
                    SubscriptionAmount = 850m,
                    MaxSites = 2,
                    MaxUsers = 25,
                    MaxEquipements = 120,
                    StorageUsedGb = 14.8,
                    MaxStorageGb = 100.0,
                    CapacityTonsPerDay = 300,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow.AddMonths(-6)
                },
                new Societe
                {
                    Nom = "Carthage Grains & Céréales",
                    CodeTenant = "carthage-grains",
                    Category = "Meunerie & Alimentation animale",
                    ManagerNom = "Mehdi Gharbi",
                    Telephone = "+216 71 435 600",
                    Ville = "Radès",
                    Gouvernorat = "Ben Arous",
                    Adresse = "Port de Radès, Zone Portuaire",
                    EmailContact = "admin@carthagegrains.com.tn",
                    SubscriptionPlan = "Enterprise Elite",
                    SubscriptionStatus = "Actif",
                    SubscriptionAmount = 1350m,
                    MaxSites = 4,
                    MaxUsers = 45,
                    MaxEquipements = 280,
                    StorageUsedGb = 31.4,
                    MaxStorageGb = 150.0,
                    CapacityTonsPerDay = 950,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow.AddMonths(-11)
                },
                new Societe
                {
                    Nom = "Laboratoires Pharmaceutiques Unimed",
                    CodeTenant = "unimed-pharma",
                    Category = "Pharmaceutique",
                    ManagerNom = "Dr. Leila Chaabane",
                    Telephone = "+216 73 447 000",
                    Ville = "Kalaâ Kebira",
                    Gouvernorat = "Sousse",
                    Adresse = "Zone Industrielle Kalaâ Kebira",
                    EmailContact = "maintenance@unimed.com.tn",
                    SubscriptionPlan = "Enterprise Elite",
                    SubscriptionStatus = "Actif",
                    SubscriptionAmount = 1650m,
                    MaxSites = 5,
                    MaxUsers = 50,
                    MaxEquipements = 320,
                    StorageUsedGb = 39.5,
                    MaxStorageGb = 200.0,
                    CapacityTonsPerDay = 250,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow.AddMonths(-15)
                },
                new Societe
                {
                    Nom = "PlastiPack Emballages Nord",
                    CodeTenant = "plastipack-nord",
                    Category = "Emballage & Plasturgie",
                    ManagerNom = "Nabil Khelifi",
                    Telephone = "+216 72 590 120",
                    Ville = "Menzel Bourguiba",
                    Gouvernorat = "Bizerte",
                    Adresse = "Parc d'Activités Économiques Menzel Bourguiba",
                    EmailContact = "direction@plastipack.tn",
                    SubscriptionPlan = "Starter Cloud",
                    SubscriptionStatus = "Actif",
                    SubscriptionAmount = 450m,
                    MaxSites = 1,
                    MaxUsers = 12,
                    MaxEquipements = 60,
                    StorageUsedGb = 6.2,
                    MaxStorageGb = 30.0,
                    CapacityTonsPerDay = 150,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow.AddMonths(-4)
                },
                new Societe
                {
                    Nom = "Fromagerie Des Domaines",
                    CodeTenant = "fromagerie-domaines",
                    Category = "Produits Laitiers",
                    ManagerNom = "Inès Mansouri",
                    Telephone = "+216 78 450 780",
                    Ville = "Béja",
                    Gouvernorat = "Béja",
                    Adresse = "Route de Tabarka, Béja",
                    EmailContact = "contact@fromagerie-domaines.tn",
                    SubscriptionPlan = "Starter Cloud",
                    SubscriptionStatus = "En essai",
                    SubscriptionAmount = 450m,
                    MaxSites = 1,
                    MaxUsers = 10,
                    MaxEquipements = 45,
                    StorageUsedGb = 3.5,
                    MaxStorageGb = 30.0,
                    CapacityTonsPerDay = 100,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow.AddDays(-10)
                }
            };

            foreach (var soc in seedSocietes)
            {
                var existing = await _db.Societes.IgnoreQueryFilters().FirstOrDefaultAsync(s => s.CodeTenant == soc.CodeTenant);
                if (existing == null)
                {
                    _db.Societes.Add(soc);
                }
            }
            await _db.SaveChangesAsync();
        }

        // 2. Seed Plans
        if (force || await _db.PlansOffres.IgnoreQueryFilters().CountAsync() == 0)
        {
            var seedPlans = new List<PlanOffre>
            {
                new PlanOffre
                {
                    Code = "starter",
                    Nom = "Starter Cloud",
                    Description = "Parfait pour une usine unique ou une PME industrielle démarrant la digitalisation.",
                    PrixMensuel = 450m,
                    PrixAnnuel = 4500m,
                    MaxSites = 1,
                    MaxUsers = 15,
                    MaxEquipements = 80,
                    MaxStorageGb = 30,
                    SupportLevel = "Standard (Email & Ticket)",
                    FonctionnalitesJson = "[\"Gestion des Ordres de Travail\",\"Suivi de Stock Pièces\",\"Maintenance Corrective\",\"Tableaux de bord standards\",\"Application Mobile Technicien\"]",
                    IsPopular = false,
                    IsActive = true
                },
                new PlanOffre
                {
                    Code = "pro",
                    Nom = "Business Pro",
                    Description = "Solution idéale pour les entreprises multi-sites avec maintenance préventive avancée.",
                    PrixMensuel = 850m,
                    PrixAnnuel = 8500m,
                    MaxSites = 3,
                    MaxUsers = 35,
                    MaxEquipements = 200,
                    MaxStorageGb = 100,
                    SupportLevel = "Prioritaire (Téléphone & WhatsApp 24/7)",
                    FonctionnalitesJson = "[\"Tout du forfait Starter\",\"Plans de Maintenance Préventive récurrents\",\"Planning & Calendrier interactif\",\"Gestion Multi-sites / Usines\",\"Export PDF & Excel avancé\",\"Notifications SMS & WhatsApp\"]",
                    IsPopular = true,
                    IsActive = true
                },
                new PlanOffre
                {
                    Code = "enterprise",
                    Nom = "Enterprise Elite",
                    Description = "Puissance maximale pour les groupes industriels exigeant SLA 99.9% et intégrations ERP.",
                    PrixMensuel = 1450m,
                    PrixAnnuel = 14500m,
                    MaxSites = 10,
                    MaxUsers = 100,
                    MaxEquipements = 600,
                    MaxStorageGb = 250,
                    SupportLevel = "Dédié + Compte Dédié + SLA 99.9%",
                    FonctionnalitesJson = "[\"Tout du forfait Pro\",\"Intégration API REST & Webhooks\",\"Connecteurs ERP (SAP, Sage, Odoo)\",\"Module IA Prédictive & Analyse de pannes\",\"Audit trail complet & Archivage légal\",\"SSO & Sécurité renforcée\"]",
                    IsPopular = false,
                    IsActive = true
                },
                new PlanOffre
                {
                    Code = "custom",
                    Nom = "Sur-Mesure Industriel",
                    Description = "Architecture dédiée On-Premise ou Cloud privé avec développements spécifiques.",
                    PrixMensuel = 2400m,
                    PrixAnnuel = 24000m,
                    MaxSites = 99,
                    MaxUsers = 999,
                    MaxEquipements = 9999,
                    MaxStorageGb = 1000,
                    SupportLevel = "Ingénieur Dédié sur site + 24/7",
                    FonctionnalitesJson = "[\"Déploiement On-Premise ou Cloud Souverain\",\"Nombre de sites et usines illimité\",\"Modules spécifiques sur-mesure\",\"Formation sur site des équipes\",\"Garantie de continuité d'activité\"]",
                    IsPopular = false,
                    IsActive = true
                }
            };

            foreach (var pl in seedPlans)
            {
                var existing = await _db.PlansOffres.IgnoreQueryFilters().FirstOrDefaultAsync(p => p.Code == pl.Code);
                if (existing == null) _db.PlansOffres.Add(pl);
            }
            await _db.SaveChangesAsync();
        }

        // 3. Seed Sites Usines
        if (force || await _db.SitesUsines.IgnoreQueryFilters().CountAsync() == 0)
        {
            var firstTenant = await _db.Societes.IgnoreQueryFilters().FirstOrDefaultAsync(s => s.CodeTenant == "agro-sicam") ?? await _db.Societes.IgnoreQueryFilters().FirstOrDefaultAsync();
            var secondTenant = await _db.Societes.IgnoreQueryFilters().FirstOrDefaultAsync(s => s.CodeTenant == "conserverie-carthage") ?? firstTenant;
            var sfbt = await _db.Societes.IgnoreQueryFilters().FirstOrDefaultAsync(s => s.CodeTenant == "sfbt-group") ?? firstTenant;

            if (firstTenant != null)
            {
                var seedSites = new List<SiteUsine>
                {
                    new SiteUsine
                    {
                        SocieteId = firstTenant.Id,
                        Nom = "Usine Mateur Principale",
                        CodeSite = "MAT-01",
                        Ville = "Mateur",
                        Gouvernorat = "Bizerte",
                        Adresse = "Zone Industrielle Mateur 7030",
                        Type = "Usine de Transformation Tomates",
                        Statut = "Opérationnel",
                        CapaciteProduction = "550 T/jour",
                        NombreEquipements = 48,
                        NombreUtilisateurs = 22,
                        Responsable = "Ing. Karim Ben Salah",
                        Telephone = "+216 72 450 120",
                        Email = "karim.bensalah@sicam.com.tn"
                    },
                    new SiteUsine
                    {
                        SocieteId = firstTenant.Id,
                        Nom = "Unité de Concentration Zaghouan",
                        CodeSite = "ZAG-02",
                        Ville = "Zaghouan",
                        Gouvernorat = "Zaghouan",
                        Adresse = "Route Nationale 3, Zaghouan",
                        Type = "Unité d'Évaporation & Concentration",
                        Statut = "Opérationnel",
                        CapaciteProduction = "300 T/jour",
                        NombreEquipements = 32,
                        NombreUtilisateurs = 15,
                        Responsable = "Ing. Firas Mansour",
                        Telephone = "+216 72 680 340",
                        Email = "firas.mansour@sicam.com.tn"
                    },
                    new SiteUsine
                    {
                        SocieteId = secondTenant?.Id ?? firstTenant.Id,
                        Nom = "Site Korba Conserverie",
                        CodeSite = "KRB-01",
                        Ville = "Korba",
                        Gouvernorat = "Nabeul",
                        Adresse = "Route de Kelibia, Korba",
                        Type = "Usine de Conserves & Pâtes",
                        Statut = "Opérationnel",
                        CapaciteProduction = "450 T/jour",
                        NombreEquipements = 42,
                        NombreUtilisateurs = 19,
                        Responsable = "Ing. Walid Trad",
                        Telephone = "+216 72 388 900",
                        Email = "walid.trad@carthage.tn"
                    },
                    new SiteUsine
                    {
                        SocieteId = sfbt?.Id ?? firstTenant.Id,
                        Nom = "Usine Centrale Bab Saadoun",
                        CodeSite = "TUN-01",
                        Ville = "Bab Saadoun",
                        Gouvernorat = "Tunis",
                        Adresse = "Avenue de la République, Tunis",
                        Type = "Unité d'Embouteillage & Brassage",
                        Statut = "Opérationnel",
                        CapaciteProduction = "800 T/jour",
                        NombreEquipements = 74,
                        NombreUtilisateurs = 36,
                        Responsable = "Ing. Sami Zouari",
                        Telephone = "+216 71 800 240",
                        Email = "sami.zouari@sfbt.com.tn"
                    },
                    new SiteUsine
                    {
                        SocieteId = sfbt?.Id ?? firstTenant.Id,
                        Nom = "Plateforme Logistique Mégrine",
                        CodeSite = "MEG-LOG-01",
                        Ville = "Mégrine",
                        Gouvernorat = "Ben Arous",
                        Adresse = "Zone Industrielle Mégrine Riadh",
                        Type = "Centre Logistique & Maintenance Flotte",
                        Statut = "Opérationnel",
                        CapaciteProduction = "350 T/jour",
                        NombreEquipements = 28,
                        NombreUtilisateurs = 14,
                        Responsable = "Ing. Anis Jlassi",
                        Telephone = "+216 71 295 400",
                        Email = "anis.jlassi@sfbt.com.tn"
                    }
                };

                _db.SitesUsines.AddRange(seedSites);
                await _db.SaveChangesAsync();
            }
        }

        // 4. Seed Factures
        if (force || await _db.Factures.IgnoreQueryFilters().CountAsync() == 0)
        {
            var sicam = await _db.Societes.IgnoreQueryFilters().FirstOrDefaultAsync(s => s.CodeTenant == "agro-sicam");
            var carthage = await _db.Societes.IgnoreQueryFilters().FirstOrDefaultAsync(s => s.CodeTenant == "conserverie-carthage");
            var sfbt = await _db.Societes.IgnoreQueryFilters().FirstOrDefaultAsync(s => s.CodeTenant == "sfbt-group");

            var seedInvoices = new List<Facture>
            {
                new Facture
                {
                    NumeroFacture = "FAC-2026-0048",
                    SocieteId = sicam?.Id ?? 1,
                    SocieteNom = sicam?.Nom ?? "Agro-Industrie Du Sud (SICAM)",
                    MontantHT = 1450m,
                    TauxTVA = 19m,
                    MontantTTC = 1725.50m,
                    Statut = "Payée",
                    MethodePaiement = "Virement bancaire",
                    DateEmission = DateTime.UtcNow.AddDays(-12),
                    DateEcheance = DateTime.UtcNow.AddDays(18),
                    DatePaiement = DateTime.UtcNow.AddDays(-8),
                    Periode = "Mars 2026",
                    Plan = "Enterprise Elite",
                    Notes = "Abonnement mensuel SaaS - Validé par BIAT"
                },
                new Facture
                {
                    NumeroFacture = "FAC-2026-0047",
                    SocieteId = carthage?.Id ?? 1,
                    SocieteNom = carthage?.Nom ?? "Conserverie Carthage SARL",
                    MontantHT = 850m,
                    TauxTVA = 19m,
                    MontantTTC = 1011.50m,
                    Statut = "Payée",
                    MethodePaiement = "Chèque certifié",
                    DateEmission = DateTime.UtcNow.AddDays(-15),
                    DateEcheance = DateTime.UtcNow.AddDays(15),
                    DatePaiement = DateTime.UtcNow.AddDays(-10),
                    Periode = "Mars 2026",
                    Plan = "Business Pro",
                    Notes = "Abonnement mensuel régularisé"
                },
                new Facture
                {
                    NumeroFacture = "FAC-2026-0046",
                    SocieteId = sfbt?.Id ?? 1,
                    SocieteNom = sfbt?.Nom ?? "Société Tunisienne Des Boissons (SFBT)",
                    MontantHT = 1800m,
                    TauxTVA = 19m,
                    MontantTTC = 2142.00m,
                    Statut = "Payée",
                    MethodePaiement = "Virement bancaire",
                    DateEmission = DateTime.UtcNow.AddDays(-20),
                    DateEcheance = DateTime.UtcNow.AddDays(10),
                    DatePaiement = DateTime.UtcNow.AddDays(-16),
                    Periode = "Mars 2026",
                    Plan = "Enterprise Elite",
                    Notes = "Option multi-usines incluse"
                },
                new Facture
                {
                    NumeroFacture = "FAC-2026-0049",
                    SocieteId = sicam?.Id ?? 1,
                    SocieteNom = sicam?.Nom ?? "Agro-Industrie Du Sud (SICAM)",
                    MontantHT = 1450m,
                    TauxTVA = 19m,
                    MontantTTC = 1725.50m,
                    Statut = "En attente",
                    MethodePaiement = "Virement bancaire",
                    DateEmission = DateTime.UtcNow.AddDays(-2),
                    DateEcheance = DateTime.UtcNow.AddDays(28),
                    Periode = "Avril 2026",
                    Plan = "Enterprise Elite",
                    Notes = "Facture proforma renouvellement"
                }
            };

            _db.Factures.AddRange(seedInvoices);
            await _db.SaveChangesAsync();
        }

        // 5. Seed Logs
        if (force || await _db.JournauxSuperAdmin.IgnoreQueryFilters().CountAsync() == 0)
        {
            var seedLogs = new List<JournalSuperAdmin>
            {
                new JournalSuperAdmin
                {
                    DateHeure = DateTime.UtcNow.AddMinutes(-12),
                    Niveau = "INFO",
                    Action = "Connexion SuperAdmin",
                    Categorie = "Auth",
                    UserNom = "Admin Principal",
                    UserRole = "SuperAdmin",
                    SocieteNom = "Siège Central",
                    AdresseIp = "197.0.12.84",
                    Navigateur = "Chrome 122 (Windows)",
                    Details = "Authentification réussie avec clé 2FA.",
                    Statut = "Succès"
                },
                new JournalSuperAdmin
                {
                    DateHeure = DateTime.UtcNow.AddMinutes(-48),
                    Niveau = "INFO",
                    Action = "Sauvegarde Base Automatique",
                    Categorie = "Système",
                    UserNom = "Cron Service",
                    UserRole = "System",
                    SocieteNom = "Cluster Global",
                    AdresseIp = "127.0.0.1",
                    Navigateur = "Daemon",
                    Details = "Snapshot PostgreSQL complet chiffré et téléversé vers Wasabi S3 (142 Mo).",
                    Statut = "Succès"
                },
                new JournalSuperAdmin
                {
                    DateHeure = DateTime.UtcNow.AddHours(-3),
                    Niveau = "INFO",
                    Action = "Mise à niveau Abonnement",
                    Categorie = "Billing",
                    UserNom = "Sonia Trabelsi",
                    UserRole = "Administrateur",
                    SocieteNom = "Conserverie Carthage",
                    AdresseIp = "196.203.45.12",
                    Navigateur = "Firefox 124 (macOS)",
                    Details = "Passage de Starter Cloud à Business Pro validé.",
                    Statut = "Succès"
                },
                new JournalSuperAdmin
                {
                    DateHeure = DateTime.UtcNow.AddHours(-6),
                    Niveau = "WARNING",
                    Action = "Quota Stockage 80%",
                    Categorie = "Système",
                    UserNom = "Moniteur Quota",
                    UserRole = "System",
                    SocieteNom = "Agro-Industrie Du Sud",
                    AdresseIp = "127.0.0.1",
                    Navigateur = "Daemon",
                    Details = "L'espace disque consommé a atteint 42.5 Go sur 50 Go alloués.",
                    Statut = "Avertissement"
                },
                new JournalSuperAdmin
                {
                    DateHeure = DateTime.UtcNow.AddHours(-18),
                    Niveau = "SECURITY",
                    Action = "Tentative Connexion Bloquée",
                    Categorie = "Sécurité",
                    UserNom = "Anonyme",
                    UserRole = "Visiteur",
                    SocieteNom = "Inconnue",
                    AdresseIp = "45.134.20.91",
                    Navigateur = "Python-requests/2.28",
                    Details = "Blocage par le pare-feu applicatif suite à 5 échecs de mot de passe consécutifs.",
                    Statut = "Échec"
                }
            };

            _db.JournauxSuperAdmin.AddRange(seedLogs);
            await _db.SaveChangesAsync();
        }

        // 6. Seed Settings
        if (force || await _db.PlatformSettings.IgnoreQueryFilters().CountAsync() == 0)
        {
            var defaultSettings = new PlatformSettings
            {
                NomPlateforme = "GMAO SaaS Pro - Agro-Industrie",
                VersionPlateforme = "v2.5.4-LTS",
                UrlPlateforme = "https://gmao-saas.com",
                SupportEmail = "contact@gmao-saas.com",
                SupportTelephone = "+216 71 889 000",
                Devise = "TND (DT)",
                TauxTvaParDefaut = 19.0m,
                MaintenanceMode = false,
                AllowNewRegistrations = true,
                DefaultTrialDays = 14,
                SmtpHost = "smtp.mailgun.org",
                SmtpPort = 587,
                SmtpUser = "notifications@gmao-saas.com",
                SmtpSsl = true,
                StorageProvider = "AWS S3 / Wasabi",
                BackupAuto = true,
                BackupFrequence = "Quotidien (02:00 AM)",
                RetentionJours = 30,
                UpdatedAt = DateTime.UtcNow
            };
            _db.PlatformSettings.Add(defaultSettings);
            await _db.SaveChangesAsync();
        }
    }
}

// ───────────────────────────────────────────────────────────────────────────
// DTOs POUR REQUÊTES ENTRANTE
// ───────────────────────────────────────────────────────────────────────────
public class SubscriptionUpdateDto
{
    public string? Plan { get; set; }
    public string? Status { get; set; }
    public decimal Amount { get; set; }
    public DateTime? RenewalDate { get; set; }
}

public class UserCreateDto
{
    public string Nom { get; set; } = string.Empty;
    public string? Prenom { get; set; }
    public string Email { get; set; } = string.Empty;
    public string? Telephone { get; set; }
    public int RoleId { get; set; }
    public int? SocieteId { get; set; }
    public bool IsActive { get; set; } = true;
}

public class UserUpdateDto
{
    public string? Nom { get; set; }
    public string? Prenom { get; set; }
    public string? Email { get; set; }
    public string? Telephone { get; set; }
    public int RoleId { get; set; }
    public int? SocieteId { get; set; }
    public bool IsActive { get; set; } = true;
}
