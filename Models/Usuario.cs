using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CallStationApp.Models
{
    public class Usuario
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required(ErrorMessage = "O nome completo é obrigatório.")]
        [StringLength(100, ErrorMessage = "O nome completo não pode exceder 100 caracteres.")]
        [Column("nome_completo", TypeName = "varchar(100)")]
        public required string NomeCompleto { get; set; }

        [Required(ErrorMessage = "O nome de usuário é obrigatório.")]
        [StringLength(100, ErrorMessage = "O nome de usuário não pode exceder 100 caracteres.")]
        [Column("usuario", TypeName = "varchar(100)")]
        public required string NomeUsuario { get; set; }

        [EmailAddress(ErrorMessage = "O e-mail informado não é válido.")]
        [StringLength(100, ErrorMessage = "O e-mail não pode exceder 100 caracteres.")]
        [Column("email", TypeName = "varchar(100)")]
        public string? Email { get; set; }

        [Required(ErrorMessage = "Defina uma senha para o usuário.")]
        [StringLength(255, ErrorMessage = "A senha não pode exceder 255 caracteres.")]
        [Column("senha", TypeName = "varchar(255)")]
        public required string Senha { get; set; }

        [StringLength(255, ErrorMessage = "A URL da foto não pode exceder 255 caracteres.")]
        [Column("foto_usuario", TypeName = "varchar(255)")]
        public string? FotoUsuario { get; set; }
    }
}