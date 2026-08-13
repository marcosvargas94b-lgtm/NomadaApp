using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nomada.API.Data;
using Nomada.Shared.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Nomada.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class LeaderboardController : ControllerBase
    {
        private readonly NomadaDbContext _context;
        private readonly TimeSpan _mexicoOffset = TimeSpan.FromHours(-6);

        public LeaderboardController(NomadaDbContext context)
        {
            _context = context;
        }

        public class RankingResponseDto
        {
            public bool RequiereCapturaManual { get; set; }
            public Guid? ScorePendienteId { get; set; }
            public string? TipoScoreEsperado { get; set; }
            public string? TituloWodPendiente { get; set; }
            public List<ScoreDetalleDto> Resultados { get; set; } = new();
        }

        public class ScoreDetalleDto
        {
            public int WodGeneralId { get; set; }
            public string TituloWod { get; set; } = string.Empty;
            public string NombreCoach { get; set; } = string.Empty;
            public string HoraClase { get; set; } = string.Empty; // <--- HORA EXACTA DEL SCORE

            public Guid UsuarioId { get; set; }
            public string NombreCompleto { get; set; } = string.Empty;
            public string? FotoPerfil { get; set; }
            public string TipoScore { get; set; } = string.Empty;
            public string Categoria { get; set; } = string.Empty;
            public string? TiempoFormato { get; set; }
            public int? TiempoSegundos { get; set; }
            public int? Rondas { get; set; }
            public int? Repeticiones { get; set; }
        }

        [HttpGet("{gymCode}/hoy/{usuarioId}")]
        public async Task<IActionResult> GetRankingHoy(string gymCode, Guid usuarioId)
        {
            DateTime hoyMexico = DateTime.UtcNow.Add(_mexicoOffset).Date;

            var response = new RankingResponseDto();

            // 1. Verificamos si EL USUARIO tiene una captura manual pendiente hoy
            var scorePendiente = await _context.WodScores
                .FirstOrDefaultAsync(w => w.GymCode == gymCode && w.UsuarioId == usuarioId && w.Fecha == hoyMexico && w.EsCapturaManual == true);

            if (scorePendiente != null)
            {
                response.RequiereCapturaManual = true;
                response.ScorePendienteId = scorePendiente.Id;
                response.TipoScoreEsperado = scorePendiente.TipoScore;

                var wodObj = await _context.WodsGenerales.FindAsync(scorePendiente.WodGeneralId);
                response.TituloWodPendiente = wodObj != null ? $"{wodObj.Titulo} (By Coach {wodObj.NombreCoach})" : "WOD Libre";

                return Ok(response);
            }

            // 2. Traemos el Ranking Oficial cruzando directamente WodScores -> HorariosClases
            var scoresHoy = await (from s in _context.WodScores
                                   where s.GymCode == gymCode && s.Fecha == hoyMexico && s.EsCapturaManual == false
                                   join u in _context.Usuarios on s.UsuarioId equals u.Id

                                   // Join con el WOD
                                   join wg in _context.WodsGenerales on s.WodGeneralId equals wg.Id into wgGroup
                                   from wg in wgGroup.DefaultIfEmpty()

                                       // Join directo con el Horario
                                   join h in _context.HorariosClases on s.HorarioId equals h.Id into hGroup
                                   from h in hGroup.DefaultIfEmpty()

                                   select new ScoreDetalleDto
                                   {
                                       WodGeneralId = s.WodGeneralId,
                                       TituloWod = wg != null ? wg.Titulo : "WOD Libre",
                                       NombreCoach = wg != null ? wg.NombreCoach : "",
                                       HoraClase = h != null ? h.HoraTexto : "", // <--- EL HORARIO PERFECTO
                                       UsuarioId = u.Id,
                                       NombreCompleto = $"{u.Nombre} {u.ApellidoPaterno}",
                                       FotoPerfil = u.FotoPerfil,
                                       TipoScore = s.TipoScore,
                                       Categoria = s.Categoria,
                                       TiempoFormato = s.TiempoFormato,
                                       TiempoSegundos = s.TiempoSegundos,
                                       Rondas = s.Rondas,
                                       Repeticiones = s.Repeticiones
                                   }).ToListAsync();

            response.Resultados = scoresHoy;
            return Ok(response);
        }

        public class SubirScoreRequest
        {
            public string? TiempoFormato { get; set; }
            public int? Rondas { get; set; }
            public int? Repeticiones { get; set; }
        }

        [HttpPut("capturar/{scoreId}")]
        public async Task<IActionResult> SubirScoreManual(Guid scoreId, [FromBody] SubirScoreRequest request)
        {
            var score = await _context.WodScores.FindAsync(scoreId);
            if (score == null) return NotFound("Score no encontrado.");

            score.EsCapturaManual = false;

            if (score.TipoScore == "Tiempo" && !string.IsNullOrWhiteSpace(request.TiempoFormato))
            {
                score.TiempoFormato = request.TiempoFormato;
                var parts = request.TiempoFormato.Split(':');
                if (parts.Length == 2 && int.TryParse(parts[0], out int m) && int.TryParse(parts[1], out int s))
                    score.TiempoSegundos = (m * 60) + s;
            }
            else if (score.TipoScore == "Rondas")
            {
                score.Rondas = request.Rondas;
                score.Repeticiones = request.Repeticiones;
            }
            else if (score.TipoScore == "Reps")
            {
                score.Repeticiones = request.Repeticiones;
            }

            await _context.SaveChangesAsync();
            return Ok();
        }
    }
}