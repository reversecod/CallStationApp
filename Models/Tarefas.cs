using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CallStationApp.Models
{
    public class QuadroTarefa
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [Column("grupo_id")]
        public int GrupoId { get; set; }

        [Required]
        [Column("nome", TypeName = "varchar(100)")]
        [StringLength(100)]
        public string Nome { get; set; } = string.Empty;

        [Column("descricao", TypeName = "varchar(300)")]
        [StringLength(300)]
        public string? Descricao { get; set; }

        [Column("cor", TypeName = "varchar(20)")]
        [StringLength(20)]
        public string? Cor { get; set; }

        [Column("ativo")]
        public bool Ativo { get; set; } = true;

        [Required]
        [Column("criado_por_usuario_id")]
        public int CriadoPorUsuarioId { get; set; }

        [Column("data_criacao")]
        public DateTime DataCriacao { get; set; }

        [ForeignKey(nameof(GrupoId))]
        public Grupo Grupo { get; set; } = null!;

        [ForeignKey(nameof(CriadoPorUsuarioId))]
        public Usuario CriadoPorUsuario { get; set; } = null!;
    }

    public class QuadroTarefaUsuario
    {
        [Column("quadro_id")]
        public int QuadroId { get; set; }

        [Column("usuario_id")]
        public int UsuarioId { get; set; }

        [Column("permissao")]
        public PermissaoQuadroTarefa Permissao { get; set; } = PermissaoQuadroTarefa.Visualizador;

        [Column("data_adicao")]
        public DateTime DataAdicao { get; set; }

        [Column("adicionado_por_usuario_id")]
        public int AdicionadoPorUsuarioId { get; set; }

        [ForeignKey(nameof(QuadroId))]
        public QuadroTarefa Quadro { get; set; } = null!;

        [ForeignKey(nameof(UsuarioId))]
        public Usuario Usuario { get; set; } = null!;

        [ForeignKey(nameof(AdicionadoPorUsuarioId))]
        public Usuario AdicionadoPorUsuario { get; set; } = null!;
    }

    public class ColunaQuadro
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("quadro_id")]
        public int QuadroId { get; set; }

        [Column("grupo_id")]
        public int GrupoId { get; set; }

        [Column("usuario_id")]
        public int UsuarioId { get; set; }

        [Required]
        [Column("nome", TypeName = "varchar(60)")]
        [StringLength(60)]
        public string Nome { get; set; } = string.Empty;

        [Column("posicao", TypeName = "decimal(18,6)")]
        public decimal Posicao { get; set; }

        [Column("cor", TypeName = "varchar(20)")]
        [StringLength(20)]
        public string? Cor { get; set; }

        [Column("limite_wip")]
        public int? LimiteWip { get; set; }

        [Column("ativa")]
        public bool Ativa { get; set; } = true;

        [Column("data_criacao")]
        public DateTime DataCriacao { get; set; }

        [ForeignKey(nameof(QuadroId))]
        public QuadroTarefa Quadro { get; set; } = null!;

        [ForeignKey(nameof(GrupoId))]
        public Grupo Grupo { get; set; } = null!;

        [ForeignKey(nameof(UsuarioId))]
        public Usuario Usuario { get; set; } = null!;
    }

    public class CartaoTarefaPosicaoUsuario
    {
        [Column("cartao_tarefa_id")]
        public int CartaoTarefaId { get; set; }

        [Column("grupo_id")]
        public int GrupoId { get; set; }

        [Column("usuario_id")]
        public int UsuarioId { get; set; }

        [Column("coluna_id")]
        public int ColunaId { get; set; }

        [Column("ordem_coluna", TypeName = "decimal(18,6)")]
        public decimal OrdemColuna { get; set; }

        [Column("data_criacao")]
        public DateTime DataCriacao { get; set; }

        [Column("data_atualizacao")]
        public DateTime DataAtualizacao { get; set; }

        [ForeignKey(nameof(CartaoTarefaId))]
        public CartaoTarefa CartaoTarefa { get; set; } = null!;

        [ForeignKey(nameof(GrupoId))]
        public Grupo Grupo { get; set; } = null!;

        [ForeignKey(nameof(UsuarioId))]
        public Usuario Usuario { get; set; } = null!;

        public ColunaQuadro Coluna { get; set; } = null!;
    }

    public class CartaoTarefaContadorGrupo
    {
        [Key]
        [Column("grupo_id")]
        public int GrupoId { get; set; }

        [Column("ultimo_numero")]
        public int UltimoNumero { get; set; }

        [ForeignKey(nameof(GrupoId))]
        public Grupo Grupo { get; set; } = null!;
    }

    public class TemplateCartaoTarefa
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("grupo_id")]
        public int GrupoId { get; set; }

        [Column("criado_por_usuario_id")]
        public int CriadoPorUsuarioId { get; set; }

        [Required]
        [Column("nome", TypeName = "varchar(100)")]
        [StringLength(100)]
        public string Nome { get; set; } = string.Empty;

        [Column("descricao", TypeName = "text")]
        public string? Descricao { get; set; }

        [Column("prioridade")]
        public PrioridadeChamado? Prioridade { get; set; }

        [Column("criticidade")]
        public CriticidadeChamado? Criticidade { get; set; }

        [Column("urgencia")]
        public UrgenciaChamado? Urgencia { get; set; }

        [Column("cor_capa", TypeName = "varchar(20)")]
        [StringLength(20)]
        public string? CorCapa { get; set; }

        [Column("data_criacao")]
        public DateTime DataCriacao { get; set; }

        [Column("data_atualizacao")]
        public DateTime DataAtualizacao { get; set; }

        [Column("ativo")]
        public bool Ativo { get; set; } = true;

        [ForeignKey(nameof(GrupoId))]
        public Grupo Grupo { get; set; } = null!;

        [ForeignKey(nameof(CriadoPorUsuarioId))]
        public Usuario CriadoPorUsuario { get; set; } = null!;
    }

    public class CartaoTarefa
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("quadro_id")]
        public int QuadroId { get; set; }

        [Column("coluna_id")]
        public int ColunaId { get; set; }

        [Column("grupo_id")]
        public int GrupoId { get; set; }

        [Column("pai_cartao_id")]
        public int? PaiCartaoId { get; set; }

        [Column("numero_cartao_grupo")]
        public int NumeroCartaoGrupo { get; set; }

        [Required]
        [Column("titulo", TypeName = "varchar(150)")]
        [StringLength(150)]
        public string Titulo { get; set; } = string.Empty;

        [Column("descricao", TypeName = "text")]
        public string? Descricao { get; set; }

        [Column("criador_id")]
        public int CriadorId { get; set; }

        [Column("responsavel_usuario_id")]
        public int? ResponsavelUsuarioId { get; set; }

        [Column("prioridade")]
        public PrioridadeChamado? Prioridade { get; set; }

        [Column("criticidade")]
        public CriticidadeChamado? Criticidade { get; set; }

        [Column("urgencia")]
        public UrgenciaChamado? Urgencia { get; set; }

        [Column("status")]
        public StatusCartaoTarefa Status { get; set; } = StatusCartaoTarefa.Ativa;

        [Column("cor_capa", TypeName = "varchar(20)")]
        [StringLength(20)]
        public string? CorCapa { get; set; }

        [Column("imagem_capa", TypeName = "varchar(255)")]
        [StringLength(255)]
        public string? ImagemCapa { get; set; }

        [Column("data_inicio")]
        public DateTime? DataInicio { get; set; }

        [Column("data_vencimento")]
        public DateTime? DataVencimento { get; set; }

        [Column("data_vencimento_operacional")]
        public DateTime? DataVencimentoOperacional { get; set; }

        [Column("data_conclusao")]
        public DateTime? DataConclusao { get; set; }

        [Column("ordem_coluna", TypeName = "decimal(18,6)")]
        public decimal OrdemColuna { get; set; }

        [Column("percentual_conclusao", TypeName = "decimal(5,2)")]
        public decimal PercentualConclusao { get; set; }

        [Column("bloqueada")]
        public bool Bloqueada { get; set; }

        [Column("motivo_bloqueio", TypeName = "varchar(255)")]
        [StringLength(255)]
        public string? MotivoBloqueio { get; set; }

        [Column("privado")]
        public bool Privado { get; set; } = true;

        [Column("criado_rapidamente")]
        public bool CriadoRapidamente { get; set; }

        [Column("data_criacao")]
        public DateTime DataCriacao { get; set; }

        [Column("data_atualizacao")]
        public DateTime DataAtualizacao { get; set; }

        [ForeignKey(nameof(QuadroId))]
        public QuadroTarefa Quadro { get; set; } = null!;

        [ForeignKey(nameof(ColunaId))]
        public ColunaQuadro Coluna { get; set; } = null!;

        [ForeignKey(nameof(GrupoId))]
        public Grupo Grupo { get; set; } = null!;

        [ForeignKey(nameof(PaiCartaoId))]
        public CartaoTarefa? PaiCartao { get; set; }

        [ForeignKey(nameof(CriadorId))]
        public Usuario Criador { get; set; } = null!;

        [ForeignKey(nameof(ResponsavelUsuarioId))]
        public Usuario? ResponsavelUsuario { get; set; }
    }

    public class CartaoTarefaUsuario
    {
        [Column("cartao_tarefa_id")]
        public int CartaoTarefaId { get; set; }

        [Column("usuario_id")]
        public int UsuarioId { get; set; }

        [Column("tipo_participacao")]
        public TipoParticipacaoCartaoTarefa TipoParticipacao { get; set; } = TipoParticipacaoCartaoTarefa.Participante;

        [Column("permissao")]
        public PermissaoCartaoTarefa Permissao { get; set; } = PermissaoCartaoTarefa.Visualizador;

        [Column("data_adicao")]
        public DateTime DataAdicao { get; set; }

        [Column("adicionado_por_usuario_id")]
        public int AdicionadoPorUsuarioId { get; set; }

        [ForeignKey(nameof(CartaoTarefaId))]
        public CartaoTarefa CartaoTarefa { get; set; } = null!;

        [ForeignKey(nameof(UsuarioId))]
        public Usuario Usuario { get; set; } = null!;

        [ForeignKey(nameof(AdicionadoPorUsuarioId))]
        public Usuario AdicionadoPorUsuario { get; set; } = null!;
    }

    public class ComentarioTarefa
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("cartao_tarefa_id")]
        public int CartaoTarefaId { get; set; }

        [Column("usuario_id")]
        public int UsuarioId { get; set; }

        [Required]
        [Column("mensagem", TypeName = "varchar(250)")]
        [StringLength(250)]
        public string Mensagem { get; set; } = string.Empty;

        [Column("editado")]
        public bool Editado { get; set; }

        [Column("data_criacao")]
        public DateTime DataCriacao { get; set; }

        [Column("data_edicao")]
        public DateTime? DataEdicao { get; set; }

        [ForeignKey(nameof(CartaoTarefaId))]
        public CartaoTarefa CartaoTarefa { get; set; } = null!;

        [ForeignKey(nameof(UsuarioId))]
        public Usuario Usuario { get; set; } = null!;
    }

    public class AnexoTarefa
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("cartao_tarefa_id")]
        public int CartaoTarefaId { get; set; }

        [Column("usuario_id")]
        public int UsuarioId { get; set; }

        [Required]
        [Column("nome_original", TypeName = "varchar(255)")]
        [StringLength(255)]
        public string NomeOriginal { get; set; } = string.Empty;

        [Required]
        [Column("nome_arquivo", TypeName = "varchar(255)")]
        [StringLength(255)]
        public string NomeArquivo { get; set; } = string.Empty;

        [Required]
        [Column("caminho_arquivo", TypeName = "varchar(500)")]
        [StringLength(500)]
        public string CaminhoArquivo { get; set; } = string.Empty;

        [Column("tipo_arquivo", TypeName = "varchar(100)")]
        [StringLength(100)]
        public string? TipoArquivo { get; set; }

        [Column("extensao", TypeName = "varchar(20)")]
        [StringLength(20)]
        public string? Extensao { get; set; }

        [Column("tamanho_bytes")]
        public long? TamanhoBytes { get; set; }

        [Column("eh_imagem")]
        public bool EhImagem { get; set; }

        [Column("eh_capa")]
        public bool EhCapa { get; set; }

        [Column("data_upload")]
        public DateTime DataUpload { get; set; }

        [ForeignKey(nameof(CartaoTarefaId))]
        public CartaoTarefa CartaoTarefa { get; set; } = null!;

        [ForeignKey(nameof(UsuarioId))]
        public Usuario Usuario { get; set; } = null!;
    }

    public class ChecklistTarefa
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("cartao_tarefa_id")]
        public int CartaoTarefaId { get; set; }

        [Required]
        [Column("titulo", TypeName = "varchar(120)")]
        [StringLength(120)]
        public string Titulo { get; set; } = string.Empty;

        [Column("posicao", TypeName = "decimal(18,6)")]
        public decimal Posicao { get; set; }

        [Column("data_criacao")]
        public DateTime DataCriacao { get; set; }

        [ForeignKey(nameof(CartaoTarefaId))]
        public CartaoTarefa CartaoTarefa { get; set; } = null!;
    }

    public class ChecklistItemTarefa
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("checklist_id")]
        public int ChecklistId { get; set; }

        [Required]
        [Column("descricao", TypeName = "varchar(255)")]
        [StringLength(255)]
        public string Descricao { get; set; } = string.Empty;

        [Column("concluido")]
        public bool Concluido { get; set; }

        [Column("concluido_por_usuario_id")]
        public int? ConcluidoPorUsuarioId { get; set; }

        [Column("data_conclusao")]
        public DateTime? DataConclusao { get; set; }

        [Column("posicao", TypeName = "decimal(18,6)")]
        public decimal Posicao { get; set; }

        [ForeignKey(nameof(ChecklistId))]
        public ChecklistTarefa Checklist { get; set; } = null!;

        [ForeignKey(nameof(ConcluidoPorUsuarioId))]
        public Usuario? ConcluidoPorUsuario { get; set; }
    }

    public class EtiquetaTarefa
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("grupo_id")]
        public int GrupoId { get; set; }

        [Column("usuario_id")]
        public int UsuarioId { get; set; }

        [Required]
        [Column("nome", TypeName = "varchar(50)")]
        [StringLength(50)]
        public string Nome { get; set; } = string.Empty;

        [Required]
        [Column("cor", TypeName = "varchar(20)")]
        [StringLength(20)]
        public string Cor { get; set; } = string.Empty;

        [Column("data_criacao")]
        public DateTime DataCriacao { get; set; }

        [ForeignKey(nameof(GrupoId))]
        public Grupo Grupo { get; set; } = null!;

        [ForeignKey(nameof(UsuarioId))]
        public Usuario Usuario { get; set; } = null!;
    }

    public class CartaoTarefaEtiqueta
    {
        [Column("cartao_tarefa_id")]
        public int CartaoTarefaId { get; set; }

        [Column("etiqueta_id")]
        public int EtiquetaId { get; set; }

        [ForeignKey(nameof(CartaoTarefaId))]
        public CartaoTarefa CartaoTarefa { get; set; } = null!;

        [ForeignKey(nameof(EtiquetaId))]
        public EtiquetaTarefa Etiqueta { get; set; } = null!;
    }

    public class CartaoTarefaChamado
    {
        [Column("cartao_tarefa_id")]
        public int CartaoTarefaId { get; set; }

        [Column("chamado_id")]
        public int ChamadoId { get; set; }

        [Column("tipo_relacao")]
        public TipoRelacaoCartaoChamado TipoRelacao { get; set; } = TipoRelacaoCartaoChamado.Relacionada;

        [Column("ativo")]
        public bool Ativo { get; set; } = true;

        [Column("data_vinculo")]
        public DateTime DataVinculo { get; set; }

        [Column("vinculado_por_usuario_id")]
        public int VinculadoPorUsuarioId { get; set; }

        [Column("data_desvinculo")]
        public DateTime? DataDesvinculo { get; set; }

        [Column("desvinculado_por_usuario_id")]
        public int? DesvinculadoPorUsuarioId { get; set; }

        [ForeignKey(nameof(CartaoTarefaId))]
        public CartaoTarefa CartaoTarefa { get; set; } = null!;

        [ForeignKey(nameof(ChamadoId))]
        public Chamado Chamado { get; set; } = null!;

        [ForeignKey(nameof(VinculadoPorUsuarioId))]
        public Usuario VinculadoPorUsuario { get; set; } = null!;

        [ForeignKey(nameof(DesvinculadoPorUsuarioId))]
        public Usuario? DesvinculadoPorUsuario { get; set; }
    }

    public class HistoricoTarefa
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("cartao_tarefa_id")]
        public int CartaoTarefaId { get; set; }

        [Column("usuario_id")]
        public int UsuarioId { get; set; }

        [Required]
        [Column("tipo_acao", TypeName = "varchar(50)")]
        [StringLength(50)]
        public string TipoAcao { get; set; } = string.Empty;

        [Column("campo_alterado", TypeName = "varchar(100)")]
        [StringLength(100)]
        public string? CampoAlterado { get; set; }

        [Column("valor_anterior", TypeName = "text")]
        public string? ValorAnterior { get; set; }

        [Column("valor_novo", TypeName = "text")]
        public string? ValorNovo { get; set; }

        [Column("data_acao")]
        public DateTime DataAcao { get; set; }

        [ForeignKey(nameof(CartaoTarefaId))]
        public CartaoTarefa CartaoTarefa { get; set; } = null!;

        [ForeignKey(nameof(UsuarioId))]
        public Usuario Usuario { get; set; } = null!;
    }

    public class DependenciaTarefa
    {
        [Column("cartao_tarefa_id")]
        public int CartaoTarefaId { get; set; }

        [Column("cartao_dependente_id")]
        public int CartaoDependenteId { get; set; }

        [Column("tipo_dependencia")]
        public TipoDependenciaTarefa TipoDependencia { get; set; } = TipoDependenciaTarefa.Bloqueia;

        [ForeignKey(nameof(CartaoTarefaId))]
        public CartaoTarefa CartaoTarefa { get; set; } = null!;

        [ForeignKey(nameof(CartaoDependenteId))]
        public CartaoTarefa CartaoDependente { get; set; } = null!;
    }

    public enum PermissaoQuadroTarefa
    {
        Visualizador,
        Editor,
        Administrador
    }

    public enum StatusCartaoTarefa
    {
        Ativa,
        Pendente,
        Concluida,
        Arquivada,
        Cancelada
    }

    public class CartaoTarefaPeriodoPendente
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("cartao_tarefa_id")]
        public int CartaoTarefaId { get; set; }

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

        [ForeignKey(nameof(CartaoTarefaId))]
        public CartaoTarefa CartaoTarefa { get; set; } = null!;

        [ForeignKey(nameof(CriadoPorUsuarioId))]
        public Usuario CriadoPorUsuario { get; set; } = null!;

        [ForeignKey(nameof(FinalizadoPorUsuarioId))]
        public Usuario? FinalizadoPorUsuario { get; set; }
    }

    public enum TipoParticipacaoCartaoTarefa
    {
        Participante,
        Observador
    }

    public enum PermissaoCartaoTarefa
    {
        Visualizador,
        Editor
    }

    public enum TipoRelacaoCartaoChamado
    {
        Origem,
        Relacionada,
        BloqueadaPor,
        ResolveChamado
    }

    public enum TipoDependenciaTarefa
    {
        Bloqueia,
        Relacionada
    }
}
