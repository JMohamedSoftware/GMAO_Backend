using System.Threading.Tasks;
using GMAO.Domain.Entities;
using GMAO.Application.DTOs.Auth;

namespace GMAO.Application.Interfaces;

public interface IAuthService
{
    /// <summary>Login → retourne accessToken + refreshToken + user info</summary>
    Task<AuthResponseDto> LoginAsync(string email, string password);

    /// <summary>Rotation du refreshToken → nouveaux tokens</summary>
    Task<AuthResponseDto> RefreshTokenAsync(string refreshToken);

    /// <summary>Révocation du refreshToken (logout)</summary>
    Task LogoutAsync(string refreshToken);

    /// <summary>Retourne le profil de l'utilisateur à partir de son Id</summary>
    Task<UserProfileDto> GetMeAsync(int userId);

    /// <summary>Changement de mot de passe</summary>
    Task<bool> ChangePasswordAsync(int userId, string oldPassword, string newPassword);

    /// <summary>Création d'un compte utilisateur</summary>
    Task<User> RegisterAsync(User user, string password);
}
