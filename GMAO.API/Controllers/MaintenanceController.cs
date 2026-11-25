using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using GMAO.Domain.Entities;
using GMAO.Domain.Interfaces;
using GMAO.Application.Interfaces;
using GMAO.Application.DTOs.Equipements;
using GMAO.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace GMAO.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MaintenanceController : ControllerBase
{
    private readonly IMaintenanceService _maintenanceService;
    private readonly ITenantContextService _tenantContext;

    public MaintenanceController(
        IMaintenanceService maintenanceService,
        ITenantContextService tenantContext)
    {
        _maintenanceService = maintenanceService;
        _tenantContext = tenantContext;
    }

    private static string FormatFileSize(long bytes)
    {
        if (bytes >= 1024 * 1024)
            return $"{bytes / (1024.0 * 1024.0):0.#} MB";
        if (bytes >= 1024)
            return $"{bytes / 1024.0:0.#} KB";
        return $"{bytes} B";
    }

    private int GetSocieteId(GmaoDbContext? context = null) => _tenantContext.GetRequiredTenantId();

    private int GetCurrentUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("id")?.Value
            ?? User.FindFirst("sub")?.Value;
        return int.TryParse(claim, out int uid) ? uid : 0;
    }

    /// <summary>
    /// Fast Aggregated Bootstrap Endpoint.
    /// Returns essential tenant collections in a single round-trip with .AsNoTracking(),
    /// strictly enforcing RBAC scopes (_ALL, _TEAM, _OWN) on Incidents and WorkOrders.
    /// </summary>
    [HttpGet("bootstrap")]
    [Authorize]
    public async Task<IActionResult> GetBootstrapData(
        [FromServices] GmaoDbContext context,
        [FromServices] IEquipementRepository equipementRepository,
        [FromServices] IEquipeService equipeService,
        [FromServices] IUserRepository userRepository)
    {
        int societeId = GetSocieteId(context);
        int currentUserId = GetCurrentUserId();
        var isAdminOrCompanyAdmin = GMAO.Domain.Constants.GmaoRoles.IsCompanyAdmin(User);

        int? currentTeamId = null;
        if (currentUserId > 0)
        {
            currentTeamId = await context.Users.AsNoTracking()
                .Where(u => u.Id == currentUserId && u.SocieteId == societeId)
                .Select(u => u.EquipeId)
                .FirstOrDefaultAsync();
        }

        // 1. Equipements (SplitQuery + NoTracking)
        var equipements = await equipementRepository.GetAllWithDetailsAsync(societeId);
        var equipementDtos = equipements.Select(e => new EquipementDto
        {
            Id = e.Id,
            SocieteId = e.SocieteId,
            Code = e.Code,
            Designation = e.Designation,
            FamilleId = e.FamilleId,
            FamilleNom = e.Famille?.Nom ?? "",
            LocalisationId = e.LocalisationId,
            LocalisationNom = e.Localisation?.Nom ?? "",
            Marque = e.Marque,
            Modele = e.Modele,
            NumeroSerie = e.NumeroSerie,
            DateAchat = e.DateAchat,
            DateMiseEnService = e.DateMiseEnService,
            DateFinGarantie = e.DateFinGarantie,
            Criticite = (int)e.Criticite == 0 ? "Moyenne" : e.Criticite.ToString(),
            Etat = (int)e.Etat == 0 ? "EnService" : e.Etat.ToString(),
            FournisseurId = e.FournisseurId,
            FournisseurNom = e.Fournisseur?.Nom,
            PhotoUrl = e.PhotoUrl,
            Notes = e.Notes,
            CreatedAt = e.CreatedAt,
            UpdatedAt = e.UpdatedAt,
            PiecesIds = e.Pieces != null ? e.Pieces.Select(p => p.Id).ToList() : new List<int>(),
            Documents = e.Documents != null ? e.Documents.Select(d => new GMAO.Application.DTOs.Documents.DocumentDto
            {
                Id = d.Id,
                Name = d.Nom,
                Type = d.Type.ToString().ToLower(),
                Size = FormatFileSize(d.TailleFichierOctets),
                Url = $"/api/Document/{d.Id}/file",
                MimeType = d.MimeType,
                EquipementId = d.EquipementId,
                CreatedAt = d.CreatedAt
            }).ToList() : new List<GMAO.Application.DTOs.Documents.DocumentDto>()
        }).ToList();

        // 2. Fournisseurs
        var fournisseurs = await context.Fournisseurs.AsNoTracking()
            .Where(f => f.SocieteId == societeId)
            .ToListAsync();

        // 3. Pieces
        var pieces = await context.Pieces.AsNoTracking()
            .Where(p => p.SocieteId == societeId)
            .OrderBy(p => p.Reference)
            .ToListAsync();

        // 4. FamillePieces
        var pieceCategories = await context.FamillesPieces.AsNoTracking().ToListAsync();

        // 5. Demandes d'Intervention (Incidents) avec application stricte des scopes RBAC
        IQueryable<DemandeIntervention> incQuery = context.DemandesIntervention
            .AsNoTracking()
            .Where(i => i.SocieteId == societeId);

        var incScope = GMAO.Application.Security.GmaoPermissions.GetScope(User, GMAO.Application.Security.GmaoPermissions.IncidentView);
        if (incScope == GMAO.Application.Security.AccessScope.None)
        {
            incQuery = incQuery.Where(i => false);
        }
        else if (incScope == GMAO.Application.Security.AccessScope.Team)
        {
            incQuery = currentTeamId.HasValue
                ? incQuery.Where(i => i.DemandeurId == currentUserId || (i.Demandeur != null && i.Demandeur.EquipeId == currentTeamId.Value))
                : incQuery.Where(i => i.DemandeurId == currentUserId);
        }
        else if (incScope == GMAO.Application.Security.AccessScope.Own)
        {
            incQuery = incQuery.Where(i => i.DemandeurId == currentUserId);
        }

        var incidents = await incQuery
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync();

        // 6. Ordres de Travail (WorkOrders) avec application stricte des scopes RBAC
        IQueryable<OrdresTravail> otQuery = context.OrdresTravail
            .AsNoTracking()
            .Include(o => o.Campagne)
            .Include(o => o.Intervention)
            .Include(o => o.MouvementsStock)
                .ThenInclude(m => m.Piece)
            .Where(o => o.SocieteId == societeId);

        var otScope = GMAO.Application.Security.GmaoPermissions.GetScope(User, GMAO.Application.Security.GmaoPermissions.WorkOrderView);
        if (otScope == GMAO.Application.Security.AccessScope.None)
        {
            otQuery = otQuery.Where(ot => false);
        }
        else if (otScope == GMAO.Application.Security.AccessScope.Team)
        {
            otQuery = currentTeamId.HasValue
                ? otQuery.Where(ot =>
                    ot.TechnicienId == currentUserId ||
                    ot.ResponsableId == currentUserId ||
                    (ot.Technicien != null && ot.Technicien.EquipeId == currentTeamId.Value) ||
                    (ot.Responsable != null && ot.Responsable.EquipeId == currentTeamId.Value))
                : otQuery.Where(ot => ot.TechnicienId == currentUserId || ot.ResponsableId == currentUserId);
        }
        else if (otScope == GMAO.Application.Security.AccessScope.Own)
        {
            otQuery = otQuery.Where(ot => ot.TechnicienId == currentUserId || ot.ResponsableId == currentUserId);
        }

        var ordres = await otQuery
            .OrderByDescending(o => o.DateCreation)
            .ToListAsync();

        // 7. Campagnes
        var campagnes = await context.Campagnes.AsNoTracking()
            .Where(c => c.SocieteId == societeId)
            .OrderByDescending(c => c.DateDebut)
            .ToListAsync();

        // 8. Equipes
        var equipes = await equipeService.GetEquipesAsync(societeId);

        // 9. Users (strictement isolés à la société, exclusion absolue de SuperAdmin et comptes orphelins)
        var allUsers = (await userRepository.GetAllAsync())
            .Where(u => u.Role?.Nom != "SuperAdmin" && u.RoleId != 7 && u.SocieteId == societeId)
            .ToList();

        // 10. Mouvements de Stock (derniers 200)
        var mouvements = await context.MouvementsStock.AsNoTracking()
            .Include(m => m.Piece)
            .Where(m => m.Piece != null && m.Piece.SocieteId == societeId)
            .OrderByDescending(m => m.Date)
            .Take(200)
            .ToListAsync();

        return Ok(new
        {
            equipments = equipementDtos,
            suppliers = fournisseurs,
            parts = pieces,
            pieceCategories,
            incidents,
            workOrders = ordres,
            campaigns = campagnes,
            equipes,
            users = allUsers,
            movements = mouvements
        });
    }

    [HttpPost("declare-breakdown")]
    [Authorize]
    public async Task<IActionResult> DeclareBreakdown([FromQuery] int equipementId, [FromQuery] int demandeurId, [FromBody] string description)
    {
        var societeId = GetSocieteId();
        var result = await _maintenanceService.DeclareBreakdownAsync(equipementId, demandeurId, description);
        return Ok(result);
    }

    [HttpPost("create-ot")]
    [Authorize(Policy = "WorkOrderCreate")]
    public async Task<IActionResult> CreateOT([FromQuery] int demandId, [FromQuery] int responsableId, [FromQuery] int? technicienId, [FromBody] string description)
    {
        var societeId = GetSocieteId();
        var result = await _maintenanceService.CreateWorkOrderAsync(demandId, responsableId, technicienId, description);
        return Ok(result);
    }

    [HttpPost("complete-ot")]
    [Authorize(Policy = "WorkOrderUpdate")]
    public async Task<IActionResult> CompleteOT([FromQuery] int otId, [FromQuery] string diagnostic, [FromQuery] string solution, [FromQuery] decimal tempsPasse)
    {
        var societeId = GetSocieteId();
        var result = await _maintenanceService.CompleteWorkOrderAsync(otId, diagnostic, solution, tempsPasse);
        return Ok(result);
    }
}
