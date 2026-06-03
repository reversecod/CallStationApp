using CallStationApp.Authorization;
using CallStationApp.Data;
using CallStationApp.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace CallStationApp.Pages.Menu;

[Authorize]
public class DashboardModel : PageModel
{
    private const int LimiteDiasPeriodo = 366;
    private const int MesesPrevisao = 6;
    private readonly AppDbContext _context;
    private readonly GrupoAuthorizationService _grupoAuthorizationService;

    public DashboardModel(AppDbContext context, GrupoAuthorizationService grupoAuthorizationService)
    {
        _context = context;
        _grupoAuthorizationService = grupoAuthorizationService;
    }

    [BindProperty(SupportsGet = true)]
    public int GrupoId { get; set; }

    public Grupo? GrupoAtual { get; set; }
    public GrupoConfiguracao? Configuracao { get; set; }
    public string? NomeUsuarioLogado { get; set; }
    public string? FotoUsuarioLogado { get; set; }
    public bool UsuarioLogadoEhAdministrador { get; set; }
    public PermissaoUsuario UsuarioLogadoPermissao { get; set; } = PermissaoUsuario.Nenhuma;
    public bool UsuarioPodeVerUsuarios { get; set; }
    public List<FiltroOpcaoVm> Setores { get; set; } = new();
    public List<FiltroOpcaoVm> TiposProblema { get; set; } = new();
    public List<FiltroOpcaoVm> Usuarios { get; set; } = new();

    public async Task<IActionResult> OnGetAsync()
    {
        var usuarioId = GetUsuarioLogadoId();
        if (usuarioId == null)
            return RedirectToPage("/Auth/Login");

        if (GrupoId <= 0)
            return RedirectToPage("/Menu/Menu");

        var contexto = await ValidarMembroAsync(usuarioId.Value, GrupoId);
        if (contexto == null || contexto.Permissao == PermissaoUsuario.Nenhuma)
            return Forbid();

        UsuarioLogadoEhAdministrador = contexto.Permissao == PermissaoUsuario.Administracao;
        UsuarioLogadoPermissao = contexto.Permissao;
        UsuarioPodeVerUsuarios = PodeVerDadosCompletos(contexto.Permissao);

        GrupoAtual = await _context.Grupos.AsNoTracking().FirstOrDefaultAsync(g => g.Id == GrupoId);
        if (GrupoAtual == null)
            return RedirectToPage("/Menu/Menu");

        Configuracao = await _context.GruposConfiguracoes
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.GrupoId == GrupoId);

        var usuario = await _context.Usuarios
            .AsNoTracking()
            .Where(u => u.Id == usuarioId.Value)
            .Select(u => new { u.NomeUsuario, u.FotoUsuario })
            .FirstOrDefaultAsync();

