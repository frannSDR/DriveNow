using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DriveNow.Models
{
    [Table("Empleados")]
    public class Empleado
    {
        [Key]
        public int IdEmpleado { get; set; }

        public string Dni { get; set; } = string.Empty;
        public string Nombre { get; set; } = string.Empty;
        public string Apellido { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public string? FotoPerfilRuta { get; set; }

        public int IdRol { get; set; }

        [ForeignKey("IdRol")]
        public virtual Rol? Rol { get; set; }

        public virtual ICollection<Reserva> Reservas { get; set; } = new List<Reserva>();

        [NotMapped]
        public string NombreCompleto => $"{Nombre} {Apellido}";

        [NotMapped]
        public bool EsAdmin => IdRol == 1;
    }
}
