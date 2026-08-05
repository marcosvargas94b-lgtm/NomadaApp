using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nomada.API.Data;
using Nomada.Shared.Entities;
using Nomada.Shared.Models;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Nomada.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AvisosController : ControllerBase
    {
        private readonly NomadaDbContext _context;

        public AvisosController(NomadaDbContext context)
        {
            _context = context;
        }

        // ================= PARA TODOS (Leer Pizarrón) =================
        [HttpGet("{gymCode}/pizarron")]
        public async Task<IActionResult> GetAvisosActivos(string gymCode)
        {
            var fechaActual = DateTime.UtcNow;

            var avisos = await _context.AvisosBox
                .Where(a => a.GymCode == gymCode && a.FechaVencimiento >= fechaActual)
                .OrderByDescending(a => a.FechaPublicacion)
                .Select(a => new AvisoBoxDto
                {
                    Id = a.Id,
                    Titulo = a.Titulo,
                    Mensaje = a.Mensaje,
                    NombreCoach = a.NombreCoach,
                    FechaVencimiento = a.FechaVencimiento
                })
                .ToListAsync();

            return Ok(avisos);
        }

        // ================= PARA COACHES (Gestión) =================
        [HttpGet("{gymCode}/historial")]
        public async Task<IActionResult> GetTodosLosAvisos(string gymCode)
        {
            var avisos = await _context.AvisosBox
                .Where(a => a.GymCode == gymCode)
                .OrderByDescending(a => a.FechaPublicacion)
                .Select(a => new AvisoBoxDto
                {
                    Id = a.Id,
                    Titulo = a.Titulo,
                    Mensaje = a.Mensaje,
                    NombreCoach = a.NombreCoach,
                    FechaVencimiento = a.FechaVencimiento
                })
                .ToListAsync();

            return Ok(avisos);
        }

        [HttpPost]
        public async Task<IActionResult> CrearAviso([FromBody] CrearAvisoRequest request)
        {
            var coach = await _context.Usuarios.FindAsync(request.CoachId);
            if (coach == null) return BadRequest("Coach no encontrado.");

            DateTime fechaVencimientoObj;

            if (request.DiasVigencia == 1)
            {
                // "Solo hoy": Muere a la medianoche (00:00) del día actual en hora de México (UTC-6)
                DateTime horaMexico = DateTime.UtcNow.AddHours(-6);
                DateTime medianocheMexico = horaMexico.Date.AddDays(1); // 00:00 del día de mañana en MX

                // Lo devolvemos a UTC para que la base de datos lo guarde en el formato universal correcto
                fechaVencimientoObj = medianocheMexico.AddHours(6);
            }
            else
            {
                // Las demás opciones (3, 7, 15, 30) se quedan igual (sumando bloques de 24 horas)
                fechaVencimientoObj = DateTime.UtcNow.AddDays(request.DiasVigencia);
            }

            var nuevoAviso = new AvisoBox
            {
                GymCode = request.GymCode,
                Titulo = request.Titulo,
                Mensaje = request.Mensaje,
                CoachId = request.CoachId,
                NombreCoach = coach.Nombre ?? "Coach",
                FechaPublicacion = DateTime.UtcNow,
                FechaVencimiento = fechaVencimientoObj
            };

            _context.AvisosBox.Add(nuevoAviso);

            // ==========================================
            // ENVIAR NOTIFICACIÓN A TODA LA TRIBU
            // ==========================================
            var usuariosIds = await _context.Usuarios
                .Where(u => u.GymCode == request.GymCode && u.EstatusAprobacion == "Aprobado")
                .Select(u => u.Id)
                .ToListAsync();

            var notificaciones = usuariosIds.Select(uId => new Notificacion
            {
                GymCode = request.GymCode,
                UsuarioId = uId,
                Mensaje = $"📢 Aviso del Box: {request.Titulo}",
                Tipo = "AvisoBox",
                RutaNavegacion = "/acciones", // Los manda al Pizarrón
                Leida = false,
                FechaCreacion = DateTime.UtcNow
            });

            _context.Notificaciones.AddRange(notificaciones);
            // ==========================================

            await _context.SaveChangesAsync();
            return Ok();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> EliminarAviso(int id)
        {
            var aviso = await _context.AvisosBox.FindAsync(id);
            if (aviso == null) return NotFound();

            _context.AvisosBox.Remove(aviso);
            await _context.SaveChangesAsync();
            return Ok();
        }
    }
}