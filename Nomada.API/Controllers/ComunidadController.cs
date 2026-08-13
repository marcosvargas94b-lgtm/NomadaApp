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
    public class ComunidadController : ControllerBase
    {
        private readonly NomadaDbContext _context;
        // Hora de México Central (UTC-6)
        private readonly TimeSpan _mexicoOffset = TimeSpan.FromHours(-6);

        public ComunidadController(NomadaDbContext context)
        {
            _context = context;
        }

        // 1. Obtener el Feed (Posts de las últimas 24 hrs de este GIMNASIO)
        [HttpGet("{gymCode}/feed/{usuarioId}")]
        public async Task<IActionResult> GetFeed(string gymCode, Guid usuarioId)
        {
            var hace24Horas = DateTime.UtcNow.AddHours(-24);

            var posts = await _context.Posts
                .Where(p => p.GymCode == gymCode && p.FechaCreacion >= hace24Horas)
                .OrderByDescending(p => p.FechaCreacion)
                .Join(_context.Usuarios, p => p.UsuarioId, u => u.Id, (p, u) => new { p, u })
                .Select(x => new PostFeedDto
                {
                    Id = x.p.Id,
                    UsuarioId = x.p.UsuarioId,
                    NombreAutor = x.u.Nombre + " " + x.u.ApellidoPaterno,
                    Iniciales = (x.u.Nombre.Substring(0, 1) + x.u.ApellidoPaterno.Substring(0, 1)).ToUpper(),
                    Texto = x.p.Texto,
                    MediaUrl = x.p.MediaUrl,
                    EsVideo = x.p.EsVideo,
                    EsMio = x.p.UsuarioId == usuarioId,
                    TiempoTranscurrido = CalcularTiempo(x.p.FechaCreacion),
                    CantidadLikes = _context.Likes.Count(l => l.PostId == x.p.Id),
                    YoLeDiLike = _context.Likes.Any(l => l.PostId == x.p.Id && l.UsuarioId == usuarioId)
                })
                .ToListAsync();

            return Ok(posts);
        }

        // 2. Verificar si el usuario ya publicó HOY 
        [HttpGet("{gymCode}/ya-publico-hoy/{usuarioId}")]
        public async Task<IActionResult> YaPublicoHoy(string gymCode, Guid usuarioId)
        {
            DateTime horaMexicoActual = DateTime.UtcNow.Add(_mexicoOffset);

            var ultimoPost = await _context.Posts
                .Where(p => p.GymCode == gymCode && p.UsuarioId == usuarioId)
                .OrderByDescending(p => p.FechaCreacion)
                .FirstOrDefaultAsync();

            if (ultimoPost == null) return Ok(false);

            DateTime horaMexicoPost = ultimoPost.FechaCreacion.Add(_mexicoOffset);

            bool publicoHoy = horaMexicoActual.Date == horaMexicoPost.Date;

            return Ok(publicoHoy);
        }

        // 3. Crear una nueva publicación (Sellada con el GymCode)
        [HttpPost("publicar")]
        public async Task<IActionResult> Publicar([FromBody] CrearPostRequest request)
        {
            var post = new Post
            {
                GymCode = request.GymCode, // <--- SEPARACIÓN POR SUCURSAL
                UsuarioId = request.UsuarioId,
                Texto = request.Texto,
                EsVideo = request.EsVideo,
                MediaUrl = request.MediaBase64,
                FechaCreacion = DateTime.UtcNow
            };

            _context.Posts.Add(post);
            await _context.SaveChangesAsync();
            return Ok();
        }

        // 4. Dar Like a una publicación
        [HttpPost("like/{postId}/{usuarioId}")]
        public async Task<IActionResult> DarLike(int postId, Guid usuarioId)
        {
            var existe = await _context.Likes.AnyAsync(l => l.PostId == postId && l.UsuarioId == usuarioId);
            if (existe) return BadRequest("Ya diste like a esta publicación");

            // Buscamos el post para extraer su GymCode de forma inteligente
            var post = await _context.Posts.FindAsync(postId);
            if (post == null) return NotFound();

            var like = new Like
            {
                GymCode = post.GymCode, // Hereda el GymCode del Post
                PostId = postId,
                UsuarioId = usuarioId,
                FechaCreacion = DateTime.UtcNow
            };
            _context.Likes.Add(like);

            // Notificación sellada con el mismo GymCode
            if (post.UsuarioId != usuarioId)
            {
                _context.Notificaciones.Add(new Notificacion
                {
                    GymCode = post.GymCode,
                    UsuarioId = post.UsuarioId,
                    Mensaje = "❤️ Alguien ha reaccionado a tu entrenamiento.",
                    Leida = false,
                    FechaCreacion = DateTime.UtcNow
                });
            }

            await _context.SaveChangesAsync();
            return Ok();
        }

        // 5. Borrar Post (Sin cambios, usa ID único)
        [HttpDelete("borrar/{postId}")]
        public async Task<IActionResult> BorrarPost(int postId)
        {
            var post = await _context.Posts.FindAsync(postId);
            if (post == null) return NotFound();

            _context.Posts.Remove(post);
            await _context.SaveChangesAsync();
            return Ok();
        }

        // 6. Obtener Notificaciones
        [HttpGet("{gymCode}/notificaciones/{usuarioId}")]
        public async Task<IActionResult> GetNotificaciones(string gymCode, Guid usuarioId)
        {
            var notis = await _context.Notificaciones
                .Where(n => n.GymCode == gymCode && n.UsuarioId == usuarioId)
                .OrderByDescending(n => n.FechaCreacion)
                .Take(20)
                .Select(n => new NotificacionDto
                {
                    Id = n.Id,
                    Mensaje = n.Mensaje,
                    Leida = n.Leida,
                    TiempoTranscurrido = CalcularTiempo(n.FechaCreacion)
                })
                .ToListAsync();

            return Ok(notis);
        }

        // Helper para el tiempo transcurrido
        private static string CalcularTiempo(DateTime fechaUtc)
        {
            var span = DateTime.UtcNow - fechaUtc;
            if (span.TotalMinutes < 60) return $"Hace {(int)span.TotalMinutes} min";
            if (span.TotalHours < 24) return $"Hace {(int)span.TotalHours} hrs";
            return "Ayer";
        }

        // 7. Obtener Ranking de Likes (Separado por Sucursal)
        [HttpGet("{gymCode}/ranking/{anio}")]
        public async Task<IActionResult> GetRanking(string gymCode, int anio)
        {
            var ranking = await _context.Usuarios
                .Where(u => u.GymCode == gymCode && (u.EstatusAprobacion == "Aprobado" || u.RolId == 1 || u.RolId == 2))
                .Select(u => new RankingUsuarioDto
                {
                    Id = u.Id,
                    Nombre = u.Nombre,
                    ApellidoPaterno = u.ApellidoPaterno,
                    Iniciales = (u.Nombre.Substring(0, 1) + u.ApellidoPaterno.Substring(0, 1)).ToUpper(),
                    FotoPerfil = u.FotoPerfil, // <--- SE AGREGA ESTA LÍNEA PARA QUE SQL SAQUE LA FOTO
                    // Cuenta los likes SOLO de los posts de esta sucursal
                    TotalLikes = _context.Likes.Count(l => l.GymCode == gymCode &&
                        _context.Posts.Any(p => p.Id == l.PostId && p.UsuarioId == u.Id)
                        && l.FechaCreacion.Year == anio)
                })
                .Where(r => r.TotalLikes > 0)
                .OrderByDescending(r => r.TotalLikes)
                .ToListAsync();

            int pos = 1;
            foreach (var user in ranking)
            {
                user.Posicion = pos++;
            }

            return Ok(ranking);
        }

        // 8. Obtener Ranking de ASISTENCIAS por Año (Separado por Sucursal)
        [HttpGet("{gymCode}/ranking-asistencias/{anio}")]
        public async Task<IActionResult> GetRankingAsistencias(string gymCode, int anio)
        {
            var ranking = await _context.Asistencias
                .Where(a => a.GymCode == gymCode && a.FechaHora.Year == anio)
                .GroupBy(a => a.UsuarioId)
                .Select(g => new { UsuarioId = g.Key, Total = g.Count() })
                .Join(_context.Usuarios.Where(u => u.EstatusAprobacion == "Aprobado" || u.RolId == 1 || u.RolId == 2),
                      a => a.UsuarioId,
                      u => u.Id,
                      (a, u) => new RankingAsistenciaDto
                      {
                          Id = u.Id,
                          Nombre = u.Nombre,
                          ApellidoPaterno = u.ApellidoPaterno,
                          Iniciales = (u.Nombre.Substring(0, 1) + u.ApellidoPaterno.Substring(0, 1)).ToUpper(),
                          FotoPerfil = u.FotoPerfil,
                          TotalAsistencias = a.Total
                      })
                .OrderByDescending(r => r.TotalAsistencias)
                .ToListAsync();

            int pos = 1;
            foreach (var user in ranking)
            {
                user.Posicion = pos++;
            }

            return Ok(ranking);
        }
    }
}