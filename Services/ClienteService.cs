using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using DriveNow.Data;
using DriveNow.Models;

namespace DriveNow.Services
{
    public class ClienteService : IClienteService
    {
        public async Task<List<Cliente>> ObtenerActivosAsync()
        {
            using var context = new DriveNowDbContext();
            return await context.Clientes
                .Where(c => c.Activo)
                .OrderByDescending(c => c.IdCliente)
                .ToListAsync();
        }

        public async Task<Cliente?> BuscarPorIdAsync(int idCliente)
        {
            using var context = new DriveNowDbContext();
            return await context.Clientes.FindAsync(idCliente);
        }

        public async Task<Cliente?> BuscarPorDniAsync(string dni)
        {
            using var context = new DriveNowDbContext();
            string normalizado = dni.Trim().ToUpper();
            return await context.Clientes
                .FirstOrDefaultAsync(c => c.Activo && (c.Dni == normalizado || (c.Nombre + " " + c.Apellido).Contains(normalizado)));
        }

        public async Task<bool> ExisteDniAsync(string dni, int? idExcluir = null)
        {
            using var context = new DriveNowDbContext();
            string normalizado = dni.Trim().ToUpper();
            return await context.Clientes.AnyAsync(c =>
                c.Activo &&
                c.Dni == normalizado &&
                (!idExcluir.HasValue || c.IdCliente != idExcluir.Value));
        }

        public async Task<bool> ExisteLicenciaAsync(string licencia, int? idExcluir = null)
        {
            using var context = new DriveNowDbContext();
            string normalizado = licencia.Trim().ToUpper();
            return await context.Clientes.AnyAsync(c =>
                c.Activo &&
                c.NroLicencia == normalizado &&
                (!idExcluir.HasValue || c.IdCliente != idExcluir.Value));
        }

        public async Task GuardarAsync(Cliente cliente)
        {
            using var context = new DriveNowDbContext();
            if (cliente.IdCliente == 0)
            {
                context.Clientes.Add(cliente);
            }
            else
            {
                var db = await context.Clientes.FindAsync(cliente.IdCliente);
                if (db != null)
                {
                    db.Nombre = cliente.Nombre.Trim();
                    db.Apellido = cliente.Apellido.Trim();
                    db.Dni = cliente.Dni.Trim().ToUpper();
                    db.Telefono = string.IsNullOrWhiteSpace(cliente.Telefono) ? null : cliente.Telefono.Trim();
                    db.Correo = string.IsNullOrWhiteSpace(cliente.Correo) ? null : cliente.Correo.Trim();
                    db.NroLicencia = cliente.NroLicencia.Trim().ToUpper();
                    db.VencimientoLicencia = cliente.VencimientoLicencia;
                    db.Domicilio = string.IsNullOrWhiteSpace(cliente.Domicilio) ? null : cliente.Domicilio.Trim();
                    db.Localidad = string.IsNullOrWhiteSpace(cliente.Localidad) ? null : cliente.Localidad.Trim();
                    db.Provincia = string.IsNullOrWhiteSpace(cliente.Provincia) ? null : cliente.Provincia.Trim();
                    db.FechaNacimiento = cliente.FechaNacimiento;
                    db.ContactoEmergenciaNombre = string.IsNullOrWhiteSpace(cliente.ContactoEmergenciaNombre) ? null : cliente.ContactoEmergenciaNombre.Trim();
                    db.ContactoEmergenciaTelefono = string.IsNullOrWhiteSpace(cliente.ContactoEmergenciaTelefono) ? null : cliente.ContactoEmergenciaTelefono.Trim();
                    db.Nacionalidad = string.IsNullOrWhiteSpace(cliente.Nacionalidad) ? null : cliente.Nacionalidad.Trim();
                }
            }
            await context.SaveChangesAsync();
        }

        public async Task<bool> DarDeBajaAsync(int idCliente)
        {
            using var context = new DriveNowDbContext();
            var db = await context.Clientes.FindAsync(idCliente);
            if (db == null) return false;

            db.Activo = false;
            await context.SaveChangesAsync();
            return true;
        }

        public async Task<List<Cliente>> ObtenerSegunFiltroAsync(string filtro)
        {
            using var context = new DriveNowDbContext();
            var query = context.Clientes.AsQueryable();

            if (filtro == "Inactivos")
            {
                query = query.Where(c => !c.Activo);
            }
            else if (filtro == "Todos")
            {
                // Incluye tanto activos como inactivos
            }
            else
            {
                // Por defecto: solo activos
                query = query.Where(c => c.Activo);
            }

            return await query.OrderByDescending(c => c.IdCliente).ToListAsync();
        }

        public async Task<bool> ReactivarAsync(int idCliente)
        {
            using var context = new DriveNowDbContext();
            var db = await context.Clientes.FindAsync(idCliente);
            if (db == null) return false;

            db.Activo = true;
            await context.SaveChangesAsync();
            return true;
        }
    }
}
