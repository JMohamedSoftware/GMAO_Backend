using GMAO.Domain.Enums;

namespace GMAO.Domain.Entities;

public class MouvementStock
{
    public int Id { get; set; }
    public int PieceId { get; set; }
    public int? OTId { get; set; }
    public int UserId { get; set; }              // Qui a fait le mouvement
    public TypeMouvementStock Type { get; set; }
    public decimal Quantite { get; set; }
    public decimal PrixUnitaire { get; set; }
    public decimal PrixTotal { get; set; }
    public string? Reference { get; set; }       // Ex: N° bon de livraison
    public string? Motif { get; set; }
    public DateTime Date { get; set; } = DateTime.UtcNow;

    // Navigation
    public Piece Piece { get; set; } = null!;
    public OrdresTravail? OT { get; set; }
    public User User { get; set; } = null!;
}
