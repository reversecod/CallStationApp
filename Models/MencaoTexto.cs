using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CallStationApp.Models
{
    public class MencaoTexto
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("grupo_id")]
        public int GrupoId { get; set; }

        [Column("usuario_mencionado_id")]
        public int UsuarioMencionadoId { get; set; }

        [Column("usuario_autor_id")]
        public int UsuarioAutorId { get; set; }

        [Required]
        [Column("entidade_tipo", TypeName = "varchar(30)")]
        [StringLength(30)]
        public string EntidadeTipo { get; set; } = string.Empty;

        [Column("entidade_id")]
        public int EntidadeId { get; set; }

        [Required]
        [Column("campo_origem", TypeName = "varchar(40)")]
        [StringLength(40)]
        public string CampoOrigem { get; set; } = string.Empty;

        [Required]
        [Column("texto_exibido", TypeName = "varchar(100)")]
        [StringLength(100)]
        public string TextoExibido { get; set; } = string.Empty;

        [Column("posicao_inicio")]
        public int PosicaoInicio { get; set; }

        [Column("posicao_fim")]
        public int PosicaoFim { get; set; }

        [Column("criado_em")]
        public DateTime CriadoEm { get; set; } = DateTime.UtcNow;

        [ForeignKey(nameof(GrupoId))]
        public Grupo Grupo { get; set; } = null!;

        [ForeignKey(nameof(UsuarioMencionadoId))]
        public Usuario UsuarioMencionado { get; set; } = null!;

        [ForeignKey(nameof(UsuarioAutorId))]
        public Usuario UsuarioAutor { get; set; } = null!;
    }
}
