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
    public class LobbyController : ControllerBase
    {
        private readonly NomadaDbContext _context;
        private readonly TimeSpan _mexicoOffset = TimeSpan.FromHours(-6);

        public LobbyController(NomadaDbContext context)
        {
            _context = context;
        }

        // ================= PARA EL COACH =================

        [HttpPost("{gymCode}/iniciar-sesion")]
        public async Task<IActionResult> IniciarSesion(string gymCode, [FromBody] dynamic request)
        {
            int horarioId = request.GetProperty("horarioId").GetInt32();
            Guid coachId = request.GetProperty("coachId").GetGuid();
            DateTime fechaHoy = DateTime.UtcNow.Add(_mexicoOffset).Date;

            var sesionActual = await _context.SesionesClases
                .FirstOrDefaultAsync(s => s.GymCode == gymCode && s.HorarioId == horarioId && s.Fecha == fechaHoy);

            if (sesionActual == null)
            {
                string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
                var random = new Random();
                string codigo = new string(Enumerable.Repeat(chars, 5).Select(s => s[random.Next(s.Length)]).ToArray());

                sesionActual = new SesionClase
                {
                    GymCode = gymCode,
                    Fecha = fechaHoy,
                    HorarioId = horarioId,
                    CodigoAcceso = codigo,
                    CoachAperturaId = coachId
                };

                _context.SesionesClases.Add(sesionActual);
                await _context.SaveChangesAsync();
            }

            return Ok(new SesionLobbyDto { SesionId = sesionActual.Id, CodigoAcceso = sesionActual.CodigoAcceso, HorarioId = sesionActual.HorarioId });
        }

        [HttpGet("{gymCode}/sesion/{horarioId}/usuarios")]
        public async Task<IActionResult> GetUsuariosLobby(string gymCode, int horarioId)
        {
            DateTime fechaHoy = DateTime.UtcNow.Add(_mexicoOffset).Date;

            var usuarios = await _context.Reservas
                .Where(r => r.GymCode == gymCode && r.FechaReserva.Date == fechaHoy && r.HorarioId == horarioId)
                .Join(_context.Usuarios, r => r.UsuarioId, u => u.Id, (r, u) => new UsuarioLobbyDto
                {
                    ReservaId = r.Id,
                    UsuarioId = u.Id,
                    Nombre = u.Nombre,
                    ApellidoPaterno = u.ApellidoPaterno,
                    FotoPerfil = u.FotoPerfil,
                    MetodoIngreso = r.MetodoIngreso
                })
                .ToListAsync();

            return Ok(usuarios);
        }

        [HttpPost("{gymCode}/agregar-manual")]
        public async Task<IActionResult> AgregarManual(string gymCode, [FromBody] dynamic request)
        {
            Guid usuarioId = request.GetProperty("usuarioId").GetGuid();
            int horarioId = request.GetProperty("horarioId").GetInt32();
            DateTime fechaHoy = DateTime.UtcNow.Add(_mexicoOffset).Date;

            bool yaReservado = await _context.Reservas.AnyAsync(r => r.UsuarioId == usuarioId && r.FechaReserva.Date == fechaHoy && r.HorarioId == horarioId);
            if (yaReservado) return BadRequest("El atleta ya está registrado en la clase.");

            var nuevaReserva = new Reserva
            {
                GymCode = gymCode,
                UsuarioId = usuarioId,
                FechaReserva = fechaHoy,
                HorarioId = horarioId,
                MetodoIngreso = "Manual",
                FechaOperacion = DateTime.UtcNow
            };

            _context.Reservas.Add(nuevaReserva);
            await _context.SaveChangesAsync();
            return Ok();
        }

        // ================= PARA EL ATLETA =================

        [HttpPost("{gymCode}/unirse-codigo")]
        public async Task<IActionResult> UnirseConCodigo(string gymCode, [FromBody] dynamic request)
        {
            Guid usuarioId = request.GetProperty("usuarioId").GetGuid();
            string codigo = request.GetProperty("codigo").GetString();
            DateTime fechaHoy = DateTime.UtcNow.Add(_mexicoOffset).Date;

            var sesion = await _context.SesionesClases
                .FirstOrDefaultAsync(s => s.GymCode == gymCode && s.CodigoAcceso.ToUpper() == codigo.ToUpper() && s.Fecha == fechaHoy);

            if (sesion == null) return BadRequest("Código inválido o la clase ya terminó.");

            var config = await _context.ConfiguracionBox.FirstOrDefaultAsync(c => c.GymCode == gymCode) ?? new ConfiguracionBox { AforoMaximo = 20 };

            int ocupacionActual = await _context.Reservas
                .CountAsync(r => r.GymCode == gymCode && r.HorarioId == sesion.HorarioId && r.FechaReserva.Date == fechaHoy);

            if (ocupacionActual >= config.AforoMaximo)
            {
                return BadRequest("La clase ya ha alcanzado su aforo máximo.");
            }

            var horario = await _context.HorariosClases.FindAsync(sesion.HorarioId);
            string horaTexto = horario != null ? horario.HoraTexto : "hoy";

            bool yaReservado = await _context.Reservas.AnyAsync(r => r.UsuarioId == usuarioId && r.FechaReserva.Date == fechaHoy && r.HorarioId == sesion.HorarioId);

            if (yaReservado)
            {
                return BadRequest("Ya estás anotado en esta clase.");
            }

            var nuevaReserva = new Reserva
            {
                GymCode = gymCode,
                UsuarioId = usuarioId,
                FechaReserva = fechaHoy,
                HorarioId = sesion.HorarioId,
                MetodoIngreso = "Codigo",
                FechaOperacion = DateTime.UtcNow
            };

            _context.Reservas.Add(nuevaReserva);
            await _context.SaveChangesAsync();

            return Ok(new { mensaje = $"¡Listo! Has sido anotado en la lista para la clase de las {horaTexto}." });
        }

        public class ProcesarClaseRequest
        {
            public string GymCode { get; set; } = string.Empty;
            public int HorarioId { get; set; }
            public int WodGeneralId { get; set; }
            public string TipoScoreGlobal { get; set; } = string.Empty;
            public bool CapturaManualGlobal { get; set; }
            public List<AtletaProcesarDto> Scores { get; set; } = new();
        }

        public class AtletaProcesarDto
        {
            public Guid UsuarioId { get; set; }
            public string MetodoIngreso { get; set; } = string.Empty;
            public string Categoria { get; set; } = string.Empty;
            public string? TiempoFormato { get; set; }
            public int? Rondas { get; set; }
            public int? Repeticiones { get; set; }
        }

        [HttpPost("{gymCode}/procesar-clase")]
        public async Task<IActionResult> ProcesarClaseMasiva(string gymCode, [FromBody] ProcesarClaseRequest request)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                DateTime fechaActualMexico = DateTime.UtcNow.Add(_mexicoOffset);

                foreach (var atleta in request.Scores)
                {
                    // ==========================================
                    // 1. REGISTRO DE ASISTENCIA Y FATIGA (CORREGIDO)
                    // ==========================================
                    var asistencia = new Asistencia
                    {
                        GymCode = gymCode,
                        UsuarioId = atleta.UsuarioId,
                        HorarioId = request.HorarioId,
                        FechaHora = fechaActualMexico,
                        MetodoRegistro = atleta.MetodoIngreso,

                        WodGeneralId = request.WodGeneralId, // <--- Vinculamos el WOD para la IA
                        RPE = null // <--- Inicia en nulo para pedirlo hoy (y si es ayer se calculará automático)
                    };
                    _context.Asistencias.Add(asistencia);

                    // ==========================================
                    // 2. DESCUENTO DE PAQUETE DE CLASES
                    // ==========================================
                    var subActiva = await _context.Suscripciones
                        .FirstOrDefaultAsync(s => s.GymCode == gymCode && s.UsuarioId == atleta.UsuarioId && s.Activa);

                    if (subActiva != null && subActiva.TipoSuscripcion == "PaqueteClases")
                    {
                        if (subActiva.ClasesRestantes > 0)
                        {
                            subActiva.ClasesRestantes--;

                            if (subActiva.ClasesRestantes == 0)
                            {
                                subActiva.Activa = false;
                                var usuario = await _context.Usuarios.FindAsync(atleta.UsuarioId);
                                if (usuario != null) usuario.EstatusAprobacion = "Baja Temporal";
                            }
                        }
                    }

                    // ==========================================
                    // 3. REGISTRO DEL SCORE (Solo si no es Asistencia pura)
                    // ==========================================
                    if (request.WodGeneralId > 0)
                    {
                        bool esPuroAsistencia = request.TipoScoreGlobal == "Asistencia";

                        if (!esPuroAsistencia)
                        {
                            int? tiempoSegundosSql = null;
                            if (!request.CapturaManualGlobal && request.TipoScoreGlobal == "Tiempo" && !string.IsNullOrWhiteSpace(atleta.TiempoFormato))
                            {
                                var parts = atleta.TiempoFormato.Split(':');
                                if (parts.Length == 2 && int.TryParse(parts[0], out int m) && int.TryParse(parts[1], out int s))
                                    tiempoSegundosSql = (m * 60) + s;
                            }

                            var score = new WodScore
                            {
                                GymCode = gymCode,
                                WodGeneralId = request.WodGeneralId,
                                HorarioId = request.HorarioId,
                                UsuarioId = atleta.UsuarioId,
                                Fecha = fechaActualMexico.Date,
                                TipoScore = request.TipoScoreGlobal,
                                Categoria = atleta.Categoria,
                                EsCapturaManual = request.CapturaManualGlobal,
                                TiempoFormato = (!request.CapturaManualGlobal && request.TipoScoreGlobal == "Tiempo") ? atleta.TiempoFormato : null,
                                TiempoSegundos = tiempoSegundosSql,
                                Rondas = (!request.CapturaManualGlobal && request.TipoScoreGlobal == "Rondas") ? atleta.Rondas : null,
                                Repeticiones = (!request.CapturaManualGlobal) ? atleta.Repeticiones : null
                            };
                            _context.WodScores.Add(score);
                        }
                    }
                }

                // 4. LIMPIEZA DE RESERVAS
                var idsUsuariosList = request.Scores.Select(x => x.UsuarioId).ToList();
                var reservasAEliminar = await _context.Reservas
                    .Where(r => r.GymCode == gymCode && r.HorarioId == request.HorarioId && r.FechaReserva.Date == fechaActualMexico.Date && idsUsuariosList.Contains(r.UsuarioId))
                    .ToListAsync();

                if (reservasAEliminar.Any()) _context.Reservas.RemoveRange(reservasAEliminar);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return Ok();
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, ex.Message);
            }
        }
    }
}