using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DriveNow.Models
{
    [Table("Estados")]
    public class Estado
    {
        [Key]
        public int IdEstado { get; set; }

        public string Nombre { get; set; } = string.Empty;
        public string? Descripcion { get; set; }

        public virtual ICollection<Vehiculo> Vehiculos { get; set; } = new List<Vehiculo>();
        public virtual ICollection<Reserva> Reservas { get; set; } = new List<Reserva>();
    }
}