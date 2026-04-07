using CallStationApp.Models;

namespace CallStationApp.Authorization;

public static class GrupoPermissionService
{
    public static bool PodeCriarChamado(PermissaoUsuario permissao) =>
        permissao == PermissaoUsuario.Administracao ||
        permissao == PermissaoUsuario.Tecnico ||
        permissao == PermissaoUsuario.Colaborador;

    public static bool PodeExcluirChamado(
    PermissaoUsuario permissao,
    int usuarioLogadoId,
    int criadorChamadoId)
    {
        if (permissao == PermissaoUsuario.Administracao || permissao == PermissaoUsuario.Tecnico)
            return true;

        if (permissao == PermissaoUsuario.Colaborador)
            return usuarioLogadoId == criadorChamadoId;

        return false;
    }

    public static bool PodeVerChamado(
        PermissaoUsuario permissao,
        bool chamadoPublico,
        int usuarioLogadoId,
        int criadorChamadoId)
    {
        if (permissao == PermissaoUsuario.Administracao || permissao == PermissaoUsuario.Tecnico)
            return true;

        if (permissao == PermissaoUsuario.Colaborador)
            return chamadoPublico || usuarioLogadoId == criadorChamadoId;

        if (permissao == PermissaoUsuario.Nenhuma)
            return chamadoPublico;

        return false;
    }

    public static bool PodeEditarCampoChamado(
        PermissaoUsuario permissao,
        ChamadoCampoEditavel campo,
        int usuarioLogadoId,
        int criadorChamadoId)
    {
        if (permissao == PermissaoUsuario.Administracao)
            return true;

        if (permissao == PermissaoUsuario.Tecnico)
            return CampoPermitidoParaTecnico(campo);

        if (permissao == PermissaoUsuario.Colaborador)
            return usuarioLogadoId == criadorChamadoId && CampoPermitidoParaColaborador(campo);

        return false;
    }

    private static bool CampoPermitidoParaColaborador(ChamadoCampoEditavel campo) =>
        campo == ChamadoCampoEditavel.Titulo ||
        campo == ChamadoCampoEditavel.Descricao ||
        campo == ChamadoCampoEditavel.AnexoChamado;

    private static bool CampoPermitidoParaTecnico(ChamadoCampoEditavel campo) =>
        campo == ChamadoCampoEditavel.Titulo ||
        campo == ChamadoCampoEditavel.Descricao ||
        campo == ChamadoCampoEditavel.AnexoChamado ||
        campo == ChamadoCampoEditavel.Solucao ||
        campo == ChamadoCampoEditavel.OcorrenciaTipoId ||
        campo == ChamadoCampoEditavel.OcorrenciaCategoriaId ||
        campo == ChamadoCampoEditavel.OcorrenciaSubcategoriaId ||
        campo == ChamadoCampoEditavel.SetorId ||
        campo == ChamadoCampoEditavel.Prioridade ||
        campo == ChamadoCampoEditavel.Criticidade ||
        campo == ChamadoCampoEditavel.Urgencia ||
        campo == ChamadoCampoEditavel.Status ||
        campo == ChamadoCampoEditavel.DataFinalizacao ||
        campo == ChamadoCampoEditavel.PrazoResposta ||
        campo == ChamadoCampoEditavel.PrazoConclusao ||
        campo == ChamadoCampoEditavel.Publico;
}