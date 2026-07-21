using System.Threading.Tasks;
using GMAO.Domain.Entities;
using GMAO.Domain.Interfaces;
using GMAO.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace GMAO.Infrastructure.Repositories;

public class EquipementRepository : GenericRepository<Equipement>, IEquipementRepository
{
    public EquipementRepository(GmaoDbContext context) : base(context)
    {
    }

    public async Task<Equipement?> GetByCodeAsync(string code)
    {
        return await _context.Equipements.FirstOrDefaultAsync(e => e.Code == code);
    }

    public async Task<System.Collections.Generic.IEnumerable<Equipement>> GetAllWithDetailsAsync(int societeId)
    {
        return await _context.Equipements
            .Include(e => e.Famille)
            .Include(e => e.Localisation)
            .Include(e => e.Fournisseur)
            .Where(e => e.SocieteId == societeId)
            .ToListAsync();
    }

    public async Task<Equipement?> GetByIdWithDetailsAsync(int id, int societeId)
    {
        return await _context.Equipements
            .Include(e => e.Famille)
            .Include(e => e.Localisation)
            .Include(e => e.Fournisseur)
            .FirstOrDefaultAsync(e => e.Id == id && e.SocieteId == societeId);
    }
}
