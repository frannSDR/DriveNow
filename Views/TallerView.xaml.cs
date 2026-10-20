using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.EntityFrameworkCore;
using DriveNow.Data;
using DriveNow.Models;

namespace DriveNow.Views
{
    public partial class TallerView : UserControl
    {
        private ObservableCollection<Mantenimiento> _listaMantenimientos = new ObservableCollection<Mantenimiento>();
        private Mantenimiento? _mantenimientoEnEdicion = null;
        private bool _isLoaded = false;

        public TallerView()
        {
            InitializeComponent();
            Loaded += TallerView_Loaded;
        }

        private async void TallerView_Loaded(object sender, RoutedEventArgs e)
        {
            _isLoaded = true;
            await SincronizarVehiculosEnTallerAsync();
            await CargarMantenimientosAsync();
        }

        /// <summary>
        /// Garantiza que todo vehículo con IdEstado == 3 tenga una orden de mantenimiento abierta en el sistema.
        /// </summary>
        private async Task SincronizarVehiculosEnTallerAsync()
        {
            try
            {
                using var context = new DriveNowDbContext();

                var vehiculosEnTaller = await context.Vehiculos
                    .Where(v => v.Activo && v.IdEstado == 3)
                    .ToListAsync();

                var mantActivos = await context.Mantenimientos
                    .Where(m => m.Estado == "En Taller")
                    .Select(m => m.IdVehiculo)
                    .ToListAsync();

                bool huboCambios = false;

                foreach (var v in vehiculosEnTaller)
                {
                    if (!mantActivos.Contains(v.IdVehiculo))
                    {
                        // Buscamos si tiene algún daño registrado en su último contrato
                        var ultimoAlquiler = await context.Alquileres
                            .Include(a => a.Danos)
                            .Where(a => a.Reserva != null && a.Reserva.IdVehiculo == v.IdVehiculo)
                            .OrderByDescending(a => a.IdAlquiler)
                            .FirstOrDefaultAsync();

                        var dano = ultimoAlquiler?.Danos?.FirstOrDefault();

                        var nuevoMant = new Mantenimiento
                        {
                            IdVehiculo = v.IdVehiculo,
                            FechaIngreso = DateTime.Now,
                            Tipo = dano != null ? "Siniestro / Reparación de Daño" : "Mantenimiento Técnico",
                            Motivo = dano != null 
                                ? $"Daño en {dano.Zona}: {dano.Descripcion}" 
                                : "Unidad derivada a taller para inspección técnica.",
                            Diagnostico = "Pendiente de peritaje mecánico.",
                            Estado = "En Taller",
                            Costo = 0
                        };

                        context.Mantenimientos.Add(nuevoMant);
                        huboCambios = true;
                    }
                }

                if (huboCambios)
                {
                    await context.SaveChangesAsync();
                }
            }
            catch
            {
                // Manejo preventivo silencioso para no bloquear la carga
            }
        }

