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

        // Enforce Row-Level Security using Scoped Permissions
        if (user.HasClaim(System.Security.Claims.ClaimTypes.Role, "SuperAdmin") || 
            user.HasClaim(System.Security.Claims.ClaimTypes.Role, "Administrateur") ||
            user.HasClaim(System.Security.Claims.ClaimTypes.Role, "CompanyAdmin"))
        {
            return await query.ToListAsync();
        }

        var userIdString = user.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!int.TryParse(userIdString, out var userId))
        {
            // Unauthenticated or invalid user ID gets nothing
            return new System.Collections.Generic.List<OrdresTravail>();
        }

        // Check for specific scopes
        if (user.HasClaim("Permission", "WORKORDER_VIEW_ALL") || user.HasClaim("Permission", "WORKORDER_VIEW"))
        {
            // Can see all
            return await query.ToListAsync();
        }
        else if (user.HasClaim("Permission", "WORKORDER_VIEW_TEAM") || user.HasClaim("Permission", "WORKORDER_VIEW_OWN"))
        {
            // Can see own (Team behaves like own until teams are fully implemented)
            query = query.Where(ot => ot.TechnicienId == userId || ot.ResponsableId == userId);
            return await query.ToListAsync();
        }

        // No permissions
        return new System.Collections.Generic.List<OrdresTravail>();
    }
}
