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

        public string? Domicilio { get; set; }
        public string? Localidad { get; set; }
        public string? Provincia { get; set; }
        public DateTime? FechaNacimiento { get; set; }
        public string? ContactoEmergenciaNombre { get; set; }
        public string? ContactoEmergenciaTelefono { get; set; }
        public string? Nacionalidad { get; set; }

        public bool Activo { get; set; } = true; // Borrado lógico

        public virtual ICollection<Reserva> Reservas { get; set; } = new List<Reserva>();

        [NotMapped]
        public string NombreCompleto => $"{Nombre} {Apellido}";

        [NotMapped]
        public bool LicenciaVigente => VencimientoLicencia.Date >= DateTime.Today;

        [NotMapped]
        public string EstadoLicencia => LicenciaVigente ? "VALIDADO" : "VENCIDO";

        [NotMapped]
        public int? Edad => FechaNacimiento.HasValue
            ? (DateTime.Today.Year - FechaNacimiento.Value.Year - (DateTime.Today.DayOfYear < FechaNacimiento.Value.DayOfYear ? 1 : 0))
            : null;

        [NotMapped]
        public bool EsMayorDeEdad => Edad.HasValue && Edad.Value >= 21;

        [NotMapped]
        public string DomicilioCompleto => !string.IsNullOrWhiteSpace(Domicilio)
            ? $"{Domicilio}{(string.IsNullOrWhiteSpace(Localidad) ? "" : $", {Localidad}")}{(string.IsNullOrWhiteSpace(Provincia) ? "" : $" ({Provincia})")}"
            : "-";

        [NotMapped]
        public string EstadoRegistroTexto => Activo ? "Activo" : "Inactivo";
    }
}