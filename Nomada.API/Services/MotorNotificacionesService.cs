using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Nomada.API.Data;
using Nomada.Shared.Entities;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Nomada.API.Services
{
    public class MotorNotificacionesService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly TimeSpan _mexicoOffset = TimeSpan.FromHours(-6);

        public MotorNotificacionesService(IServiceScopeFactory scopeFactory)
        {
            _scopeFactory = scopeFactory;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try { await ProcesarVencimientosDiarios(); }
                catch (Exception ex) { Console.WriteLine($"Error en Notificaciones: {ex.Message}"); }

                await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
            }
        }

        private async Task ProcesarVencimientosDiarios()
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<NomadaDbContext>();

            DateTime hoyMexico = DateTime.UtcNow.Add(_mexicoOffset).Date;

            // Traemos suscripciones activas
            var suscripciones = await db.Suscripciones.Where(s => s.Activa).ToListAsync();

            foreach (var sub in suscripciones)
            {
                // 1. REGLA PARA CLASES Y DROP-IN
                if (sub.TipoSuscripcion == "PaqueteClases")
                {
                    if (sub.ClasesRestantes == 2)
                    {
                        GenerarNotificacion(db, sub.UsuarioId, "Te quedan solo 2 clases. ¡No pierdas tu racha, renueva pronto!", "AlertaPago", "/mis-pagos");
                    }
                    else if (sub.ClasesRestantes <= 0)
                    {
                        // Si se acabaron sus clases y no le hemos puesto fecha de caducidad, se la sellamos HOY
                        if (!sub.FechaFin.HasValue)
                        {
                            sub.FechaFin = DateTime.UtcNow.Date; // Empieza a correr el tiempo de gracia
                            GenerarNotificacion(db, sub.UsuarioId, "Has tomado tu última clase pagada. Tienes 2 días de gracia para renovar tu plan.", "AlertaPago", "/mis-pagos");
                        }
                    }
                }

                // 2. REGLA PARA TIEMPO (Mensual, Semanal, y Clases en periodo de gracia)
                if (sub.FechaFin.HasValue)
                {
                    DateTime fechaCorte = sub.FechaFin.Value.Add(_mexicoOffset).Date;
                    int diasDiferencia = (sub.FechaFin.Value.Date - hoyMexico).Days;

                    if (diasDiferencia == 2)
                    {
                        GenerarNotificacion(db, sub.UsuarioId, "Tu plan está a 2 días de vencer. Asegura tu lugar.", "AlertaPago", "/mis-pagos");
                    }
                    else if (diasDiferencia == 0 && sub.TipoSuscripcion != "PaqueteClases") // El de clases ya se avisó arriba
                    {
                        GenerarNotificacion(db, sub.UsuarioId, "Tu plan vence el día de HOY. Por favor pasa a renovar.", "AlertaPago", "/mis-pagos");
                    }
                    else if (diasDiferencia == -1)
                    {
                        GenerarNotificacion(db, sub.UsuarioId, "Tu plan venció ayer. Tienes un día de retraso en tu pago.", "AlertaPago", "/mis-pagos");
                    }
                    else if (diasDiferencia == -2)
                    {
                        GenerarNotificacion(db, sub.UsuarioId, "Tienes 2 días de retraso. Si pasa un día más sin pago, tu cuenta será dada de baja.", "AlertaPago", "/mis-pagos");
                    }
                    else if (diasDiferencia <= -3)
                    {
                        var usuario = await db.Usuarios.FindAsync(sub.UsuarioId);
                        if (usuario != null)
                        {
                            // APLICAMOS LA BAJA TEMPORAL
                            usuario.EstatusAprobacion = "Baja Temporal";
                            sub.Activa = false;
                        }
                    }
                }
            }

            await db.SaveChangesAsync();
        }

        private void GenerarNotificacion(NomadaDbContext db, Guid usuarioId, string mensaje, string tipo, string ruta)
        {
            bool yaNotificadoHoy = db.Notificaciones.Any(n => n.UsuarioId == usuarioId && n.Mensaje == mensaje && n.FechaCreacion >= DateTime.UtcNow.AddHours(-24));
            if (!yaNotificadoHoy)
            {
                db.Notificaciones.Add(new Notificacion { UsuarioId = usuarioId, Mensaje = mensaje, Tipo = tipo, RutaNavegacion = ruta, Leida = false, FechaCreacion = DateTime.UtcNow });
            }
        }
    }
}