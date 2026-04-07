using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CallStationApp.Models
{
    public class InfoUsuarioGrupo
    {
        [Required]
        [Column("usuario_id")]
        public int UsuarioId { get; set; }

        [ForeignKey(nameof(UsuarioId))]
        public Usuario Usuario { get; set; } = null!;

        [Required]
        [Column("grupo_id")]
        public int GrupoId { get; set; }

        [ForeignKey(nameof(GrupoId))]
        public Grupo Grupo { get; set; } = null!;

        [Column("apelido", TypeName = "varchar(100)")]
        [StringLength(100, ErrorMessage = "O apelido não pode exceder 100 caracteres.")]
        public string? Apelido { get; set; }

        [Column("descricao_ativo", TypeName = "varchar(500)")]
        [StringLength(500, ErrorMessage = "A descrição do ativo não pode exceder 500 caracteres.")]
        public string? DescricaoAtivo { get; set; }

        [Column("identificador_interno", TypeName = "varchar(50)")]
        [StringLength(50, ErrorMessage = "O identificador interno não pode exceder 50 caracteres.")]
        public string? IdentificadorInterno { get; set; }

        [Column("observacao", TypeName = "varchar(500)")]
        [StringLength(500, ErrorMessage = "A observação não pode exceder 500 caracteres.")]
        public string? Observacao { get; set; }

        [Column("data_atualizacao_ativo")]
        public DateTime? DataAtualizacaoAtivo { get; set; }

        [Column("data_atualizacao_registro")]
        public DateTime DataAtualizacaoRegistro { get; set; } = DateTime.UtcNow;
    }
}