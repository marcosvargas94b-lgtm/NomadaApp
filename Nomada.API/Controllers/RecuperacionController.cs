using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nomada.API.Data;
using Nomada.Shared.Entities;
using Nomada.Shared.Models; 
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace Nomada.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class RecuperacionController : ControllerBase
    {
        private readonly NomadaDbContext _context;
        private readonly TimeSpan _mexicoOffset = TimeSpan.FromHours(-6);

        public RecuperacionController(NomadaDbContext context)
        {
            _context = context;
        }

        [HttpGet("{gymCode}/{usuarioId}")]
        public async Task<IActionResult> ObtenerEstadoRecuperacion(string gymCode, Guid usuarioId)
        {
            try
            {
                var hoyMexico = DateTime.UtcNow.Add(_mexicoOffset);
                var fechaBusqueda = hoyMexico.Date.AddDays(-5); // Miramos 5 días atrás como acordamos

                // 1. OBTENER PERFIL FISIOLÓGICO DEL ATLETA
                var usuario = await _context.Usuarios.FindAsync(usuarioId);
                double factorFisiologico = 1.0;
                string sexoAEnviar = "M";
                bool tienePerfilCompleto = false;

                if (usuario != null)
                {
                    if (!string.IsNullOrEmpty(usuario.Sexo))
                    {
                        // Con StartsWith atrapamos "F", "Femenino", "femenino", etc.
                        sexoAEnviar = usuario.Sexo.ToUpper().StartsWith("F") ? "F" : "M";
                    }

                    if (usuario.Peso > 0 && usuario.Estatura > 0 && !string.IsNullOrEmpty(usuario.Sexo))
                    {
                        tienePerfilCompleto = true;

                        double estaturaMts = (double)usuario.Estatura / 100.0;
                        double bmi = (double)usuario.Peso / (estaturaMts * estaturaMts);
                        double factorSexo = sexoAEnviar == "F" ? 1.05 : 1.0;

                        double factorBmi = (bmi >= 18.5 && bmi <= 25) ? 1.05 : (bmi > 30 ? 0.95 : 1.0);

                        int edadCalc = Math.Max(20, (DateTime.Today.Year - usuario.FechaNacimiento.Year));
                        double factorEdad = 1.0 - ((edadCalc - 25) * 0.002);

                        factorFisiologico = factorEdad * factorSexo * factorBmi;
                    }
                }

                // 2. INICIALIZAR DICCIONARIO DE FATIGA
                var catalogo = new List<string> {
                    "Pecho", "Espalda Alta", "Lumbares", "Hombros", "Bíceps", "Tríceps",
                    "Antebrazos", "Abdomen", "Oblicuos", "Cuádriceps", "Isquiotibiales",
                    "Glúteos", "Pantorrillas", "Trapecios", "Full Body / Cardio"
                };

                var fatigaDic = catalogo.ToDictionary(m => m, m => 0.0);

                var musculosGrandes = new HashSet<string> { "Pecho", "Espalda Alta", "Cuádriceps", "Isquiotibiales", "Glúteos" };
                var musculosMedianos = new HashSet<string> { "Hombros", "Trapecios", "Lumbares", "Full Body / Cardio" };

                // 3. OBTENER LAS ASISTENCIAS RECIENTES
                var asistencias = await _context.Asistencias
                    .Where(a => a.GymCode == gymCode && a.UsuarioId == usuarioId && a.FechaHora >= fechaBusqueda)
                    .ToListAsync();                 

                var respuesta = new EstadoRecuperacionDto();

                foreach (var asistencia in asistencias)
                {
                    if (asistencia.WodGeneralId > 0)
                    {
                        var wod = await _context.WodsGenerales.FindAsync(asistencia.WodGeneralId);

                        if (wod != null && !string.IsNullOrEmpty(wod.JsonFatigaMuscular))
                        {
                            int rpeAUtilizar = 7; // Asumimos 7 por default para días pasados

                            if (asistencia.RPE == null)
                            {
                                if (asistencia.FechaHora.Date == hoyMexico.Date)
                                {
                                    // Pide el de hoy
                                    respuesta.RequiereRPEHoy = true;
                                    respuesta.AsistenciaPendienteId = asistencia.Id;
                                    respuesta.TituloWodPendiente = wod.Titulo;
                                    continue;
                                }
                            }
                            else
                            {
                                rpeAUtilizar = asistencia.RPE.Value;
                            }

                            double factorRPE = rpeAUtilizar <= 4 ? 0.5 : rpeAUtilizar <= 6 ? 0.8 : rpeAUtilizar == 7 ? 1.0 : 1.2;
                            double horasTranscurridas = Math.Max((hoyMexico - asistencia.FechaHora).TotalHours, 0);

                            try
                            {
                                var fatigaBaseDict = JsonSerializer.Deserialize<Dictionary<string, int>>(wod.JsonFatigaMuscular);

                                if (fatigaBaseDict != null)
                                {
                                    foreach (var kvp in fatigaBaseDict)
                                    {
                                        if (kvp.Value > 0)
                                        {
                                            bool esGrande = musculosGrandes.Contains(kvp.Key);
                                            bool esMediano = musculosMedianos.Contains(kvp.Key);

                                            double recupPorHoraBase = esGrande ? 1.5 : esMediano ? 2.0 : 3.0;

                                            // EL NERFEO QUE ACORDAMOS DEL 50%
                                            double fatigaGenerada = (kvp.Value * 0.5) * factorRPE;
                                            double fatigaRestante = fatigaGenerada - (horasTranscurridas * (recupPorHoraBase * factorFisiologico));

                                            if (fatigaRestante > 0 && fatigaDic.ContainsKey(kvp.Key))
                                            {
                                                fatigaDic[kvp.Key] += fatigaRestante;
                                            }
                                        }
                                    }
                                }
                            }
                            catch { }
                        }
                    }
                }


                // =========================================================
                // 3.5 OBTENER ENTRENAMIENTOS PERSONALIZADOS REALIZADOS
                // =========================================================
                var personalizadosRealizados = await _context.EntrenosPersonalizadosRealizados
                    .Where(p => p.GymCode == gymCode && p.AtletaId == usuarioId && p.FechaRealizacion >= fechaBusqueda)
                    .ToListAsync();

                foreach (var entreno in personalizadosRealizados)
                {
                    if (!string.IsNullOrEmpty(entreno.JsonFatigaAjustada))
                    {
                        double horasTranscurridas = Math.Max((hoyMexico - entreno.FechaRealizacion).TotalHours, 0);

                        try
                        {
                            var fatigaAjustadaDict = JsonSerializer.Deserialize<Dictionary<string, int>>(entreno.JsonFatigaAjustada);

                            if (fatigaAjustadaDict != null)
                            {
                                foreach (var kvp in fatigaAjustadaDict)
                                {
                                    if (kvp.Value > 0)
                                    {
                                        bool esGrande = musculosGrandes.Contains(kvp.Key);
                                        bool esMediano = musculosMedianos.Contains(kvp.Key);
                                        double recupPorHoraBase = esGrande ? 1.5 : esMediano ? 2.0 : 3.0;

                                        // La IA ya ajustó los porcentajes basándose en las notas del atleta.
                                        // Usamos un multiplicador RPE neutro (1.0) porque la IA ya hizo el trabajo de subir/bajar la carga.
                                        double fatigaGenerada = (kvp.Value * 0.5) * 1.0;
                                        double fatigaRestante = fatigaGenerada - (horasTranscurridas * (recupPorHoraBase * factorFisiologico));

                                        if (fatigaRestante > 0 && fatigaDic.ContainsKey(kvp.Key))
                                        {
                                            fatigaDic[kvp.Key] += fatigaRestante;
                                        }
                                    }
                                }
                            }
                        }
                        catch { /* Ignorar JSON malformados */ }
                    }
                }
                // =========================================================
                // =========================================================
                // 3.6 OBTENER DÍAS PROVISIONALES IA REALIZADOS
                // =========================================================
                var provisionalesRealizados = await _context.RutinasProvisionalesDias
                    .Join(_context.RutinasProvisionalesIA,
                          dia => dia.RutinaProvisionalId,
                          plan => plan.Id,
                          (dia, plan) => new { dia, plan })
                    .Where(x => x.plan.GymCode == gymCode &&
                                x.plan.UsuarioId == usuarioId &&
                                x.dia.Completado &&
                                x.dia.FechaRealizacion >= fechaBusqueda)
                    .Select(x => x.dia)
                    .ToListAsync();

                foreach (var dia in provisionalesRealizados)
                {
                    if (!string.IsNullOrEmpty(dia.JsonFatigaAjustada) && dia.FechaRealizacion.HasValue)
                    {
                        double horasTranscurridas = Math.Max((hoyMexico - dia.FechaRealizacion.Value).TotalHours, 0);

                        try
                        {
                            var fatigaDict = JsonSerializer.Deserialize<Dictionary<string, int>>(dia.JsonFatigaAjustada);
                            if (fatigaDict != null)
                            {
                                foreach (var kvp in fatigaDict)
                                {
                                    if (kvp.Value > 0)
                                    {
                                        bool esGrande = musculosGrandes.Contains(kvp.Key);
                                        bool esMediano = musculosMedianos.Contains(kvp.Key);
                                        double recupPorHoraBase = esGrande ? 1.5 : esMediano ? 2.0 : 3.0;

                                        double fatigaGenerada = (kvp.Value * 0.5) * 1.0;
                                        double fatigaRestante = fatigaGenerada - (horasTranscurridas * (recupPorHoraBase * factorFisiologico));

                                        if (fatigaRestante > 0 && fatigaDic.ContainsKey(kvp.Key))
                                        {
                                            fatigaDic[kvp.Key] += fatigaRestante;
                                        }
                                    }
                                }
                            }
                        }
                        catch { }
                    }
                }
                // 4. CONSOLIDAR DATOS PARA EL MAPA DE CALOR
                var listaMusculos = fatigaDic.Select(kvp =>
                {
                    double fatigaReal = Math.Min(kvp.Value, 100.0);
                    int recuperacion = (int)Math.Max(100.0 - fatigaReal, 0);

                    return new MusculoRecuperacionDto { Nombre = kvp.Key, Porcentaje = recuperacion };
                }).ToList();

                double promedioGlobal = listaMusculos.Average(m => m.Porcentaje);

                string mensaje = "¡CUERPO AL 100%! LISTO PARA ROMPERLA.";
                if (promedioGlobal < 60) mensaje = "FATIGA ALTA. PRIORIZA EL DESCANSO HOY.";
                else if (promedioGlobal < 85) mensaje = "RECUPERACIÓN EN PROCESO. ENTRENA INTELIGENTE.";
                else if (promedioGlobal < 95) mensaje = "BUENA DISPONIBILIDAD. ¡DALE DURO!";

                respuesta.DisponibilidadGlobal = Math.Round(promedioGlobal, 1);
                respuesta.Mensaje = mensaje;
                respuesta.Musculos = listaMusculos;
                respuesta.Sexo = sexoAEnviar;
                respuesta.PerfilCompleto = tienePerfilCompleto;
                return Ok(respuesta);
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }

        [HttpPut("capturar-rpe/{asistenciaId}")]
        public async Task<IActionResult> CapturarRPE(int asistenciaId, [FromBody] GuardarRPERequest request)
        {
            var asistencia = await _context.Asistencias.FindAsync(asistenciaId);
            if (asistencia == null) return NotFound("Asistencia no encontrada");

            asistencia.RPE = request.RPE;
            await _context.SaveChangesAsync();

            return Ok();
        }
    }
}