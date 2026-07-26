using System.Collections.Generic;
using System.Threading.Tasks;
using GMAO.Domain.Entities;

namespace GMAO.Application.Interfaces
{
    public interface ILocalisationService
    {
        Task<IEnumerable<Localisation>> GetAllAsync();
        Task<Localisation?> GetByIdAsync(int id);
        Task<Localisation> CreateAsync(Localisation localisation);
        Task<Localisation?> UpdateAsync(int id, Localisation localisation);
        Task<bool> DeleteAsync(int id);
        Task<IEnumerable<Localisation>> GetTreeAsync();
    }
}
