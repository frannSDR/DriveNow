using DriveNow.Data;
using DriveNow.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Identity.Client.NativeInterop;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net;
using System.Security.Policy;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace DriveNow.Views
{
    public partial class ClientesView : UserControl
    {
        private ObservableCollection<Cliente> _listaClientes = new ObservableCollection<Cliente>();
        private Cliente? _clienteEnEdicion = null; // null = Alta, con valor = Edición

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

            // El Dueño tiene acceso de supervisión ejecutiva (solo lectura)
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
                var clientesDb = await context.Clientes
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

        // Cargar datos en el formulario para editar
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
                btnGuardarCliente.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981")); // Verde esmeralda
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

                // Validación de unicidad de DNI excluyendo al cliente en edición si corresponde
                bool existeDni = await context.Clientes.AnyAsync(c =>
                    c.Dni == dni &&
                    (_clienteEnEdicion == null || c.IdCliente != _clienteEnEdicion.IdCliente));

                if (existeDni)
                {
                    MessageBox.Show("Ya existe otro cliente registrado con ese número de DNI.", "DNI Duplicado", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Validación de unicidad de Licencia excluyendo al cliente en edición si corresponde
                bool existeLicencia = await context.Clientes.AnyAsync(c =>
                    c.NroLicencia == licencia &&
                    (_clienteEnEdicion == null || c.IdCliente != _clienteEnEdicion.IdCliente));

                if (existeLicencia)
                {
                    MessageBox.Show("Ya existe otro cliente registrado con ese número de licencia.", "Licencia Duplicada", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (_clienteEnEdicion == null)
                {
                    // --- ALTA NUEVA ---
                    var nuevoCliente = new Cliente
                    {
                        Nombre = txtNombre.Text.Trim(),
                        Apellido = txtApellido.Text.Trim(),
                        Dni = dni,
                        Telefono = string.IsNullOrWhiteSpace(txtTelefono.Text) ? null : txtTelefono.Text.Trim(),
                        Correo = string.IsNullOrWhiteSpace(txtCorreo.Text) ? null : txtCorreo.Text.Trim(),
                        NroLicencia = licencia,
                        VencimientoLicencia = dpVencimiento.SelectedDate.Value
                    }
                    ;

                    context.Clientes.Add(nuevoCliente); 
                    await context.SaveChangesAsync(); 

                    _listaClientes.Insert(0, nuevoCliente); 
                    MostrarTarjetaValidacion(nuevoCliente); 
                    MessageBox.Show("Cliente registrado exitosamente en la base de datos.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    // --- ACTUALIZACIÓN ---
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

                        // Actualizamos la referencia visual y la tarjeta
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
                var confirmacion = MessageBox.Show($"¿Desea eliminar a {cliente.NombreCompleto}?", "Confirmar Eliminación", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (confirmacion != MessageBoxResult.Yes) return; 

                try
                {
                    using var context = new DriveNowDbContext();

                    bool tieneReservas = await context.Reservas.AnyAsync(r => r.IdCliente == cliente.IdCliente);
                    if (tieneReservas) 
                    {
                        MessageBox.Show("No se puede eliminar el cliente porque posee historial de reservas o alquileres.", "Operación Bloqueada", MessageBoxButton.OK, MessageBoxImage.Warning); 
                        return; 
                    }

                    var clienteABorrar = await context.Clientes.FindAsync(cliente.IdCliente); 
                    if (clienteABorrar != null) 
                    {
                        context.Clientes.Remove(clienteABorrar); 
                        await context.SaveChangesAsync(); 
                        _listaClientes.Remove(cliente); 

                        if (_clienteEnEdicion?.IdCliente == cliente.IdCliente)
                        {
                            ResetearFormulario();
                        }

                        MessageBox.Show("Cliente eliminado.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information); 
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error al eliminar del servidor: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error); 
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
                    .FirstOrDefaultAsync(c => c.Dni == dniBuscado);

                if (cliente != null) 
                {
                    MostrarTarjetaValidacion(cliente);
                }
                else
                {
                    MessageBox.Show("Cliente no encontrado en el padrón del servidor.", "Sin Resultados", MessageBoxButton.OK, MessageBoxImage.Warning); 
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
    }
}