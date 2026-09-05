using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DriveNow.Models
{
    [Table("FotosVehiculo")]
    public class FotoVehiculo
    {
        [Key]
        public int IdFoto { get; set; }

        public int IdVehiculo { get; set; }
        public string RutaFoto { get; set; } = string.Empty;
        public bool EsPrincipal { get; set; }
        public DateTime FechaSubida { get; set; } = DateTime.Now;

        [ForeignKey("IdVehiculo")]
        public virtual Vehiculo? Vehiculo { get; set; }
    }
}