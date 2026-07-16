using GMAO.Domain.Enums;

namespace GMAO.Domain.Entities;

public class Document
{
    public int Id { get; set; }
    public string Nom { get; set; } = string.Empty;
    public string Chemin { get; set; } = string.Empty;    // Chemin du fichier sur le serveur
    public TypeDocument Type { get; set; } = TypeDocument.Autre;
    public long TailleFichierOctets { get; set; }
    public string? MimeType { get; set; }
    public int? EquipementId { get; set; }
    public int? OTId { get; set; }
    public int? PlanPreventifId { get; set; }
    public int UploadedById { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Equipement? Equipement { get; set; }
    public OrdresTravail? OT { get; set; }
    public PlanPreventif? PlanPreventif { get; set; }
    public User UploadedBy { get; set; } = null!;
}
