using DriveNow.Data;
using DriveNow.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DriveNow.Views
{
    public partial class FlotaView : UserControl
    {
        private ObservableCollection<Vehiculo> _listaVehiculos = new ObservableCollection<Vehiculo>();
        private Vehiculo? _vehiculoEnEdicion = null;
        private bool _isFormatting = false;

        public FlotaView()
        {
            InitializeComponent();
            Loaded += FlotaView_Loaded;
        }

        private async void FlotaView_Loaded(object sender, RoutedEventArgs e)
        {
            AplicarPermisosSegunRol();
            await CargarCatalogosAsync();
            await CargarFlotaDesdeBdAsync();
        }

        private void AplicarPermisosSegunRol()
        {
            if (LoginView.EmpleadoActual == null) return;

            var rol = (RolUsuario)LoginView.EmpleadoActual.IdRol;

            if (rol == RolUsuario.Dueno)
            {
                pnlFormularioVehiculo.Visibility = Visibility.Collapsed;
                colAccionesFlota.Visibility = Visibility.Collapsed;
            }
            else
            {
                pnlFormularioVehiculo.Visibility = Visibility.Visible;
                colAccionesFlota.Visibility = Visibility.Visible;
            }
        }

        private async Task CargarCatalogosAsync()
        {
            try
            {
                using var context = new DriveNowDbContext();

                var categorias = await context.Categorias.ToListAsync();
                cmbCategoria.ItemsSource = categorias;
                if (categorias.Count > 0) cmbCategoria.SelectedIndex = 0;

                var estadosValidos = new[] { "Disponible", "Alquilado", "En Taller", "Mantenimiento" };
                var estados = await context.Estados
                    .Where(e => estadosValidos.Contains(e.Nombre))
                    .ToListAsync();

                cmbEstado.ItemsSource = estados;
                if (estados.Count > 0) cmbEstado.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cargar catálogos: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task CargarFlotaDesdeBdAsync()
        {
            try
            {
                using var context = new DriveNowDbContext();
                // Filtramos únicamente los vehículos con borrado lógico activo
                var vehiculos = await context.Vehiculos
                    .Include(v => v.Categoria)
                    .Include(v => v.Estado)
                    .Where(v => v.Activo)
                    .OrderByDescending(v => v.IdVehiculo)
                    .ToListAsync();

                _listaVehiculos = new ObservableCollection<Vehiculo>(vehiculos);
                dgvFlota.ItemsSource = _listaVehiculos;
                ActualizarMetricas();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cargar la flota: {ex.Message}", "Error de Conexión", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ActualizarMetricas()
        {
            lblDisponibles.Text = _listaVehiculos.Count(v => v.Estado?.Nombre == "Disponible").ToString();
            lblAlquilados.Text = _listaVehiculos.Count(v => v.Estado?.Nombre == "Alquilado").ToString();
            lblReservados.Text = _listaVehiculos.Count(v => v.Estado?.Nombre == "Reservado").ToString();
            lblMantenimiento.Text = _listaVehiculos.Count(v => v.Estado?.Nombre == "En Taller" || v.Estado?.Nombre == "Mantenimiento").ToString();
        }

        private async void btnGuardarVehiculo_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtPatente.Text) ||
                string.IsNullOrWhiteSpace(txtMarca.Text) ||
                string.IsNullOrWhiteSpace(txtModelo.Text) ||
                string.IsNullOrWhiteSpace(txtAnio.Text) ||
                cmbCategoria.SelectedValue == null ||
                cmbEstado.SelectedValue == null)
            {
                MessageBox.Show("Por favor complete todos los campos obligatorios.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!int.TryParse(txtAnio.Text.Trim(), out int anio) || anio < 1990 || anio > DateTime.Now.Year + 1)
            {
                MessageBox.Show("Ingrese un año válido.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            string patente = txtPatente.Text.Trim().ToUpper();
            var regexMercosur = new Regex(@"^[A-Z]{2} [0-9]{3} [A-Z]{2}$");

            if (!regexMercosur.IsMatch(patente))
            {
                MessageBox.Show("El formato de patente debe ser válido (ej. AA 123 AA).", "Patente Inválida", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            int idCat = (int)cmbCategoria.SelectedValue;
            int idEst = (int)cmbEstado.SelectedValue;

            try
            {
                using var context = new DriveNowDbContext();

                bool existePatente = await context.Vehiculos.AnyAsync(v =>
                    v.Patente == patente &&
                    v.Activo &&
                    (_vehiculoEnEdicion == null || v.IdVehiculo != _vehiculoEnEdicion.IdVehiculo));

                if (existePatente)
                {
                    MessageBox.Show("Ya existe un vehículo activo registrado con esa patente.", "Patente Duplicada", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (_vehiculoEnEdicion == null)
                {
                    var nuevo = new Vehiculo
                    {
                        Patente = patente,
                        Marca = txtMarca.Text.Trim(),
                        Modelo = txtModelo.Text.Trim(),
                        Anio = anio,
                        IdCategoria = idCat,
                        IdEstado = idEst,
                        Activo = true
                    };

                    context.Vehiculos.Add(nuevo);
                    await context.SaveChangesAsync();
                    MessageBox.Show("Vehículo registrado exitosamente.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    var vehiculoDb = await context.Vehiculos
                        .Include(v => v.Estado)
                        .FirstOrDefaultAsync(v => v.IdVehiculo == _vehiculoEnEdicion.IdVehiculo);

                    if (vehiculoDb != null)
                    {
                        if (vehiculoDb.Estado?.Nombre == "Alquilado")
                        {
                            MessageBox.Show("No se pueden guardar los cambios porque el vehículo se encuentra alquilado.", "Acción Rechazada", MessageBoxButton.OK, MessageBoxImage.Warning);
                            ResetearFormularioFlota();
                            await CargarFlotaDesdeBdAsync();
                            return;
                        }

                        vehiculoDb.Patente = patente;
                        vehiculoDb.Marca = txtMarca.Text.Trim();
                        vehiculoDb.Modelo = txtModelo.Text.Trim();
                        vehiculoDb.Anio = anio;
                        vehiculoDb.IdCategoria = idCat;
                        vehiculoDb.IdEstado = idEst;

                        await context.SaveChangesAsync();
                        MessageBox.Show("Vehículo actualizado exitosamente.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }

                await CargarFlotaDesdeBdAsync();
                ResetearFormularioFlota();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al guardar en el servidor: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnEditarVehiculo_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is Vehiculo vehiculo)
            {
                if (vehiculo.Estado?.Nombre == "Alquilado")
                {
                    MessageBox.Show(
                        $"El vehículo {vehiculo.MarcaModelo} ({vehiculo.Patente}) se encuentra actualmente ALQUILADO.\n" +
                        "No es posible modificar sus datos mientras esté en posesión de un cliente.",
                        "Operación Bloqueada",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                _vehiculoEnEdicion = vehiculo;

                txtPatente.Text = vehiculo.Patente;
                txtMarca.Text = vehiculo.Marca;
                txtModelo.Text = vehiculo.Modelo;
                txtAnio.Text = vehiculo.Anio.ToString();
                cmbCategoria.SelectedValue = vehiculo.IdCategoria;
                cmbEstado.SelectedValue = vehiculo.IdEstado;

                lblTituloFormularioFlota.Text = $"✏️ Editando Vehículo: {vehiculo.MarcaModelo} ({vehiculo.Patente})";
                btnGuardarVehiculo.Content = "Actualizar Cambios";
                btnGuardarVehiculo.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981"));
                btnCancelarFlota.Visibility = Visibility.Visible;
                btnLimpiarFlota.Visibility = Visibility.Collapsed;
            }
        }

        private async void btnEliminarFila_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is Vehiculo vehiculoSeleccionado)
            {
                if (vehiculoSeleccionado.Estado?.Nombre == "Alquilado" || vehiculoSeleccionado.Estado?.Nombre == "Reservado")
                {
                    MessageBox.Show($"No se puede dar de baja el vehículo porque se encuentra en estado '{vehiculoSeleccionado.Estado.Nombre}'.", "Acción Bloqueada", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var res = MessageBox.Show($"¿Seguro que desea dar de baja el vehículo {vehiculoSeleccionado.MarcaModelo} ({vehiculoSeleccionado.Patente})?", "Confirmar Baja", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (res != MessageBoxResult.Yes) return;

                try
                {
                    using var context = new DriveNowDbContext();

                    var itemADesactivar = await context.Vehiculos.FindAsync(vehiculoSeleccionado.IdVehiculo);
                    if (itemADesactivar != null)
                    {
                        // Borrado lógico
                        itemADesactivar.Activo = false;
                        await context.SaveChangesAsync();

                        _listaVehiculos.Remove(vehiculoSeleccionado);

                        if (_vehiculoEnEdicion?.IdVehiculo == vehiculoSeleccionado.IdVehiculo)
                        {
                            ResetearFormularioFlota();
                        }

                        ActualizarMetricas();
                        MessageBox.Show("Vehículo dado de baja exitosamente del inventario activo.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error al procesar la baja: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void txtBuscar_TextChanged(object sender, TextChangedEventArgs e)
        {
            string filtro = txtBuscar.Text.Trim().ToLower();
            if (string.IsNullOrEmpty(filtro))
            {
                dgvFlota.ItemsSource = _listaVehiculos;
            }
            else
            {
                dgvFlota.ItemsSource = _listaVehiculos.Where(v =>
                    v.Patente.ToLower().Contains(filtro) ||
                    v.Marca.ToLower().Contains(filtro) ||
                    v.Modelo.ToLower().Contains(filtro)).ToList();
            }
        }

        private void txtPatente_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isFormatting) return;

            _isFormatting = true;

            string raw = Regex.Replace(txtPatente.Text.ToUpper(), @"[^A-Z0-9]", "");
            if (raw.Length > 7) raw = raw.Substring(0, 7);

            string formatted = raw;
            if (raw.Length > 5)
            {
                formatted = $"{raw.Substring(0, 2)} {raw.Substring(2, 3)} {raw.Substring(5)}";
            }
            else if (raw.Length > 2)
            {
                formatted = $"{raw.Substring(0, 2)} {raw.Substring(2)}";
            }

            txtPatente.Text = formatted;
            txtPatente.CaretIndex = txtPatente.Text.Length;

            _isFormatting = false;
        }

        private void btnCancelarFlota_Click(object sender, RoutedEventArgs e) => ResetearFormularioFlota();
        private void btnLimpiarFlota_Click(object sender, RoutedEventArgs e) => ResetearFormularioFlota();

        private void ResetearFormularioFlota()
        {
            _vehiculoEnEdicion = null;

            txtPatente.Clear();
            txtMarca.Clear();
            txtModelo.Clear();
            txtAnio.Clear();
            if (cmbCategoria.Items.Count > 0) cmbCategoria.SelectedIndex = 0;
            if (cmbEstado.Items.Count > 0) cmbEstado.SelectedIndex = 0;

            lblTituloFormularioFlota.Text = "➕ Alta de vehículo";
            btnGuardarVehiculo.Content = "Guardar";
            btnGuardarVehiculo.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2563EB"));
            btnCancelarFlota.Visibility = Visibility.Collapsed;
            btnLimpiarFlota.Visibility = Visibility.Visible;
        }
    }
}