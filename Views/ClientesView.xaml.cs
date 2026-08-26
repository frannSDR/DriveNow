using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DriveNow.Models;

namespace DriveNow.Views
{
    public partial class ClientesView : UserControl
    {
        private ObservableCollection<Cliente> listaClientes = new ObservableCollection<Cliente>();

        public ClientesView()
        {
            InitializeComponent();
            dpVencimiento.SelectedDate = DateTime.Now.AddYears(2);
            CargarClientesDePrueba();
            dgvClientes.ItemsSource = listaClientes;
        }

        private void CargarClientesDePrueba()
        {
            listaClientes.Add(new Cliente
            {
                IdCliente = 1,
                Dni = "45892011M",
                NombreCompleto = "Juan Rodríguez",
                Telefono = "+34 600 123 456",
                Correo = "juan.rodriguez@email.com",
                NroLicencia = "B-987654",
                VencimientoLicencia = new DateTime(2028, 12, 1)
            });

            listaClientes.Add(new Cliente
            {
                IdCliente = 2,
                Dni = "71234568T",
                NombreCompleto = "María López",
                Telefono = "+34 611 987 654",
                Correo = "maria.lopez@email.com",
                NroLicencia = "B-123456",
                VencimientoLicencia = new DateTime(2023, 6, 1) // Licencia vencida
            });
        }

        private void btnGuardarCliente_Click(object sender, RoutedEventArgs e)
        {
            // Validaciones de campos obligatorios (RF-07)
            if (string.IsNullOrWhiteSpace(txtNombre.Text) ||
                string.IsNullOrWhiteSpace(txtDni.Text) ||
                string.IsNullOrWhiteSpace(txtTelefono.Text) ||
                string.IsNullOrWhiteSpace(txtCorreo.Text) ||
                string.IsNullOrWhiteSpace(txtLicencia.Text) ||
                !dpVencimiento.SelectedDate.HasValue)
            {
                MessageBox.Show("Por favor, complete todos los campos obligatorios.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string dni = txtDni.Text.Trim().ToUpper();

            // Verificamos DNI unico
            if (listaClientes.Any(c => c.Dni.Equals(dni, StringComparison.OrdinalIgnoreCase)))
            {
                MessageBox.Show("Ya existe un cliente registrado con ese número de DNI.", "DNI Duplicado", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            Cliente nuevo = new Cliente
            {
                IdCliente = listaClientes.Count + 1,
                NombreCompleto = txtNombre.Text.Trim(),
                Dni = dni,
                Telefono = txtTelefono.Text.Trim(),
                Correo = txtCorreo.Text.Trim(),
                NroLicencia = txtLicencia.Text.Trim(),
                VencimientoLicencia = dpVencimiento.SelectedDate.Value
            };

            listaClientes.Add(nuevo);
            MostrarTarjetaValidacion(nuevo);
            LimpiarFormulario();
            MessageBox.Show("Cliente registrado exitosamente.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void btnBuscarValidacion_Click(object sender, RoutedEventArgs e)
        {
            string dniBuscado = txtValidarDni.Text.Trim();
            Cliente cliente = listaClientes.FirstOrDefault(c => c.Dni.Equals(dniBuscado, StringComparison.OrdinalIgnoreCase));

            if (cliente != null)
            {
                MostrarTarjetaValidacion(cliente);
            }
            else
            {
                MessageBox.Show("Cliente no encontrado en el padrón.", "Búsqueda sin resultados", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void dgvClientes_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dgvClientes.SelectedItem is Cliente cliente)
            {
                MostrarTarjetaValidacion(cliente);
            }
        }

        private void MostrarTarjetaValidacion(Cliente cliente)
        {
            lblNombreValidado.Text = cliente.NombreCompleto;
            lblDniValidado.Text = $"DNI: {cliente.Dni}";
            lblVencimientoTexto.Text = $"Estado de Licencia Vence: {cliente.VencimientoFormato}";

            if (cliente.LicenciaVencida)
            {
                badgeEstado.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#7F1D1D"));
                lblBadgeTexto.Text = "VENCIDA";
                lblBadgeTexto.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F87171"));
                pbLicencia.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EF4444"));
                lblLicenciaEstadoMsg.Text = "⚠️ Licencia caducada. Operación bloqueada.";
                lblLicenciaEstadoMsg.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F87171"));
            }
            else
            {
                badgeEstado.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#065F46"));
                lblBadgeTexto.Text = "VALIDADO";
                lblBadgeTexto.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#34D399"));
                pbLicencia.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981"));
                lblLicenciaEstadoMsg.Text = "✅ Licencia válida y en regla";
                lblLicenciaEstadoMsg.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#34D399"));
            }
        }

        private void btnEliminarCliente_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is Cliente cliente)
            {
                MessageBoxResult res = MessageBox.Show($"¿Desea eliminar a {cliente.NombreCompleto}?", "Confirmar", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (res == MessageBoxResult.Yes)
                {
                    listaClientes.Remove(cliente);
                }
            }
        }

        private void btnLimpiar_Click(object sender, RoutedEventArgs e) => LimpiarFormulario();

        private void LimpiarFormulario()
        {
            txtNombre.Clear();
            txtDni.Clear();
            txtTelefono.Clear();
            txtCorreo.Clear();
            txtLicencia.Clear();
            dpVencimiento.SelectedDate = DateTime.Now.AddYears(2);
        }
    }
}