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

        private async Task EnsureDefaultLocalisationAsync(int societeId)
        {
            var exists = await _context.Localisations.AnyAsync(l => l.SocieteId == societeId);
            if (!exists)
            {
                var soc = await _context.Societes.IgnoreQueryFilters().FirstOrDefaultAsync(s => s.Id == societeId);
                if (soc != null)
                {
                    var defaultLoc = new Localisation
                    {
                        SocieteId = soc.Id,
                        Nom = $"Usine {soc.Nom}",
                        Description = $"Site et usine principale de {soc.Nom}",
                        ParentId = null
                    };
                    _context.Localisations.Add(defaultLoc);
                    await _context.SaveChangesAsync();

                    // Rattacher automatiquement les équipements orphelins existants à cette usine
                    var orphanEquipments = await _context.Equipements.Where(e => e.SocieteId == soc.Id && e.LocalisationId == null).ToListAsync();
                    foreach (var eq in orphanEquipments)
                    {
                        eq.LocalisationId = defaultLoc.Id;
                    }
                    if (orphanEquipments.Any())
                    {
                        await _context.SaveChangesAsync();
                    }
                }
            }
        }

        public async Task<IEnumerable<Localisation>> GetAllAsync(int? societeId = null)
        {
            if (societeId.HasValue && societeId.Value > 0)
            {
                await EnsureDefaultLocalisationAsync(societeId.Value);
            }

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
            if (societeId.HasValue && societeId.Value > 0)
            {
                await EnsureDefaultLocalisationAsync(societeId.Value);
            }

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
