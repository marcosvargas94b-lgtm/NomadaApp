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
    public class WodController : ControllerBase
    {
        private readonly NomadaDbContext _context;

        public WodController(NomadaDbContext context)
        {
            _context = context;
        }

        // ================= CATÁLOGO DE EJERCICIOS =================
        [HttpGet("catalogo")]
        public async Task<IActionResult> GetCatalogo()
        {
            var catalogo = await _context.CatalogoEjercicios.OrderBy(c => c.Nombre).ToListAsync();
            return Ok(catalogo);
        }

        [HttpPost("catalogo")]
        public async Task<IActionResult> AgregarAlCatalogo([FromBody] CatalogoEjercicio ejercicio)
        {
            _context.CatalogoEjercicios.Add(ejercicio);
            await _context.SaveChangesAsync();
            return Ok();
        }

        [HttpDelete("catalogo/{id}")]
        public async Task<IActionResult> EliminarDelCatalogo(int id)
        {
            var ej = await _context.CatalogoEjercicios.FindAsync(id);
            if (ej != null)
            {
                _context.CatalogoEjercicios.Remove(ej);
                await _context.SaveChangesAsync();
            }
            return Ok();
        }

        // ================= GESTIÓN DEL WOD =================
        [HttpGet("dia/{fechaStr}")]
        public async Task<IActionResult> GetWodsPorFecha(string fechaStr)
        {
            if (!DateTime.TryParse(fechaStr, out DateTime fecha)) return BadRequest();
            fecha = fecha.Date;

            var wodsGenerales = await _context.WodsGenerales.Where(w => w.Fecha == fecha).OrderBy(w => w.FechaCreacion).ToListAsync();
            var resultado = new List<WodGeneralDto>();

            foreach (var wod in wodsGenerales)
            {
                var secciones = await _context.WodsSecciones
                    .Where(s => s.WodGeneralId == wod.Id).OrderBy(s => s.Orden)
                    .Select(s => new WodSeccionDto { Subtitulo = s.Subtitulo, Contenido = s.Contenido, Orden = s.Orden }).ToListAsync();

                // Traemos los videos asociados a este WOD
                var ejercicios = await _context.WodEjercicios
                    .Where(we => we.WodGeneralId == wod.Id)
                    .Join(_context.CatalogoEjercicios, we => we.EjercicioId, c => c.Id, (we, c) => new EjercicioDto
                    {
                        Id = c.Id,
                        Nombre = c.Nombre,
                        UrlVideo = c.UrlVideo
                    }).ToListAsync();

                resultado.Add(new WodGeneralDto
                {
                    Id = wod.Id,
                    Titulo = wod.Titulo,
                    Fecha = wod.Fecha,
                    CoachId = wod.CoachId, // <--- AGREGAR ESTA LÍNEA AQUÍ
                    NombreCoach = wod.NombreCoach,
                    Secciones = secciones,
                    Ejercicios = ejercicios
                });
            }
            return Ok(resultado);
        }

        [HttpPost]
        public async Task<IActionResult> CrearWod([FromBody] CrearWodRequest request)
        {
            var coach = await _context.Usuarios.FindAsync(request.CoachId);
            if (coach == null) return BadRequest("Coach no encontrado");

            var nuevoWod = new WodGeneral
            {
                Titulo = request.Titulo,
                Fecha = request.Fecha.Date,
                CoachId = request.CoachId,
                NombreCoach = $"{coach.Nombre} {coach.ApellidoPaterno}",
                FechaCreacion = DateTime.UtcNow
            };

            _context.WodsGenerales.Add(nuevoWod);
            await _context.SaveChangesAsync();

            if (request.Secciones.Any())
            {
                _context.WodsSecciones.AddRange(request.Secciones.Select(s => new WodSeccion
                {
                    WodGeneralId = nuevoWod.Id,
                    Subtitulo = s.Subtitulo,
                    Contenido = s.Contenido,
                    Orden = s.Orden
                }));
            }

            // Enlazamos los videos del catálogo seleccionados
            if (request.EjerciciosIds.Any())
            {
                _context.WodEjercicios.AddRange(request.EjerciciosIds.Select(eId => new WodEjercicio
                {
                    WodGeneralId = nuevoWod.Id,
                    EjercicioId = eId
                }));
            }

            await _context.SaveChangesAsync();
            return Ok();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> EliminarWod(int id)
        {
            var wod = await _context.WodsGenerales.FindAsync(id);
            if (wod != null)
            {
                _context.WodsGenerales.Remove(wod);
                await _context.SaveChangesAsync();
            }
            return Ok();
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> ActualizarWod(int id, [FromBody] ActualizarWodRequest request)
        {
            var wod = await _context.WodsGenerales.FindAsync(id);
            if (wod == null) return NotFound();

            // 1. Actualizamos los datos generales
            wod.Titulo = request.Titulo;
            wod.Fecha = request.Fecha.Date;

            // 2. Borramos las secciones y videos "viejos" de este WOD
            var seccionesViejas = _context.WodsSecciones.Where(s => s.WodGeneralId == id);
            _context.WodsSecciones.RemoveRange(seccionesViejas);

            var ejerciciosViejos = _context.WodEjercicios.Where(e => e.WodGeneralId == id);
            _context.WodEjercicios.RemoveRange(ejerciciosViejos);

            await _context.SaveChangesAsync();

            // 3. Insertamos la nueva estructura que mandó el Coach
            if (request.Secciones.Any())
            {
                _context.WodsSecciones.AddRange(request.Secciones.Select(s => new WodSeccion
                {
                    WodGeneralId = id,
                    Subtitulo = s.Subtitulo,
                    Contenido = s.Contenido,
                    Orden = s.Orden
                }));
            }

            if (request.EjerciciosIds.Any())
            {
                _context.WodEjercicios.AddRange(request.EjerciciosIds.Select(eId => new WodEjercicio
                {
                    WodGeneralId = id,
                    EjercicioId = eId
                }));
            }

            await _context.SaveChangesAsync();
            return Ok();
        }
    }
}