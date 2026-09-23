using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.EntityFrameworkCore;
using DriveNow.Data;
using DriveNow.Models;
using DriveNow.Security;

namespace DriveNow.Views
{
    public partial class UsuariosView : UserControl
    {
        private ObservableCollection<Empleado> _listaEmpleados = new ObservableCollection<Empleado>();
        private Empleado? _empleadoEnEdicion = null;

        public UsuariosView()
        {
            InitializeComponent();
            Loaded += async (s, e) => await InicializarDatosAsync();
        }

        private async Task InicializarDatosAsync()
        {
            try
            {
                using var context = new DriveNowDbContext();

                var roles = await context.Roles.ToListAsync();
                cmbRoles.ItemsSource = roles;
                if (roles.Count > 0) cmbRoles.SelectedIndex = 0;

                await CargarEmpleadosAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cargar datos de personal: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task CargarEmpleadosAsync()
        {
            using var context = new DriveNowDbContext();
            // Filtrar únicamente los empleados activos
            var empleados = await context.Empleados
                .Include(emp => emp.Rol)
                .Where(emp => emp.Activo)
                .OrderBy(emp => emp.IdEmpleado)
                .ToListAsync();

            _listaEmpleados = new ObservableCollection<Empleado>(empleados);
            dgvEmpleados.ItemsSource = _listaEmpleados;
        }

        private void btnEditarEmpleado_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is Empleado emp)
            {
                _empleadoEnEdicion = emp;

                txtDni.Text = emp.Dni;
                txtNombre.Text = emp.Nombre;
                txtApellido.Text = emp.Apellido;
                txtEmail.Text = emp.Email;
                txtPassword.Clear();
                cmbRoles.SelectedValue = emp.IdRol;

                lblTituloFormulario.Text = $"✏️ Editando Personal: {emp.NombreCompleto}";
                lblPassAviso.Text = "Nueva Contraseña (dejar en blanco para mantener actual)";
                btnGuardarUsuario.Content = "Actualizar Cambios";
                btnGuardarUsuario.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981"));
                btnCancelarEdicion.Visibility = Visibility.Visible;
                btnLimpiar.Visibility = Visibility.Collapsed;
            }
        }

