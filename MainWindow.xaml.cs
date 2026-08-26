using DriveNow.Views;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DriveNow
{
    public partial class MainWindow : Window
    {

        // Pinceles para los estados activo e inactivo
        private readonly SolidColorBrush brushActivoBg = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0EA5E9")); // Azul celeste

        private readonly SolidColorBrush brushInactivoBg = new SolidColorBrush(Colors.Transparent);
        
        public MainWindow()
        {
            InitializeComponent();
            
            // Cargamos el Dashboard al inicio y marcamos el boton 'dashboard' como activo
            ContenedorPrincipal.Content = new DashboardView();
            ActualizarBotonActivo(btnNavDashboard);
        }

        private void ActualizarBotonActivo(Button botonSeleccionado)
        {
            // Lista de todos los botones de navegacion del menu
            Button[] botones = { btnNavDashboard, btnNavFlota, btnNavClientes, btnNavReservas, btnNavAlquileres };

            foreach (Button btn in botones)
            {
                if (btn == botonSeleccionado)
                {
                    // Estado Activo / Seleccionado
                    btn.Background = brushActivoBg;
                    btn.FontWeight = FontWeights.SemiBold;
                }
                else
                {
                    // Estado Inactivo
                    btn.Background = brushInactivoBg;
                    btn.FontWeight = FontWeights.Medium;
                }
            }
        }

        // Cargamos la vista de cada seccion del menu
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

        private void btnCerrarSesion_Click(object sender, RoutedEventArgs e)
        {
            // no hace nada aun
            ActualizarBotonActivo(btnCerrarSesion);
        }

        private void btnAdminPerfil_Click(object sender, RoutedEventArgs e)
        {
            // no hace nada aun
            ActualizarBotonActivo(btnAdminPerfil);
        }
    }
}