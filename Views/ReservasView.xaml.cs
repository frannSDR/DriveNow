using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using DriveNow.Models;

namespace DriveNow.Views
{
    public partial class ReservasView : UserControl
    {
        private ObservableCollection<Reserva> listaReservas = new ObservableCollection<Reserva>();
        private List<Vehiculo> catalogoVehiculos = new List<Vehiculo>();
        private List<Cliente> catalogoClientes = new List<Cliente>();
        private Cliente clienteActual = null;

        public ReservasView()
        {
            InitializeComponent();
            InicializarDatos();
            dgvReservas.ItemsSource = listaReservas;
            dpFechaInicio.SelectedDate = DateTime.Now.Date;
            dpFechaFin.SelectedDate = DateTime.Now.Date.AddDays(3);
            RefrescarVehiculosDisponibles();
        }

        private void InicializarDatos()
        {
            // Catálogos en memoria para cruce de datos
            catalogoClientes.Add(new Cliente { Dni = "45892011M", NombreCompleto = "Carlos Mendoza", VencimientoLicencia = DateTime.Now.AddYears(1) });
            catalogoClientes.Add(new Cliente { Dni = "71234568T", NombreCompleto = "Lucía Ferreyra", VencimientoLicencia = DateTime.Now.AddYears(2) });

            catalogoVehiculos.Add(new Vehiculo { Patente = "AB 123 CD", Marca = "Toyota", Modelo = "Yaris", Categoria = "Compacto", Estado = "Disponible" });
            catalogoVehiculos.Add(new Vehiculo { Patente = "EF 456 GH", Marca = "Ford", Modelo = "Ranger", Categoria = "Pickup", Estado = "Disponible" });
            catalogoVehiculos.Add(new Vehiculo { Patente = "IJ 789 KL", Marca = "VW", Modelo = "Vento", Categoria = "Sedán", Estado = "Disponible" });
            catalogoVehiculos.Add(new Vehiculo { Patente = "MN 321 OP", Marca = "Jeep", Modelo = "Renegade", Categoria = "SUV", Estado = "Disponible" });

            // Reservas iniciales de prueba
            listaReservas.Add(new Reserva
            {
                IdReserva = 1,
                CodigoReserva = "RES-1001",
                DniCliente = "45892011M",
                NombreCliente = "Carlos Mendoza",
                PatenteVehiculo = "AB 123 CD",
                VehiculoDetalle = "Toyota Yaris (Compacto)",
                Categoria = "Compacto",
                FechaInicio = DateTime.Now.Date.AddDays(1),
                FechaFin = DateTime.Now.Date.AddDays(4),
                Estado = "Activa"
            });
        }

        private void btnBuscarClienteReserva_Click(object sender, RoutedEventArgs e)
        {
            string busqueda = txtDniCliente.Text.Trim();
            clienteActual = catalogoClientes.FirstOrDefault(c =>
                c.Dni.Equals(busqueda, StringComparison.OrdinalIgnoreCase) ||
                c.NombreCompleto.IndexOf(busqueda, StringComparison.OrdinalIgnoreCase) >= 0);

            if (clienteActual != null)
            {
                if (clienteActual.LicenciaVencida)
                {
                    MessageBox.Show("El cliente tiene la licencia de conducir vencida. No puede realizar reservas.", "Bloqueo Operativo", MessageBoxButton.OK, MessageBoxImage.Warning);
                    clienteActual = null;
                    lblClienteSeleccionado.Text = "Cliente: Bloqueado (Licencia Vencida)";
                    return;
                }
                lblClienteSeleccionado.Text = $"Cliente: {clienteActual.NombreCompleto} (DNI: {clienteActual.Dni})";
            }
            else
            {
                MessageBox.Show("Cliente no encontrado. Debe registrarlo primero en Gestión de Clientes.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Information);
                lblClienteSeleccionado.Text = "Cliente: No encontrado";
            }
        }

        private void dpFechas_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            RefrescarVehiculosDisponibles();
        }

        private void cmbCategoriaReserva_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            RefrescarVehiculosDisponibles();
        }

