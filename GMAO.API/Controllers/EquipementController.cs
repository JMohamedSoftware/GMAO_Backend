using System.Threading.Tasks;
using System.Linq;
using System.Security.Claims;
using GMAO.Domain.Entities;
using GMAO.Domain.Interfaces;
using GMAO.Application.DTOs.Equipements;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

using GMAO.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace GMAO.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class EquipementController : ControllerBase
{
    private readonly IEquipementRepository _equipementRepository;
    private readonly IGenericRepository<Piece> _pieceRepository;
    private readonly GmaoDbContext _context;

    public EquipementController(
        IEquipementRepository equipementRepository, 
        IGenericRepository<Piece> pieceRepository,
        GmaoDbContext context)
    {
        _equipementRepository = equipementRepository;
        _pieceRepository = pieceRepository;
        _context = context;
    }

    private int GetSocieteId()
    {
        var tenantHeader = Request.Headers["X-Tenant-Id"].FirstOrDefault()?.Trim();
        if (!string.IsNullOrEmpty(tenantHeader) && tenantHeader != "null" && tenantHeader != "undefined")
        {
            if (int.TryParse(tenantHeader, out int tenantId) && tenantId > 0)
                return tenantId;

            var soc = _context.Societes.AsNoTracking().FirstOrDefault(s => s.CodeTenant == tenantHeader);
            if (soc != null && soc.Id > 0)
                return soc.Id;
        }

        var societeIdClaim = User.FindFirst("SocieteId")?.Value;
        if (int.TryParse(societeIdClaim, out int societeId) && societeId > 0)
            return societeId;

        return 1;
    }

    private static string FormatFileSize(long bytes)
    {
        if (bytes >= 1024 * 1024)
            return $"{bytes / (1024.0 * 1024.0):0.#} MB";
        if (bytes >= 1024)
            return $"{bytes / 1024.0:0.#} KB";
        return $"{bytes} B";
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var societeId = GetSocieteId();
        var equipements = await _equipementRepository.GetAllWithDetailsAsync(societeId);
        
        var dtos = equipements.Select(e => new EquipementDto
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
        });

        return Ok(dtos);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var societeId = GetSocieteId();
        var e = await _equipementRepository.GetByIdWithDetailsAsync(id, societeId);
        if (e == null) return NotFound();

        var dto = new EquipementDto
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
        };

        return Ok(dto);
    }

    private async Task<int> ResolveFamilleIdAsync(int? familleId, string? categoryOrNom)
    {
        // 1. Prioritize user-provided category name
        if (!string.IsNullOrWhiteSpace(categoryOrNom))
        {
            var raw = categoryOrNom.Trim();
            var lower = raw.ToLower();

            // Direct match in DB
            var direct = await _context.Familles.FirstOrDefaultAsync(f => f.Nom.ToLower() == lower);
            if (direct != null) return direct.Id;

            // Keyword fuzzy match
            if (lower.Contains("chaud") || lower.Contains("chod")) // Chaudières, Chaudière, chodir, chaudiere
            {
                var chaud = await _context.Familles.FirstOrDefaultAsync(f => f.Nom.ToLower().Contains("chaud"));
                if (chaud != null) return chaud.Id;
            }
            else if (lower.Contains("pomp")) // Pompes, Pompe
            {
                var pomp = await _context.Familles.FirstOrDefaultAsync(f => f.Nom.ToLower().Contains("pomp"));
                if (pomp != null) return pomp.Id;
            }
            else if (lower.Contains("convoy"))
            {
                var conv = await _context.Familles.FirstOrDefaultAsync(f => f.Nom.ToLower().Contains("convoy"));
                if (conv != null) return conv.Id;
            }
            else if (lower.Contains("compress"))
            {
                var comp = await _context.Familles.FirstOrDefaultAsync(f => f.Nom.ToLower().Contains("compress"));
                if (comp != null) return comp.Id;
            }
            else if (lower.Contains("moteur"))
            {
                var mot = await _context.Familles.FirstOrDefaultAsync(f => f.Nom.ToLower().Contains("moteur"));
                if (mot != null) return mot.Id;
            }
            else if (lower.Contains("refrig") || lower.Contains("réfrig") || lower.Contains("frigo"))
            {
                var refg = await _context.Familles.FirstOrDefaultAsync(f => f.Nom.ToLower().Contains("réfrig"));
                if (refg != null) return refg.Id;
            }
            else if (lower.Contains("steril") || lower.Contains("stéril") || lower.Contains("autoclave"))
            {
                var ster = await _context.Familles.FirstOrDefaultAsync(f => f.Nom.ToLower().Contains("stéril"));
                if (ster != null) return ster.Id;
            }
            else if (lower.Contains("rempliss") || lower.Contains("dos"))
            {
                var remp = await _context.Familles.FirstOrDefaultAsync(f => f.Nom.ToLower().Contains("rempliss"));
                if (remp != null) return remp.Id;
            }

            // Create new Famille in PostgreSQL DB for custom category
            try
            {
                var newFam = new Famille
                {
                    Nom = raw,
                    Description = $"Famille {raw}"
                };
                _context.Familles.Add(newFam);
                await _context.SaveChangesAsync();
                return newFam.Id;
            }
            catch
            {
                var fallback = await _context.Familles.FirstOrDefaultAsync(f => f.Nom.ToLower() == lower);
                if (fallback != null) return fallback.Id;
            }
        }

        // 2. Fallback to familleId if valid
        if (familleId.HasValue && familleId.Value > 0)
        {
            var exists = await _context.Familles.AnyAsync(f => f.Id == familleId.Value);
            if (exists) return familleId.Value;
        }

        return 1;
    }

    private static GMAO.Domain.Enums.CriticiteEquipement ParseCriticite(string? c)
    {
        if (string.IsNullOrWhiteSpace(c)) return GMAO.Domain.Enums.CriticiteEquipement.Moyenne;
        var s = c.Trim().ToLower().Replace(" ", "").Replace("_", "");
        if (s == "1" || s == "faible" || s == "low") return GMAO.Domain.Enums.CriticiteEquipement.Faible;
        if (s == "2" || s == "moyenne" || s == "medium") return GMAO.Domain.Enums.CriticiteEquipement.Moyenne;
        if (s == "3" || s == "haute" || s == "high") return GMAO.Domain.Enums.CriticiteEquipement.Haute;
        if (s == "4" || s == "critique" || s == "critical") return GMAO.Domain.Enums.CriticiteEquipement.Critique;
        return GMAO.Domain.Enums.CriticiteEquipement.Moyenne;
    }

    private static GMAO.Domain.Enums.EtatEquipement ParseEtat(string? e)
    {
        if (string.IsNullOrWhiteSpace(e)) return GMAO.Domain.Enums.EtatEquipement.EnService;
        var s = e.Trim().ToLower().Replace(" ", "").Replace("_", "");
        if (s == "1" || s == "enservice" || s == "service") return GMAO.Domain.Enums.EtatEquipement.EnService;
        if (s == "2" || s == "enpanne" || s == "panne") return GMAO.Domain.Enums.EtatEquipement.EnPanne;
        if (s == "3" || s == "enmaintenance" || s == "maintenance") return GMAO.Domain.Enums.EtatEquipement.EnMaintenance;
        if (s == "4" || s == "horsservice") return GMAO.Domain.Enums.EtatEquipement.HorsService;
        if (s == "5" || s == "enattente") return GMAO.Domain.Enums.EtatEquipement.EnAttente;
        return GMAO.Domain.Enums.EtatEquipement.EnService;
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] EquipementInputDto dto)
    {
        var societeId = GetSocieteId();
        
        var designation = dto.Designation ?? dto.Name ?? "Nouvel Équipement";
        var code = string.IsNullOrWhiteSpace(dto.Code) ? $"EQ-{DateTime.UtcNow.Ticks % 100000:D5}" : dto.Code;
        var familleId = await ResolveFamilleIdAsync(dto.FamilleId, dto.Category ?? dto.FamilleNom);

        var equipement = new Equipement
        {
            SocieteId = societeId,
            Code = code,
            Designation = designation,
            FamilleId = familleId,
            LocalisationId = dto.LocalisationId,
            Marque = dto.Marque ?? dto.Brand,
            Modele = dto.Modele ?? dto.Model,
            NumeroSerie = dto.NumeroSerie ?? dto.SerialNumber,
            DateAchat = dto.DateAchat,
            DateMiseEnService = dto.DateMiseEnService ?? DateOnly.FromDateTime(DateTime.UtcNow),
            DateFinGarantie = dto.DateFinGarantie,
            Criticite = ParseCriticite(dto.Criticite ?? dto.Criticality),
            Etat = ParseEtat(dto.Etat ?? dto.Status),
            FournisseurId = dto.FournisseurId,
            PhotoUrl = dto.PhotoUrl,
            Notes = dto.Notes,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        
        await _equipementRepository.AddAsync(equipement);
        
        var created = await _equipementRepository.GetByIdWithDetailsAsync(equipement.Id, societeId);
        var resDto = new EquipementDto
        {
            Id = created?.Id ?? equipement.Id,
            SocieteId = societeId,
            Code = created?.Code ?? equipement.Code,
            Designation = created?.Designation ?? equipement.Designation,
            FamilleId = created?.FamilleId ?? equipement.FamilleId,
            FamilleNom = created?.Famille?.Nom ?? "",
            LocalisationId = created?.LocalisationId ?? equipement.LocalisationId,
            LocalisationNom = created?.Localisation?.Nom ?? "",
            Marque = created?.Marque ?? equipement.Marque,
            Modele = created?.Modele ?? equipement.Modele,
            NumeroSerie = created?.NumeroSerie ?? equipement.NumeroSerie,
            DateAchat = created?.DateAchat ?? equipement.DateAchat,
            DateMiseEnService = created?.DateMiseEnService ?? equipement.DateMiseEnService,
            DateFinGarantie = created?.DateFinGarantie ?? equipement.DateFinGarantie,
            Criticite = (int)(created?.Criticite ?? equipement.Criticite) == 0 ? "Moyenne" : (created?.Criticite ?? equipement.Criticite).ToString(),
            Etat = (int)(created?.Etat ?? equipement.Etat) == 0 ? "EnService" : (created?.Etat ?? equipement.Etat).ToString(),
            FournisseurId = created?.FournisseurId ?? equipement.FournisseurId,
            FournisseurNom = created?.Fournisseur?.Nom,
            PhotoUrl = created?.PhotoUrl ?? equipement.PhotoUrl,
            Notes = created?.Notes ?? equipement.Notes,
            CreatedAt = created?.CreatedAt ?? equipement.CreatedAt,
            UpdatedAt = created?.UpdatedAt ?? equipement.UpdatedAt
        };

        return CreatedAtAction(nameof(GetById), new { id = resDto.Id }, resDto);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] EquipementInputDto dto)
    {
        var societeId = GetSocieteId();
        var existing = await _context.Equipements.Include(e => e.Famille)
            .FirstOrDefaultAsync(e => e.Id == id && (e.SocieteId == societeId || e.SocieteId == 0 || societeId == 1));

        if (existing == null)
        {
            existing = await _context.Equipements.Include(e => e.Famille).FirstOrDefaultAsync(e => e.Id == id);
            if (existing == null) return NotFound();
        }

        existing.Code = dto.Code ?? existing.Code;
        existing.Designation = dto.Designation ?? dto.Name ?? existing.Designation;
        
        var categoryName = !string.IsNullOrWhiteSpace(dto.Category) ? dto.Category : dto.FamilleNom;
        if (!string.IsNullOrWhiteSpace(categoryName) || dto.FamilleId.HasValue)
        {
            existing.FamilleId = await ResolveFamilleIdAsync(dto.FamilleId, categoryName);
            existing.Famille = null!; // Clear navigation property so EF Core updates the foreign key without in-memory entity conflict
        }

        if (dto.LocalisationId.HasValue) existing.LocalisationId = dto.LocalisationId.Value;
        existing.Marque = dto.Marque ?? dto.Brand ?? existing.Marque;
        existing.Modele = dto.Modele ?? dto.Model ?? existing.Modele;
        existing.NumeroSerie = dto.NumeroSerie ?? dto.SerialNumber ?? existing.NumeroSerie;
        existing.DateAchat = dto.DateAchat ?? existing.DateAchat;
        existing.DateMiseEnService = dto.DateMiseEnService ?? existing.DateMiseEnService;
        existing.DateFinGarantie = dto.DateFinGarantie ?? existing.DateFinGarantie;
        
        if (!string.IsNullOrWhiteSpace(dto.Criticite) || !string.IsNullOrWhiteSpace(dto.Criticality))
        {
            existing.Criticite = ParseCriticite(dto.Criticite ?? dto.Criticality);
        }
        if (!string.IsNullOrWhiteSpace(dto.Etat) || !string.IsNullOrWhiteSpace(dto.Status))
        {
            existing.Etat = ParseEtat(dto.Etat ?? dto.Status);
        }

        existing.FournisseurId = dto.FournisseurId ?? existing.FournisseurId;
        existing.PhotoUrl = dto.PhotoUrl ?? existing.PhotoUrl;
        existing.Notes = dto.Notes ?? existing.Notes;
        existing.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var societeId = GetSocieteId();
        var equipement = await _equipementRepository.GetByIdAsync(id);
        if (equipement == null || equipement.SocieteId != societeId) return NotFound();
        
        await _equipementRepository.DeleteAsync(equipement);
        return NoContent();
    }

    [HttpPost("{id}/pieces/{pieceIdentifier}")]
    public async Task<IActionResult> LinkPiece(string id, string pieceIdentifier)
    {
        var societeId = GetSocieteId();
        
        Equipement? equipement = null;
        if (int.TryParse(id, out int eqId))
        {
            equipement = await _equipementRepository.GetByIdWithDetailsAsync(eqId, societeId);
        }
        if (equipement == null)
        {
            equipement = await _context.Equipements
                .Include(e => e.Pieces)
                .FirstOrDefaultAsync(e => e.Code == id && e.SocieteId == societeId);
        }

        if (equipement == null) return NotFound("Equipement not found");

        Piece? piece = null;
        if (int.TryParse(pieceIdentifier, out int pId))
        {
            piece = await _pieceRepository.GetByIdAsync(pId);
        }
        if (piece == null)
        {
            piece = await _context.Pieces.FirstOrDefaultAsync(p => p.Reference == pieceIdentifier && p.SocieteId == societeId);
        }

        if (piece == null || piece.SocieteId != societeId) return NotFound("Piece not found");

        if (!equipement.Pieces.Any(p => p.Id == piece.Id))
        {
            equipement.Pieces.Add(piece);
            await _context.SaveChangesAsync();
        }

        return Ok();
    }

    [HttpDelete("{id}/pieces/{pieceIdentifier}")]
    public async Task<IActionResult> UnlinkPiece(string id, string pieceIdentifier)
    {
        var societeId = GetSocieteId();
        
        Equipement? equipement = null;
        if (int.TryParse(id, out int eqId))
        {
            equipement = await _equipementRepository.GetByIdWithDetailsAsync(eqId, societeId);
        }
        if (equipement == null)
        {
            equipement = await _context.Equipements
                .Include(e => e.Pieces)
                .FirstOrDefaultAsync(e => e.Code == id && e.SocieteId == societeId);
        }

        if (equipement == null) return NotFound("Equipement not found");

        var piece = equipement.Pieces.FirstOrDefault(p => 
            p.Id.ToString() == pieceIdentifier || 
            p.Reference.Equals(pieceIdentifier, StringComparison.OrdinalIgnoreCase));

        if (piece != null)
        {
            equipement.Pieces.Remove(piece);
            await _context.SaveChangesAsync();
        }

        return Ok();
    }
}
