using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CallStationApp.Models
{
    public class Chamado
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [Column("numero_chamado_grupo")]
        public int NumeroChamadoGrupo { get; set; }

        [Required]
        [Column("numero_chamado_usuario")]
        public int NumeroChamadoUsuario { get; set; }

        [Required]
        [Column("numero_chamado_usuario_grupo")]
        public int NumeroChamadoUsuarioGrupo { get; set; }

        [Column("titulo", TypeName = "varchar(42)")]
        [StringLength(42)]
        public string? Titulo { get; set; }

        [Column("descricao", TypeName = "varchar(500)")]
        [StringLength(500)]
        public string? Descricao { get; set; }

        [Column("solucao", TypeName = "varchar(500)")]
        [StringLength(500)]
        public string? Solucao { get; set; }

        [Required]
        [Column("grupo_id")]
        public int GrupoId { get; set; }

        [Required]
        [Column("criador_chamado_id")]
        public int CriadorChamadoId { get; set; }

        [Column("ocorrencia_tipo_id")]
        public int? OcorrenciaTipoId { get; set; }

        [Column("ocorrencia_categoria_id")]
        public int? OcorrenciaCategoriaId { get; set; }

        [Column("ocorrencia_subcategoria_id")]
        public int? OcorrenciaSubcategoriaId { get; set; }

        [Column("setor_id")]
        public int? SetorId { get; set; }

        [Column("anexo_chamado", TypeName = "varchar(255)")]
        [StringLength(255)]
        public string? AnexoChamado { get; set; }

        [Column("prioridade")]
        public PrioridadeChamado? Prioridade { get; set; }

        [Column("criticidade")]
        public CriticidadeChamado? Criticidade { get; set; }

        [Column("urgencia")]
        public UrgenciaChamado? Urgencia { get; set; }

        [Required]
        [Column("status")]
        public StatusChamado Status { get; set; } = StatusChamado.Aberto;

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

        [Column("prazo_conclusao_operacional")]
        public DateTime? PrazoConclusaoOperacional { get; set; }

        [Column("publico")]
        public bool Publico { get; set; } = false;

        public ICollection<HistoricoAlteracaoChamado> HistoricoAlteracoes { get; set; } = new List<HistoricoAlteracaoChamado>();
    }

    public class ChamadoPeriodoPendente
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("chamado_id")]
        public int ChamadoId { get; set; }

        [Column("inicio_pendente")]
        public DateTime InicioPendente { get; set; }

        [Column("fim_pendente")]
        public DateTime? FimPendente { get; set; }

        [Column("duracao_segundos")]
        public long? DuracaoSegundos { get; set; }

        [Column("observacao_entrada", TypeName = "varchar(500)")]
        [StringLength(500)]
        public string? ObservacaoEntrada { get; set; }

        [Column("observacao_saida", TypeName = "varchar(500)")]
        [StringLength(500)]
        public string? ObservacaoSaida { get; set; }

        [Column("criado_por_usuario_id")]
        public int CriadoPorUsuarioId { get; set; }

        [Column("finalizado_por_usuario_id")]
        public int? FinalizadoPorUsuarioId { get; set; }

        [Column("criado_em")]
        public DateTime CriadoEm { get; set; } = DateTime.UtcNow;

        [Column("atualizado_em")]
        public DateTime? AtualizadoEm { get; set; }

        [ForeignKey(nameof(ChamadoId))]
        public Chamado Chamado { get; set; } = null!;

        [ForeignKey(nameof(CriadoPorUsuarioId))]
        public Usuario CriadoPorUsuario { get; set; } = null!;

        [ForeignKey(nameof(FinalizadoPorUsuarioId))]
        public Usuario? FinalizadoPorUsuario { get; set; }
    }

    public class ChamadoVinculo
    {
        [Column("chamado_id_menor")]
        public int ChamadoIdMenor { get; set; }

        [Column("chamado_id_maior")]
        public int ChamadoIdMaior { get; set; }

        [Column("grupo_id")]
        public int GrupoId { get; set; }

        [Column("data_vinculo")]
        public DateTime DataVinculo { get; set; }

        [Column("vinculado_por_usuario_id")]
        public int VinculadoPorUsuarioId { get; set; }

        [ForeignKey(nameof(ChamadoIdMenor))]
        public Chamado ChamadoMenor { get; set; } = null!;

        [ForeignKey(nameof(ChamadoIdMaior))]
        public Chamado ChamadoMaior { get; set; } = null!;

        [ForeignKey(nameof(GrupoId))]
        public Grupo Grupo { get; set; } = null!;

        [ForeignKey(nameof(VinculadoPorUsuarioId))]
        public Usuario VinculadoPorUsuario { get; set; } = null!;
    }

    public enum StatusChamado
    {
        [Display(Name = "Aberto")]
        Aberto,
        [Display(Name = "Em Andamento")]
        EmAndamento,
        [Display(Name = "Pendente")]
        Pendente,
        [Display(Name = "Em atraso")]
        EmAtraso,
        [Display(Name = "Concluido")]
        Concluido,
        [Display(Name = "Fechado")]
        Fechado,
        [Display(Name = "Reaberto")]
        Reaberto,
        [Display(Name = "Cancelado")]
        Cancelado,
        [Display(Name = "Excluido")]
        Excluido
    }

    public enum UrgenciaChamado
    {
        [Display(Name = "Nao Urgente")]
        NaoUrgente,
        [Display(Name = "Pouca Urgencia")]
        PoucaUrgencia,
        [Display(Name = "Urgente")]
        Urgente,
        [Display(Name = "Emergencia")]
        Emergencia
    }

    public enum CriticidadeChamado
    {
        [Display(Name = "Baixa Criticidade")]
        Baixa,
        [Display(Name = "Media Criticidade")]
        Media,
        [Display(Name = "Alta Criticidade")]
        Alta,
        [Display(Name = "Critico")]
        Critico
    }

    public enum PrioridadeChamado
    {
        [Display(Name = "Prioridade Baixa")]
        Baixa,
        [Display(Name = "Prioridade Media")]
        Media,
        [Display(Name = "Prioridade Alta")]
        Alta,
        [Display(Name = "Prioridade Critica")]
        Critica
    }
}
