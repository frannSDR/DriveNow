using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.IO;
using System.Linq;

namespace DriveNow.Models
{
    [Table("Vehiculos")]
    public class Vehiculo
    {
        [Key]
        public int IdVehiculo { get; set; }

        public string Marca { get; set; } = string.Empty;
        public string Modelo { get; set; } = string.Empty;
        public int Anio { get; set; }
        public string Patente { get; set; } = string.Empty;

        public int IdEstado { get; set; }
        public int IdCategoria { get; set; }

        public bool Activo { get; set; } = true; // Borrado lógico

        [ForeignKey("IdEstado")]
        public virtual Estado? Estado { get; set; }

        [ForeignKey("IdCategoria")]
        public virtual Categoria? Categoria { get; set; }

        public virtual ICollection<FotoVehiculo> Fotos { get; set; } = new List<FotoVehiculo>();
        public virtual ICollection<Reserva> Reservas { get; set; } = new List<Reserva>();

        [NotMapped]
        public string DescripcionCompleta => $"{Marca} {Modelo} ({Anio}) - {Patente}";

        [NotMapped]
        public string? FotoPortada => Fotos?.FirstOrDefault(f => f.EsPrincipal)?.RutaFoto ?? Fotos?.FirstOrDefault()?.RutaFoto;

        public string? ImagenUrl { get; set; }

        [NotMapped]
        public string ImagenMostrar
        {
            get
            {
                // 1. Si la ruta guardada existe tal cual en disco
                if (!string.IsNullOrEmpty(ImagenUrl) && File.Exists(ImagenUrl))
                {
                    return ImagenUrl;
                }

                // 2. Si no existe la ruta absoluta completa, buscar por nombre de archivo en carpetas locales de Assets
                if (!string.IsNullOrEmpty(ImagenUrl))
                {
                    string nombreArchivo = Path.GetFileName(ImagenUrl);
                    string localVehiculos = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Vehiculos", nombreArchivo);
                    if (File.Exists(localVehiculos)) return localVehiculos;

                    string localModelos = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Modelos", nombreArchivo);
                    if (File.Exists(localModelos)) return localModelos;
                }

                // 3. Auto-detección por Modelo o Marca en Assets/Modelos (ej. corolla, 208, 86)
                try
                {
                    var carpetasBusqueda = new List<string>
                    {
                        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Modelos"),
                        Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "Assets", "Modelos")),
                        Path.Combine(Directory.GetCurrentDirectory(), "Assets", "Modelos")
                    };

                    string? carpeta = carpetasBusqueda.FirstOrDefault(d => Directory.Exists(d) && Directory.GetFiles(d).Length > 0);
                    if (!string.IsNullOrEmpty(carpeta))
                    {
                        string modeloBuscado = Modelo?.ToLower().Trim() ?? "";
                        string marcaBuscada = Marca?.ToLower().Trim() ?? "";
                        string textoCompleto = $"{marcaBuscada} {modeloBuscado}".Trim();

                        var extensionesValidas = new[] { ".jpg", ".jpeg", ".png", ".webp", ".bmp" };
                        var archivos = Directory.GetFiles(carpeta, "*.*")
                            .Where(f => extensionesValidas.Contains(Path.GetExtension(f).ToLower()))
                            .ToList();

                        if (!string.IsNullOrEmpty(modeloBuscado))
                        {
                            var foto = archivos.FirstOrDefault(f =>
                            {
                                string nombreSinExt = Path.GetFileNameWithoutExtension(f).ToLower().Trim();
                                return modeloBuscado.Contains(nombreSinExt) ||
                                       nombreSinExt.Contains(modeloBuscado) ||
                                       textoCompleto.Contains(nombreSinExt);
                            });

                            if (!string.IsNullOrEmpty(foto)) return foto;
                        }
                    }
                }
                catch { }

                return "pack://application:,,,/Resources/car_placeholder.png";
            }
        }

        [NotMapped]
        public string MarcaModelo => $"{Marca} {Modelo}";
    }
}
