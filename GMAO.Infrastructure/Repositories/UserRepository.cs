using System.Threading.Tasks;
using GMAO.Domain.Entities;
using GMAO.Domain.Interfaces;
using GMAO.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace GMAO.Infrastructure.Repositories;

public class UserRepository : GenericRepository<User>, IUserRepository
{
    public UserRepository(GmaoDbContext context) : base(context)
    {
    }

    // Override to include Role so the frontend can display the correct role name
    public override async Task<IEnumerable<User>> GetAllAsync()
    {
        return await _context.Users
            .Include(u => u.Role)
                .ThenInclude(r => r.RolePermissions)
            .Include(u => u.Societe)
            .ToListAsync();
    }

    public async Task<User?> GetByEmailAsync(string email)
    {
        return await _context.Users
            .Include(u => u.Role)
                .ThenInclude(r => r.RolePermissions)
            .Include(u => u.Societe)
            .Include(u => u.TechnicienCompetences)
                .ThenInclude(tc => tc.Competence)
            .FirstOrDefaultAsync(u => u.Email == email);
    }
}
