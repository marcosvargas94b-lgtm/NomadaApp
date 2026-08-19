using System;

namespace Nomada.Shared.Models
{
    public class RetoCatalogoDto
    {
        public int Id { get; set; }
        public string Titulo { get; set; } = "";
        public string Descripcion { get; set; } = "";
        public string UrlImagen { get; set; } = "";
        public bool EsPremioMaximo { get; set; }
        public bool EsAutomatico { get; set; }
        public bool Desbloqueado { get; set; } // Para la UI del atleta
        public DateTime? FechaDesbloqueo { get; set; }
    }

    public class AsignarRetoRequest
    {
        public string GymCode { get; set; } = "";
        public Guid AtletaId { get; set; }
        public int RetoCatalogoId { get; set; }
    }
}