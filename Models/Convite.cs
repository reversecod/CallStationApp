using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CallStationApp.Models
{
    public class ConviteGrupo
    {
        public int Id { get; set; }
        public int GrupoId { get; set; }
        public int RemetenteUsuarioId { get; set; }
        public int DestinatarioUsuarioId { get; set; }

        public StatusConviteGrupo Status { get; set; } = StatusConviteGrupo.Pendente;

        public string? Mensagem { get; set; }
        public DateTime DataCriacao { get; set; }
        public DateTime? DataResposta { get; set; }

        public Grupo? Grupo { get; set; }
        public Usuario? RemetenteUsuario { get; set; }
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