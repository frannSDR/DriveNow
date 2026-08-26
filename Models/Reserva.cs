using System;

namespace DriveNow.Models
{
    public class Reserva
    {
        public int IdReserva { get; set; }
        public string CodigoReserva { get; set; } = string.Empty; // Ej: "RES-1001" / Debera ser generado automaticamente, partiendo de 'AAA-0000'
        public string DniCliente { get; set; } = string.Empty;
        public string NombreCliente { get; set; } = string.Empty;
        public string PatenteVehiculo { get; set; } = string.Empty;
        public string VehiculoDetalle { get; set; } = string.Empty; // Ej: "Toyota Corolla (Sedán)"
        public string Categoria { get; set; } = string.Empty;
        public DateTime FechaInicio { get; set; }
        public DateTime FechaFin { get; set; }
        public string Estado { get; set; } = "Activa"; // "Activa", "Pendiente", "Cancelada", "En Curso"

        public string PeriodoTexto => $"{FechaInicio:dd/MM/yyyy} al {FechaFin:dd/MM/yyyy}";
        public int DiasTotales => (int)(FechaFin.Date - FechaInicio.Date).TotalDays + 1;
    }
}