        NomeUsuarioLogado = usuario?.NomeUsuario;
        FotoUsuarioLogado = usuario?.FotoUsuario;
        await CarregarFiltrosAsync(contexto.Permissao);
        return Page();
    }

    public async Task<IActionResult> OnPostObterGraficoPrincipalAsync([FromBody] DashboardFiltroRequest request)
    {
        var acesso = await ValidarAcessoDashboardAsync();
        if (acesso.Result != null)
            return acesso.Result;

        if (request == null)
            return BadRequest(new { success = false, message = "Requisição inválida." });

        var filtro = NormalizarFiltro(request, acesso.Contexto!.Permissao, acesso.UsuarioId);
        if (filtro.Erro != null)
            return BadRequest(new { success = false, message = filtro.Erro });

        var query = AplicarFiltros(MontarQueryChamadosVisiveis(acesso.UsuarioId, acesso.Contexto.Permissao), filtro);
        var dimensao = (request.DimensaoPrincipal ?? "evolucao").Trim().ToLowerInvariant();
        object dados;

        if (dimensao == "status")
        {
            var pontosStatus = await query
                .GroupBy(c => c.Status)
                .Select(g => new { Status = g.Key, Total = g.Count() })
                .OrderBy(x => x.Status)
                .ToListAsync();
            var pontos = pontosStatus
                .Select(p => new DashboardPontoVm(p.Status.ToString(), p.Total))
                .ToList();
            dados = new GraficoDashboardVm("bar", "Chamados por status", pontos);
        }
        else if (dimensao == "setor")
        {
            var pontos = await (from chamado in query
                                join setor in _context.Setores.AsNoTracking() on chamado.SetorId equals setor.Id into setorJoin
                                from setor in setorJoin.DefaultIfEmpty()
                                group chamado by setor == null ? "Sem setor" : setor.NomeSetor
                                into g
                                orderby g.Count() descending
                                select new DashboardPontoVm(g.Key, g.Count()))
                .Take(12)
                .ToListAsync();
            dados = new GraficoDashboardVm("bar", "Chamados por setor", pontos);
        }
        else if (dimensao == "tipo")
        {
            var pontos = await (from chamado in query
                                join tipo in _context.OcorrenciasTipo.AsNoTracking() on chamado.OcorrenciaTipoId equals tipo.Id into tipoJoin
                                from tipo in tipoJoin.DefaultIfEmpty()
                                group chamado by tipo == null ? "Sem tipo" : tipo.TipoOcorrencia
                                into g
                                orderby g.Count() descending
                                select new DashboardPontoVm(g.Key, g.Count()))
                .Take(12)
                .ToListAsync();
            dados = new GraficoDashboardVm("bar", "Chamados por tipo de problema", pontos);
        }
        else
        {
            var pontosMes = await query
                .GroupBy(c => new { c.DataCriacao.Year, c.DataCriacao.Month })
                .Select(g => new { g.Key.Year, g.Key.Month, Total = g.Count() })
                .OrderBy(x => x.Year)
                .ThenBy(x => x.Month)
                .Take(24)
                .ToListAsync();

            var pontos = pontosMes
                .Select(p => new DashboardPontoVm($"{p.Year}-{p.Month:00}", p.Total))
                .ToList();
            dados = new GraficoDashboardVm("line", "Evolucao mensal de chamados", pontos);
        }

        return new JsonResult(new { success = true, dados });
    }

    public async Task<IActionResult> OnPostObterResumoDashboardAsync([FromBody] DashboardFiltroRequest request)
    {
        var acesso = await ValidarAcessoDashboardAsync();
        if (acesso.Result != null)
            return acesso.Result;

        if (request == null)
            return BadRequest(new { success = false, message = "Requisição inválida." });

        var filtro = NormalizarFiltro(request, acesso.Contexto!.Permissao, acesso.UsuarioId);
        if (filtro.Erro != null)
            return BadRequest(new { success = false, message = filtro.Erro });

        var query = AplicarFiltros(MontarQueryChamadosVisiveis(acesso.UsuarioId, acesso.Contexto.Permissao), filtro);
        var podeVerUsuarios = PodeVerDadosCompletos(acesso.Contexto.Permissao);
        var reabertosIds = _context.HistoricoStatusChamados
            .AsNoTracking()
            .Where(h => h.StatusNovo == StatusNovoChamado.Reaberto)
            .Select(h => h.ChamadoId)
            .Distinct();

        var inicioMesFiltro = new DateTime(filtro.DataInicial.Year, filtro.DataInicial.Month, 1);
        var fimMesFiltro = inicioMesFiltro.AddMonths(1);
        var indicadoresAgregados = await query
            .GroupBy(_ => 1)
            .Select(g => new
            {
                TotalChamados = g.Count(),
                TotalFechados = g.Sum(c => c.Status == StatusChamado.Concluido || c.Status == StatusChamado.Fechado ? 1 : 0),
                TotalReabertos = g.Sum(c => reabertosIds.Contains(c.Id) &&
                                             (c.Status == StatusChamado.Concluido || c.Status == StatusChamado.Fechado)
                    ? 1
                    : 0),
                TotalMes = g.Sum(c => c.DataCriacao >= inicioMesFiltro && c.DataCriacao < fimMesFiltro ? 1 : 0),
                TempoMedioRespostaMinutos = g.Average(c => c.DataInicioAtendimento.HasValue
                    ? (double?)EF.Functions.DateDiffMinute(c.DataCriacao, c.DataInicioAtendimento.Value)
                    : null)
            })
            .FirstOrDefaultAsync();

        var totalChamados = indicadoresAgregados?.TotalChamados ?? 0;
        var totalFechados = indicadoresAgregados?.TotalFechados ?? 0;
        var totalReabertos = indicadoresAgregados?.TotalReabertos ?? 0;
        var totalMes = indicadoresAgregados?.TotalMes ?? 0;
        var usarSlaBruto = string.Equals(request.TipoSla, "bruto", StringComparison.OrdinalIgnoreCase);
        var tempoMedioSolucao = await CalcularTempoMedioSolucaoAsync(query, usarSlaBruto);
        var tempoMedioResposta = indicadoresAgregados?.TempoMedioRespostaMinutos.HasValue == true
            ? (double?)Math.Round(indicadoresAgregados.TempoMedioRespostaMinutos.Value / 60, 1)
            : null;

        var porSetor = await (from chamado in query
                              join setor in _context.Setores.AsNoTracking() on chamado.SetorId equals setor.Id into setorJoin
                              from setor in setorJoin.DefaultIfEmpty()
                              group chamado by setor == null ? "Sem setor" : setor.NomeSetor
                              into g
                              orderby g.Count() descending
                              select new DashboardPontoVm(g.Key, g.Count()))
            .Take(12)
            .ToListAsync();

        var porTipo = await (from chamado in query
                             join tipo in _context.OcorrenciasTipo.AsNoTracking() on chamado.OcorrenciaTipoId equals tipo.Id into tipoJoin
                             from tipo in tipoJoin.DefaultIfEmpty()
                             group chamado by tipo == null ? "Sem tipo" : tipo.TipoOcorrencia
                             into g
                             orderby g.Count() descending
                             select new DashboardPontoVm(g.Key, g.Count()))
            .Take(12)
            .ToListAsync();

        var statusAgrupados = await query
            .GroupBy(c => c.Status)
            .Select(g => new { Status = g.Key, Total = g.Count() })
            .OrderBy(x => x.Status)
            .ToListAsync();
        var porStatus = statusAgrupados
            .Select(p => new DashboardPontoVm(p.Status.ToString(), p.Total))
            .ToList();

        var porUsuario = podeVerUsuarios
            ? await (from chamado in query
                     join usuarioGrupo in _context.UsuariosGrupos.AsNoTracking()
                         on new { UsuarioId = chamado.CriadorChamadoId, chamado.GrupoId } equals new { usuarioGrupo.UsuarioId, usuarioGrupo.GrupoId }
                     join usuario in _context.Usuarios.AsNoTracking() on chamado.CriadorChamadoId equals usuario.Id
                     join info in _context.InfoUsuariosGrupos.AsNoTracking()
                         on new { chamado.CriadorChamadoId, chamado.GrupoId } equals new { CriadorChamadoId = info.UsuarioId, info.GrupoId } into infoJoin
                     from infoUsuario in infoJoin.DefaultIfEmpty()
                     where usuarioGrupo.Ativo
                     group chamado by infoUsuario != null && !string.IsNullOrWhiteSpace(infoUsuario.Apelido) ? infoUsuario.Apelido! : usuario.NomeUsuario
                     into g
                     orderby g.Count() descending
                     select new DashboardPontoVm(g.Key, g.Count()))
                .Take(12)
                .ToListAsync()
            : new List<DashboardPontoVm>();

        var problemasReabertos = await (from chamado in query
                                        join tipo in _context.OcorrenciasTipo.AsNoTracking() on chamado.OcorrenciaTipoId equals tipo.Id into tipoJoin
                                        from tipo in tipoJoin.DefaultIfEmpty()
                                        where reabertosIds.Contains(chamado.Id)
                                        group chamado by tipo == null ? "Sem tipo" : tipo.TipoOcorrencia
                                        into g
                                        orderby g.Count() descending
                                        select new DashboardPontoVm(g.Key, g.Count()))
            .Take(8)
            .ToListAsync();

        var tarefasPorStatus = await MontarTarefasPorStatusAsync(acesso.UsuarioId, acesso.Contexto.Permissao, filtro);
        var previsao = await MontarPrevisaoAsync(acesso.UsuarioId, acesso.Contexto.Permissao, filtro);

        return new JsonResult(new
        {
            success = true,
            dados = new DashboardResumoVm(
                new DashboardIndicadoresVm(totalChamados, totalFechados, totalReabertos, totalMes, tempoMedioResposta, tempoMedioSolucao),
                porSetor,
                porTipo,
                porStatus,
                porUsuario,
                problemasReabertos,
                tarefasPorStatus,
                previsao,
                podeVerUsuarios,
                usarSlaBruto ? "bruto" : "liquido")
        });
    }

    private async Task CarregarFiltrosAsync(PermissaoUsuario permissao)
    {
        Setores = await _context.Setores
            .AsNoTracking()
            .Where(s => s.GrupoId == GrupoId)
            .OrderBy(s => s.NomeSetor)
            .Select(s => new FiltroOpcaoVm(s.Id, s.NomeSetor))
            .ToListAsync();

        TiposProblema = await _context.OcorrenciasTipo
            .AsNoTracking()
            .Where(t => t.GrupoId == GrupoId)
            .OrderBy(t => t.TipoOcorrencia)
            .Select(t => new FiltroOpcaoVm(t.Id, t.TipoOcorrencia))
            .ToListAsync();

        if (!PodeVerDadosCompletos(permissao))
            return;

        Usuarios = await (from usuarioGrupo in _context.UsuariosGrupos.AsNoTracking()
                          join usuario in _context.Usuarios.AsNoTracking() on usuarioGrupo.UsuarioId equals usuario.Id
                          join info in _context.InfoUsuariosGrupos.AsNoTracking()
                              on new { usuarioGrupo.UsuarioId, usuarioGrupo.GrupoId } equals new { info.UsuarioId, info.GrupoId } into infoJoin
                          from infoUsuario in infoJoin.DefaultIfEmpty()
                          where usuarioGrupo.GrupoId == GrupoId && usuarioGrupo.Ativo
                          orderby infoUsuario != null && !string.IsNullOrWhiteSpace(infoUsuario.Apelido) ? infoUsuario.Apelido : usuario.NomeUsuario
                          select new FiltroOpcaoVm(
                              usuarioGrupo.UsuarioId,
                              infoUsuario != null && !string.IsNullOrWhiteSpace(infoUsuario.Apelido) ? infoUsuario.Apelido! : usuario.NomeUsuario))
            .ToListAsync();
    }

    private IQueryable<Chamado> MontarQueryChamadosVisiveis(int usuarioId, PermissaoUsuario permissao)
    {
        var query = _context.Chamados
            .AsNoTracking()
            .Where(c => c.GrupoId == GrupoId && c.Status != StatusChamado.Excluido);

        if (permissao == PermissaoUsuario.Colaborador)
            query = query.Where(c => c.Publico || c.CriadorChamadoId == usuarioId);

        return query;
    }

    private IQueryable<Chamado> AplicarFiltros(IQueryable<Chamado> query, FiltroNormalizado filtro)
    {
        var dataFinalExclusiva = filtro.DataFinal.Date.AddDays(1);
        query = query.Where(c => c.DataCriacao >= filtro.DataInicial.Date && c.DataCriacao < dataFinalExclusiva);

        if (filtro.Mes.HasValue)
            query = query.Where(c => c.DataCriacao.Year == filtro.DataInicial.Year && c.DataCriacao.Month == filtro.Mes.Value);

        if (filtro.SetorId.HasValue)
            query = query.Where(c => c.SetorId == filtro.SetorId.Value);

        if (filtro.UsuarioId.HasValue)
            query = query.Where(c => c.CriadorChamadoId == filtro.UsuarioId.Value);

        if (filtro.TipoId.HasValue)
            query = query.Where(c => c.OcorrenciaTipoId == filtro.TipoId.Value);

        if (filtro.Status.HasValue)
            query = query.Where(c => c.Status == filtro.Status.Value);

        if (filtro.ApenasFechados)
            query = query.Where(c => c.Status == StatusChamado.Concluido || c.Status == StatusChamado.Fechado);

        if (filtro.ApenasReabertos)
        {
            var reabertosIds = _context.HistoricoStatusChamados
                .AsNoTracking()
                .Where(h => h.StatusNovo == StatusNovoChamado.Reaberto)
                .Select(h => h.ChamadoId)
                .Distinct();
            query = query.Where(c => reabertosIds.Contains(c.Id));
        }

        return query;
    }

    private FiltroNormalizado NormalizarFiltro(DashboardFiltroRequest request, PermissaoUsuario permissao, int usuarioId)
    {
        var hoje = DateTime.UtcNow.Date;
        var dataInicial = request.DataInicial?.Date ?? hoje.AddDays(-29);
        var dataFinal = request.DataFinal?.Date ?? hoje;

        if (request.Mes is < 1 or > 12)
            return FiltroNormalizado.Invalido("Mês inválido.");

        if (request.Mes.HasValue && !request.DataInicial.HasValue && !request.DataFinal.HasValue)
        {
            dataInicial = new DateTime(hoje.Year, request.Mes.Value, 1);
            dataFinal = dataInicial.AddMonths(1).AddDays(-1);
        }

        if (dataFinal < dataInicial)
            return FiltroNormalizado.Invalido("Data final deve ser maior ou igual à data inicial.");

        if ((dataFinal - dataInicial).TotalDays > LimiteDiasPeriodo)
            return FiltroNormalizado.Invalido("Período máximo permitido: 366 dias.");

        int? usuarioFiltro = PodeVerDadosCompletos(permissao) ? request.UsuarioId : null;

        return new FiltroNormalizado(
            dataInicial,
            dataFinal,
            request.Mes,
            request.SetorId,
            usuarioFiltro,
            request.TipoId,
            Enum.TryParse<StatusChamado>(request.Status, out var status) ? status : null,
            request.ApenasFechados,
            request.ApenasReabertos,
            request.IncluirPrevisao,
            null);
    }

    private async Task<double?> CalcularTempoMedioSolucaoAsync(IQueryable<Chamado> query, bool usarSlaBruto)
    {
        var chamados = await query
            .Where(c => c.DataFinalizacao.HasValue)
            .Select(c => new { c.Id, c.DataCriacao, DataFinalizacao = c.DataFinalizacao!.Value })
            .ToListAsync();

        if (chamados.Count == 0)
            return null;

        var ids = chamados.Select(c => c.Id).ToList();
        var pausasPorChamado = usarSlaBruto
            ? new Dictionary<int, long>()
            : await _context.ChamadosPeriodosPendentes
                .AsNoTracking()
                .Where(p => ids.Contains(p.ChamadoId))
                .GroupBy(p => p.ChamadoId)
                .Select(g => new
                {
                    ChamadoId = g.Key,
                    Segundos = g.Sum(p => p.DuracaoSegundos ?? (p.FimPendente.HasValue ? EF.Functions.DateDiffSecond(p.InicioPendente, p.FimPendente.Value) : 0))
                })
                .ToDictionaryAsync(x => x.ChamadoId, x => x.Segundos);

        var mediaHoras = chamados
            .Select(c =>
            {
                var brutoSegundos = Math.Max(0, (c.DataFinalizacao - c.DataCriacao).TotalSeconds);
                var pausaSegundos = pausasPorChamado.GetValueOrDefault(c.Id);
                return Math.Max(0, brutoSegundos - pausaSegundos) / 3600d;
            })
            .Average();

        return Math.Round(mediaHoras, 1);
    }

    private async Task<List<DashboardPontoVm>> MontarTarefasPorStatusAsync(int usuarioId, PermissaoUsuario permissao, FiltroNormalizado filtro)
    {
        var dataFinalExclusiva = filtro.DataFinal.Date.AddDays(1);
        var query = _context.CartoesTarefas
            .AsNoTracking()
            .Where(c => c.GrupoId == GrupoId && c.DataCriacao >= filtro.DataInicial.Date && c.DataCriacao < dataFinalExclusiva);

        if (permissao == PermissaoUsuario.Colaborador)
            query = query.Where(c => !c.Privado || c.CriadorId == usuarioId || c.ResponsavelUsuarioId == usuarioId);

        var statusAgrupados = await query
            .GroupBy(c => c.Status)
            .Select(g => new { Status = g.Key, Total = g.Count() })
            .OrderBy(x => x.Status)
            .ToListAsync();

        return statusAgrupados
            .Select(p => new DashboardPontoVm(p.Status.ToString(), p.Total))
            .ToList();
    }

    private async Task<DashboardPrevisaoVm> MontarPrevisaoAsync(int usuarioId, PermissaoUsuario permissao, FiltroNormalizado filtro)
    {
        if (!filtro.IncluirPrevisao)
            return new DashboardPrevisaoVm(false, "Previsao desativada nos filtros.", null, null, new List<DashboardPontoVm>());

        var dataInicioHistorico = filtro.DataFinal.Date.AddMonths(-MesesPrevisao + 1);
        var query = MontarQueryChamadosVisiveis(usuarioId, permissao)
            .Where(c => c.DataCriacao >= dataInicioHistorico && c.DataCriacao <= filtro.DataFinal.Date.AddDays(1));

        var pontosMes = await query
            .GroupBy(c => new { c.DataCriacao.Year, c.DataCriacao.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Total = g.Count() })
            .OrderBy(x => x.Year)
            .ThenBy(x => x.Month)
            .ToListAsync();

        var pontos = pontosMes
            .Select(p => new DashboardPontoVm($"{p.Year}-{p.Month:00}", p.Total))
            .ToList();

        if (pontos.Count < 3)
            return new DashboardPrevisaoVm(false, "Nao ha dados historicos suficientes para uma previsao confiavel.", null, null, pontos);

        var media = pontos.Average(p => p.Valor);
        var tendencia = pontos.Last().Valor - pontos.First().Valor;
        var estimativa = Math.Max(0, Math.Round(media + (tendencia / Math.Max(pontos.Count - 1, 1)), 0));
        var confiabilidade = pontos.Count >= MesesPrevisao ? "media" : "baixa";
        return new DashboardPrevisaoVm(true, "Estimativa simples baseada nos ultimos meses visiveis.", estimativa, confiabilidade, pontos);
    }

    private async Task<(int UsuarioId, GrupoMemberContext? Contexto, IActionResult? Result)> ValidarAcessoDashboardAsync()
    {
        var usuarioId = GetUsuarioLogadoId();
        if (usuarioId == null)
            return (0, null, new JsonResult(new { success = false, message = "Usuário não autenticado." }) { StatusCode = StatusCodes.Status401Unauthorized });

        if (GrupoId <= 0)
            return (usuarioId.Value, null, BadRequest(new { success = false, message = "Grupo inválido." }));

        var contexto = await ValidarMembroAsync(usuarioId.Value, GrupoId);
        if (contexto == null || contexto.Permissao == PermissaoUsuario.Nenhuma)
            return (usuarioId.Value, contexto, new JsonResult(new { success = false, message = "Acesso negado ao dashboard." }) { StatusCode = StatusCodes.Status403Forbidden });

        return (usuarioId.Value, contexto, null);
    }

    private async Task<GrupoMemberContext?> ValidarMembroAsync(int usuarioId, int grupoId) =>
        await _grupoAuthorizationService.ObterContextoMembroAsync(usuarioId, grupoId);

    private int? GetUsuarioLogadoId()
    {
        var claim = User.FindFirst("Id")?.Value;
        return int.TryParse(claim, out var id) ? id : null;
    }

    private static bool PodeVerDadosCompletos(PermissaoUsuario permissao) =>
        permissao == PermissaoUsuario.Administracao || permissao == PermissaoUsuario.Tecnico;

    public record DashboardFiltroRequest(DateTime? DataInicial, DateTime? DataFinal, int? Mes, int? SetorId, int? UsuarioId, int? TipoId, string? Status, bool ApenasFechados, bool ApenasReabertos, bool IncluirPrevisao, string? DimensaoPrincipal, string? MetricaCruzada, string? TipoSla);
    public record FiltroOpcaoVm(int Id, string Nome);
    public record DashboardPontoVm(string Label, double Valor);
    public record GraficoDashboardVm(string Tipo, string Titulo, List<DashboardPontoVm> Pontos);
    public record DashboardIndicadoresVm(int TotalChamados, int TotalFechados, int TotalReabertos, int TotalMes, double? TempoMedioRespostaHoras, double? TempoMedioSolucaoHoras);
    public record DashboardPrevisaoVm(bool Disponivel, string Mensagem, double? EstimativaProximoMes, string? Confiabilidade, List<DashboardPontoVm> Historico);
    public record DashboardResumoVm(DashboardIndicadoresVm Indicadores, List<DashboardPontoVm> PorSetor, List<DashboardPontoVm> PorTipo, List<DashboardPontoVm> PorStatus, List<DashboardPontoVm> PorUsuario, List<DashboardPontoVm> ProblemasReabertos, List<DashboardPontoVm> TarefasPorStatus, DashboardPrevisaoVm Previsao, bool PodeVerUsuarios, string TipoSla);
    private record FiltroNormalizado(DateTime DataInicial, DateTime DataFinal, int? Mes, int? SetorId, int? UsuarioId, int? TipoId, StatusChamado? Status, bool ApenasFechados, bool ApenasReabertos, bool IncluirPrevisao, string? Erro)
    {
        public static FiltroNormalizado Invalido(string erro) => new(DateTime.UtcNow.Date, DateTime.UtcNow.Date, null, null, null, null, null, false, false, false, erro);
    }
}
