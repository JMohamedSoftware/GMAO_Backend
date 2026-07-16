using System.Threading.Tasks;
using GMAO.Domain.Entities;
using GMAO.Domain.Interfaces;
using GMAO.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace GMAO.Infrastructure.Repositories;

public class OrdreTravailRepository : GenericRepository<OrdresTravail>, IOrdreTravailRepository
{
    public OrdreTravailRepository(GmaoDbContext context) : base(context)
    {
    }

    public async Task<OrdresTravail?> GetByNumeroAsync(string numeroOT)
    {
        // Squelette à implémenter étape par étape
        throw new System.NotImplementedException();
    }
}
