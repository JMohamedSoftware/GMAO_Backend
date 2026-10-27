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

        public async Task<IEnumerable<Localisation>> GetAllAsync(int? societeId = null)
        {
            var query = _context.Localisations.Include(l => l.SousLocalisations).AsQueryable();
            if (societeId.HasValue && societeId.Value > 0)
            {
                query = query.Where(l => l.SocieteId == societeId.Value);
            }
            return await query.ToListAsync();
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
            var existingLocalisation = await _context.Localisations.FirstOrDefaultAsync(l => l.Id == id);
            if (existingLocalisation == null) return null;

            existingLocalisation.Nom = localisation.Nom;
            existingLocalisation.Description = localisation.Description;
            existingLocalisation.ParentId = localisation.ParentId;

            await _context.SaveChangesAsync();
            return existingLocalisation;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var localisation = await _context.Localisations.FirstOrDefaultAsync(l => l.Id == id);
            if (localisation == null) return false;

            await DeleteSubTreeAsync(localisation.Id);
            await _context.SaveChangesAsync();
            return true;
        }

        private async Task DeleteSubTreeAsync(int id)
        {
            var children = await _context.Localisations.Where(l => l.ParentId == id).ToListAsync();
            foreach (var child in children)
            {
                await DeleteSubTreeAsync(child.Id);
            }

            var equipments = await _context.Equipements.Where(e => e.LocalisationId == id).ToListAsync();
            foreach (var eq in equipments)
            {
                eq.LocalisationId = null;
            }

            var loc = await _context.Localisations.FirstOrDefaultAsync(l => l.Id == id);
            if (loc != null)
            {
                _context.Localisations.Remove(loc);
            }
        }

        public async Task<IEnumerable<Localisation>> GetTreeAsync(int? societeId = null)
        {
            var query = _context.Localisations.AsQueryable();
            if (societeId.HasValue && societeId.Value > 0)
            {
                query = query.Where(l => l.SocieteId == societeId.Value);
            }
            var allNodes = await query.ToListAsync();
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
