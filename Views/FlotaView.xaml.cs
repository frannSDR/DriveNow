using DriveNow.Data;
using DriveNow.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace DriveNow.Views
{
    public partial class FlotaView : UserControl
    {
        private ObservableCollection<Vehiculo> _listaVehiculos = new ObservableCollection<Vehiculo>();
        private Vehiculo? _vehiculoEnEdicion = null;
        private bool _isFormatting = false;
        private string _filtroEstado = "Todos";
        private bool _ignorarCambioFiltros = false;
        private string _rutaFotoSeleccionada = string.Empty;

        public FlotaView()
        {
            InitializeComponent();
            Loaded += FlotaView_Loaded;
        }

        private async void FlotaView_Loaded(object sender, RoutedEventArgs e)
        {
            AplicarPermisosSegunRol();
            await CargarCatalogosAsync();
            await CargarFlotaDesdeBdAsync();
        }

        private void AplicarPermisosSegunRol()
        {
            if (LoginView.EmpleadoActual == null) return;

            var rol = (RolUsuario)LoginView.EmpleadoActual.IdRol;

            if (rol == RolUsuario.Dueno)
            {
                btnNuevoVehiculo.Visibility = Visibility.Collapsed;
            }
            else
            {
                btnNuevoVehiculo.Visibility = Visibility.Visible;
            }
        }

        private async Task CargarCatalogosAsync()
        {
            try
            {
                using var context = new DriveNowDbContext();

                var categorias = await context.Categorias.ToListAsync();
                cmbCategoria.ItemsSource = categorias;
                if (categorias.Count > 0) cmbCategoria.SelectedIndex = 0;

                var categoriasFiltro = new List<Categoria>
                {
                    new Categoria { IdCategoria = 0, Nombre = "Todas las categorías" }
                };
                categoriasFiltro.AddRange(categorias);
                cmbFiltroCategoria.ItemsSource = categoriasFiltro;
                cmbFiltroCategoria.SelectedIndex = 0;

                var estadosFormulario = new[] { "Disponible", "En Taller" };
                var estados = await context.Estados
                    .Where(e => estadosFormulario.Contains(e.Nombre))
                    .OrderBy(e => e.IdEstado)
                    .ToListAsync();

                cmbEstado.ItemsSource = estados;
                if (estados.Count > 0) cmbEstado.SelectedIndex = 0;

                var estadosFiltro = new List<string>
                {
                    "Todos los estados",
                    "Disponible",
                    "Alquilado",
                    "En Taller"
                };
                cmbFiltroEstado.ItemsSource = estadosFiltro;
                cmbFiltroEstado.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cargar catálogos: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task CargarFlotaDesdeBdAsync()
        {
            try
            {
                using var context = new DriveNowDbContext();
                var vehiculos = await context.Vehiculos
                    .Include(v => v.Categoria)
                    .Include(v => v.Estado)
                    .Where(v => v.Activo)
                    .OrderByDescending(v => v.IdVehiculo)
                    .ToListAsync();

                _listaVehiculos = new ObservableCollection<Vehiculo>(vehiculos);
                ActualizarMetricas();
                AplicarFiltrosFlota();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cargar la flota: {ex.Message}", "Error de Conexión", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ActualizarMetricas()
        {
            lblTotalFlota.Text = _listaVehiculos.Count.ToString();
            lblDisponibles.Text = _listaVehiculos.Count(v => v.Estado?.Nombre == "Disponible").ToString();
            lblAlquilados.Text = _listaVehiculos.Count(v => v.Estado?.Nombre == "Alquilado").ToString();
            lblReservados.Text = _listaVehiculos.Count(v => v.Estado?.Nombre == "Reservado").ToString();
            lblMantenimiento.Text = _listaVehiculos.Count(v => v.Estado?.Nombre == "En Taller" || v.Estado?.Nombre == "Mantenimiento").ToString();
        }

        private void btnNuevoVehiculo_Click(object sender, RoutedEventArgs e)
        {
            ResetearFormularioFlota();
            modalOverlay.Visibility = Visibility.Visible;
        }

        private void btnCerrarModal_Click(object sender, RoutedEventArgs e)
        {
            ResetearFormularioFlota();
            modalOverlay.Visibility = Visibility.Collapsed;
        }

        private void btnAdjuntarFotoVehiculo_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dlg = new OpenFileDialog
            {
                Filter = "Imágenes (*.jpg;*.jpeg;*.png)|*.jpg;*.jpeg;*.png"
            };

            if (dlg.ShowDialog() == true)
            {
                _rutaFotoSeleccionada = dlg.FileName;
                lblFotoStatus.Text = Path.GetFileName(_rutaFotoSeleccionada);
            }
        }

        private string CopiarImagenACarpetaLocal(string rutaOrigen, string patente)
        {
            if (string.IsNullOrEmpty(rutaOrigen) || !File.Exists(rutaOrigen))
                return string.Empty;

            try
            {
                string carpetaDestino = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Vehiculos");
                if (!Directory.Exists(carpetaDestino))
                {
                    Directory.CreateDirectory(carpetaDestino);
                }

                string ext = Path.GetExtension(rutaOrigen);
                string nombreArchivo = $"{patente.Replace(" ", "")}_{DateTime.Now.Ticks}{ext}";
                string rutaFinal = Path.Combine(carpetaDestino, nombreArchivo);

                File.Copy(rutaOrigen, rutaFinal, true);
                return rutaFinal;
            }
            catch
            {
                return rutaOrigen;
            }
        }

        private string ObtenerFotoPorDefectoSegunModeloYCategoria(string modelo, string categoria)
        {
            try
            {
                string carpetaModelos = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Modelos");

                if (!Directory.Exists(carpetaModelos))
                {
                    Directory.CreateDirectory(carpetaModelos);
                    return string.Empty;
                }

                string modeloLimpio = modelo.ToLower().Trim();
                string categoriaLimpia = categoria.ToLower().Trim();

                var archivos = Directory.GetFiles(carpetaModelos, "*.*")
                    .Where(f => f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                                f.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                                f.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase));

                if (!string.IsNullOrEmpty(modeloLimpio))
                {
                    var fotoModelo = archivos.FirstOrDefault(f => Path.GetFileNameWithoutExtension(f).ToLower().Contains(modeloLimpio));
                    if (!string.IsNullOrEmpty(fotoModelo)) return fotoModelo;
                }

                if (!string.IsNullOrEmpty(categoriaLimpia))
                {
                    var fotoCategoria = archivos.FirstOrDefault(f => Path.GetFileNameWithoutExtension(f).ToLower().Contains(categoriaLimpia));
                    if (!string.IsNullOrEmpty(fotoCategoria)) return fotoCategoria;
                }
            }
            catch
            {
                // Ignorar error si no encuentra la carpeta
            }

            return string.Empty;
        }

        private async void btnGuardarVehiculo_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtPatente.Text) ||
                string.IsNullOrWhiteSpace(txtMarca.Text) ||
                string.IsNullOrWhiteSpace(txtModelo.Text) ||
                string.IsNullOrWhiteSpace(txtAnio.Text) ||
                cmbCategoria.SelectedValue == null ||
                cmbEstado.SelectedValue == null)
            {
                MessageBox.Show("Por favor complete todos los campos obligatorios.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!int.TryParse(txtAnio.Text.Trim(), out int anio) || anio < 1990 || anio > DateTime.Now.Year + 1)
            {
                MessageBox.Show("Ingrese un año válido.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            string patente = txtPatente.Text.Trim().ToUpper();
            var regexMercosur = new Regex(@"^[A-Z]{2} [0-9]{3} [A-Z]{2}$");

            if (!regexMercosur.IsMatch(patente))
            {
                MessageBox.Show("El formato de patente debe ser válido (ej. AA 123 AA).", "Patente Inválida", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            int idCat = (int)cmbCategoria.SelectedValue;
            int idEst = (int)cmbEstado.SelectedValue;

            try
            {
                using var context = new DriveNowDbContext();

                bool existePatente = await context.Vehiculos.AnyAsync(v =>
                    v.Patente == patente &&
                    v.Activo &&
                    (_vehiculoEnEdicion == null || v.IdVehiculo != _vehiculoEnEdicion.IdVehiculo));

                if (existePatente)
                {
                    MessageBox.Show("Ya existe un vehículo activo registrado con esa patente.", "Patente Duplicada", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                string rutaGuardada = string.Empty;

                if (!string.IsNullOrEmpty(_rutaFotoSeleccionada))
                {
                    rutaGuardada = CopiarImagenACarpetaLocal(_rutaFotoSeleccionada, patente);
                }
                else
                {
                    string nombreCategoria = (cmbCategoria.SelectedItem as Categoria)?.Nombre ?? string.Empty;
                    rutaGuardada = ObtenerFotoPorDefectoSegunModeloYCategoria(txtModelo.Text.Trim(), nombreCategoria);

                    if (string.IsNullOrEmpty(rutaGuardada) && _vehiculoEnEdicion != null)
                    {
                        rutaGuardada = _vehiculoEnEdicion.ImagenUrl ?? string.Empty;
                    }
                }

                if (_vehiculoEnEdicion == null)
                {
                    var nuevo = new Vehiculo
                    {
                        Patente = patente,
                        Marca = txtMarca.Text.Trim(),
                        Modelo = txtModelo.Text.Trim(),
                        Anio = anio,
                        IdCategoria = idCat,
                        IdEstado = idEst,
                        ImagenUrl = rutaGuardada,
                        Activo = true
                    };

                    context.Vehiculos.Add(nuevo);
                    await context.SaveChangesAsync();
                    MessageBox.Show("Vehículo registrado exitosamente.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    var vehiculoDb = await context.Vehiculos
                        .Include(v => v.Estado)
                        .FirstOrDefaultAsync(v => v.IdVehiculo == _vehiculoEnEdicion.IdVehiculo);

                    if (vehiculoDb != null)
                    {
                        if (vehiculoDb.Estado?.Nombre == "Alquilado")
                        {
                            MessageBox.Show("No se pueden guardar los cambios porque el vehículo se encuentra alquilado.", "Acción Rechazada", MessageBoxButton.OK, MessageBoxImage.Warning);
                            ResetearFormularioFlota();
                            modalOverlay.Visibility = Visibility.Collapsed;
                            await CargarFlotaDesdeBdAsync();
                            return;
                        }

                        vehiculoDb.Patente = patente;
                        vehiculoDb.Marca = txtMarca.Text.Trim();
                        vehiculoDb.Modelo = txtModelo.Text.Trim();
                        vehiculoDb.Anio = anio;
                        vehiculoDb.IdCategoria = idCat;
                        vehiculoDb.IdEstado = idEst;
                        if (!string.IsNullOrEmpty(rutaGuardada)) vehiculoDb.ImagenUrl = rutaGuardada;

                        await context.SaveChangesAsync();
                        MessageBox.Show("Vehículo actualizado exitosamente.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }

                modalOverlay.Visibility = Visibility.Collapsed;
                await CargarFlotaDesdeBdAsync();
                ResetearFormularioFlota();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al guardar en el servidor: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnEditarVehiculo_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is Vehiculo vehiculo)
            {
                if (vehiculo.Estado?.Nombre == "Alquilado")
                {
                    MessageBox.Show(
                        $"El vehículo {vehiculo.MarcaModelo} ({vehiculo.Patente}) se encuentra actualmente ALQUILADO.\n" +
                        "No es posible modificar sus datos mientras esté en posesión de un cliente.",
                        "Operación Bloqueada",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                _vehiculoEnEdicion = vehiculo;

                txtPatente.Text = vehiculo.Patente;
                txtMarca.Text = vehiculo.Marca;
                txtModelo.Text = vehiculo.Modelo;
                txtAnio.Text = vehiculo.Anio.ToString();
                cmbCategoria.SelectedValue = vehiculo.IdCategoria;
                cmbEstado.SelectedValue = vehiculo.IdEstado;
                lblFotoStatus.Text = !string.IsNullOrEmpty(vehiculo.ImagenUrl) ? Path.GetFileName(vehiculo.ImagenUrl) : "Sin foto";

                lblTituloFormularioFlota.Text = $"✏️ Editando Vehículo: {vehiculo.MarcaModelo} ({vehiculo.Patente})";
                btnGuardarVehiculo.Content = "Actualizar Cambios";
                btnGuardarVehiculo.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981"));

                modalOverlay.Visibility = Visibility.Visible;
            }
        }

        private async void btnEliminarFila_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is Vehiculo vehiculoSeleccionado)
            {
                if (vehiculoSeleccionado.Estado?.Nombre == "Alquilado" || vehiculoSeleccionado.Estado?.Nombre == "Reservado")
                {
                    MessageBox.Show($"No se puede dar de baja el vehículo porque se encuentra en estado '{vehiculoSeleccionado.Estado.Nombre}'.", "Acción Bloqueada", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var res = MessageBox.Show($"¿Seguro que desea dar de baja el vehículo {vehiculoSeleccionado.MarcaModelo} ({vehiculoSeleccionado.Patente})?", "Confirmar Baja", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (res != MessageBoxResult.Yes) return;

                try
                {
                    using var context = new DriveNowDbContext();

                    var itemADesactivar = await context.Vehiculos.FindAsync(vehiculoSeleccionado.IdVehiculo);
                    if (itemADesactivar != null)
                    {
                        itemADesactivar.Activo = false;
                        await context.SaveChangesAsync();

                        _listaVehiculos.Remove(vehiculoSeleccionado);

                        if (_vehiculoEnEdicion?.IdVehiculo == vehiculoSeleccionado.IdVehiculo)
                        {
                            ResetearFormularioFlota();
                            modalOverlay.Visibility = Visibility.Collapsed;
                        }

                        ActualizarMetricas();
                        AplicarFiltrosFlota();
                        MessageBox.Show("Vehículo dado de baja exitosamente del inventario activo.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error al procesar la baja: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void txtBuscar_TextChanged(object sender, TextChangedEventArgs e) => AplicarFiltrosFlota();

        private void FiltroCombinado_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_ignorarCambioFiltros || !IsLoaded) return;

            if (sender == cmbFiltroEstado && cmbFiltroEstado.SelectedItem is string estadoSeleccionado)
            {
                _filtroEstado = estadoSeleccionado == "Todos los estados" ? "Todos" : estadoSeleccionado;
            }

            AplicarFiltrosFlota();
        }

        private void CardFiltro_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.Tag is string estadoTag)
            {
                _filtroEstado = (_filtroEstado == estadoTag && estadoTag != "Todos") ? "Todos" : estadoTag;

                _ignorarCambioFiltros = true;
                if (_filtroEstado == "Todos")
                {
                    cmbFiltroEstado.SelectedIndex = 0;
                }
                else if (_filtroEstado == "Mantenimiento" || _filtroEstado == "En Taller")
                {
                    cmbFiltroEstado.SelectedItem = "En Taller";
                }
                else
                {
                    cmbFiltroEstado.SelectedItem = _filtroEstado;
                }
                _ignorarCambioFiltros = false;

                AplicarFiltrosFlota();
            }
        }

        private void btnLimpiarFiltrosFlota_Click(object sender, RoutedEventArgs e)
        {
            _ignorarCambioFiltros = true;
            txtBuscar.Clear();
            if (cmbFiltroCategoria.Items.Count > 0) cmbFiltroCategoria.SelectedIndex = 0;
            if (cmbFiltroEstado.Items.Count > 0) cmbFiltroEstado.SelectedIndex = 0;
            _filtroEstado = "Todos";
            _ignorarCambioFiltros = false;

            AplicarFiltrosFlota();
        }

        private void AplicarFiltrosFlota()
        {
            if (_ignorarCambioFiltros) return;

            IEnumerable<Vehiculo> filtrados = _listaVehiculos;

            if (!string.IsNullOrEmpty(_filtroEstado) && _filtroEstado != "Todos")
            {
                if (_filtroEstado == "Mantenimiento" || _filtroEstado == "En Taller")
                {
                    filtrados = filtrados.Where(v => v.Estado?.Nombre == "En Taller" || v.Estado?.Nombre == "Mantenimiento");
                }
                else
                {
                    filtrados = filtrados.Where(v => v.Estado?.Nombre == _filtroEstado);
                }
            }

            if (cmbFiltroCategoria != null && cmbFiltroCategoria.SelectedValue is int idCat && idCat > 0)
            {
                filtrados = filtrados.Where(v => v.IdCategoria == idCat);
            }

            string texto = txtBuscar?.Text.Trim().ToLower() ?? string.Empty;
            if (!string.IsNullOrEmpty(texto))
            {
                filtrados = filtrados.Where(v =>
                    v.Patente.ToLower().Contains(texto) ||
                    v.Marca.ToLower().Contains(texto) ||
                    v.Modelo.ToLower().Contains(texto));
            }

            var lista = filtrados.ToList();
            icFlotaTarjetas.ItemsSource = lista;
            if (lblContadorFiltrados != null)
            {
                lblContadorFiltrados.Text = $"Mostrando: {lista.Count} de {_listaVehiculos.Count} vehículos";
            }
            ResaltarTarjetaSeleccionada(_filtroEstado);
        }

        private void ResaltarTarjetaSeleccionada(string estado)
        {
            if (cardFiltroTodos == null) return;

            var brushTransparente = Brushes.Transparent;
            var brushBgNormal = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E293B"));

            cardFiltroTodos.BorderBrush = brushTransparente;
            cardFiltroTodos.Background = brushBgNormal;
            cardFiltroDisponibles.BorderBrush = brushTransparente;
            cardFiltroDisponibles.Background = brushBgNormal;
            cardFiltroAlquilados.BorderBrush = brushTransparente;
            cardFiltroAlquilados.Background = brushBgNormal;
            cardFiltroReservados.BorderBrush = brushTransparente;
            cardFiltroReservados.Background = brushBgNormal;
            cardFiltroMantenimiento.BorderBrush = brushTransparente;
            cardFiltroMantenimiento.Background = brushBgNormal;

            switch (estado)
            {
                case "Disponible":
                    cardFiltroDisponibles.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4ADE80"));
                    cardFiltroDisponibles.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1A2E35"));
                    break;
                case "Alquilado":
                    cardFiltroAlquilados.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FBBF24"));
                    cardFiltroAlquilados.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#332E22"));
                    break;
                case "Reservado":
                    cardFiltroReservados.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#60A5FA"));
                    cardFiltroReservados.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E2E4A"));
                    break;
                case "Mantenimiento":
                    cardFiltroMantenimiento.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F87171"));
                    cardFiltroMantenimiento.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#352227"));
                    break;
                default:
                    cardFiltroTodos.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#38BDF8"));
                    cardFiltroTodos.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1B2E42"));
                    break;
            }
        }

        private void txtPatente_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isFormatting) return;

            _isFormatting = true;

            string raw = Regex.Replace(txtPatente.Text.ToUpper(), @"[^A-Z0-9]", "");
            if (raw.Length > 7) raw = raw.Substring(0, 7);

            string formatted = raw;
            if (raw.Length > 5)
            {
                formatted = $"{raw.Substring(0, 2)} {raw.Substring(2, 3)} {raw.Substring(5)}";
            }
            else if (raw.Length > 2)
            {
                formatted = $"{raw.Substring(0, 2)} {raw.Substring(2)}";
            }

            txtPatente.Text = formatted;
            txtPatente.CaretIndex = txtPatente.Text.Length;

            _isFormatting = false;
        }

        private void ResetearFormularioFlota()
        {
            _vehiculoEnEdicion = null;
            _rutaFotoSeleccionada = string.Empty;

            txtPatente.Clear();
            txtMarca.Clear();
            txtModelo.Clear();
            txtAnio.Clear();
            lblFotoStatus.Text = "Sin foto";
            if (cmbCategoria.Items.Count > 0) cmbCategoria.SelectedIndex = 0;
            if (cmbEstado.Items.Count > 0) cmbEstado.SelectedIndex = 0;

            lblTituloFormularioFlota.Text = "➕ Alta de vehículo";
            btnGuardarVehiculo.Content = "Guardar Vehículo";
            btnGuardarVehiculo.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2563EB"));
        }
    }
}