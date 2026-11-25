using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Claims;
using System.Threading.Tasks;
using GMAO.Application.DTOs.Documents;
using GMAO.Application.Interfaces;
using GMAO.Domain.Entities;
using GMAO.Domain.Enums;
using GMAO.Infrastructure.Data;
using GMAO.Infrastructure.Storage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace GMAO.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DocumentController : ControllerBase
{
    private readonly GmaoDbContext _context;
    private readonly IFileStorageService _fileStorageService;
    private readonly ITenantContextService _tenantContext;
    private readonly IConfiguration? _configuration;
    private readonly ILogger<DocumentController>? _logger;

    public DocumentController(
        GmaoDbContext context,
        IFileStorageService fileStorageService,
        ITenantContextService tenantContext,
        IConfiguration? configuration = null,
        ILogger<DocumentController>? logger = null)
    {
        _context = context;
        _fileStorageService = fileStorageService;
        _tenantContext = tenantContext;
        _configuration = configuration;
        _logger = logger;
    }

    private int GetCurrentUserId()
    {
        var idClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("id")?.Value
            ?? User.FindFirst("sub")?.Value;
        if (int.TryParse(idClaim, out int id) && id > 0)
            return id;

        throw new UnauthorizedAccessException("Utilisateur non identifié dans le jeton d'authentification.");
    }

    private static string FormatFileSize(long bytes)
    {
        if (bytes >= 1024 * 1024)
            return $"{bytes / (1024.0 * 1024.0):0.#} MB";
        if (bytes >= 1024)
            return $"{bytes / 1024.0:0.#} KB";
        return $"{bytes} B";
    }

    private IFileStorageService ResolveStorageForDocument(Document doc)
    {
        if (_fileStorageService is HybridFileStorageService hybrid)
        {
            return hybrid.ResolveByProvider(doc.StorageProvider);
        }
        return _fileStorageService;
    }

    private async Task<bool> CanAccessDocumentAsync(Document doc, int societeId)
    {
        if (_tenantContext.IsSuperAdmin && !_tenantContext.IsImpersonating) return true;
        if (_tenantContext.IsSuperAdmin && _tenantContext.IsImpersonating)
        {
            // En impersonation SuperAdmin, vérifier la correspondance avec la société impersonnée
        }

        if (doc.SocieteId.HasValue && doc.SocieteId.Value > 0)
        {
            return doc.SocieteId.Value == societeId;
        }

        if (doc.EquipementId.HasValue)
        {
            var eq = await _context.Equipements.IgnoreQueryFilters().AsNoTracking()
                .FirstOrDefaultAsync(e => e.Id == doc.EquipementId.Value);
            if (eq != null) return eq.SocieteId == societeId;
        }

        if (doc.OTId.HasValue)
        {
            var ot = await _context.OrdresTravail.IgnoreQueryFilters().AsNoTracking()
                .FirstOrDefaultAsync(o => o.Id == doc.OTId.Value);
            if (ot != null) return ot.SocieteId == societeId;
        }

        if (doc.PlanPreventifId.HasValue)
        {
            var plan = await _context.PlansPreventifs.IgnoreQueryFilters().AsNoTracking()
                .Include(p => p.Equipement)
                .FirstOrDefaultAsync(p => p.Id == doc.PlanPreventifId.Value);
            if (plan?.Equipement != null) return plan.Equipement.SocieteId == societeId;
        }

        if (doc.IncidentId.HasValue)
        {
            var inc = await _context.DemandesIntervention.IgnoreQueryFilters().AsNoTracking()
                .FirstOrDefaultAsync(i => i.Id == doc.IncidentId.Value);
            if (inc != null) return inc.SocieteId == societeId;
        }

        if (doc.UploadedById > 0)
        {
            var uploader = await _context.Users.IgnoreQueryFilters().AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == doc.UploadedById);
            if (uploader?.SocieteId != null) return uploader.SocieteId.Value == societeId;
        }

        return false;
    }

    private bool HasUploadPermission()
    {
        return User.IsInRole(GMAO.Domain.Constants.GmaoRoles.SuperAdmin)
            || User.IsInRole(GMAO.Domain.Constants.GmaoRoles.Administrateur)
            || User.IsInRole(GMAO.Domain.Constants.GmaoRoles.ResponsableMaintenance)
            || User.IsInRole(GMAO.Domain.Constants.GmaoRoles.ChefEquipe)
            || User.IsInRole(GMAO.Domain.Constants.GmaoRoles.Technicien)
            || GMAO.Application.Security.GmaoPermissions.HasPermission(User, GMAO.Application.Security.GmaoPermissions.EquipmentUpdate)
            || GMAO.Application.Security.GmaoPermissions.HasPermission(User, GMAO.Application.Security.GmaoPermissions.WorkOrderUpdate)
            || GMAO.Application.Security.GmaoPermissions.HasPermission(User, GMAO.Application.Security.GmaoPermissions.IncidentCreate)
            || GMAO.Application.Security.GmaoPermissions.HasPermission(User, GMAO.Application.Security.GmaoPermissions.PreventiveUpdate);
    }

    [HttpGet("equipement/{equipementId}")]
    public async Task<IActionResult> GetByEquipement(int equipementId)
    {
        int societeId = _tenantContext.GetRequiredTenantId();

        var equipement = await _context.Equipements.AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == equipementId && (User.IsInRole("SuperAdmin") || e.SocieteId == societeId));
        if (equipement == null) return NotFound("Équipement introuvable.");

        var docs = await _context.Documents
            .Where(d => d.EquipementId == equipementId)
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync();

        var dtos = docs.Select(MapToDto);
        return Ok(dtos);
    }

    [HttpPost("upload")]
    public async Task<IActionResult> Upload(
        [FromForm] IFormFile file,
        [FromForm] string? type,
        [FromForm] string? nom,
        [FromForm] int? equipementId,
        [FromForm] int? otId,
        [FromForm] int? planPreventifId,
        [FromForm] int? incidentId = null)
    {
        if (!HasUploadPermission())
            return Forbid();

        if (file == null || file.Length == 0)
            return BadRequest("Aucun fichier fourni.");

        int maxMb = int.TryParse(_configuration?["FileStorage:MaxFileSizeMB"], out int cfgMb) && cfgMb > 0 ? cfgMb : 20;
        long maxBytes = (long)maxMb * 1024L * 1024L;

        int societeId = _tenantContext.GetRequiredTenantId();
        int userId = GetCurrentUserId();

        // Validation d'appartenance des entités liées au tenant
        if (equipementId.HasValue && !User.IsInRole("SuperAdmin"))
        {
            var eqExists = await _context.Equipements.AnyAsync(e => e.Id == equipementId.Value && e.SocieteId == societeId);
            if (!eqExists) return BadRequest("L'équipement lié n'appartient pas à votre société.");
        }

        if (otId.HasValue && !User.IsInRole("SuperAdmin"))
        {
            var otExists = await _context.OrdresTravail.AnyAsync(o => o.Id == otId.Value && o.SocieteId == societeId);
            if (!otExists) return BadRequest("L'ordre de travail lié n'appartient pas à votre société.");
        }

        if (incidentId.HasValue && !User.IsInRole("SuperAdmin"))
        {
            var incExists = await _context.DemandesIntervention.AnyAsync(i => i.Id == incidentId.Value && i.SocieteId == societeId);
            if (!incExists) return BadRequest("L'incident lié n'appartient pas à votre société.");
        }

        // Validation stricte extension + MIME + Magic Bytes + calcul SHA-256
        MemoryStream validatedStream;
        string sha256Hex;
        string verifiedMime;
        try
        {
            await using var rawStream = file.OpenReadStream();
            (validatedStream, sha256Hex, verifiedMime) = await FileValidationHelper.ValidateSignatureAndHashAsync(
                rawStream, file.FileName, file.ContentType, maxBytes);
        }
        catch (InvalidOperationException valEx)
        {
            return BadRequest(valEx.Message);
        }

        var docType = ParseDocumentType(type);
        StoredFileResult stored;
        await using (validatedStream)
        {
            try
            {
                stored = await _fileStorageService.UploadAsync(validatedStream, file.FileName, verifiedMime, societeId);
            }
            catch (HttpRequestException httpEx)
            {
                _logger?.LogError(httpEx, "Stockage documentaire temporairement indisponible lors de l'upload.");
                return StatusCode(StatusCodes.Status503ServiceUnavailable, "Le service de stockage documentaire est temporairement indisponible.");
            }
        }

        var document = new Document
        {
            Nom = string.IsNullOrWhiteSpace(nom) ? Path.GetFileName(file.FileName) : nom.Trim(),
            Chemin = stored.StoragePath,
            Type = docType,
            TailleFichierOctets = stored.FileSizeBytes > 0 ? stored.FileSizeBytes : file.Length,
            MimeType = verifiedMime,
            SocieteId = societeId,
            StorageProvider = stored.Provider,
            BucketName = stored.BucketName,
            Sha256Checksum = string.IsNullOrEmpty(stored.Sha256Checksum) ? sha256Hex : stored.Sha256Checksum,
            EquipementId = equipementId,
            OTId = otId,
            PlanPreventifId = planPreventifId,
            IncidentId = incidentId,
            UploadedById = userId,
            CreatedAt = DateTime.UtcNow
        };

        try
        {
            _context.Documents.Add(document);
            await _context.SaveChangesAsync();
        }
        catch (Exception dbEx)
        {
            // Compensation obligatoire : supprimer le blob uploadé si la persistance PostgreSQL échoue
            _logger?.LogError(dbEx, "Échec de sauvegarde PostgreSQL après upload ; suppression compensatoire du fichier {Path}", stored.StoragePath);
            try
            {
                await _fileStorageService.DeleteAsync(stored.StoragePath);
            }
            catch { }
            return StatusCode(StatusCodes.Status500InternalServerError, "Erreur lors de l'enregistrement des métadonnées documentaires.");
        }

        return CreatedAtAction(nameof(GetFile), new { id = document.Id }, MapToDto(document));
    }

    [HttpGet("{id}/signed-url")]
    public async Task<IActionResult> GetSignedUrl(int id, [FromQuery] int expiresIn = 120)
    {
        int societeId = _tenantContext.GetRequiredTenantId();

        var document = await _context.Documents.IgnoreQueryFilters().FirstOrDefaultAsync(d => d.Id == id);
        if (document == null) return NotFound("Document non trouvé.");

        if (!await CanAccessDocumentAsync(document, societeId))
            return Forbid();

        var storage = ResolveStorageForDocument(document);
        try
        {
            var signed = await storage.CreateSignedUrlAsync(document.Chemin, expiresIn);
            if (signed != null)
            {
                return Ok(signed);
            }

            return Ok(new SignedUrlResult
            {
                Url = $"/api/Document/{document.Id}/file",
                ExpiresInSeconds = 0,
                IsExternalSignedUrl = false,
                Provider = document.StorageProvider
            });
        }
        catch (HttpRequestException httpEx)
        {
            _logger?.LogError(httpEx, "Impossible de générer la Signed URL pour Document #{Id}", id);
            return StatusCode(StatusCodes.Status503ServiceUnavailable, "Service de stockage documentaire indisponible.");
        }
    }

    [HttpGet("{id}/file")]
    public async Task<IActionResult> GetFile(int id)
    {
        int societeId = _tenantContext.GetRequiredTenantId();

        var document = await _context.Documents.IgnoreQueryFilters().FirstOrDefaultAsync(d => d.Id == id);
        if (document == null) return NotFound("Document non trouvé.");

        // Contrôle d'isolation stricte multi-tenant
        if (!await CanAccessDocumentAsync(document, societeId))
            return Forbid();

        var storage = ResolveStorageForDocument(document);
        (Stream stream, string contentType, string fileName)? fileResult;
        try
        {
            fileResult = await storage.DownloadAsync(document.Chemin);
        }
        catch (HttpRequestException)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, "Service de stockage documentaire temporairement indisponible.");
        }

        if (!fileResult.HasValue)
            return NotFound("Fichier physique introuvable sur le serveur de stockage.");

        var (stream, contentType, _) = fileResult.Value;
        Response.Headers.Append("Content-Disposition", $"inline; filename=\"{Uri.EscapeDataString(document.Nom)}\"");
        return File(stream, document.MimeType ?? contentType, enableRangeProcessing: true);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        int societeId = _tenantContext.GetRequiredTenantId();

        var document = await _context.Documents.IgnoreQueryFilters().FirstOrDefaultAsync(d => d.Id == id);
        if (document == null) return NotFound("Document non trouvé.");

        // Contrôle d'isolation stricte
        if (!await CanAccessDocumentAsync(document, societeId))
            return Forbid();

        var storage = ResolveStorageForDocument(document);
        await storage.DeleteAsync(document.Chemin);

        _context.Documents.Remove(document);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    private static TypeDocument ParseDocumentType(string? type)
    {
        var docType = TypeDocument.Autre;
        if (string.IsNullOrWhiteSpace(type)) return docType;

        if (Enum.TryParse<TypeDocument>(type, true, out var parsedType))
            return parsedType;

        var t = type.Trim().ToLowerInvariant();
        if (t.Contains("notice") || t.Contains("manuel")) docType = TypeDocument.Manuel;
        else if (t.Contains("schema") || t.Contains("électrique") || t.Contains("electrical") || t.Contains("plan")) docType = TypeDocument.Schema;
        else if (t.Contains("photo") || t.Contains("image")) docType = TypeDocument.Photo;
        else if (t.Contains("rapport")) docType = TypeDocument.Rapport;
        else if (t.Contains("contrat")) docType = TypeDocument.Contrat;
        else if (t.Contains("certif")) docType = TypeDocument.Certificat;
        return docType;
    }

    private static DocumentDto MapToDto(Document d) => new()
    {
        Id = d.Id,
        Name = d.Nom,
        Type = d.Type.ToString().ToLower(),
        Size = FormatFileSize(d.TailleFichierOctets),
        Url = $"/api/Document/{d.Id}/file",
        MimeType = d.MimeType,
        EquipementId = d.EquipementId,
        OTId = d.OTId,
        IncidentId = d.IncidentId,
        StorageProvider = d.StorageProvider,
        Sha256Checksum = d.Sha256Checksum,
        CreatedAt = d.CreatedAt
    };
}
