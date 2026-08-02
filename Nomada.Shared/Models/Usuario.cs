using System;

namespace Nomada.Shared.Models
{
    public class Usuario
    {
        public Guid Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string ApellidoPaterno { get; set; } = string.Empty;

        // El signo "?" permite que C# acepte el NULL de la base de datos
        public string? ApellidoMaterno { get; set; }

        public DateTime FechaNacimiento { get; set; }
        public string Sexo { get; set; } = string.Empty;
        public string Correo { get; set; } = string.Empty;

        public byte[] PasswordHash { get; set; }
        public byte[] PasswordSalt { get; set; }

        public int RolId { get; set; }
        public Rol? Rol { get; set; } // Opcional al momento de hacer el Login

        public bool Activo { get; set; } = true;

        // Agregamos el "?" porque al crear el usuario, el token es NULL en SQL
        public string? ResetToken { get; set; }
        public DateTime? ResetTokenExpiracion { get; set; }

        public string EstatusAprobacion { get; set; } = "En Espera";
    }
}