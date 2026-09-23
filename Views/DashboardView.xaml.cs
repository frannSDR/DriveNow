using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.EntityFrameworkCore;
using DriveNow.Data;
using DriveNow.Models;

namespace DriveNow.Views
{
    public class UltimoAlquilerDto
    {
        public string CodigoAlquiler { get; set; } = string.Empty;
        public string Cliente { get; set; } = string.Empty;
        public string Vehiculo { get; set; } = string.Empty;
        public DateTime FechaSalida { get; set; }
        public string EstadoTexto { get; set; } = string.Empty;
    }

    public partial class DashboardView : UserControl
    {
        public DashboardView()
        {
            InitializeComponent();
            Loaded += async (s, e) => await CargarDashboardAsync();
        }

        private async Task CargarDashboardAsync()
        {
            // 1. Saludo personalizado con el usuario activo
            var usuario = LoginView.EmpleadoActual;
            if (usuario != null)
            {
                lblBienvenida.Text = $"Hola, {usuario.Nombre} 👋";
                lblSubtituloRol.Text = $"Sesión activa como: {usuario.Rol?.Nombre ?? "Personal"}";
            }
            lblFechaActual.Text = DateTime.Today.ToString("dddd, dd MMMM yyyy");

            try
            {
                using var context = new DriveNowDbContext();

                // 2. Métricas de Flota (filtrando borrado lógico)
                int totalVehiculos = await context.Vehiculos.CountAsync(v => v.Activo);
                int disponibles = await context.Vehiculos.CountAsync(v => v.Activo && v.Estado != null && v.Estado.Nombre == "Disponible");
                int enTaller = await context.Vehiculos.CountAsync(v => v.Activo && v.Estado != null && (v.Estado.Nombre == "En Taller" || v.Estado.Nombre == "Mantenimiento"));

                lblKpiDisponibles.Text = disponibles.ToString();
                lblKpiTotalFlota.Text = $"{totalVehiculos} vehículos en flota total";

                // 3. Métricas de Alquileres y Reservas
                int alquileresActivos = await context.Alquileres.CountAsync(a => a.FechaEntregaDevolucion == null);
                lblKpiAlquileresActivos.Text = alquileresActivos.ToString();

                int reservasConfirmadas = await context.Reservas.CountAsync(r => r.Alquiler == null && r.Estado != null && r.Estado.Nombre != "Cancelada");
                lblKpiReservasConfirmadas.Text = reservasConfirmadas.ToString();

                // 4. Facturación del mes actual
                DateTime inicioMes = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
                decimal facturacionMes = await context.Alquileres
                    .Where(a => a.FechaEntregaReal >= inicioMes)
                    .SumAsync(a => a.CostoTotal + a.MontoMora); 
                lblKpiFacturacionMes.Text = facturacionMes.ToString("C");

                // 5. Gestión de Alertas Operativas
                int morasActivas = await context.Alquileres
                    .Include(a => a.Reserva)
                    .CountAsync(a => a.FechaEntregaDevolucion == null && a.Reserva != null && DateTime.Today > a.Reserva.FechaHasta.Date);

                bool hayAlertas = false;

                if (morasActivas > 0)
                {
                    cardAlertaMora.Visibility = Visibility.Visible;
                    lblAlertaMoraDesc.Text = $"{morasActivas} contrato(s) superaron la fecha límite de devolución.";
                    hayAlertas = true;
                }
                else
                {
                    cardAlertaMora.Visibility = Visibility.Collapsed;
                }

                if (enTaller > 0)
                {
                    cardAlertaTaller.Visibility = Visibility.Visible;
                    lblAlertaTallerDesc.Text = $"{enTaller} vehículo(s) en mantenimiento preventivo o reparación.";
                    hayAlertas = true;
                }
                else
                {
                    cardAlertaTaller.Visibility = Visibility.Collapsed;
                }

                cardTodoOk.Visibility = hayAlertas ? Visibility.Collapsed : Visibility.Visible;

                // 6. Últimos 5 contratos de alquiler
                var ultimos = await context.Alquileres
                    .Include(a => a.Reserva!)
                        .ThenInclude(r => r.Cliente)
                    .Include(a => a.Reserva!)
                        .ThenInclude(r => r.Vehiculo)
                    .OrderByDescending(a => a.IdAlquiler)
                    .Take(5)
                    .Select(a => new UltimoAlquilerDto
                    {
                        CodigoAlquiler = $"ALQ-{a.IdAlquiler:D4}",
                        Cliente = a.Reserva != null && a.Reserva.Cliente != null ? a.Reserva.Cliente.NombreCompleto : "Sin Cliente",
                        Vehiculo = a.Reserva != null && a.Reserva.Vehiculo != null ? $"{a.Reserva.Vehiculo.MarcaModelo} ({a.Reserva.Vehiculo.Patente})" : "Sin Vehículo",
                        FechaSalida = a.FechaEntregaReal,
                        EstadoTexto = a.FechaEntregaDevolucion == null ? "En Curso 🔑" : "Finalizado ✅"
                    })
                    .ToListAsync();

                dgvUltimosAlquileres.ItemsSource = ultimos;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cargar el panel principal: {ex.Message}", "Error de Conexión", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}