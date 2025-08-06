using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CallStationApp.Models
{
    public class ComentarioChamado
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }
        
        [Required]
        [Column("chamado_id")]
        public int ChamadoId { get; set; }
        [ForeignKey("ChamadoId")]
        public required Chamado? Chamado { get; set; }
        
        [Required]
        [Column("usuario_id")]
        public int UsuarioId { get; set; }
        [ForeignKey("UsuarioId")]
        public required Usuario? Usuario { get; set; }
        
        [Required]
        [Column("mensagem", TypeName = "text")]
        public required string Mensagem { get; set; }
        
        [Column("anexo_comentario", TypeName = "varchar(255)")]
        public string? AnexoComentario { get; set; }
        
        [Column("data_comentario")]
        public DateTime DataComentario { get; set; } = DateTime.Now;
    }
}