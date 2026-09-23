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
                var query = context.Clientes.AsQueryable();

                int filtroIndex = cmbFiltroEstadoCliente?.SelectedIndex ?? 0;
                if (filtroIndex == 1) // Inactivos / Dados de baja
                {
                    query = query.Where(c => !c.Activo);
                }
                else if (filtroIndex == 2) // Todos los registros
                {
                    // Sin restricción de activo
                }
                else // Solo activos (por defecto)
                {
                    query = query.Where(c => c.Activo);
                }

                var clientesDb = await query
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

        private async void cmbFiltroEstadoCliente_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded) return;
            await CargarClientesDesdeBdAsync();
        }

        private void btnEditarCliente_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is Cliente cliente)
            {
                _clienteEnEdicion = cliente;

                txtNombre.Text = cliente.Nombre;
                txtApellido.Text = cliente.Apellido;
                txtDni.Text = cliente.Dni;
                dpFechaNacimiento.SelectedDate = cliente.FechaNacimiento;
                txtTelefono.Text = cliente.Telefono ?? string.Empty;
                txtCorreo.Text = cliente.Correo ?? string.Empty;
                txtDomicilio.Text = cliente.Domicilio ?? string.Empty;
                txtLocalidad.Text = cliente.Localidad ?? string.Empty;
                txtProvincia.Text = cliente.Provincia ?? string.Empty;
                txtLicencia.Text = cliente.NroLicencia;
                dpVencimiento.SelectedDate = cliente.VencimientoLicencia;
                txtNacionalidad.Text = string.IsNullOrWhiteSpace(cliente.Nacionalidad) ? "Argentina" : cliente.Nacionalidad;
                txtContactoEmergenciaNombre.Text = cliente.ContactoEmergenciaNombre ?? string.Empty;
                txtContactoEmergenciaTelefono.Text = cliente.ContactoEmergenciaTelefono ?? string.Empty;

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
                MessageBox.Show("Por favor, complete todos los campos obligatorios (Nombre, Apellido, DNI, Licencia y Vencimiento).", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Regla de Negocio: Verificación de edad mínima recomendada para alquiler (21 años)
            if (dpFechaNacimiento.SelectedDate.HasValue)
            {
                var fechaNac = dpFechaNacimiento.SelectedDate.Value;
                int edad = DateTime.Today.Year - fechaNac.Year - (DateTime.Today.DayOfYear < fechaNac.DayOfYear ? 1 : 0);
                if (edad < 21)
                {
                    var resp = MessageBox.Show(
                        $"El conductor tiene {edad} años (menor al requisito mínimo de 21 años de la aseguradora).\n\n" +
                        "¿Desea registrarlo de todas formas bajo autorización especial de la gerencia?",
                        "Alerta de Edad Mínima",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning);

                    if (resp != MessageBoxResult.Yes) return;
                }
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
                        FechaNacimiento = dpFechaNacimiento.SelectedDate,
                        Telefono = string.IsNullOrWhiteSpace(txtTelefono.Text) ? null : txtTelefono.Text.Trim(),
                        Correo = string.IsNullOrWhiteSpace(txtCorreo.Text) ? null : txtCorreo.Text.Trim(),
                        Domicilio = string.IsNullOrWhiteSpace(txtDomicilio.Text) ? null : txtDomicilio.Text.Trim(),
                        Localidad = string.IsNullOrWhiteSpace(txtLocalidad.Text) ? null : txtLocalidad.Text.Trim(),
                        Provincia = string.IsNullOrWhiteSpace(txtProvincia.Text) ? null : txtProvincia.Text.Trim(),
                        NroLicencia = licencia,
                        VencimientoLicencia = dpVencimiento.SelectedDate.Value,
                        Nacionalidad = string.IsNullOrWhiteSpace(txtNacionalidad.Text) ? "Argentina" : txtNacionalidad.Text.Trim(),
                        ContactoEmergenciaNombre = string.IsNullOrWhiteSpace(txtContactoEmergenciaNombre.Text) ? null : txtContactoEmergenciaNombre.Text.Trim(),
                        ContactoEmergenciaTelefono = string.IsNullOrWhiteSpace(txtContactoEmergenciaTelefono.Text) ? null : txtContactoEmergenciaTelefono.Text.Trim(),
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
                        clienteDb.FechaNacimiento = dpFechaNacimiento.SelectedDate;
                        clienteDb.Telefono = string.IsNullOrWhiteSpace(txtTelefono.Text) ? null : txtTelefono.Text.Trim();
                        clienteDb.Correo = string.IsNullOrWhiteSpace(txtCorreo.Text) ? null : txtCorreo.Text.Trim();
                        clienteDb.Domicilio = string.IsNullOrWhiteSpace(txtDomicilio.Text) ? null : txtDomicilio.Text.Trim();
                        clienteDb.Localidad = string.IsNullOrWhiteSpace(txtLocalidad.Text) ? null : txtLocalidad.Text.Trim();
                        clienteDb.Provincia = string.IsNullOrWhiteSpace(txtProvincia.Text) ? null : txtProvincia.Text.Trim();
                        clienteDb.NroLicencia = licencia;
                        clienteDb.VencimientoLicencia = dpVencimiento.SelectedDate.Value;
                        clienteDb.Nacionalidad = string.IsNullOrWhiteSpace(txtNacionalidad.Text) ? "Argentina" : txtNacionalidad.Text.Trim();
                        clienteDb.ContactoEmergenciaNombre = string.IsNullOrWhiteSpace(txtContactoEmergenciaNombre.Text) ? null : txtContactoEmergenciaNombre.Text.Trim();
                        clienteDb.ContactoEmergenciaTelefono = string.IsNullOrWhiteSpace(txtContactoEmergenciaTelefono.Text) ? null : txtContactoEmergenciaTelefono.Text.Trim();

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

                        if (_clienteEnEdicion?.IdCliente == cliente.IdCliente)
                        {
                            ResetearFormulario();
                        }

                        MessageBox.Show("Cliente dado de baja exitosamente del padrón activo.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
                        await CargarClientesDesdeBdAsync();
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error al dar de baja al cliente: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async void btnReactivarCliente_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is Cliente cliente)
            {
                var confirmacion = MessageBox.Show(
                    $"¿Desea reactivar a {cliente.NombreCompleto} e incorporarlo nuevamente al padrón activo?",
                    "Confirmar Reactivación",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (confirmacion != MessageBoxResult.Yes) return;

                try
                {
                    using var context = new DriveNowDbContext();
                    var clienteDb = await context.Clientes.FindAsync(cliente.IdCliente);
                    if (clienteDb != null)
                    {
                        clienteDb.Activo = true;
                        await context.SaveChangesAsync();

                        MessageBox.Show($"El cliente {clienteDb.NombreCompleto} ha sido reactivado exitosamente.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
                        await CargarClientesDesdeBdAsync();
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error al reactivar cliente: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
            lblEdadValidada.Text = cliente.Edad.HasValue 
                ? $"Edad: {cliente.Edad.Value} años {(cliente.EsMayorDeEdad ? "(Habilitado)" : "(Requiere autorización)")}"
                : "Edad: No especificada";
            lblDomicilioValidado.Text = $"Domicilio: {cliente.DomicilioCompleto}";
            lblContactoEmergenciaValidado.Text = !string.IsNullOrWhiteSpace(cliente.ContactoEmergenciaNombre)
                ? $"Emergencia: {cliente.ContactoEmergenciaNombre} ({cliente.ContactoEmergenciaTelefono ?? "-"})"
                : "Emergencia: No informado";
            lblVencimientoTexto.Text = $"Estado Licencia (Vence: {cliente.VencimientoLicencia:dd/MM/yyyy})";

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
            dpFechaNacimiento.SelectedDate = null;
            txtTelefono.Clear();
            txtCorreo.Clear();
            txtDomicilio.Clear();
            txtLocalidad.Clear();
            txtProvincia.Clear();
            txtLicencia.Clear();
            dpVencimiento.SelectedDate = DateTime.Today.AddYears(2);
            txtNacionalidad.Text = "Argentina";
            txtContactoEmergenciaNombre.Clear();
            txtContactoEmergenciaTelefono.Clear();

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
                    (c.Correo != null && c.Correo.ToLower().Contains(filtro)) ||
                    (c.Domicilio != null && c.Domicilio.ToLower().Contains(filtro)) ||
                    (c.Localidad != null && c.Localidad.ToLower().Contains(filtro)) ||
                    (c.Provincia != null && c.Provincia.ToLower().Contains(filtro))
                ).ToList();
            }
        }
    }
}