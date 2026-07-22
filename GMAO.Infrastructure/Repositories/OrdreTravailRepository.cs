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

    public async Task<System.Collections.Generic.IEnumerable<OrdresTravail>> GetAllForUserAsync(System.Security.Claims.ClaimsPrincipal user)
    {
        var query = _context.Set<OrdresTravail>().AsQueryable();

        // Enforce Row-Level Security
        if (!user.HasClaim(System.Security.Claims.ClaimTypes.Role, "SuperAdmin") && 
            !user.HasClaim(System.Security.Claims.ClaimTypes.Role, "Admin"))
        {
            var userIdString = user.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (int.TryParse(userIdString, out var userId))
            {
                // Technicians can only see their own work orders
                query = query.Where(ot => ot.TechnicienId == userId);
            }
            else
            {
                // Unauthenticated or invalid user ID gets nothing
                return new System.Collections.Generic.List<OrdresTravail>();
            }
        }

        return await query.ToListAsync();
    }
}
