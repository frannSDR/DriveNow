using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DriveNow.Models
{
    [Table("Clientes")]
    public class Cliente
    {
        [Key]
        public int IdCliente { get; set; }

        public string Dni { get; set; } = string.Empty;
        public string Nombre { get; set; } = string.Empty;
        public string Apellido { get; set; } = string.Empty;
        public string? Telefono { get; set; }
        public string? Correo { get; set; }
        public string NroLicencia { get; set; } = string.Empty;
        public DateTime VencimientoLicencia { get; set; }

        public virtual ICollection<Reserva> Reservas { get; set; } = new List<Reserva>();

        // Propiedades calculadas en tiempo real para la UI
        [NotMapped]
        public string NombreCompleto => $"{Nombre} {Apellido}";

        [NotMapped]
        public bool LicenciaVigente => VencimientoLicencia.Date >= DateTime.Today;

        [NotMapped]
        public string EstadoLicencia => LicenciaVigente ? "VALIDADO" : "VENCIDO";
    }
}