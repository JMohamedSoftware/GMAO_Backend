using GMAO.Application.Interfaces;
using GMAO.Domain.Entities;
using GMAO.Domain.Enums;
using GMAO.Domain.Interfaces;

namespace GMAO.Application.Services;

public class MaintenanceService : IMaintenanceService
{
    private readonly IEquipementRepository _equipementRepository;
    private readonly IOrdreTravailRepository _ordreTravailRepository;
    private readonly IGenericRepository<DemandeIntervention>? _demandeRepository;
    private readonly IGenericRepository<Intervention>? _interventionRepository;

    public MaintenanceService(
        IEquipementRepository equipementRepository,
        IOrdreTravailRepository ordreTravailRepository,
        IGenericRepository<DemandeIntervention>? demandeRepository = null,
        IGenericRepository<Intervention>? interventionRepository = null)
    {
        _equipementRepository = equipementRepository;
        _ordreTravailRepository = ordreTravailRepository;
        _demandeRepository = demandeRepository;
        _interventionRepository = interventionRepository;
    }

    public async Task<DemandeIntervention> DeclareBreakdownAsync(int equipementId, int demandeurId, string description)
    {
        if (string.IsNullOrWhiteSpace(description))
            throw new ArgumentException("La description de la panne est obligatoire.", nameof(description));

        var equipement = await _equipementRepository.GetByIdAsync(equipementId)
            ?? throw new KeyNotFoundException($"Équipement #{equipementId} introuvable dans votre société.");

        equipement.Etat = EtatEquipement.EnPanne;
        equipement.UpdatedAt = DateTime.UtcNow;
        await _equipementRepository.UpdateAsync(equipement);

        var demande = new DemandeIntervention
        {
            SocieteId = equipement.SocieteId,
            EquipementId = equipement.Id,
            DemandeurId = demandeurId,
            Description = description.Trim(),
            DatePanne = DateTime.UtcNow,
            Priorite = PrioriteIntervention.Haute,
            Statut = StatutDemande.EnAttente,
            CreatedAt = DateTime.UtcNow
        };

        if (_demandeRepository != null)
            await _demandeRepository.AddAsync(demande);

        return demande;
    }

    public async Task<OrdresTravail> CreateWorkOrderAsync(int demandId, int responsableId, int? technicienId, string description)
    {
        if (_demandeRepository == null)
            throw new InvalidOperationException("Dépôt des demandes d'intervention non configuré.");

        var demande = await _demandeRepository.GetByIdAsync(demandId)
            ?? throw new KeyNotFoundException($"Demande d'intervention #{demandId} introuvable dans votre société.");

        var ot = new OrdresTravail
        {
            SocieteId = demande.SocieteId,
            NumeroOT = $"OT-{DateTime.UtcNow:yyyyMMdd}-{demande.SocieteId}-{Guid.NewGuid().ToString("N")[..4].ToUpperInvariant()}",
            DemandeId = demande.Id,
            EquipementId = demande.EquipementId,
            ResponsableId = responsableId,
            TechnicienId = technicienId,
            Priorite = demande.Priorite,
            TypeMaintenance = TypeMaintenance.Corrective,
            Statut = technicienId.HasValue ? StatutOT.Planifie : StatutOT.Cree,
            DateCreation = DateTime.UtcNow,
            Description = string.IsNullOrWhiteSpace(description) ? demande.Description : description.Trim()
        };

        await _ordreTravailRepository.AddAsync(ot);

        demande.Statut = StatutDemande.TransformeeEnOT;
        await _demandeRepository.UpdateAsync(demande);

        return ot;
    }

    public async Task<Intervention> CompleteWorkOrderAsync(int otId, string diagnostic, string solution, decimal tempsPasse)
    {
        if (tempsPasse < 0)
            throw new ArgumentOutOfRangeException(nameof(tempsPasse), "Le temps passé ne peut pas être négatif.");

        var ot = await _ordreTravailRepository.GetByIdAsync(otId)
            ?? throw new KeyNotFoundException($"Ordre de travail #{otId} introuvable dans votre société.");

        if (ot.Statut == StatutOT.Cloture || ot.Statut == StatutOT.Annule)
            throw new InvalidOperationException($"L'ordre de travail #{otId} est déjà dans un état terminal ({ot.Statut}).");

        var now = DateTime.UtcNow;
        var intervention = new Intervention
        {
            OTId = ot.Id,
            Diagnostic = diagnostic,
            Solution = solution,
            TempsPasse = tempsPasse,
            DateDebut = ot.DateDebutReelle ?? now,
            DateFin = now,
            Statut = StatutIntervention.Terminee,
            DateIntervention = now
        };

        if (_interventionRepository != null)
            await _interventionRepository.AddAsync(intervention);

        ot.Statut = StatutOT.Termine;
        ot.DateFinReelle = now;
        await _ordreTravailRepository.UpdateAsync(ot);

        var equipement = await _equipementRepository.GetByIdAsync(ot.EquipementId);
        if (equipement != null)
        {
            equipement.Etat = EtatEquipement.EnService;
            equipement.UpdatedAt = now;
            await _equipementRepository.UpdateAsync(equipement);
        }

        return intervention;
    }
}
