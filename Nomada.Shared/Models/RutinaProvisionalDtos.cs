using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nomada.Shared.Models
{
    public class GenerarRutinaProvisionalRequest
    {
        public string GymCode { get; set; } = string.Empty;
        public Guid UsuarioId { get; set; }
        public int Dias { get; set; } = 1; // 1 a 5
        public string Entorno { get; set; } = "CasaAutocargas"; // 'CasaAutocargas', 'CasaMancuernas', 'Gimnasio'
        public string Dificultad { get; set; } = "Intermedio"; // 'Principiante', 'Intermedio', 'Avanzado'
        public string Notas { get; set; } = string.Empty;
    }

    public class RutinaProvisionalDiaDto
    {
        public int Id { get; set; }
        public int DiaNumero { get; set; }
        public string TituloDia { get; set; } = string.Empty;
        public List<WodSeccionDto> Secciones { get; set; } = new();
        public bool Completado { get; set; }
        public DateTime? FechaRealizacion { get; set; }
        public string? NotasAtleta { get; set; }
    }

    public class RutinaProvisionalCompletaDto
    {
        public int Id { get; set; }
        public int DiasTotales { get; set; }
        public DateTime FechaExpiracion { get; set; }
        public List<RutinaProvisionalDiaDto> Dias { get; set; } = new();
    }

    public class CompletarDiaProvisionalRequest
    {
        public string NotasAtleta { get; set; } = string.Empty;
    }

    // Modelo interno para la respuesta de Gemini
    public class DiaGeneradoIADto
    {
        public int DiaNumero { get; set; }
        public string TituloDia { get; set; } = string.Empty;
        public List<WodSeccionDto> Secciones { get; set; } = new();
        public Dictionary<string, int> FatigaMuscular { get; set; } = new();
    }
}
