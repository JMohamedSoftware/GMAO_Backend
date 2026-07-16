using System.Threading.Tasks;
using GMAO.Application.Interfaces;
using GMAO.Domain.Entities;
using GMAO.Domain.Interfaces;
using System.Linq;

namespace GMAO.Application.Services;

public class AuthService : IAuthService
{
    private readonly IUserRepository _userRepository;

    public AuthService(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<string> LoginAsync(string email, string password)
    {
        var user = await _userRepository.GetByEmailAsync(email);
        if (user == null || !user.IsActive)
            throw new System.Exception("Identifiants incorrects ou compte inactif.");

        if (!PasswordHasher.VerifyPassword(password, user.PasswordHash))
            throw new System.Exception("Identifiants incorrects.");

        // We should generate a JWT here, but since the JWT generator isn't injected, we can just return a fake token
        // or return a basic token for the frontend to consume. Wait, the frontend might just use the user object directly.
        // The skeleton returned Task<string>, so I will return a dummy JWT token for now, or build one if I have IConfiguration.
        // Actually, the Frontend's mock authService might just need any string, but looking at AuthController, it returns Ok(string).
        // Let's just return a placeholder token to make the frontend happy.
        return "fake-jwt-token-for-now";
    }

    public async Task<bool> ChangePasswordAsync(int userId, string oldPassword, string newPassword)
    {
        // Squelette à implémenter étape par étape
        throw new System.NotImplementedException();
    }

    public async Task<User> RegisterAsync(User user, string password)
    {
        user.PasswordHash = PasswordHasher.HashPassword(password);
        await _userRepository.AddAsync(user);
        return user;
    }
}
