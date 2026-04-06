using System;

namespace CallStationApp.Authorization;

public enum ChamadoCampoEditavel
{
    Titulo = 1,
    Descricao = 2,
    Solucao = 3,

    OcorrenciaTipoId = 4,
    OcorrenciaCategoriaId = 5,
    OcorrenciaSubcategoriaId = 6,
    SetorId = 7,

    AnexoChamado = 8,
    Prioridade = 9,
    Criticidade = 10,
    Urgencia = 11,

    Status = 12,
    DataFinalizacao = 13,
    PrazoResposta = 14,
    PrazoConclusao = 15,
    Publico = 16
}
