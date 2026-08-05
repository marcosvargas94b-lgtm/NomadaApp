using System;
using System.Collections.Generic;

namespace Nomada.Shared.Models
{
    // Modelo para mostrar los datos en la tabla
    public class UsuarioAdminDto
    {
        public Guid Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string ApellidoPaterno { get; set; } = string.Empty;
        public string Correo { get; set; } = string.Empty;
        public string? TipoSuscripcion { get; set; } // Puede ser "Mensual", "Semanal", "PaqueteClases", o null si es nuevo
        public int? DiasRestantes { get; set; }
        public int? ClasesRestantes { get; set; }
        public bool Seleccionado { get; set; }
        public bool IsLoading { get; set; }
        public int? RolId { get; set; } // 1=SuperAdmin, 2=Coach, 3=Atleta
        public List<int> PermisosIds { get; set; } = new();
    }

    // Modelo para enviar instrucciones de cambio de estatus a la API
    public class ActualizarEstatusRequest
    {
        public List<Guid> UsuarioIds { get; set; } = new List<Guid>();
        public string NuevoEstatus { get; set; } = string.Empty;
    }

    public class RegistrarCobroRequest
    {
        public string GymCode { get; set; } = string.Empty;
        public Guid AtletaId { get; set; }
        public Guid CoachId { get; set; } // Para saber quién hizo el cobro en caja
        public string TipoCobro { get; set; } = string.Empty; // "Mensual", "Semanal", "PaqueteClases", "Especial"
        public decimal Monto { get; set; }
        public DateTime? FechaPagoMensual { get; set; }
        public int? NumeroSemanas { get; set; }
        public int? NumeroClases { get; set; }
        public string? Descripcion { get; set; }
    }

    public class PermisoDto
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
    }

    public class AsignarRolPermisoRequest
    {
        public Guid UsuarioId { get; set; }
        public int RolId { get; set; } // 2 para Coach, 3 para regresarlo a Atleta
        public List<int> PermisosIds { get; set; } = new();
    }

}