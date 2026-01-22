using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CallStationApp.Models
{
    public class Grupo
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required(ErrorMessage = "O campo nome é obrigatório.")]
        [Column("nome", TypeName = "varchar(100)")]
        [StringLength(100, ErrorMessage = "O nome do grupo não pode exceder 100 caracteres.")]
        public required string Nome { get; set; }

        [Column("descricao_grupo", TypeName = "varchar(200)")]
        [StringLength(200, ErrorMessage = "A descrição do grupo não pode exceder 200 caracteres.")]
        public string? DescricaoGrupo { get; set; }

        [Column("foto_grupo", TypeName = "varchar(255)")]
        [StringLength(255, ErrorMessage = "A URL da foto do grupo não pode exceder 255 caracteres.")]
        public string? FotoGrupo { get; set; }

        // ETIQUETA COMO ENUM (ALINHADO COM O BANCO)
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