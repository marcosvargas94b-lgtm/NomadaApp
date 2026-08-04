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

    public class Post
    {
        public int Id { get; set; }
        public Guid UsuarioId { get; set; }
        public string? Texto { get; set; }
        public string? MediaUrl { get; set; }
        public bool EsVideo { get; set; }
        public DateTime FechaCreacion { get; set; }
    }

    public class Like
    {
        public int Id { get; set; }
        public int PostId { get; set; }
        public Guid UsuarioId { get; set; }
        public DateTime FechaCreacion { get; set; }
    }

    public class Notificacion
    {
        public int Id { get; set; }
        public Guid UsuarioId { get; set; }
        public string Mensaje { get; set; } = string.Empty;
        public string Tipo { get; set; } = "General"; // "Like", "AlertaPago", "Baja"
        public string? RutaNavegacion { get; set; } // Ejemplo: "/mis-pagos" o "/inicio"
        public bool Leida { get; set; }
        public DateTime FechaCreacion { get; set; }
    }

    public class FraseMotivacional
    {
        public int Id { get; set; }
        public string Texto { get; set; } = string.Empty;
        public string? Autor { get; set; }
    }

    // ================= MÓDULO DE RESERVAS =================

    public class HorarioClase
    {
        public int Id { get; set; }
        public string HoraTexto { get; set; } = string.Empty; // Ej. "07:00 AM"
        public TimeSpan HoraOrden { get; set; } // Para que SQL los ordene correctamente
        public bool Activo { get; set; } = true;
    }

    public class Reserva
    {
        public int Id { get; set; }
        public Guid UsuarioId { get; set; }
        public int HorarioId { get; set; }
        public DateTime FechaReserva { get; set; } // Ej. 04-Agosto-2026
        public DateTime FechaOperacion { get; set; } = DateTime.UtcNow;
    }

    public class ConfiguracionBox
    {
        public int Id { get; set; }
        public int AforoMaximo { get; set; } = 20; // Límite por defecto
    }

    // ================= MÓDULO DE ENTRENAMIENTO (WOD) =================

    public class WodGeneral
    {
        public int Id { get; set; }
        public string Titulo { get; set; } = string.Empty; // Ej. "Viernes de Murph" o "Clase Coach Juan"
        public DateTime Fecha { get; set; } // El día al que pertenece este WOD
        public Guid CoachId { get; set; }
        public string NombreCoach { get; set; } = string.Empty; // Marca de agua
        public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
    }

    public class WodSeccion
    {
        public int Id { get; set; }
        public int WodGeneralId { get; set; }
        public string Subtitulo { get; set; } = string.Empty; // Ej. "Calentamiento", "AMRAP 15 Min"
        public string Contenido { get; set; } = string.Empty; // Ej. "10 saltos\n10 burpees"
        public int Orden { get; set; } // Para que no se revuelvan las secciones
    }

    public class CatalogoEjercicio
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string UrlVideo { get; set; } = string.Empty;
    }

    public class WodEjercicio
    {
        public int Id { get; set; }
        public int WodGeneralId { get; set; }
        public int EjercicioId { get; set; }
    }
}