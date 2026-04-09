using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CallStationApp.Models
{
    public class Tarefa
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required(ErrorMessage = "O titulo e obrigatorio.")]
        [Column("titulo", TypeName = "varchar(50)")]
        [StringLength(50, ErrorMessage = "O titulo nao pode exceder 50 caracteres.")]
        public required string Titulo { get; set; }

        [Required(ErrorMessage = "A descricao e obrigatoria.")]
        [Column("descricao", TypeName = "varchar(500)")]
        [StringLength(500, ErrorMessage = "A descricao nao pode exceder 500 caracteres.")]
        public required string Descricao { get; set; }

        [Required]
        [Column("criador_id")]
        public required int CriadorId { get; set; }

        [ForeignKey("CriadorId")]
        public required Usuario Usuario { get; set; }

        [Required]
        [Column("grupo_id")]
        public int GrupoId { get; set; }

        [ForeignKey("GrupoId")]
        public Grupo Grupo { get; set; } = null!;

        [Column("criticidade")]
        [EnumDataType(typeof(CriticidadeTarefa))]
        public CriticidadeTarefa? Criticidade { get; set; }

        [Column("urgencia")]
        [EnumDataType(typeof(UrgenciaTarefa))]
        public UrgenciaTarefa? Urgencia { get; set; }

        [Column("status")]
        [EnumDataType(typeof(StatusTarefa))]
        public StatusTarefa Status { get; set; } = StatusTarefa.Pendente;

        [Column("data_criacao")]
        public DateTime DataCriacao { get; set; } = DateTime.UtcNow;

        [Column("data_conclusao")]
        public DateTime? DataConclusao { get; set; }
    }

    public enum StatusTarefa
    {
        [Display(Name = "Pendente")]
        Pendente,
        [Display(Name = "Em Andamento")]
        EmAndamento,
        [Display(Name = "Concluida")]
        Concluida,
        [Display(Name = "Cancelada")]
        Cancelada
    }

    public enum UrgenciaTarefa
    {
        [Display(Name = "Nao Urgente")]
        NaoUrgente,
        [Display(Name = "Pouca Urgencia")]
        PoucaUrgencia,
        [Display(Name = "Urgente")]
        Urgente,
        [Display(Name = "Emergencia")]
        Emergencia
    }

    public enum CriticidadeTarefa
    {
        [Display(Name = "Baixa Criticidade")]
        Baixa,
        [Display(Name = "Media Criticidade")]
        Media,
        [Display(Name = "Alta Criticidade")]
        Alta,
        [Display(Name = "Critico")]
        Critico
    }
}
