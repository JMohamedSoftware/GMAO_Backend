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
        var societeIdClaim = User.FindFirst("SocieteId")?.Value;
        if (int.TryParse(societeIdClaim, out int societeId))
            return societeId;
        return 0; // Or throw exception
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
            Criticite = e.Criticite.ToString(),
            Etat = e.Etat.ToString(),
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
            Criticite = e.Criticite.ToString(),
            Etat = e.Etat.ToString(),
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

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] Equipement equipement)
    {
        var societeId = GetSocieteId();
        equipement.SocieteId = societeId;
        
        await _equipementRepository.AddAsync(equipement);
        return CreatedAtAction(nameof(GetById), new { id = equipement.Id }, equipement);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] Equipement equipement)
    {
        if (id != equipement.Id) return BadRequest();
        
        var societeId = GetSocieteId();
        var existing = await _equipementRepository.GetByIdAsync(id);
        if (existing == null || existing.SocieteId != societeId) return NotFound();

        equipement.SocieteId = societeId;
        await _equipementRepository.UpdateAsync(equipement);
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
