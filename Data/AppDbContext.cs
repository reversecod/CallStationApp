using CallStationApp.Models;
using Microsoft.EntityFrameworkCore;

namespace CallStationApp.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options)
        {
        }

        // DbSets
        public DbSet<Usuario> Usuarios { get; set; }
        public DbSet<Grupo> Grupos { get; set; }
        public DbSet<GrupoConfiguracao> GruposConfiguracoes { get; set; }
        public DbSet<GrupoTipoChamado> GruposTiposChamados { get; set; }
        public DbSet<GrupoAuditoria> GruposAuditorias { get; set; }
        public DbSet<UsuarioGrupo> UsuariosGrupos { get; set; }
        public DbSet<InfoUsuarioGrupo> InfoUsuariosGrupos { get; set; }
        public DbSet<OcorrenciaTipo> OcorrenciasTipo { get; set; }
        public DbSet<Setor> Setores { get; set; }
        public DbSet<OcorrenciaCategoria> OcorrenciasCategoria { get; set; }
        public DbSet<OcorrenciaSubcategoria> OcorrenciasSubcategoria { get; set; }
        public DbSet<Chamado> Chamados { get; set; }
        public DbSet<ChamadoVinculo> ChamadosVinculos { get; set; }
        public DbSet<HistoricoStatusChamado> HistoricoStatusChamados { get; set; }
        public DbSet<ComentarioChamado> ComentariosChamados { get; set; }
        public DbSet<QuadroTarefa> QuadrosTarefas { get; set; }
        public DbSet<QuadroTarefaUsuario> QuadrosTarefasUsuarios { get; set; }
        public DbSet<ColunaQuadro> ColunasQuadro { get; set; }
        public DbSet<CartaoTarefaContadorGrupo> CartaoTarefaContadorGrupo { get; set; }
        public DbSet<TemplateCartaoTarefa> TemplatesCartoesTarefas { get; set; }
        public DbSet<CartaoTarefa> CartoesTarefas { get; set; }
        public DbSet<CartaoTarefaUsuario> CartoesTarefasUsuarios { get; set; }
        public DbSet<ComentarioTarefa> ComentariosTarefas { get; set; }
        public DbSet<AnexoTarefa> AnexosTarefas { get; set; }
        public DbSet<ChecklistTarefa> ChecklistsTarefas { get; set; }
        public DbSet<ChecklistItemTarefa> ChecklistItensTarefas { get; set; }
        public DbSet<EtiquetaTarefa> EtiquetasTarefas { get; set; }
        public DbSet<CartaoTarefaEtiqueta> CartoesTarefasEtiquetas { get; set; }
        public DbSet<CartaoTarefaChamado> CartoesTarefasChamados { get; set; }
        public DbSet<HistoricoTarefa> HistoricoTarefas { get; set; }
        public DbSet<DependenciaTarefa> DependenciasTarefas { get; set; }
        public DbSet<FeedbackChamado> FeedbacksChamados { get; set; }
        public DbSet<ConviteGrupo> ConvitesGrupo { get; set; }
        public DbSet<Notificacao> Notificacoes { get; set; }
        public DbSet<MencaoTexto> MencoesTextos { get; set; }
        public DbSet<HistoricoAlteracaoChamado> HistoricoAlteracoesChamado { get; set; }
        public DbSet<ChamadoContadorGrupo> ChamadosContadorGrupo { get; set; }
        public DbSet<ChamadoContadorUsuario> ChamadosContadorUsuario { get; set; }
        public DbSet<ChamadoContadorUsuarioGrupo> ChamadosContadorUsuarioGrupo { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // ===== Usuário =====
            modelBuilder.Entity<Usuario>(entity =>
            {
                entity.ToTable("Usuarios");

                entity.HasIndex(u => u.NomeUsuario).IsUnique();
                entity.HasIndex(u => u.Email).IsUnique();

                entity.Property(u => u.NomeCompleto)
                    .IsRequired()
                    .HasColumnName("nome_completo")
                    .HasColumnType("varchar(100)")
                    .HasMaxLength(100);

                entity.Property(u => u.NomeUsuario)
                    .IsRequired()
                    .HasColumnName("nome_usuario")
                    .HasColumnType("varchar(20)")
                    .HasMaxLength(20);

                entity.Property(u => u.Email)
                    .HasColumnType("varchar(100)")
                    .HasMaxLength(100);

                entity.Property(u => u.Senha)
                    .IsRequired()
                    .HasColumnType("varchar(255)")
                    .HasMaxLength(255);

                entity.Property(u => u.FotoUsuario)
                    .HasColumnName("foto_usuario")
                    .HasColumnType("varchar(255)")
                    .HasMaxLength(255);

                entity.Property(u => u.ModoEscuro)
                    .HasColumnName("modo_escuro")
                    .HasColumnType("boolean")
                    .HasDefaultValue(false);

                entity.Property(u => u.DataCriacao)
                    .HasColumnName("data_criacao")
                    .HasColumnType("datetime")
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");
            });

            // ===== Grupo =====
            modelBuilder.Entity<Grupo>(entity =>
            {
                entity.ToTable("Grupos");

                entity.Property(g => g.Nome)
                    .IsRequired()
                    .HasColumnType("varchar(35)")
                    .HasMaxLength(35);

                entity.Property(g => g.DescricaoGrupo)
                    .HasColumnName("descricao_grupo")
                    .HasColumnType("varchar(200)")
                    .HasMaxLength(200);

                entity.Property(g => g.FotoGrupo)
                    .HasColumnName("foto_grupo")
                    .HasColumnType("varchar(255)")
                    .HasMaxLength(255);

                // ✅ ENUM mapeado corretamente
                entity.Property(g => g.EtiquetaCor)
                    .HasColumnName("etiqueta_cor")
                    .HasConversion<string>()
                    .HasColumnType(
                        "ENUM('branco','vermelho','laranja','amarelo','verde','azul','roxo','rosa','preto')"
                    );

                entity.Property(g => g.DataCriacao)
                    .HasColumnName("data_criacao")
                    .HasColumnType("datetime")
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");

                entity.HasIndex(g => new { g.Nome, g.CriadorId })
                    .IsUnique();
            });

            // ===== GrupoConfiguracao =====
            modelBuilder.Entity<GrupoConfiguracao>(entity =>
            {
                entity.ToTable("Grupos_configuracoes");

                entity.HasKey(c => c.GrupoId);

                entity.Property(c => c.Slug)
                    .HasColumnType("varchar(60)")
                    .HasMaxLength(60);

                entity.Property(c => c.Ativo)
                    .HasColumnType("boolean")
                    .HasDefaultValue(true);

                entity.Property(c => c.ObrigarSetor)
                    .HasColumnName("obrigar_setor")
                    .HasColumnType("boolean")
                    .HasDefaultValue(false);

                entity.Property(c => c.ObrigarTipoOcorrencia)
                    .HasColumnName("obrigar_tipo_ocorrencia")
                    .HasColumnType("boolean")
                    .HasDefaultValue(false);

                entity.Property(c => c.ObrigarCategoria)
                    .HasColumnName("obrigar_categoria")
                    .HasColumnType("boolean")
                    .HasDefaultValue(false);

                entity.Property(c => c.ObrigarSubcategoria)
                    .HasColumnName("obrigar_subcategoria")
                    .HasColumnType("boolean")
                    .HasDefaultValue(false);

                entity.Property(c => c.PermitirChamadoPublico)
                    .HasColumnName("permitir_chamado_publico")
                    .HasColumnType("boolean")
                    .HasDefaultValue(true);

                entity.Property(c => c.ExigirSolucaoParaConcluir)
                    .HasColumnName("exigir_solucao_para_concluir")
                    .HasColumnType("boolean")
                    .HasDefaultValue(false);

                entity.Property(c => c.AutomatizarPendentePorPrazoConclusao)
                    .HasColumnName("automatizar_pendente_por_prazo_conclusao")
                    .HasColumnType("boolean")
                    .HasDefaultValue(false);

                entity.Property(c => c.NotificarAdministradoresSla)
                    .HasColumnName("notificar_administradores_sla")
                    .HasColumnType("boolean")
                    .HasDefaultValue(true);

                entity.Property(c => c.ExibirDataFinalizacaoModal)
                    .HasColumnName("exibir_data_finalizacao_modal")
                    .HasColumnType("boolean")
                    .HasDefaultValue(true);

                entity.Property(c => c.ExibirPrazoRespostaModal)
                    .HasColumnName("exibir_prazo_resposta_modal")
                    .HasColumnType("boolean")
                    .HasDefaultValue(true);

                entity.Property(c => c.ExibirPrazoConclusaoModal)
                    .HasColumnName("exibir_prazo_conclusao_modal")
                    .HasColumnType("boolean")
                    .HasDefaultValue(true);

                entity.Property(c => c.AparenciaTelaTipo)
                    .HasColumnName("aparencia_tela_tipo")
                    .HasColumnType("varchar(20)")
                    .HasMaxLength(20);

                entity.Property(c => c.AparenciaTelaValor)
                    .HasColumnName("aparencia_tela_valor")
                    .HasColumnType("varchar(255)")
                    .HasMaxLength(255);

                entity.Property(c => c.AparenciaSidebarTipo)
                    .HasColumnName("aparencia_sidebar_tipo")
                    .HasColumnType("varchar(20)")
                    .HasMaxLength(20);

                entity.Property(c => c.AparenciaSidebarValor)
                    .HasColumnName("aparencia_sidebar_valor")
                    .HasColumnType("varchar(255)")
                    .HasMaxLength(255);

                entity.Property(c => c.AparenciaMenuAtivoCor)
                    .HasColumnName("aparencia_menu_ativo_cor")
                    .HasColumnType("varchar(7)")
                    .HasMaxLength(7);

                entity.Property(c => c.AparenciaSidebarTextoFundoCor)
                    .HasColumnName("aparencia_sidebar_texto_fundo_cor")
                    .HasColumnType("varchar(7)")
                    .HasMaxLength(7);

                entity.Property(c => c.DataAtualizacao)
                    .HasColumnName("data_atualizacao")
                    .HasColumnType("datetime")
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");

                entity.HasIndex(c => c.Slug)
                    .IsUnique()
                    .HasDatabaseName("uq_grupos_configuracoes_slug");

                entity.HasOne(c => c.Grupo)
                    .WithMany()
                    .HasForeignKey(c => c.GrupoId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // ===== GrupoTipoChamado =====
            modelBuilder.Entity<GrupoTipoChamado>(entity =>
            {
                entity.ToTable("Grupos_tipos_chamados");

                entity.Property(t => t.Nome)
                    .IsRequired()
                    .HasColumnType("varchar(50)")
                    .HasMaxLength(50);

                entity.Property(t => t.Descricao)
                    .HasColumnType("varchar(160)")
                    .HasMaxLength(160);

                entity.Property(t => t.Ativo)
                    .HasColumnType("boolean")
                    .HasDefaultValue(true);

                entity.Property(t => t.DataCriacao)
                    .HasColumnName("data_criacao")
                    .HasColumnType("datetime")
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");

                entity.Property(t => t.DataArquivamento)
                    .HasColumnName("data_arquivamento")
                    .HasColumnType("datetime");

                entity.HasIndex(t => new { t.GrupoId, t.Nome })
                    .IsUnique()
                    .HasDatabaseName("uq_grupos_tipos_chamados_grupo_nome");

                entity.HasIndex(t => new { t.GrupoId, t.Ativo, t.Posicao })
                    .HasDatabaseName("idx_grupos_tipos_chamados_grupo_ativo_posicao");

                entity.HasOne(t => t.Grupo)
                    .WithMany()
                    .HasForeignKey(t => t.GrupoId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // ===== GrupoAuditoria =====
            modelBuilder.Entity<GrupoAuditoria>(entity =>
            {
                entity.ToTable("Grupos_auditorias");

                entity.Property(a => a.TipoAcao)
                    .IsRequired()
                    .HasColumnType("varchar(50)")
                    .HasMaxLength(50);

                entity.Property(a => a.Entidade)
                    .IsRequired()
                    .HasColumnType("varchar(50)")
                    .HasMaxLength(50);

                entity.Property(a => a.CampoAlterado)
                    .HasColumnName("campo_alterado")
                    .HasColumnType("varchar(80)")
                    .HasMaxLength(80);

                entity.Property(a => a.ValorAnterior)
                    .HasColumnName("valor_anterior")
                    .HasColumnType("varchar(500)")
                    .HasMaxLength(500);

                entity.Property(a => a.ValorNovo)
                    .HasColumnName("valor_novo")
                    .HasColumnType("varchar(500)")
                    .HasMaxLength(500);

                entity.Property(a => a.IpOrigem)
                    .HasColumnName("ip_origem")
                    .HasColumnType("varchar(64)")
                    .HasMaxLength(64);

                entity.Property(a => a.UserAgent)
                    .HasColumnName("user_agent")
                    .HasColumnType("varchar(255)")
                    .HasMaxLength(255);

                entity.Property(a => a.DataAcao)
                    .HasColumnName("data_acao")
                    .HasColumnType("datetime")
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");

                entity.HasIndex(a => new { a.GrupoId, a.DataAcao })
                    .HasDatabaseName("idx_grupos_auditorias_grupo_data");

                entity.HasIndex(a => new { a.GrupoId, a.TipoAcao })
                    .HasDatabaseName("idx_grupos_auditorias_grupo_acao");

                entity.HasOne(a => a.Grupo)
                    .WithMany()
                    .HasForeignKey(a => a.GrupoId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(a => a.Usuario)
                    .WithMany()
                    .HasForeignKey(a => a.UsuarioId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // ===== UsuarioGrupo =====
            modelBuilder.Entity<UsuarioGrupo>(entity =>
            {
                entity.ToTable("Usuarios_grupos");

                entity.HasKey(ug => new { ug.UsuarioId, ug.GrupoId });

                entity.Property(ug => ug.Permissao)
                    .IsRequired()
                    .HasConversion<string>()
                    .HasColumnType("ENUM('Nenhuma','Colaborador','Tecnico','Administracao')")
                    .HasDefaultValue(PermissaoUsuario.Nenhuma);

                entity.Property(ug => ug.DataAdicao)
                    .HasColumnName("data_adicao")
                    .HasColumnType("datetime")
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");

                entity.Property(ug => ug.DataUltimoAcesso)
                    .HasColumnName("data_ultimo_acesso")
                    .HasColumnType("datetime");

                entity.Property(ug => ug.GrupoFixado)
                    .HasColumnName("grupo_fixado")
                    .HasColumnType("boolean")
                    .HasDefaultValue(false);

                entity.Property(ug => ug.OrdemGrupoFixado)
                    .HasColumnName("ordem_grupo_fixado");

                entity.Property(ug => ug.Ativo)
                    .HasColumnName("ativo")
                    .HasColumnType("boolean")
                    .HasDefaultValue(true);

                entity.Property(ug => ug.DataRemocao)
                    .HasColumnName("data_remocao")
                    .HasColumnType("datetime");

                entity.Property(ug => ug.RemovidoPorUsuarioId)
                    .HasColumnName("removido_por_usuario_id");

                entity.HasOne(ug => ug.RemovidoPorUsuario)
                    .WithMany()
                    .HasForeignKey(ug => ug.RemovidoPorUsuarioId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(ug => new { ug.GrupoId, ug.Ativo })
                    .HasDatabaseName("idx_ug_grupo_ativo");

                entity.HasIndex(ug => new { ug.UsuarioId, ug.Ativo })
                    .HasDatabaseName("idx_ug_usuario_ativo");

                entity.HasIndex(ug => new { ug.UsuarioId, ug.Ativo, ug.DataUltimoAcesso })
                    .HasDatabaseName("idx_ug_usuario_ativo_ultimo_acesso");

                entity.HasIndex(ug => new { ug.UsuarioId, ug.Ativo, ug.GrupoFixado, ug.OrdemGrupoFixado })
                    .HasDatabaseName("idx_ug_usuario_fixados_ordem");

                entity.HasIndex(ug => new { ug.UsuarioId, ug.GrupoId, ug.Ativo })
                    .HasDatabaseName("idx_ug_usuario_grupo_ativo");

                entity.HasIndex(ug => new { ug.GrupoId, ug.Ativo, ug.Permissao })
                    .HasDatabaseName("idx_ug_grupo_ativo_permissao");
            });

            // ===== InfoUsuarioGrupo =====
            modelBuilder.Entity<InfoUsuarioGrupo>(entity =>
            {
                entity.ToTable("Info_usuarios_grupos");

                entity.HasKey(iu => new { iu.UsuarioId, iu.GrupoId });

                entity.HasOne<UsuarioGrupo>()
                    .WithMany()
                    .HasForeignKey(iu => new { iu.UsuarioId, iu.GrupoId });

                entity.Property(iu => iu.Apelido)
                    .HasColumnType("varchar(35)")
                    .HasMaxLength(35);

                entity.Property(iu => iu.DescricaoAtivo)
                    .HasColumnName("descricao_ativo")
                    .HasColumnType("varchar(500)")
                    .HasMaxLength(500);

                entity.Property(iu => iu.IdentificadorInterno)
                    .HasColumnName("identificador_interno")
                    .HasColumnType("varchar(50)")
                    .HasMaxLength(50);

                entity.Property(iu => iu.Observacao)
                    .HasColumnName("observacao")
                    .HasColumnType("varchar(500)")
                    .HasMaxLength(500);

                entity.Property(iu => iu.DataAtualizacaoAtivo)
                    .HasColumnName("data_atualizacao_ativo")
                    .HasColumnType("datetime");

                entity.Property(iu => iu.DataAtualizacaoRegistro)
                    .IsRequired()
                    .HasColumnName("data_atualizacao_registro")
                    .HasColumnType("datetime")
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");

                entity.HasIndex(iu => new { iu.UsuarioId, iu.GrupoId })
                    .IsUnique();
            });

            // ===== OcorrenciaTipo =====
            modelBuilder.Entity<OcorrenciaTipo>(entity =>
            {
                entity.ToTable("Ocorrencias_tipo");

                entity.Property(ot => ot.TipoOcorrencia)
                    .IsRequired()
                    .HasColumnName("tipo_ocorrencia")
                    .HasColumnType("varchar(50)")
                    .HasMaxLength(50);

                entity.Property(ot => ot.GrupoId)
                    .IsRequired();

                entity.HasOne<UsuarioGrupo>()
                    .WithMany()
                    .HasForeignKey(ot => new { ot.UsuarioId, ot.GrupoId });

                entity.HasIndex(ot => new { ot.TipoOcorrencia, ot.GrupoId })
                    .IsUnique();
            });

            // ===== Setor =====
            modelBuilder.Entity<Setor>().ToTable("Setores");

            modelBuilder.Entity<Setor>(entity =>
            {
                entity.Property(s => s.NomeSetor)
                    .IsRequired()
                    .HasColumnName("nome_setor")
                    .HasColumnType("varchar(50)")
                    .HasMaxLength(50);

                entity.Property(s => s.GrupoId)
                    .IsRequired();

                entity.HasOne<UsuarioGrupo>()
                    .WithMany()
                    .HasForeignKey(s => new { s.UsuarioId, s.GrupoId });

                entity.HasIndex(s => new { s.NomeSetor, s.GrupoId })
                    .IsUnique();
            });

            // ===== OcorrenciaCategoria =====
            modelBuilder.Entity<OcorrenciaCategoria>(entity =>
            {
                entity.ToTable("Ocorrencias_categoria");

                entity.Property(ot => ot.CategoriaOcorrencia)
                    .IsRequired()
                    .HasColumnName("categoria_ocorrencia")
                    .HasColumnType("varchar(50)")
                    .HasMaxLength(50);

                entity.HasIndex(ot => new { ot.TipoId, ot.CategoriaOcorrencia })
                    .IsUnique();
            });

            // ===== OcorrenciaSubcategoria =====
            modelBuilder.Entity<OcorrenciaSubcategoria>(entity =>
            {
                entity.ToTable("Ocorrencias_subcategoria");

                entity.Property(ot => ot.SubcategoriaOcorrencia)
                    .IsRequired()
                    .HasColumnName("subcategoria_ocorrencia")
                    .HasColumnType("varchar(100)")
                    .HasMaxLength(100);

                entity.HasIndex(ot => new { ot.CategoriaId, ot.SubcategoriaOcorrencia })
                    .IsUnique();
            });

            // ===== Chamado =====
            modelBuilder.Entity<Chamado>(entity =>
            {
                entity.ToTable("Chamados");

                // ===== CAMPOS BÁSICOS =====
                entity.Property(c => c.Titulo)
                    .HasColumnType("varchar(35)")
                    .HasMaxLength(35);

                entity.Property(c => c.Descricao)
                    .HasColumnType("varchar(500)")
                    .HasMaxLength(500);

                entity.Property(c => c.Solucao)
                    .HasColumnType("varchar(500)")
                    .HasMaxLength(500);

                entity.Property(c => c.AnexoChamado)
                    .HasColumnName("anexo_chamado")
                    .HasColumnType("varchar(255)")
                    .HasMaxLength(255);

                // ===== ENUMS =====
                entity.Property(c => c.Prioridade)
                    .HasConversion<string>()
                    .HasColumnType("ENUM('Baixa','Media','Alta','Critica')");

                entity.Property(c => c.Criticidade)
                    .HasConversion<string>()
                    .HasColumnType("ENUM('Baixa','Media','Alta','Critico')");

                entity.Property(c => c.Urgencia)
                    .HasConversion<string>()
                    .HasColumnType("ENUM('NaoUrgente','PoucaUrgencia','Urgente','Emergencia')");

                entity.Property(c => c.Status)
                    .HasConversion<string>()
                    .HasColumnType("ENUM('Aberto','EmAndamento','Pendente','Concluido','Fechado','Reaberto','Cancelado','Excluido')")
                    .HasDefaultValue(StatusChamado.Aberto);

                // ===== DATAS =====
                entity.Property(c => c.DataCriacao)
                    .HasColumnName("data_criacao")
                    .HasColumnType("datetime")
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");

                entity.Property(c => c.DataFinalizacao)
                    .HasColumnName("data_finalizacao")
                    .HasColumnType("datetime");

                entity.Property(c => c.PrazoResposta)
                    .HasColumnName("prazo_resposta")
                    .HasColumnType("datetime");

                entity.Property(c => c.PrazoConclusao)
                    .HasColumnName("prazo_conclusao")
                    .HasColumnType("datetime");

                entity.Property(c => c.Publico)
                    .HasColumnType("boolean")
                    .HasDefaultValue(false);

                // ===== CAMPOS OBRIGATÓRIOS DO FLUXO =====
                entity.Property(c => c.GrupoId)
                    .IsRequired();

                entity.Property(c => c.CriadorChamadoId)
                    .IsRequired();

                // ===== IDENTIFICADORES DE NEGÓCIO =====
                entity.Property(c => c.NumeroChamadoGrupo)
                    .IsRequired()
                    .HasColumnName("numero_chamado_grupo");

                entity.Property(c => c.NumeroChamadoUsuario)
                    .IsRequired()
                    .HasColumnName("numero_chamado_usuario");

                entity.Property(c => c.NumeroChamadoUsuarioGrupo)
                    .IsRequired()
                    .HasColumnName("numero_chamado_usuario_grupo");

                // ===== FK COMPOSTA (REGRA DO SISTEMA) =====
                entity.HasOne<Usuario>()
                    .WithMany()
                    .HasForeignKey(c => c.CriadorChamadoId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne<Grupo>()
                    .WithMany()
                    .HasForeignKey(c => c.GrupoId)
                    .OnDelete(DeleteBehavior.Restrict);

                // ===== ÍNDICES =====
                entity.HasIndex(c => c.GrupoId).HasDatabaseName("idx_chamados_grupo");
                entity.HasIndex(c => c.Status).HasDatabaseName("idx_chamados_status");
                entity.HasIndex(c => c.CriadorChamadoId).HasDatabaseName("idx_chamados_criador");
                entity.HasIndex(c => c.DataCriacao).HasDatabaseName("idx_chamados_data_criacao");
                entity.HasIndex(c => new { c.GrupoId, c.Status, c.DataCriacao })
                    .HasDatabaseName("idx_chamados_grupo_status_data");
                entity.HasIndex(c => new { c.GrupoId, c.Status, c.PrazoConclusao })
                    .HasDatabaseName("idx_chamados_grupo_status_prazo_conclusao");
                entity.HasIndex(c => new { c.GrupoId, c.Publico, c.DataCriacao })
                    .HasDatabaseName("idx_chamados_grupo_publico_data");
                entity.HasIndex(c => new { c.GrupoId, c.CriadorChamadoId, c.DataCriacao })
                    .HasDatabaseName("idx_chamados_grupo_criador_data");

                entity.HasIndex(c => new { c.GrupoId, c.NumeroChamadoGrupo })
                    .IsUnique()
                    .HasDatabaseName("ux_chamado_grupo_numero");

                entity.HasIndex(c => new { c.CriadorChamadoId, c.NumeroChamadoUsuario })
                    .IsUnique()
                    .HasDatabaseName("ux_chamado_usuario_numero");

                entity.HasIndex(c => new { c.CriadorChamadoId, c.GrupoId, c.NumeroChamadoUsuarioGrupo })
                    .IsUnique()
                    .HasDatabaseName("ux_chamado_usuario_grupo_numero");
            });

            modelBuilder.Entity<ChamadoVinculo>(entity =>
            {
                entity.ToTable("Chamados_vinculos");
                entity.HasKey(v => new { v.ChamadoIdMenor, v.ChamadoIdMaior });
                entity.ToTable(t => t.HasCheckConstraint("CK_chamados_vinculos_ordem", "chamado_id_menor < chamado_id_maior"));

                entity.Property(v => v.GrupoId)
                    .HasColumnName("grupo_id")
                    .IsRequired();

                entity.Property(v => v.DataVinculo)
                    .HasColumnName("data_vinculo")
                    .HasColumnType("datetime")
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");

                entity.Property(v => v.VinculadoPorUsuarioId)
                    .HasColumnName("vinculado_por_usuario_id")
                    .IsRequired();

                entity.HasIndex(v => new { v.GrupoId, v.ChamadoIdMenor })
                    .HasDatabaseName("idx_chamados_vinculos_grupo_menor");

                entity.HasIndex(v => new { v.GrupoId, v.ChamadoIdMaior })
                    .HasDatabaseName("idx_chamados_vinculos_grupo_maior");

                entity.HasOne(v => v.ChamadoMenor)
                    .WithMany()
                    .HasForeignKey(v => v.ChamadoIdMenor)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(v => v.ChamadoMaior)
                    .WithMany()
                    .HasForeignKey(v => v.ChamadoIdMaior)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(v => v.Grupo)
                    .WithMany()
                    .HasForeignKey(v => v.GrupoId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(v => v.VinculadoPorUsuario)
                    .WithMany()
                    .HasForeignKey(v => v.VinculadoPorUsuarioId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // ===== HistoricoStatusChamado =====
            modelBuilder.Entity<HistoricoStatusChamado>(entity =>
            {
                entity.ToTable("Historico_status_chamados");

                entity.Property(h => h.StatusAnterior)
                    .IsRequired()
                    .HasConversion<string>()
                    .HasColumnType("ENUM('Aberto','EmAndamento','Pendente','Concluido','Fechado','Reaberto','Cancelado','Excluido')");

                entity.Property(h => h.StatusNovo)
                    .IsRequired()
                    .HasConversion<string>()
                    .HasColumnType("ENUM('Aberto','EmAndamento','Pendente','Concluido','Fechado','Reaberto','Cancelado','Excluido')");

                entity.Property(h => h.DataTransicao)
                    .HasColumnName("data_transicao")
                    .HasColumnType("datetime")
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");

                entity.Property(h => h.OrigemAutomatica)
                    .HasColumnName("origem_automatica")
                    .HasColumnType("boolean")
                    .HasDefaultValue(false);

                entity.Property(h => h.DescricaoOrigem)
                    .HasColumnName("descricao_origem")
                    .HasColumnType("varchar(100)")
                    .HasMaxLength(100);

                entity.HasIndex(h => new { h.ChamadoId }).HasDatabaseName("idx_historico_chamado");

                entity.HasIndex(h => new { h.ChamadoId, h.DataTransicao })
                    .HasDatabaseName("idx_historico_chamado_data");

                entity.HasOne(h => h.Usuario)
                    .WithMany()
                    .HasForeignKey(h => h.UsuarioId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // ===== ComentarioChamado =====
            modelBuilder.Entity<ComentarioChamado>(entity =>
            {
                entity.ToTable("Comentarios_chamados");

                entity.Property(cc => cc.Mensagem)
                    .IsRequired()
                    .HasColumnType("varchar(250)")
                    .HasMaxLength(250);

                entity.Property(cc => cc.AnexoComentario)
                    .HasColumnName("anexo_comentario")
                    .HasColumnType("varchar(255)")
                    .HasMaxLength(255);

                entity.Property(cc => cc.DataComentario)
                    .HasColumnName("data_comentario")
                    .HasColumnType("datetime")
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");

                entity.HasIndex(c => new { c.ChamadoId, c.DataComentario }).HasDatabaseName("idx_comentario_chamado_data");
                entity.HasIndex(c => new { c.UsuarioId, c.DataComentario }).HasDatabaseName("idx_comentario_usuario_data");
            });

            // ===== Quadros e tarefas =====
            modelBuilder.Entity<QuadroTarefa>(entity =>
            {
                entity.ToTable("Quadros_tarefas");

                entity.Property(q => q.Nome).IsRequired().HasColumnType("varchar(100)").HasMaxLength(100);
                entity.Property(q => q.Descricao).HasColumnType("varchar(300)").HasMaxLength(300);
                entity.Property(q => q.Cor).HasColumnType("varchar(20)").HasMaxLength(20);
                entity.Property(q => q.Ativo).HasColumnType("boolean").HasDefaultValue(true);
                entity.Property(q => q.DataCriacao).HasColumnName("data_criacao").HasColumnType("datetime").HasDefaultValueSql("CURRENT_TIMESTAMP");

                entity.HasIndex(q => new { q.GrupoId, q.Nome }).IsUnique().HasDatabaseName("uq_quadros_tarefas_grupo_nome");
                entity.HasIndex(q => new { q.GrupoId, q.Ativo }).HasDatabaseName("idx_quadros_tarefas_grupo_ativo");

                entity.HasOne(q => q.Grupo).WithMany().HasForeignKey(q => q.GrupoId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(q => q.CriadoPorUsuario).WithMany().HasForeignKey(q => q.CriadoPorUsuarioId).OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<QuadroTarefaUsuario>(entity =>
            {
                entity.ToTable("Quadros_tarefas_usuarios");
                entity.HasKey(q => new { q.QuadroId, q.UsuarioId });

                entity.Property(q => q.Permissao)
                    .HasConversion<string>()
                    .HasColumnType("ENUM('Visualizador','Editor','Administrador')")
                    .HasDefaultValue(PermissaoQuadroTarefa.Visualizador);

                entity.Property(q => q.DataAdicao).HasColumnName("data_adicao").HasColumnType("datetime").HasDefaultValueSql("CURRENT_TIMESTAMP");

                entity.HasIndex(q => new { q.UsuarioId, q.Permissao }).HasDatabaseName("idx_quadros_tarefas_usuarios_usuario_permissao");

                entity.HasOne(q => q.Quadro).WithMany().HasForeignKey(q => q.QuadroId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(q => q.Usuario).WithMany().HasForeignKey(q => q.UsuarioId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(q => q.AdicionadoPorUsuario).WithMany().HasForeignKey(q => q.AdicionadoPorUsuarioId).OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<ColunaQuadro>(entity =>
            {
                entity.ToTable("Colunas_quadro");

                entity.Property(c => c.Nome).IsRequired().HasColumnType("varchar(60)").HasMaxLength(60);
                entity.Property(c => c.Posicao).HasPrecision(18, 6);
                entity.Property(c => c.Cor).HasColumnType("varchar(20)").HasMaxLength(20);
                entity.Property(c => c.Ativa).HasColumnType("boolean").HasDefaultValue(true);
                entity.Property(c => c.DataCriacao).HasColumnName("data_criacao").HasColumnType("datetime").HasDefaultValueSql("CURRENT_TIMESTAMP");

                entity.HasIndex(c => new { c.QuadroId, c.Posicao }).IsUnique().HasDatabaseName("uq_colunas_quadro_posicao");
                entity.HasIndex(c => new { c.QuadroId, c.Nome }).IsUnique().HasDatabaseName("uq_colunas_quadro_nome");
                entity.HasIndex(c => new { c.QuadroId, c.Ativa, c.Posicao }).HasDatabaseName("idx_colunas_quadro_quadro_ativa_posicao");

                entity.HasOne(c => c.Quadro).WithMany().HasForeignKey(c => c.QuadroId).OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<CartaoTarefaContadorGrupo>(entity =>
            {
                entity.ToTable("Cartao_tarefa_contador_grupo");
                entity.HasKey(c => c.GrupoId);
                entity.Property(c => c.UltimoNumero).HasColumnName("ultimo_numero").HasDefaultValue(0);
                entity.HasOne(c => c.Grupo).WithMany().HasForeignKey(c => c.GrupoId).OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<TemplateCartaoTarefa>(entity =>
            {
                entity.ToTable("Templates_cartoes_tarefas");
                entity.Property(t => t.Nome).IsRequired().HasColumnType("varchar(100)").HasMaxLength(100);
                entity.Property(t => t.Descricao).HasColumnType("text");
                entity.Property(t => t.Prioridade).HasConversion<string>().HasColumnType("ENUM('Baixa','Media','Alta','Critica')");
                entity.Property(t => t.Criticidade).HasConversion<string>().HasColumnType("ENUM('Baixa','Media','Alta','Critico')");
                entity.Property(t => t.Urgencia).HasConversion<string>().HasColumnType("ENUM('NaoUrgente','PoucaUrgencia','Urgente','Emergencia')");
                entity.Property(t => t.CorCapa).HasColumnName("cor_capa").HasColumnType("varchar(20)").HasMaxLength(20);
                entity.Property(t => t.DataCriacao).HasColumnName("data_criacao").HasColumnType("datetime").HasDefaultValueSql("CURRENT_TIMESTAMP");
                entity.Property(t => t.DataAtualizacao).HasColumnName("data_atualizacao").HasColumnType("datetime").HasDefaultValueSql("CURRENT_TIMESTAMP");
                entity.Property(t => t.Ativo).HasColumnType("boolean").HasDefaultValue(true);
                entity.HasIndex(t => new { t.GrupoId, t.Nome }).IsUnique().HasDatabaseName("uq_templates_cartoes_tarefas_grupo_nome");
                entity.HasIndex(t => new { t.GrupoId, t.Ativo }).HasDatabaseName("idx_templates_cartoes_tarefas_grupo_ativo");
                entity.HasOne(t => t.Grupo).WithMany().HasForeignKey(t => t.GrupoId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(t => t.CriadoPorUsuario).WithMany().HasForeignKey(t => t.CriadoPorUsuarioId).OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<CartaoTarefa>(entity =>
            {
                entity.ToTable("Cartoes_tarefas");

                entity.Property(c => c.Titulo).IsRequired().HasColumnType("varchar(150)").HasMaxLength(150);
                entity.Property(c => c.Descricao).HasColumnType("text");
                entity.Property(c => c.Prioridade).HasConversion<string>().HasColumnType("ENUM('Baixa','Media','Alta','Critica')");
                entity.Property(c => c.Criticidade).HasConversion<string>().HasColumnType("ENUM('Baixa','Media','Alta','Critico')");
                entity.Property(c => c.Urgencia).HasConversion<string>().HasColumnType("ENUM('NaoUrgente','PoucaUrgencia','Urgente','Emergencia')");
                entity.Property(c => c.Status).HasConversion<string>().HasColumnType("ENUM('Ativa','Concluida','Arquivada','Cancelada')").HasDefaultValue(StatusCartaoTarefa.Ativa);
                entity.Property(c => c.CorCapa).HasColumnName("cor_capa").HasColumnType("varchar(20)").HasMaxLength(20);
                entity.Property(c => c.ImagemCapa).HasColumnName("imagem_capa").HasColumnType("varchar(255)").HasMaxLength(255);
                entity.Property(c => c.OrdemColuna).HasColumnName("ordem_coluna").HasPrecision(18, 6);
                entity.Property(c => c.PercentualConclusao).HasColumnName("percentual_conclusao").HasPrecision(5, 2).HasDefaultValue(0.00m);
                entity.Property(c => c.Bloqueada).HasColumnType("boolean").HasDefaultValue(false);
                entity.Property(c => c.MotivoBloqueio).HasColumnName("motivo_bloqueio").HasColumnType("varchar(255)").HasMaxLength(255);
                entity.Property(c => c.Privado).HasColumnType("boolean").HasDefaultValue(true);
                entity.Property(c => c.CriadoRapidamente).HasColumnName("criado_rapidamente").HasColumnType("boolean").HasDefaultValue(false);
                entity.Property(c => c.DataCriacao).HasColumnName("data_criacao").HasColumnType("datetime").HasDefaultValueSql("CURRENT_TIMESTAMP");
                entity.Property(c => c.DataAtualizacao).HasColumnName("data_atualizacao").HasColumnType("datetime").HasDefaultValueSql("CURRENT_TIMESTAMP");

                entity.HasIndex(c => new { c.GrupoId, c.NumeroCartaoGrupo }).IsUnique().HasDatabaseName("uq_cartoes_tarefas_numero_grupo");
                entity.HasIndex(c => new { c.QuadroId, c.ColunaId, c.OrdemColuna }).HasDatabaseName("idx_cartoes_tarefas_quadro_coluna_ordem");
                entity.HasIndex(c => new { c.ResponsavelUsuarioId, c.Status, c.DataVencimento }).HasDatabaseName("idx_cartoes_tarefas_responsavel_status");
                entity.HasIndex(c => new { c.GrupoId, c.Status, c.DataCriacao }).HasDatabaseName("idx_cartoes_tarefas_grupo_status");
                entity.HasIndex(c => new { c.CriadorId, c.DataCriacao }).HasDatabaseName("idx_cartoes_tarefas_criador");
                entity.HasIndex(c => c.PaiCartaoId).HasDatabaseName("idx_cartoes_tarefas_pai");

                entity.HasOne(c => c.Quadro).WithMany().HasForeignKey(c => c.QuadroId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(c => c.Coluna).WithMany().HasForeignKey(c => c.ColunaId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(c => c.Grupo).WithMany().HasForeignKey(c => c.GrupoId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(c => c.PaiCartao).WithMany().HasForeignKey(c => c.PaiCartaoId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(c => c.Criador).WithMany().HasForeignKey(c => c.CriadorId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(c => c.ResponsavelUsuario).WithMany().HasForeignKey(c => c.ResponsavelUsuarioId).OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<CartaoTarefaUsuario>(entity =>
            {
                entity.ToTable("Cartoes_tarefas_usuarios");
                entity.HasKey(c => new { c.CartaoTarefaId, c.UsuarioId });

                entity.Property(c => c.TipoParticipacao).HasConversion<string>().HasColumnType("ENUM('Participante','Observador')").HasDefaultValue(TipoParticipacaoCartaoTarefa.Participante);
                entity.Property(c => c.Permissao).HasConversion<string>().HasColumnType("ENUM('Visualizador','Editor')").HasDefaultValue(PermissaoCartaoTarefa.Visualizador);
                entity.Property(c => c.DataAdicao).HasColumnName("data_adicao").HasColumnType("datetime").HasDefaultValueSql("CURRENT_TIMESTAMP");

                entity.HasIndex(c => new { c.UsuarioId, c.Permissao }).HasDatabaseName("idx_cartoes_tarefas_usuarios_usuario_permissao");

                entity.HasOne(c => c.CartaoTarefa).WithMany().HasForeignKey(c => c.CartaoTarefaId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(c => c.Usuario).WithMany().HasForeignKey(c => c.UsuarioId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(c => c.AdicionadoPorUsuario).WithMany().HasForeignKey(c => c.AdicionadoPorUsuarioId).OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<ComentarioTarefa>(entity =>
            {
                entity.ToTable("Comentarios_tarefas");
                entity.Property(c => c.Mensagem).IsRequired().HasColumnType("varchar(250)").HasMaxLength(250);
                entity.Property(c => c.Editado).HasColumnType("boolean").HasDefaultValue(false);
                entity.Property(c => c.DataCriacao).HasColumnName("data_criacao").HasColumnType("datetime").HasDefaultValueSql("CURRENT_TIMESTAMP");
                entity.Property(c => c.DataEdicao).HasColumnName("data_edicao").HasColumnType("datetime");
                entity.HasIndex(c => new { c.CartaoTarefaId, c.DataCriacao }).HasDatabaseName("idx_comentarios_tarefas_cartao_data");
                entity.HasOne(c => c.CartaoTarefa).WithMany().HasForeignKey(c => c.CartaoTarefaId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(c => c.Usuario).WithMany().HasForeignKey(c => c.UsuarioId).OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<AnexoTarefa>(entity =>
            {
                entity.ToTable("Anexos_tarefas");
                entity.Property(a => a.NomeOriginal).IsRequired().HasColumnName("nome_original").HasColumnType("varchar(255)").HasMaxLength(255);
                entity.Property(a => a.NomeArquivo).IsRequired().HasColumnName("nome_arquivo").HasColumnType("varchar(255)").HasMaxLength(255);
                entity.Property(a => a.CaminhoArquivo).IsRequired().HasColumnName("caminho_arquivo").HasColumnType("varchar(500)").HasMaxLength(500);
                entity.Property(a => a.TipoArquivo).HasColumnName("tipo_arquivo").HasColumnType("varchar(100)").HasMaxLength(100);
                entity.Property(a => a.Extensao).HasColumnName("extensao").HasColumnType("varchar(20)").HasMaxLength(20);
                entity.Property(a => a.EhImagem).HasColumnName("eh_imagem").HasColumnType("boolean").HasDefaultValue(false);
                entity.Property(a => a.EhCapa).HasColumnName("eh_capa").HasColumnType("boolean").HasDefaultValue(false);
                entity.Property(a => a.DataUpload).HasColumnName("data_upload").HasColumnType("datetime").HasDefaultValueSql("CURRENT_TIMESTAMP");
                entity.HasIndex(a => a.CartaoTarefaId).HasDatabaseName("idx_anexos_tarefas_cartao");
                entity.HasOne(a => a.CartaoTarefa).WithMany().HasForeignKey(a => a.CartaoTarefaId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(a => a.Usuario).WithMany().HasForeignKey(a => a.UsuarioId).OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<ChecklistTarefa>(entity =>
            {
                entity.ToTable("Checklists_tarefas");
                entity.Property(c => c.Titulo).IsRequired().HasColumnType("varchar(120)").HasMaxLength(120);
                entity.Property(c => c.Posicao).HasPrecision(18, 6);
                entity.Property(c => c.DataCriacao).HasColumnName("data_criacao").HasColumnType("datetime").HasDefaultValueSql("CURRENT_TIMESTAMP");
                entity.HasIndex(c => new { c.CartaoTarefaId, c.Posicao }).HasDatabaseName("idx_checklists_tarefas_cartao_posicao");
                entity.HasOne(c => c.CartaoTarefa).WithMany().HasForeignKey(c => c.CartaoTarefaId).OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<ChecklistItemTarefa>(entity =>
            {
                entity.ToTable("Checklist_itens_tarefas");
                entity.Property(c => c.Descricao).IsRequired().HasColumnType("varchar(255)").HasMaxLength(255);
                entity.Property(c => c.Concluido).HasColumnType("boolean").HasDefaultValue(false);
                entity.Property(c => c.DataConclusao).HasColumnName("data_conclusao").HasColumnType("datetime");
                entity.Property(c => c.Posicao).HasPrecision(18, 6);
                entity.HasIndex(c => new { c.ChecklistId, c.Posicao }).HasDatabaseName("idx_checklist_itens_tarefas_checklist_posicao");
                entity.HasOne(c => c.Checklist).WithMany().HasForeignKey(c => c.ChecklistId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(c => c.ConcluidoPorUsuario).WithMany().HasForeignKey(c => c.ConcluidoPorUsuarioId).OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<EtiquetaTarefa>(entity =>
            {
                entity.ToTable("Etiquetas_tarefas");
                entity.Property(e => e.Nome).IsRequired().HasColumnType("varchar(50)").HasMaxLength(50);
                entity.Property(e => e.Cor).IsRequired().HasColumnType("varchar(20)").HasMaxLength(20);
                entity.Property(e => e.DataCriacao).HasColumnName("data_criacao").HasColumnType("datetime").HasDefaultValueSql("CURRENT_TIMESTAMP");
                entity.HasIndex(e => new { e.GrupoId, e.UsuarioId }).HasDatabaseName("idx_etiquetas_tarefas_grupo_usuario");
                entity.HasIndex(e => new { e.GrupoId, e.UsuarioId, e.Nome }).IsUnique().HasDatabaseName("uq_etiquetas_tarefas_grupo_usuario_nome");
                entity.HasOne(e => e.Grupo).WithMany().HasForeignKey(e => e.GrupoId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(e => e.Usuario).WithMany().HasForeignKey(e => e.UsuarioId).OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<CartaoTarefaEtiqueta>(entity =>
            {
                entity.ToTable("Cartoes_tarefas_etiquetas");
                entity.HasKey(e => new { e.CartaoTarefaId, e.EtiquetaId });
                entity.HasOne(e => e.CartaoTarefa).WithMany().HasForeignKey(e => e.CartaoTarefaId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(e => e.Etiqueta).WithMany().HasForeignKey(e => e.EtiquetaId).OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<CartaoTarefaChamado>(entity =>
            {
                entity.ToTable("Cartoes_tarefas_chamados");
                entity.HasKey(c => new { c.CartaoTarefaId, c.ChamadoId });

                entity.Property(c => c.TipoRelacao).HasConversion<string>().HasColumnType("ENUM('Origem','Relacionada','BloqueadaPor','ResolveChamado')").HasDefaultValue(TipoRelacaoCartaoChamado.Relacionada);
                entity.Property(c => c.Ativo).HasColumnType("boolean").HasDefaultValue(true);
                entity.Property(c => c.DataVinculo).HasColumnName("data_vinculo").HasColumnType("datetime").HasDefaultValueSql("CURRENT_TIMESTAMP");
                entity.Property(c => c.DataDesvinculo).HasColumnName("data_desvinculo").HasColumnType("datetime");

                entity.HasIndex(c => new { c.ChamadoId, c.Ativo }).HasDatabaseName("idx_cartoes_tarefas_chamados_chamado_ativo");
                entity.HasIndex(c => new { c.CartaoTarefaId, c.Ativo }).HasDatabaseName("idx_cartoes_tarefas_chamados_cartao_ativo");

                entity.HasOne(c => c.CartaoTarefa).WithMany().HasForeignKey(c => c.CartaoTarefaId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(c => c.Chamado).WithMany().HasForeignKey(c => c.ChamadoId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(c => c.VinculadoPorUsuario).WithMany().HasForeignKey(c => c.VinculadoPorUsuarioId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(c => c.DesvinculadoPorUsuario).WithMany().HasForeignKey(c => c.DesvinculadoPorUsuarioId).OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<HistoricoTarefa>(entity =>
            {
                entity.ToTable("Historico_tarefas");
                entity.Property(h => h.TipoAcao).IsRequired().HasColumnType("varchar(50)").HasMaxLength(50);
                entity.Property(h => h.CampoAlterado).HasColumnType("varchar(100)").HasMaxLength(100);
                entity.Property(h => h.ValorAnterior).HasColumnType("text");
                entity.Property(h => h.ValorNovo).HasColumnType("text");
                entity.Property(h => h.DataAcao).HasColumnName("data_acao").HasColumnType("datetime").HasDefaultValueSql("CURRENT_TIMESTAMP");
                entity.HasIndex(h => new { h.CartaoTarefaId, h.DataAcao }).HasDatabaseName("idx_historico_tarefas_cartao_data");
                entity.HasIndex(h => h.UsuarioId).HasDatabaseName("idx_historico_tarefas_usuario");
                entity.HasOne(h => h.CartaoTarefa).WithMany().HasForeignKey(h => h.CartaoTarefaId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(h => h.Usuario).WithMany().HasForeignKey(h => h.UsuarioId).OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<DependenciaTarefa>(entity =>
            {
                entity.ToTable("Dependencias_tarefas");
                entity.HasKey(d => new { d.CartaoTarefaId, d.CartaoDependenteId });
                entity.Property(d => d.TipoDependencia).HasConversion<string>().HasColumnType("ENUM('Bloqueia','Relacionada')").HasDefaultValue(TipoDependenciaTarefa.Bloqueia);
                entity.HasOne(d => d.CartaoTarefa).WithMany().HasForeignKey(d => d.CartaoTarefaId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(d => d.CartaoDependente).WithMany().HasForeignKey(d => d.CartaoDependenteId).OnDelete(DeleteBehavior.Restrict);
            });

            // ===== FeedbackChamado =====
            modelBuilder.Entity<FeedbackChamado>(entity =>
            {
                entity.ToTable("Feedbacks_chamados");

                entity.Property(f => f.Avaliacao)
                    .IsRequired()
                    .HasColumnName("nota")
                    .HasColumnType("int");

                entity.Property(f => f.Comentario)
                    .HasColumnType("varchar(250)")
                    .HasMaxLength(250);

                entity.Property(f => f.TempoResposta)
                    .HasColumnName("tempo_resposta")
                    .HasColumnType("int");

                entity.Property(f => f.TempoResolucao)
                    .HasColumnName("tempo_resolucao")
                    .HasColumnType("int");

                entity.Property(f => f.DataAvaliacao)
                    .HasColumnName("data_avaliacao")
                    .HasColumnType("datetime")
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");

                entity.HasIndex(f => new { f.ChamadoId, f.AvaliadorId }).IsUnique();
                entity.HasIndex(f => new { f.AvaliadorId }).HasDatabaseName("idx_feedback_avaliador");
                entity.HasIndex(f => new { f.ChamadoId }).HasDatabaseName("idx_feedback_chamado");
            });

            // ===== ConviteGrupo =====
            modelBuilder.Entity<ConviteGrupo>(entity =>
            {
                entity.ToTable("Convites_grupo");

                entity.Property(c => c.Status)
                    .IsRequired()
                    .HasConversion<string>()
                    .HasColumnType("ENUM('Pendente','Aceito','Recusado','Cancelado')")
                    .HasDefaultValue(StatusConviteGrupo.Pendente);

                entity.Property(c => c.Mensagem)
                    .HasColumnType("varchar(255)")
                    .HasMaxLength(255);

                entity.Property(c => c.DataCriacao)
                    .HasColumnName("data_criacao")
                    .HasColumnType("datetime")
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");

                entity.Property(c => c.DataResposta)
                    .HasColumnName("data_resposta")
                    .HasColumnType("datetime");

                entity.HasIndex(c => new { c.DestinatarioUsuarioId, c.Status })
                    .HasDatabaseName("idx_convite_destinatario_status");

                entity.HasIndex(c => new { c.GrupoId, c.Status })
                    .HasDatabaseName("idx_convite_grupo_status");
            });

            // ===== Notificacao =====
            modelBuilder.Entity<Notificacao>(entity =>
            {
                entity.ToTable("Notificacoes");

                entity.Property(n => n.Tipo)
                    .IsRequired()
                    .HasConversion<string>()
                    .HasColumnType("ENUM('ConviteGrupo','Sistema','Chamado','Tarefa')")
                    .HasDefaultValue(TipoNotificacao.Sistema);

                entity.Property(n => n.GrupoId)
                    .HasColumnName("grupo_id")
                    .IsRequired();

                entity.Property(n => n.Titulo)
                    .IsRequired()
                    .HasColumnType("varchar(150)")
                    .HasMaxLength(150);

                entity.Property(n => n.Mensagem)
                    .IsRequired()
                    .HasColumnType("varchar(500)")
                    .HasMaxLength(500);

                entity.Property(n => n.Lida)
                    .HasColumnType("boolean")
                    .HasDefaultValue(false);

                entity.Property(n => n.DataCriacao)
                    .HasColumnName("data_criacao")
                    .HasColumnType("datetime")
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");

                entity.Property(n => n.DataLeitura)
                    .HasColumnName("data_leitura")
                    .HasColumnType("datetime");

                entity.Property(n => n.ReferenciaTipo)
                    .HasColumnName("referencia_tipo")
                    .HasColumnType("varchar(50)")
                    .HasMaxLength(50);

                entity.Property(n => n.UsuarioOrigemId)
                    .HasColumnName("usuario_origem_id");

                entity.Property(n => n.MencaoId)
                    .HasColumnName("mencao_id");

                entity.Property(n => n.LinkDestino)
                    .HasColumnName("link_destino")
                    .HasColumnType("varchar(255)")
                    .HasMaxLength(255);

                entity.HasIndex(n => new { n.UsuarioId, n.Lida, n.DataCriacao })
                    .HasDatabaseName("idx_notificacao_usuario_lida");

                entity.HasIndex(n => new { n.UsuarioId, n.GrupoId, n.Lida, n.DataCriacao })
                    .HasDatabaseName("idx_notificacao_usuario_grupo_lida_data");

                entity.HasIndex(n => new { n.UsuarioId, n.Lida, n.Tipo, n.ReferenciaTipo, n.ReferenciaId })
                    .HasDatabaseName("idx_notificacao_usuario_referencia_lida");

                entity.HasIndex(n => n.MencaoId)
                    .IsUnique()
                    .HasDatabaseName("uq_notificacoes_mencao");

                entity.HasOne(n => n.Grupo)
                    .WithMany()
                    .HasForeignKey(n => n.GrupoId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne<Usuario>()
                    .WithMany()
                    .HasForeignKey(n => n.UsuarioOrigemId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne<MencaoTexto>()
                    .WithMany()
                    .HasForeignKey(n => n.MencaoId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<MencaoTexto>(entity =>
            {
                entity.ToTable("Mencoes_textos");

                entity.Property(m => m.EntidadeTipo)
                    .IsRequired()
                    .HasColumnName("entidade_tipo")
                    .HasColumnType("varchar(30)")
                    .HasMaxLength(30);

                entity.Property(m => m.CampoOrigem)
                    .IsRequired()
                    .HasColumnName("campo_origem")
                    .HasColumnType("varchar(40)")
                    .HasMaxLength(40);

                entity.Property(m => m.TextoExibido)
                    .IsRequired()
                    .HasColumnName("texto_exibido")
                    .HasColumnType("varchar(100)")
                    .HasMaxLength(100);

                entity.Property(m => m.CriadoEm)
                    .HasColumnName("criado_em")
                    .HasColumnType("datetime")
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");

                entity.HasIndex(m => new { m.GrupoId, m.EntidadeTipo, m.EntidadeId, m.CampoOrigem })
                    .HasDatabaseName("idx_mencoes_contexto");

                entity.HasIndex(m => new { m.UsuarioMencionadoId, m.CriadoEm })
                    .HasDatabaseName("idx_mencoes_usuario");

                entity.HasOne(m => m.Grupo)
                    .WithMany()
                    .HasForeignKey(m => m.GrupoId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(m => m.UsuarioMencionado)
                    .WithMany()
                    .HasForeignKey(m => m.UsuarioMencionadoId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(m => m.UsuarioAutor)
                    .WithMany()
                    .HasForeignKey(m => m.UsuarioAutorId)
                    .OnDelete(DeleteBehavior.Restrict);
            });
            modelBuilder.Entity<HistoricoAlteracaoChamado>(entity =>
            {
                entity.ToTable("Historico_alteracoes_chamado");

                entity.HasKey(e => e.Id);

                entity.Property(e => e.Id)
                    .HasColumnName("id");

                entity.Property(e => e.ChamadoId)
                    .HasColumnName("chamado_id")
                    .IsRequired();

                entity.Property(e => e.GrupoId)
                    .HasColumnName("grupo_id")
                    .IsRequired();

                entity.Property(e => e.UsuarioId)
                    .HasColumnName("usuario_id")
                    .IsRequired();

                entity.Property(e => e.CampoAlterado)
                    .HasColumnName("campo_alterado")
                    .HasMaxLength(100)
                    .IsRequired();

                entity.Property(e => e.ValorAnterior)
                    .HasColumnName("valor_anterior")
                    .HasMaxLength(500);

                entity.Property(e => e.ValorAlterado)
                    .HasColumnName("valor_alterado")
                    .HasMaxLength(500);

                entity.Property(e => e.TipoAlteracao)
                    .HasColumnName("tipo_alteracao")
                    .HasMaxLength(50)
                    .IsRequired();

                entity.Property(e => e.DataAlteracao)
                    .HasColumnName("data_alteracao")
                    .HasColumnType("datetime")
                    .HasDefaultValueSql("CURRENT_TIMESTAMP")
                    .IsRequired();

                entity.HasIndex(e => new { e.ChamadoId, e.DataAlteracao })
                    .HasDatabaseName("IX_historico_alteracoes_chamado_chamado_data");

                entity.HasIndex(e => new { e.ChamadoId, e.CampoAlterado, e.DataAlteracao })
                    .HasDatabaseName("IX_historico_alteracoes_chamado_campo_data");

                entity.HasIndex(e => e.UsuarioId)
                    .HasDatabaseName("IX_historico_alteracoes_chamado_usuario");

                entity.HasOne(e => e.Chamado)
                    .WithMany(c => c.HistoricoAlteracoes)
                    .HasForeignKey(e => e.ChamadoId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.Grupo)
                    .WithMany(g => g.HistoricoAlteracoesChamado)
                    .HasForeignKey(e => e.GrupoId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.Usuario)
                    .WithMany(u => u.HistoricoAlteracoesChamado)
                    .HasForeignKey(e => e.UsuarioId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<ChamadoContadorGrupo>(entity =>
            {
                entity.ToTable("Chamado_contador_grupo");

                entity.HasKey(x => x.GrupoId);

                entity.Property(x => x.GrupoId)
                    .HasColumnName("grupo_id");

                entity.Property(x => x.UltimoNumero)
                    .HasColumnName("ultimo_numero")
                    .IsRequired();

                entity.HasOne(x => x.Grupo)
                    .WithMany()
                    .HasForeignKey(x => x.GrupoId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<ChamadoContadorUsuario>(entity =>
            {
                entity.ToTable("Chamado_contador_usuario");

                entity.HasKey(x => x.UsuarioId);

                entity.Property(x => x.UsuarioId)
                    .HasColumnName("usuario_id");

                entity.Property(x => x.UltimoNumero)
                    .HasColumnName("ultimo_numero")
                    .IsRequired();

                entity.HasOne(x => x.Usuario)
                    .WithMany()
                    .HasForeignKey(x => x.UsuarioId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<ChamadoContadorUsuarioGrupo>(entity =>
            {
                entity.ToTable("Chamado_contador_usuario_grupo");

                entity.HasKey(x => new { x.UsuarioId, x.GrupoId });

                entity.Property(x => x.UsuarioId)
                    .HasColumnName("usuario_id");

                entity.Property(x => x.GrupoId)
                    .HasColumnName("grupo_id");

                entity.Property(x => x.UltimoNumero)
                    .HasColumnName("ultimo_numero")
                    .IsRequired();

                entity.HasOne(x => x.Usuario)
                    .WithMany()
                    .HasForeignKey(x => x.UsuarioId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(x => x.Grupo)
                    .WithMany()
                    .HasForeignKey(x => x.GrupoId)
                    .OnDelete(DeleteBehavior.Restrict);
            });
        }
    }
}
