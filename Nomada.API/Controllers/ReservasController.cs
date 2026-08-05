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

        // 1. Obtener los 7 días a partir de hoy (No requiere DB)
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

        // 2. Obtener horarios y aforo de un día específico por Gimnasio
        [HttpGet("{gymCode}/horarios/{fechaStr}/{usuarioId}")]
        public async Task<IActionResult> GetHorarios(string gymCode, string fechaStr, Guid usuarioId)
        {
            if (!DateTime.TryParse(fechaStr, out DateTime fecha)) return BadRequest();
            fecha = fecha.Date;

            // Obtener o crear configuración de aforo específica de esta sucursal
            var config = await _context.ConfiguracionBox.FirstOrDefaultAsync(c => c.GymCode == gymCode);
            if (config == null)
            {
                config = new ConfiguracionBox { GymCode = gymCode, AforoMaximo = 20 };
                _context.ConfiguracionBox.Add(config);
                await _context.SaveChangesAsync();
            }

            // Obtener horarios activos de este gimnasio
            var horarios = await _context.HorariosClases
                .Where(h => h.GymCode == gymCode && h.Activo)
                .OrderBy(h => h.HoraOrden)
                .ToListAsync();

            // Obtener reservas de ese día
            var reservasDelDia = await _context.Reservas
                .Where(r => r.GymCode == gymCode && r.FechaReserva.Date == fecha)
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

            // Buscar si ya está agendado en ese horario
            var reservaExistente = await _context.Reservas
                .FirstOrDefaultAsync(r => r.GymCode == request.GymCode && r.UsuarioId == request.UsuarioId && r.HorarioId == request.HorarioId && r.FechaReserva == request.FechaReserva);

            if (reservaExistente != null)
            {
                // Si ya está, lo cancelamos (lo borramos)
                _context.Reservas.Remove(reservaExistente);
                await _context.SaveChangesAsync();
                return Ok(new { Mensaje = "Reserva cancelada", Agendado = false });
            }

            // Si NO está agendado, validamos Aforo
            var config = await _context.ConfiguracionBox.FirstOrDefaultAsync(c => c.GymCode == request.GymCode) ?? new ConfiguracionBox { AforoMaximo = 20 };
            int ocupacion = await _context.Reservas.CountAsync(r => r.GymCode == request.GymCode && r.HorarioId == request.HorarioId && r.FechaReserva == request.FechaReserva);

            if (ocupacion >= config.AforoMaximo)
            {
                return BadRequest("El horario ya alcanzó su máxima capacidad.");
            }

            // Validamos que el usuario no esté en otra clase ese mismo día
            var agendadoOtroHorario = await _context.Reservas.AnyAsync(r => r.GymCode == request.GymCode && r.UsuarioId == request.UsuarioId && r.FechaReserva == request.FechaReserva);
            if (agendadoOtroHorario) return BadRequest("Ya tienes una clase agendada para este día.");

            var nuevaReserva = new Reserva
            {
                GymCode = request.GymCode,
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

        [HttpGet("{gymCode}/admin/config")]
        public async Task<IActionResult> GetConfiguracion(string gymCode)
        {
            var config = await _context.ConfiguracionBox.FirstOrDefaultAsync(c => c.GymCode == gymCode);
            if (config == null) return Ok(new { AforoMaximo = 20 });
            return Ok(new { AforoMaximo = config.AforoMaximo });
        }

        [HttpPut("{gymCode}/admin/config")]
        public async Task<IActionResult> UpdateConfiguracion(string gymCode, [FromBody] ConfiguracionBox nuevaConfig)
        {
            var config = await _context.ConfiguracionBox.FirstOrDefaultAsync(c => c.GymCode == gymCode);
            if (config == null)
            {
                _context.ConfiguracionBox.Add(new ConfiguracionBox { GymCode = gymCode, AforoMaximo = nuevaConfig.AforoMaximo });
            }
            else
            {
                config.AforoMaximo = nuevaConfig.AforoMaximo;
            }
            await _context.SaveChangesAsync();
            return Ok();
        }

        [HttpGet("{gymCode}/admin/horarios")]
        public async Task<IActionResult> GetAllHorarios(string gymCode)
        {
            var horarios = await _context.HorariosClases.Where(h => h.GymCode == gymCode).OrderBy(h => h.HoraOrden).ToListAsync();
            return Ok(horarios);
        }

        [HttpPost("admin/horarios")]
        public async Task<IActionResult> CrearHorario([FromBody] HorarioClase horario)
        {
            horario.Activo = true;
            // El GymCode viene dentro del objeto horario enviado desde el frontend
            _context.HorariosClases.Add(horario);
            await _context.SaveChangesAsync();
            return Ok();
        }

        [HttpPut("admin/horarios/{id}/toggle")]
        public async Task<IActionResult> ToggleHorarioActivo(int id)
        {
            // El Id autoincremental de SQL ya nos da el registro único a editar.
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

            var reservasAsociadas = await _context.Reservas
                .Where(r => r.HorarioId == id)
                .ToListAsync();

            if (reservasAsociadas.Any())
            {
                _context.Reservas.RemoveRange(reservasAsociadas);
            }

            _context.HorariosClases.Remove(horario);
            await _context.SaveChangesAsync();

            return Ok();
        }
    }
}