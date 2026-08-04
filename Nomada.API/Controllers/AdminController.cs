using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nomada.API.Data;
using Nomada.Shared.Models;
using Nomada.Shared.Entities; // Agregamos la referencia a las Entidades
using System;
using System.Collections.Generic; // Para usar List<>
using System.Linq;
using System.Threading.Tasks;

namespace Nomada.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AdminController : ControllerBase
    {
        private readonly NomadaDbContext _context;

        public AdminController(NomadaDbContext context)
        {
            _context = context;
        }

        // 1. Obtener usuarios (Optimizado según el estatus)
        [HttpGet("usuarios/{estatus}")]
        public async Task<IActionResult> GetUsuariosPorEstatus(string estatus)
        {
            // A. Si es para la Tribu Nómada, calculamos fechas de suscripción y clases
            if (estatus == "Aprobado")
            {
                var usuarios = await _context.Usuarios
                    .Where(u => u.EstatusAprobacion == estatus)
                    .Select(u => new UsuarioAdminDto
                    {
                        Id = u.Id,
                        Nombre = u.Nombre,
                        ApellidoPaterno = u.ApellidoPaterno,
                        Correo = u.Correo,

                        RolId = u.RolId,
                        PermisosIds = _context.UsuarioPermisos
                                        .Where(up => up.UsuarioId == u.Id)
                                        .Select(up => up.PermisoId)
                                        .ToList(),

                        TipoSuscripcion = _context.Suscripciones
                            .Where(s => s.UsuarioId == u.Id && s.Activa)
                            .Select(s => s.TipoSuscripcion)
                            .FirstOrDefault(),

                        DiasRestantes = _context.Suscripciones
                            .Where(s => s.UsuarioId == u.Id && s.Activa && s.FechaFin != null)
                            .Select(s => (int?)EF.Functions.DateDiffDay(DateTime.UtcNow, s.FechaFin))
                            .FirstOrDefault(),

                        ClasesRestantes = _context.Suscripciones
                            .Where(s => s.UsuarioId == u.Id && s.Activa)
                            .Select(s => s.ClasesRestantes)
                            .FirstOrDefault()
                    })
                    .ToListAsync();

                return Ok(usuarios);
            }
            // B. Si es para Aceptar/Rechazar, hacemos una consulta ultra ligera
            else
            {
                var usuarios = await _context.Usuarios
                    .Where(u => u.EstatusAprobacion == estatus)
                    .Select(u => new UsuarioAdminDto
                    {
                        Id = u.Id,
                        Nombre = u.Nombre,
                        ApellidoPaterno = u.ApellidoPaterno,
                        Correo = u.Correo
                        // No calculamos fechas aquí para ahorrar recursos
                    })
                    .ToListAsync();

                return Ok(usuarios);
            }
        }

        // 2. Lógica Matemática para Pagos Adelantados, Cortes y SUSTITUCIÓN DE PLANES
        [HttpPost("cobrar")]
        public async Task<IActionResult> RegistrarCobro([FromBody] RegistrarCobroRequest request)
        {
            // A. Registrar el dinero en caja (IngresoDto)
            var ingreso = new Ingreso
            {
                UsuarioId = request.AtletaId,
                RecibidoPorId = request.CoachId,
                TipoCobro = request.TipoCobro,
                Monto = request.Monto,
                FechaCobro = DateTime.UtcNow,
                Descripcion = request.Descripcion
            };
            _context.Ingresos.Add(ingreso);

            // B. Actualizar las fechas de corte o clases (Solo si NO es Clase Especial)
            if (request.TipoCobro != "Especial")
            {
                // [NÓMADA FIX] Lógica de Sustitución Automática de Planes

                // 1. Identificamos la suscripción activa principal o creamos una nueva
                var subActual = await _context.Suscripciones
                    .FirstOrDefaultAsync(s => s.UsuarioId == request.AtletaId && s.Activa);

                if (subActual == null)
                {
                    subActual = new Suscripcion
                    {
                        UsuarioId = request.AtletaId,
                        Activa = true,
                        FechaInicio = DateTime.UtcNow
                    };
                    _context.Suscripciones.Add(subActual);
                }

                // --- BLOQUE DE SEGURIDAD: SUSTITUCIÓN DE PLAN ---

                // 2. Buscamos CUALQUIER OTRA suscripción activa (error del sistema) y la desactivamos
                var otrasSubsActivas = await _context.Suscripciones
                    .Where(s => s.UsuarioId == request.AtletaId && s.Activa && s.Id != subActual.Id)
                    .ToListAsync();

                foreach (var otraSub in otrasSubsActivas)
                {
                    otraSub.Activa = false;
                }

                // 3. Si el atleta cambia drásticamente de plan (ejemplo: de Clases a Mensual),
                // reseteamos los contadores de clases para que no se sumen al nuevo plan de tiempo.
                if (subActual.TipoSuscripcion == "PaqueteClases" &&
                    (request.TipoCobro == "Mensual" || request.TipoCobro == "Semanal"))
                {
                    subActual.ClasesRestantes = null;
                }

                // Actualizamos el tipo al nuevo cobro efectuado
                subActual.TipoSuscripcion = request.TipoCobro;


                // C. Lógica Matemática de Fechas y Clases (Heredada y Verificada)
                if (request.TipoCobro == "Mensual" || request.TipoCobro == "Semanal")
                {
                    DateTime fechaBase = (subActual.FechaFin.HasValue && subActual.FechaFin > DateTime.UtcNow)
                                         ? subActual.FechaFin.Value
                                         : (request.FechaPagoMensual ?? DateTime.UtcNow);

                    if (request.TipoCobro == "Mensual")
                    {
                        subActual.FechaFin = fechaBase.AddMonths(1);
                    }
                    else
                    {
                        subActual.FechaFin = fechaBase.AddDays((request.NumeroSemanas ?? 1) * 7);
                    }

                    // Aseguramos que los planes de tiempo no tengan clases restantes
                    subActual.ClasesRestantes = null;
                }
                else if (request.TipoCobro == "PaqueteClases")
                {
                    // Al recargar clases, heredamos las restantes (recargo de paquete)
                    // y limpiamos FechaFin (que pudo haber sellado el motor de notificaciones).
                    subActual.ClasesRestantes = (subActual.ClasesRestantes ?? 0) + (request.NumeroClases ?? 1);
                    subActual.FechaFin = null;
                }
            }

            // D. Si el usuario estaba dado de baja temporal, al pagar lo reactivamos
            var usuario = await _context.Usuarios.FindAsync(request.AtletaId);
            if (usuario != null && usuario.EstatusAprobacion == "Baja Temporal")
            {
                usuario.EstatusAprobacion = "Aprobado";
            }

            await _context.SaveChangesAsync();
            return Ok();
        }

        // 3. Actualizar el estatus masivamente (Aceptar o Rechazar)
        [HttpPut("usuarios/estatus")]
        public async Task<IActionResult> ActualizarEstatus([FromBody] ActualizarEstatusRequest request)
        {
            var usuarios = await _context.Usuarios
                .Where(u => request.UsuarioIds.Contains(u.Id))
                .ToListAsync();

            foreach (var user in usuarios)
            {
                user.EstatusAprobacion = request.NuevoEstatus;
            }

            await _context.SaveChangesAsync();
            return Ok();
        }

        // 4. Eliminar usuario permanentemente (Botón de basura)
        [HttpDelete("usuarios/{id}")]
        public async Task<IActionResult> EliminarUsuario(Guid id)
        {
            var usuario = await _context.Usuarios.FindAsync(id);
            if (usuario == null) return NotFound();

            _context.Usuarios.Remove(usuario);
            await _context.SaveChangesAsync();

            return Ok();
        }

        // --- MÓDULO DE STAFF Y PERMISOS ---

        // 1. Obtener el catálogo de permisos disponibles
        [HttpGet("permisos-catalogo")]
        public async Task<IActionResult> GetCatologoPermisos()
        {
            var permisos = await _context.Permisos
                .Select(p => new PermisoDto { Id = p.Id, Nombre = p.Nombre })
                .ToListAsync();
            return Ok(permisos);
        }

        // 2. Obtener los nombres de los permisos de un usuario específico (Para los Candados en la App)
        [HttpGet("mis-permisos/{usuarioId}")]
        public async Task<IActionResult> GetMisPermisos(Guid usuarioId)
        {
            var permisos = await _context.UsuarioPermisos
                .Where(up => up.UsuarioId == usuarioId)
                .Join(_context.Permisos, up => up.PermisoId, p => p.Id, (up, p) => p.Nombre)
                .ToListAsync();
            return Ok(permisos);
        }

        // 3. Convertir Atleta a Coach (o viceversa) y guardar sus permisos
        [HttpPut("staff/roles")]
        public async Task<IActionResult> ActualizarRolYPermisos([FromBody] AsignarRolPermisoRequest request)
        {
            var user = await _context.Usuarios.FindAsync(request.UsuarioId);
            if (user == null) return NotFound();

            // Actualizamos el Rol (2 = Coach, 3 = Atleta)
            user.RolId = request.RolId;

            // Limpiamos los permisos anteriores para evitar duplicados
            var permisosViejos = _context.UsuarioPermisos.Where(up => up.UsuarioId == request.UsuarioId);
            _context.UsuarioPermisos.RemoveRange(permisosViejos);

            // Si lo convertimos en Coach y le mandamos permisos, los guardamos
            if (request.RolId == 2 && request.PermisosIds.Any())
            {
                var nuevosPermisos = request.PermisosIds.Select(pid => new UsuarioPermiso
                {
                    UsuarioId = request.UsuarioId,
                    PermisoId = pid
                });
                _context.UsuarioPermisos.AddRange(nuevosPermisos);
            }

            await _context.SaveChangesAsync();
            return Ok();
        }
    }
}