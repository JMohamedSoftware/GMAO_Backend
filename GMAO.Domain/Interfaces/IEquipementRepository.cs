using System.Threading.Tasks;
using GMAO.Domain.Entities;

namespace GMAO.Domain.Interfaces;

public interface IEquipementRepository : IGenericRepository<Equipement>
{
    Task<Equipement?> GetByCodeAsync(string code);
}
