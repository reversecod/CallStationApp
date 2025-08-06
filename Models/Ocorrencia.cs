using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CallStationApp.Models
{
    public class OcorrenciaTipo
    {
        [Key] 
        [Column("id")] 
        public int Id { get; set; }

        [Required]
        [Column("tipo_ocorrencia", TypeName = "varchar(50)")]
        public required string TipoOcorrencia { get; set; }

        [Column("usuario_id")] 
        public int UsuarioId { get; set; }
        [ForeignKey("EmpresaId")] 
        public required Usuario? Usuario { get; set; }

        [Column("grupo_id")] 
        public int GrupoId { get; set; }
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
        public int TipoId { get; set; }
        [ForeignKey("TipoId")] 
        public required OcorrenciaTipo? OcorrenciaTipo { get; set; }
        
        [Required]
        [Column("categoria_ocorrencia", TypeName = "varchar(50)")]
        public required string CategoriaOcorrencia { get; set; }
    }
    public class OcorrenciaSubcategoria
    {
        [Key] 
        [Column("id")] 
        public int Id { get; set; }

        [Required]
        [Column("categoria_id")] 
        public int CategoriaId { get; set; }
        [ForeignKey("CategoriaId")] 
        public required OcorrenciaCategoria? OcorrenciaCategoria { get; set; }
        
        [Required]
        [Column("subcategoria_ocorrencia", TypeName = "varchar(100)")]
        public required string SubcategoriaOcorrencia { get; set; }
    }
}