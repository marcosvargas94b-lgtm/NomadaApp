using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nomada.API.Data;
using Nomada.API.Helpers;
using Nomada.Shared.Models;
using System.Threading.Tasks;

namespace Nomada.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly NomadaDbContext _context;

        public AuthController(NomadaDbContext context)
        {
            _context = context;
        }

        [HttpPost("registro")]
        public async Task<IActionResult> Registrar(RegistroRequest request)
        {
            // 1. Validar si el correo ya existe
            if (await _context.Usuarios.AnyAsync(u => u.Correo == request.Correo))
            {
                return BadRequest("El correo ya está registrado.");
            }

            // 2. Crear el Hash de la contraseña
            PasswordHelper.CrearPasswordHash(request.Password, out byte[] passwordHash, out byte[] passwordSalt);

            // 3. Ensamblar el nuevo Usuario
            var nuevoUsuario = new Usuario
            {
                Nombre = request.Nombre,
                ApellidoPaterno = request.ApellidoPaterno,
                ApellidoMaterno = request.ApellidoMaterno,
                FechaNacimiento = request.FechaNacimiento,
                Sexo = request.Sexo,
                Correo = request.Correo,
                PasswordHash = passwordHash,
                PasswordSalt = passwordSalt,
                RolId = 3, // 3 = Atleta (Por defecto todos nacen como atletas)
                EstatusAprobacion = "En Espera",
                Activo = true
            };

            // 4. Guardar en la base de datos
            _context.Usuarios.Add(nuevoUsuario);
            await _context.SaveChangesAsync();

            return Ok(new { mensaje = "Usuario creado exitosamente. En espera de aprobación." });
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login(LoginRequest request)
        {
            // 1. Buscar al usuario en la base de datos
            var usuario = await _context.Usuarios.FirstOrDefaultAsync(u => u.Correo == request.Correo);

            if (usuario == null)
            {
                return BadRequest("Credenciales incorrectas.");
            }

            // 2. Verificar la contraseña usando tu Helper
            if (!PasswordHelper.VerificarPasswordHash(request.Password, usuario.PasswordHash, usuario.PasswordSalt))
            {
                return BadRequest("Credenciales incorrectas.");
            }

            // 3. El Candado: Verificar el estatus
            if (usuario.EstatusAprobacion == "En Espera")
            {
                return BadRequest("Tu cuenta aún está en lista de espera.");
            }

            if (usuario.EstatusAprobacion == "Rechazado")
            {
                return BadRequest("Tu solicitud ha sido rechazada. Contacta a un administrador.");
            }

            if (!usuario.Activo)
            {
                return BadRequest("Tu cuenta está desactivada.");
            }

            // 4. Si pasa todos los filtros, le damos acceso.
            // Por ahora devolvemos sus datos básicos (Más adelante implementaremos tokens JWT de alta seguridad si lo requieres)
            return Ok(new
            {
                mensaje = "Bienvenido",
                id = usuario.Id,
                nombre = usuario.Nombre,
                rolId = usuario.RolId
            });
        }
    }
}