using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nomada.API.Data;
using Nomada.Shared.Models;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Nomada.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class BoxController : ControllerBase
    {
        private readonly NomadaDbContext _context;

        public BoxController(NomadaDbContext context)
        {
            _context = context;
        }

        // 1. Obtener Frase Aleatoria (Filtrado por Gimnasio)
        [HttpGet("{gymCode}/frase-aleatoria")]
        public async Task<IActionResult> GetFraseAleatoria(string gymCode)
        {
            // Contamos cuántas frases existen en la base de datos de ESTA sucursal
            int totalFrases = await _context.FrasesMotivacionales
                .Where(f => f.GymCode == gymCode)
                .CountAsync();

            if (totalFrases == 0)
            {
                // Frase por defecto por si la tabla está vacía
                return Ok(new FraseDto { Texto = "Entrena hoy para ser mejor que ayer.", Autor = "Nómada" });
            }

            // Generamos un índice aleatorio
            int indiceAleatorio = new Random().Next(0, totalFrases);

            // Traemos solo esa frase específica
            var frase = await _context.FrasesMotivacionales
                .Where(f => f.GymCode == gymCode)
                .Skip(indiceAleatorio)
                .Select(f => new FraseDto
                {
                    Id = f.Id,
                    Texto = f.Texto,
                    Autor = f.Autor
                })
                .FirstOrDefaultAsync();

            return Ok(frase);
        }

        // 2. Obtener Banner de Alerta de Suscripción para el Home (Filtrado por Gimnasio)
        [HttpGet("{gymCode}/alerta-suscripcion/{usuarioId}")]
        public async Task<IActionResult> GetAlertaSuscripcion(string gymCode, Guid usuarioId)
        {
            var sub = await _context.Suscripciones
                .FirstOrDefaultAsync(s => s.UsuarioId == usuarioId && s.GymCode == gymCode && s.Activa);

            if (sub == null)
                return Ok(new AlertaSuscripcionDto { Mostrar = false });

            var alerta = new AlertaSuscripcionDto { Mostrar = false };
            DateTime hoyMexico = DateTime.UtcNow.Add(TimeSpan.FromHours(-6)).Date;

            if (sub.TipoSuscripcion == "PaqueteClases")
            {
                if (sub.ClasesRestantes == 2)
                {
                    alerta.Mostrar = true;
                    alerta.NivelRiesgo = "Warning";
                    alerta.Mensaje = "Te quedan 2 clases en tu paquete.";
                }
                else if (sub.ClasesRestantes <= 0)
                {
                    alerta.Mostrar = true;
                    alerta.NivelRiesgo = "Danger";
                    alerta.Mensaje = "Última clase tomada. Renueva hoy para no perder tu acceso.";
                }
            }
            else if (sub.FechaFin.HasValue)
            {
                int diasDiferencia = (sub.FechaFin.Value.Date - hoyMexico).Days;

                if (diasDiferencia == 2 || diasDiferencia == 1)
                {
                    alerta.Mostrar = true;
                    alerta.NivelRiesgo = "Warning";
                    alerta.Mensaje = $"Tu plan vence en {diasDiferencia} día(s).";
                }
                else if (diasDiferencia == 0)
                {
                    alerta.Mostrar = true;
                    alerta.NivelRiesgo = "Danger";
                    alerta.Mensaje = "Tu plan vence HOY. Evita interrupciones en tu servicio.";
                }
                else if (diasDiferencia < 0)
                {
                    alerta.Mostrar = true;
                    alerta.NivelRiesgo = "Danger";
                    alerta.Mensaje = $"Plan vencido. Tienes {Math.Abs(diasDiferencia)} día(s) de retraso.";
                }
            }

            return Ok(alerta);
        }
    }
}