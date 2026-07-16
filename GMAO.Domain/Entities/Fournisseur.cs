namespace GMAO.Domain.Entities;

public class Fournisseur
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Nom { get; set; } = string.Empty;
    public string? Adresse { get; set; }
    public string? Telephone { get; set; }
    public string? Email { get; set; }
    public string? Contact { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public ICollection<Equipement> Equipements { get; set; } = new List<Equipement>();
    public ICollection<Piece> Pieces { get; set; } = new List<Piece>();
}
