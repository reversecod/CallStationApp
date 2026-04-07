using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CallStationApp.Models
{
    [Table("Convites_grupo")]
    public class ConviteGrupo
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [Column("grupo_id")]
        public int GrupoId { get; set; }

        [Required]
        [Column("remetente_usuario_id")]
        public int RemetenteUsuarioId { get; set; }

        [Required]
        [Column("destinatario_usuario_id")]
        public int DestinatarioUsuarioId { get; set; }

        [Required]
        [Column("status")]
        public StatusConviteGrupo Status { get; set; } = StatusConviteGrupo.Pendente;

        [Column("mensagem", TypeName = "varchar(255)")]
        [StringLength(255)]
        public string? Mensagem { get; set; }

        [Column("data_criacao")]
        public DateTime DataCriacao { get; set; } = DateTime.UtcNow;

        [Column("data_resposta")]
        public DateTime? DataResposta { get; set; }

        [ForeignKey(nameof(GrupoId))]
        public Grupo? Grupo { get; set; }

        [ForeignKey(nameof(RemetenteUsuarioId))]
        public Usuario? RemetenteUsuario { get; set; }

        [ForeignKey(nameof(DestinatarioUsuarioId))]
        public Usuario? DestinatarioUsuario { get; set; }
    }

    public enum StatusConviteGrupo
    {
        Pendente,
        Aceito,
        Recusado,
        Cancelado
    }
}