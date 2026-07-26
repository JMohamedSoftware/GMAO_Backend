using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GMAO.Application.DTOs.Equipes;
using GMAO.Application.Interfaces;
using GMAO.Domain.Entities;
using GMAO.Domain.Interfaces;

namespace GMAO.Application.Services
{
    public class EquipeService : IEquipeService
    {
        private readonly IEquipeRepository _equipeRepository;
        private readonly IUserRepository _userRepository;

        public EquipeService(IEquipeRepository equipeRepository, IUserRepository userRepository)
        {
            _equipeRepository = equipeRepository;
            _userRepository = userRepository;
        }

        public async Task<IEnumerable<EquipeDto>> GetEquipesAsync(int societeId)
        {
            var equipes = await _equipeRepository.GetEquipesBySocieteAsync(societeId);
            return equipes.Select(MapToDto);
        }

        public async Task<EquipeDto?> GetEquipeByIdAsync(int id)
        {
            var equipe = await _equipeRepository.GetEquipeWithMembresAsync(id);
            return equipe == null ? null : MapToDto(equipe);
        }

        public async Task<EquipeDto> CreateEquipeAsync(CreateEquipeDto dto, int societeId)
        {
            var equipe = new Equipe
            {
                Nom = dto.Nom,
                Description = dto.Description,
                ChefEquipeId = dto.ChefEquipeId,
                SocieteId = societeId,
                CreatedAt = DateTime.UtcNow
            };

            foreach (var membreId in dto.MembreIds)
            {
                var user = await _userRepository.GetByIdAsync(membreId);
                if (user != null)
                {
                    equipe.Membres.Add(user);
                }
            }

            await _equipeRepository.AddAsync(equipe);
            return MapToDto(equipe);
        }

        public async Task<EquipeDto> UpdateEquipeAsync(int id, UpdateEquipeDto dto)
        {
            var equipe = await _equipeRepository.GetEquipeWithMembresAsync(id);
            if (equipe == null) throw new Exception("Equipe non trouvée");

            equipe.Nom = dto.Nom;
            equipe.Description = dto.Description;
            equipe.ChefEquipeId = dto.ChefEquipeId;

            if (dto.MembreIds != null)
            {
                equipe.Membres.Clear();
                foreach (var membreId in dto.MembreIds)
                {
                    var user = await _userRepository.GetByIdAsync(membreId);
                    if (user != null)
                    {
                        equipe.Membres.Add(user);
                    }
                }
            }

            await _equipeRepository.UpdateAsync(equipe);
            return MapToDto(equipe);
        }

        public async Task DeleteEquipeAsync(int id)
        {
            var equipe = await _equipeRepository.GetByIdAsync(id);
            if (equipe != null)
            {
                await _equipeRepository.DeleteAsync(equipe);
            }
        }

        public async Task AddMembreAsync(int equipeId, int userId)
        {
            var equipe = await _equipeRepository.GetEquipeWithMembresAsync(equipeId);
            var user = await _userRepository.GetByIdAsync(userId);
            if (equipe != null && user != null && !equipe.Membres.Any(m => m.Id == userId))
            {
                equipe.Membres.Add(user);
                await _equipeRepository.UpdateAsync(equipe);
            }
        }

        public async Task RemoveMembreAsync(int equipeId, int userId)
        {
            var equipe = await _equipeRepository.GetEquipeWithMembresAsync(equipeId);
            if (equipe != null)
            {
                var membre = equipe.Membres.FirstOrDefault(m => m.Id == userId);
                if (membre != null)
                {
                    equipe.Membres.Remove(membre);
                    await _equipeRepository.UpdateAsync(equipe);
                }
            }
        }

        private EquipeDto MapToDto(Equipe e)
        {
            return new EquipeDto
            {
                Id = e.Id,
                Nom = e.Nom,
                Description = e.Description,
                ChefEquipeId = e.ChefEquipeId,
                ChefEquipeNom = e.ChefEquipe != null ? $"{e.ChefEquipe.Prenom} {e.ChefEquipe.Nom}" : null,
                SocieteId = e.SocieteId,
                CreatedAt = e.CreatedAt,
                Membres = e.Membres.Select(m => new EquipeMembreDto
                {
                    Id = m.Id,
                    Nom = m.Nom,
                    Prenom = m.Prenom,
                    Email = m.Email,
                    Role = m.Role?.Nom
                }).ToList()
            };
        }
    }
}
