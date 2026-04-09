using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CallStationApp.Models
{
    public class HistoricoStatusChamado
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [Column("chamado_id")]
        public required int ChamadoId { get; set; }

        [ForeignKey("ChamadoId")]
        public Chamado? Chamado { get; set; }

        [Required]
        [Column("status_anterior")]
        [EnumDataType(typeof(StatusAnteriorChamado))]
        public required StatusAnteriorChamado StatusAnterior { get; set; }

        [Required]
        [Column("status_novo")]
        [EnumDataType(typeof(StatusNovoChamado))]
        public required StatusNovoChamado StatusNovo { get; set; }

        [Column("data_transicao", TypeName = "datetime")]
        public DateTime DataTransicao { get; set; } = DateTime.UtcNow;
    }

    public enum StatusAnteriorChamado
    {
        Aberto,
        EmAndamento,
        Pendente,
        Concluido,
        Fechado,
        Reaberto,
        Cancelado,
        Excluido
    }

    public enum StatusNovoChamado
    {
        Aberto,
        EmAndamento,
        Pendente,
        Concluido,
        Fechado,
        Reaberto,
        Cancelado,
        Excluido
    }
}
