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
        
        private Vehiculo? _vehiculoEnEdicion = null; // null = Alta, con valor = Edición
        
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

            // El Dueño tiene acceso de solo lectura (supervisión ejecutiva)
            if (rol == RolUsuario.Dueno)
            {
                pnlFormularioVehiculo.Visibility = Visibility.Collapsed;
                colAccionesFlota.Visibility = Visibility.Collapsed;
            }
            else
            {
                // SuperAdmin, Administrador y Operador pueden operar sobre los vehículos
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

                var estados = await context.Estados.ToListAsync();
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
                var vehiculos = await context.Vehiculos
                    .Include(v => v.Categoria)
                    .Include(v => v.Estado)
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

            // Regex para formato estricto: 2 Letras + Espacio + 3 Números + Espacio + 2 Letras
            var regexMercosur = new Regex(@"^[A-Z]{2} [0-9]{3} [A-Z]{2}$");

            if (!regexMercosur.IsMatch(patente))
            {
                MessageBox.Show("El formato de patente debe ser válido (ej. AA 123 AA).",
                                "Patente Inválida",
                                MessageBoxButton.OK,
                                MessageBoxImage.Warning);
                return;
            }

            int idCat = (int)cmbCategoria.SelectedValue;
            int idEst = (int)cmbEstado.SelectedValue;

            try
            {
                using var context = new DriveNowDbContext();

                // Validación RN-01: Patente única (excluyendo el vehículo actual si se está editando)
                bool existePatente = await context.Vehiculos.AnyAsync(v =>
                    v.Patente == patente &&
                    (_vehiculoEnEdicion == null || v.IdVehiculo != _vehiculoEnEdicion.IdVehiculo));

                if (existePatente)
                {
                    MessageBox.Show("Ya existe un vehículo registrado con esa patente.", "Patente Duplicada", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (_vehiculoEnEdicion == null)
                {
                    // --- ALTA NUEVA ---
                    var nuevo = new Vehiculo
                    {
                        Patente = patente,
                        Marca = txtMarca.Text.Trim(),
                        Modelo = txtModelo.Text.Trim(),
                        Anio = anio,
                        IdCategoria = idCat,
                        IdEstado = idEst
                    };

                    context.Vehiculos.Add(nuevo);
                    await context.SaveChangesAsync();
                    MessageBox.Show("Vehículo registrado exitosamente.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    // --- ACTUALIZACIÓN ---
                    var vehiculoDb = await context.Vehiculos.FindAsync(_vehiculoEnEdicion.IdVehiculo);
                    if (vehiculoDb != null)
                    {
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
                    MessageBox.Show($"No se puede eliminar el vehículo porque se encuentra en estado '{vehiculoSeleccionado.Estado.Nombre}'.", "Acción Bloqueada", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var res = MessageBox.Show($"¿Seguro que desea eliminar el vehículo {vehiculoSeleccionado.MarcaModelo} ({vehiculoSeleccionado.Patente})?", "Confirmar Eliminación", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (res != MessageBoxResult.Yes) return;

                try
                {
                    using var context = new DriveNowDbContext();

                    bool tieneHistorial = await context.Reservas.AnyAsync(r => r.IdVehiculo == vehiculoSeleccionado.IdVehiculo);
                    if (tieneHistorial)
                    {
                        MessageBox.Show("No se puede eliminar el vehículo porque posee historial de reservas asociadas.", "Operación Bloqueada", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    var itemABorrar = await context.Vehiculos.FindAsync(vehiculoSeleccionado.IdVehiculo);
                    if (itemABorrar != null)
                    {
                        context.Vehiculos.Remove(itemABorrar);
                        await context.SaveChangesAsync();
                        _listaVehiculos.Remove(vehiculoSeleccionado);

                        if (_vehiculoEnEdicion?.IdVehiculo == vehiculoSeleccionado.IdVehiculo)
                        {
                            ResetearFormularioFlota();
                        }

                        ActualizarMetricas();
                        MessageBox.Show("Vehículo eliminado.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error al eliminar: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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

            // 1. Tomar solo caracteres alfanuméricos y pasarlos a mayúsculas
            string raw = Regex.Replace(txtPatente.Text.ToUpper(), @"[^A-Z0-9]", "");

            // Limitar a máximo 7 caracteres útiles
            if (raw.Length > 7)
                raw = raw.Substring(0, 7);

            // 2. Construir la máscara dinámica según lo que va escribiendo:
            // Formato objetivo: AA 123 AA
            string formatted = raw;

            if (raw.Length > 5)
            {
                // Más de 5 caracteres: AA 123 + resto
                formatted = $"{raw.Substring(0, 2)} {raw.Substring(2, 3)} {raw.Substring(5)}";
            }
            else if (raw.Length > 2)
            {
                // Entre 3 y 5 caracteres: AA + resto
                formatted = $"{raw.Substring(0, 2)} {raw.Substring(2)}";
            }

            txtPatente.Text = formatted;
            txtPatente.CaretIndex = txtPatente.Text.Length; // Mantiene el cursor siempre al final

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