using System;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using GMAO.Application.DTOs.Documents;
using GMAO.Domain.Entities;
using GMAO.Domain.Enums;
using GMAO.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GMAO.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DocumentController : ControllerBase
{
    private readonly GmaoDbContext _context;
    private readonly IWebHostEnvironment _env;

    public DocumentController(GmaoDbContext context, IWebHostEnvironment env)
    {
        _context = context;
        _env = env;
    }

    private int GetCurrentUserId()
    {
        var idClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (int.TryParse(idClaim, out int id) && id > 0)
            return id;
        
        // Fallback: first user in DB if claim is missing
        return _context.Users.Select(u => u.Id).FirstOrDefault();
    }

    private static string FormatFileSize(long bytes)
    {
        if (bytes >= 1024 * 1024)
            return $"{bytes / (1024.0 * 1024.0):0.#} MB";
        if (bytes >= 1024)
            return $"{bytes / 1024.0:0.#} KB";
        return $"{bytes} B";
    }

    [HttpGet("equipement/{equipementId}")]
    [Authorize]
    public async Task<IActionResult> GetByEquipement(int equipementId)
    {
        var docs = await _context.Documents
            .Where(d => d.EquipementId == equipementId)
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync();

        var dtos = docs.Select(d => new DocumentDto
        {
            Id = d.Id,
            Name = d.Nom,
            Type = d.Type.ToString().ToLower(),
            Size = FormatFileSize(d.TailleFichierOctets),
            Url = $"/api/Document/{d.Id}/file",
            MimeType = d.MimeType,
            EquipementId = d.EquipementId,
            CreatedAt = d.CreatedAt
        });

        return Ok(dtos);
    }

    [HttpPost("upload")]
    [Authorize]
    public async Task<IActionResult> Upload(
        [FromForm] IFormFile file,
        [FromForm] string? type,
        [FromForm] string? nom,
        [FromForm] int? equipementId,
        [FromForm] int? otId,
        [FromForm] int? planPreventifId)
    {
        if (file == null || file.Length == 0)
            return BadRequest("Aucun fichier fourni.");

        // Maximum size: 30 MB
        if (file.Length > 30 * 1024 * 1024)
            return BadRequest("Le fichier dépasse la taille maximale autorisée (30 Mo).");

        // Parse TypeDocument
        var docType = TypeDocument.Autre;
        if (!string.IsNullOrWhiteSpace(type))
        {
            if (Enum.TryParse<TypeDocument>(type, true, out var parsedType))
            {
                docType = parsedType;
            }
            else
            {
                var t = type.Trim().ToLower();
                if (t.Contains("notice") || t.Contains("manuel")) docType = TypeDocument.Manuel;
                else if (t.Contains("schema") || t.Contains("électrique") || t.Contains("electrical") || t.Contains("plan")) docType = TypeDocument.Schema;
                else if (t.Contains("photo") || t.Contains("image")) docType = TypeDocument.Photo;
                else if (t.Contains("rapport")) docType = TypeDocument.Rapport;
                else if (t.Contains("contrat")) docType = TypeDocument.Contrat;
                else if (t.Contains("certif")) docType = TypeDocument.Certificat;
            }
        }

        // Determine upload path
        var webRoot = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
        var uploadsDir = Path.Combine(webRoot, "uploads", "documents");
        if (!Directory.Exists(uploadsDir))
        {
            Directory.CreateDirectory(uploadsDir);
        }

        var fileExtension = Path.GetExtension(file.FileName);
        var uniqueFileName = $"{Guid.NewGuid():N}{fileExtension}";
        var physicalPath = Path.Combine(uploadsDir, uniqueFileName);

        using (var stream = new FileStream(physicalPath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        var relativePath = Path.Combine("uploads", "documents", uniqueFileName).Replace("\\", "/");
        var userId = GetCurrentUserId();

        var document = new Document
        {
            Nom = string.IsNullOrWhiteSpace(nom) ? file.FileName : nom.Trim(),
            Chemin = relativePath,
            Type = docType,
            TailleFichierOctets = file.Length,
            MimeType = string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType,
            EquipementId = equipementId,
            OTId = otId,
            PlanPreventifId = planPreventifId,
            UploadedById = userId,
            CreatedAt = DateTime.UtcNow
        };

        _context.Documents.Add(document);
        await _context.SaveChangesAsync();

        var dto = new DocumentDto
        {
            Id = document.Id,
            Name = document.Nom,
            Type = document.Type.ToString().ToLower(),
            Size = FormatFileSize(document.TailleFichierOctets),
            Url = $"/api/Document/{document.Id}/file",
            MimeType = document.MimeType,
            EquipementId = document.EquipementId,
            CreatedAt = document.CreatedAt
        };

        return CreatedAtAction(nameof(GetFile), new { id = document.Id }, dto);
    }

    [HttpGet("{id}/file")]
    [AllowAnonymous]
    public async Task<IActionResult> GetFile(int id)
    {
        var document = await _context.Documents.FindAsync(id);
        if (document == null) return NotFound("Document non trouvé.");

        var webRoot = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
        var fullPath = Path.Combine(webRoot, document.Chemin.TrimStart('/'));

        if (!System.IO.File.Exists(fullPath))
            return NotFound("Fichier physique introuvable sur le serveur.");

        var contentType = document.MimeType ?? "application/octet-stream";
        Response.Headers.Append("Content-Disposition", $"inline; filename=\"{Uri.EscapeDataString(document.Nom)}\"");
        return PhysicalFile(fullPath, contentType, enableRangeProcessing: true);
    }

    [HttpDelete("{id}")]
    [Authorize]
    public async Task<IActionResult> Delete(int id)
    {
        var document = await _context.Documents.FindAsync(id);
        if (document == null) return NotFound("Document non trouvé.");

        try
        {
            var webRoot = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            var fullPath = Path.Combine(webRoot, document.Chemin.TrimStart('/'));
            if (System.IO.File.Exists(fullPath))
            {
                System.IO.File.Delete(fullPath);
            }
        }
        catch
        {
            // Ignore file deletion error, proceed to DB removal
        }

        _context.Documents.Remove(document);
        await _context.SaveChangesAsync();

        return NoContent();
    }
}
