namespace GMAO.Domain.Entities;

public class Facture
{
    public int Id { get; set; }
    public string NumeroFacture { get; set; } = string.Empty;
    public int SocieteId { get; set; }
    public Societe? Societe { get; set; }
    public string SocieteNom { get; set; } = string.Empty;
    
    public decimal MontantHT { get; set; }
    public decimal TauxTVA { get; set; } = 19.0m;
    public decimal MontantTTC { get; set; }
    
    public string Statut { get; set; } = "En attente"; // "Payée", "En attente", "En retard", "Annulée"
    public string MethodePaiement { get; set; } = "Virement bancaire";
    
    public DateTime DateEmission { get; set; } = DateTime.UtcNow;
    public DateTime DateEcheance { get; set; } = DateTime.UtcNow.AddDays(30);
    public DateTime? DatePaiement { get; set; }
    
    public string Periode { get; set; } = string.Empty; // ex: "Mars 2026"
    public string Plan { get; set; } = "Business Pro";
    public string? Notes { get; set; }
}
