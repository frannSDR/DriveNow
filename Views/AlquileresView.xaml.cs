using DriveNow.Data;
using DriveNow.Models;
using DriveNow.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DriveNow.Views
{
    public partial class AlquileresView : UserControl
    {
        private const decimal TarifaPorDefecto = 25000m;
        private ObservableCollection<Alquiler> _historialAlquileres = new ObservableCollection<Alquiler>();
        private Reserva? _reservaSeleccionadaEntrega = null;
        private Alquiler? _alquilerSeleccionadoDevolucion = null;
        private RegistroDanoResultado? _danoPendienteDevolucion = null;

        private List<Reserva> _catalogoReservasPendientes = new();
        private List<Alquiler> _catalogoAlquileresEnCurso = new();

        public AlquileresView()
        {
            InitializeComponent();
            Loaded += AlquileresView_Loaded;
        }

        private async void AlquileresView_Loaded(object sender, RoutedEventArgs e)
        {
            AplicarPermisosSegunRol();
            await CargarHistorialAsync();
            await CargarListadosAyudaAsync();
        }

        private void AplicarPermisosSegunRol()
        {
            if (LoginView.EmpleadoActual == null) return;

            var rol = (RolUsuario)LoginView.EmpleadoActual.IdRol;

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
                    .Include(a => a.Reserva!)
                        .ThenInclude(r => r.Cliente)
                    .Include(a => a.Reserva!)
                        .ThenInclude(r => r.Vehiculo!)
                            .ThenInclude(v => v.Categoria)
                    .Include(a => a.EmpleadoEntrega)      // Auditoría: Quién entregó
                    .Include(a => a.EmpleadoDevolucion)  // Auditoría: Quién recibió
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

        private async Task CargarListadosAyudaAsync()
        {
            try
            {
                using var context = new DriveNowDbContext();
                _catalogoReservasPendientes = await context.Reservas
                    .Include(r => r.Cliente)
                    .Include(r => r.Vehiculo!)
                        .ThenInclude(v => v.Categoria)
                    .Include(r => r.Alquiler)
                    .Include(r => r.Estado)
                    .Where(r => r.Alquiler == null && r.Estado != null && r.Estado.Nombre != "Cancelada")
                    .OrderBy(r => r.FechaDesde)
                    .ToListAsync();

                _catalogoAlquileresEnCurso = await context.Alquileres
                    .Include(a => a.Reserva!)
                        .ThenInclude(r => r.Cliente)
                    .Include(a => a.Reserva!)
                        .ThenInclude(r => r.Vehiculo!)
                            .ThenInclude(v => v.Categoria)
                    .Where(a => a.FechaEntregaDevolucion == null)
                    .OrderByDescending(a => a.IdAlquiler)
                    .ToListAsync();
            }
            catch
            {
                // Manejo silencioso en background
            }
        }

        private async Task SeleccionarReservaParaEntregaAsync(Reserva res)
        {
            _reservaSeleccionadaEntrega = res;
            txtBuscarReservaEntrega.Text = res.CodigoReserva;
            lblDetalleReservaEntrega.Text = $"{res.CodigoReserva}: {res.Vehiculo?.MarcaModelo} ({res.Vehiculo?.Patente}) - {res.Cliente?.NombreCompleto}";
            txtCombustibleSalida.Text = "100";

            int? ultimoKm = await ObtenerUltimoKilometrajeVehiculoAsync(res.IdVehiculo);
            if (ultimoKm.HasValue)
            {
                txtKmSalida.Text = ultimoKm.Value.ToString();
                txtKmSalida.ToolTip = $"Odómetro heredado automáticamente del último retorno ({ultimoKm.Value} km).";
            }
            else
            {
                txtKmSalida.Clear();
                txtKmSalida.ToolTip = "Primer alquiler de la unidad: ingrese el kilometraje del tablero.";
            }
        }

        private async Task<int?> ObtenerUltimoKilometrajeVehiculoAsync(int idVehiculo)
        {
            try
            {
                using var context = new DriveNowDbContext();
                var ultimoAlquiler = await context.Alquileres
                    .Where(a => a.Reserva != null && a.Reserva.IdVehiculo == idVehiculo && a.KilometrajeEntrada.HasValue)
                    .OrderByDescending(a => a.FechaEntregaDevolucion)
                    .FirstOrDefaultAsync();

                return ultimoAlquiler?.KilometrajeEntrada;
            }
            catch
            {
                return null;
            }
        }

        private async void btnVerReservasPendientes_Click(object sender, RoutedEventArgs e)
        {
            await CargarListadosAyudaAsync();
            if (_catalogoReservasPendientes.Count == 0)
            {
                MessageBox.Show("No hay reservas activas pendientes de entrega en este momento.", "Sin Reservas Pendientes", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            lstReservasPendientes.ItemsSource = _catalogoReservasPendientes;
            popupReservasPendientes.IsOpen = true;
        }

        private void txtBuscarReservaEntrega_TextChanged(object sender, TextChangedEventArgs e)
        {
            string filtro = txtBuscarReservaEntrega.Text.Trim().ToLower();
            if (string.IsNullOrEmpty(filtro))
            {
                popupReservasPendientes.IsOpen = false;
                return;
            }

            var coincidencias = _catalogoReservasPendientes.Where(r =>
                r.CodigoReserva.ToLower().Contains(filtro) ||
                (r.Cliente != null && (r.Cliente.NombreCompleto.ToLower().Contains(filtro) || r.Cliente.Dni.ToLower().Contains(filtro))) ||
                (r.Vehiculo != null && (r.Vehiculo.MarcaModelo.ToLower().Contains(filtro) || r.Vehiculo.Patente.ToLower().Contains(filtro)))
            ).Take(8).ToList();

            if (coincidencias.Count > 0)
            {
                lstReservasPendientes.ItemsSource = coincidencias;
                popupReservasPendientes.IsOpen = true;
            }
            else
            {
                popupReservasPendientes.IsOpen = false;
            }
        }

        private async void lstReservasPendientes_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (lstReservasPendientes.SelectedItem is Reserva res)
            {
                popupReservasPendientes.IsOpen = false;
                lstReservasPendientes.SelectedItem = null;
                await SeleccionarReservaParaEntregaAsync(res);
            }
        }

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
                    .Include(r => r.Vehiculo!)
                        .ThenInclude(v => v.Categoria)
                    .Include(r => r.Alquiler)
                    .Include(r => r.Estado)
                    .FirstOrDefaultAsync(r =>
                        r.Alquiler == null &&
                        r.Estado != null && r.Estado.Nombre != "Cancelada" &&
                        ((r.Cliente != null && r.Cliente.Dni == busqueda) || r.IdReserva.ToString() == busqueda.Replace("RES-", "").TrimStart('0')));

                if (_reservaSeleccionadaEntrega != null)
                {
                    await SeleccionarReservaParaEntregaAsync(_reservaSeleccionadaEntrega);
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

        private async void btnRegistrarEntrega_Click(object sender, RoutedEventArgs e)
        {
            if (_reservaSeleccionadaEntrega == null)
            {
                MessageBox.Show("Debe cargar una reserva válida primero.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!decimal.TryParse(txtCombustibleSalida.Text.Trim(), out decimal combustible) || combustible < 25 || combustible > 100)
            {
                MessageBox.Show("El combustible inicial debe ser un porcentaje válido entre 25% y 100% (política reglamentaria de entrega).", "Inspección de Combustible", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!int.TryParse(txtKmSalida.Text.Trim(), out int kmSalida) || kmSalida < 0)
            {
                MessageBox.Show("Ingrese un kilometraje de salida válido.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            int? ultimoKm = await ObtenerUltimoKilometrajeVehiculoAsync(_reservaSeleccionadaEntrega.IdVehiculo);
            if (ultimoKm.HasValue && kmSalida < ultimoKm.Value)
            {
                MessageBox.Show($"El odómetro de salida ({kmSalida} km) no puede ser inferior al último kilometraje registrado de la unidad ({ultimoKm.Value} km).\n\n" +
                                "Verifique el odómetro en el tablero del vehículo.", 
                                "Incoherencia en Odómetro", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                using var context = new DriveNowDbContext();

                var tarifaDiaria = _reservaSeleccionadaEntrega.Vehiculo?.Categoria?.TarifaDiaria ?? TarifaPorDefecto;
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
                    MontoMora = 0,
                    IdEmpleadoEntrega = LoginView.EmpleadoActual?.IdEmpleado
                };

                var estadoAlquilado = await context.Estados.FirstOrDefaultAsync(est => est.Nombre == "Alquilado");
                var vehiculoDb = await context.Vehiculos.FindAsync(_reservaSeleccionadaEntrega.IdVehiculo);
                if (vehiculoDb != null && estadoAlquilado != null)
                {
                    vehiculoDb.IdEstado = estadoAlquilado.IdEstado;
                }

                context.Alquileres.Add(nuevoAlquiler);
                await context.SaveChangesAsync();

                await CargarHistorialAsync();
                await CargarListadosAyudaAsync();

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

        private void SeleccionarAlquilerParaDevolucion(Alquiler alq)
        {
            _alquilerSeleccionadoDevolucion = alq;
            txtBuscarAlquilerDev.Text = alq.CodigoAlquiler;
            string marcaModelo = alq.Reserva?.Vehiculo?.MarcaModelo ?? "Vehículo";
            string clienteNom = alq.Reserva?.Cliente?.NombreCompleto ?? "Cliente";
            lblDetalleAlquilerDev.Text = $"{alq.CodigoAlquiler}: {marcaModelo} - {clienteNom}";
            txtKmEntrada.Text = alq.KilometrajeSalida.ToString();
            txtCombustibleEntrada.Text = "100";
            LimpiarDanoPendiente();
        }

        private async void btnVerAlquileresEnCurso_Click(object sender, RoutedEventArgs e)
        {
            await CargarListadosAyudaAsync();
            if (_catalogoAlquileresEnCurso.Count == 0)
            {
                MessageBox.Show("No hay contratos de alquiler en curso en este momento.", "Sin Alquileres en Curso", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            lstAlquileresEnCurso.ItemsSource = _catalogoAlquileresEnCurso;
            popupAlquileresEnCurso.IsOpen = true;
        }

        private void txtBuscarAlquilerDev_TextChanged(object sender, TextChangedEventArgs e)
        {
            string filtro = txtBuscarAlquilerDev.Text.Trim().ToLower();
            if (string.IsNullOrEmpty(filtro))
            {
                popupAlquileresEnCurso.IsOpen = false;
                return;
            }

            var coincidencias = _catalogoAlquileresEnCurso.Where(a =>
                a.CodigoAlquiler.ToLower().Contains(filtro) ||
                (a.Reserva?.Cliente != null && (a.Reserva.Cliente.NombreCompleto.ToLower().Contains(filtro) || a.Reserva.Cliente.Dni.ToLower().Contains(filtro))) ||
                (a.Reserva?.Vehiculo != null && (a.Reserva.Vehiculo.MarcaModelo.ToLower().Contains(filtro) || a.Reserva.Vehiculo.Patente.ToLower().Contains(filtro)))
            ).Take(8).ToList();

            if (coincidencias.Count > 0)
            {
                lstAlquileresEnCurso.ItemsSource = coincidencias;
                popupAlquileresEnCurso.IsOpen = true;
            }
            else
            {
                popupAlquileresEnCurso.IsOpen = false;
            }
        }

        private void lstAlquileresEnCurso_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (lstAlquileresEnCurso.SelectedItem is Alquiler alq)
            {
                popupAlquileresEnCurso.IsOpen = false;
                lstAlquileresEnCurso.SelectedItem = null;
                SeleccionarAlquilerParaDevolucion(alq);
            }
        }

        private async void btnCargarAlquilerDev_Click(object sender, RoutedEventArgs e)
        {
            string busqueda = txtBuscarAlquilerDev.Text.Trim();
            if (string.IsNullOrEmpty(busqueda)) return;

            try
            {
                using var context = new DriveNowDbContext();
                _alquilerSeleccionadoDevolucion = await context.Alquileres
                    .Include(a => a.Reserva!)
                        .ThenInclude(r => r.Cliente)
                    .Include(a => a.Reserva!)
                        .ThenInclude(r => r.Vehiculo!)
                            .ThenInclude(v => v.Categoria)
                    .FirstOrDefaultAsync(a =>
                        a.FechaEntregaDevolucion == null &&
                        a.Reserva != null && a.Reserva.Vehiculo != null &&
                        (a.Reserva.Vehiculo.Patente == busqueda || 
                         a.IdAlquiler.ToString() == busqueda.Replace("ALQ-", "").TrimStart('0') ||
                         (a.Reserva.Cliente != null && (a.Reserva.Cliente.Dni == busqueda || a.Reserva.Cliente.Nombre.Contains(busqueda) || a.Reserva.Cliente.Apellido.Contains(busqueda)))));

                if (_alquilerSeleccionadoDevolucion != null)
                {
                    SeleccionarAlquilerParaDevolucion(_alquilerSeleccionadoDevolucion);
                }
                else
                {
                    MessageBox.Show("No se encontró ningún alquiler activo con esa patente, código o cliente.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Information);
                    LimpiarDanoPendiente();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al buscar contrato: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LimpiarDanoPendiente()
        {
            _danoPendienteDevolucion = null;
            bdrResumenDano.Visibility = Visibility.Collapsed;
            lblFotoDanioStatus.Text = "✓ Sin novedades ni daños reportados";
            lblFotoDanioStatus.Foreground = new SolidColorBrush(Color.FromRgb(16, 185, 129));
        }

        private void btnInspeccionDanos_Click(object sender, RoutedEventArgs e)
        {
            if (_alquilerSeleccionadoDevolucion == null)
            {
                MessageBox.Show("Debe buscar y cargar un contrato de alquiler activo primero antes de realizar la inspección.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string infoVehiculo = $"{_alquilerSeleccionadoDevolucion.Reserva?.Vehiculo?.MarcaModelo} ({_alquilerSeleccionadoDevolucion.Reserva?.Vehiculo?.Patente})";
            var dlg = new RegistroDanoWindow(infoVehiculo, _danoPendienteDevolucion)
            {
                Owner = Window.GetWindow(this)
            };

            if (dlg.ShowDialog() == true && dlg.Resultado != null)
            {
                _danoPendienteDevolucion = dlg.Resultado;
                bdrResumenDano.Visibility = Visibility.Visible;
                lblDanoRegistradoDetalle.Text = _danoPendienteDevolucion.ResumenTexto;
                lblFotoDanioStatus.Text = "⚠️ Daño registrado para inspección";
                lblFotoDanioStatus.Foreground = new SolidColorBrush(Color.FromRgb(248, 113, 113));
            }
        }

        private void btnQuitarDano_Click(object sender, RoutedEventArgs e)
        {
            LimpiarDanoPendiente();
        }

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

            if (!decimal.TryParse(txtCombustibleEntrada.Text.Trim(), out decimal combEntrada) || combEntrada < 0 || combEntrada > 100)
            {
                MessageBox.Show("El nivel de combustible de retorno debe ser un porcentaje válido entre 0% y 100%.", "Nivel de Combustible Inválido", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                using var context = new DriveNowDbContext();
                var alquilerDb = await context.Alquileres
                    .Include(a => a.Reserva!)
                        .ThenInclude(r => r.Cliente)
                    .Include(a => a.Reserva!)
                        .ThenInclude(r => r.Vehiculo!)
                            .ThenInclude(v => v.Categoria)
                    .Include(a => a.EmpleadoEntrega)
                    .Include(a => a.EmpleadoDevolucion)
                    .FirstOrDefaultAsync(a => a.IdAlquiler == _alquilerSeleccionadoDevolucion.IdAlquiler);

                if (alquilerDb == null) return;

                alquilerDb.FechaEntregaDevolucion = DateTime.Now;
                alquilerDb.KilometrajeEntrada = kmEntrada;
                alquilerDb.CombustibleEntrada = combEntrada;
                alquilerDb.IdEmpleadoDevolucion = LoginView.EmpleadoActual?.IdEmpleado; // Auditoría: Quién recibe

                if (alquilerDb.Reserva != null && alquilerDb.FechaEntregaDevolucion.Value.Date > alquilerDb.Reserva.FechaHasta.Date)
                {
                    int diasRetraso = (alquilerDb.FechaEntregaDevolucion.Value.Date - alquilerDb.Reserva.FechaHasta.Date).Days;
                    decimal tarifaDiaria = alquilerDb.Reserva.Vehiculo?.Categoria?.TarifaDiaria ?? TarifaPorDefecto;
                    alquilerDb.MontoMora = diasRetraso * (tarifaDiaria * 1.5m);
                }

                bool inmovilizado = false;
                if (_danoPendienteDevolucion != null)
                {
                    inmovilizado = _danoPendienteDevolucion.InmovilizarVehiculo;
                    string rutaFotoPersistir = string.Empty;
                    if (!string.IsNullOrEmpty(_danoPendienteDevolucion.RutaFoto) && File.Exists(_danoPendienteDevolucion.RutaFoto))
                    {
                        try
                        {
                            string carpetaEvidencias = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Evidencias");
                            if (!Directory.Exists(carpetaEvidencias))
                            {
                                Directory.CreateDirectory(carpetaEvidencias);
                            }
                            string extension = Path.GetExtension(_danoPendienteDevolucion.RutaFoto);
                            string nombreArchivo = $"DANO_{alquilerDb.CodigoAlquiler}_{DateTime.Now:yyyyMMdd_HHmmss}{extension}";
                            string destino = Path.Combine(carpetaEvidencias, nombreArchivo);
                            File.Copy(_danoPendienteDevolucion.RutaFoto, destino, true);
                            rutaFotoPersistir = destino;
                        }
                        catch
                        {
                            rutaFotoPersistir = _danoPendienteDevolucion.RutaFoto;
                        }
                    }

                    var nuevoDano = new Dano
                    {
                        IdAlquiler = alquilerDb.IdAlquiler,
                        Zona = _danoPendienteDevolucion.Sector,
                        Descripcion = $"{_danoPendienteDevolucion.Gravedad}: {_danoPendienteDevolucion.Descripcion}",
                        EvidenciaFotografica = rutaFotoPersistir
                    };
                    context.Danos.Add(nuevoDano);
                }

                string estadoDestino = inmovilizado ? "En Taller" : "Disponible";
                var estadoObj = await context.Estados.FirstOrDefaultAsync(est => est.Nombre == estadoDestino);
                if (alquilerDb.Reserva?.Vehiculo != null && estadoObj != null)
                {
                    var vehiculo = await context.Vehiculos.FindAsync(alquilerDb.Reserva.Vehiculo.IdVehiculo);
                    if (vehiculo != null) vehiculo.IdEstado = estadoObj.IdEstado;
                }

                await context.SaveChangesAsync();

                // Aseguramos que EmpleadoDevolucion esté asignado para la generación inmediata
                alquilerDb.EmpleadoDevolucion = LoginView.EmpleadoActual;

                await CargarHistorialAsync();
                await CargarListadosAyudaAsync();

                string mensaje = "Vehículo recibido y contrato finalizado.";
                if (alquilerDb.MontoMora > 0)
                    mensaje += $"\n⚠️ Se aplicó una mora por retraso de {alquilerDb.MontoMora:C}.";

                if (inmovilizado)
                    mensaje += "\n🔧 El vehículo fue derivado a Taller / Mantenimiento por gravedad de daños.";

                MessageBox.Show(mensaje, "Check-in Completado", MessageBoxButton.OK, MessageBoxImage.Information);

                // Preguntar si desea expedir la factura oficial en PDF
                var promptFactura = MessageBox.Show(
                    $"¿Desea generar e imprimir la Factura Oficial en formato PDF para el cliente {alquilerDb.Reserva?.Cliente?.NombreCompleto}?",
                    "Expedición de Factura",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (promptFactura == MessageBoxResult.Yes)
                {
                    GenerarYAbrirFactura(alquilerDb);
                }

                _alquilerSeleccionadoDevolucion = null;
                lblDetalleAlquilerDev.Text = "Alquiler: No seleccionado";
                txtBuscarAlquilerDev.Clear();
                LimpiarDanoPendiente();
                txtKmEntrada.Clear();
                txtCombustibleEntrada.Text = "100";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al procesar devolución: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void txtBuscarAlquiler_TextChanged(object sender, TextChangedEventArgs e)
        {
            string filtro = txtBuscarAlquiler.Text.Trim().ToLower();
            if (string.IsNullOrEmpty(filtro))
            {
                dgvHistorial.ItemsSource = _historialAlquileres;
            }
            else
            {
                dgvHistorial.ItemsSource = _historialAlquileres.Where(a =>
                    a.CodigoAlquiler.ToLower().Contains(filtro) ||
                    (a.Reserva?.Cliente != null && a.Reserva.Cliente.NombreCompleto.ToLower().Contains(filtro)) ||
                    (a.Reserva?.Vehiculo != null && a.Reserva.Vehiculo.Patente.ToLower().Contains(filtro)) ||
                    (a.Reserva?.Vehiculo != null && a.Reserva.Vehiculo.MarcaModelo.ToLower().Contains(filtro)) ||
                    (a.EmpleadoEntrega != null && a.EmpleadoEntrega.NombreCompleto.ToLower().Contains(filtro)) ||
                    (a.EmpleadoDevolucion != null && a.EmpleadoDevolucion.NombreCompleto.ToLower().Contains(filtro))
                ).ToList();
            }
        }

        private void btnVerDanos_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is Alquiler alquiler)
            {
                var ventana = new DetalleDanoWindow(alquiler)
                {
                    Owner = Window.GetWindow(this)
                };
                ventana.ShowDialog();
            }
        }

        private void btnGenerarFacturaFila_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is Alquiler alquiler)
            {
                GenerarYAbrirFactura(alquiler);
            }
        }

        private void GenerarYAbrirFactura(Alquiler alquiler)
        {
            try
            {
                var saveDlg = new SaveFileDialog
                {
                    Title = "Guardar Factura en PDF",
                    FileName = $"Factura_{alquiler.CodigoAlquiler}.pdf",
                    DefaultExt = ".pdf",
                    Filter = "Documento PDF (*.pdf)|*.pdf"
                };

                string carpetaDocs = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "DriveNow_Facturas");
                if (!Directory.Exists(carpetaDocs))
                {
                    Directory.CreateDirectory(carpetaDocs);
                }
                saveDlg.InitialDirectory = carpetaDocs;

                if (saveDlg.ShowDialog() == true)
                {
                    var facturaService = App.Services.GetService<IFacturaPdfService>() ?? new FacturaPdfService();
                    facturaService.GenerarFacturaPdf(alquiler, saveDlg.FileName);

                    var res = MessageBox.Show(
                        $"Factura generada exitosamente en:\n{saveDlg.FileName}\n\n¿Desea abrir el archivo PDF ahora?",
                        "Factura Generada",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Information);

                    if (res == MessageBoxResult.Yes)
                    {
                        Process.Start(new ProcessStartInfo(saveDlg.FileName) { UseShellExecute = true });
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al generar factura PDF: {ex.Message}", "Error Facturación", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}