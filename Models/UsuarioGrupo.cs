using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CallStationApp.Models
{
    public class UsuarioGrupo
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }
            
        [Required]
        [Column("usuario_id")]
        public required int UsuarioId { get; set; }
        [ForeignKey("usuario_id")]
        public required Usuario Usuario { get; set; }
            
        [Required] 
        [Column("grupo_id")]
        public required int GrupoId { get; set; }
        [ForeignKey("grupo_id")]
        public required Grupo Grupo { get; set; }
        
        [Required(ErrorMessage = "Defina uma permissão para o usuário.")]
        [Column("permissao")]
        [EnumDataType(typeof(PermissaoUsuario))]
        public required PermissaoUsuario Permissao { get; set; }
        
        [Column("data_adicao")]
        public DateTime DataAdicao { get; set; } = DateTime.Now;
    }
    public enum PermissaoUsuario
    {
        [Display(Name = "Nenhuma")]
        Nenhuma,
        [Display(Name = "Colaborador")]
        Colaborador,
        [Display(Name = "Técnico")]
        Tecnico,
        [Display(Name = "Administração")]
        Administracao
    }
}