using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CallStationApp.Models
{
    public class Grupo
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required(ErrorMessage = "O campo nome e obrigatorio.")]
        [Column("nome", TypeName = "varchar(35)")]
        [StringLength(35, ErrorMessage = "O nome do grupo nao pode exceder 35 caracteres.")]
        public required string Nome { get; set; }

        [Column("descricao_grupo", TypeName = "varchar(200)")]
        [StringLength(200, ErrorMessage = "A descricao do grupo nao pode exceder 200 caracteres.")]
        public string? DescricaoGrupo { get; set; }

        [Column("foto_grupo", TypeName = "varchar(255)")]
        [StringLength(255, ErrorMessage = "A URL da foto do grupo nao pode exceder 255 caracteres.")]
        public string? FotoGrupo { get; set; }

        [Required]
        [Column("etiqueta_cor")]
        public EtiquetaCor EtiquetaCor { get; set; }

        [Required]
        [Column("criador_id")]
        public int CriadorId { get; set; }

        [ForeignKey(nameof(CriadorId))]
        public Usuario Usuario { get; set; } = null!;

        [Column("data_criacao")]
        public DateTime DataCriacao { get; set; }

        public ICollection<HistoricoAlteracaoChamado> HistoricoAlteracoesChamado { get; set; } = new List<HistoricoAlteracaoChamado>();
    }

    public enum EtiquetaCor
    {
        branco,
        vermelho,
        laranja,
        amarelo,
        verde,
        azul,
        roxo,
        rosa,
        preto
    }
}
