using GMAO.Domain.Enums;

namespace GMAO.Domain.Entities;

public class Document
{
    public int Id { get; set; }
    public string Nom { get; set; } = string.Empty;
    public string Chemin { get; set; } = string.Empty;    // Chemin relatif local ou clé objet bucket Supabase (tenants/{SocieteId}/documents/{Guid}.{ext})
    public TypeDocument Type { get; set; } = TypeDocument.Autre;
    public long TailleFichierOctets { get; set; }
    public string? MimeType { get; set; }
    public int? EquipementId { get; set; }
    public int? OTId { get; set; }
    public int? PlanPreventifId { get; set; }
    public int? IncidentId { get; set; }
    public int? SocieteId { get; set; }
    public string StorageProvider { get; set; } = "Local"; // "Local" (historique) ou "Supabase"
    public string? BucketName { get; set; }                // ex: "gmao-documents"
    public string? Sha256Checksum { get; set; }            // Empreinte SHA-256 hexadécimale (64 chars)
    public int UploadedById { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Societe? Societe { get; set; }
    public Equipement? Equipement { get; set; }
    public OrdresTravail? OT { get; set; }
    public PlanPreventif? PlanPreventif { get; set; }
    public DemandeIntervention? Incident { get; set; }
    public User? UploadedBy { get; set; }
}
