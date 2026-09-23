using System.Collections.Generic;
using System.Threading.Tasks;
using DriveNow.Models;

namespace DriveNow.Services
{
    public interface IAlquilerService
    {
        Task<List<Alquiler>> ObtenerHistorialAsync();
        Task<Reserva?> BuscarReservaParaEntregaAsync(string criterio);
        Task<Alquiler> RegistrarEntregaAsync(int idReserva, decimal combustible, int kmSalida, int? idEmpleado);
        Task<Alquiler?> BuscarAlquilerParaDevolucionAsync(string criterio);
        Task<Alquiler> RegistrarDevolucionAsync(int idAlquiler, decimal combustibleEntrada, int kmEntrada, string? fotoDanioRuta, int? idEmpleado);
    }
}
