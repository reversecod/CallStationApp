using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CallStationApp.Models
{
    public class Ocorrencia
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }
        
        [Required]
        [Column("tipo_ocorrencia", TypeName = "varchar(50)")]
        public required string TipoOcorrencia { get; set; }
        
        [Column("empresa_id")]
        public int EmpresaId { get; set; }
        [Required]
        [ForeignKey("EmpresaId")]
        public required Empresa Empresa { get; set; }
    }
}