using DriveNow.Data;
using DriveNow.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Security.Policy;
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
                pnlFormularioReserva.Visibility = Visibility.Collapsed;
                colAccionesReserva.Visibility = Visibility.Collapsed;
            }
            else
            {
                pnlFormularioReserva.Visibility = Visibility.Visible;
                colAccionesReserva.Visibility = Visibility.Visible;
            }
        }

        private async Task CargarDatosInicialesAsync()
        {
            try
            {
                using var context = new DriveNowDbContext(); 

                _catalogoCategorias = await context.Categorias.ToListAsync();

                // Combo de Categorías: Opción inicial "Todas" + categorías reales
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
                    .ToListAsync(); 

                var reservas = await context.Reservas
                    .Include(r => r.Cliente)
                    .Include(r => r.Vehiculo)
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

        private async void btnBuscarClienteReserva_Click(object sender, RoutedEventArgs e)
        {
            string busqueda = txtDniCliente.Text.Trim(); 
            if (string.IsNullOrWhiteSpace(busqueda)) return; 

            try
            {
                using var context = new DriveNowDbContext(); 
                _clienteActual = await context.Clientes.FirstOrDefaultAsync(c =>  
                    c.Dni == busqueda || (c.Nombre + " " + c.Apellido).Contains(busqueda)); 

                if (_clienteActual != null) 
                {
                    if (!_clienteActual.LicenciaVigente) 
                    {
                        MessageBox.Show("El cliente tiene la licencia de conducir vencida. No puede realizar reservas.", "Bloqueo Operativo", MessageBoxButton.OK, MessageBoxImage.Warning); 
                        _clienteActual = null; 
                        lblClienteSeleccionado.Text = "Cliente: Bloqueado (Licencia Vencida)"; 
                        return;  
                    }
                    lblClienteSeleccionado.Text = $"Cliente: {_clienteActual.NombreCompleto} (DNI: {_clienteActual.Dni})"; 
                }
                else
                {
                    MessageBox.Show("Cliente no encontrado en la base de datos.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Information); 
                    lblClienteSeleccionado.Text = "Cliente: No encontrado"; 
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error); 
            }
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
            // Filtro por categoría (0 = Todas)
            if (idCatSeleccionada > 0 && v.IdCategoria != idCatSeleccionada)
                return false;

            // Vehículos en taller no se reservan
            if (v.Estado?.Nombre == "En Taller" || v.Estado?.Nombre == "Mantenimiento")
                return false;

            // Superposición de fechas con reservas no canceladas 
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
            if (dias <= 0) dias = 1; // Mínimo 1 día de facturación

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

            try
            {
                using var context = new DriveNowDbContext(); 

                var estadoConfirmada = await context.Estados.FirstOrDefaultAsync(est => est.Nombre == "Confirmada" || est.Nombre == "Reservado") 
                                       ?? await context.Estados.FirstAsync();  

                int idEmpleado = LoginView.EmpleadoActual?.IdEmpleado ?? 1;  

                var nueva = new Reserva 
                {
                    FechaDesde = dpFechaInicio.SelectedDate.Value.Date, 
                    FechaHasta = dpFechaFin.SelectedDate.Value.Date, 
                    IdCliente = _clienteActual.IdCliente, 
                    IdVehiculo = autoSeleccionado.IdVehiculo, 
                    IdEmpleado = idEmpleado,
                    IdEstado = estadoConfirmada.IdEstado
                }
                ;

                context.Reservas.Add(nueva); 
                await context.SaveChangesAsync(); 

                // Reasociar navegación para que la tabla muestre los nombres de inmediato
                nueva.Cliente = _clienteActual; 
                nueva.Vehiculo = _catalogoVehiculos.First(v => v.IdVehiculo == autoSeleccionado.IdVehiculo); 
                nueva.Estado = estadoConfirmada; 

                _listaReservas.Insert(0, nueva); 
                RefrescarVehiculosDisponibles(); 
                ActualizarTableroDisponibilidad(); 

                MessageBox.Show($"Reserva {nueva.CodigoReserva} registrada con éxito.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
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

        private void btnCancelarReserva_Click(object sender, RoutedEventArgs e) => LimpiarFormulario(); 

        private void LimpiarFormulario()
        {
            _clienteActual = null; 
            txtDniCliente.Clear(); 
            lblClienteSeleccionado.Text = "Cliente: No seleccionado"; 
            dpFechaInicio.SelectedDate = DateTime.Today; 
            dpFechaFin.SelectedDate = DateTime.Today.AddDays(3); 
            CalcularPresupuesto();
        }
    }
}