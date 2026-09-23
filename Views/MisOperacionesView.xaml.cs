using DriveNow.Data;
using DriveNow.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace DriveNow.Views
{
    public partial class MisOperacionesView : UserControl
    {
        public MisOperacionesView()
        {
            InitializeComponent();
            Loaded += async (s, e) => await CargarMisMovimientosAsync();
        }

        private async Task CargarMisMovimientosAsync()
        {
            if (LoginView.EmpleadoActual == null) return;

            int idActual = LoginView.EmpleadoActual.IdEmpleado;
            lblTituloGestion.Text = $"👤 Historial de Operaciones: {LoginView.EmpleadoActual.NombreCompleto}";

            try
            {
                using var context = new DriveNowDbContext();

                // 1. Reservas creadas por este operador
                var misReservas = await context.Reservas
                    .Include(r => r.Cliente)
                    .Include(r => r.Vehiculo)
                    .Include(r => r.Estado)
                    .Where(r => r.IdEmpleado == idActual)
                    .OrderByDescending(r => r.IdReserva)
                    .ToListAsync();

                dgvMisReservas.ItemsSource = misReservas;
                lblTotalMisReservas.Text = misReservas.Count.ToString();

                // 2. Alquileres donde este operador entregó O recibió
                var misAlquileres = await context.Alquileres
                    .Include(a => a.Reserva!)
                        .ThenInclude(r => r.Cliente)
                    .Include(a => a.Reserva!)
                        .ThenInclude(r => r.Vehiculo)
                    .Include(a => a.EmpleadoEntrega)     // <-- Trae nombre de quien entregó
                    .Include(a => a.EmpleadoDevolucion) // <-- Trae nombre de quien recibió
                    .Where(a => a.IdEmpleadoEntrega == idActual || a.IdEmpleadoDevolucion == idActual)
                    .OrderByDescending(a => a.IdAlquiler)
                    .ToListAsync();

                dgvMisAlquileres.ItemsSource = misAlquileres;
                lblTotalMisEntregas.Text = misAlquileres.Count(a => a.IdEmpleadoEntrega == idActual).ToString();
                lblTotalMisDevoluciones.Text = misAlquileres.Count(a => a.IdEmpleadoDevolucion == idActual).ToString();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cargar tus operaciones: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}