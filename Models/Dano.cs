using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DriveNow.Models
{
    [Table("Danos")]
    public class Dano
    {
        [Key]
        public int IdDano { get; set; }

        public string Zona { get; set; } = string.Empty;
        public string Descripcion { get; set; } = string.Empty;
        public string? EvidenciaFotografica { get; set; }

        public int IdAlquiler { get; set; }

        [ForeignKey("IdAlquiler")]
        public virtual Alquiler? Alquiler { get; set; }
    }
}