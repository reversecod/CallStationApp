using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CallStationApp.Models
{
    public class Chamado
    {
        // =====================
        // ID TÉCNICO
        // =====================
        [Key]
        [Column("id")]
        public int Id { get; set; }

        // =====================
        // IDS DE NEGÓCIO
        // =====================
        [Required]
        [Column("numero_chamado_grupo")]
        public int NumeroChamadoGrupo { get; set; }

        [Required]
        [Column("numero_chamado_usuario")]
        public int NumeroChamadoUsuario { get; set; }

        [Required]
        [Column("numero_chamado_usuario_grupo")]
        public int NumeroChamadoUsuarioGrupo { get; set; }

        // =====================
        // CONTEÚDO
        // =====================
        [Column("titulo", TypeName = "varchar(50)")]
        [StringLength(50)]
        public string? Titulo { get; set; }

        [Column("descricao", TypeName = "varchar(1000)")]
        [StringLength(1000)]
        public string? Descricao { get; set; }

        [Column("solucao", TypeName = "varchar(1000)")]
        [StringLength(1000)]
        public string? Solucao { get; set; }

        // =====================
        // RELAÇÃO COM GRUPO / USUÁRIO
        // =====================
        [Required]
        [Column("grupo_id")]
        public int GrupoId { get; set; }

        [Required]
        [Column("criador_chamado_id")]
        public int CriadorChamadoId { get; set; }

        // =====================
        // CLASSIFICAÇÕES
        // =====================
        [Column("ocorrencia_tipo_id")]
        public int? OcorrenciaTipoId { get; set; }

        [Column("ocorrencia_categoria_id")]
        public int? OcorrenciaCategoriaId { get; set; }

        [Column("ocorrencia_subcategoria_id")]
        public int? OcorrenciaSubcategoriaId { get; set; }

        [Column("setor_id")]
        public int? SetorId { get; set; }

        // =====================
        // OUTROS CAMPOS
        // =====================
        [Column("anexo_chamado", TypeName = "varchar(255)")]
        [StringLength(255)]
        public string? AnexoChamado { get; set; }

        // =====================
        // ENUMS
        // =====================
        [Column("prioridade")]
        public PrioridadeChamado? Prioridade { get; set; }

        [Column("criticidade")]
        public CriticidadeChamado? Criticidade { get; set; }

        [Column("urgencia")]
        public UrgenciaChamado? Urgencia { get; set; }

        [Required]
        [Column("status")]
        public StatusChamado Status { get; set; } = StatusChamado.Aberto;

        // =====================
        // DATAS
        // =====================
        [Column("data_inicio_atendimento")]
        public DateTime? DataInicioAtendimento { get; set; }

        [Column("data_criacao")]
        public DateTime DataCriacao { get; set; }

        [Column("data_finalizacao")]
        public DateTime? DataFinalizacao { get; set; }

        [Column("prazo_resposta")]
        public DateTime? PrazoResposta { get; set; }

        [Column("prazo_conclusao")]
        public DateTime? PrazoConclusao { get; set; }

        // =====================
        // VISIBILIDADE
        // =====================
        [Column("publico")]
        public bool Publico { get; set; } = false;
    }

    public enum StatusChamado
    {
        [Display(Name = "Aberto")]
        Aberto,

        [Display(Name = "Em Andamento")]
        EmAndamento,

        [Display(Name = "Pendente")]
        Pendente,

        [Display(Name = "Concluído")]
        Concluido,

        [Display(Name = "Fechado")]
        Fechado,

        [Display(Name = "Reaberto")]
        Reaberto,

        [Display(Name = "Cancelado")]
        Cancelado,

        [Display(Name = "Excluído")]
        Excluido
    }

    public enum UrgenciaChamado
    {
        [Display(Name = "Não Urgente")]
        NaoUrgente,

        [Display(Name = "Pouca Urgência")]
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

    public enum PrioridadeChamado
    {
        [Display(Name = "Prioridade Baixa")]
        Baixa,

        [Display(Name = "Prioridade Média")]
        Media,

        [Display(Name = "Prioridade Alta")]
        Alta,

        [Display(Name = "Prioridade Crítica")]
        Critica
    }
}