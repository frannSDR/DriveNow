using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DriveNow.Models
{
    [Table("Roles")]
    public class Rol
    {
        [Key]
        public int IdRol { get; set; }

        public string Nombre { get; set; } = string.Empty;
        public string? Descripcion { get; set; }

        public virtual ICollection<Empleado> Empleados { get; set; } = new List<Empleado>();
    }
}