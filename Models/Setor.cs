using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CallStationApp.Models
{
    public class Setor
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }
        
        [Required]
        [Column("nome_setor", TypeName = "varchar(50)")]
        public required string NomeSetor { get; set; }
        
        [Required]
        [Column("usuario_id")]
        public required int UsuarioId { get; set; }
        [ForeignKey("UsuarioId")]
        public required Usuario Usuario { get; set; }
        
        [Column("grupo_id")]
        public int GrupoId { get; set; }
        [ForeignKey("GrupoId")]
        public Grupo? Grupo { get; set; }
    }
}