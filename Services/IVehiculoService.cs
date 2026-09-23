using System.Collections.Generic;
using System.Threading.Tasks;
using DriveNow.Models;

namespace DriveNow.Services
{
    public interface IVehiculoService
    {
        Task<List<Vehiculo>> ObtenerFlotaActivaAsync();
        Task<List<Categoria>> ObtenerCategoriasAsync();
        Task<List<Estado>> ObtenerEstadosVehiculoAsync();
        Task<bool> ExistePatenteAsync(string patente, int? idExcluir = null);
        Task GuardarAsync(Vehiculo vehiculo);
        Task<bool> DarDeBajaAsync(int idVehiculo);
    }
}
