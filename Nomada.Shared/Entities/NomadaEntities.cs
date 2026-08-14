using System;
using System.ComponentModel.DataAnnotations;

namespace Nomada.Shared.Entities
{
    public class Suscripcion
    {
        public int Id { get; set; }
        public string GymCode { get; set; } = string.Empty; // Neutro y Obligatorio
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
        public string GymCode { get; set; } = string.Empty;
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
        public string GymCode { get; set; } = string.Empty;
        public Guid UsuarioId { get; set; }
        public DateTime FechaHora { get; set; }
        public string MetodoRegistro { get; set; } = string.Empty;
        public int HorarioId { get; set; }
        public int WodGeneralId { get; set; }
        public int? RPE { get; set; } // El nivel de esfuerzo de esa clase
    }

    public class WodScore
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [MaxLength(50)]
        public string GymCode { get; set; } = string.Empty;

        [Required]
        public int WodGeneralId { get; set; }

        [Required]
        public Guid UsuarioId { get; set; }

        public DateTime Fecha { get; set; }

        [Required]
        [MaxLength(20)]
        public string TipoScore { get; set; } = string.Empty; // 'Tiempo', 'Rondas', 'Reps', 'Asistencia'

        // Métricas
        [MaxLength(10)]
        public string? TiempoFormato { get; set; }
        public int? TiempoSegundos { get; set; }
        public int? Rondas { get; set; }
        public int? Repeticiones { get; set; }
        public int HorarioId { get; set; }
        [Required]
        [MaxLength(20)]
        public string Categoria { get; set; } = "General";
        public bool EsCapturaManual { get; set; } = false;
        public DateTime FechaRegistro { get; set; } = DateTime.UtcNow;
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
        public string GymCode { get; set; } = string.Empty;
        public Guid UsuarioId { get; set; }
        public string? Texto { get; set; }
        public string? MediaUrl { get; set; }
        public bool EsVideo { get; set; }
        public DateTime FechaCreacion { get; set; }
    }

    public class Like
    {
        public int Id { get; set; }
        public string GymCode { get; set; } = string.Empty;
        public int PostId { get; set; }
        public Guid UsuarioId { get; set; }
        public DateTime FechaCreacion { get; set; }
    }

    public class Notificacion
    {
        public int Id { get; set; }
        public string GymCode { get; set; } = string.Empty;
        public Guid UsuarioId { get; set; }
        public string Mensaje { get; set; } = string.Empty;
        public string Tipo { get; set; } = "General";
        public string? RutaNavegacion { get; set; }
        public bool Leida { get; set; }
        public DateTime FechaCreacion { get; set; }
    }

    public class FraseMotivacional
    {
        public int Id { get; set; }
        public string GymCode { get; set; } = string.Empty;
        public string Texto { get; set; } = string.Empty;
        public string? Autor { get; set; }
    }

    // ================= MÓDULO DE RESERVAS =================

    public class HorarioClase
    {
        public int Id { get; set; }
        public string GymCode { get; set; } = string.Empty;
        public string HoraTexto { get; set; } = string.Empty;
        public TimeSpan HoraOrden { get; set; }
        public bool Activo { get; set; } = true;
    }

    public class Reserva
    {
        public int Id { get; set; }
        public string GymCode { get; set; } = string.Empty;
        public Guid UsuarioId { get; set; }
        public int HorarioId { get; set; }
        public DateTime FechaReserva { get; set; }
        public DateTime FechaOperacion { get; set; } = DateTime.UtcNow;
        public string MetodoIngreso { get; set; } = "App"; // App, Codigo, Manual
        public bool AsistenciaConfirmada { get; set; } = false;
    }

    public class ConfiguracionBox
    {
        public int Id { get; set; }
        public string GymCode { get; set; } = string.Empty;
        public int AforoMaximo { get; set; } = 20;
    }

    // ================= MÓDULO DE ENTRENAMIENTO (WOD) =================

    public class WodGeneral
    {
        public int Id { get; set; }
        public string GymCode { get; set; } = string.Empty;
        public string Titulo { get; set; } = string.Empty;
        public DateTime Fecha { get; set; }
        public Guid CoachId { get; set; }
        public string NombreCoach { get; set; } = string.Empty;
        public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
        public string? JsonFatigaMuscular { get; set; } // Análisis de la IA
        public bool TienePesos { get; set; }
    }

    public class WodSeccion
    {
        public int Id { get; set; }
        public int WodGeneralId { get; set; }
        public string Subtitulo { get; set; } = string.Empty;
        public string Contenido { get; set; } = string.Empty;
        public int Orden { get; set; }
    }

    public class CatalogoEjercicio
    {
        public int Id { get; set; }
        public string GymCode { get; set; } = string.Empty;
        public string Nombre { get; set; } = string.Empty;
        public string UrlVideo { get; set; } = string.Empty;
    }

    public class WodEjercicio
    {
        public int Id { get; set; }
        public int WodGeneralId { get; set; }
        public int EjercicioId { get; set; }
    }

    public class AvisoBox
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(20)]
        public string GymCode { get; set; } = string.Empty; // Nuestro candado maestro

        [Required]
        [MaxLength(50)]
        public string Titulo { get; set; } = string.Empty; // Ej. "¡Fiesta Patrias!"

        [Required]
        public string Mensaje { get; set; } = string.Empty; // El texto principal del aviso

        public Guid CoachId { get; set; } // Quién lo publicó
        public string NombreCoach { get; set; } = string.Empty; // Firma del coach

        public DateTime FechaPublicacion { get; set; } = DateTime.UtcNow;
        public DateTime FechaVencimiento { get; set; } // Cuándo deja de aparecer en el pizarrón
    }

    public class SesionClase
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(20)]
        public string GymCode { get; set; } = string.Empty;

        public DateTime Fecha { get; set; }
        public int HorarioId { get; set; }

        [Required]
        [MaxLength(10)]
        public string CodigoAcceso { get; set; } = string.Empty;

        public Guid CoachAperturaId { get; set; }
    }

    // ================= MÓDULO DE ENTRENAMIENTO PERSONALIZADO =================

    public class WodPersonalizado
    {
        public int Id { get; set; }
        public string GymCode { get; set; } = string.Empty;
        public Guid CoachId { get; set; }
        public Guid AtletaId { get; set; } // El dueño de la rutina
        public string Titulo { get; set; } = string.Empty;
        public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
        public string? JsonFatigaMuscular { get; set; } // Fatiga Base
        public bool TienePesos { get; set; }
    }

    public class WodPersonalizadoSeccion
    {
        public int Id { get; set; }
        public int WodPersonalizadoId { get; set; }
        public string Subtitulo { get; set; } = string.Empty;
        public string Contenido { get; set; } = string.Empty;
        public int Orden { get; set; }
    }

    public class WodPersonalizadoEjercicio
    {
        public int Id { get; set; }
        public int WodPersonalizadoId { get; set; }
        public int EjercicioId { get; set; }
    }

    public class EntrenoPersonalizadoRealizado
    {
        public int Id { get; set; }
        public string GymCode { get; set; } = string.Empty;
        public Guid AtletaId { get; set; }
        public int WodPersonalizadoId { get; set; }
        public DateTime FechaRealizacion { get; set; }
        public string? NotasAtleta { get; set; }
        public string? JsonFatigaAjustada { get; set; } // Modificado por IA tras leer las notas
    }

    public class RutinaProvisionalIA
    {
        public int Id { get; set; }
        public string GymCode { get; set; } = string.Empty;
        public Guid UsuarioId { get; set; }
        public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
        public DateTime FechaExpiracion { get; set; }
        public int DiasTotales { get; set; }
        public string Entorno { get; set; } = string.Empty;
        public string Dificultad { get; set; } = string.Empty;
        public string? NotasSolicitud { get; set; }
    }

    public class RutinaProvisionalDia
    {
        public int Id { get; set; }
        public int RutinaProvisionalId { get; set; }
        public int DiaNumero { get; set; }
        public string TituloDia { get; set; } = string.Empty;
        public string ContenidoJson { get; set; } = string.Empty;
        public bool Completado { get; set; }
        public DateTime? FechaRealizacion { get; set; }
        public string? NotasAtleta { get; set; }
        public string? JsonFatigaAjustada { get; set; }
    }

}