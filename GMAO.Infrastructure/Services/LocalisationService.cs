using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using GMAO.Application.Interfaces;
using GMAO.Domain.Entities;
using GMAO.Infrastructure.Data;

namespace GMAO.Infrastructure.Services
{
    public class LocalisationService : ILocalisationService
    {
        private readonly GmaoDbContext _context;

        public LocalisationService(GmaoDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<Localisation>> GetAllAsync()
        {
            return await _context.Localisations
                .Include(l => l.SousLocalisations)
                .ToListAsync();
        }

        public async Task<Localisation?> GetByIdAsync(int id)
        {
            return await _context.Localisations
                .Include(l => l.SousLocalisations)
                .Include(l => l.Equipements)
                .FirstOrDefaultAsync(l => l.Id == id);
        }

        public async Task<Localisation> CreateAsync(Localisation localisation)
        {
            _context.Localisations.Add(localisation);
            await _context.SaveChangesAsync();
            return localisation;
        }

        public async Task<Localisation?> UpdateAsync(int id, Localisation localisation)
        {
            var existingLocalisation = await _context.Localisations.FindAsync(id);
            if (existingLocalisation == null) return null;

            existingLocalisation.Nom = localisation.Nom;
            existingLocalisation.Description = localisation.Description;
            existingLocalisation.ParentId = localisation.ParentId;

            await _context.SaveChangesAsync();
            return existingLocalisation;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var localisation = await _context.Localisations
                .Include(l => l.Equipements)
                .Include(l => l.SousLocalisations)
                .FirstOrDefaultAsync(l => l.Id == id);
                
            if (localisation == null) return false;

            // Prevent deletion if there are equipments or sub-localisations attached
            if (localisation.Equipements.Any() || localisation.SousLocalisations.Any())
            {
                return false;
            }

            _context.Localisations.Remove(localisation);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<IEnumerable<Localisation>> GetTreeAsync()
        {
            // Only fetch root nodes, and recursively include their children
            var allNodes = await _context.Localisations.ToListAsync();
            var roots = allNodes.Where(n => n.ParentId == null).ToList();

            foreach (var root in roots)
            {
                BuildTree(root, allNodes);
            }

            return roots;
        }

        private void BuildTree(Localisation node, List<Localisation> allNodes)
        {
            var children = allNodes.Where(n => n.ParentId == node.Id).ToList();
            node.SousLocalisations = children;
            foreach (var child in children)
            {
                BuildTree(child, allNodes);
            }
        }
    }
}
