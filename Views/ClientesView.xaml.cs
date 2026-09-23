using DriveNow.Data;
using DriveNow.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DriveNow.Views
{
    public partial class ClientesView : UserControl
    {
        private ObservableCollection<Cliente> _listaClientes = new ObservableCollection<Cliente>();
        private Cliente? _clienteEnEdicion = null;

        public ClientesView()
        {
            InitializeComponent();
            dpVencimiento.SelectedDate = DateTime.Today.AddYears(2);
            Loaded += ClientesView_Loaded;
        }

        private async void ClientesView_Loaded(object sender, RoutedEventArgs e)
        {
            AplicarPermisosSegunRol();
            await CargarClientesDesdeBdAsync();
        }

        private void AplicarPermisosSegunRol()
        {
            if (LoginView.EmpleadoActual == null) return;

            var rol = (RolUsuario)LoginView.EmpleadoActual.IdRol;

            if (rol == RolUsuario.Dueno)
            {
                pnlFormularioCliente.Visibility = Visibility.Collapsed;
                colAccionesCliente.Visibility = Visibility.Collapsed;
            }
            else
            {
                pnlFormularioCliente.Visibility = Visibility.Visible;
                colAccionesCliente.Visibility = Visibility.Visible;
            }
        }

        private async Task CargarClientesDesdeBdAsync()
        {
            try
            {
                using var context = new DriveNowDbContext();
                // Solo cargamos clientes activos
                var clientesDb = await context.Clientes
                    .Where(c => c.Activo)
                    .OrderByDescending(c => c.IdCliente)
                    .ToListAsync();

                _listaClientes = new ObservableCollection<Cliente>(clientesDb);
                dgvClientes.ItemsSource = _listaClientes;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al conectar con la base de datos: {ex.Message}", "Error de Conexión", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnEditarCliente_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is Cliente cliente)
            {
                _clienteEnEdicion = cliente;

                txtNombre.Text = cliente.Nombre;
                txtApellido.Text = cliente.Apellido;
                txtDni.Text = cliente.Dni;
                txtTelefono.Text = cliente.Telefono ?? string.Empty;
                txtCorreo.Text = cliente.Correo ?? string.Empty;
                txtLicencia.Text = cliente.NroLicencia;
                dpVencimiento.SelectedDate = cliente.VencimientoLicencia;

                MostrarTarjetaValidacion(cliente);

                lblTituloFormularioCliente.Text = $"✏️ Editando: {cliente.NombreCompleto}";
                btnGuardarCliente.Content = "Actualizar Cliente";
                btnGuardarCliente.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981"));
                btnCancelarEdicion.Visibility = Visibility.Visible;
                btnLimpiarCliente.Visibility = Visibility.Collapsed;
            }
        }

        private async void btnGuardarCliente_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtNombre.Text) ||
                string.IsNullOrWhiteSpace(txtApellido.Text) ||
                string.IsNullOrWhiteSpace(txtDni.Text) ||
                string.IsNullOrWhiteSpace(txtLicencia.Text) ||
                !dpVencimiento.SelectedDate.HasValue)
            {
                MessageBox.Show("Por favor, complete todos los campos obligatorios.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string dni = txtDni.Text.Trim().ToUpper();
            string licencia = txtLicencia.Text.Trim().ToUpper();

            try
            {
                using var context = new DriveNowDbContext();

                bool existeDni = await context.Clientes.AnyAsync(c =>
                    c.Dni == dni &&
                    c.Activo &&
                    (_clienteEnEdicion == null || c.IdCliente != _clienteEnEdicion.IdCliente));

                if (existeDni)
                {
                    MessageBox.Show("Ya existe otro cliente activo registrado con ese número de DNI.", "DNI Duplicado", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                bool existeLicencia = await context.Clientes.AnyAsync(c =>
                    c.NroLicencia == licencia &&
                    c.Activo &&
                    (_clienteEnEdicion == null || c.IdCliente != _clienteEnEdicion.IdCliente));

                if (existeLicencia)
                {
                    MessageBox.Show("Ya existe otro cliente activo registrado con ese número de licencia.", "Licencia Duplicada", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (_clienteEnEdicion == null)
                {
                    var nuevoCliente = new Cliente
                    {
                        Nombre = txtNombre.Text.Trim(),
                        Apellido = txtApellido.Text.Trim(),
                        Dni = dni,
                        Telefono = string.IsNullOrWhiteSpace(txtTelefono.Text) ? null : txtTelefono.Text.Trim(),
                        Correo = string.IsNullOrWhiteSpace(txtCorreo.Text) ? null : txtCorreo.Text.Trim(),
                        NroLicencia = licencia,
                        VencimientoLicencia = dpVencimiento.SelectedDate.Value,
                        Activo = true
                    };

                    context.Clientes.Add(nuevoCliente);
                    await context.SaveChangesAsync();

                    _listaClientes.Insert(0, nuevoCliente);
                    MostrarTarjetaValidacion(nuevoCliente);
                    MessageBox.Show("Cliente registrado exitosamente.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    var clienteDb = await context.Clientes.FindAsync(_clienteEnEdicion.IdCliente);
                    if (clienteDb != null)
                    {
                        clienteDb.Nombre = txtNombre.Text.Trim();
                        clienteDb.Apellido = txtApellido.Text.Trim();
                        clienteDb.Dni = dni;
                        clienteDb.Telefono = string.IsNullOrWhiteSpace(txtTelefono.Text) ? null : txtTelefono.Text.Trim();
                        clienteDb.Correo = string.IsNullOrWhiteSpace(txtCorreo.Text) ? null : txtCorreo.Text.Trim();
                        clienteDb.NroLicencia = licencia;
                        clienteDb.VencimientoLicencia = dpVencimiento.SelectedDate.Value;

                        await context.SaveChangesAsync();

                        MostrarTarjetaValidacion(clienteDb);
                        await CargarClientesDesdeBdAsync();

                        MessageBox.Show("Datos del cliente actualizados correctamente.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }

                ResetearFormulario();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al guardar en el servidor: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void btnEliminarCliente_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is Cliente cliente)
            {
                var confirmacion = MessageBox.Show($"¿Desea dar de baja a {cliente.NombreCompleto}?", "Confirmar Baja", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (confirmacion != MessageBoxResult.Yes) return;

                try
                {
                    using var context = new DriveNowDbContext();

                    // Borrado lógico: cambiamos bandera a inactivo
                    var clienteADesactivar = await context.Clientes.FindAsync(cliente.IdCliente);
                    if (clienteADesactivar != null)
                    {
                        clienteADesactivar.Activo = false;
                        await context.SaveChangesAsync();
                        _listaClientes.Remove(cliente);

                        if (_clienteEnEdicion?.IdCliente == cliente.IdCliente)
                        {
                            ResetearFormulario();
                        }

                        MessageBox.Show("Cliente dado de baja exitosamente del padrón activo.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error al dar de baja al cliente: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async void btnBuscarValidacion_Click(object sender, RoutedEventArgs e)
        {
            string dniBuscado = txtValidarDni.Text.Trim();
            if (string.IsNullOrWhiteSpace(dniBuscado)) return;

            try
            {
                using var context = new DriveNowDbContext();
                var cliente = await context.Clientes
                    .FirstOrDefaultAsync(c => c.Dni == dniBuscado && c.Activo);

                if (cliente != null)
                {
                    MostrarTarjetaValidacion(cliente);
                }
                else
                {
                    MessageBox.Show("Cliente no encontrado en el padrón activo.", "Sin Resultados", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error de consulta: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
            lblVencimientoTexto.Text = $"Vence: {cliente.VencimientoLicencia:dd/MM/yyyy}";

            if (!cliente.LicenciaVigente)
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

        private void btnCancelarEdicion_Click(object sender, RoutedEventArgs e) => ResetearFormulario();
        private void btnLimpiar_Click(object sender, RoutedEventArgs e) => ResetearFormulario();

        private void ResetearFormulario()
        {
            _clienteEnEdicion = null;

            txtNombre.Clear();
            txtApellido.Clear();
            txtDni.Clear();
            txtTelefono.Clear();
            txtCorreo.Clear();
            txtLicencia.Clear();
            dpVencimiento.SelectedDate = DateTime.Today.AddYears(2);

            lblTituloFormularioCliente.Text = "👤 Registro de cliente";
            btnGuardarCliente.Content = "Guardar Registro";
            btnGuardarCliente.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2563EB"));
            btnCancelarEdicion.Visibility = Visibility.Collapsed;
            btnLimpiarCliente.Visibility = Visibility.Visible;
        }

        private void txtBuscarCliente_TextChanged(object sender, TextChangedEventArgs e)
        {
            string filtro = txtBuscarCliente.Text.Trim().ToLower();
            if (string.IsNullOrEmpty(filtro))
            {
                dgvClientes.ItemsSource = _listaClientes;
            }
            else
            {
                dgvClientes.ItemsSource = _listaClientes.Where(c =>
                    c.NombreCompleto.ToLower().Contains(filtro) ||
                    c.Dni.ToLower().Contains(filtro) ||
                    c.NroLicencia.ToLower().Contains(filtro) ||
                    (c.Telefono != null && c.Telefono.ToLower().Contains(filtro)) ||
                    (c.Correo != null && c.Correo.ToLower().Contains(filtro))
                ).ToList();
            }
        }
    }
}