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
        
        // DbSets para as entidades do seu modelo de dados onde "Dbset <Tabela no C#> Tabela no banco de dados"
        public DbSet<Usuario> Usuarios { get; set; }
        public DbSet<UsuarioGrupo> UsuariosGrupos  { get; set; }
        public DbSet<Grupo> Grupos { get; set; }
        public DbSet<InfoUsuarioGrupo> InfoUsuariosGrupos { get; set; }
        public DbSet<OcorrenciaTipo> OcorrenciasTipo { get; set; }
        public DbSet<OcorrenciaCategoria> OcorrenciasCategoria { get; set; }
        public DbSet<OcorrenciaSubcategoria> OcorrenciasSubcategoria { get; set; }
        public DbSet<Setor> Setores { get; set; }
        public DbSet<Chamado> Chamados { get; set; }
        public DbSet<HistoricoStatusChamado> HistoricoStatusChamados { get; set; }
        public DbSet<ComentarioChamado> ComentariosChamados { get; set; }
        public DbSet<Tarefa> Tarefas { get; set; }
        public DbSet<FeedbackChamado> FeedbacksChamados { get; set; }
        
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            // Configurações adicionais de mapeamento, se necessário
            modelBuilder.Entity<Usuario>(entity =>
            {
                entity.ToTable("Usuarios");
                
                entity.HasIndex(u => u.NomeUsuario).IsUnique();
                entity.HasIndex(u => u.Email).IsUnique(); // corresponde à UNIQUE(email)
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
                    .HasMaxLength(255); // tamanho máximo para hash de senha
                
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
            
            modelBuilder.Entity<UsuarioGrupo>(entity =>
                {
                entity.ToTable("Usuarios_grupos");
                
                entity.Property(ug => ug.Permissao)
                    .IsRequired()
                    .HasConversion<string>()
                    .HasColumnType("ENUM('Nenhuma','Colaborador','Tecnico','Administracao')")
                    .HasDefaultValue("Nenhuma");
                
                entity.Property(ug => ug.DataAdicao)
                    .HasColumnName("data_adicao")
                    .HasColumnType("datetime")
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");

                entity.HasIndex(ug => new { ug.UsuarioId, ug.GrupoId })
                    .HasDatabaseName("idx_usuario_grupo");
                });
            
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

                entity.Property(g => g.DataCriacao)
                    .HasColumnName("data_criacao")
                    .HasColumnType("datetime")
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");
                
                entity.HasIndex(g => new { g.Nome, g.CriadorId })
                    .IsUnique(); // corresponde à UNIQUE(nome, criador_id)
            });
            
            modelBuilder.Entity<InfoUsuarioGrupo>(entity => 
            {
                entity.ToTable("Info_usuarios_grupos");
                
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
                
                entity.HasIndex(iu => new { iu.UsuarioId,iu.GrupoId })
                    .IsUnique(); // garante que não haja duplicatas de usuário/grupo
            });

            modelBuilder.Entity<OcorrenciaTipo>(entity =>
            {
                entity.ToTable("Ocorrencia_Tipos");
                
                entity.Property(ot => ot.TipoOcorrencia)
                    .IsRequired()
                    .HasColumnName("tipo_ocorrencia")
                    .HasColumnType("varchar(100)")
                    .HasMaxLength(100);
                
                entity.HasIndex(ot => new { ot.TipoOcorrencia, ot.UsuarioId, ot.GrupoId })
                    .IsUnique();
                
            });
            modelBuilder.Entity<OcorrenciaCategoria>(entity =>
            {
                entity.ToTable("Ocorrencia_categorias");
                
                entity.Property(ot => ot.CategoriaOcorrencia)
                    .IsRequired()
                    .HasColumnName("categoria_ocorrencia")
                    .HasColumnType("varchar(50)")
                    .HasMaxLength(50);
                
                entity.HasIndex(ot => new { ot.TipoId, ot.CategoriaOcorrencia})
                    .IsUnique();
                
            });
            modelBuilder.Entity<OcorrenciaSubcategoria>(entity =>
            {
                entity.ToTable("Ocorrencia_subcategorias");
                
                entity.Property(ot => ot.SubcategoriaOcorrencia)
                    .IsRequired()
                    .HasColumnName("subcategoria_ocorrencia")
                    .HasColumnType("varchar(100)")
                    .HasMaxLength(100);
                
                entity.HasIndex(ot => new { ot.CategoriaId, ot.SubcategoriaOcorrencia })
                    .IsUnique();
                
            });
            
            modelBuilder.Entity<Setor>().ToTable("Setores");
            
            modelBuilder.Entity<Setor>(entity =>
            {
                entity.Property(s => s.NomeSetor)
                    .IsRequired()
                    .HasColumnName("nome_setor")
                    .HasColumnType("varchar(50)")
                    .HasMaxLength(50);
                
                entity.HasIndex(s => new { s.NomeSetor, s.UsuarioId, s.GrupoId })
                    .IsUnique(); // garante que não haja setores duplicados para o mesmo usuário
            });

            modelBuilder.Entity<Chamado>(entity =>
            {
                entity.ToTable("Chamados");
                
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

                entity.Property(c => c.CriadorSolicitacao)
                    .HasColumnName("criador_solicitacao")
                    .HasColumnType("varchar(100)")
                    .HasMaxLength(100);

                entity.Property(c => c.Criticidade)
                    .HasConversion<string>()
                    .HasColumnType("ENUM('Baixa','Media','Alta','Critico')");

                entity.Property(c => c.Status)
                    .HasConversion<string>()
                    .HasColumnType("ENUM('Aberto','EmAndamento','Pendente','Concluido','Fechado','Reaberto','Cancelado','Excluído')")
                    .HasDefaultValue("Aberto");

                entity.Property(c => c.DataCriacao)
                    .HasColumnName("data_criacao")
                    .HasColumnType("datetime")
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");

                entity.Property(c => c.DataFinalizacao)
                    .HasColumnName("data_finalizacao")
                    .HasColumnType("datetime")
                    .HasDefaultValueSql("CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP");
                
                entity.Property(c => c.PrazoResposta)
                    .HasColumnName("prazo_resposta")
                    .HasColumnType("datetime");
                
                entity.Property(c => c.PrazoConclusao)
                    .HasColumnName("prazo_conclusao")
                    .HasColumnType("datetime");
                
                entity.Property(c => c.Publico)
                    .HasColumnType("boolean")
                    .HasDefaultValue(false);
                
                entity.HasIndex(c => c.GrupoId).HasDatabaseName("idx_chamados_grupo");
                entity.HasIndex(c => c.Status).HasDatabaseName("idx_chamados_status");
                entity.HasIndex(c => c.CriadorChamadoId).HasDatabaseName("idx_chamados_criador");
                entity.HasIndex(c => c.DataCriacao).HasDatabaseName("idx_chamados_data_criacao");
            });

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
                
                entity.HasIndex(h => new { h.ChamadoId}).HasDatabaseName("idx_historico_chamado");
            });
            
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

                entity.HasIndex(c => new { c.ChamadoId}).HasDatabaseName("idx_comentario_chamado"); // garante que não haja comentários duplicados para o mesmo chamado e usuário
                entity.HasIndex(c => new { c.UsuarioId}).HasDatabaseName("idx_comentario_usuario_chamado");
            });
            
            modelBuilder.Entity<Tarefa>(entity =>
            {
                entity.ToTable("Tarefas");
                
                entity.Property(t => t.Titulo)
                    .IsRequired()
                    .HasColumnType("varchar(50)")
                    .HasMaxLength(50);

                entity.Property(t => t.Descricao)
                    .IsRequired()
                    .HasColumnType("text");
                
                entity.Property(t => t.Status)
                    .HasConversion<string>()
                    .HasColumnType("ENUM('Pendente','EmAndamento','Concluida','Cancelada')")
                    .HasDefaultValue("Pendente");
                
                entity.Property(t => t.Criticidade)
                    .HasConversion<string>()
                    .HasColumnType("ENUM('Baixa','Media','Alta','Critico')");

                entity.Property(t => t.Urgencia)
                    .HasConversion<string>()
                    .HasColumnType("ENUM('NaoUrgente','PoucaUrgencia','Urgente','Emergencia')");

                entity.Property(t => t.DataCriacao)
                    .HasColumnName("data_criacao")
                    .HasColumnType("datetime")
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");

                entity.Property(t => t.DataConclusao)
                    .HasColumnName("data_conclusao")
                    .HasColumnType("datetime");

                entity.HasIndex(t => new {t.GrupoId }).HasDatabaseName("idx_tarefas_grupo");
                entity.HasIndex(t => new { t.CriadorId}).HasDatabaseName("idx_tarefas_criador");
                entity.HasIndex(t => new { t.Status }).HasDatabaseName("idx_tarefas_status");
            });
            
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
        }
    }
}