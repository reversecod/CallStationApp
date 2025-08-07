using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CallStationApp.Models
{
    public class OcorrenciaTipo
    {
        [Key] 
        [Column("id")] 
        public int Id { get; set; }

        [Required(ErrorMessage = "O campo do tipo de ocorrência é obrigatório.")]
        [Column("tipo_ocorrencia", TypeName = "varchar(50)")]
        [StringLength(50, ErrorMessage = "O tipo de ocorrência não pode exceder 50 caracteres.")]
        public required string TipoOcorrencia { get; set; }
    
        [Required]
        [Column("usuario_id")] 
        public required int UsuarioId { get; set; }
        [ForeignKey("EmpresaId")] 
        public required Usuario Usuario { get; set; }

        [Column("grupo_id")] 
        public int? GrupoId { get; set; }
        [ForeignKey("GrupoId")] 
        public Grupo? Grupo { get; set; }
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
        
        [Required(ErrorMessage = "O campo da categoria de ocorrência é obrigatório.")]
        [Column("categoria_ocorrencia", TypeName = "varchar(50)")]
        [StringLength(50, ErrorMessage = "A categoria de ocorrência não pode exceder 50 caracteres.")]
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
        
        [Required(ErrorMessage = "O campo da subcategoria de ocorrência é obrigatório.")]
        [Column("subcategoria_ocorrencia", TypeName = "varchar(100)")]
        [StringLength(100, ErrorMessage = "A subcategoria de ocorrência não pode exceder 100 caracteres.")]
        public required string SubcategoriaOcorrencia { get; set; }
    }
}