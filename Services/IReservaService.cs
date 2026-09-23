using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DriveNow.Models;

namespace DriveNow.Services
{
    public interface IReservaService
    {
        Task<List<Reserva>> ObtenerReservasAsync();
        Task<bool> VehiculoDisponibleAsync(int idVehiculo, DateTime inicio, DateTime fin);
        Task<Reserva> CrearReservaAsync(Reserva reserva);
        Task<bool> CancelarReservaAsync(int idReserva);
    }
}
