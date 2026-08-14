using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nomada.API.Data;
using Nomada.Shared.Entities;
using Nomada.Shared.Models;
using Nomada.API.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace Nomada.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ProvisionalesController : ControllerBase
    {
        private readonly NomadaDbContext _context;
        private readonly GeminiAIService _iaService;
        private readonly TimeSpan _mexicoOffset = TimeSpan.FromHours(-6);

        public ProvisionalesController(NomadaDbContext context, GeminiAIService iaService)
        {
            _context = context;
            _iaService = iaService;
        }

        // 1. OBTENER PLAN ACTIVO
        [HttpGet("{gymCode}/{usuarioId}")]
        public async Task<IActionResult> ObtenerPlanActivo(string gymCode, Guid usuarioId)
        {
            var ahora = DateTime.UtcNow;

            var plan = await _context.RutinasProvisionalesIA
                .Where(r => r.GymCode == gymCode && r.UsuarioId == usuarioId && r.FechaExpiracion > ahora)
                .OrderByDescending(r => r.FechaCreacion)
                .FirstOrDefaultAsync();

            if (plan == null) return Ok(null);

            var dias = await _context.RutinasProvisionalesDias
                .Where(d => d.RutinaProvisionalId == plan.Id)
                .OrderBy(d => d.DiaNumero)
                .ToListAsync();

            var resultado = new RutinaProvisionalCompletaDto
            {
                Id = plan.Id,
                DiasTotales = plan.DiasTotales,
                FechaExpiracion = plan.FechaExpiracion,
                Dias = dias.Select(d => new RutinaProvisionalDiaDto
                {
                    Id = d.Id,
                    DiaNumero = d.DiaNumero,
                    TituloDia = d.TituloDia,
                    Secciones = JsonSerializer.Deserialize<List<WodSeccionDto>>(d.ContenidoJson) ?? new(),
                    Completado = d.Completado,
                    FechaRealizacion = d.FechaRealizacion,
                    NotasAtleta = d.NotasAtleta
                }).ToList()
            };

            return Ok(resultado);
        }

        // 2. GENERAR PLAN CON LA IA (1 a 5 Días)
        [HttpPost("generar")]
        public async Task<IActionResult> GenerarPlan([FromBody] GenerarRutinaProvisionalRequest request)
        {
            if (request.Dias < 1 || request.Dias > 5) return BadRequest("Los días deben ser entre 1 y 5.");

            // Calculamos la expiración exacta: días del plan + 48 horas después
            var fechaCreacion = DateTime.UtcNow;
            var fechaExpiracion = fechaCreacion.AddDays(request.Dias).AddHours(48);

            // Generamos la rutina con Gemini
            var diasGenerados = await _iaService.GenerarPlanProvisionalIA(request.Dias, request.Entorno, request.Dificultad, request.Notas);
            if (!diasGenerados.Any()) return StatusCode(500, "Error al generar la rutina con la IA.");

            var nuevoPlan = new RutinaProvisionalIA
            {
                GymCode = request.GymCode,
                UsuarioId = request.UsuarioId,
                FechaCreacion = fechaCreacion,
                FechaExpiracion = fechaExpiracion,
                DiasTotales = request.Dias,
                Entorno = request.Entorno,
                Dificultad = request.Dificultad,
                NotasSolicitud = request.Notas
            };

            _context.RutinasProvisionalesIA.Add(nuevoPlan);
            await _context.SaveChangesAsync();

            foreach (var d in diasGenerados)
            {
                var diaEntity = new RutinaProvisionalDia
                {
                    RutinaProvisionalId = nuevoPlan.Id,
                    DiaNumero = d.DiaNumero,
                    TituloDia = d.TituloDia,
                    ContenidoJson = JsonSerializer.Serialize(d.Secciones),
                    Completado = false,
                    JsonFatigaAjustada = JsonSerializer.Serialize(d.FatigaMuscular)
                };
                _context.RutinasProvisionalesDias.Add(diaEntity);
            }

            await _context.SaveChangesAsync();
            return Ok();
        }

        // 3. COMPLETAR DÍA Y AJUSTAR FATIGA CON NOTAS
        [HttpPost("completar-dia/{diaId}")]
        public async Task<IActionResult> CompletarDia(int diaId, [FromBody] CompletarDiaProvisionalRequest request)
        {
            var dia = await _context.RutinasProvisionalesDias.FindAsync(diaId);
            if (dia == null) return NotFound("Día no encontrado.");

            dia.Completado = true;
            dia.FechaRealizacion = DateTime.UtcNow.Add(_mexicoOffset);
            dia.NotasAtleta = request.NotasAtleta;

            if (!string.IsNullOrWhiteSpace(request.NotasAtleta) && !string.IsNullOrEmpty(dia.JsonFatigaAjustada))
            {
                try
                {
                    var ajuste = await _iaService.AjustarFatigaPorNotas(dia.JsonFatigaAjustada, request.NotasAtleta);
                    dia.JsonFatigaAjustada = JsonSerializer.Serialize(ajuste.FatigaMuscular);
                }
                catch { }
            }

            await _context.SaveChangesAsync();
            return Ok();
        }
    }
}