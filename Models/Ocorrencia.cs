using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CallStationApp.Models
{
    public class OcorrenciaTipo
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required(ErrorMessage = "O campo do tipo de ocorrencia e obrigatorio.")]
        [Column("tipo_ocorrencia", TypeName = "varchar(50)")]
        [StringLength(50, ErrorMessage = "O tipo de ocorrencia nao pode exceder 50 caracteres.")]
        public required string TipoOcorrencia { get; set; }

        [Required]
        [Column("usuario_id")]
        public required int UsuarioId { get; set; }

        [Required]
        [Column("grupo_id")]
        public int GrupoId { get; set; }

        [ForeignKey("GrupoId")]
        public Grupo Grupo { get; set; } = null!;
    }

    public class OcorrenciaCategoria
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [Column("tipo_id")]
        public required int TipoId { get; set; }

        [ForeignKey("TipoId")]
        public required OcorrenciaTipo OcorrenciaTipo { get; set; }

        [Required(ErrorMessage = "O campo da categoria de ocorrencia e obrigatorio.")]
        [Column("categoria_ocorrencia", TypeName = "varchar(50)")]
        [StringLength(50, ErrorMessage = "A categoria de ocorrencia nao pode exceder 50 caracteres.")]
        public required string CategoriaOcorrencia { get; set; }
    }

    public class OcorrenciaSubcategoria
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [Column("categoria_id")]
        public required int CategoriaId { get; set; }

        [ForeignKey("CategoriaId")]
        public required OcorrenciaCategoria OcorrenciaCategoria { get; set; }

        [Required(ErrorMessage = "O campo da subcategoria de ocorrencia e obrigatorio.")]
        [Column("subcategoria_ocorrencia", TypeName = "varchar(100)")]
        [StringLength(100, ErrorMessage = "A subcategoria de ocorrencia nao pode exceder 100 caracteres.")]
        public required string SubcategoriaOcorrencia { get; set; }
    }
}
