using System;

namespace Nomada.Shared.Models
{
    public class FraseDto
    {
        public int Id { get; set; }
        public string Texto { get; set; } = string.Empty;
        public string? Autor { get; set; }
    }
    public class AlertaSuscripcionDto
    {
        public bool Mostrar { get; set; }
        public string Mensaje { get; set; } = string.Empty;
        public string NivelRiesgo { get; set; } = "Info"; // Info, Warning, Danger
    }
}