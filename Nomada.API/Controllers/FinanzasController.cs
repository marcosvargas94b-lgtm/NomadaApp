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
    public class FinanzasController : ControllerBase
    {
        private readonly NomadaDbContext _context;

        public FinanzasController(NomadaDbContext context)
        {
            _context = context;
        }

        // 1. Obtener Totales y Gráfica del Año (Filtrado por Sucursal)
        [HttpGet("{gymCode}/resumen/{anio}")]
        public async Task<IActionResult> GetResumen(string gymCode, int anio)
        {
            var ingresosAnio = await _context.Ingresos
                .Where(i => i.GymCode == gymCode && i.FechaCobro.Year == anio)
                .ToListAsync();

            var dto = new ResumenFinanzasDto();
            dto.TotalAnio = ingresosAnio.Sum(i => i.Monto);

            // Calculamos el mes actual
            dto.TotalMesActual = ingresosAnio
                .Where(i => i.FechaCobro.Month == DateTime.UtcNow.Month)
                .Sum(i => i.Monto);

            // Llenamos el arreglo de 12 meses para la gráfica
            for (int i = 1; i <= 12; i++)
            {
                dto.IngresosPorMes[i - 1] = ingresosAnio.Where(x => x.FechaCobro.Month == i).Sum(x => x.Monto);
            }

            return Ok(dto);
        }

        // 2. Obtener Historial (Lista y Filtros de Sucursal)
        [HttpGet("{gymCode}/historial")]
        public async Task<IActionResult> GetHistorial(string gymCode, [FromQuery] int mes, [FromQuery] int anio)
        {
            var query = _context.Ingresos.Where(i => i.GymCode == gymCode && i.FechaCobro.Year == anio);

            // Si el mes es mayor a 0, filtramos por ese mes. Si es 0, trae todo el año.
            if (mes > 0)
            {
                query = query.Where(i => i.FechaCobro.Month == mes);
            }

            var lista = await query
                .OrderByDescending(i => i.FechaCobro)
                .Select(i => new IngresoDto
                {
                    Id = i.Id,
                    Monto = i.Monto,
                    FechaCobro = i.FechaCobro,
                    TipoCobro = i.TipoCobro,
                    Descripcion = i.Descripcion,
                    Atleta = _context.Usuarios.Where(u => u.Id == i.UsuarioId).Select(u => u.Nombre + " " + u.ApellidoPaterno).FirstOrDefault() ?? "Desconocido",
                    Coach = _context.Usuarios.Where(u => u.Id == i.RecibidoPorId).Select(u => u.Nombre).FirstOrDefault() ?? "Admin"
                })
                .ToListAsync();

            return Ok(lista);
        }

        // 3. Obtener el historial de un atleta específico
        [HttpGet("{gymCode}/mis-pagos/{usuarioId}")]
        public async Task<IActionResult> GetMisPagos(string gymCode, Guid usuarioId)
        {
            var pagos = await _context.Ingresos
                .Where(i => i.GymCode == gymCode && i.UsuarioId == usuarioId)
                .OrderByDescending(i => i.FechaCobro)
                .Select(i => new IngresoDto
                {
                    Id = i.Id,
                    Monto = i.Monto,
                    FechaCobro = i.FechaCobro,
                    TipoCobro = i.TipoCobro,
                    Descripcion = i.Descripcion,
                    // Buscamos quién le cobró
                    Coach = _context.Usuarios
                        .Where(u => u.Id == i.RecibidoPorId)
                        .Select(u => u.Nombre)
                        .FirstOrDefault() ?? "Nómada"
                })
                .ToListAsync();

            return Ok(pagos);
        }
    }
}