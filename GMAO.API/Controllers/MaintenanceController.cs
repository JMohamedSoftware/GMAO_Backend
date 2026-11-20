using System;
using System.Collections.Generic;
using System.Linq;
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

    public MaintenanceController(IMaintenanceService maintenanceService)
    {
        _maintenanceService = maintenanceService;
    }

    private static string FormatFileSize(long bytes)
    {
        if (bytes >= 1024 * 1024)
            return $"{bytes / (1024.0 * 1024.0):0.#} MB";
        if (bytes >= 1024)
            return $"{bytes / 1024.0:0.#} KB";
        return $"{bytes} B";
    }

    private int GetSocieteId(GmaoDbContext context)
    {
        var tenantHeader = Request.Headers["X-Tenant-Id"].FirstOrDefault()?.Trim();
        if (!string.IsNullOrEmpty(tenantHeader) && tenantHeader != "null" && tenantHeader != "undefined")
        {
            if (int.TryParse(tenantHeader, out int tenantId) && tenantId > 0)
                return tenantId;

            var soc = context.Societes.AsNoTracking().FirstOrDefault(s => s.CodeTenant == tenantHeader);
            if (soc != null && soc.Id > 0)
                return soc.Id;
        }

        var societeIdClaim = User.FindFirst("SocieteId")?.Value;
        if (int.TryParse(societeIdClaim, out int societeId) && societeId > 0)
            return societeId;

        return 1;
    }

    /// <summary>
    /// Fast Aggregated Bootstrap Endpoint.
    /// Returns essential tenant collections in a single round-trip with .AsNoTracking().
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
        var isSuperAdmin = User.IsInRole("SuperAdmin");
        var hasTenantHeader = Request.Headers.ContainsKey("X-Tenant-Id");
        IQueryable<Fournisseur> fourQuery = context.Fournisseurs.AsNoTracking();
        if (hasTenantHeader || !isSuperAdmin)
            fourQuery = fourQuery.Where(f => f.SocieteId == societeId);
        var fournisseurs = await fourQuery.ToListAsync();

        // 3. Pieces
        var pieces = await context.Pieces.AsNoTracking()
            .Where(p => p.SocieteId == societeId || (societeId == 1 && (p.SocieteId == null || p.SocieteId == 0)))
            .OrderBy(p => p.Reference)
            .ToListAsync();

        // 4. FamillePieces
        var pieceCategories = await context.FamillesPieces.AsNoTracking().ToListAsync();

        // 5. Demandes d'Intervention (Incidents)
        var incidents = await context.DemandesIntervention.AsNoTracking()
            .Where(i => i.SocieteId == societeId)
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync();

        // 6. Ordres de Travail (WorkOrders)
        var ordres = await context.OrdresTravail.AsNoTracking()
            .Include(o => o.Campagne)
            .Where(o => o.SocieteId == societeId)
            .OrderByDescending(o => o.DateCreation)
            .ToListAsync();

        // 7. Campagnes
        var campagnes = await context.Campagnes.AsNoTracking()
            .Where(c => c.SocieteId == societeId)
            .OrderByDescending(c => c.DateDebut)
            .ToListAsync();

        // 8. Equipes
        var equipes = await equipeService.GetEquipesAsync(societeId);

        // 9. Users
        var allUsers = await userRepository.GetAllAsync();
        if (!isSuperAdmin)
        {
            allUsers = allUsers.Where(u => u.Role?.Nom != "SuperAdmin" && u.RoleId != 7 && (u.SocieteId == null || u.SocieteId == societeId)).ToList();
        }

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
    [Authorize] // Simple authentication is enough to report a breakdown usually
    public async Task<IActionResult> DeclareBreakdown([FromQuery] int equipementId, [FromQuery] int demandeurId, [FromBody] string description)
    {
        var result = await _maintenanceService.DeclareBreakdownAsync(equipementId, demandeurId, description);
        return Ok(result);
    }

    [HttpPost("create-ot")]
    [Authorize(Policy = "WorkOrderCreate")]
    public async Task<IActionResult> CreateOT([FromQuery] int demandId, [FromQuery] int responsableId, [FromQuery] int? technicienId, [FromBody] string description)
    {
        var result = await _maintenanceService.CreateWorkOrderAsync(demandId, responsableId, technicienId, description);
        return Ok(result);
    }

    [HttpPost("complete-ot")]
    [Authorize(Policy = "WorkOrderUpdate")]
    public async Task<IActionResult> CompleteOT([FromQuery] int otId, [FromQuery] string diagnostic, [FromQuery] string solution, [FromQuery] decimal tempsPasse)
    {
        var result = await _maintenanceService.CompleteWorkOrderAsync(otId, diagnostic, solution, tempsPasse);
        return Ok(result);
    }
}
