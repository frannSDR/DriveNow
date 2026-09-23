using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using DriveNow.Data;
using DriveNow.Models;

namespace DriveNow.Services
{
    public class AlquilerService : IAlquilerService
    {
        private const decimal TarifaPorDefecto = 25000m;

        public async Task<List<Alquiler>> ObtenerHistorialAsync()
        {
            using var context = new DriveNowDbContext();
            return await context.Alquileres
                .Include(a => a.Reserva!)
                    .ThenInclude(r => r.Cliente)
                .Include(a => a.Reserva!)
                    .ThenInclude(r => r.Vehiculo!)
                        .ThenInclude(v => v.Categoria)
                .Include(a => a.EmpleadoEntrega)
                .Include(a => a.EmpleadoDevolucion)
                .Include(a => a.Danos)
                .OrderByDescending(a => a.IdAlquiler)
                .ToListAsync();
        }

        public async Task<Reserva?> BuscarReservaParaEntregaAsync(string criterio)
        {
            using var context = new DriveNowDbContext();
            string busqueda = criterio.Trim();
            return await context.Reservas
                .Include(r => r.Cliente)
                .Include(r => r.Vehiculo!)
                    .ThenInclude(v => v.Categoria)
                .Include(r => r.Alquiler)
                .Include(r => r.Estado)
                .FirstOrDefaultAsync(r =>
                    r.Alquiler == null &&
                    r.Estado != null && r.Estado.Nombre != "Cancelada" &&
                    ((r.Cliente != null && r.Cliente.Dni == busqueda) || r.IdReserva.ToString() == busqueda.Replace("RES-", "").TrimStart('0')));
        }

        public async Task<Alquiler> RegistrarEntregaAsync(int idReserva, decimal combustible, int kmSalida, int? idEmpleado)
        {
            if (combustible < 25 || combustible > 100)
                throw new ArgumentOutOfRangeException(nameof(combustible), "El combustible inicial debe ser un porcentaje entre 25% y 100%.");

            if (kmSalida < 0)
                throw new ArgumentOutOfRangeException(nameof(kmSalida), "El kilometraje de salida no puede ser negativo.");

            using var context = new DriveNowDbContext();
            var reserva = await context.Reservas
                .Include(r => r.Vehiculo!)
                    .ThenInclude(v => v.Categoria)
                .FirstOrDefaultAsync(r => r.IdReserva == idReserva);

            if (reserva == null)
                throw new InvalidOperationException("Reserva no encontrada.");

            var tarifaDiaria = reserva.Vehiculo?.Categoria?.TarifaDiaria ?? TarifaPorDefecto;
            int dias = (reserva.FechaHasta.Date - reserva.FechaDesde.Date).Days;
            if (dias <= 0) dias = 1;
            decimal costoBase = tarifaDiaria * dias;

            var nuevoAlquiler = new Alquiler
            {
                IdReserva = idReserva,
                FechaEntregaReal = DateTime.Now,
                CombustibleSalida = combustible,
                KilometrajeSalida = kmSalida,
                CostoTotal = costoBase,
                MontoMora = 0,
                IdEmpleadoEntrega = idEmpleado
            };

            var estadoAlquilado = await context.Estados.FirstOrDefaultAsync(est => est.Nombre == "Alquilado");
            var vehiculoDb = await context.Vehiculos.FindAsync(reserva.IdVehiculo);
            if (vehiculoDb != null && estadoAlquilado != null)
            {
                vehiculoDb.IdEstado = estadoAlquilado.IdEstado;
            }

            context.Alquileres.Add(nuevoAlquiler);
            await context.SaveChangesAsync();
            return nuevoAlquiler;
        }

        public async Task<Alquiler?> BuscarAlquilerParaDevolucionAsync(string criterio)
        {
            using var context = new DriveNowDbContext();
            string busqueda = criterio.Trim();
            return await context.Alquileres
                .Include(a => a.Reserva!)
                    .ThenInclude(r => r.Cliente)
                .Include(a => a.Reserva!)
                    .ThenInclude(r => r.Vehiculo!)
                        .ThenInclude(v => v.Categoria)
                .FirstOrDefaultAsync(a =>
                    a.FechaEntregaDevolucion == null &&
                    a.Reserva != null && a.Reserva.Vehiculo != null &&
                    (a.Reserva.Vehiculo.Patente == busqueda || a.IdAlquiler.ToString() == busqueda.Replace("ALQ-", "").TrimStart('0')));
        }

