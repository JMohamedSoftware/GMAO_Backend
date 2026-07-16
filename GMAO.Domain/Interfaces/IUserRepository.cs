using System.Threading.Tasks;
using GMAO.Domain.Entities;

namespace GMAO.Domain.Interfaces;

public interface IUserRepository : IGenericRepository<User>
{
    Task<User?> GetByEmailAsync(string email);
}
