using System;
using System.Collections.Generic;

namespace Nomada.Shared.Models
{
    public class EjercicioDto
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string UrlVideo { get; set; } = string.Empty;
    }
    public class WodSeccionDto
    {
        public string Subtitulo { get; set; } = string.Empty;
        public string Contenido { get; set; } = string.Empty;
        public int Orden { get; set; }
    }

    public class WodGeneralDto
    {
        public int Id { get; set; }
        public string Titulo { get; set; } = string.Empty;
        public DateTime Fecha { get; set; }
        public Guid CoachId { get; set; } // <--- ESTO ES LO NUEVO
        public string NombreCoach { get; set; } = string.Empty;
        public List<WodSeccionDto> Secciones { get; set; } = new();
        public List<EjercicioDto> Ejercicios { get; set; } = new();
    }

    public class CrearWodRequest
    {
        public string Titulo { get; set; } = string.Empty;
        public DateTime Fecha { get; set; }
        public Guid CoachId { get; set; }
        public List<WodSeccionDto> Secciones { get; set; } = new();
        public List<int> EjerciciosIds { get; set; } = new(); // IDs seleccionados del catálogo
    }
    public class ActualizarWodRequest
    {
        public int Id { get; set; }
        public string Titulo { get; set; } = string.Empty;
        public DateTime Fecha { get; set; }
        public Guid CoachId { get; set; }
        public List<WodSeccionDto> Secciones { get; set; } = new();
        public List<int> EjerciciosIds { get; set; } = new();
    }
}