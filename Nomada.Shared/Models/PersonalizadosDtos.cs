using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nomada.Shared.Models
{
    // ================= DTOs DE ENTRENAMIENTO PERSONALIZADO =================
    public class CrearWodPersonalizadoRequest
    {
        public string GymCode { get; set; } = string.Empty;
        public Guid CoachId { get; set; }
        public Guid AtletaId { get; set; }
        public string Titulo { get; set; } = string.Empty;
        public List<WodSeccionDto> Secciones { get; set; } = new();
        public List<int> EjerciciosIds { get; set; } = new();
    }
    public class ActualizarWodPersonalizadoRequest
    {
        public string Titulo { get; set; } = string.Empty;
        public List<WodSeccionDto> Secciones { get; set; } = new();
        public List<int> EjerciciosIds { get; set; } = new();
    }
    public class FinalizarEntrenoRequest
    {
        public string NotasAtleta { get; set; } = string.Empty;
    }

    public class RutinaAtletaResumenDto
    {
        public int Id { get; set; }
        public string Titulo { get; set; } = string.Empty;
        public DateTime FechaCreacion { get; set; }
        public bool RealizadoHoy { get; set; }
    }
}