        public async Task CargarMantenimientosAsync()
        {
            try
            {
                using var context = new DriveNowDbContext();

                var mantenimientos = await context.Mantenimientos
                    .Include(m => m.Vehiculo)
                        .ThenInclude(v => v!.Categoria)
                    .Include(m => m.EmpleadoMecanico)
                    .OrderByDescending(m => m.FechaIngreso)
                    .ToListAsync();

                _listaMantenimientos = new ObservableCollection<Mantenimiento>(mantenimientos);
                ActualizarKPIs();
                AplicarFiltros();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cargar registros del taller: {ex.Message}", "Error de Conexión", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ActualizarKPIs()
        {
            if (lblKpiEnTaller == null || lblKpiDanos == null || lblKpiPreventivo == null || lblKpiFinalizados == null || _listaMantenimientos == null) return;

            int activos = _listaMantenimientos.Count(m => m != null && m.Estado == "En Taller");
            int danos = _listaMantenimientos.Count(m => m != null && m.Estado == "En Taller" && m.Tipo != null && m.Tipo.Contains("Siniestro"));
            int preventivo = _listaMantenimientos.Count(m => m != null && m.Estado == "En Taller" && m.Tipo != null && (m.Tipo.Contains("Preventivo") || m.Tipo.Contains("Service")));
            int finalizados = _listaMantenimientos.Count(m => m != null && m.Estado == "Finalizado");

            lblKpiEnTaller.Text = activos.ToString();
            lblKpiDanos.Text = danos.ToString();
            lblKpiPreventivo.Text = preventivo.ToString();
            lblKpiFinalizados.Text = finalizados.ToString();
        }

        private void AplicarFiltros()
        {
            if (!_isLoaded || dgvMantenimientos == null || _listaMantenimientos == null) return;

            var filtrados = _listaMantenimientos.AsEnumerable();

            // Filtro por Estado
            if (cmbFiltroEstado?.SelectedItem is ComboBoxItem item)
            {
                string estadoFiltro = item.Content?.ToString() ?? "";
                if (estadoFiltro.Contains("Activos"))
                {
                    filtrados = filtrados.Where(m => m != null && m.Estado == "En Taller");
                }
                else if (estadoFiltro.Contains("Finalizado"))
                {
                    filtrados = filtrados.Where(m => m != null && m.Estado == "Finalizado");
                }
            }

            // Filtro por Búsqueda de texto
            string busqueda = txtBusquedaTaller?.Text.Trim().ToLower() ?? "";
            if (!string.IsNullOrEmpty(busqueda))
            {
                filtrados = filtrados.Where(m =>
                    m != null && (
                    (m.Vehiculo != null && m.Vehiculo.MarcaModelo != null && m.Vehiculo.MarcaModelo.ToLower().Contains(busqueda)) ||
                    (m.Vehiculo != null && m.Vehiculo.Patente != null && m.Vehiculo.Patente.ToLower().Contains(busqueda)) ||
                    (m.Tipo != null && m.Tipo.ToLower().Contains(busqueda)) ||
                    (m.Motivo != null && m.Motivo.ToLower().Contains(busqueda)) ||
                    (m.Diagnostico != null && m.Diagnostico.ToLower().Contains(busqueda)) ||
                    (m.TrabajoRealizado != null && m.TrabajoRealizado.ToLower().Contains(busqueda)))
                );
            }

            dgvMantenimientos.ItemsSource = filtrados.ToList();
        }

        private void cmbFiltroEstado_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isLoaded || dgvMantenimientos == null) return;
            AplicarFiltros();
        }

