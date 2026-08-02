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
    public class ComunidadController : ControllerBase
    {
        private readonly NomadaDbContext _context;
        // Hora de México Central (UTC-6)
        private readonly TimeSpan _mexicoOffset = TimeSpan.FromHours(-6);

        public ComunidadController(NomadaDbContext context)
        {
            _context = context;
        }

        // 1. Obtener el Feed (Posts de las últimas 24 hrs)
        [HttpGet("feed/{usuarioId}")]
        public async Task<IActionResult> GetFeed(Guid usuarioId)
        {
            var hace24Horas = DateTime.UtcNow.AddHours(-24);

            // Traemos los posts con sus autores y likes
            var posts = await _context.Posts
                .Where(p => p.FechaCreacion >= hace24Horas)
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
                    // Lógica para mostrar "Hace 2 hrs" (Calculado localmente en C#)
                    TiempoTranscurrido = CalcularTiempo(x.p.FechaCreacion),
                    CantidadLikes = _context.Likes.Count(l => l.PostId == x.p.Id),
                    YoLeDiLike = _context.Likes.Any(l => l.PostId == x.p.Id && l.UsuarioId == usuarioId)
                })
                .ToListAsync();

            return Ok(posts);
        }

        // 2. Verificar si el usuario ya publicó HOY (En horario de México)
        [HttpGet("ya-publico-hoy/{usuarioId}")]
        public async Task<IActionResult> YaPublicoHoy(Guid usuarioId)
        {
            DateTime horaMexicoActual = DateTime.UtcNow.Add(_mexicoOffset);

            var ultimoPost = await _context.Posts
                .Where(p => p.UsuarioId == usuarioId)
                .OrderByDescending(p => p.FechaCreacion)
                .FirstOrDefaultAsync();

            if (ultimoPost == null) return Ok(false);

            DateTime horaMexicoPost = ultimoPost.FechaCreacion.Add(_mexicoOffset);

            // Si la fecha coincide (día, mes, año), ya publicó hoy
            bool publicoHoy = horaMexicoActual.Date == horaMexicoPost.Date;

            return Ok(publicoHoy);
        }

        // 3. Crear una nueva publicación
        [HttpPost("publicar")]
        public async Task<IActionResult> Publicar([FromBody] CrearPostRequest request)
        {
            var post = new Post
            {
                UsuarioId = request.UsuarioId,
                Texto = request.Texto,
                EsVideo = request.EsVideo,
                MediaUrl = request.MediaBase64, // Temporalmente guardaremos base64 directo aquí
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
            // Verificamos que no exista el like (para evitar errores de duplicidad)
            var existe = await _context.Likes.AnyAsync(l => l.PostId == postId && l.UsuarioId == usuarioId);
            if (existe) return BadRequest("Ya diste like a esta publicación");

            var like = new Like { PostId = postId, UsuarioId = usuarioId, FechaCreacion = DateTime.UtcNow };
            _context.Likes.Add(like);

            // Creamos la notificación para el dueño del post
            var post = await _context.Posts.FindAsync(postId);
            if (post != null && post.UsuarioId != usuarioId)
            {
                _context.Notificaciones.Add(new Notificacion
                {
                    UsuarioId = post.UsuarioId,
                    Mensaje = "❤️ Alguien ha reaccionado a tu entrenamiento.",
                    Leida = false,
                    FechaCreacion = DateTime.UtcNow
                });
            }

            await _context.SaveChangesAsync();
            return Ok();
        }

        // 5. Borrar Post (y sus likes en cascada por SQL)
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
        [HttpGet("notificaciones/{usuarioId}")]
        public async Task<IActionResult> GetNotificaciones(Guid usuarioId)
        {
            var notis = await _context.Notificaciones
                .Where(n => n.UsuarioId == usuarioId)
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

        // 7. Obtener Ranking de Likes
        [HttpGet("ranking/{anio}")]
        public async Task<IActionResult> GetRanking(int anio)
        {
            var ranking = await _context.Usuarios
                .Where(u => u.EstatusAprobacion == "Aprobado" || u.RolId == 1 || u.RolId == 2)
                .Select(u => new RankingUsuarioDto
                {
                    Id = u.Id,
                    Nombre = u.Nombre,
                    ApellidoPaterno = u.ApellidoPaterno,
                    Iniciales = (u.Nombre.Substring(0, 1) + u.ApellidoPaterno.Substring(0, 1)).ToUpper(),
                    // Cuenta los likes de todos los posts que pertenecen a este usuario en este año
                    TotalLikes = _context.Likes.Count(l =>
                        _context.Posts.Any(p => p.Id == l.PostId && p.UsuarioId == u.Id)
                        && l.FechaCreacion.Year == anio)
                })
                .Where(r => r.TotalLikes > 0) // Opcional: Solo mostramos a los que tienen al menos 1 like
                .OrderByDescending(r => r.TotalLikes)
                .ToListAsync();

            // Asignamos la posición (1ero, 2do, 3ro...)
            int pos = 1;
            foreach (var user in ranking)
            {
                user.Posicion = pos++;
            }

            return Ok(ranking);
        }
    }
}