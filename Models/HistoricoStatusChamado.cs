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
        public required Chamado? Chamado { get; set; }
        
        [Required]
        [Column("status_anterior")]
        [EnumDataType(typeof(StatusAnteriorChamado))]
        public required StatusAnteriorChamado StatusAnterior { get; set; }
        
        [Required]
        [Column("status_novo")]
        [EnumDataType(typeof(StatusNovoChamado))]
        public required StatusNovoChamado StatusNovo { get; set; }
        
        [Column("data_transicao", TypeName = "datetime")]
        public DateTime DataTransicao { get; set; } = DateTime.Now;
    }
    
    public enum StatusAnteriorChamado
    {
        Aberto,
        EmAndamento,
        Pendente, //colocar pendente quando o chamado for aguardando resposta do usuário ou após o término do SLA
        Concluido,
        Fechado,
        Reaberto,
        Cancelado,
        Exluido
    }
    
    public enum StatusNovoChamado
    {
        Aberto,
        EmAndamento,
        Pendente, //colocar pendente quando o chamado for aguardando resposta do usuário ou após o término do SLA
        Concluido,
        Fechado,
        Reaberto,
        Cancelado,
        Exluido
    }
}