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
        // Squelette à implémenter étape par étape
        throw new System.NotImplementedException();
    }
}
