using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CallStationApp.Models
{
    [Table("Notificacoes")]
    public class Notificacao
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [Column("usuario_id")]
        public int UsuarioId { get; set; }

        [Required]
        [Column("tipo")]
        public TipoNotificacao Tipo { get; set; } = TipoNotificacao.Sistema;

        [Required]
        [Column("titulo", TypeName = "varchar(150)")]
        [StringLength(150)]
        public string Titulo { get; set; } = string.Empty;

        [Required]
        [Column("mensagem", TypeName = "varchar(500)")]
        [StringLength(500)]
        public string Mensagem { get; set; } = string.Empty;

        [Column("lida")]
        public bool Lida { get; set; } = false;

        [Column("data_criacao")]
        public DateTime DataCriacao { get; set; } = DateTime.UtcNow;

        [Column("data_leitura")]
        public DateTime? DataLeitura { get; set; }

        [Column("referencia_id")]
        public int? ReferenciaId { get; set; }

        [Column("referencia_tipo", TypeName = "varchar(50)")]
        [StringLength(50)]
        public string? ReferenciaTipo { get; set; }

        [Column("link_destino", TypeName = "varchar(255)")]
        [StringLength(255)]
        public string? LinkDestino { get; set; }

        [ForeignKey(nameof(UsuarioId))]
        public Usuario? Usuario { get; set; }
    }

    public enum TipoNotificacao
    {
        ConviteGrupo,
        Sistema,
        Chamado,
        Tarefa
    }
}