using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.EntityFrameworkCore;
using DriveNow.Data;
using DriveNow.Models;
using DriveNow.Security;

namespace DriveNow.Views
{
    public partial class MiCuentaView : UserControl
    {
        public MiCuentaView()
        {
            InitializeComponent();
            Loaded += (s, e) => CargarDatosPerfil();
        }

        private void CargarDatosPerfil()
        {
            var empleado = LoginView.EmpleadoActual;
            if (empleado == null) return; 

            lblNombreCompleto.Text = empleado.NombreCompleto; 
            lblDni.Text = empleado.Dni;
            lblEmail.Text = empleado.Email;
            lblRolBadge.Text = empleado.Rol?.Nombre?.ToUpper() ?? "SIN ROL";

            // Extraer iniciales para el avatar
            string inicialNombre = !string.IsNullOrEmpty(empleado.Nombre) ? empleado.Nombre[0].ToString() : "D"; 
            string inicialApellido = !string.IsNullOrEmpty(empleado.Apellido) ? empleado.Apellido[0].ToString() : "N";
            lblInicialesAvatar.Text = $"{inicialNombre}{inicialApellido}".ToUpper();
        }

        private async void btnActualizarPass_Click(object sender, RoutedEventArgs e)
        {
            string passActual = txtPassActual.Password.Trim();
            string passNueva = txtPassNueva.Password.Trim();
            string passConfirmar = txtPassConfirmar.Password.Trim();

            if (string.IsNullOrWhiteSpace(passActual) || string.IsNullOrWhiteSpace(passNueva) || string.IsNullOrWhiteSpace(passConfirmar))
            {
                MessageBox.Show("Complete todos los campos de contraseña.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (passNueva.Length < 6)
            {
                MessageBox.Show("La nueva contraseña debe contener un mínimo de 6 caracteres.", "Requisito de Seguridad", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (passNueva != passConfirmar)
            {
                MessageBox.Show("La nueva contraseña y su confirmación no coinciden.", "Discrepancia", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (passActual == passNueva)
            {
                MessageBox.Show("La nueva contraseña no puede ser idéntica a la actual.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var usuarioSesion = LoginView.EmpleadoActual;
                if (usuarioSesion == null) return; 

                using var context = new DriveNowDbContext(); 
                var empleadoDb = await context.Empleados.FindAsync(usuarioSesion.IdEmpleado);

                if (empleadoDb == null)
                {
                    MessageBox.Show("No se encontró el registro del empleado en la base de datos.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // 1. Validar la contraseña actual con BCrypt
                bool passCorrecta = BCrypt.Net.BCrypt.Verify(passActual, empleadoDb.PasswordHash);
                if (!passCorrecta)
                {
                    MessageBox.Show("La contraseña actual ingresada es incorrecta.", "Autenticación Fallida", MessageBoxButton.OK, MessageBoxImage.Error);
                    txtPassActual.Clear();
                    txtPassActual.Focus();
                    return;
                }

                // 2. Hashear la nueva contraseña y persistir
                empleadoDb.PasswordHash = PasswordHelper.HashearPassword(passNueva); 
                await context.SaveChangesAsync();

                // Actualizar hash en memoria de la sesión activa
                usuarioSesion.PasswordHash = empleadoDb.PasswordHash;

                MessageBox.Show("Tu contraseña ha sido actualizada exitosamente.", "Seguridad Actualizada", MessageBoxButton.OK, MessageBoxImage.Information);
                LimpiarCamposPassword();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al actualizar credenciales: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error); 
            }
        }

        private void btnLimpiarPass_Click(object sender, RoutedEventArgs e) => LimpiarCamposPassword();

        private void LimpiarCamposPassword()
        {
            txtPassActual.Clear();
            txtPassNueva.Clear();
            txtPassConfirmar.Clear();
        }
    }
}