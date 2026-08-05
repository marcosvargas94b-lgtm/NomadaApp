using System;

namespace Nomada.Shared.Models
{
    public class AvisoBoxDto
    {
        public int Id { get; set; }
        public string Titulo { get; set; } = string.Empty;
        public string Mensaje { get; set; } = string.Empty;
        public string NombreCoach { get; set; } = string.Empty;
        public DateTime FechaVencimiento { get; set; }
    }

    public class CrearAvisoRequest
    {
        public string GymCode { get; set; } = string.Empty;
        public Guid CoachId { get; set; }
        public string Titulo { get; set; } = string.Empty;
        public string Mensaje { get; set; } = string.Empty;
        public int DiasVigencia { get; set; } // Por defecto 1 o 3 días
    }
}