using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using DriveNow.Data;
using DriveNow.Models;

namespace DriveNow.Services
{
    public class ReservaService : IReservaService
    {
        public async Task<List<Reserva>> ObtenerReservasAsync()
        {
            using var context = new DriveNowDbContext();
            return await context.Reservas
                .Include(r => r.Cliente)
                .Include(r => r.Empleado)
                .Include(r => r.Vehiculo!)
                    .ThenInclude(v => v.Categoria)
                .Include(r => r.Estado)
                .OrderByDescending(r => r.IdReserva)
                .ToListAsync();
        }

        public async Task<bool> VehiculoDisponibleAsync(int idVehiculo, DateTime inicio, DateTime fin)
        {
            using var context = new DriveNowDbContext();
            var vehiculo = await context.Vehiculos
                .Include(v => v.Estado)
                .FirstOrDefaultAsync(v => v.IdVehiculo == idVehiculo);

            if (vehiculo == null || !vehiculo.Activo || vehiculo.Estado?.Nombre == "En Taller" || vehiculo.Estado?.Nombre == "Mantenimiento")
                return false;

            bool ocupado = await context.Reservas.AnyAsync(r =>
                r.IdVehiculo == idVehiculo &&
                r.Estado != null && r.Estado.Nombre != "Cancelada" &&
                inicio <= r.FechaHasta && fin >= r.FechaDesde);

            return !ocupado;
        }

        public async Task<Reserva> CrearReservaAsync(Reserva reserva)
        {
            using var context = new DriveNowDbContext();
            context.Reservas.Add(reserva);
            await context.SaveChangesAsync();
            return reserva;
        }

        public async Task<bool> CancelarReservaAsync(int idReserva)
        {
            using var context = new DriveNowDbContext();
            var reserva = await context.Reservas.FindAsync(idReserva);
            if (reserva == null) return false;

            var estadoCancelada = await context.Estados.FirstOrDefaultAsync(e => e.Nombre == "Cancelada");
            if (estadoCancelada == null) return false;

            reserva.IdEstado = estadoCancelada.IdEstado;
            await context.SaveChangesAsync();
            return true;
        }
    }
}
