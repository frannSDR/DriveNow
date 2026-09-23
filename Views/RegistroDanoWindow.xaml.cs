using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace DriveNow.Views
{
    public class RegistroDanoResultado
    {
        public string Sector { get; set; } = string.Empty;
        public string Gravedad { get; set; } = string.Empty;
        public string Descripcion { get; set; } = string.Empty;
        public string? RutaFoto { get; set; }
        public bool InmovilizarVehiculo { get; set; }

        public string ResumenTexto => $"{Sector} ({Gravedad}) - {(InmovilizarVehiculo ? "Deriva a Taller" : "Permanece Disponible")}";
    }

    public partial class RegistroDanoWindow : Window
    {
        public RegistroDanoResultado? Resultado { get; private set; }
        private string? _rutaFotoSeleccionada = null;

        public RegistroDanoWindow(string infoVehiculo, RegistroDanoResultado? previo = null)
        {
            InitializeComponent();
            lblSubtituloVehiculo.Text = $"Vehículo inspeccionado: {infoVehiculo}";

            CargarSectoresYGravedad();

            if (previo != null)
            {
                cmbSector.SelectedItem = previo.Sector;
                cmbGravedad.SelectedItem = previo.Gravedad;
                txtDescripcion.Text = previo.Descripcion;
                chkInmovilizar.IsChecked = previo.InmovilizarVehiculo;
                if (!string.IsNullOrEmpty(previo.RutaFoto) && File.Exists(previo.RutaFoto))
                {
                    CargarPreviaFoto(previo.RutaFoto);
                }
            }
        }

        private void CargarSectoresYGravedad()
        {
            var sectores = new List<string>
            {
                "Paragolpes Delantero",
                "Paragolpes Trasero",
                "Óptica / Faro Delantero",
                "Óptica / Faro Trasero",
                "Puerta Conductor",
                "Puerta Acompañante",
                "Capot / Techo",
                "Baúl / Portón Trasero",
                "Parabrisas / Luneta / Cristales",
                "Neumáticos / Llantas",
                "Interior / Tapizado",
                "Mecánica / Motor",
                "Carrocería General / Otro"
            };
            cmbSector.ItemsSource = sectores;
            cmbSector.SelectedIndex = 0;

            var gravedades = new List<string> { "Leve", "Moderado", "Grave" };
            cmbGravedad.ItemsSource = gravedades;
            cmbGravedad.SelectedIndex = 1; // Por defecto: Moderado
        }

        private void cmbGravedad_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cmbGravedad.SelectedItem is string gravedad)
            {
                // Si es leve, sugerimos desmarcar inmovilización; si es moderado o grave, se marca
                chkInmovilizar.IsChecked = gravedad != "Leve";
            }
        }

        private void btnSeleccionarFoto_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Title = "Seleccionar Evidencia Fotográfica",
                Filter = "Imágenes (*.jpg;*.jpeg;*.png)|*.jpg;*.jpeg;*.png"
            };

            if (dlg.ShowDialog() == true)
            {
                CargarPreviaFoto(dlg.FileName);
            }
        }

        private void CargarPreviaFoto(string ruta)
        {
            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(ruta, UriKind.Absolute);
                bitmap.CacheOption = BitmapCacheOption.OnLoad; // No bloquea el archivo en disco
                bitmap.EndInit();

                imgPrevia.Source = bitmap;
                pnlVistaPrevia.Visibility = Visibility.Visible;
                _rutaFotoSeleccionada = ruta;
                lblFotoStatus.Text = $"Archivo: {Path.GetFileName(ruta)}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"No se pudo previsualizar la imagen: {ex.Message}", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void btnConfirmar_Click(object sender, RoutedEventArgs e)
        {
            string sector = cmbSector.SelectedItem as string ?? "Carrocería General";
            string gravedad = cmbGravedad.SelectedItem as string ?? "Moderado";
            string descripcion = txtDescripcion.Text.Trim();

            if (string.IsNullOrWhiteSpace(descripcion))
            {
                MessageBox.Show("Por favor redacte una breve descripción del daño detectado.", "Campo Requerido", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            Resultado = new RegistroDanoResultado
            {
                Sector = sector,
                Gravedad = gravedad,
                Descripcion = descripcion,
                RutaFoto = _rutaFotoSeleccionada,
                InmovilizarVehiculo = chkInmovilizar.IsChecked == true
            };

            DialogResult = true;
            Close();
        }

        private void btnCancelar_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
