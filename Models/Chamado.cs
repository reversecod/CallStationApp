using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CallStationApp.Models
{
    public class Chamado
    {
        [Key]
        [Column("id")]
        public int Id { get; set; } 
        
        [Column("titulo", TypeName = "varchar(50)")]
        public string? Titulo { get; set; }
        
        [Column("descricao", TypeName = "text")]
        public string? Descricao { get; set; }
        
        [Column("solucao", TypeName = "text")]
        public string? Solucao { get; set; }
        
        [Column("empresa_id")]
        public int EmpresaId { get; set; }
        [ForeignKey("EmpresaId")]
        public Empresa? Empresa { get; set; }
        
        [Column("ocorrencia_id")]
        public int OcorrenciaId { get; set; }
        [ForeignKey("OcorrenciaId")]
        public Ocorrencia? Ocorrencia { get; set; }
        
        [Column("urgencia_id")]
        public int UrgenciaId { get; set; }
        [ForeignKey("UrgenciaId")]
        public Urgencia? Urgencia { get; set; }
        
        [Column("setor_id")]
        public int SetorId { get; set; }
        [ForeignKey("SetorId")]
        public Setor? Setor { get; set; }
        
        [Column("criador_solicitacao", TypeName = "varchar(100)")]
        public string? CriadorSolicitacao { get; set; }
        
        [Column("criador_chamado", TypeName = "varchar(100)")]
        public string? CriadorChamado { get; set; }
        
        [Column("status")]
        [EnumDataType(typeof(StatusChamado))]
        public StatusChamado StatusAtual { get; set; } // Assuming Status is an enum defined elsewhere
        
        [Column("data_criacao")]
        public DateTime DataCriacao { get; set; }
        
        [Column("data_finalizacao")]
        public DateTime? DataFinalizacao { get; set; }
    }
    public enum StatusChamado
    {
        Aberto,
        EmAndamento,
        Concluido,
        Cancelado
    }
}