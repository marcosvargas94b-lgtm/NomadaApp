using Microsoft.AspNetCore.Http;
using System;

namespace Nomada.API.Models // <--- Nota cómo ahora vive en la API
{
    public class ActualizarPerfilForm
    {
        public Guid Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string ApellidoPaterno { get; set; } = string.Empty;
        public string? ApellidoMaterno { get; set; }
        public DateTime FechaNacimiento { get; set; }

        public string? FrasePersonal { get; set; }
        public string? EjercicioFavorito { get; set; }
        public string? EjercicioMenosFavorito { get; set; }

        public decimal? Peso { get; set; }
        public decimal? Estatura { get; set; }

        public string? PasswordActual { get; set; }
        public string? NuevaPassword { get; set; }

        // Archivos a subir
        public IFormFile? FotoPerfil { get; set; }
        public IFormFile? FotoDestacada1 { get; set; }
        public IFormFile? FotoDestacada2 { get; set; }
        public IFormFile? FotoDestacada3 { get; set; }
    }
}