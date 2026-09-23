using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using DriveNow.Models;

namespace DriveNow.Data
{
    public class DriveNowDbContext : DbContext
    {
        public DriveNowDbContext() { }

        public DriveNowDbContext(DbContextOptions<DriveNowDbContext> options) : base(options) { }

        public DbSet<Rol> Roles { get; set; }
        public DbSet<Estado> Estados { get; set; }
        public DbSet<Categoria> Categorias { get; set; }
        public DbSet<Cliente> Clientes { get; set; }
        public DbSet<Empleado> Empleados { get; set; }
        public DbSet<Vehiculo> Vehiculos { get; set; }
        public DbSet<FotoVehiculo> FotosVehiculo { get; set; }
        public DbSet<Reserva> Reservas { get; set; }
        public DbSet<Alquiler> Alquileres { get; set; }
        public DbSet<Dano> Danos { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                var builder = new ConfigurationBuilder()
                    .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);

                var configuration = builder.Build();
                string? connectionString = configuration.GetConnectionString("DefaultConnection");

                // Respaldo de seguridad en caso de que no se encuentre el archivo de configuración
                if (string.IsNullOrWhiteSpace(connectionString))
                {
                    connectionString = "Server=(localdb)\\MSSQLLocalDB;Database=DriveNow_DB;Trusted_Connection=True;TrustServerCertificate=True;";
                }

                optionsBuilder.UseSqlServer(connectionString);
            }
        }
    }
}