        private void txtBusquedaTaller_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_isLoaded || dgvMantenimientos == null) return;
            AplicarFiltros();
        }

        // ==========================================
        // MODAL 1: PROGRAMAR SERVICE PREVENTIVO
        // ==========================================
        private async void btnNuevoService_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                using var context = new DriveNowDbContext();

                // Vehículos disponibles para entrar a service
                var disponibles = await context.Vehiculos
                    .Include(v => v.Categoria)
                    .Where(v => v.Activo && v.IdEstado == 1) // Disponible
                    .OrderBy(v => v.Marca)
                    .ThenBy(v => v.Modelo)
                    .ToListAsync();

                if (disponibles.Count == 0)
                {
                    MessageBox.Show("No hay vehículos en estado 'Disponible' en este momento para enviar a service.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                cmbVehiculosService.ItemsSource = disponibles;
                cmbVehiculosService.SelectedIndex = 0;
                txtObservacionesService.Text = "Cambio de aceite sintético 5W-30, cambio de filtro de aceite, filtro de aire y revisión de fluidos.";
                modalServiceOverlay.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cargar vehículos: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnCerrarModalService_Click(object sender, RoutedEventArgs e)
        {
            modalServiceOverlay.Visibility = Visibility.Collapsed;
        }

        private async void btnConfirmarIngresoService_Click(object sender, RoutedEventArgs e)
        {
            if (cmbVehiculosService.SelectedItem is not Vehiculo vehiculoSeleccionado)
            {
                MessageBox.Show("Seleccione un vehículo de la lista.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string tipo = (cmbTipoService.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Service Preventivo";
            string observaciones = txtObservacionesService.Text.Trim();
            if (string.IsNullOrEmpty(observaciones))
            {
                observaciones = $"Ingreso a {tipo} según plan de mantenimiento periódico.";
            }

            try
            {
                using var context = new DriveNowDbContext();

                var vehiculoDb = await context.Vehiculos.FindAsync(vehiculoSeleccionado.IdVehiculo);
                if (vehiculoDb != null)
                {
                    vehiculoDb.IdEstado = 3; // "En Taller"
                }

                var nuevoMant = new Mantenimiento
                {
                    IdVehiculo = vehiculoSeleccionado.IdVehiculo,
                    FechaIngreso = DateTime.Now,
                    Tipo = tipo,
                    Motivo = observaciones,
                    Diagnostico = "Inspección técnica programada.",
                    Estado = "En Taller",
                    Costo = 0
                };

                context.Mantenimientos.Add(nuevoMant);
                await context.SaveChangesAsync();

                modalServiceOverlay.Visibility = Visibility.Collapsed;
                MessageBox.Show($"El vehículo {vehiculoSeleccionado.MarcaModelo} ({vehiculoSeleccionado.Patente}) fue ingresado exitosamente al taller.", "Ingreso Confirmado", MessageBoxButton.OK, MessageBoxImage.Information);

                await CargarMantenimientosAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al registrar ingreso al taller: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ==========================================
        // MODAL 2: ORDEN DE TRABAJO Y DIAGNÓSTICO
        // ==========================================
        private void btnEditarTrabajo_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is Mantenimiento mant)
            {
                _mantenimientoEnEdicion = mant;
                lblSubtituloVehiculoTrabajo.Text = $"{mant.Vehiculo?.MarcaModelo} ({mant.Vehiculo?.Patente}) - {mant.Tipo}";
                lblMotivoIngresoModal.Text = mant.Motivo;
                txtDiagnosticoModal.Text = mant.Diagnostico ?? string.Empty;
                txtTrabajoModal.Text = mant.TrabajoRealizado ?? string.Empty;
                txtCostoModal.Text = mant.Costo.ToString("N2");

                modalTrabajoOverlay.Visibility = Visibility.Visible;
            }
        }

        private void btnCerrarModalTrabajo_Click(object sender, RoutedEventArgs e)
        {
            modalTrabajoOverlay.Visibility = Visibility.Collapsed;
            _mantenimientoEnEdicion = null;
        }

        private async void btnGuardarTrabajo_Click(object sender, RoutedEventArgs e)
        {
            if (_mantenimientoEnEdicion == null) return;

            string diagnostico = txtDiagnosticoModal.Text.Trim();
            string trabajo = txtTrabajoModal.Text.Trim();
            string costoStr = txtCostoModal.Text.Trim().Replace("$", "").Replace(".", "").Replace(",", ".");
            decimal.TryParse(costoStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal costo);

            try
            {
                using var context = new DriveNowDbContext();

                var mantDb = await context.Mantenimientos.FindAsync(_mantenimientoEnEdicion.IdMantenimiento);
                if (mantDb != null)
                {
                    mantDb.Diagnostico = diagnostico;
                    mantDb.TrabajoRealizado = trabajo;
                    mantDb.Costo = costo;

                    if (LoginView.EmpleadoActual != null)
                    {
                        mantDb.IdEmpleadoMecanico = LoginView.EmpleadoActual.IdEmpleado;
                    }

                    await context.SaveChangesAsync();
                }

                modalTrabajoOverlay.Visibility = Visibility.Collapsed;
                _mantenimientoEnEdicion = null;
                MessageBox.Show("Ficha de trabajo técnico actualizada correctamente.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);

                await CargarMantenimientosAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al guardar cambios técnicos: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ==========================================
        // LIBERACIÓN Y ALTA A LA FLOTA (A DISPONIBLE)
        // ==========================================
        private async void btnLiberarVehiculo_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is Mantenimiento mant)
            {
                string infoAuto = $"{mant.Vehiculo?.MarcaModelo} ({mant.Vehiculo?.Patente})";
                var res = MessageBox.Show(
                    $"¿Confirma la finalización de los trabajos y el alta del vehículo {infoAuto}?\n\nEl vehículo volverá inmediatamente al estado 'Disponible' para reservas y alquileres.",
                    "Liberar Vehículo a la Flota",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (res != MessageBoxResult.Yes) return;

                try
                {
                    using var context = new DriveNowDbContext();

                    var mantDb = await context.Mantenimientos.FindAsync(mant.IdMantenimiento);
                    if (mantDb != null)
                    {
                        mantDb.Estado = "Finalizado";
                        mantDb.FechaEgreso = DateTime.Now;
                        if (LoginView.EmpleadoActual != null)
                        {
                            mantDb.IdEmpleadoMecanico = LoginView.EmpleadoActual.IdEmpleado;
                        }
                    }

                    var vehiculoDb = await context.Vehiculos.FindAsync(mant.IdVehiculo);
                    if (vehiculoDb != null)
                    {
                        vehiculoDb.IdEstado = 1; // "Disponible"
                    }

                    await context.SaveChangesAsync();

                    MessageBox.Show(
                        $"¡Excelente! El vehículo {infoAuto} ha sido reparado con éxito y liberado nuevamente a la flota operativa.",
                        "Vehículo Disponible",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);

                    await CargarMantenimientosAsync();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error al liberar vehículo: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
}
