using System;

namespace Nomada.Shared.Models
{
    public class TimerConfiguracion
    {
        public string Modo { get; set; } = "For Time"; // For Time, AMRAP, Intervalos, EMOM
        public int TiempoTrabajoSegundos { get; set; } = 0; // O Time Cap
        public int TiempoDescansoSegundos { get; set; } = 0;
        public int Rondas { get; set; } = 1;
        public int Bloques { get; set; } = 1;
        public int DescansoEntreBloquesSegundos { get; set; } = 0;
        public int CuentaRegresivaInicial { get; set; } = 7; // Tus 7 segundos por defecto
    }
}