using DriveNow.Data;
using DriveNow.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Win32;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Security.Policy;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace DriveNow.Views
{
    public partial class AlquileresView : UserControl
    {
        private ObservableCollection<Alquiler> _historialAlquileres = new ObservableCollection<Alquiler>();
        private Reserva? _reservaSeleccionadaEntrega = null;
        private Alquiler? _alquilerSeleccionadoDevolucion = null;
        private string _rutaFotoDanioSeleccionada = string.Empty;

        public AlquileresView()
        {
            InitializeComponent();
            Loaded += AlquileresView_Loaded;
        }

        private async void AlquileresView_Loaded(object sender, RoutedEventArgs e)
        {
            AplicarPermisosSegunRol();
            await CargarHistorialAsync();
        }

        private void AplicarPermisosSegunRol()
        {
            if (LoginView.EmpleadoActual == null) return;

            var rol = (RolUsuario)LoginView.EmpleadoActual.IdRol;

            // El Dueño tiene supervisión ejecutiva (solo lectura)
            if (rol == RolUsuario.Dueno)
            {
                pnlOperacionesAlquiler.Visibility = Visibility.Collapsed;
            }
            else
            {
                pnlOperacionesAlquiler.Visibility = Visibility.Visible;
            }
        }

        private async Task CargarHistorialAsync()
        {
            try
            {
                using var context = new DriveNowDbContext(); 
                var alquileres = await context.Alquileres
                    .Include(a => a.Reserva)
                        .ThenInclude(r => r.Cliente)
                    .Include(a => a.Reserva)
                        .ThenInclude(r => r.Vehiculo)
                            .ThenInclude(v => v.Categoria)
                    .Include(a => a.Danos)
                    .OrderByDescending(a => a.IdAlquiler)
                    .ToListAsync(); 

                _historialAlquileres = new ObservableCollection<Alquiler>(alquileres); 
                dgvHistorial.ItemsSource = _historialAlquileres; 
                ActualizarKPIs(); 
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cargar contratos de alquiler: {ex.Message}", "Error de Conexión", MessageBoxButton.OK, MessageBoxImage.Error); 
            }
        }

        private void ActualizarKPIs()
        {
            lblKpiActivos.Text = _historialAlquileres.Count(a => a.FechaEntregaDevolucion == null).ToString(); 
            lblKpiDevueltos.Text = _historialAlquileres.Count(a => a.FechaEntregaDevolucion.HasValue && a.FechaEntregaDevolucion.Value.Date == DateTime.Today).ToString(); 
            lblKpiMora.Text = _historialAlquileres.Count(a => a.FechaEntregaDevolucion == null && a.Reserva != null && DateTime.Today > a.Reserva.FechaHasta.Date).ToString();
            lblKpiTotal.Text = _historialAlquileres.Count.ToString();
        }

        // 1. CARGAR RESERVA PARA ENTREGA
        private async void btnCargarReservaEntrega_Click(object sender, RoutedEventArgs e)
        {
            string busqueda = txtBuscarReservaEntrega.Text.Trim();
            if (string.IsNullOrEmpty(busqueda)) 
            {
                MessageBox.Show("Ingrese el código de reserva o DNI del cliente.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning); 
                return; 
            }

            try
            {
                using var context = new DriveNowDbContext(); 

                _reservaSeleccionadaEntrega = await context.Reservas
                    .Include(r => r.Cliente)
                    .Include(r => r.Vehiculo)
                        .ThenInclude(v => v.Categoria)
                    .Include(r => r.Alquiler)
                    .Include(r => r.Estado)
                    .FirstOrDefaultAsync(r =>
                        r.Alquiler == null &&
                        r.Estado.Nombre != "Cancelada" &&
                        (r.Cliente.Dni == busqueda || r.IdReserva.ToString() == busqueda.Replace("RES-", "").TrimStart('0'))); 

                if (_reservaSeleccionadaEntrega != null)
                {
                    lblDetalleReservaEntrega.Text = $"{_reservaSeleccionadaEntrega.CodigoReserva}: {_reservaSeleccionadaEntrega.Vehiculo?.MarcaModelo} ({_reservaSeleccionadaEntrega.Vehiculo?.Patente}) - {_reservaSeleccionadaEntrega.Cliente?.NombreCompleto}";
                }
                else
                {
                    MessageBox.Show("No se encontró ninguna reserva activa pendiente de entrega para ese criterio.", "Sin Resultados", MessageBoxButton.OK, MessageBoxImage.Information);
                    lblDetalleReservaEntrega.Text = "Reserva: No seleccionada"; 
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al consultar: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error); 
            }
        }

        // 2. REGISTRAR ENTREGA (CHECK-OUT)
        private async void btnRegistrarEntrega_Click(object sender, RoutedEventArgs e)
        {
            if (_reservaSeleccionadaEntrega == null) 
            {
                MessageBox.Show("Debe cargar una reserva válida primero.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
                return; 
            }

            if (!decimal.TryParse(txtCombustibleSalida.Text.Trim(), out decimal combustible) || combustible < 25) 
            {
                MessageBox.Show("El combustible inicial no puede ser inferior al 25% reglamentario.", "Inspección", MessageBoxButton.OK, MessageBoxImage.Warning);
                return; 
            }

            if (!int.TryParse(txtKmSalida.Text.Trim(), out int kmSalida) || kmSalida < 0) 
            {
                MessageBox.Show("Ingrese un kilometraje de salida válido.", "Error", MessageBoxButton.OK, MessageBoxImage.Error); 
                return; 
            }

            try
            {
                using var context = new DriveNowDbContext();

                var tarifaDiaria = _reservaSeleccionadaEntrega.Vehiculo?.Categoria?.TarifaDiaria ?? 25000; 
                int dias = (_reservaSeleccionadaEntrega.FechaHasta.Date - _reservaSeleccionadaEntrega.FechaDesde.Date).Days;
                if (dias <= 0) dias = 1;
                decimal costoBase = tarifaDiaria * dias;

                var nuevoAlquiler = new Alquiler
                {
                    IdReserva = _reservaSeleccionadaEntrega.IdReserva,
                    FechaEntregaReal = DateTime.Now,
                    CombustibleSalida = combustible,
                    KilometrajeSalida = kmSalida,
                    CostoTotal = costoBase,
                    MontoMora = 0
                }
                ;

                // Actualizar estado del vehículo a 'Alquilado'[cite: 12]
                var estadoAlquilado = await context.Estados.FirstOrDefaultAsync(est => est.Nombre == "Alquilado"); 
                var vehiculoDb = await context.Vehiculos.FindAsync(_reservaSeleccionadaEntrega.IdVehiculo); 
                if (vehiculoDb != null && estadoAlquilado != null) 
                {
                    vehiculoDb.IdEstado = estadoAlquilado.IdEstado; 
                }

                context.Alquileres.Add(nuevoAlquiler); 
                await context.SaveChangesAsync();

                await CargarHistorialAsync(); 

                MessageBox.Show($"Entrega de llaves completada. Contrato ALQ-{nuevoAlquiler.IdAlquiler:D4} iniciado.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);

                _reservaSeleccionadaEntrega = null; 
                lblDetalleReservaEntrega.Text = "Reserva: No seleccionada"; 
                txtBuscarReservaEntrega.Clear(); 
                txtKmSalida.Clear();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al registrar entrega: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error); 
            }
        }

        // 3. CARGAR ALQUILER PARA DEVOLUCIÓN
        private async void btnCargarAlquilerDev_Click(object sender, RoutedEventArgs e)
        {
            string busqueda = txtBuscarAlquilerDev.Text.Trim();
            if (string.IsNullOrEmpty(busqueda)) return; 

            try
            {
                using var context = new DriveNowDbContext();
                _alquilerSeleccionadoDevolucion = await context.Alquileres
                    .Include(a => a.Reserva)
                        .ThenInclude(r => r.Cliente)
                    .Include(a => a.Reserva)
                        .ThenInclude(r => r.Vehiculo)
                            .ThenInclude(v => v.Categoria)
                    .FirstOrDefaultAsync(a => 
                        a.FechaEntregaDevolucion == null && 
                        (a.Reserva.Vehiculo.Patente == busqueda || a.IdAlquiler.ToString() == busqueda.Replace("ALQ-", "").TrimStart('0'))); 

                if (_alquilerSeleccionadoDevolucion != null)
                {
                    lblDetalleAlquilerDev.Text = $"ALQ-{_alquilerSeleccionadoDevolucion.IdAlquiler:D4}: {_alquilerSeleccionadoDevolucion.Reserva.Vehiculo.MarcaModelo} - {_alquilerSeleccionadoDevolucion.Reserva.Cliente.NombreCompleto}";
                    txtKmEntrada.Text = _alquilerSeleccionadoDevolucion.KilometrajeSalida.ToString();
                }
                else
                {
                    MessageBox.Show("No se encontró ningún alquiler activo con esa patente o código.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Information); 
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al buscar contrato: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnAdjuntarDanio_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dlg = new OpenFileDialog { Filter = "Imágenes|*.jpg;*.png;*.jpeg" }; 
            if (dlg.ShowDialog() == true) 
            {
                _rutaFotoDanioSeleccionada = dlg.FileName; 
                lblFotoDanioStatus.Text = $"Foto: {Path.GetFileName(_rutaFotoDanioSeleccionada)}";
            }
        }

        // 4. FINALIZAR ALQUILER (CHECK-IN)
        private async void btnFinalizarAlquiler_Click(object sender, RoutedEventArgs e)
        {
            if (_alquilerSeleccionadoDevolucion == null) 
            {
                MessageBox.Show("Debe cargar un contrato de alquiler activo primero.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning); 
                return; 
            }

            if (!int.TryParse(txtKmEntrada.Text.Trim(), out int kmEntrada) || kmEntrada < _alquilerSeleccionadoDevolucion.KilometrajeSalida) 
            {
                MessageBox.Show($"El kilometraje final no puede ser menor al inicial ({_alquilerSeleccionadoDevolucion.KilometrajeSalida} km).", "Error en Kilometraje", MessageBoxButton.OK, MessageBoxImage.Error);
                return; 
            }

            decimal combEntrada = decimal.TryParse(txtCombustibleEntrada.Text.Trim(), out decimal c) ? c : 100; 

            try
            {
                using var context = new DriveNowDbContext();
                var alquilerDb = await context.Alquileres
                    .Include(a => a.Reserva)
                        .ThenInclude(r => r.Vehiculo)
                            .ThenInclude(v => v.Categoria)
                    .FirstOrDefaultAsync(a => a.IdAlquiler == _alquilerSeleccionadoDevolucion.IdAlquiler); 

                if (alquilerDb == null) return; 

                alquilerDb.FechaEntregaDevolucion = DateTime.Now;
                alquilerDb.KilometrajeEntrada = kmEntrada; 
                alquilerDb.CombustibleEntrada = combEntrada; 

                // Cálculo de mora: días de retraso * (tarifa diaria * 1.5 de penalización)
                if (alquilerDb.Reserva != null && alquilerDb.FechaEntregaDevolucion.Value.Date > alquilerDb.Reserva.FechaHasta.Date) 
                {
                    int diasRetraso = (alquilerDb.FechaEntregaDevolucion.Value.Date - alquilerDb.Reserva.FechaHasta.Date).Days; 
                    decimal tarifaDiaria = alquilerDb.Reserva.Vehiculo?.Categoria?.TarifaDiaria ?? 30000;
                    alquilerDb.MontoMora = diasRetraso * (tarifaDiaria * 1.5m);
                }

                // Registro de daño si se adjuntó evidencia
                if (!string.IsNullOrEmpty(_rutaFotoDanioSeleccionada)) 
                {
                    var nuevoDano = new Dano
                    {
                        IdAlquiler = alquilerDb.IdAlquiler,
                        Zona = "Carrocería / General",
                        Descripcion = "Daño detectado en la inspección de retorno",
                        EvidenciaFotografica = _rutaFotoDanioSeleccionada
                    }
                    ;
                    context.Danos.Add(nuevoDano); 
                }

                // Transición del vehículo: 'En Taller' si hubo daño, 'Disponible' si retornó en orden
                string estadoDestino = !string.IsNullOrEmpty(_rutaFotoDanioSeleccionada) ? "En Taller" : "Disponible"; 
                var estadoObj = await context.Estados.FirstOrDefaultAsync(est => est.Nombre == estadoDestino); 
                if (alquilerDb.Reserva?.Vehiculo != null && estadoObj != null) 
                {
                    var vehiculo = await context.Vehiculos.FindAsync(alquilerDb.Reserva.Vehiculo.IdVehiculo); 
                    if (vehiculo != null) vehiculo.IdEstado = estadoObj.IdEstado; 
                }

                await context.SaveChangesAsync();

                await CargarHistorialAsync(); 

                string mensaje = "Vehículo recibido y contrato finalizado."; 
                if (alquilerDb.MontoMora > 0) 
                    mensaje += $"\n⚠️ Se aplicó una mora por retraso de {alquilerDb.MontoMora:C}.";

                if (!string.IsNullOrEmpty(_rutaFotoDanioSeleccionada)) 
                    mensaje += "\n🔧 El vehículo fue derivado automáticamente a Taller por daño registrado.";

                MessageBox.Show(mensaje, "Check-in Completado", MessageBoxButton.OK, MessageBoxImage.Information);

                _alquilerSeleccionadoDevolucion = null; 
                lblDetalleAlquilerDev.Text = "Alquiler: No seleccionado"; 
                txtBuscarAlquilerDev.Clear(); 
                _rutaFotoDanioSeleccionada = string.Empty; 
                lblFotoDanioStatus.Text = "Sin daños reportados"; 
                txtKmEntrada.Clear();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al procesar devolución: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error); 
            }
        }
    }
}