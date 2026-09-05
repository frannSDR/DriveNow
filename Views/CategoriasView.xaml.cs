using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.EntityFrameworkCore;
using DriveNow.Data;
using DriveNow.Models;

namespace DriveNow.Views
{
    public partial class CategoriasView : UserControl
    {
        private ObservableCollection<Categoria> _listaCategorias = new ObservableCollection<Categoria>();
        private Categoria? _categoriaEnEdicion = null;

        public CategoriasView()
        {
            InitializeComponent();
            Loaded += async (s, e) =>
            {
                AplicarPermisosSegunRol();
                await CargarCategoriasDesdeBdAsync();
            };
        }

        private void AplicarPermisosSegunRol()
        {
            if (LoginView.EmpleadoActual == null) return;

            var rol = (RolUsuario)LoginView.EmpleadoActual.IdRol;

            // Regla de Negocio: Operadores y Dueños no pueden alterar tarifas ni crear categorías
            if (rol == RolUsuario.Operador || rol == RolUsuario.Dueno)
            {
                pnlFormularioCategorias.Visibility = Visibility.Collapsed;
                colAccionesCategorias.Visibility = Visibility.Collapsed;
            }
            else
            {
                pnlFormularioCategorias.Visibility = Visibility.Visible;
                colAccionesCategorias.Visibility = Visibility.Visible;
            }
        }

        private async Task CargarCategoriasDesdeBdAsync()
        {
            try
            {
                using var context = new DriveNowDbContext();
                var categorias = await context.Categorias
                    .OrderBy(c => c.TarifaDiaria)
                    .ToListAsync();

                _listaCategorias = new ObservableCollection<Categoria>(categorias);
                dgvCategorias.ItemsSource = _listaCategorias;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cargar el tarifario: {ex.Message}", "Error de Conexión", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnEditarCategoria_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is Categoria cat)
            {
                _categoriaEnEdicion = cat;

                txtNombre.Text = cat.Nombre;
                txtTarifaDiaria.Text = cat.TarifaDiaria.ToString("0.00", CultureInfo.InvariantCulture);
                txtDescripcion.Text = cat.Descripcion ?? string.Empty;

                lblTituloFormularioCat.Text = $"✏️ Editando Tarifa: {cat.Nombre}";
                btnGuardarCategoria.Content = "Actualizar Tarifa";
                btnGuardarCategoria.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981"));
                btnCancelarEdicion.Visibility = Visibility.Visible;
                btnLimpiarCat.Visibility = Visibility.Collapsed;
            }
        }

        private async void btnGuardarCategoria_Click(object sender, RoutedEventArgs e)
        {
            string nombre = txtNombre.Text.Trim();
            string tarifaStr = txtTarifaDiaria.Text.Trim().Replace(',', '.');
            string descripcion = txtDescripcion.Text.Trim();

            if (string.IsNullOrWhiteSpace(nombre) || string.IsNullOrWhiteSpace(tarifaStr))
            {
                MessageBox.Show("Complete el nombre y el valor de la tarifa diaria.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!decimal.TryParse(tarifaStr, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal tarifa) || tarifa <= 0)
            {
                MessageBox.Show("Ingrese un monto de tarifa válido y mayor a cero.", "Importe Inválido", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                using var context = new DriveNowDbContext();

                // Validar unicidad del nombre
                bool existeNombre = await context.Categorias.AnyAsync(c =>
                    c.Nombre == nombre &&
                    (_categoriaEnEdicion == null || c.IdCategoria != _categoriaEnEdicion.IdCategoria));

                if (existeNombre)
                {
                    MessageBox.Show("Ya existe una categoría registrada con ese nombre.", "Categoría Duplicada", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (_categoriaEnEdicion == null)
                {
                    // Alta nueva
                    var nuevaCat = new Categoria
                    {
                        Nombre = nombre,
                        TarifaDiaria = tarifa,
                        Descripcion = string.IsNullOrWhiteSpace(descripcion) ? null : descripcion
                    };

                    context.Categorias.Add(nuevaCat);
                    await context.SaveChangesAsync();
                    MessageBox.Show("Categoría agregada al tarifario.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    // Actualización
                    var catDb = await context.Categorias.FindAsync(_categoriaEnEdicion.IdCategoria);
                    if (catDb != null)
                    {
                        catDb.Nombre = nombre;
                        catDb.TarifaDiaria = tarifa;
                        catDb.Descripcion = string.IsNullOrWhiteSpace(descripcion) ? null : descripcion;

                        await context.SaveChangesAsync();
                        MessageBox.Show("Tarifa y detalles actualizados exitosamente.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }

                await CargarCategoriasDesdeBdAsync();
                ResetearFormulario();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al guardar en el servidor: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnCancelarEdicion_Click(object sender, RoutedEventArgs e) => ResetearFormulario();

        private void btnLimpiarCat_Click(object sender, RoutedEventArgs e) => ResetearFormulario();

        private void ResetearFormulario()
        {
            _categoriaEnEdicion = null;

            txtNombre.Clear();
            txtTarifaDiaria.Clear();
            txtDescripcion.Clear();

            lblTituloFormularioCat.Text = "➕ Nueva Categoría";
            btnGuardarCategoria.Content = "Guardar Categoría";
            btnGuardarCategoria.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2563EB"));
            btnCancelarEdicion.Visibility = Visibility.Collapsed;
            btnLimpiarCat.Visibility = Visibility.Visible;
        }
    }
}