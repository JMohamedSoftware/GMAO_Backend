using System.Collections.Generic;
using GMAO.Domain.Entities;

namespace GMAO.Application.DTOs.Auth;

public class RegisterDto
{
    public User User { get; set; } = null!;
    public string Password { get; set; } = string.Empty;
    public List<int> CompetenceIds { get; set; } = new List<int>();
}
