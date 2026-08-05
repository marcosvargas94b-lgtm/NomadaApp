using System;
using System.Collections.Generic;

namespace Nomada.Shared.Models
{
    public class DiaReservaDto
    {
        public DateTime Fecha { get; set; }
        public string DiaSemana { get; set; } = string.Empty; // "LUN", "MAR"
        public string NumeroDia { get; set; } = string.Empty; // "04"
    }

    public class AtletaAgendadoDto
    {
        public Guid UsuarioId { get; set; }
        public string NombreCompleto { get; set; } = string.Empty;
        public string Iniciales { get; set; } = string.Empty;
    }

    public class HorarioDisponibleDto
    {
        public int HorarioId { get; set; }
        public string HoraTexto { get; set; } = string.Empty;
        public int OcupacionActual { get; set; }
        public int AforoMaximo { get; set; }
        public bool YoEstoyAgendado { get; set; }
        public bool EstaLleno => OcupacionActual >= AforoMaximo;
        public List<AtletaAgendadoDto> AtletasAgendados { get; set; } = new();
    }

    public class HacerReservaRequest
    {
        public string GymCode { get; set; } = string.Empty;
        public Guid UsuarioId { get; set; }
        public int HorarioId { get; set; }
        public DateTime FechaReserva { get; set; }
    }
}