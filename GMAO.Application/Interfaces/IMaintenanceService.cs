using System.Threading.Tasks;
using GMAO.Domain.Entities;

namespace GMAO.Application.Interfaces;

public interface IMaintenanceService
{
    Task<DemandeIntervention> DeclareBreakdownAsync(int equipementId, int demandeurId, string description);
    Task<OrdresTravail> CreateWorkOrderAsync(int demandId, int responsableId, int? technicienId, string description);
    Task<Intervention> CompleteWorkOrderAsync(int otId, string diagnostic, string solution, decimal tempsPasse);
}
