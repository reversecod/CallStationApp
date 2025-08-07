using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CallStationApp.Models
{
    public class FeedbackChamado
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
        [Column("avaliador_id")]
        public required int AvaliadorId { get; set; }
        [ForeignKey("AvaliadorId")]
        public required Usuario Usuario { get; set; }
        
        [Required(ErrorMessage = "O campo avaliação é obrigatório.")]
        [Range(1, 5, ErrorMessage = "A avaliação deve ser um valor entre 1 e 5.")]
        [Column("avaliacao", TypeName = "int")]
        public required int Avaliacao { get; set; } 
        
        [Column("comentario", TypeName = "varchar(500)")]
        [StringLength(500, ErrorMessage = "O comentário não pode exceder 500 caracteres.")]
        public string? Comentario { get; set; }
        
        [Column("tempo_resposta")]
        public int? TempoResposta { get; set; }
        
        [Column("tempo_resolucao")]
        public int? TempoResolucao { get; set; }
                
        [Column("data_avaliacao")]
        public DateTime DataAvaliacao { get; set; } = DateTime.Now;
    }
}