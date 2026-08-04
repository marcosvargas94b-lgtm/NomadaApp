using Microsoft.EntityFrameworkCore;
using Nomada.Shared.Models;
using Nomada.Shared.Entities;

namespace Nomada.API.Data
{
    public class NomadaDbContext : DbContext
    {
        public NomadaDbContext(DbContextOptions<NomadaDbContext> options) : base(options)
        {
        }

        public DbSet<Usuario> Usuarios { get; set; }
        public DbSet<Rol> Roles { get; set; }
        public DbSet<Suscripcion> Suscripciones { get; set; }
        public DbSet<Ingreso> Ingresos { get; set; }
        public DbSet<Asistencia> Asistencias { get; set; }
        public DbSet<Permiso> Permisos { get; set; }
        public DbSet<UsuarioPermiso> UsuarioPermisos { get; set; }
        public DbSet<Post> Posts { get; set; }
        public DbSet<Like> Likes { get; set; }
        public DbSet<Notificacion> Notificaciones { get; set; }
        public DbSet<FraseMotivacional> FrasesMotivacionales { get; set; }
        public DbSet<HorarioClase> HorariosClases { get; set; }
        public DbSet<Reserva> Reservas { get; set; }
        public DbSet<ConfiguracionBox> ConfiguracionBox { get; set; }
        public DbSet<WodGeneral> WodsGenerales { get; set; }
        public DbSet<WodSeccion> WodsSecciones { get; set; }
        public DbSet<CatalogoEjercicio> CatalogoEjercicios { get; set; }
        public DbSet<WodEjercicio> WodEjercicios { get; set; }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Siempre es buena práctica llamar al método base
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Suscripcion>().ToTable("Suscripciones");
            modelBuilder.Entity<Ingreso>().ToTable("Ingresos");
            modelBuilder.Entity<Asistencia>().ToTable("Asistencias");
            modelBuilder.Entity<Permiso>().ToTable("Permisos");
            modelBuilder.Entity<CatalogoEjercicio>().ToTable("CatalogoEjercicios");
            modelBuilder.Entity<WodEjercicio>().ToTable("WodEjercicios");
            modelBuilder.Entity<Post>().ToTable("Posts");
            modelBuilder.Entity<Like>().ToTable("Likes");
            modelBuilder.Entity<Notificacion>().ToTable("Notificaciones");
            modelBuilder.Entity<FraseMotivacional>().ToTable("FrasesMotivacionales");
            modelBuilder.Entity<HorarioClase>().ToTable("HorariosClases");
            modelBuilder.Entity<Reserva>().ToTable("Reservas");
            modelBuilder.Entity<ConfiguracionBox>().ToTable("ConfiguracionBox");
            modelBuilder.Entity<WodGeneral>().ToTable("WodsGenerales");
            modelBuilder.Entity<WodSeccion>().ToTable("WodsSecciones");

            modelBuilder.Entity<ConfiguracionBox>()
                .Property(c => c.AforoMaximo)
                .HasDefaultValue(20);

            modelBuilder.Entity<Like>()
                .HasIndex(l => new { l.PostId, l.UsuarioId })
                .IsUnique();

            modelBuilder.Entity<Rol>(entity =>
            {
                entity.ToTable("Roles");
                entity.HasKey(e => e.Id);
            });

            modelBuilder.Entity<UsuarioPermiso>()
                .ToTable("UsuarioPermisos")
                .HasKey(up => new { up.UsuarioId, up.PermisoId });

            // (Opcional, pero buena práctica) Especificar los tipos de datos decimales
            modelBuilder.Entity<Ingreso>()
                .Property(i => i.Monto)
                .HasColumnType("decimal(10,2)");

            // Mapeo explícito de la tabla Usuarios
            modelBuilder.Entity<Usuario>(entity =>
            {
                entity.ToTable("Usuarios");
                entity.HasKey(e => e.Id);

                // Le decimos a C# que el Id en SQL se genera solo (el NEWID() que le pusimos)
                entity.Property(e => e.Id).HasDefaultValueSql("NEWID()");

                entity.Property(e => e.EstatusAprobacion)
                      .HasMaxLength(20)
                      .HasDefaultValue("En Espera");

                // Configuramos la relación (Un Usuario tiene Un Rol)
                entity.HasOne(d => d.Rol)
                      .WithMany()
                      .HasForeignKey(d => d.RolId)
                      .OnDelete(DeleteBehavior.Restrict); // Evita que si borras un rol, se borren los usuarios
            });
        }
    }
}