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
        public required int ChamadoId { get; set; }
        [ForeignKey("ChamadoId")]
        public required Chamado Chamado { get; set; }
        
        [Required]
        [Column("usuario_id")]
        public required int UsuarioId { get; set; }
        [ForeignKey("UsuarioId")]
        public required Usuario Usuario { get; set; }
        
        [Required(ErrorMessage = "O campo mensagem é obrigatório.")]
        [Column("mensagem", TypeName = "varchar(500)")]
        [StringLength(500, ErrorMessage = "A mensagem do comentário não pode exceder 500 caracteres.")]
        public required string Mensagem { get; set; }
        
        [Column("anexo_comentario", TypeName = "varchar(255)")]
        [StringLength(255, ErrorMessage = "O anexo do comentário não pode exceder 255 caracteres.")]
        public string? AnexoComentario { get; set; }
        
        [Column("data_comentario")]
        public DateTime DataComentario { get; set; } = DateTime.Now;
    }
}