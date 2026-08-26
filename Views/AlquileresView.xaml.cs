using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using DriveNow.Models;

namespace DriveNow.Views
{
    public partial class AlquileresView : UserControl
    {
        private ObservableCollection<Alquiler> historialAlquileres = new ObservableCollection<Alquiler>();
        private Alquiler alquilerSeleccionadoParaDevolucion = null;
        private string rutaFotoDanioSeleccionada = string.Empty;

        public AlquileresView()
        {
            InitializeComponent();
            CargarDatosIniciales();
            dgvHistorial.ItemsSource = historialAlquileres;
            ActualizarKPIs();
        }

        private void CargarDatosIniciales()
        {
            historialAlquileres.Add(new Alquiler
            {
                IdAlquiler = 1,
                CodigoAlquiler = "ALQ-1902",
                Cliente = "Carlos Méndez",
                DniCliente = "45678912K",
                Vehiculo = "Audi A4 (1234-LMN)",
                Patente = "1234-LMN",
                FechaEntregaReal = DateTime.Now.AddDays(-3),
                FechaDevolucionReal = DateTime.Now.AddDays(-1),
                KmSalida = 45000,
                KmEntrada = 45458,
                Estado = "Finalizado"
            });

            historialAlquileres.Add(new Alquiler
            {
                IdAlquiler = 2,
                CodigoAlquiler = "ALQ-1905",
                Cliente = "Laura Gómez",
                DniCliente = "78922348J",
                Vehiculo = "BMW Serie 3 (9836-XYZ)",
                Patente = "9836-XYZ",
                FechaEntregaReal = DateTime.Now.AddDays(-2),
                FechaDevolucionPactada = DateTime.Now.AddHours(-4), // 4 horas de retraso
                KmSalida = 52000,
                Estado = "En Curso"
            });
        }

        private void ActualizarKPIs()
        {
            lblKpiActivos.Text = historialAlquileres.Count(a => a.Estado == "En Curso").ToString();
            lblKpiDevueltos.Text = historialAlquileres.Count(a => a.Estado == "Finalizado").ToString();
            lblKpiMora.Text = historialAlquileres.Count(a => a.Estado == "En Curso" && DateTime.Now > a.FechaDevolucionPactada).ToString();
        }