        public async Task<Alquiler> RegistrarDevolucionAsync(int idAlquiler, decimal combustibleEntrada, int kmEntrada, string? fotoDanioRuta, int? idEmpleado)
        {
            if (combustibleEntrada < 0 || combustibleEntrada > 100)
                throw new ArgumentOutOfRangeException(nameof(combustibleEntrada), "El nivel de combustible de retorno debe ser un porcentaje entre 0% y 100%.");

            using var context = new DriveNowDbContext();
            var alquilerDb = await context.Alquileres
                .Include(a => a.Reserva!)
                    .ThenInclude(r => r.Vehiculo!)
                        .ThenInclude(v => v.Categoria)
                .FirstOrDefaultAsync(a => a.IdAlquiler == idAlquiler);

            if (alquilerDb == null)
                throw new InvalidOperationException("Alquiler no encontrado.");

            if (kmEntrada < alquilerDb.KilometrajeSalida)
                throw new InvalidOperationException($"El kilometraje final ({kmEntrada} km) no puede ser menor al inicial ({alquilerDb.KilometrajeSalida} km).");

            alquilerDb.FechaEntregaDevolucion = DateTime.Now;
            alquilerDb.KilometrajeEntrada = kmEntrada;
            alquilerDb.CombustibleEntrada = combustibleEntrada;
            alquilerDb.IdEmpleadoDevolucion = idEmpleado;

            if (alquilerDb.Reserva != null && alquilerDb.FechaEntregaDevolucion.Value.Date > alquilerDb.Reserva.FechaHasta.Date)
            {
                int diasRetraso = (alquilerDb.FechaEntregaDevolucion.Value.Date - alquilerDb.Reserva.FechaHasta.Date).Days;
                decimal tarifaDiaria = alquilerDb.Reserva.Vehiculo?.Categoria?.TarifaDiaria ?? TarifaPorDefecto;
                alquilerDb.MontoMora = diasRetraso * (tarifaDiaria * 1.5m);
            }

            if (!string.IsNullOrEmpty(fotoDanioRuta))
            {
                string rutaFotoPersistir = fotoDanioRuta;
                try
                {
                    string carpetaEvidencias = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Evidencias");
                    if (!Directory.Exists(carpetaEvidencias))
                    {
                        Directory.CreateDirectory(carpetaEvidencias);
                    }
                    string extension = Path.GetExtension(fotoDanioRuta);
                    string nombreArchivo = $"DANO_{alquilerDb.CodigoAlquiler}_{DateTime.Now:yyyyMMdd_HHmmss}{extension}";
                    string destino = Path.Combine(carpetaEvidencias, nombreArchivo);
                    File.Copy(fotoDanioRuta, destino, true);
                    rutaFotoPersistir = destino;
                }
                catch
                {
                    // Preservar la ruta original si falla la copia
                }

                var nuevoDano = new Dano
                {
                    IdAlquiler = alquilerDb.IdAlquiler,
                    Zona = "Carrocería / General",
                    Descripcion = "Daño detectado en la inspección de retorno",
                    EvidenciaFotografica = rutaFotoPersistir
                };
                context.Danos.Add(nuevoDano);
            }

            string estadoDestino = !string.IsNullOrEmpty(fotoDanioRuta) ? "En Taller" : "Disponible";
            var estadoObj = await context.Estados.FirstOrDefaultAsync(est => est.Nombre == estadoDestino);
            if (alquilerDb.Reserva?.Vehiculo != null && estadoObj != null)
            {
                var vehiculo = await context.Vehiculos.FindAsync(alquilerDb.Reserva.Vehiculo.IdVehiculo);
                if (vehiculo != null) vehiculo.IdEstado = estadoObj.IdEstado;
            }

            await context.SaveChangesAsync();
            return alquilerDb;
        }
    }
}
