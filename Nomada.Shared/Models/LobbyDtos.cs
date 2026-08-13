using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nomada.Shared.Models
{
    public class UsuarioLobbyDto
    {
        public int ReservaId { get; set; } 
        public Guid UsuarioId { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string ApellidoPaterno { get; set; } = string.Empty;
        public string? FotoPerfil { get; set; }
        public string MetodoIngreso { get; set; } = string.Empty;
    }

    public class SesionLobbyDto
    {
        public int SesionId { get; set; }
        public string CodigoAcceso { get; set; } = string.Empty;
        public int HorarioId { get; set; }
    }

}
