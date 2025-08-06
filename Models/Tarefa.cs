using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CallStationApp.Models
{
    public class Tarefa
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }
        
        [Required]
        [Column("titulo", TypeName = "varchar(50)")]
        public required string Titulo { get; set; } 
        
        [Required]
        [Column("descricao", TypeName = "text")]
        public required string Descricao { get; set; }
        
        [Required]
        [Column("criador_id")]
        public required int CriadorId { get; set; }
        [ForeignKey("CriadorId")]
        public required Usuario Usuario{ get; set; }
        
        [Column("grupo_id")]
        public int GrupoId { get; set; }
        [ForeignKey("GrupoId")]
        public Grupo? Grupo { get; set; }
        
        [Column("criticidade")]
        [EnumDataType(typeof(CriticidadeTarefa))]
        public CriticidadeTarefa Criticidade { get; set; } 
        
        [Column("urgencia")]
        [EnumDataType(typeof(UrgenciaTarefa))]
        public UrgenciaTarefa Urgencia { get; set; } 
        
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
        [Display(Name = "Aberto")]
        Aberto,
        [Display(Name = "Em Andamento")]
        EmAndamento,
        [Display(Name = "Concluído")]
        Concluido,
        [Display(Name = "Cancelado")]
        Cancelado,
        [Display(Name = "Excluído")]
        Excluido
    }
    
    public enum UrgenciaTarefa
    {
        [Display(Name = "Não Urgente")]
        NaoUrgente,
        [Display(Name = "Pouco Urgente")]
        PoucaUrgencia,
        [Display(Name = "Urgente")]
        Urgente,
        [Display(Name = "Emergência")]
        Emergencia
    }
    public enum CriticidadeTarefa
    {  
        [Display(Name = "Baixa Criticidade")]
        Baixa,
        [Display(Name = "Média Criticidade")]
        Media,
        [Display(Name = "Alta Criticidade")]
        Alta,
        [Display(Name = "Crítico")]
        Critico
    }
}