        private async void btnGuardarUsuario_Click(object sender, RoutedEventArgs e)
        {
            string dni = txtDni.Text.Trim();
            string nombre = txtNombre.Text.Trim();
            string apellido = txtApellido.Text.Trim();
            string email = txtEmail.Text.Trim();
            string pass = txtPassword.Password.Trim();

            if (string.IsNullOrWhiteSpace(dni) || string.IsNullOrWhiteSpace(nombre) ||
                string.IsNullOrWhiteSpace(apellido) || string.IsNullOrWhiteSpace(email) ||
                cmbRoles.SelectedValue == null)
            {
                MessageBox.Show("Complete todos los campos obligatorios.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (_empleadoEnEdicion == null && string.IsNullOrWhiteSpace(pass))
            {
                MessageBox.Show("Debe asignar una contraseña inicial para crear al empleado.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            int idRol = (int)cmbRoles.SelectedValue;

            try
            {
                using var context = new DriveNowDbContext();

                bool emailExiste = await context.Empleados.AnyAsync(emp =>
                    emp.Email == email &&
                    emp.Activo &&
                    (_empleadoEnEdicion == null || emp.IdEmpleado != _empleadoEnEdicion.IdEmpleado));

                if (emailExiste)
                {
                    MessageBox.Show("Ya existe otro empleado activo registrado con ese correo.", "Email Duplicado", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                bool dniExiste = await context.Empleados.AnyAsync(emp =>
                    emp.Dni == dni &&
                    emp.Activo &&
                    (_empleadoEnEdicion == null || emp.IdEmpleado != _empleadoEnEdicion.IdEmpleado));

                if (dniExiste)
                {
                    MessageBox.Show("Ya existe otro empleado activo registrado con ese DNI.", "DNI Duplicado", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (_empleadoEnEdicion == null)
                {
                    var nuevo = new Empleado
                    {
                        Dni = dni,
                        Nombre = nombre,
                        Apellido = apellido,
                        Email = email,
                        PasswordHash = PasswordHelper.HashearPassword(pass),
                        IdRol = idRol,
                        Activo = true
                    };

                    context.Empleados.Add(nuevo);
                    await context.SaveChangesAsync();
                    MessageBox.Show("Empleado registrado exitosamente.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    var empDb = await context.Empleados.FindAsync(_empleadoEnEdicion.IdEmpleado);
                    if (empDb != null)
                    {
                        empDb.Dni = dni;
                        empDb.Nombre = nombre;
                        empDb.Apellido = apellido;
                        empDb.Email = email;
                        empDb.IdRol = idRol;

                        if (!string.IsNullOrWhiteSpace(pass))
                        {
                            empDb.PasswordHash = PasswordHelper.HashearPassword(pass);
                        }

                        await context.SaveChangesAsync();
                        MessageBox.Show("Datos actualizados correctamente.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }

                await CargarEmpleadosAsync();
                ResetearFormulario();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al guardar en el servidor: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnCancelarEdicion_Click(object sender, RoutedEventArgs e) => ResetearFormulario();
        private void btnLimpiar_Click(object sender, RoutedEventArgs e) => ResetearFormulario();

        private void ResetearFormulario()
        {
            _empleadoEnEdicion = null;

            txtDni.Clear();
            txtNombre.Clear();
            txtApellido.Clear();
            txtEmail.Clear();
            txtPassword.Clear();
            if (cmbRoles.Items.Count > 0) cmbRoles.SelectedIndex = 0;

            lblTituloFormulario.Text = "➕ Registrar Nuevo Miembro del Personal";
            lblPassAviso.Text = "Contraseña Inicial";
            btnGuardarUsuario.Content = "Guardar Usuario";
            btnGuardarUsuario.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2563EB"));
            btnCancelarEdicion.Visibility = Visibility.Collapsed;
            btnLimpiar.Visibility = Visibility.Visible;
        }

        private async void btnEliminarEmpleado_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is Empleado empSeleccionado)
            {
                if (empSeleccionado.IdEmpleado == LoginView.EmpleadoActual?.IdEmpleado)
                {
                    MessageBox.Show("No puedes dar de baja tu propia cuenta en sesión activa.", "Acción Bloqueada", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var res = MessageBox.Show($"¿Desea dar de baja a {empSeleccionado.NombreCompleto} ({empSeleccionado.Rol?.Nombre})?", "Confirmar Baja", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (res != MessageBoxResult.Yes) return;

                try
                {
                    using var context = new DriveNowDbContext();

                    var empADesactivar = await context.Empleados.FindAsync(empSeleccionado.IdEmpleado);
                    if (empADesactivar != null)
                    {
                        // Borrado lógico
                        empADesactivar.Activo = false;
                        await context.SaveChangesAsync();
                        _listaEmpleados.Remove(empSeleccionado);

                        if (_empleadoEnEdicion?.IdEmpleado == empSeleccionado.IdEmpleado)
                        {
                            ResetearFormulario();
                        }

                        MessageBox.Show("Empleado dado de baja exitosamente.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error al procesar la baja: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void txtBuscarEmpleado_TextChanged(object sender, TextChangedEventArgs e)
        {
            string filtro = txtBuscarEmpleado.Text.Trim().ToLower();
            if (string.IsNullOrEmpty(filtro))
            {
                dgvEmpleados.ItemsSource = _listaEmpleados;
            }
            else
            {
                dgvEmpleados.ItemsSource = _listaEmpleados.Where(emp =>
                    emp.NombreCompleto.ToLower().Contains(filtro) ||
                    emp.Dni.ToLower().Contains(filtro) ||
                    emp.Email.ToLower().Contains(filtro)).ToList();
            }
        }
    }
}