        private void RefrescarVehiculosDisponibles()
        {
            if (cmbVehiculosDisponibles == null || !dpFechaInicio.SelectedDate.HasValue || !dpFechaFin.SelectedDate.HasValue)
                return;

            DateTime inicio = dpFechaInicio.SelectedDate.Value.Date;
            DateTime fin = dpFechaFin.SelectedDate.Value.Date;
            string catSeleccionada = ((ComboBoxItem)cmbCategoriaReserva.SelectedItem)?.Content.ToString() ?? "Todas";

            // Filtramos vehículos que NO tengan reservas superpuestas en el rango
            var disponibles = catalogoVehiculos.Where(v =>
            {
                if (catSeleccionada != "Todas" && v.Categoria != catSeleccionada)
                    return false;

                // Validación RF-12: Superposición de fechas
                bool superpuesta = listaReservas.Any(r =>
                    r.PatenteVehiculo == v.Patente &&
                    r.Estado != "Cancelada" &&
                    ((inicio >= r.FechaInicio && inicio <= r.FechaFin) ||
                     (fin >= r.FechaInicio && fin <= r.FechaFin) ||
                     (inicio <= r.FechaInicio && fin >= r.FechaFin)));

                return !superpuesta;
            }).Select(v => $"{v.Patente} - {v.MarcaModelo} ({v.Categoria})").ToList();

            cmbVehiculosDisponibles.ItemsSource = disponibles;
            if (disponibles.Count > 0)
                cmbVehiculosDisponibles.SelectedIndex = 0;
        }

        private void btnConfirmarReserva_Click(object sender, RoutedEventArgs e)
        {
            if (clienteActual == null)
            {
                MessageBox.Show("Debe validar un cliente antes de crear la reserva.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!dpFechaInicio.SelectedDate.HasValue || !dpFechaFin.SelectedDate.HasValue)
            {
                MessageBox.Show("Seleccione el rango de fechas de inicio y fin.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (dpFechaFin.SelectedDate.Value < dpFechaInicio.SelectedDate.Value)
            {
                MessageBox.Show("La fecha de fin no puede ser anterior a la fecha de inicio.", "Error en Fechas", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (cmbVehiculosDisponibles.SelectedItem == null)
            {
                MessageBox.Show("No hay vehículos disponibles seleccionados para las fechas elegidas.", "Sin disponibilidad", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string autoTexto = cmbVehiculosDisponibles.SelectedItem.ToString();
            string patente = autoTexto.Split('-')[0].Trim();
            var vehiculo = catalogoVehiculos.First(v => v.Patente == patente);

            Reserva nueva = new Reserva
            {
                IdReserva = listaReservas.Count + 1,
                CodigoReserva = $"RES-{1000 + listaReservas.Count + 1}",
                DniCliente = clienteActual.Dni,
                NombreCliente = clienteActual.NombreCompleto,
                PatenteVehiculo = vehiculo.Patente,
                VehiculoDetalle = $"{vehiculo.MarcaModelo} ({vehiculo.Categoria})",
                Categoria = vehiculo.Categoria,
                FechaInicio = dpFechaInicio.SelectedDate.Value.Date,
                FechaFin = dpFechaFin.SelectedDate.Value.Date,
                Estado = "Activa"
            };

            listaReservas.Add(nueva);
            RefrescarVehiculosDisponibles();
            MessageBox.Show($"Reserva {nueva.CodigoReserva} confirmada con éxito.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
            LimpiarFormulario();
        }

        private void btnCancelarReservaFila_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is Reserva reserva)
            {
                // Regla RF-15: Impedir cancelación si está en curso
                if (reserva.Estado == "En Curso")
                {
                    MessageBox.Show("No se puede cancelar una reserva que ya se encuentra 'En Curso'.", "Acción Denegada", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                MessageBoxResult res = MessageBox.Show($"¿Desea cancelar la reserva {reserva.CodigoReserva} de {reserva.NombreCliente}?", "Cancelar Reserva", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (res == MessageBoxResult.Yes)
                {
                    reserva.Estado = "Cancelada";
                    dgvReservas.Items.Refresh();
                    RefrescarVehiculosDisponibles();
                }
            }
        }

        private void btnCancelarReserva_Click(object sender, RoutedEventArgs e) => LimpiarFormulario();

        private void LimpiarFormulario()
        {
            clienteActual = null;
            txtDniCliente.Clear();
            lblClienteSeleccionado.Text = "Cliente: No seleccionado";
            dpFechaInicio.SelectedDate = DateTime.Now.Date;
            dpFechaFin.SelectedDate = DateTime.Now.Date.AddDays(3);
        }
    }
}