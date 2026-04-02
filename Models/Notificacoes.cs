using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CallStationApp.Models
{
    public class Notificacao
    {
        public int Id { get; set; }
        public int UsuarioId { get; set; }

        public TipoNotificacao Tipo { get; set; } = TipoNotificacao.Sistema;

        public string Titulo { get; set; } = string.Empty;
        public string Mensagem { get; set; } = string.Empty;

        public bool Lida { get; set; } = false;
        public DateTime DataCriacao { get; set; }
        public DateTime? DataLeitura { get; set; }

        public int? ReferenciaId { get; set; }
        public string? ReferenciaTipo { get; set; }
        public string? LinkDestino { get; set; }

        public Usuario? Usuario { get; set; }
    }

    public enum TipoNotificacao
    {
        Convite,
        Sistema,
        Chamado,
        Tarefa
    }
}