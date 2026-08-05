using System;

namespace Nomada.Shared.Models
{
    public class RegistroRequest
    {
        public string Nombre { get; set; } = string.Empty;
        public string ApellidoPaterno { get; set; } = string.Empty;
        public string ApellidoMaterno { get; set; } = string.Empty;
        public DateTime FechaNacimiento { get; set; } = DateTime.Today.AddYears(-18);
        public string Sexo { get; set; } = string.Empty;
        public string Correo { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string ConfirmPassword { get; set; } = string.Empty;
        public string GymCode { get; set; } = string.Empty;
    }
}