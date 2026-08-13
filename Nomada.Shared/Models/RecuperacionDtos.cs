using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nomada.Shared.Models
{
    // ================= MÓDULO DE RECUPERACIÓN (IA) =================
    public class EstadoRecuperacionDto
    {
        public double DisponibilidadGlobal { get; set; }
        public string Mensaje { get; set; } = string.Empty;
        public List<MusculoRecuperacionDto> Musculos { get; set; } = new();

        public bool RequiereRPEHoy { get; set; }
        public int? AsistenciaPendienteId { get; set; }
        public string? TituloWodPendiente { get; set; }

        // --- LOS DOS NUEVOS CAMPOS ---
        public string Sexo { get; set; } = "M";
        public bool PerfilCompleto { get; set; } = true;
    }

    public class MusculoRecuperacionDto
    {
        public string Nombre { get; set; } = string.Empty;
        public int Porcentaje { get; set; }
    }

    public class GuardarRPERequest
    {
        public int RPE { get; set; }
    }
}
