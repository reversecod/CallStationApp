using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CallStationApp.Models
{
    public class Grupo
    {
        [Key]
        [Column("id")]
        public int Id { get; set; } 
        
        [Required]
        [Column("nome", TypeName = "varchar(100)")]
        public required string Nome { get; set; }
        
        [Column("descricao_grupo", TypeName = "varchar(200)")]
        public string? DescricaoGrupo { get; set; }
        
        [Column("foto_grupo", TypeName = "varchar(255)")]
        public string? FotoGrupo { get; set; }
        
        [Required]
        [Column("criador_id")]
        public int CriadorId { get; set; }
        [ForeignKey("CriadorId")]
        public Usuario? Usuario { get; set; }
        
        [Column("data_criacao")]
        public DateTime DataCriacao { get; set; }
    }
}