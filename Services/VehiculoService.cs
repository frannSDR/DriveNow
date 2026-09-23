using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using DriveNow.Data;
using DriveNow.Models;

namespace DriveNow.Services
{
    public class VehiculoService : IVehiculoService
    {
        public async Task<List<Vehiculo>> ObtenerFlotaActivaAsync()
        {
            using var context = new DriveNowDbContext();
            return await context.Vehiculos
                .Include(v => v.Categoria)
                .Include(v => v.Estado)
                .Where(v => v.Activo)
                .OrderByDescending(v => v.IdVehiculo)
                .ToListAsync();
        }

        public async Task<List<Categoria>> ObtenerCategoriasAsync()
        {
            using var context = new DriveNowDbContext();
            return await context.Categorias
                .OrderBy(c => c.TarifaDiaria)
                .ToListAsync();
        }

        public async Task<List<Estado>> ObtenerEstadosVehiculoAsync()
        {
            using var context = new DriveNowDbContext();
            var estadosValidos = new[] { "Disponible", "Alquilado", "En Taller", "Mantenimiento" };
            return await context.Estados
                .Where(e => estadosValidos.Contains(e.Nombre))
                .ToListAsync();
        }

        public async Task<bool> ExistePatenteAsync(string patente, int? idExcluir = null)
        {
            using var context = new DriveNowDbContext();
            string normalizada = patente.Trim().ToUpper();
            return await context.Vehiculos.AnyAsync(v =>
                v.Activo &&
                v.Patente == normalizada &&
                (!idExcluir.HasValue || v.IdVehiculo != idExcluir.Value));
        }

        public async Task GuardarAsync(Vehiculo vehiculo)
        {
            using var context = new DriveNowDbContext();
            if (vehiculo.IdVehiculo == 0)
            {
                context.Vehiculos.Add(vehiculo);
            }
            else
            {
                var db = await context.Vehiculos.FindAsync(vehiculo.IdVehiculo);
                if (db != null)
                {
                    db.Patente = vehiculo.Patente.Trim().ToUpper();
                    db.Marca = vehiculo.Marca.Trim();
                    db.Modelo = vehiculo.Modelo.Trim();
                    db.Anio = vehiculo.Anio;
                    db.IdCategoria = vehiculo.IdCategoria;
                    db.IdEstado = vehiculo.IdEstado;
                }
            }
            await context.SaveChangesAsync();
        }

        public async Task<bool> DarDeBajaAsync(int idVehiculo)
        {
            using var context = new DriveNowDbContext();
            var db = await context.Vehiculos
                .Include(v => v.Estado)
                .FirstOrDefaultAsync(v => v.IdVehiculo == idVehiculo);

            if (db == null || db.Estado?.Nombre == "Alquilado" || db.Estado?.Nombre == "Reservado")
                return false;

            db.Activo = false;
            await context.SaveChangesAsync();
            return true;
        }
    }
}
