using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DriveNow.Models
{
    [Table("Reservas")]
    public class Reserva
    {
        [Key]
        public int IdReserva { get; set; }

        public DateTime FechaDesde { get; set; }
        public DateTime FechaHasta { get; set; }

        public int IdEstado { get; set; }
        public int IdCliente { get; set; }
        public int IdEmpleado { get; set; }
        public int IdVehiculo { get; set; }

        [ForeignKey("IdEstado")]
        public virtual Estado? Estado { get; set; }

        [ForeignKey("IdCliente")]
        public virtual Cliente? Cliente { get; set; }

        [ForeignKey("IdEmpleado")]
        public virtual Empleado? Empleado { get; set; }

        [ForeignKey("IdVehiculo")]
        public virtual Vehiculo? Vehiculo { get; set; }

        // Relación 1 a 1 con Alquiler
        public virtual Alquiler? Alquiler { get; set; }

        [NotMapped]
        public int TotalDias => Math.Max(1, (FechaHasta.Date - FechaDesde.Date).Days);

        [NotMapped]
        public string CodigoReserva => $"RES-{IdReserva:D4}";

        [NotMapped]
        public string NombreCliente => Cliente != null ? $"{Cliente.Nombre} {Cliente.Apellido}" : "-";

        [NotMapped]
        public string VehiculoDetalle => Vehiculo != null ? $"{Vehiculo.Marca} {Vehiculo.Modelo} ({Vehiculo.Patente})" : "-";

        [NotMapped]
        public string PeriodoTexto => $"{FechaDesde:dd/MM/yyyy} - {FechaHasta:dd/MM/yyyy}";

        [NotMapped]
        public string EstadoNombre => Estado?.Nombre ?? "Confirmada";
    }
}