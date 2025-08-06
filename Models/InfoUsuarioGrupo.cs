using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CallStationApp.Models
{ 
    public class InfoUsuarioGrupo
    {
        [Key] 
        [Column("id")] 
        public int Id { get; set; }
        
        [Required]
        [Column("usuario_id")]
        public required string UsuarioId { get; set; }
        [ForeignKey("UsuarioId")]
        public Usuario? Usuario { get; set; }
        
        [Column("grupo_id")]
        public string GrupoId { get; set; }
        [ForeignKey("GrupoId")]
        public Grupo? Grupo { get; set; }

        [Column("apelido", TypeName = "varchar(100)")]
        public string? Apelido { get; set; }
        
        [Column("descricao_ativo")]
        public string? DescricaoAtivo { get; set; }
        
        [Column("identificador_interno", TypeName = "varchar(50)")]
        public string? IdentificadorInterno { get; set; }
        
        [Column("observacao")]
        public string? Observacao { get; set; }
        
        [Column("data_atualizacao_ativo")]
        public DateTime? DataAtualizacaoAtivo { get; set; } = null;
        
        [Column("data_atualizacao_registro")]
        public DateTime? DataAtualizacaoRegistro { get; set; } = DateTime.Now;

    }
}