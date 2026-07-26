namespace GMAO.Domain.Entities;

public class Equipe
{
    public int Id { get; set; }
    public string Nom { get; set; } = string.Empty;
    public string? Description { get; set; }
    
    public int SocieteId { get; set; }
    public Societe? Societe { get; set; }
    
    public int? ChefEquipeId { get; set; }
    public User? ChefEquipe { get; set; }

    public ICollection<User> Membres { get; set; } = new List<User>();
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}