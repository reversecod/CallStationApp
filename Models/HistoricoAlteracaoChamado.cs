using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CallStationApp.Models;

public class HistoricoAlteracaoChamado
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Required]
    [Column("chamado_id")]
    public int ChamadoId { get; set; }

    [Required]
    [Column("grupo_id")]
    public int GrupoId { get; set; }

    [Required]
    [Column("usuario_id")]
    public int UsuarioId { get; set; }

    [Required]
    [StringLength(100)]
    [Column("campo_alterado")]
    public string CampoAlterado { get; set; } = string.Empty;

    [StringLength(500)]
    [Column("valor_anterior")]
    public string? ValorAnterior { get; set; }

    [StringLength(500)]
    [Column("valor_alterado")]
    public string? ValorAlterado { get; set; }

    [Required]
    [StringLength(50)]
    [Column("tipo_alteracao")]
    public string TipoAlteracao { get; set; } = string.Empty;

    [Required]
    [Column("data_alteracao")]
    public DateTime DataAlteracao { get; set; }

    [ForeignKey(nameof(ChamadoId))]
    public Chamado Chamado { get; set; } = null!;

    [ForeignKey(nameof(GrupoId))]
    public Grupo Grupo { get; set; } = null!;

    [ForeignKey(nameof(UsuarioId))]
    public Usuario Usuario { get; set; } = null!;
}