using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nomada.API.Data;
using Nomada.API.Services;
using Nomada.Shared.Models;
using Nomada.API.Models;
using System;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace Nomada.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PerfilController : ControllerBase
    {
        private readonly NomadaDbContext _context;
        private readonly IBlobStorageService _blobService;

        public PerfilController(NomadaDbContext context, IBlobStorageService blobService)
        {
            _context = context;
            _blobService = blobService;
        }

        [HttpGet("{gymCode}/{id}")]
        public async Task<IActionResult> GetPerfil(string gymCode, Guid id)
        {
            var user = await _context.Usuarios
                .FirstOrDefaultAsync(u => u.Id == id && u.GymCode == gymCode); // CANDADO GYMCODE

            if (user == null) return NotFound();

            // Por seguridad, vaciamos el hash antes de mandarlo al Frontend
            user.PasswordHash = new byte[0];
            user.PasswordSalt = new byte[0];

            return Ok(user);
        }

        [HttpPut("{gymCode}/actualizar")]
        public async Task<IActionResult> ActualizarPerfil(string gymCode, [FromForm] ActualizarPerfilForm request)
        {
            var user = await _context.Usuarios
                .FirstOrDefaultAsync(u => u.Id == request.Id && u.GymCode == gymCode);

            if (user == null) return NotFound();

            // 1. Actualizar Textos
            user.Nombre = request.Nombre;
            user.ApellidoPaterno = request.ApellidoPaterno;
            user.ApellidoMaterno = request.ApellidoMaterno;
            user.FechaNacimiento = request.FechaNacimiento;
            user.FrasePersonal = request.FrasePersonal;
            user.EjercicioFavorito = request.EjercicioFavorito;
            user.EjercicioMenosFavorito = request.EjercicioMenosFavorito;
            user.Peso = request.Peso;
            user.Estatura = request.Estatura;

            // 2. Actualizar Contraseña (Lógica de Hashes)
            if (!string.IsNullOrEmpty(request.PasswordActual) && !string.IsNullOrEmpty(request.NuevaPassword))
            {
                if (!VerificarPasswordHash(request.PasswordActual, user.PasswordHash, user.PasswordSalt))
                {
                    return BadRequest("La contraseña actual es incorrecta.");
                }

                CrearPasswordHash(request.NuevaPassword, out byte[] passwordHash, out byte[] passwordSalt);
                user.PasswordHash = passwordHash;
                user.PasswordSalt = passwordSalt;
            }

            // 3. Subir Fotos a Azure
            if (request.FotoPerfil != null) user.FotoPerfil = await _blobService.SubirImagenUsuarioAsync(request.FotoPerfil);
            if (request.FotoDestacada1 != null) user.FotoDestacada1 = await _blobService.SubirImagenUsuarioAsync(request.FotoDestacada1);
            if (request.FotoDestacada2 != null) user.FotoDestacada2 = await _blobService.SubirImagenUsuarioAsync(request.FotoDestacada2);
            if (request.FotoDestacada3 != null) user.FotoDestacada3 = await _blobService.SubirImagenUsuarioAsync(request.FotoDestacada3);

            await _context.SaveChangesAsync();
            return Ok();
        }

        // ================= METODOS DE ENCRIPTACIÓN =================
        private void CrearPasswordHash(string password, out byte[] passwordHash, out byte[] passwordSalt)
        {
            using (var hmac = new HMACSHA512())
            {
                passwordSalt = hmac.Key;
                passwordHash = hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(password));
            }
        }

        private bool VerificarPasswordHash(string password, byte[] passwordHash, byte[] passwordSalt)
        {
            using (var hmac = new HMACSHA512(passwordSalt))
            {
                var computedHash = hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(password));
                return computedHash.SequenceEqual(passwordHash);
            }
        }
    }
}