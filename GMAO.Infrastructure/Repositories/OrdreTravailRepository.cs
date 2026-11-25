using System.Security.Claims;
using GMAO.Application.Security;
using GMAO.Domain.Constants;
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
        if (string.IsNullOrWhiteSpace(numeroOT))
            return null;

        var normalized = numeroOT.Trim();
        return await _context.Set<OrdresTravail>()
            .AsNoTracking()
            .Include(o => o.Equipement)
            .Include(o => o.Technicien)
            .Include(o => o.Intervention)
            .FirstOrDefaultAsync(o => o.NumeroOT == normalized);
    }

    public async Task<IEnumerable<OrdresTravail>> GetAllForUserAsync(ClaimsPrincipal user)
    {
        var query = _context.Set<OrdresTravail>()
            .AsNoTracking()
            .Include(o => o.Campagne)
            .Include(o => o.Technicien)
            .Include(o => o.Intervention)
            .AsQueryable();

        if (GmaoRoles.IsCompanyAdmin(user))
            return await query.ToListAsync();

        var scope = GmaoPermissions.GetScope(user, GmaoPermissions.WorkOrderView);
        if (scope == AccessScope.All)
            return await query.ToListAsync();

        var userIdString = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!int.TryParse(userIdString, out var userId))
            return new List<OrdresTravail>();

        if (scope == AccessScope.Team)
        {
            var currentUserEntity = await _context.Set<User>().AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);
            int? userEquipeId = currentUserEntity?.EquipeId;

            if (userEquipeId.HasValue)
            {
                query = query.Where(ot =>
                    ot.TechnicienId == userId ||
                    ot.ResponsableId == userId ||
                    (ot.Technicien != null && ot.Technicien.EquipeId == userEquipeId.Value) ||
                    (ot.Responsable != null && ot.Responsable.EquipeId == userEquipeId.Value));
            }
            else
            {
                query = query.Where(ot => ot.TechnicienId == userId || ot.ResponsableId == userId);
            }

            return await query.ToListAsync();
        }

        if (scope == AccessScope.Own)
        {
            query = query.Where(ot => ot.TechnicienId == userId || ot.ResponsableId == userId);
            return await query.ToListAsync();
        }

        return new List<OrdresTravail>();
    }
}
