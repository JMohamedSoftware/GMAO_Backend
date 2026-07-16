using System.Threading.Tasks;
using GMAO.Domain.Entities;

namespace GMAO.Application.Interfaces;

public interface IAuthService
{
    Task<string> LoginAsync(string email, string password);
    Task<bool> ChangePasswordAsync(int userId, string oldPassword, string newPassword);
    Task<User> RegisterAsync(User user, string password);
}
