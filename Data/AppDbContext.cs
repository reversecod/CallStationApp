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
        public DbSet<UsuarioGrupo> UsuariosGrupos { get; set; }
        public DbSet<InfoUsuarioGrupo> InfoUsuariosGrupos { get; set; }
        public DbSet<OcorrenciaTipo> OcorrenciasTipo { get; set; }
        public DbSet<Setor> Setores { get; set; }
        public DbSet<OcorrenciaCategoria> OcorrenciasCategoria { get; set; }
        public DbSet<OcorrenciaSubcategoria> OcorrenciasSubcategoria { get; set; }
        public DbSet<Chamado> Chamados { get; set; }
        public DbSet<HistoricoStatusChamado> HistoricoStatusChamados { get; set; }
        public DbSet<ComentarioChamado> ComentariosChamados { get; set; }
        public DbSet<Tarefa> Tarefas { get; set; }
        public DbSet<FeedbackChamado> FeedbacksChamados { get; set; }
        public DbSet<ConviteGrupo> ConvitesGrupo { get; set; }
        public DbSet<Notificacao> Notificacoes { get; set; }

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
                    .HasColumnType("varchar(100)")
                    .HasMaxLength(100);

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
                    .HasColumnType("varchar(100)")
                    .HasMaxLength(100);

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
                    .HasColumnType("varchar(100)")
                    .HasMaxLength(100);

                entity.Property(iu => iu.DescricaoAtivo)
                    .HasColumnName("descricao_ativo")
                    .HasColumnType("text");

                entity.Property(iu => iu.IdentificadorInterno)
                    .HasColumnName("identificador_interno")
                    .HasColumnType("varchar(50)")
                    .HasMaxLength(50);

                entity.Property(iu => iu.Observacao)
                    .HasColumnName("observacao")
                    .HasColumnType("text");

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
                entity.ToTable("Ocorrencias_Tipo");

                entity.Property(ot => ot.TipoOcorrencia)
                    .IsRequired()
                    .HasColumnName("tipo_ocorrencia")
                    .HasColumnType("varchar(100)")
                    .HasMaxLength(100);

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
                    .HasColumnType("varchar(50)")
                    .HasMaxLength(50);

                entity.Property(c => c.Descricao)
                    .HasColumnType("text");

                entity.Property(c => c.Solucao)
                    .HasColumnType("text");

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
                entity.HasOne<UsuarioGrupo>()
                    .WithMany()
                    .HasForeignKey(c => new { c.CriadorChamadoId, c.GrupoId });

                // ===== ÍNDICES =====
                entity.HasIndex(c => c.GrupoId).HasDatabaseName("idx_chamados_grupo");
                entity.HasIndex(c => c.Status).HasDatabaseName("idx_chamados_status");
                entity.HasIndex(c => c.CriadorChamadoId).HasDatabaseName("idx_chamados_criador");
                entity.HasIndex(c => c.DataCriacao).HasDatabaseName("idx_chamados_data_criacao");

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

            // ===== HistoricoStatusChamado =====
            modelBuilder.Entity<HistoricoStatusChamado>(entity =>
            {
                entity.ToTable("Historico_Status_Chamados");

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

                entity.HasIndex(h => new { h.ChamadoId }).HasDatabaseName("idx_historico_chamado");
            });

            // ===== ComentarioChamado =====
            modelBuilder.Entity<ComentarioChamado>(entity =>
            {
                entity.ToTable("Comentarios_chamados");

                entity.Property(cc => cc.Mensagem)
                    .IsRequired()
                    .HasColumnType("text");

                entity.Property(cc => cc.AnexoComentario)
                    .HasColumnName("anexo_comentario")
                    .HasColumnType("varchar(255)")
                    .HasMaxLength(255);

                entity.Property(cc => cc.DataComentario)
                    .HasColumnName("data_comentario")
                    .HasColumnType("datetime")
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");

                entity.HasIndex(c => new { c.ChamadoId }).HasDatabaseName("idx_comentario_chamado");
                entity.HasIndex(c => new { c.UsuarioId }).HasDatabaseName("idx_comentario_usuario_chamado");
            });

            // ===== Tarefa =====
            modelBuilder.Entity<Tarefa>(entity =>
            {
                entity.ToTable("Tarefas");

                // ===== CAMPOS BÁSICOS =====
                entity.Property(t => t.Titulo)
                    .IsRequired()
                    .HasColumnType("varchar(50)")
                    .HasMaxLength(50);

                entity.Property(t => t.Descricao)
                    .IsRequired()
                    .HasColumnType("text");

                // ===== ENUMS =====
                entity.Property(t => t.Status)
                    .HasConversion<string>()
                    .HasColumnType("ENUM('Pendente','EmAndamento','Concluida','Cancelada')")
                    .HasDefaultValue(StatusTarefa.Pendente);

                entity.Property(t => t.Criticidade)
                    .HasConversion<string>()
                    .HasColumnType("ENUM('Baixa','Media','Alta','Critico')");

                entity.Property(t => t.Urgencia)
                    .HasConversion<string>()
                    .HasColumnType("ENUM('NaoUrgente','PoucaUrgencia','Urgente','Emergencia')");

                // ===== DATAS =====
                entity.Property(t => t.DataCriacao)
                    .HasColumnName("data_criacao")
                    .HasColumnType("datetime")
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");

                entity.Property(t => t.DataConclusao)
                    .HasColumnName("data_conclusao")
                    .HasColumnType("datetime");

                // ===== CAMPOS OBRIGATÓRIOS DO FLUXO =====
                entity.Property(t => t.GrupoId)
                    .IsRequired();

                entity.Property(t => t.CriadorId)
                    .IsRequired();

                // ===== FK COMPOSTA (REGRA DO SISTEMA) =====
                entity.HasOne<UsuarioGrupo>()
                    .WithMany()
                    .HasForeignKey(t => new { t.CriadorId, t.GrupoId });

                // ===== ÍNDICES =====
                entity.HasIndex(t => t.GrupoId).HasDatabaseName("idx_tarefas_grupo");
                entity.HasIndex(t => t.CriadorId).HasDatabaseName("idx_tarefas_criador");
                entity.HasIndex(t => t.Status).HasDatabaseName("idx_tarefas_status");
            });

            // ===== FeedbackChamado =====
            modelBuilder.Entity<FeedbackChamado>(entity =>
            {
                entity.ToTable("Feedbacks_chamados");

                entity.Property(f => f.Avaliacao)
                    .IsRequired()
                    .HasColumnType("int");

                entity.Property(f => f.Comentario)
                    .HasColumnType("text");

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

                entity.Property(n => n.LinkDestino)
                    .HasColumnName("link_destino")
                    .HasColumnType("varchar(255)")
                    .HasMaxLength(255);

                entity.HasIndex(n => new { n.UsuarioId, n.Lida, n.DataCriacao })
                    .HasDatabaseName("idx_notificacao_usuario_lida");
            });
        }
    }
}