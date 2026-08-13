using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nomada.API.Data;
using Nomada.Shared.Entities;
using Nomada.Shared.Models;
using Nomada.API.Services;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace Nomada.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PersonalizadosController : ControllerBase
    {
        private readonly NomadaDbContext _context;
        private readonly GeminiAIService _iaService;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly TimeSpan _mexicoOffset = TimeSpan.FromHours(-6);

        public PersonalizadosController(NomadaDbContext context, GeminiAIService iaService, IServiceScopeFactory scopeFactory)
        {
            _context = context;
            _iaService = iaService;
            _scopeFactory = scopeFactory;
        }      

        // ======================================================================
        // 1. COACH: CREAR RUTINA EN LA BÓVEDA
        // ======================================================================
        [HttpPost]
        public async Task<IActionResult> CrearRutinaPersonalizada([FromBody] CrearWodPersonalizadoRequest request)
        {
            // 1. Guardamos la estructura rápido
            var nuevoWod = new WodPersonalizado
            {
                GymCode = request.GymCode,
                CoachId = request.CoachId,
                AtletaId = request.AtletaId,
                Titulo = request.Titulo,
                FechaCreacion = DateTime.UtcNow,
                JsonFatigaMuscular = null,
                TienePesos = false
            };

            _context.WodsPersonalizados.Add(nuevoWod);
            await _context.SaveChangesAsync();

            if (request.Secciones.Any())
            {
                _context.WodsPersonalizadosSecciones.AddRange(request.Secciones.Select(s => new WodPersonalizadoSeccion
                {
                    WodPersonalizadoId = nuevoWod.Id,
                    Subtitulo = s.Subtitulo,
                    Contenido = s.Contenido,
                    Orden = s.Orden
                }));
            }

            if (request.EjerciciosIds.Any())
            {
                _context.WodsPersonalizadosEjercicios.AddRange(request.EjerciciosIds.Select(eId => new WodPersonalizadoEjercicio
                {
                    WodPersonalizadoId = nuevoWod.Id,
                    EjercicioId = eId
                }));
            }

            await _context.SaveChangesAsync();

            // 2. DISPARAMOS EL ANÁLISIS IA EN SEGUNDO PLANO (Fatiga Base)
            string textoWod = string.Join("\n", request.Secciones.Select(s => $"{s.Subtitulo}\n{s.Contenido}"));
            int wodIdGenerado = nuevoWod.Id;

            _ = Task.Run(async () =>
            {
                using (var scope = _scopeFactory.CreateScope())
                {
                    var scopedContext = scope.ServiceProvider.GetRequiredService<NomadaDbContext>();
                    var iaServ = scope.ServiceProvider.GetRequiredService<GeminiAIService>();

                    try
                    {
                        var analisis = await iaServ.AnalizarImpactoWod(textoWod);
                        var wodParaActualizar = await scopedContext.WodsPersonalizados.FindAsync(wodIdGenerado);

                        if (wodParaActualizar != null)
                        {
                            wodParaActualizar.JsonFatigaMuscular = JsonSerializer.Serialize(analisis.FatigaMuscular);
                            wodParaActualizar.TienePesos = analisis.TienePesos;
                            await scopedContext.SaveChangesAsync();
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error Background IA (Crear Rutina Personalizada): {ex.Message}");
                    }
                }
            });

            return Ok();
        }

        // ======================================================================
        // 2. ATLETA: VER SU CATÁLOGO DE RUTINAS
        // ======================================================================
        [HttpGet("{gymCode}/atleta/{atletaId}")]
        public async Task<IActionResult> ObtenerCatalogoAtleta(string gymCode, Guid atletaId)
        {
            DateTime hoyMexico = DateTime.UtcNow.Add(_mexicoOffset).Date;

            // Traemos todas las rutinas del atleta
            var rutinas = await _context.WodsPersonalizados
                .Where(w => w.GymCode == gymCode && w.AtletaId == atletaId)
                .OrderByDescending(w => w.FechaCreacion)
                .ToListAsync();

            // Buscamos cuáles ya realizó HOY para bloquearlas (Candado verde)
            var idsRealizadosHoy = await _context.EntrenosPersonalizadosRealizados
                .Where(e => e.AtletaId == atletaId && e.FechaRealizacion.Date == hoyMexico)
                .Select(e => e.WodPersonalizadoId)
                .ToListAsync();

            var resultado = rutinas.Select(r => new RutinaAtletaResumenDto
            {
                Id = r.Id,
                Titulo = r.Titulo,
                FechaCreacion = r.FechaCreacion,
                RealizadoHoy = idsRealizadosHoy.Contains(r.Id)
            }).ToList();

            return Ok(resultado);
        }

        // ======================================================================
        // 3. ATLETA: VER EL DETALLE DE UNA RUTINA (Para entrenar)
        // ======================================================================
        [HttpGet("detalle/{rutinaId}")]
        public async Task<IActionResult> ObtenerDetalleRutina(int rutinaId)
        {
            var rutina = await _context.WodsPersonalizados.FindAsync(rutinaId);
            if (rutina == null) return NotFound();

            var secciones = await _context.WodsPersonalizadosSecciones
                .Where(s => s.WodPersonalizadoId == rutinaId).OrderBy(s => s.Orden)
                .Select(s => new WodSeccionDto { Subtitulo = s.Subtitulo, Contenido = s.Contenido, Orden = s.Orden })
                .ToListAsync();

            var ejercicios = await _context.WodsPersonalizadosEjercicios
                .Where(we => we.WodPersonalizadoId == rutinaId)
                .Join(_context.CatalogoEjercicios, we => we.EjercicioId, c => c.Id, (we, c) => new EjercicioDto
                {
                    Id = c.Id,
                    Nombre = c.Nombre,
                    UrlVideo = c.UrlVideo
                }).ToListAsync();

            return Ok(new
            {
                Id = rutina.Id,
                Titulo = rutina.Titulo,
                Secciones = secciones,
                Ejercicios = ejercicios
            });
        }

        // ======================================================================
        // 4. ATLETA: FINALIZAR ENTRENAMIENTO (Dispara IA de Notas)
        // ======================================================================
        [HttpPost("finalizar/{gymCode}/{rutinaId}/{atletaId}")]
        public async Task<IActionResult> FinalizarEntrenamiento(string gymCode, int rutinaId, Guid atletaId, [FromBody] FinalizarEntrenoRequest request)
        {
            var rutinaOriginal = await _context.WodsPersonalizados.FindAsync(rutinaId);
            if (rutinaOriginal == null) return NotFound("Rutina no encontrada.");

            DateTime fechaActualMexico = DateTime.UtcNow.Add(_mexicoOffset);

            // 1. Guardamos el registro de inmediato para que la app se destrabe
            var entrenoRealizado = new EntrenoPersonalizadoRealizado
            {
                GymCode = gymCode,
                AtletaId = atletaId,
                WodPersonalizadoId = rutinaId,
                FechaRealizacion = fechaActualMexico,
                NotasAtleta = request.NotasAtleta,
                JsonFatigaAjustada = rutinaOriginal.JsonFatigaMuscular // Iniciamos con el base por defecto
            };

            _context.EntrenosPersonalizadosRealizados.Add(entrenoRealizado);
            await _context.SaveChangesAsync();

            // 2. MAGIA EN SEGUNDO PLANO: RE-ANÁLISIS DE LA IA SI HAY NOTAS
            if (!string.IsNullOrWhiteSpace(request.NotasAtleta) && !string.IsNullOrEmpty(rutinaOriginal.JsonFatigaMuscular))
            {
                int entrenoIdGenerado = entrenoRealizado.Id;
                string fatigaBaseJson = rutinaOriginal.JsonFatigaMuscular;
                string notasTexto = request.NotasAtleta;

                _ = Task.Run(async () =>
                {
                    using (var scope = _scopeFactory.CreateScope())
                    {
                        var scopedContext = scope.ServiceProvider.GetRequiredService<NomadaDbContext>();
                        var iaServ = scope.ServiceProvider.GetRequiredService<GeminiAIService>();

                        try
                        {
                            // Le pedimos a la IA que modifique el JSON original
                            var analisisAjustado = await iaServ.AjustarFatigaPorNotas(fatigaBaseJson, notasTexto);

                            var entrenoParaActualizar = await scopedContext.EntrenosPersonalizadosRealizados.FindAsync(entrenoIdGenerado);

                            if (entrenoParaActualizar != null)
                            {
                                entrenoParaActualizar.JsonFatigaAjustada = JsonSerializer.Serialize(analisisAjustado.FatigaMuscular);
                                await scopedContext.SaveChangesAsync();
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error Background IA (Ajuste por Notas): {ex.Message}");
                        }
                    }
                });
            }

            return Ok();
        }
    }
}