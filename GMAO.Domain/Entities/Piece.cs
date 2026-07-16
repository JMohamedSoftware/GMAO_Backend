namespace GMAO.Domain.Entities;

public class Piece
{
    public int Id { get; set; }
    public string Reference { get; set; } = string.Empty;
    public string Designation { get; set; } = string.Empty;
    public int? FamillePieceId { get; set; }
    public decimal StockActuel { get; set; } = 0;
    public decimal StockMinimum { get; set; } = 0;
    public decimal StockMaximum { get; set; } = 0;
    public string? Unite { get; set; }         // Pièce, kg, litre, m...
    public decimal PrixUnitaire { get; set; } = 0;
    public int? FournisseurId { get; set; }
    public string? Emplacement { get; set; }  // Emplacement dans le magasin
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public FamillePiece? FamillePiece { get; set; }
    public Fournisseur? Fournisseur { get; set; }
    public ICollection<MouvementStock> MouvementsStock { get; set; } = new List<MouvementStock>();
}
