using System;

namespace Nomada.Shared.Entities
{
    public class Suscripcion
    {
        public int Id { get; set; }
        public Guid UsuarioId { get; set; }
        public string TipoSuscripcion { get; set; } = string.Empty;
        public DateTime FechaInicio { get; set; }
        public DateTime? FechaFin { get; set; }
        public int? ClasesRestantes { get; set; }
        public bool Activa { get; set; }
    }

    public class Ingreso
    {
        public int Id { get; set; }
        public Guid UsuarioId { get; set; }
        public Guid RecibidoPorId { get; set; }
        public string TipoCobro { get; set; } = string.Empty;
        public decimal Monto { get; set; }
        public DateTime FechaCobro { get; set; }
        public string? Descripcion { get; set; }
    }

    public class Asistencia
    {
        public int Id { get; set; }
        public Guid UsuarioId { get; set; }
        public DateTime FechaHora { get; set; }
        public string MetodoRegistro { get; set; } = string.Empty;
    }

    public class Permiso
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string? Descripcion { get; set; }
    }

    public class UsuarioPermiso
    {
        public Guid UsuarioId { get; set; }
        public int PermisoId { get; set; }
    }
}