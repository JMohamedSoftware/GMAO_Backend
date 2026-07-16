using System.Threading.Tasks;
using GMAO.Application.Interfaces;
using GMAO.Domain.Entities;
using GMAO.Domain.Interfaces;

namespace GMAO.Application.Services;

public class MaintenanceService : IMaintenanceService
{
    private readonly IEquipementRepository _equipementRepository;
    private readonly IOrdreTravailRepository _ordreTravailRepository;

    public MaintenanceService(
        IEquipementRepository equipementRepository,
        IOrdreTravailRepository ordreTravailRepository)
    {
        _equipementRepository = equipementRepository;
        _ordreTravailRepository = ordreTravailRepository;
    }

    public async Task<DemandeIntervention> DeclareBreakdownAsync(int equipementId, int demandeurId, string description)
    {
        // Squelette à implémenter étape par étape
        throw new System.NotImplementedException();
    }

    public async Task<OrdresTravail> CreateWorkOrderAsync(int demandId, int responsableId, int? technicienId, string description)
    {
        // Squelette à implémenter étape par étape
        throw new System.NotImplementedException();
    }

    public async Task<Intervention> CompleteWorkOrderAsync(int otId, string diagnostic, string solution, decimal tempsPasse)
    {
        // Squelette à implémenter étape par étape
        throw new System.NotImplementedException();
    }
}
