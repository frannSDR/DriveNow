using DriveNow.Data;
using DriveNow.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace DriveNow.Views
{
    public class VehiculoDisponibleDto
    {
        public int IdVehiculo { get; set; }
        public string Patente { get; set; } = string.Empty;
        public string MarcaModelo { get; set; } = string.Empty;
        public string CategoriaNombre { get; set; } = string.Empty;
        public decimal TarifaDiaria { get; set; }
        public string TextoMostrar => $"{Patente} - {MarcaModelo} ({CategoriaNombre})";
    }

    public class KpiCategoriaDto
    {
        public string Nombre { get; set; } = string.Empty;
        public string RatioTexto { get; set; } = string.Empty;
        public double Porcentaje { get; set; }
    }

    public partial class ReservasView : UserControl
    {
        private ObservableCollection<Reserva> _listaReservas = new ObservableCollection<Reserva>();
        private List<Vehiculo> _catalogoVehiculos = new List<Vehiculo>();
        private List<Categoria> _catalogoCategorias = new List<Categoria>();
        private List<Cliente> _catalogoClientes = new List<Cliente>();
        private Cliente? _clienteActual = null;

        public ReservasView()
        {
            InitializeComponent();
            dpFechaInicio.SelectedDate = DateTime.Today;
            dpFechaFin.SelectedDate = DateTime.Today.AddDays(3);
            Loaded += ReservasView_Loaded;
        }

        private async void ReservasView_Loaded(object sender, RoutedEventArgs e)
        {
            AplicarPermisosSegunRol();
            await CargarDatosInicialesAsync();
        }

        private void AplicarPermisosSegunRol()
        {
            if (LoginView.EmpleadoActual == null) return;

            var rol = (RolUsuario)LoginView.EmpleadoActual.IdRol;

            if (rol == RolUsuario.Dueno)
            {
                btnNuevaReserva.Visibility = Visibility.Collapsed;
                colAccionesReserva.Visibility = Visibility.Collapsed;
            }
            else
            {
                btnNuevaReserva.Visibility = Visibility.Visible;
                colAccionesReserva.Visibility = Visibility.Visible;
            }
        }

        private async Task CargarDatosInicialesAsync()
        {
            try
            {
                using var context = new DriveNowDbContext();

                _catalogoCategorias = await context.Categorias.ToListAsync();

                var listaCombo = new List<Categoria>
                {
                    new Categoria { IdCategoria = 0, Nombre = "Todas las categorías" }
                };
                listaCombo.AddRange(_catalogoCategorias);
                cmbCategoriaReserva.ItemsSource = listaCombo;
                cmbCategoriaReserva.SelectedIndex = 0;

                _catalogoVehiculos = await context.Vehiculos
                    .Include(v => v.Categoria)
                    .Include(v => v.Estado)
                    .Where(v => v.Activo)
                    .ToListAsync();

                _catalogoClientes = await context.Clientes
                    .Where(c => c.Activo)
                    .OrderBy(c => c.Apellido)
                    .ThenBy(c => c.Nombre)
                    .ToListAsync();

                var reservas = await context.Reservas
                    .Include(r => r.Cliente)
                    .Include(r => r.Empleado)
                    .Include(r => r.Vehiculo!)
                        .ThenInclude(v => v.Categoria)
                    .Include(r => r.Estado)
                    .OrderByDescending(r => r.IdReserva)
                    .ToListAsync();

                _listaReservas = new ObservableCollection<Reserva>(reservas);
                dgvReservas.ItemsSource = _listaReservas;

                RefrescarVehiculosDisponibles();
                ActualizarTableroDisponibilidad();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cargar reservas: {ex.Message}", "Error de Conexión", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnNuevaReserva_Click(object sender, RoutedEventArgs e)
        {
            LimpiarFormulario();
            modalOverlay.Visibility = Visibility.Visible;
        }

        private void btnCerrarModal_Click(object sender, RoutedEventArgs e)
        {
            LimpiarFormulario();
            modalOverlay.Visibility = Visibility.Collapsed;
        }

        private void txtBuscarClienteReserva_TextChanged(object sender, TextChangedEventArgs e)
        {
            string filtro = txtBuscarClienteReserva.Text.Trim().ToLower();
            if (string.IsNullOrEmpty(filtro))
            {
                popupSugerenciasClientes.IsOpen = false;
                return;
            }

            var coincidencias = _catalogoClientes.Where(c =>
                c.Dni.ToLower().Contains(filtro) ||
                c.Nombre.ToLower().Contains(filtro) ||
                c.Apellido.ToLower().Contains(filtro) ||
                c.NombreCompleto.ToLower().Contains(filtro)).Take(8).ToList();

            if (coincidencias.Count > 0)
            {
                lstSugerenciasClientes.ItemsSource = coincidencias;
                popupSugerenciasClientes.IsOpen = true;
            }
            else
            {
                popupSugerenciasClientes.IsOpen = false;
            }
        }

        private void btnVerPadronClientes_Click(object sender, RoutedEventArgs e)
        {
            if (_catalogoClientes.Count == 0)
            {
                MessageBox.Show("No hay clientes activos registrados en el padrón.", "Padrón Vacío", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            lstSugerenciasClientes.ItemsSource = _catalogoClientes.Take(25).ToList();
            popupSugerenciasClientes.IsOpen = true;
        }

        private void lstSugerenciasClientes_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (lstSugerenciasClientes.SelectedItem is Cliente cliente)
            {
                popupSugerenciasClientes.IsOpen = false;
                lstSugerenciasClientes.SelectedItem = null;

                if (!cliente.LicenciaVigente)
                {
                    MessageBox.Show(
                        $"El cliente {cliente.NombreCompleto} posee la licencia de conducir VENCIDA ({cliente.VencimientoLicencia:dd/MM/yyyy}).\n\n" +
                        "Las políticas de la rentadora y aseguradora prohíben reservar unidades a conductores inhabilitados.",
                        "Bloqueo Operativo",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                SeleccionarCliente(cliente);
            }
        }

        private void SeleccionarCliente(Cliente cliente)
        {
            _clienteActual = cliente;
            lblClienteSeleccionadoNombre.Text = $"👤 {cliente.NombreCompleto}";
            lblClienteSeleccionadoInfo.Text = $"DNI: {cliente.Dni}  •  Tel: {cliente.Telefono ?? "-"}  •  Licencia: {cliente.NroLicencia}";

            pnlBuscadorCliente.Visibility = Visibility.Collapsed;
            pnlClienteSeleccionado.Visibility = Visibility.Visible;
        }

        private void btnCambiarCliente_Click(object sender, RoutedEventArgs e)
        {
            ResetearSeleccionCliente();
        }

        private void ResetearSeleccionCliente()
        {
            _clienteActual = null;
            txtBuscarClienteReserva.Clear();
            popupSugerenciasClientes.IsOpen = false;
            pnlClienteSeleccionado.Visibility = Visibility.Collapsed;
            pnlBuscadorCliente.Visibility = Visibility.Visible;
        }

        private void dpFechas_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            RefrescarVehiculosDisponibles();
            CalcularPresupuesto();
        }

        private void cmbCategoriaReserva_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            RefrescarVehiculosDisponibles();
        }

        private void cmbVehiculosDisponibles_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            CalcularPresupuesto();
        }

        private void RefrescarVehiculosDisponibles()
        {
            if (cmbVehiculosDisponibles == null || !dpFechaInicio.SelectedDate.HasValue || !dpFechaFin.SelectedDate.HasValue)
                return;

            DateTime inicio = dpFechaInicio.SelectedDate.Value.Date;
            DateTime fin = dpFechaFin.SelectedDate.Value.Date;

            int idCatSeleccionada = (cmbCategoriaReserva.SelectedValue is int id) ? id : 0;

            var disponibles = _catalogoVehiculos.Where(v =>
            {
                if (idCatSeleccionada > 0 && v.IdCategoria != idCatSeleccionada)
                    return false;

                if (v.Estado?.Nombre == "En Taller" || v.Estado?.Nombre == "Mantenimiento")
                    return false;

                bool superpuesta = _listaReservas.Any(r =>
                    r.IdVehiculo == v.IdVehiculo &&
                    r.Estado?.Nombre != "Cancelada" &&
                    ((inicio >= r.FechaDesde && inicio <= r.FechaHasta) ||
                     (fin >= r.FechaDesde && fin <= r.FechaHasta) ||
                     (inicio <= r.FechaDesde && fin >= r.FechaHasta)));

                return !superpuesta;
            })
            .Select(v => new VehiculoDisponibleDto
            {
                IdVehiculo = v.IdVehiculo,
                Patente = v.Patente,
                MarcaModelo = v.MarcaModelo,
                CategoriaNombre = v.Categoria?.Nombre ?? "Sin Cat.",
                TarifaDiaria = v.Categoria?.TarifaDiaria ?? 0
            })
            .ToList();

            cmbVehiculosDisponibles.ItemsSource = disponibles;
            if (disponibles.Count > 0)
                cmbVehiculosDisponibles.SelectedIndex = 0;
            else
                CalcularPresupuesto();
        }

        private void CalcularPresupuesto()
        {
            if (lblTotalPresupuesto == null) return;

            if (!dpFechaInicio.SelectedDate.HasValue || !dpFechaFin.SelectedDate.HasValue)
            {
                lblDuracionDias.Text = "Duración: 0 días";
                lblTotalPresupuesto.Text = "$0.00";
                return;
            }

            int dias = (dpFechaFin.SelectedDate.Value.Date - dpFechaInicio.SelectedDate.Value.Date).Days;
            if (dias <= 0) dias = 1;

            lblDuracionDias.Text = $"Duración: {dias} día(s)";

            if (cmbVehiculosDisponibles.SelectedItem is VehiculoDisponibleDto auto)
            {
                lblTarifaAplicada.Text = $"Tarifa por día: {auto.TarifaDiaria:C}";
                decimal total = dias * auto.TarifaDiaria;
                lblTotalPresupuesto.Text = total.ToString("C");
            }
            else
            {
                lblTarifaAplicada.Text = "Tarifa por día: $0.00";
                lblTotalPresupuesto.Text = "$0.00";
            }
        }

        private void ActualizarTableroDisponibilidad()
        {
            int total = _catalogoVehiculos.Count;
            if (total == 0) return;

            int ocupados = _catalogoVehiculos.Count(v => v.Estado?.Nombre == "Alquilado" || v.Estado?.Nombre == "Reservado");
            int porcentaje = (int)((double)ocupados / total * 100);
            lblPorcentajeOcupacion.Text = $"{porcentaje}%";

            var listaKpis = new List<KpiCategoriaDto>();
            foreach (var cat in _catalogoCategorias)
            {
                int totalCat = _catalogoVehiculos.Count(v => v.IdCategoria == cat.IdCategoria);
                int dispCat = _catalogoVehiculos.Count(v => v.IdCategoria == cat.IdCategoria && v.Estado?.Nombre == "Disponible");

                listaKpis.Add(new KpiCategoriaDto
                {
                    Nombre = cat.Nombre,
                    RatioTexto = $"{dispCat}/{totalCat}",
                    Porcentaje = totalCat > 0 ? ((double)dispCat / totalCat) * 100 : 0
                });
            }

            icCategoriasOcupacion.ItemsSource = listaKpis;
        }

        private async void btnConfirmarReserva_Click(object sender, RoutedEventArgs e)
        {
            if (_clienteActual == null)
            {
                MessageBox.Show("Debe validar un cliente antes de crear la reserva.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!dpFechaInicio.SelectedDate.HasValue || !dpFechaFin.SelectedDate.HasValue)
            {
                MessageBox.Show("Seleccione el rango de fechas.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (dpFechaFin.SelectedDate.Value < dpFechaInicio.SelectedDate.Value)
            {
                MessageBox.Show("La fecha de fin no puede ser anterior a la fecha de inicio.", "Error en Fechas", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (cmbVehiculosDisponibles.SelectedItem is not VehiculoDisponibleDto autoSeleccionado)
            {
                MessageBox.Show("No hay vehículos disponibles seleccionados para las fechas elegidas.", "Sin disponibilidad", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            DateTime inicio = dpFechaInicio.SelectedDate.Value.Date;
            DateTime fin = dpFechaFin.SelectedDate.Value.Date;
            int idVehiculo = autoSeleccionado.IdVehiculo;

            try
            {
                using var context = new DriveNowDbContext();

                var vehiculoDb = await context.Vehiculos
                    .Include(v => v.Estado)
                    .FirstOrDefaultAsync(v => v.IdVehiculo == idVehiculo);

                if (vehiculoDb == null || !vehiculoDb.Activo || vehiculoDb.Estado?.Nombre == "En Taller" || vehiculoDb.Estado?.Nombre == "Mantenimiento")
                {
                    MessageBox.Show("El vehículo seleccionado ya no está disponible (inactivo o en taller). Por favor seleccione otro.", "Vehículo No Disponible", MessageBoxButton.OK, MessageBoxImage.Warning);
                    await CargarDatosInicialesAsync();
                    return;
                }

                bool existeSuperposicion = await context.Reservas.AnyAsync(r =>
                    r.IdVehiculo == idVehiculo &&
                    r.Estado != null && r.Estado.Nombre != "Cancelada" &&
                    inicio <= r.FechaHasta && fin >= r.FechaDesde);

                if (existeSuperposicion)
                {
                    MessageBox.Show("El vehículo seleccionado acaba de ser reservado por otro operador para las fechas indicadas. Por favor elija otro auto.", "Conflicto de Reserva", MessageBoxButton.OK, MessageBoxImage.Warning);
                    await CargarDatosInicialesAsync();
                    return;
                }

                var estadoConfirmada = await context.Estados.FirstOrDefaultAsync(est => est.Nombre == "Confirmada" || est.Nombre == "Reservado")
                                       ?? await context.Estados.FirstAsync();

                int idEmpleado = LoginView.EmpleadoActual?.IdEmpleado ?? 1;

                var nueva = new Reserva
                {
                    FechaDesde = inicio,
                    FechaHasta = fin,
                    IdCliente = _clienteActual.IdCliente,
                    IdVehiculo = idVehiculo,
                    IdEmpleado = idEmpleado,
                    IdEstado = estadoConfirmada.IdEstado
                };

                context.Reservas.Add(nueva);
                await context.SaveChangesAsync();

                nueva.Cliente = _clienteActual;
                nueva.Vehiculo = _catalogoVehiculos.First(v => v.IdVehiculo == idVehiculo);
                nueva.Estado = estadoConfirmada;
                nueva.Empleado = LoginView.EmpleadoActual;

                _listaReservas.Insert(0, nueva);
                RefrescarVehiculosDisponibles();
                ActualizarTableroDisponibilidad();

                MessageBox.Show($"Reserva {nueva.CodigoReserva} registrada con éxito.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);

                modalOverlay.Visibility = Visibility.Collapsed;
                LimpiarFormulario();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al guardar reserva: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void btnCancelarReservaFila_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is Reserva reserva)
            {
                if (reserva.Estado?.Nombre == "Cancelada")
                {
                    MessageBox.Show("Esta reserva ya se encuentra cancelada.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                if (reserva.Estado?.Nombre == "Alquilado" || reserva.Alquiler != null)
                {
                    MessageBox.Show("No se puede cancelar una reserva que ya cuenta con un alquiler en curso o finalizado.", "Acción Denegada", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var res = MessageBox.Show($"¿Desea cancelar la reserva {reserva.CodigoReserva}?", "Cancelar Reserva", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (res != MessageBoxResult.Yes) return;

                try
                {
                    using var context = new DriveNowDbContext();
                    var estadoCancelada = await context.Estados.FirstOrDefaultAsync(est => est.Nombre == "Cancelada");
                    var reservaDb = await context.Reservas.FindAsync(reserva.IdReserva);

                    if (reservaDb != null && estadoCancelada != null)
                    {
                        reservaDb.IdEstado = estadoCancelada.IdEstado;
                        await context.SaveChangesAsync();

                        reserva.Estado = estadoCancelada;
                        dgvReservas.Items.Refresh();
                        RefrescarVehiculosDisponibles();
                        ActualizarTableroDisponibilidad();

                        MessageBox.Show("Reserva cancelada exitosamente.", "Información", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error al cancelar: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void LimpiarFormulario()
        {
            ResetearSeleccionCliente();
            dpFechaInicio.SelectedDate = DateTime.Today;
            dpFechaFin.SelectedDate = DateTime.Today.AddDays(3);
            CalcularPresupuesto();
        }

        private void txtBuscarReserva_TextChanged(object sender, TextChangedEventArgs e)
        {
            string filtro = txtBuscarReserva.Text.Trim().ToLower();
            if (string.IsNullOrEmpty(filtro))
            {
                dgvReservas.ItemsSource = _listaReservas;
            }
            else
            {
                dgvReservas.ItemsSource = _listaReservas.Where(r =>
                    r.CodigoReserva.ToLower().Contains(filtro) ||
                    (r.Cliente != null && r.Cliente.NombreCompleto.ToLower().Contains(filtro)) ||
                    (r.Cliente != null && r.Cliente.Dni.ToLower().Contains(filtro)) ||
                    (r.Vehiculo != null && r.Vehiculo.Patente.ToLower().Contains(filtro)) ||
                    (r.Vehiculo != null && r.Vehiculo.MarcaModelo.ToLower().Contains(filtro)) ||
                    (r.Empleado != null && r.Empleado.NombreCompleto.ToLower().Contains(filtro)) ||
                    (r.Estado != null && r.Estado.Nombre.ToLower().Contains(filtro))
                ).ToList();
            }
        }
    }
}