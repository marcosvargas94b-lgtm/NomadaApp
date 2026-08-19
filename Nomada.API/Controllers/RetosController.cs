using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nomada.API.Data;
using Nomada.Shared.Entities;
using Nomada.Shared.Models;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace Nomada.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class RetosController : ControllerBase
    {
        private readonly NomadaDbContext _context;

        public RetosController(NomadaDbContext context)
        {
            _context = context;
        }

        // ===============================================
        // MÓDULO ADMIN: GESTIÓN DE CATÁLOGO Y ASIGNACIONES
        // ===============================================

        [HttpGet("{gymCode}/catalogo")]
        public async Task<IActionResult> GetCatalogo(string gymCode)
        {
            var retos = await _context.RetosCatalogo
                .Where(r => r.GymCode == gymCode)
                .OrderByDescending(r => r.EsPremioMaximo)
                .ThenBy(r => r.Titulo)
                .Select(r => new RetoCatalogoDto
                {
                    Id = r.Id,
                    Titulo = r.Titulo,
                    Descripcion = r.Descripcion,
                    UrlImagen = r.UrlImagen,
                    EsPremioMaximo = r.EsPremioMaximo,
                    EsAutomatico = r.EsAutomatico
                }).ToListAsync();
            return Ok(retos);
        }

        // El Coach puede crear un reto manual (Ej. "Gana la Competencia 2026")
        [HttpPost("catalogo")]
        public async Task<IActionResult> CrearReto([FromBody] RetoCatalogo nuevoReto)
        {
            nuevoReto.EsAutomatico = false; // Todo lo que crea el coach es manual
            nuevoReto.FechaCreacion = DateTime.UtcNow;
            _context.RetosCatalogo.Add(nuevoReto);
            await _context.SaveChangesAsync();
            return Ok();
        }

        [HttpPost("asignar")]
        public async Task<IActionResult> AsignarRetoAAtleta([FromBody] AsignarRetoRequest req)
        {
            var existe = await _context.RetosAtletas
                .AnyAsync(r => r.AtletaId == req.AtletaId && r.RetoCatalogoId == req.RetoCatalogoId);

            if (existe) return BadRequest("El atleta ya tiene esta medalla.");

            var medalla = new RetoAtleta
            {
                GymCode = req.GymCode,
                AtletaId = req.AtletaId,
                RetoCatalogoId = req.RetoCatalogoId,
                FechaDesbloqueo = DateTime.UtcNow
            };

            _context.RetosAtletas.Add(medalla);
            await _context.SaveChangesAsync();
            return Ok();
        }

        [HttpDelete("remover/{atletaId}/{retoId}")]
        public async Task<IActionResult> RemoverRetoDeAtleta(Guid atletaId, int retoId)
        {
            var medalla = await _context.RetosAtletas
                .FirstOrDefaultAsync(r => r.AtletaId == atletaId && r.RetoCatalogoId == retoId);

            if (medalla != null)
            {
                _context.RetosAtletas.Remove(medalla);
                await _context.SaveChangesAsync();
            }
            return Ok();
        }


        // ===============================================
        // MÓDULO ATLETA: EL ÁLBUM Y EVALUACIÓN DE REGLAS
        // ===============================================

        [HttpGet("{gymCode}/album/{atletaId}")]
        public async Task<IActionResult> GetAlbumAtleta(string gymCode, Guid atletaId)
        {
            // 1. Antes de devolver el álbum, el motor evalúa si acaba de ganar algo
            await EvaluarRetosAutomaticos(gymCode, atletaId);

            // 2. Traer el catálogo completo e indicar cuáles ya tiene desbloqueadas
            var catalogo = await _context.RetosCatalogo.Where(r => r.GymCode == gymCode).ToListAsync();
            var desbloqueados = await _context.RetosAtletas.Where(r => r.AtletaId == atletaId).ToListAsync();

            var album = catalogo.Select(c => new RetoCatalogoDto
            {
                Id = c.Id,
                Titulo = c.Titulo,
                Descripcion = c.Descripcion,
                UrlImagen = c.UrlImagen,
                EsPremioMaximo = c.EsPremioMaximo,
                EsAutomatico = c.EsAutomatico,
                Desbloqueado = desbloqueados.Any(d => d.RetoCatalogoId == c.Id),
                FechaDesbloqueo = desbloqueados.FirstOrDefault(d => d.RetoCatalogoId == c.Id)?.FechaDesbloqueo
            })
            .OrderByDescending(r => r.EsPremioMaximo)
            .ThenByDescending(r => r.Desbloqueado) // Las que ya tiene salen primero
            .ToList();

            return Ok(album);
        }

        // ===============================================
        // EL CEREBRO: MOTOR DE REGLAS AUTOMÁTICAS
        // ===============================================
        private async Task EvaluarRetosAutomaticos(string gymCode, Guid atletaId)
        {
            var retosAutomaticos = await _context.RetosCatalogo
                .Where(r => r.GymCode == gymCode && r.EsAutomatico)
                .ToListAsync();

            var retosYaDesbloqueados = await _context.RetosAtletas
                .Where(r => r.AtletaId == atletaId)
                .Select(r => r.RetoCatalogoId)
                .ToListAsync();

            foreach (var reto in retosAutomaticos)
            {
                if (retosYaDesbloqueados.Contains(reto.Id)) continue; // Ya lo tiene, lo saltamos

                bool loGano = false;

                switch (reto.ReglaInterna)
                {
                    case "FOTO_PERFIL":
                        var user = await _context.Usuarios.FindAsync(atletaId);
                        loGano = !string.IsNullOrEmpty(user?.FotoPerfil);
                        break;

                    case "PRIMER_PR":
                        loGano = await _context.WodScores.AnyAsync(s => s.UsuarioId == atletaId);
                        break;

                    case "PAGOS_3_MESES":
                        // Si tiene al menos 3 registros en Ingresos
                        var pagos = await _context.Ingresos.CountAsync(i => i.UsuarioId == atletaId && i.TipoCobro == "Mensual");
                        loGano = pagos >= 3;
                        break;

                    case "ASISTENCIA_DOBLE":
                        // Busca si en el mismo día tiene una asistencia AM (< 12:00) y una PM (> 16:00)
                        var diasDoble = await _context.Asistencias
                            .Where(a => a.UsuarioId == atletaId)
                            .GroupBy(a => a.FechaHora.Date)
                            .AnyAsync(g => g.Any(x => x.FechaHora.Hour < 12) && g.Any(x => x.FechaHora.Hour > 16));
                        loGano = diasDoble;
                        break;

                    case "SEMANA_5_DIAS":
                        // Si hay al menos 5 asistencias en los últimos 7 días
                        var hace7Dias = DateTime.UtcNow.AddDays(-7);
                        var asistenciasSemana = await _context.Asistencias
                            .Where(a => a.UsuarioId == atletaId && a.FechaHora >= hace7Dias)
                            .Select(a => a.FechaHora.Date)
                            .Distinct()
                            .CountAsync();
                        loGano = asistenciasSemana >= 5;
                        break;

                    case "MES_20_DIAS":
                        var mesActual = DateTime.UtcNow.Month;
                        var anioActual = DateTime.UtcNow.Year;
                        var asistenciasMes = await _context.Asistencias
                            .Where(a => a.UsuarioId == atletaId && a.FechaHora.Month == mesActual && a.FechaHora.Year == anioActual)
                            .Select(a => a.FechaHora.Date)
                            .Distinct()
                            .CountAsync();
                        loGano = asistenciasMes >= 20;
                        break;

                    case "UN_ANIO_ACTIVO":
                        // Pagos o suscripciones activas por 12 meses (Simplificado a 12 ingresos)
                        var pagosAnuales = await _context.Ingresos.CountAsync(i => i.UsuarioId == atletaId && i.TipoCobro == "Mensual");
                        loGano = pagosAnuales >= 12;
                        break;
                }

                // Si cumplió la regla, le otorgamos la medalla!
                if (loGano)
                {
                    _context.RetosAtletas.Add(new RetoAtleta
                    {
                        GymCode = gymCode,
                        AtletaId = atletaId,
                        RetoCatalogoId = reto.Id,
                        FechaDesbloqueo = DateTime.UtcNow
                    });
                }
            }

            await _context.SaveChangesAsync();
        }
    }
}