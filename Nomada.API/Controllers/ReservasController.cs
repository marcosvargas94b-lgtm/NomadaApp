using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nomada.API.Data;
using Nomada.Shared.Entities;
using Nomada.Shared.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Nomada.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ReservasController : ControllerBase
    {
        private readonly NomadaDbContext _context;
        private readonly TimeSpan _mexicoOffset = TimeSpan.FromHours(-6);

        public ReservasController(NomadaDbContext context)
        {
            _context = context;
        }

        // 1. Obtener los 7 días a partir de hoy
        [HttpGet("dias-disponibles")]
        public IActionResult GetDiasDisponibles()
        {
            DateTime hoyMexico = DateTime.UtcNow.Add(_mexicoOffset).Date;
            var dias = new List<DiaReservaDto>();
            string[] nombresDias = { "DOM", "LUN", "MAR", "MIÉ", "JUE", "VIE", "SÁB" };

            for (int i = 0; i < 7; i++)
            {
                var fecha = hoyMexico.AddDays(i);
                dias.Add(new DiaReservaDto
                {
                    Fecha = fecha,
                    DiaSemana = nombresDias[(int)fecha.DayOfWeek],
                    NumeroDia = fecha.ToString("dd")
                });
            }
            return Ok(dias);
        }

        // 2. Obtener horarios y aforo de un día específico
        [HttpGet("horarios/{fechaStr}/{usuarioId}")]
        public async Task<IActionResult> GetHorarios(string fechaStr, Guid usuarioId)
        {
            if (!DateTime.TryParse(fechaStr, out DateTime fecha)) return BadRequest();
            fecha = fecha.Date;

            // Obtener o crear configuración de aforo
            var config = await _context.ConfiguracionBox.FirstOrDefaultAsync();
            if (config == null)
            {
                config = new ConfiguracionBox { AforoMaximo = 20 };
                _context.ConfiguracionBox.Add(config);
                await _context.SaveChangesAsync();
            }

            // Obtener horarios activos
            var horarios = await _context.HorariosClases
                .Where(h => h.Activo)
                .OrderBy(h => h.HoraOrden)
                .ToListAsync();

            // Obtener reservas de ese día
            var reservasDelDia = await _context.Reservas
                .Where(r => r.FechaReserva.Date == fecha)
                .Join(_context.Usuarios, r => r.UsuarioId, u => u.Id, (r, u) => new { r, u })
                .ToListAsync();

            var resultado = new List<HorarioDisponibleDto>();

            foreach (var h in horarios)
            {
                var reservasEsteHorario = reservasDelDia.Where(x => x.r.HorarioId == h.Id).ToList();

                var dto = new HorarioDisponibleDto
                {
                    HorarioId = h.Id,
                    HoraTexto = h.HoraTexto,
                    AforoMaximo = config.AforoMaximo,
                    OcupacionActual = reservasEsteHorario.Count,
                    YoEstoyAgendado = reservasEsteHorario.Any(x => x.r.UsuarioId == usuarioId),
                    AtletasAgendados = reservasEsteHorario.Select(x => new AtletaAgendadoDto
                    {
                        UsuarioId = x.u.Id,
                        NombreCompleto = $"{x.u.Nombre} {x.u.ApellidoPaterno}",
                        Iniciales = (x.u.Nombre.Substring(0, 1) + x.u.ApellidoPaterno.Substring(0, 1)).ToUpper()
                    }).ToList()
                };
                resultado.Add(dto);
            }

            return Ok(resultado);
        }

        // 3. Agendar o Cancelar (Toggle)
        [HttpPost("agendar")]
        public async Task<IActionResult> ToggleReserva([FromBody] HacerReservaRequest request)
        {
            request.FechaReserva = request.FechaReserva.Date;

            // Buscar si ya está agendado
            var reservaExistente = await _context.Reservas
                .FirstOrDefaultAsync(r => r.UsuarioId == request.UsuarioId && r.HorarioId == request.HorarioId && r.FechaReserva == request.FechaReserva);

            if (reservaExistente != null)
            {
                // Si ya está, lo cancelamos (lo borramos)
                _context.Reservas.Remove(reservaExistente);
                await _context.SaveChangesAsync();
                return Ok(new { Mensaje = "Reserva cancelada", Agendado = false });
            }

            // Si NO está agendado, validamos Aforo
            var config = await _context.ConfiguracionBox.FirstOrDefaultAsync() ?? new ConfiguracionBox { AforoMaximo = 20 };
            int ocupacion = await _context.Reservas.CountAsync(r => r.HorarioId == request.HorarioId && r.FechaReserva == request.FechaReserva);

            if (ocupacion >= config.AforoMaximo)
            {
                return BadRequest("El horario ya alcanzó su máxima capacidad.");
            }

            // Validamos que el usuario no esté en otra clase ese mismo día (Regla de negocio opcional)
            var agendadoOtroHorario = await _context.Reservas.AnyAsync(r => r.UsuarioId == request.UsuarioId && r.FechaReserva == request.FechaReserva);
            if (agendadoOtroHorario) return BadRequest("Ya tienes una clase agendada para este día.");

            var nuevaReserva = new Reserva
            {
                UsuarioId = request.UsuarioId,
                HorarioId = request.HorarioId,
                FechaReserva = request.FechaReserva,
                FechaOperacion = DateTime.UtcNow
            };

            _context.Reservas.Add(nuevaReserva);
            await _context.SaveChangesAsync();
            return Ok(new { Mensaje = "Agendado con éxito", Agendado = true });
        }

        // ================= MÓDULO ADMINISTRATIVO (COACH/ADMIN) =================

        [HttpGet("config")]
        public async Task<IActionResult> GetConfiguracion()
        {
            var config = await _context.ConfiguracionBox.FirstOrDefaultAsync();
            if (config == null) return Ok(new { AforoMaximo = 20 });
            return Ok(new { AforoMaximo = config.AforoMaximo });
        }

        [HttpPut("config")]
        public async Task<IActionResult> UpdateConfiguracion([FromBody] ConfiguracionBox nuevaConfig)
        {
            var config = await _context.ConfiguracionBox.FirstOrDefaultAsync();
            if (config == null)
            {
                _context.ConfiguracionBox.Add(new ConfiguracionBox { AforoMaximo = nuevaConfig.AforoMaximo });
            }
            else
            {
                config.AforoMaximo = nuevaConfig.AforoMaximo;
            }
            await _context.SaveChangesAsync();
            return Ok();
        }

        [HttpGet("admin/horarios")]
        public async Task<IActionResult> GetAllHorarios()
        {
            var horarios = await _context.HorariosClases.OrderBy(h => h.HoraOrden).ToListAsync();
            return Ok(horarios);
        }

        [HttpPost("admin/horarios")]
        public async Task<IActionResult> CrearHorario([FromBody] HorarioClase horario)
        {
            horario.Activo = true;
            _context.HorariosClases.Add(horario);
            await _context.SaveChangesAsync();
            return Ok();
        }

        [HttpPut("admin/horarios/{id}/toggle")]
        public async Task<IActionResult> ToggleHorarioActivo(int id)
        {
            var horario = await _context.HorariosClases.FindAsync(id);
            if (horario == null) return NotFound();

            horario.Activo = !horario.Activo;
            await _context.SaveChangesAsync();
            return Ok();
        }

        [HttpDelete("admin/horarios/{id}")]
        public async Task<IActionResult> EliminarHorario(int id)
        {
            var horario = await _context.HorariosClases.FindAsync(id);
            if (horario == null) return NotFound();

            // 1. Buscamos y CARGAMOS (ToListAsync) todas las reservas huérfanas de este horario
            var reservasAsociadas = await _context.Reservas
                .Where(r => r.HorarioId == id)
                .ToListAsync();

            // 2. Si hay reservas, las destruimos primero
            if (reservasAsociadas.Any())
            {
                _context.Reservas.RemoveRange(reservasAsociadas);
            }

            // 3. Ahora que el horario está limpio, lo borramos con seguridad
            _context.HorariosClases.Remove(horario);
            await _context.SaveChangesAsync();

            return Ok();
        }
    }
}