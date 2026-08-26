using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using DriveNow.Models;

namespace DriveNow.Views
{
    public partial class FlotaView : UserControl
    {
        // Colección observable para que el DataGrid se actualice automáticamente
        private ObservableCollection<Vehiculo> listaVehiculos = new ObservableCollection<Vehiculo>();

        public FlotaView()
        {
            InitializeComponent();
            CargarDatosDePrueba();
            dgvFlota.ItemsSource = listaVehiculos;
            ActualizarMetricas();
        }

        private void CargarDatosDePrueba()
        {
            listaVehiculos.Add(new Vehiculo { Patente = "AB 123 CD", Marca = "Toyota", Modelo = "Corolla", Categoria = "Sedán", Año = 2023, Estado = "Disponible" });
            listaVehiculos.Add(new Vehiculo { Patente = "EF 456 GH", Marca = "Ford", Modelo = "Ranger", Categoria = "Pickup", Año = 2022, Estado = "Alquilado" });
            listaVehiculos.Add(new Vehiculo { Patente = "IJ 789 KL", Marca = "VW", Modelo = "Polo", Categoria = "Compacto", Año = 2024, Estado = "Reservado" });
        }

        private void ActualizarMetricas()
        {
            lblDisponibles.Text = listaVehiculos.Count(v => v.Estado == "Disponible").ToString();
            lblAlquilados.Text = listaVehiculos.Count(v => v.Estado == "Alquilado").ToString();
            lblReservados.Text = listaVehiculos.Count(v => v.Estado == "Reservado").ToString();
            lblMantenimiento.Text = listaVehiculos.Count(v => v.Estado == "Mantenimiento").ToString();
        }

        private void btnGuardar_Click(object sender, RoutedEventArgs e)
        {
            // Validaciones de campos obligatorios
            if (string.IsNullOrWhiteSpace(txtPatente.Text) ||
                string.IsNullOrWhiteSpace(txtMarca.Text) ||
                string.IsNullOrWhiteSpace(txtModelo.Text) ||
                string.IsNullOrWhiteSpace(txtAnio.Text))
            {
                MessageBox.Show("Por favor complete todos los campos obligatorios.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Validar año
            if (!int.TryParse(txtAnio.Text.Trim(), out int anio) || anio < 1990 || anio > DateTime.Now.Year + 1)
            {
                MessageBox.Show("Ingrese un año válido.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            string patente = txtPatente.Text.Trim().ToUpper();

            // Regla de Negocio RN-01: Patente única
            if (listaVehiculos.Any(v => v.Patente.Equals(patente, StringComparison.OrdinalIgnoreCase)))
            {
                MessageBox.Show("Ya existe un vehículo registrado con esa patente.", "Patente Duplicada", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Alta de nuevo vehículo
            Vehiculo nuevo = new Vehiculo
            {
                Patente = patente,
                Marca = txtMarca.Text.Trim(),
                Modelo = txtModelo.Text.Trim(),
                Categoria = ((ComboBoxItem)cmbCategoria.SelectedItem).Content.ToString() ?? "Compacto",
                Año = anio,
                Estado = "Disponible"
            };

            listaVehiculos.Add(nuevo);
            ActualizarMetricas();
            LimpiarFormulario();
            MessageBox.Show("Vehículo registrado exitosamente en la flota.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void btnEliminarFila_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is Vehiculo vehiculoSeleccionado)
            {
                // Regla de Negocio RF-06: Bloquear baja si está alquilado o reservado
                if (vehiculoSeleccionado.Estado == "Alquilado" || vehiculoSeleccionado.Estado == "Reservado")
                {
                    MessageBox.Show($"No se puede eliminar el vehículo porque se encuentra en estado '{vehiculoSeleccionado.Estado}'.", "Acción Bloqueada", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                MessageBoxResult res = MessageBox.Show($"¿Seguro que desea eliminar el vehículo {vehiculoSeleccionado.MarcaModelo} ({vehiculoSeleccionado.Patente})?", "Confirmar Eliminación", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (res == MessageBoxResult.Yes)
                {
                    listaVehiculos.Remove(vehiculoSeleccionado);
                    ActualizarMetricas();
                }
            }
        }

        private void txtBuscar_TextChanged(object sender, TextChangedEventArgs e)
        {
            string filtro = txtBuscar.Text.Trim().ToLower();
            if (string.IsNullOrEmpty(filtro))
            {
                dgvFlota.ItemsSource = listaVehiculos;
            }
            else
            {
                dgvFlota.ItemsSource = listaVehiculos.Where(v =>
                    v.Patente.ToLower().Contains(filtro) ||
                    v.Marca.ToLower().Contains(filtro) ||
                    v.Modelo.ToLower().Contains(filtro)).ToList();
            }
        }

        private void btnCancelar_Click(object sender, RoutedEventArgs e) => LimpiarFormulario();

        private void LimpiarFormulario()
        {
            txtPatente.Clear();
            txtMarca.Clear();
            txtModelo.Clear();
            txtAnio.Clear();
            cmbCategoria.SelectedIndex = 0;
        }
    }
}