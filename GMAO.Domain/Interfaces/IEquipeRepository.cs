using System.Collections.Generic;
using System.Threading.Tasks;
using GMAO.Domain.Entities;

namespace GMAO.Domain.Interfaces
{
    public interface IEquipeRepository : IGenericRepository<Equipe>
    {
        Task<IEnumerable<Equipe>> GetEquipesBySocieteAsync(int societeId);
        Task<Equipe?> GetEquipeWithMembresAsync(int id);
    }
}