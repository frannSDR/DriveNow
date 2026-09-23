using System.Collections.Generic;
using System.Threading.Tasks;
using DriveNow.Models;

namespace DriveNow.Services
{
    public interface IClienteService
    {
        Task<List<Cliente>> ObtenerActivosAsync();
        Task<List<Cliente>> ObtenerSegunFiltroAsync(string filtro);
        Task<Cliente?> BuscarPorIdAsync(int idCliente);
        Task<Cliente?> BuscarPorDniAsync(string dni);
        Task<bool> ExisteDniAsync(string dni, int? idExcluir = null);
        Task<bool> ExisteLicenciaAsync(string licencia, int? idExcluir = null);
        Task GuardarAsync(Cliente cliente);
        Task<bool> DarDeBajaAsync(int idCliente);
        Task<bool> ReactivarAsync(int idCliente);
    }
}
