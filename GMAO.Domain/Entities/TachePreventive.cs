namespace GMAO.Domain.Entities;

public class TachePreventive
{
    public int Id { get; set; }
    public int PlanId { get; set; }
    public string Description { get; set; } = string.Empty;
    public int Ordre { get; set; }                   // Ordre d'exécution
    public int? DureeEstimeeMinutes { get; set; }
    public bool EstObligatoire { get; set; } = true;

    // Navigation
    public PlanPreventif Plan { get; set; } = null!;
}
