using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nomada.API.Data;
using Nomada.Shared.Models;
using System;
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

        // 1. Obtener Frase Aleatoria
        [HttpGet("frase-aleatoria")]
        public async Task<IActionResult> GetFraseAleatoria()
        {
            // Contamos cuántas frases existen en la base de datos
            int totalFrases = await _context.FrasesMotivacionales.CountAsync();

            if (totalFrases == 0)
            {
                // Frase por defecto por si la tabla está vacía
                return Ok(new FraseDto { Texto = "Entrena hoy para ser mejor que ayer.", Autor = "Nómada" });
            }

            // Generamos un índice aleatorio
            int indiceAleatorio = new Random().Next(0, totalFrases);

            // Traemos solo esa frase específica
            var frase = await _context.FrasesMotivacionales
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
    }
}