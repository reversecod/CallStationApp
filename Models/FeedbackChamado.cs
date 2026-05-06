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

        [Required(ErrorMessage = "O campo avaliacao e obrigatorio.")]
        [Range(1, 5, ErrorMessage = "A avaliacao deve ser um valor entre 1 e 5.")]
        [Column("nota", TypeName = "int")]
        public required int Avaliacao { get; set; }

        [Column("comentario", TypeName = "varchar(250)")]
        [StringLength(250, ErrorMessage = "O comentario nao pode exceder 250 caracteres.")]
        public string? Comentario { get; set; }

        [Column("tempo_resposta")]
        public int? TempoResposta { get; set; }

        [Column("tempo_resolucao")]
        public int? TempoResolucao { get; set; }

        [Column("data_avaliacao")]
        public DateTime DataAvaliacao { get; set; } = DateTime.UtcNow;
    }
}
