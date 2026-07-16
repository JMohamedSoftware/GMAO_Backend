namespace GMAO.Domain.Entities;

public class FamillePiece
{
    public int Id { get; set; }
    public string Nom { get; set; } = string.Empty;
    public string? Description { get; set; }

    // Navigation
    public ICollection<Piece> Pieces { get; set; } = new List<Piece>();
}
