using System;
using System.Collections.Generic;

namespace GMAO.Application.DTOs.Equipes
{
    public class EquipeDto
    {
        public int Id { get; set; }
        public string Nom { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int? ChefEquipeId { get; set; }
        public string? ChefEquipeNom { get; set; }
        public int SocieteId { get; set; }
        public DateTime CreatedAt { get; set; }
        public List<EquipeMembreDto> Membres { get; set; } = new List<EquipeMembreDto>();
    }

    public class EquipeMembreDto
    {
        public int Id { get; set; }
        public string Nom { get; set; } = string.Empty;
        public string Prenom { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? Role { get; set; }
    }

    public class CreateEquipeDto
    {
        public string Nom { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int? ChefEquipeId { get; set; }
        public List<int> MembreIds { get; set; } = new List<int>();
    }

    public class UpdateEquipeDto
    {
        public string Nom { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int? ChefEquipeId { get; set; }
        public List<int>? MembreIds { get; set; }
    }
}