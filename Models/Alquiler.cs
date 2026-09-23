using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DriveNow.Models
{
    [Table("Alquileres")]
    public class Alquiler
    {
        [Key]
        public int IdAlquiler { get; set; }

        public int IdReserva { get; set; }
        public DateTime FechaEntregaReal { get; set; }
        public DateTime? FechaEntregaDevolucion { get; set; }

        [Column(TypeName = "decimal(5,2)")]
        public decimal CombustibleSalida { get; set; }

        [Column(TypeName = "decimal(5,2)")]
        public decimal? CombustibleEntrada { get; set; }

        public int KilometrajeSalida { get; set; }
        public int? KilometrajeEntrada { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal CostoTotal { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal MontoMora { get; set; } = 0;

        [ForeignKey("IdReserva")]
        public virtual Reserva? Reserva { get; set; }

        public virtual ICollection<Dano> Danos { get; set; } = new List<Dano>();

        [NotMapped]
        public decimal TotalLiquidado => CostoTotal + MontoMora;

        [NotMapped]
        public bool TieneDanos => Danos != null && Danos.Count > 0;

        [NotMapped]
        public string CodigoAlquiler => $"ALQ-{IdAlquiler:D4}";

        [NotMapped]
        public string ClienteTexto => Reserva?.Cliente != null ? $"{Reserva.Cliente.Nombre} {Reserva.Cliente.Apellido}" : "-";

        [NotMapped]
        public string VehiculoTexto => Reserva?.Vehiculo != null ? $"{Reserva.Vehiculo.Marca} {Reserva.Vehiculo.Modelo} ({Reserva.Vehiculo.Patente})" : "-";

        [NotMapped]
        public string EntregaTexto => FechaEntregaReal.ToString("dd/MM/yyyy HH:mm");

        [NotMapped]
        public string DevolucionTexto => FechaEntregaDevolucion.HasValue ? FechaEntregaDevolucion.Value.ToString("dd/MM/yyyy HH:mm") : "En curso";

        [NotMapped]
        public string KmTotalesTexto => KilometrajeEntrada.HasValue ? $"{KilometrajeEntrada.Value - KilometrajeSalida} km" : "-";

        [NotMapped]
        public string MoraTexto => MontoMora > 0 ? $"${MontoMora:N2}" : "$0.00";

        [NotMapped]
        public string EstadoTexto => FechaEntregaDevolucion.HasValue ? "Finalizado" : "En Curso";

        [NotMapped]
        public string KmRecorridosTexto => KilometrajeEntrada.HasValue ? $"{KilometrajeEntrada.Value - KilometrajeSalida} km"
            : "En curso";

        [NotMapped]
        public decimal CostoTotalFinal => CostoTotal + MontoMora;

        public int? IdEmpleadoEntrega { get; set; }

        [ForeignKey("IdEmpleadoEntrega")]
        public virtual Empleado? EmpleadoEntrega { get; set; }

        public int? IdEmpleadoDevolucion { get; set; }

        [ForeignKey("IdEmpleadoDevolucion")]
        public virtual Empleado? EmpleadoDevolucion { get; set; }
    }
}