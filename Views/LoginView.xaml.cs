using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using Microsoft.EntityFrameworkCore;
using DriveNow.Data;
using DriveNow.Models;
using DriveNow.Security;

namespace DriveNow.Views
{
    public partial class LoginView : Window
    {
        // Sesión activa disponible desacoplada mediante UserSession
        public static Empleado? EmpleadoActual => UserSession.Current.EmpleadoActual;

        public static void CerrarSesion()
        {
            UserSession.Current.CerrarSesion();
        }

        private readonly Storyboard _spinnerStoryboard;

        public LoginView()
        {
            InitializeComponent();
            _spinnerStoryboard = (Storyboard)FindResource("SpinnerStoryboard");
        }

        private bool _iniciandoSesion = false;

        private async void btnIniciarSesion_Click(object sender, RoutedEventArgs e)
        {
            if (_iniciandoSesion) return; // evita doble submit
            _iniciandoSesion = true;

            string email = txtLoginEmail.Text.Trim();
            string pass = txtLoginPass.Password.Trim();

            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(pass))
            {
                MessageBox.Show("Complete su correo y contraseña.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
                _iniciandoSesion = false;
                return;
            }

            lblTextoBotonLogin.Visibility = Visibility.Collapsed;
            pnlCargando.Visibility = Visibility.Visible;
            _spinnerStoryboard.Begin();

            try
            {
                using var context = new DriveNowDbContext();

                // 1. Buscamos al empleado por correo incluyendo su Rol asociado
                var empleado = await context.Empleados
                    .Include(emp => emp.Rol)
                    .FirstOrDefaultAsync(emp => emp.Email == email);

                // 2. Si no existe o la contraseña no coincide con el hash
                if (empleado == null || !PasswordHelper.VerificarPassword(pass, empleado.PasswordHash))
                {
                    MessageBox.Show("Correo o contraseña incorrectos.", "Error de Autenticación", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // 2.1 Verificar si la cuenta del empleado está activa
                if (!empleado.Activo)
                {
                    MessageBox.Show("Esta cuenta se encuentra inactiva o dada de baja. Comuníquese con la administración.", "Acceso Bloqueado", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // 3. Autenticación exitosa: guardamos sesión en el servicio y abrimos ventana principal
                UserSession.Current.IniciarSesion(empleado);

                await Task.Delay(400);

                MainWindow main = new MainWindow();
                main.Show();
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al conectar con el servidor: {ex.Message}", "Error de Conexión", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                lblTextoBotonLogin.Visibility = Visibility.Visible;
                pnlCargando.Visibility = Visibility.Collapsed;
                _spinnerStoryboard.Stop();
                _iniciandoSesion = false;
            }
        }
    }
}