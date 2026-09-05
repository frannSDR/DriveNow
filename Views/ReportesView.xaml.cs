using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.EntityFrameworkCore;
using DriveNow.Data;

namespace DriveNow.Views
{
    public class CategoriaFinancieraDto
    {
        public string Categoria { get; set; } = string.Empty;
        public int CantidadAlquileres { get; set; }
        public decimal TotalRecaudado { get; set; }
    }

    public class ContratoLiquidadoDto
    {
        public string CodigoAlquiler { get; set; } = string.Empty;
        public string Cliente { get; set; } = string.Empty;
        public string Vehiculo { get; set; } = string.Empty;
        public decimal CostoBase { get; set; }
        public decimal MontoMora { get; set; }
        public decimal TotalCobrado => CostoBase + MontoMora;
    }

    public partial class ReportesView : UserControl
    {
        public ReportesView()
        {
            InitializeComponent();
            // Por defecto, muestra el mes actual completo
            dpDesde.SelectedDate = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            dpHasta.SelectedDate = DateTime.Today;
            Loaded += async (s, e) => await CargarMetricasFinancierasAsync();
        }

        private async void btnFiltrar_Click(object sender, RoutedEventArgs e)
        {
            await CargarMetricasFinancierasAsync();
        }

        private async void FiltroFechas_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (IsLoaded)
            {
                await CargarMetricasFinancierasAsync();
            }
        }

        private async Task CargarMetricasFinancierasAsync()
        {
            if (!dpDesde.SelectedDate.HasValue || !dpHasta.SelectedDate.HasValue) return;

            DateTime fechaInicio = dpDesde.SelectedDate.Value.Date;
            DateTime fechaFin = dpHasta.SelectedDate.Value.Date.AddDays(1).AddTicks(-1);

            try
            {
                using var context = new DriveNowDbContext();

                // Obtenemos solo los contratos finalizados en el período
                var contratos = await context.Alquileres
                    .Include(a => a.Reserva)
                        .ThenInclude(r => r.Cliente)
                    .Include(a => a.Reserva)
                        .ThenInclude(r => r.Vehiculo)
                            .ThenInclude(v => v.Categoria)
                    .Where(a => a.FechaEntregaDevolucion.HasValue &&
                                a.FechaEntregaDevolucion.Value >= fechaInicio &&
                                a.FechaEntregaDevolucion.Value <= fechaFin)
                    .ToListAsync();

                // 1. Cálculos de KPIs generales
                decimal totalBase = contratos.Sum(a => a.CostoTotal);
                decimal totalMora = contratos.Sum(a => a.MontoMora);
                decimal facturacionTotal = totalBase + totalMora;
                int cantidadContratos = contratos.Count;
                decimal ticketPromedio = cantidadContratos > 0 ? facturacionTotal / cantidadContratos : 0;

                lblIngresosTotales.Text = facturacionTotal.ToString("C");
                lblIngresosBase.Text = totalBase.ToString("C");
                lblIngresosMora.Text = totalMora.ToString("C");
                lblTicketPromedio.Text = ticketPromedio.ToString("C");
                lblTotalContratos.Text = $"{cantidadContratos} contrato(s) liquidado(s)";

                // 2. Desglose por Categoría
                var categoriasDb = await context.Categorias.ToListAsync();
                var agrupadoCat = categoriasDb.Select(cat =>
                {
                    var contratosCat = contratos.Where(c => c.Reserva?.Vehiculo?.IdCategoria == cat.IdCategoria).ToList();
                    return new CategoriaFinancieraDto
                    {
                        Categoria = cat.Nombre,
                        CantidadAlquileres = contratosCat.Count,
                        TotalRecaudado = contratosCat.Sum(c => c.CostoTotal + c.MontoMora)
                    };
                }).OrderByDescending(dto => dto.TotalRecaudado).ToList();

                dgvRendimientoCat.ItemsSource = agrupadoCat;

                // 3. Detalle de Contratos
                var listaContratos = contratos.Select(a => new ContratoLiquidadoDto
                {
                    CodigoAlquiler = $"ALQ-{a.IdAlquiler:D4}",
                    Cliente = a.Reserva?.Cliente?.NombreCompleto ?? "N/A",
                    Vehiculo = $"{a.Reserva?.Vehiculo?.MarcaModelo} ({a.Reserva?.Vehiculo?.Patente})",
                    CostoBase = a.CostoTotal,
                    MontoMora = a.MontoMora
                }).ToList();

                dgvContratosCerrados.ItemsSource = listaContratos;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al calcular reportes financieros: {ex.Message}", "Error de Conexión", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}