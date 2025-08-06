using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CallStationApp.Models
{
    public class Usuario
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }
        
        [Required]
        [Column("nome_completo", TypeName = "varchar(100)")]
        public required string NomeCompleto { get; set; }
        
        [Required] 
        [Column("usuario", TypeName = "varchar(100)")]
        public required string NomeUsuario { get; set; }
        
        [Column("email", TypeName = "varchar(100)")]
        public string? Email { get; set; }
        
        [Required]
        [Column("senha", TypeName = "varchar(255)")]
        public required string Senha { get; set; }
        
        [Column("foto_usuario", TypeName = "varchar(255)")]
        public string? FotoUsuario { get; set; }
    }
}