        private void btnCargarReservaEntrega_Click(object sender, RoutedEventArgs e)
        {
            string busqueda = txtBuscarReservaEntrega.Text.Trim();
            if (string.IsNullOrEmpty(busqueda))
            {
                MessageBox.Show("Ingrese un código de reserva o DNI.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            lblDetalleReservaEntrega.Text = $"Reserva RES-1002 cargada: Toyota Corolla (AB 123 CD)";
        }

        private void btnRegistrarEntrega_Click(object sender, RoutedEventArgs e)
        {
            if (!int.TryParse(txtCombustibleSalida.Text.Trim(), out int combustible) || combustible < 25)
            {
                // Regla RN-03 / RF-19: Combustible minimo
                MessageBox.Show("El nivel de combustible es inaceptable para entrega (Mínimo 25%).", "Advertencia de Inspección", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!int.TryParse(txtKmSalida.Text.Trim(), out int kmSalida))
            {
                MessageBox.Show("Ingrese un kilometraje válido.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            Alquiler nuevo = new Alquiler
            {
                IdAlquiler = historialAlquileres.Count + 1,
                CodigoAlquiler = $"ALQ-{1900 + historialAlquileres.Count + 1}",
                Cliente = "Cliente Operativo",
                DniCliente = "30111222",
                Vehiculo = "Toyota Corolla (AB 123 CD)",
                Patente = "AB 123 CD",
                FechaEntregaReal = DateTime.Now,
                FechaDevolucionPactada = DateTime.Now.AddDays(2),
                CombustibleSalida = combustible,
                KmSalida = kmSalida,
                Estado = "En Curso"
            };

            historialAlquileres.Add(nuevo);
            ActualizarKPIs();
            MessageBox.Show($"Entrega formal registrada. Alquiler {nuevo.CodigoAlquiler} iniciado.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void btnCargarAlquilerDev_Click(object sender, RoutedEventArgs e)
        {
            string busqueda = txtBuscarAlquilerDev.Text.Trim();
            alquilerSeleccionadoParaDevolucion = historialAlquileres.FirstOrDefault(a =>
                a.Estado == "En Curso" &&
                (a.CodigoAlquiler.Equals(busqueda, StringComparison.OrdinalIgnoreCase) || a.Patente.Equals(busqueda, StringComparison.OrdinalIgnoreCase)));

            if (alquilerSeleccionadoParaDevolucion != null)
            {
                lblDetalleAlquilerDev.Text = $"{alquilerSeleccionadoParaDevolucion.CodigoAlquiler} - {alquilerSeleccionadoParaDevolucion.Cliente} ({alquilerSeleccionadoParaDevolucion.Vehiculo})";
            }
            else
            {
                MessageBox.Show("No se encontró ningún alquiler activo con esos datos.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void btnAdjuntarDanio_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dlg = new OpenFileDialog();
            dlg.Filter = "Imágenes|*.jpg;*.png;*.jpeg";
            if (dlg.ShowDialog() == true)
            {
                rutaFotoDanioSeleccionada = dlg.FileName;
                lblFotoDanioStatus.Text = "Foto de daño adjuntada ✔";
            }
        }

        private void btnFinalizarAlquiler_Click(object sender, RoutedEventArgs e)
        {
            if (alquilerSeleccionadoParaDevolucion == null)
            {
                MessageBox.Show("Debe cargar un alquiler activo primero.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!int.TryParse(txtKmEntrada.Text.Trim(), out int kmEntrada) || kmEntrada < alquilerSeleccionadoParaDevolucion.KmSalida)
            {
                MessageBox.Show("El kilometraje de retorno no puede ser menor al de salida.", "Error de Kilometraje", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            alquilerSeleccionadoParaDevolucion.FechaDevolucionReal = DateTime.Now;
            alquilerSeleccionadoParaDevolucion.KmEntrada = kmEntrada;
            alquilerSeleccionadoParaDevolucion.CombustibleEntrada = int.TryParse(txtCombustibleEntrada.Text.Trim(), out int c) ? c : 100;
            alquilerSeleccionadoParaDevolucion.RutaEvidenciaDanio = rutaFotoDanioSeleccionada;

            // RF-21: Cálculo automático de mora
            if (alquilerSeleccionadoParaDevolucion.FechaDevolucionReal > alquilerSeleccionadoParaDevolucion.FechaDevolucionPactada)
            {
                var diff = alquilerSeleccionadoParaDevolucion.FechaDevolucionReal.Value - alquilerSeleccionadoParaDevolucion.FechaDevolucionPactada;
                alquilerSeleccionadoParaDevolucion.HorasMora = Math.Max(1, (int)diff.TotalHours);
                alquilerSeleccionadoParaDevolucion.MontoMora = alquilerSeleccionadoParaDevolucion.HorasMora * 5000; // Tarifa mora por hora
            }

            alquilerSeleccionadoParaDevolucion.Estado = "Finalizado";
            dgvHistorial.Items.Refresh();
            ActualizarKPIs();

            string msgFinal = $"Alquiler finalizado correctamente.";
            if (alquilerSeleccionadoParaDevolucion.HorasMora > 0)
                msgFinal += $"\n⚠️ Se detectó retraso: {alquilerSeleccionadoParaDevolucion.HorasMora} horas de mora registradas.";

            MessageBox.Show(msgFinal, "Devolución Exitosa", MessageBoxButton.OK, MessageBoxImage.Information);
            alquilerSeleccionadoParaDevolucion = null;
            lblDetalleAlquilerDev.Text = "Alquiler: No seleccionado";
        }
    }
}