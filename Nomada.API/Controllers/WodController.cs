using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nomada.API.Data;
using Nomada.Shared.Entities;
using Nomada.Shared.Models;
using Nomada.API.Services;
using Microsoft.AspNetCore.Http;
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
        private readonly IBlobStorageService _blobService;
        public WodController(NomadaDbContext context, IBlobStorageService blobService) 
        {
            _context = context;
            _blobService = blobService; 
        }

        // ================= CATÁLOGO DE EJERCICIOS =================
        [HttpGet("{gymCode}/catalogo")]
        public async Task<IActionResult> GetCatalogo(string gymCode)
        {
            var catalogo = await _context.CatalogoEjercicios
                .Where(c => c.GymCode == gymCode)
                .OrderBy(c => c.Nombre)
                .ToListAsync();
            return Ok(catalogo);
        }

        [HttpPost("catalogo")]
        public async Task<IActionResult> AgregarAlCatalogo([FromBody] CatalogoEjercicio ejercicio)
        {
            // El GymCode ya debe venir en el objeto 'ejercicio'
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
        [HttpGet("{gymCode}/dia/{fechaStr}")]
        public async Task<IActionResult> GetWodsPorFecha(string gymCode, string fechaStr)
        {
            if (!DateTime.TryParse(fechaStr, out DateTime fecha)) return BadRequest();
            fecha = fecha.Date;

            var wodsGenerales = await _context.WodsGenerales
                .Where(w => w.GymCode == gymCode && w.Fecha == fecha)
                .OrderBy(w => w.FechaCreacion)
                .ToListAsync();

            var resultado = new List<WodGeneralDto>();

            foreach (var wod in wodsGenerales)
            {
                var secciones = await _context.WodsSecciones
                    .Where(s => s.WodGeneralId == wod.Id).OrderBy(s => s.Orden)
                    .Select(s => new WodSeccionDto { Subtitulo = s.Subtitulo, Contenido = s.Contenido, Orden = s.Orden }).ToListAsync();

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
                    CoachId = wod.CoachId,
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
                GymCode = request.GymCode, // <--- GUARDAR WOD EN EL GIMNASIO CORRECTO
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

            wod.Titulo = request.Titulo;
            wod.Fecha = request.Fecha.Date;

            var seccionesViejas = _context.WodsSecciones.Where(s => s.WodGeneralId == id);
            _context.WodsSecciones.RemoveRange(seccionesViejas);

            var ejerciciosViejos = _context.WodEjercicios.Where(e => e.WodGeneralId == id);
            _context.WodEjercicios.RemoveRange(ejerciciosViejos);

            await _context.SaveChangesAsync();

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

        [HttpPost("catalogo/subir-video")]
        public async Task<IActionResult> AgregarAlCatalogoVideo([FromForm] string gymCode, [FromForm] string nombre, IFormFile video)
        {
            if (video == null || string.IsNullOrEmpty(gymCode) || string.IsNullOrEmpty(nombre))
                return BadRequest("Faltan datos o el video está vacío");

            // 1. Mandamos el video a la nube de Azure
            string urlAzure = await _blobService.SubirVideoCatalogoAsync(video);

            if (urlAzure == null)
                return StatusCode(500, "Error al subir el video a Azure");

            // 2. Guardamos el registro en SQL con el link mágico que nos dio Azure
            var nuevoEjercicio = new CatalogoEjercicio
            {
                GymCode = gymCode,
                Nombre = nombre,
                UrlVideo = urlAzure
            };

            _context.CatalogoEjercicios.Add(nuevoEjercicio);
            await _context.SaveChangesAsync();

            return Ok();
        }
    }
}