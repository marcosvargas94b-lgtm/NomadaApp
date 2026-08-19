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
    public class EvaluacionesController : ControllerBase
    {
        private readonly NomadaDbContext _context;

        public EvaluacionesController(NomadaDbContext context)
        {
            _context = context;
        }

        // ==========================================
        // MÓDULO 1: CATÁLOGO DE EVALUACIONES
        // ==========================================
        [HttpGet("{gymCode}/catalogo")]
        public async Task<IActionResult> GetCatalogo(string gymCode)
        {
            var catalogo = await _context.EvaluacionesCatalogo
                .Where(e => e.GymCode == gymCode)
                .OrderBy(e => e.Nombre)
                .Select(e => new EvaluacionCatalogoDto
                {
                    Id = e.Id,
                    Nombre = e.Nombre,
                    TipoMedida = e.TipoMedida
                }).ToListAsync();
            return Ok(catalogo);
        }

        [HttpPost("catalogo")]
        public async Task<IActionResult> CrearEvaluacionCatalogo([FromBody] CrearEvaluacionCatalogoRequest request)
        {
            var evaluacion = new EvaluacionCatalogo
            {
                GymCode = request.GymCode,
                Nombre = request.Nombre,
                TipoMedida = request.TipoMedida,
                FechaCreacion = DateTime.UtcNow
            };
            _context.EvaluacionesCatalogo.Add(evaluacion);
            await _context.SaveChangesAsync();
            return Ok();
        }

        [HttpDelete("catalogo/{id}")]
        public async Task<IActionResult> EliminarEvaluacionCatalogo(int id)
        {
            var evaluacion = await _context.EvaluacionesCatalogo.FindAsync(id);
            if (evaluacion == null) return NotFound();
            _context.EvaluacionesCatalogo.Remove(evaluacion);
            await _context.SaveChangesAsync();
            return Ok();
        }

        // ==========================================
        // MÓDULO 2: MARCAS (PRs) Y COMUNIDAD
        // ==========================================

        // Devuelve TODAS las marcas del atleta (agrupadas por evaluación para ver progreso)
        [HttpGet("{gymCode}/atleta/{atletaId}/historial")]
        public async Task<IActionResult> GetHistorialAtleta(string gymCode, Guid atletaId)
        {
            var marcas = await _context.EvaluacionesAtletas
                .Include(ea => ea.EvaluacionCatalogo)
                .Where(ea => ea.GymCode == gymCode && ea.AtletaId == atletaId)
                .OrderByDescending(ea => ea.FechaRegistro)
                .Select(ea => new EvaluacionAtletaDto
                {
                    Id = ea.Id,
                    EvaluacionCatalogoId = ea.EvaluacionCatalogoId,
                    NombreEvaluacion = ea.EvaluacionCatalogo.Nombre,
                    TipoMedida = ea.EvaluacionCatalogo.TipoMedida,
                    Resultado = ea.Resultado,
                    FechaRegistro = ea.FechaRegistro,
                    RegistradoPorCoach = ea.RegistradoPorCoach
                }).ToListAsync();

            return Ok(marcas);
        }

        // Registrar una nueva marca (se guarda en el historial)
        [HttpPost("marca")]
        public async Task<IActionResult> RegistrarMarca([FromBody] RegistrarMarcaAtletaRequest request)
        {
            var marca = new EvaluacionAtleta
            {
                GymCode = request.GymCode,
                AtletaId = request.AtletaId,
                EvaluacionCatalogoId = request.EvaluacionCatalogoId,
                Resultado = request.Resultado,
                FechaRegistro = request.FechaRegistro,
                RegistradoPorCoach = request.EsCoach,
                RegistradoPorUsuarioId = request.RegistradoPorId
            };
            _context.EvaluacionesAtletas.Add(marca);
            await _context.SaveChangesAsync();
            return Ok();
        }

        // Eliminar solo si lo registró el mismo atleta (o un coach)
        [HttpDelete("marca/{id}/{usuarioPeticionId}")]
        public async Task<IActionResult> EliminarMarca(int id, Guid usuarioPeticionId)
        {
            var marca = await _context.EvaluacionesAtletas.FindAsync(id);
            if (marca == null) return NotFound();

            // Si lo registró un coach, el atleta no lo puede borrar
            if (marca.RegistradoPorCoach && marca.RegistradoPorUsuarioId != usuarioPeticionId)
            {
                // Revisamos si el que pide borrar es SuperAdmin o Coach (Lógica simplificada)
                var user = await _context.Usuarios.FindAsync(usuarioPeticionId);
                if (user == null || user.RolId == 3) // Si es Atleta normal, denegado
                {
                    return BadRequest("No puedes eliminar un PR certificado por un Coach.");
                }
            }

            _context.EvaluacionesAtletas.Remove(marca);
            await _context.SaveChangesAsync();
            return Ok();
        }

        // ==========================================
        // MÓDULO 3: PRIVACIDAD Y COMUNIDAD
        // ==========================================

        [HttpGet("{gymCode}/privacidad/{atletaId}")]
        public async Task<IActionResult> GetPrivacidad(string gymCode, Guid atletaId)
        {
            var user = await _context.Usuarios.FindAsync(atletaId);
            if (user == null) return NotFound();
            return Ok(user.MostrarPRsPublicos);
        }

        [HttpPut("{gymCode}/privacidad/{atletaId}")]
        public async Task<IActionResult> TogglePrivacidad(string gymCode, Guid atletaId, [FromBody] bool mostrar)
        {
            var user = await _context.Usuarios.FindAsync(atletaId);
            if (user == null) return NotFound();
            user.MostrarPRsPublicos = mostrar;
            await _context.SaveChangesAsync();
            return Ok();
        }

        // Obtiene la lista de atletas que han optado por mostrar sus PRs
        [HttpGet("{gymCode}/comunidad-prs/{atletaSolicitanteId}")]
        public async Task<IActionResult> GetAtletasConPRs(string gymCode, Guid atletaSolicitanteId)
        {
            var solicitante = await _context.Usuarios.FindAsync(atletaSolicitanteId);
            if (solicitante == null || (!solicitante.MostrarPRsPublicos && solicitante.RolId == 3))
            {
                // Si es atleta y no muestra sus PRs, no puede ver a los demás
                return BadRequest("Debes activar la visibilidad de tus PRs para ver los del resto de la tribu.");
            }

            var atletas = await _context.Usuarios
                .Where(u => u.GymCode == gymCode && u.MostrarPRsPublicos && u.Activo == true && u.Id != atletaSolicitanteId)
                .Select(u => new UsuarioAdminDto
                {
                    Id = u.Id,
                    Nombre = u.Nombre,
                    ApellidoPaterno = u.ApellidoPaterno,
                    FotoPerfil = u.FotoPerfil
                })
                .OrderBy(u => u.Nombre)
                .ToListAsync();

            return Ok(atletas);
        }
    }
}