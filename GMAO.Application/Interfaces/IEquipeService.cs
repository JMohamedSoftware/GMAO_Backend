using System.Collections.Generic;
using System.Threading.Tasks;
using GMAO.Application.DTOs.Equipes;

namespace GMAO.Application.Interfaces
{
    public interface IEquipeService
    {
        Task<IEnumerable<EquipeDto>> GetEquipesAsync(int societeId);
        Task<EquipeDto?> GetEquipeByIdAsync(int id);
        Task<EquipeDto> CreateEquipeAsync(CreateEquipeDto dto, int societeId);
        Task<EquipeDto> UpdateEquipeAsync(int id, UpdateEquipeDto dto);
        Task DeleteEquipeAsync(int id);
        Task AddMembreAsync(int equipeId, int userId);
        Task RemoveMembreAsync(int equipeId, int userId);
    }
}