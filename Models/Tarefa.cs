using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CallStationApp.Models
{
    public class Tarefa
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }
        
        [Required(ErrorMessage = "O título é obrigatório.")]
        [Column("titulo", TypeName = "varchar(50)")]
        [StringLength(50, ErrorMessage = "O título não pode exceder 50 caracteres.")]
        public required string Titulo { get; set; } 
        
        [Required(ErrorMessage = "A descrição é obrigatória.")]
        [Column("descricao", TypeName = "varchar(500)")]
        [StringLength(500, ErrorMessage = "A descrição não pode exceder 500 caracteres.")]
        public required string Descricao { get; set; }
        
        [Required]
        [Column("criador_id")]
        public required int CriadorId { get; set; }
        [ForeignKey("CriadorId")]
        public required Usuario Usuario{ get; set; }
        
        [Column("grupo_id")]
        public int? GrupoId { get; set; }
        [ForeignKey("GrupoId")]
        public Grupo? Grupo { get; set; }
        
        [Column("criticidade")]
        [EnumDataType(typeof(CriticidadeTarefa))]
        public CriticidadeTarefa? Criticidade { get; set; } 
        
        [Column("urgencia")]
        [EnumDataType(typeof(UrgenciaTarefa))]
        public UrgenciaTarefa? Urgencia { get; set; } 
        
        [Column("status")]
        [EnumDataType(typeof(StatusTarefa))]
        public StatusTarefa? Status { get; set; } 
        
        [Column("data_criacao")]
        public DateTime DataCriacao  { get; set; } = DateTime.Now;
        
        [Column("data_conclusao")]
        public DateTime? DataConclusao { get; set; }
    }
    
    public enum StatusTarefa
    {
        [Display(Name = "Pendente")]
        Pendente,
        [Display(Name = "Em Andamento")]
        EmAndamento,
        [Display(Name = "Concluída")]
        Concluida,
        [Display(Name = "Cancelada")]
        Cancelada
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