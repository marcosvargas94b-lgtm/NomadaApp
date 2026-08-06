using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nomada.API.Data;
using Nomada.API.Helpers;
using Nomada.Shared.Entities; // Asegúrate de tener esta referencia para 'Usuario'
using Nomada.Shared.Models;
using System;
using System.Linq;
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
            // 1. Validar si el correo ya existe EN ESE GIMNASIO ESPECÍFICO
            if (await _context.Usuarios.AnyAsync(u => u.Correo == request.Correo && u.GymCode == request.GymCode))
            {
                return BadRequest("El correo ya está registrado en esta sucursal.");
            }

            // 2. Crear el Hash de la contraseña
            PasswordHelper.CrearPasswordHash(request.Password, out byte[] passwordHash, out byte[] passwordSalt);

            // 3. Ensamblar el nuevo Usuario
            var nuevoUsuario = new Usuario
            {
                GymCode = request.GymCode, // <--- ANCLAMOS AL USUARIO A SU GYM
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
            // 1. Buscar al usuario en la base de datos (Filtrado por Correo Y GymCode)
            var usuario = await _context.Usuarios.FirstOrDefaultAsync(u => u.Correo == request.Correo && u.GymCode == request.GymCode);

            if (usuario == null)
            {
                // MENSAJE ESPECÍFICO DE CORREO INEXISTENTE
                return BadRequest("El correo ingresado no está registrado.");
            }

            // 2. Verificar la contraseña usando tu Helper
            if (!PasswordHelper.VerificarPasswordHash(request.Password, usuario.PasswordHash, usuario.PasswordSalt))
            {
                // MENSAJE ESPECÍFICO DE CONTRASEÑA INCORRECTA
                return BadRequest("La contraseña es incorrecta.");
            }

            // 3. El Candado: Verificar el estatus
            if (usuario.EstatusAprobacion == "En Espera")
            {
                return BadRequest("Tu cuenta aún está en lista de espera.");
            }

            if (usuario.EstatusAprobacion == "Baja Temporal")
            {
                return BadRequest("Tu cuenta ha sido suspendida temporalmente por falta de pago. Por favor, acércate a recepción.");
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
            return Ok(new
            {
                mensaje = "Bienvenido",
                id = usuario.Id,
                nombre = usuario.Nombre,
                rolId = usuario.RolId,
                gymCode = usuario.GymCode
            });
        }

        [HttpGet("estatus/{usuarioId}")]
        public async Task<IActionResult> GetEstatus(Guid usuarioId)
        {
            // El ID es un GUID único global, pero igual es buena práctica mantener la consulta limpia
            var estatus = await _context.Usuarios
                .Where(u => u.Id == usuarioId)
                .Select(u => u.EstatusAprobacion)
                .FirstOrDefaultAsync();

            return Ok(new { Estatus = estatus ?? "Baja" });
        }
    }
}