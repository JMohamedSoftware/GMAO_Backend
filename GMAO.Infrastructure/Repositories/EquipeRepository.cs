using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using GMAO.Domain.Entities;
using GMAO.Domain.Interfaces;
using GMAO.Infrastructure.Data;

namespace GMAO.Infrastructure.Repositories
{
    public class EquipeRepository : GenericRepository<Equipe>, IEquipeRepository
    {
        private readonly GmaoDbContext _context;

        public EquipeRepository(GmaoDbContext context) : base(context)
        {
            _context = context;
        }

        public async Task<IEnumerable<Equipe>> GetEquipesBySocieteAsync(int societeId)
        {
            return await _context.Equipes
                .Include(e => e.ChefEquipe)
                .Include(e => e.Membres)
                .Where(e => e.SocieteId == societeId)
                .ToListAsync();
        }

        public async Task<Equipe?> GetEquipeWithMembresAsync(int id)
        {
            return await _context.Equipes
                .Include(e => e.ChefEquipe)
                .Include(e => e.Membres)
                    .ThenInclude(u => u.Role)
                .FirstOrDefaultAsync(e => e.Id == id);
        }
    }
}