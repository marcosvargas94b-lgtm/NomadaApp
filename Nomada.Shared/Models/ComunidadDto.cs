using System;

namespace Nomada.Shared.Models
{
    public class CrearPostRequest
    {
        public Guid UsuarioId { get; set; }
        public string? Texto { get; set; }
        public string? MediaBase64 { get; set; } // Enviaremos la foto/video en texto base64 temporalmente
        public bool EsVideo { get; set; }
    }

    public class PostFeedDto
    {
        public int Id { get; set; }
        public Guid UsuarioId { get; set; }
        public string NombreAutor { get; set; } = string.Empty;
        public string Iniciales { get; set; } = string.Empty;
        public string? Texto { get; set; }
        public string? MediaUrl { get; set; }
        public bool EsVideo { get; set; }
        public string TiempoTranscurrido { get; set; } = string.Empty;
        public int CantidadLikes { get; set; }
        public bool YoLeDiLike { get; set; }
        public bool EsMio { get; set; }
    }

    public class NotificacionDto
    {
        public int Id { get; set; }
        public string Mensaje { get; set; } = string.Empty;
        public string TiempoTranscurrido { get; set; } = string.Empty;
        public bool Leida { get; set; }
    }

    public class RankingUsuarioDto
    {
        public Guid Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string ApellidoPaterno { get; set; } = string.Empty;
        public string Iniciales { get; set; } = string.Empty;
        public int TotalLikes { get; set; }
        public int Posicion { get; set; } // 1 (Oro), 2 (Plata), 3 (Bronce)...
    }
}