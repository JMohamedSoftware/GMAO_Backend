using GMAO.Domain.Enums;

namespace GMAO.Domain.Entities;

public class Intervention
{
    public int Id { get; set; }
    public int OTId { get; set; }
    public string? Diagnostic { get; set; }
    public string? CausePanne { get; set; }
    public string? Solution { get; set; }
    public decimal? TempsPasse { get; set; }  // En heures
    public string? Observation { get; set; }
    public DateTime? DateDebut { get; set; }
    public DateTime? DateFin { get; set; }
    public StatutIntervention Statut { get; set; } = StatutIntervention.NonDemarree;
    public DateTime DateIntervention { get; set; } = DateTime.UtcNow;

    // Navigation
    public OrdresTravail OT { get; set; } = null!;
}
