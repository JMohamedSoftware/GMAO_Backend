using System.Threading.Tasks;
using GMAO.Domain.Entities;

namespace GMAO.Domain.Interfaces;

public interface IOrdreTravailRepository : IGenericRepository<OrdresTravail>
{
    Task<OrdresTravail?> GetByNumeroAsync(string numeroOT);
}
