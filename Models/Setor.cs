using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CallStationApp.Models
{
    public class Setor
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }
        
        [Required]
        [Column("nome_setor", TypeName = "varchar(50)")]
        public required string NomeSetor { get; set; }
        
        [Required]
        [Column("empresa_id")]
        public required int EmpresaId { get; set; }
        [Required]
        [ForeignKey("EmpresaId")]
        public required Empresa Empresa { get; set; }
    }
}