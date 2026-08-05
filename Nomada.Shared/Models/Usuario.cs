using System;

namespace Nomada.Shared.Models
{
    public class Usuario
    {
        public Guid Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string ApellidoPaterno { get; set; } = string.Empty;
        public string? ApellidoMaterno { get; set; }
        public DateTime FechaNacimiento { get; set; }
        public string Sexo { get; set; } = string.Empty;
        public string Correo { get; set; } = string.Empty;

        public byte[] PasswordHash { get; set; }
        public byte[] PasswordSalt { get; set; }

        public int RolId { get; set; }
        public Rol? Rol { get; set; }

        public bool Activo { get; set; } = true;

        public string? ResetToken { get; set; }
        public DateTime? ResetTokenExpiracion { get; set; }
        public string GymCode { get; set; } = string.Empty;
        public string EstatusAprobacion { get; set; } = "En Espera";

        // ================= NUEVOS CAMPOS DEL PERFIL =================
        public string? FotoPerfil { get; set; }
        public string? FrasePersonal { get; set; }
        public string? EjercicioFavorito { get; set; }
        public string? EjercicioMenosFavorito { get; set; }
        public decimal? Peso { get; set; }
        public decimal? Estatura { get; set; }
        public string? FotoDestacada1 { get; set; }
        public string? FotoDestacada2 { get; set; }
        public string? FotoDestacada3 { get; set; }
    }
}