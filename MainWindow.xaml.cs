using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DriveNow.Models;
using DriveNow.Views;

namespace DriveNow
{
    public partial class MainWindow : Window
    {
        
        public MainWindow()
        {
            InitializeComponent();
            ConfigurarPermisosSegunRol();
            ContenedorPrincipal.Content = new DashboardView();
            ActualizarBotonActivo(btnNavDashboard);
        }

        private void ConfigurarPermisosSegunRol()
        {
            var empleado = LoginView.EmpleadoActual;
            if (empleado == null) return;

            var rol = (RolUsuario)empleado.IdRol;

            switch (rol)
            {
                case RolUsuario.Operador:
                    btnNavDashboard.Visibility = Visibility.Visible;
                    btnNavClientes.Visibility = Visibility.Visible;
                    btnNavReservas.Visibility = Visibility.Visible;
                    btnNavAlquileres.Visibility = Visibility.Visible;
                    btnNavFlota.Visibility = Visibility.Visible;
                    btnNavReportes.Visibility = Visibility.Collapsed;
                    btnNavUsuarios.Visibility = Visibility.Collapsed;
                    btnNavCategorias.Visibility = Visibility.Collapsed;
                    break;

                case RolUsuario.Administrador:
                    btnNavDashboard.Visibility = Visibility.Visible;
                    btnNavClientes.Visibility = Visibility.Visible;
                    btnNavReservas.Visibility = Visibility.Visible;
                    btnNavAlquileres.Visibility = Visibility.Visible;
                    btnNavFlota.Visibility = Visibility.Visible;
                    btnNavReportes.Visibility = Visibility.Collapsed;
                    btnNavUsuarios.Visibility = Visibility.Collapsed;
                    btnNavCategorias.Visibility = Visibility.Visible;
                    break;

                case RolUsuario.Dueno:
                    btnNavDashboard.Visibility = Visibility.Visible;
                    btnNavFlota.Visibility = Visibility.Collapsed;
                    btnNavReportes.Visibility = Visibility.Visible;
                    btnNavClientes.Visibility = Visibility.Collapsed;
                    btnNavReservas.Visibility = Visibility.Collapsed;
                    btnNavAlquileres.Visibility = Visibility.Collapsed;
                    btnNavUsuarios.Visibility = Visibility.Collapsed;
                    btnNavCategorias.Visibility = Visibility.Collapsed;
                    break;

                case RolUsuario.SuperAdmin:
                    btnNavDashboard.Visibility = Visibility.Visible;
                    btnNavFlota.Visibility = Visibility.Visible;
                    btnNavClientes.Visibility = Visibility.Visible;
                    btnNavReservas.Visibility = Visibility.Visible;
                    btnNavAlquileres.Visibility = Visibility.Visible;
                    btnNavReportes.Visibility = Visibility.Visible;
                    btnNavUsuarios.Visibility = Visibility.Visible;
                    btnNavCategorias.Visibility = Visibility.Visible;
                    break;
            }
        }

        private void ActualizarBotonActivo(Button botonSeleccionado)
        {
            Button[] botones = {
                btnNavDashboard, btnNavFlota, btnNavClientes,
                btnNavReservas, btnNavAlquileres, btnNavReportes,
                btnNavUsuarios, btnNavCategorias, btnNavCuenta
            };

            foreach (Button btn in botones)
            {
                // Al usar la propiedad Tag, el Trigger de XAML aplica la selección limpia
                btn.Tag = (btn == botonSeleccionado) ? "Activo" : null;
            }
        }

        private void btnNavDashboard_Click(object sender, RoutedEventArgs e)
        {
            ContenedorPrincipal.Content = new DashboardView();
            ActualizarBotonActivo(btnNavDashboard);
        }

        private void btnNavFlota_Click(object sender, RoutedEventArgs e)
        {
            ContenedorPrincipal.Content = new FlotaView();
            ActualizarBotonActivo(btnNavFlota);
        }

        private void btnNavClientes_Click(object sender, RoutedEventArgs e)
        {
            ContenedorPrincipal.Content = new ClientesView();
            ActualizarBotonActivo(btnNavClientes);
        }

        private void btnNavReservas_Click(object sender, RoutedEventArgs e)
        {
            ContenedorPrincipal.Content = new ReservasView();
            ActualizarBotonActivo(btnNavReservas);
        }

        private void btnNavAlquileres_Click(object sender, RoutedEventArgs e)
        {
            ContenedorPrincipal.Content = new AlquileresView();
            ActualizarBotonActivo(btnNavAlquileres);
        }

        private void btnNavReportes_Click(object sender, RoutedEventArgs e)
        {
            ContenedorPrincipal.Content = new ReportesView();
            ActualizarBotonActivo(btnNavReportes);
        }

        private void btnNavUsuarios_Click(object sender, RoutedEventArgs e)
        {
            ContenedorPrincipal.Content = new UsuariosView();
            ActualizarBotonActivo(btnNavUsuarios);
        }

        private void btnNavCuenta_Click(object sender, RoutedEventArgs e)
        {
            ContenedorPrincipal.Content = new MiCuentaView(); 
            ActualizarBotonActivo(btnNavCuenta);
        }

        private void btnCerrarSesion_Click(object sender, RoutedEventArgs e)
        {
            var res = MessageBox.Show("¿Desea cerrar la sesión actual?", "Cerrar Sesión", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (res == MessageBoxResult.Yes)
            {
                LoginView login = new LoginView();
                login.Show();
                this.Close();
            }
        }

        private void btnNavCategorias_Click(object sender, RoutedEventArgs e)
        {
            ContenedorPrincipal.Content = new CategoriasView();
            ActualizarBotonActivo(btnNavCategorias);
        }
    }
}