using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DriveNow.Models
{
    [Table("Mantenimientos")]
    public class Mantenimiento
    {
        [Key]
        public int IdMantenimiento { get; set; }

        public int IdVehiculo { get; set; }

        public DateTime FechaIngreso { get; set; } = DateTime.Now;

        public DateTime? FechaEgreso { get; set; }

        [MaxLength(50)]
        public string Tipo { get; set; } = "Mantenimiento Preventivo"; // "Siniestro / Reparación de Daño", "Mantenimiento Preventivo", etc.

        [MaxLength(500)]
        public string Motivo { get; set; } = string.Empty;

        public string? Diagnostico { get; set; }

        public string? TrabajoRealizado { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Costo { get; set; } = 0;

        public int? IdEmpleadoMecanico { get; set; }

        [MaxLength(50)]
        public string Estado { get; set; } = "En Taller"; // "En Taller", "Finalizado"

        [ForeignKey("IdVehiculo")]
        public virtual Vehiculo? Vehiculo { get; set; }

        [ForeignKey("IdEmpleadoMecanico")]
        public virtual Empleado? EmpleadoMecanico { get; set; }

        [NotMapped]
        public string DuracionTexto
        {
            get
            {
                var fin = FechaEgreso ?? DateTime.Now;
                var diff = fin - FechaIngreso;
                if (diff.TotalDays >= 1)
                    return $"{(int)diff.TotalDays} día(s)";
                return $"{(int)diff.TotalHours} hora(s)";
            }
        }
    }
}
