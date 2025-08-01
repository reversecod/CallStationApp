using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CallStationApp.Models
{
    public class Tarefa
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }
        
        [Column("titulo", TypeName = "varchar(50)")]
        public string Titulo { get; set; } = "Nova Tarefa";
        
        [Required]
        [Column("descricao", TypeName = "text")]
        public required string Descricao { get; set; }
        
        [Column("empresa_id")]
        public int EmpresaId { get; set; }
        [Required]
        [ForeignKey("EmpresaId")]
        public required Empresa Empresa { get; set; }
        
        public int CriadorId { get; set; }
        [ForeignKey("CriadorId")]
        [Required]
        public required Usuario Usuario { get; set; }
        
        [Column("urgencia_id")]
        public int UrgenciaId { get; set; }
        [ForeignKey("UrgenciaId")]
        public Urgencia? Urgencia { get; set; }
        
        [Column("status")]
        [EnumDataType(typeof(StatusTarefa))]
        public StatusTarefa StatusAtual { get; set; } 
        
        [Column("data_criacao")]
        public DateTime DataCriacao  { get; set; }
        
        [Column("data_conclusao")]
        public DateTime? DataConclusao { get; set; }
    }
    
    public enum StatusTarefa
    {
        Pendente,
        EmAndamento,
        Concluida,
        Cancelada
    }
}