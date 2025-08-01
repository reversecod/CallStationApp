using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CallStationApp.Models
{
    public class Empresa
    {
        [Key]
        [Column("id")]
        public int Id { get; set; } 
        
        [Required]
        [Column("nome", TypeName = "varchar(100)")]
        public required string Nome { get; set; }
        
        [Required]
        [Column("cnpj", TypeName = "varchar(20)")]
        public required string CNPJ { get; set; }
        
        [Column("data_criacao")]
        public DateTime DataCriacao { get; set; }
    }
}