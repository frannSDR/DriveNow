using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media.Imaging;
using DriveNow.Models;

namespace DriveNow.Views
{
    public partial class DetalleDanoWindow : Window
    {
        public DetalleDanoWindow(Alquiler alquiler)
        {
            InitializeComponent();
            CargarDatos(alquiler);
        }

        private void CargarDatos(Alquiler alquiler)
        {
            lblTituloContrato.Text = $"🔍 Inspección de Daño - Contrato {alquiler.CodigoAlquiler}";
            lblSubtituloVehiculo.Text = $"Vehículo: {alquiler.VehiculoTexto} | Cliente: {alquiler.ClienteTexto}";

            var dano = alquiler.Danos.FirstOrDefault();
            if (dano == null)
            {
                lblZona.Text = "No especificada";
                lblDescripcion.Text = "No se encontraron detalles de daños registrados para este contrato.";
                pnlSinImagen.Visibility = Visibility.Visible;
                lblMensajeSinImagen.Text = "Este contrato no posee daños reportados.";
                return;
            }

            lblZona.Text = string.IsNullOrWhiteSpace(dano.Zona) ? "General" : dano.Zona;
            lblDescripcion.Text = string.IsNullOrWhiteSpace(dano.Descripcion) ? "Sin descripción detallada." : dano.Descripcion;

            if (string.IsNullOrWhiteSpace(dano.EvidenciaFotografica))
            {
                pnlSinImagen.Visibility = Visibility.Visible;
                lblMensajeSinImagen.Text = "No se adjuntó fotografía en la inspección.";
                return;
            }

            try
            {
                if (File.Exists(dano.EvidenciaFotografica))
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(dano.EvidenciaFotografica, UriKind.Absolute);
                    bitmap.CacheOption = BitmapCacheOption.OnLoad; // Carga en memoria sin bloquear el archivo
                    bitmap.EndInit();

                    imgEvidencia.Source = bitmap;
                    pnlSinImagen.Visibility = Visibility.Collapsed;
                }
                else
                {
                    pnlSinImagen.Visibility = Visibility.Visible;
                    lblMensajeSinImagen.Text = $"El archivo de fotografía no fue encontrado en el disco:\n{dano.EvidenciaFotografica}";
                }
            }
            catch (Exception ex)
            {
                pnlSinImagen.Visibility = Visibility.Visible;
                lblMensajeSinImagen.Text = $"Error al cargar la imagen: {ex.Message}";
            }
        }

        private void btnCerrar_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
