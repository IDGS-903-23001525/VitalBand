using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VitalBand.Models
{
    [Table("PATOLOGIAS_CATALOGO")]
    public class PatologiaCatalogo
    {
        [Key]
        public int id { get; set; }

        [Required]
        [StringLength(100)]
        public string nombre_enfermedad { get; set; } = string.Empty;

        public string? descripcion { get; set; }
    }
}
