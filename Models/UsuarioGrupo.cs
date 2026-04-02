using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CallStationApp.Models
{
    public class UsuarioGrupo
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

        [Required(ErrorMessage = "Defina uma permissão para o usuário.")]
        [Column("permissao")]
        [EnumDataType(typeof(PermissaoUsuario))]
        public PermissaoUsuario Permissao { get; set; } = PermissaoUsuario.Nenhuma;

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