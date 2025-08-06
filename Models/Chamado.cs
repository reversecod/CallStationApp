using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CallStationApp.Models
{
    public class Chamado
    {
        [Key]
        [Column("id")]
        public int Id { get; set; } 
        
        [Column("titulo", TypeName = "varchar(50)")]
        public string? Titulo { get; set; }
        
        [Column("descricao", TypeName = "text")]
        public string? Descricao { get; set; }
        
        [Column("solucao", TypeName = "text")]
        public string? Solucao { get; set; }
        
        [Column("grupo_id")]
        public int? GrupoId { get; set; }
        [ForeignKey("GrupoId")]
        public Grupo? Grupo { get; set; }
        
        [Column("ocorrencia_tipo_id")]
        public int OcorrenciaTipoId { get; set; }
        [ForeignKey("OcorrenciaTipoId")]
        public OcorrenciaTipo? OcorrenciaTipo { get; set; }
        
        [Column("ocorrencia_categoria_id")]
        public int OcorrenciaCategoriaId { get; set; }
        [ForeignKey("OcorrenciaCategoriaId")]
        public OcorrenciaCategoria? OcorrenciaCategoria { get; set; }
        
        [Column("ocorrencia_subcategoria_id")]
        public int OcorrenciaSubcategoriaId { get; set; }
        [ForeignKey("OcorrenciaSubcategoriaId")]
        public OcorrenciaSubcategoria? OcorrenciaSubcategoria { get; set; }
        
        [Column("setor_id")]
        public int SetorId { get; set; }
        [ForeignKey("SetorId")]
        public Setor? Setor { get; set; }
        
        [Column("anexo_chamado", TypeName = "varchar(255)")]
        public string? AnexoChamado { get; set; }
        
        [Column("criador_solicitacao", TypeName = "varchar(100)")]
        public string? CriadorSolicitacao { get; set; }
        
        [Required]
        [Column("criador_chamado")]
        public required string CriadorChamado { get; set; }
        [ForeignKey("CriadorChamado")]
        public Usuario? Usuario { get; set; }
        
        [Column("criticidade")]
        [EnumDataType(typeof(CriticidadeChamado))]
        public CriticidadeChamado Criticidade { get; set; } 
        
        [Column("urgencia")]
        [EnumDataType(typeof(UrgenciaChamado))]
        public UrgenciaChamado Urgencia { get; set; } 
        
        [Column("status")]
        [EnumDataType(typeof(StatusChamado))]
        public StatusChamado StatusAtual { get; set; } 
        
        [Column("data_criacao")]
        public DateTime DataCriacao { get; set; } = DateTime.Now;
        
        [Column("data_finalizacao")]
        public DateTime? DataFinalizacao { get; set; }
        
        [Column("prazo_resposta")]
        public DateTime? PrazoResposta { get; set; } 
        
        [Column("prazo_conclusao")]
        public DateTime? PrazoConclusao { get; set; }
        
        [Column("publico")]
        public bool Observacoes { get; set; } = false;
        
    }
    public enum StatusChamado
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
    public enum UrgenciaChamado
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
    public enum CriticidadeChamado
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