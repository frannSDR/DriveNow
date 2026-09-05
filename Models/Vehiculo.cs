using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
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

        [NotMapped]
        public string MarcaModelo => $"{Marca} {Modelo}";
    }
}