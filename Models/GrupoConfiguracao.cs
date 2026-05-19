using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CallStationApp.Models
{
    public class GrupoConfiguracao
    {
        [Key]
        [Column("grupo_id")]
        public int GrupoId { get; set; }

        [ForeignKey(nameof(GrupoId))]
        public Grupo Grupo { get; set; } = null!;

        [Column("slug", TypeName = "varchar(60)")]
        [StringLength(60)]
        public string? Slug { get; set; }

        [Required]
        [Column("ativo")]
        public bool Ativo { get; set; } = true;

        [Required]
        [Column("obrigar_setor")]
        public bool ObrigarSetor { get; set; }

        [Required]
        [Column("obrigar_tipo_ocorrencia")]
        public bool ObrigarTipoOcorrencia { get; set; }

        [Required]
        [Column("obrigar_categoria")]
        public bool ObrigarCategoria { get; set; }

        [Required]
        [Column("obrigar_subcategoria")]
        public bool ObrigarSubcategoria { get; set; }

        [Required]
        [Column("permitir_chamado_publico")]
        public bool PermitirChamadoPublico { get; set; } = true;

        [Required]
        [Column("exigir_solucao_para_concluir")]
        public bool ExigirSolucaoParaConcluir { get; set; }

        [Column("dias_para_fechamento_automatico")]
        public int? DiasParaFechamentoAutomatico { get; set; }

        [Required]
        [Column("automatizar_pendente_por_prazo_conclusao")]
        public bool AutomatizarPendentePorPrazoConclusao { get; set; }

        [Column("horas_apos_vencimento_para_pendente")]
        public int? HorasAposVencimentoParaPendente { get; set; }

        [Column("horas_antes_prazo_para_alerta")]
        public int? HorasAntesPrazoParaAlerta { get; set; }

        [Required]
        [Column("notificar_administradores_sla")]
        public bool NotificarAdministradoresSla { get; set; } = true;

        [Required]
        [Column("exibir_data_finalizacao_modal")]
        public bool ExibirDataFinalizacaoModal { get; set; } = true;

        [Required]
        [Column("exibir_prazo_resposta_modal")]
        public bool ExibirPrazoRespostaModal { get; set; } = true;

        [Required]
        [Column("exibir_prazo_conclusao_modal")]
        public bool ExibirPrazoConclusaoModal { get; set; } = true;

        [Column("aparencia_tela_tipo", TypeName = "varchar(20)")]
        [StringLength(20)]
        public string? AparenciaTelaTipo { get; set; }

        [Column("aparencia_tela_valor", TypeName = "varchar(255)")]
        [StringLength(255)]
        public string? AparenciaTelaValor { get; set; }

        [Column("aparencia_sidebar_tipo", TypeName = "varchar(20)")]
        [StringLength(20)]
        public string? AparenciaSidebarTipo { get; set; }

        [Column("aparencia_sidebar_valor", TypeName = "varchar(255)")]
        [StringLength(255)]
        public string? AparenciaSidebarValor { get; set; }

        [Column("aparencia_menu_ativo_cor", TypeName = "varchar(7)")]
        [StringLength(7)]
        public string? AparenciaMenuAtivoCor { get; set; }

        [Column("aparencia_sidebar_texto_fundo_cor", TypeName = "varchar(7)")]
        [StringLength(7)]
        public string? AparenciaSidebarTextoFundoCor { get; set; }

        [Column("atualizado_por_usuario_id")]
        public int? AtualizadoPorUsuarioId { get; set; }

        [Column("data_atualizacao")]
        public DateTime DataAtualizacao { get; set; } = DateTime.UtcNow;
    }

    public class GrupoTipoChamado
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [Column("grupo_id")]
        public int GrupoId { get; set; }

        [ForeignKey(nameof(GrupoId))]
        public Grupo Grupo { get; set; } = null!;

        [Required]
        [Column("nome", TypeName = "varchar(50)")]
        [StringLength(50)]
        public string Nome { get; set; } = string.Empty;

        [Column("descricao", TypeName = "varchar(160)")]
        [StringLength(160)]
        public string? Descricao { get; set; }

        [Required]
        [Column("ativo")]
        public bool Ativo { get; set; } = true;

        [Required]
        [Column("posicao")]
        public int Posicao { get; set; }

        [Required]
        [Column("criado_por_usuario_id")]
        public int CriadoPorUsuarioId { get; set; }

        [Column("arquivado_por_usuario_id")]
        public int? ArquivadoPorUsuarioId { get; set; }

        [Column("data_criacao")]
        public DateTime DataCriacao { get; set; } = DateTime.UtcNow;

        [Column("data_arquivamento")]
        public DateTime? DataArquivamento { get; set; }
    }

    public class GrupoAuditoria
    {
        [Key]
        [Column("id")]
        public long Id { get; set; }

        [Required]
        [Column("grupo_id")]
        public int GrupoId { get; set; }

        [ForeignKey(nameof(GrupoId))]
        public Grupo Grupo { get; set; } = null!;

        [Required]
        [Column("usuario_id")]
        public int UsuarioId { get; set; }

        [ForeignKey(nameof(UsuarioId))]
        public Usuario Usuario { get; set; } = null!;

        [Required]
        [Column("tipo_acao", TypeName = "varchar(50)")]
        [StringLength(50)]
        public string TipoAcao { get; set; } = string.Empty;

        [Required]
        [Column("entidade", TypeName = "varchar(50)")]
        [StringLength(50)]
        public string Entidade { get; set; } = string.Empty;

        [Column("entidade_id")]
        public int? EntidadeId { get; set; }

        [Column("campo_alterado", TypeName = "varchar(80)")]
        [StringLength(80)]
        public string? CampoAlterado { get; set; }

        [Column("valor_anterior", TypeName = "varchar(500)")]
        [StringLength(500)]
        public string? ValorAnterior { get; set; }

        [Column("valor_novo", TypeName = "varchar(500)")]
        [StringLength(500)]
        public string? ValorNovo { get; set; }

        [Column("ip_origem", TypeName = "varchar(64)")]
        [StringLength(64)]
        public string? IpOrigem { get; set; }

        [Column("user_agent", TypeName = "varchar(255)")]
        [StringLength(255)]
        public string? UserAgent { get; set; }

        [Required]
        [Column("data_acao")]
        public DateTime DataAcao { get; set; } = DateTime.UtcNow;
    }
}
