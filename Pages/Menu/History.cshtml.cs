using CallStationApp.Authorization;
using CallStationApp.Data;
using CallStationApp.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace CallStationApp.Pages.Menu;

[Authorize]
public class HistoryModel : PageModel
{
    private const int TamanhoPagina = 20;
    private const string ReferenciaTipoComentarioHistorico = "ComentarioHistoricoChamado";
    private const string ReferenciaTipoComentarioChamado = "ComentarioChamado";
    private static readonly StatusChamado[] StatusFinais =
    {
        StatusChamado.Concluido,
        StatusChamado.Fechado,
        StatusChamado.Cancelado
    };

    private static readonly StatusChamado[] StatusAlteraveisHistorico =
    {
        StatusChamado.Concluido,
        StatusChamado.Fechado,
        StatusChamado.Reaberto,
        StatusChamado.Cancelado
    };

    private readonly AppDbContext _context;
    private readonly GrupoAuthorizationService _grupoAuthorizationService;

    public HistoryModel(AppDbContext context, GrupoAuthorizationService grupoAuthorizationService)
    {
        _context = context;
        _grupoAuthorizationService = grupoAuthorizationService;
    }

    [BindProperty(SupportsGet = true)]
    public int GrupoId { get; set; }

    [BindProperty(SupportsGet = true, Name = "page")]
    public int PaginaAtual { get; set; } = 1;

    public string? NomeUsuarioLogado { get; set; }
    public string? FotoUsuarioLogado { get; set; }
    public bool UsuarioLogadoEhAdministrador { get; set; }
    public PermissaoUsuario UsuarioLogadoPermissao { get; set; } = PermissaoUsuario.Nenhuma;
    public bool UsuarioLogadoPodeComentarHistorico { get; set; }
    public bool UsuarioLogadoPodeAlterarStatusHistorico { get; set; }
    public Grupo? GrupoAtual { get; set; }
    public List<HistoricoChamadoVm> Chamados { get; set; } = new();
    public bool TemPaginaAnterior => PaginaAtual > 1;
    public bool TemProximaPagina { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var usuarioId = GetUsuarioLogadoId();
        if (usuarioId == null)
            return RedirectToPage("/Auth/Login");

        if (GrupoId <= 0)
            return RedirectToPage("/Menu/Menu");

        var contextoMembro = await ValidarMembroAsync(usuarioId.Value, GrupoId);
        if (contextoMembro == null)
            return Forbid();

        UsuarioLogadoEhAdministrador = contextoMembro.Permissao == PermissaoUsuario.Administracao;
        UsuarioLogadoPermissao = contextoMembro.Permissao;
        if (contextoMembro.Permissao == PermissaoUsuario.Nenhuma)
            return Forbid();

        UsuarioLogadoPodeComentarHistorico = PodeGerenciarChamadoNoHistorico(contextoMembro.Permissao);
        UsuarioLogadoPodeAlterarStatusHistorico = PodeGerenciarChamadoNoHistorico(contextoMembro.Permissao);

        GrupoAtual = await _context.Grupos
            .AsNoTracking()
            .FirstOrDefaultAsync(g => g.Id == GrupoId);

        if (GrupoAtual == null)
            return RedirectToPage("/Menu/Menu");

        var usuario = await _context.Usuarios
            .AsNoTracking()
            .Where(u => u.Id == usuarioId.Value)
            .Select(u => new { u.NomeUsuario, u.FotoUsuario })
            .FirstOrDefaultAsync();

        NomeUsuarioLogado = usuario?.NomeUsuario;
        FotoUsuarioLogado = usuario?.FotoUsuario;

        var query = _context.Chamados
            .AsNoTracking()
            .Where(c => c.GrupoId == GrupoId &&
                        c.Status != StatusChamado.Excluido &&
                        StatusFinais.Contains(c.Status));

        var queryComVisibilidade = query.Select(c => new
        {
            Chamado = c,
            PublicoHistorico = _context.HistoricoAlteracoesChamado
                .Where(h => h.ChamadoId == c.Id && h.CampoAlterado == "Publico")
                .OrderByDescending(h => h.DataAlteracao)
                .Select(h => h.ValorAlterado)
                .FirstOrDefault()
        });

        if (contextoMembro.Permissao == PermissaoUsuario.Colaborador)
        {
            queryComVisibilidade = queryComVisibilidade.Where(x =>
                x.Chamado.CriadorChamadoId == usuarioId.Value ||
                (
                    x.PublicoHistorico == null
                        ? x.Chamado.Publico
                        : x.PublicoHistorico == "True" ||
                          x.PublicoHistorico == "true" ||
                          x.PublicoHistorico == "1"
                ));
        }
        else if (contextoMembro.Permissao == PermissaoUsuario.Nenhuma)
        {
            queryComVisibilidade = queryComVisibilidade.Where(x =>
                x.PublicoHistorico == null
                    ? x.Chamado.Publico
                    : x.PublicoHistorico == "True" ||
                      x.PublicoHistorico == "true" ||
                      x.PublicoHistorico == "1");
        }

        var chamadosPagina = await queryComVisibilidade
            .OrderBy(x => x.Chamado.NumeroChamadoGrupo)
            .ThenBy(x => x.Chamado.Id)
            .Skip((Math.Max(PaginaAtual, 1) - 1) * TamanhoPagina)
            .Take(TamanhoPagina + 1)
            .Select(x => new
            {
                x.Chamado.Id,
                x.Chamado.GrupoId,
                x.Chamado.NumeroChamadoGrupo,
                x.Chamado.Titulo,
                x.Chamado.Status,
                x.Chamado.DataCriacao,
                x.Chamado.DataFinalizacao,
                x.Chamado.CriadorChamadoId
            })
            .ToListAsync();

        var chamadosPaginaIds = chamadosPagina.Select(c => c.Id).ToList();
        var criadorIds = chamadosPagina.Select(c => c.CriadorChamadoId).Distinct().ToList();

        var historicosFinais = chamadosPaginaIds.Count == 0
            ? new List<HistoricoFinalResumo>()
            : await _context.HistoricoStatusChamados
                .AsNoTracking()
                .Where(h => chamadosPaginaIds.Contains(h.ChamadoId) &&
                            (h.StatusNovo == StatusNovoChamado.Concluido ||
                             h.StatusNovo == StatusNovoChamado.Fechado ||
                             h.StatusNovo == StatusNovoChamado.Cancelado))
                .OrderByDescending(h => h.DataTransicao)
                .Select(h => new HistoricoFinalResumo
                {
                    ChamadoId = h.ChamadoId,
                    DataTransicao = h.DataTransicao,
                    UsuarioId = h.UsuarioId,
                    OrigemAutomatica = h.OrigemAutomatica
                })
                .ToListAsync();

        var ultimoHistoricoFinalPorChamado = historicosFinais
            .GroupBy(h => h.ChamadoId)
            .ToDictionary(g => g.Key, g => g.First());

        var criadoresPorId = criadorIds.Count == 0
            ? new Dictionary<int, string>()
            : await (
                    from usuarioGrupo in _context.UsuariosGrupos.AsNoTracking()
                    join usuarioCriador in _context.Usuarios.AsNoTracking() on usuarioGrupo.UsuarioId equals usuarioCriador.Id
                    join info in _context.InfoUsuariosGrupos.AsNoTracking()
                        on new { usuarioGrupo.UsuarioId, usuarioGrupo.GrupoId } equals new { info.UsuarioId, info.GrupoId } into infoJoin
                    from infoUsuario in infoJoin.DefaultIfEmpty()
                    where usuarioGrupo.GrupoId == GrupoId && criadorIds.Contains(usuarioGrupo.UsuarioId)
                    select new
                    {
                        usuarioGrupo.UsuarioId,
                        Nome = infoUsuario != null && !string.IsNullOrWhiteSpace(infoUsuario.Apelido)
                            ? infoUsuario.Apelido!
                            : usuarioCriador.NomeUsuario
                    })
                .ToDictionaryAsync(x => x.UsuarioId, x => x.Nome);

        var finalizadorIds = historicosFinais
            .Where(h => !h.OrigemAutomatica && h.UsuarioId.HasValue)
            .Select(h => h.UsuarioId!.Value)
            .Distinct()
            .ToList();

        var finalizadoresPorId = finalizadorIds.Count == 0
            ? new Dictionary<int, string>()
            : await _context.Usuarios
                .AsNoTracking()
                .Where(u => finalizadorIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u.NomeUsuario);

        Chamados = chamadosPagina
            .Select(chamado =>
            {
                ultimoHistoricoFinalPorChamado.TryGetValue(chamado.Id, out var historicoFinal);
                var finalizadoPor = "Não registrado";

                if (historicoFinal != null)
                {
                    finalizadoPor = historicoFinal.OrigemAutomatica
                        ? "Sistema"
                        : historicoFinal.UsuarioId.HasValue
                            ? finalizadoresPorId.GetValueOrDefault(historicoFinal.UsuarioId.Value, "Não registrado")
                            : "Não registrado";
                }

                return new HistoricoChamadoVm
                {
                    Id = chamado.Id,
                    NumeroChamadoGrupo = chamado.NumeroChamadoGrupo,
                    Titulo = chamado.Titulo,
                    Status = chamado.Status,
                    DataCriacao = chamado.DataCriacao,
                    DataFinal = historicoFinal?.DataTransicao ?? chamado.DataFinalizacao,
                    CriadoPor = criadoresPorId.GetValueOrDefault(chamado.CriadorChamadoId, "Não registrado"),
                    FinalizadoPor = finalizadoPor
                };
            })
            .ToList();

        TemProximaPagina = Chamados.Count > TamanhoPagina;
        if (TemProximaPagina)
            Chamados.RemoveAt(Chamados.Count - 1);

        if (Chamados.Count > 0)
        {
            var chamadosIds = Chamados.Select(c => c.Id).ToList();
            var chamadosComComentariosNaoLidos = await _context.Notificacoes
                .AsNoTracking()
                .Where(n => n.UsuarioId == usuarioId.Value &&
                            n.GrupoId == GrupoId &&
                            !n.Lida &&
                            n.Tipo == TipoNotificacao.Chamado &&
                            (n.ReferenciaTipo == ReferenciaTipoComentarioHistorico ||
                             n.ReferenciaTipo == ReferenciaTipoComentarioChamado) &&
                            n.ReferenciaId.HasValue &&
                            chamadosIds.Contains(n.ReferenciaId.Value))
                .Select(n => n.ReferenciaId!.Value)
                .Distinct()
                .ToListAsync();

            var chamadosComComentariosNaoLidosSet = chamadosComComentariosNaoLidos.ToHashSet();
            foreach (var chamado in Chamados)
            {
                chamado.TemComentariosNaoLidos = chamadosComComentariosNaoLidosSet.Contains(chamado.Id);
            }
        }

        return Page();
    }

    public async Task<IActionResult> OnGetComentariosAsync(int chamadoId)
    {
        var usuarioId = GetUsuarioLogadoId();
        if (usuarioId == null)
            return Unauthorized();

        var resultadoAcesso = await ObterChamadoHistoricoComAcessoAsync(usuarioId.Value, GrupoId, chamadoId);
        if (!resultadoAcesso.Success)
            return new JsonResult(new { success = false, message = resultadoAcesso.Message });

        var chamado = resultadoAcesso.Chamado!;
        var contextoMembro = resultadoAcesso.ContextoMembro!;

        var comentarios = await (
            from comentario in _context.ComentariosChamados.AsNoTracking()
            join usuario in _context.Usuarios.AsNoTracking()
                on comentario.UsuarioId equals usuario.Id
            join info in _context.InfoUsuariosGrupos.AsNoTracking()
                on new { UsuarioId = comentario.UsuarioId, GrupoId = chamado.GrupoId }
                equals new { info.UsuarioId, info.GrupoId } into infoJoin
            from infoUsuario in infoJoin.DefaultIfEmpty()
            where comentario.ChamadoId == chamado.Id
            orderby comentario.DataComentario ascending, comentario.Id ascending
            select new
            {
                id = comentario.Id,
                usuarioId = comentario.UsuarioId,
                autor = infoUsuario != null && !string.IsNullOrWhiteSpace(infoUsuario.Apelido)
                    ? infoUsuario.Apelido
                    : usuario.NomeUsuario,
                texto = comentario.Mensagem,
                anexo = comentario.AnexoComentario,
                dataComentario = comentario.DataComentario
            })
            .ToListAsync();

        var comentariosComAnexo = comentarios.Select(comentario => new
        {
            comentario.id,
            comentario.usuarioId,
            comentario.autor,
            comentario.texto,
            comentario.dataComentario,
            anexoUrl = string.IsNullOrWhiteSpace(comentario.anexo)
                ? null
                : Url.Page("/Menu/Home", "AnexoComentarioChamado", new
                {
                    grupoId = chamado.GrupoId,
                    chamadoId = chamado.Id,
                    comentarioId = comentario.id
                })
        }).ToList();

        return new JsonResult(new
        {
            success = true,
            dados = new
            {
                chamadoId = chamado.Id,
                podeComentar = PodeGerenciarChamadoNoHistorico(contextoMembro.Permissao),
                comentarios = comentariosComAnexo
            }
        });
    }

    public async Task<IActionResult> OnPostMarcarComentariosVisualizadosAsync([FromBody] ChamadoHistoricoRequest request)
    {
        var usuarioId = GetUsuarioLogadoId();
        if (usuarioId == null)
            return Unauthorized();

        if (request == null || request.ChamadoId <= 0)
            return new JsonResult(new { success = false, message = "Chamado inválido." });

        var resultadoAcesso = await ObterChamadoHistoricoComAcessoAsync(usuarioId.Value, GrupoId, request.ChamadoId);
        if (!resultadoAcesso.Success)
            return new JsonResult(new { success = false, message = resultadoAcesso.Message });

        var strategy = _context.Database.CreateExecutionStrategy();

        try
        {
            return await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _context.Database.BeginTransactionAsync();

                try
                {
                    var notificacoes = await _context.Notificacoes
                        .Where(n => n.UsuarioId == usuarioId.Value &&
                                    n.GrupoId == GrupoId &&
                                    !n.Lida &&
                                    n.Tipo == TipoNotificacao.Chamado &&
                                    (n.ReferenciaTipo == ReferenciaTipoComentarioHistorico ||
                                     n.ReferenciaTipo == ReferenciaTipoComentarioChamado) &&
                                    n.ReferenciaId == request.ChamadoId)
                        .ToListAsync();

                    var agora = DateTime.UtcNow;
                    foreach (var notificacao in notificacoes)
                    {
                        notificacao.Lida = true;
                        notificacao.DataLeitura = agora;
                    }

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    return (IActionResult)new JsonResult(new
                    {
                        success = true,
                        dados = new { chamadoId = request.ChamadoId }
                    });
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            });
        }
        catch (Exception)
        {
            return new JsonResult(new { success = false, message = "Não foi possível atualizar a visualização dos comentários." });
        }
    }

    public async Task<IActionResult> OnPostAdicionarComentarioAsync([FromBody] AdicionarComentarioHistoricoRequest request)
    {
        var usuarioId = GetUsuarioLogadoId();
        if (usuarioId == null)
            return Unauthorized();

        if (request == null || request.ChamadoId <= 0 || string.IsNullOrWhiteSpace(request.Mensagem))
            return new JsonResult(new { success = false, message = "Comentário inválido." });

        var resultadoAcesso = await ObterChamadoHistoricoComAcessoAsync(usuarioId.Value, GrupoId, request.ChamadoId);
        if (!resultadoAcesso.Success)
            return new JsonResult(new { success = false, message = resultadoAcesso.Message });

        if (!PodeGerenciarChamadoNoHistorico(resultadoAcesso.ContextoMembro!.Permissao))
        {
            return new JsonResult(new
            {
                success = false,
                message = "Você não tem permissão para comentar neste chamado."
            });
        }

        var mensagem = request.Mensagem.Trim();
        if (mensagem.Length > 500)
            return new JsonResult(new { success = false, message = "O comentário não pode exceder 500 caracteres." });

        var strategy = _context.Database.CreateExecutionStrategy();

        try
        {
            return await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _context.Database.BeginTransactionAsync();

                try
                {
                    var usuario = await _context.Usuarios.FirstAsync(u => u.Id == usuarioId.Value);
                    var comentario = new ComentarioChamado
                    {
                        ChamadoId = resultadoAcesso.Chamado!.Id,
                        Chamado = resultadoAcesso.Chamado,
                        UsuarioId = usuarioId.Value,
                        Usuario = usuario,
                        Mensagem = mensagem,
                        DataComentario = DateTime.UtcNow
                    };

                    _context.ComentariosChamados.Add(comentario);

                    _context.HistoricoAlteracoesChamado.Add(new HistoricoAlteracaoChamado
                    {
                        ChamadoId = resultadoAcesso.Chamado.Id,
                        GrupoId = resultadoAcesso.Chamado.GrupoId,
                        UsuarioId = usuarioId.Value,
                        CampoAlterado = "Comentario",
                        ValorAlterado = mensagem,
                        TipoAlteracao = "ComentarioManual",
                        DataAlteracao = DateTime.UtcNow
                    });

                    var membrosGrupo = await _context.UsuariosGrupos
                        .AsNoTracking()
                        .Where(ug => ug.GrupoId == resultadoAcesso.Chamado.GrupoId &&
                                     ug.Ativo &&
                                     ug.UsuarioId != usuarioId.Value)
                        .Select(ug => new { ug.UsuarioId, ug.Permissao })
                        .ToListAsync();

                    var notificacoes = membrosGrupo
                        .Where(membro => GrupoPermissionService.PodeVerChamado(
                            membro.Permissao,
                            resultadoAcesso.EstaPublicoNoHistorico,
                            membro.UsuarioId,
                            resultadoAcesso.Chamado.CriadorChamadoId))
                        .Select(membro => new Notificacao
                        {
                            UsuarioId = membro.UsuarioId,
                            GrupoId = resultadoAcesso.Chamado.GrupoId,
                            Tipo = TipoNotificacao.Chamado,
                            Titulo = "Novo comentário em chamado",
                            Mensagem = $"Chamado #{resultadoAcesso.Chamado.NumeroChamadoGrupo}: novo comentário registrado.",
                            ReferenciaId = resultadoAcesso.Chamado.Id,
                            ReferenciaTipo = ReferenciaTipoComentarioHistorico,
                            LinkDestino = Url.Page("/Menu/History", new { grupoId = resultadoAcesso.Chamado.GrupoId }),
                            DataCriacao = DateTime.UtcNow
                        })
                        .ToList();

                    if (notificacoes.Count > 0)
                        _context.Notificacoes.AddRange(notificacoes);

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    return (IActionResult)new JsonResult(new
                    {
                        success = true,
                        dados = new
                        {
                            chamadoId = resultadoAcesso.Chamado.Id,
                            comentarioId = comentario.Id,
                            message = "Comentário adicionado com sucesso."
                        }
                    });
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            });
        }
        catch (Exception)
        {
            return new JsonResult(new { success = false, message = "Não foi possível adicionar o comentário." });
        }
    }

    public async Task<IActionResult> OnPostAlterarStatusHistoricoAsync([FromBody] AlterarStatusHistoricoRequest request)
    {
        var usuarioId = GetUsuarioLogadoId();
        if (usuarioId == null)
            return Unauthorized();

        if (request == null || request.ChamadoId <= 0 || string.IsNullOrWhiteSpace(request.Status))
            return new JsonResult(new { success = false, message = "Dados inválidos para alteração de status." });

        var resultadoAcesso = await ObterChamadoHistoricoComAcessoAsync(usuarioId.Value, GrupoId, request.ChamadoId);
        if (!resultadoAcesso.Success)
            return new JsonResult(new { success = false, message = resultadoAcesso.Message });

        if (!PodeGerenciarChamadoNoHistorico(resultadoAcesso.ContextoMembro!.Permissao))
        {
            return new JsonResult(new
            {
                success = false,
                message = "Você não tem permissão para alterar o status deste chamado."
            });
        }

        if (!Enum.TryParse<StatusChamado>(request.Status.Trim(), out var novoStatus))
            return new JsonResult(new { success = false, message = "Status inválido." });

        if (!StatusAlteraveisHistorico.Contains(novoStatus))
            return new JsonResult(new { success = false, message = "Status não permitido para alteração pelo histórico." });

        var statusAnterior = resultadoAcesso.Chamado!.Status;
        if (statusAnterior == novoStatus)
        {
            return new JsonResult(new
            {
                success = false,
                message = "O chamado já está com este status."
            });
        }

        var strategy = _context.Database.CreateExecutionStrategy();

        try
        {
            return await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _context.Database.BeginTransactionAsync();

                try
                {
                    resultadoAcesso.Chamado.Status = novoStatus;

                    var statusFinalAtual = StatusFinais.Contains(novoStatus);
                    if (statusFinalAtual)
                        resultadoAcesso.Chamado.DataFinalizacao = DateTime.UtcNow;
                    else if (statusAnterior is StatusChamado.Concluido or StatusChamado.Fechado or StatusChamado.Cancelado)
                        resultadoAcesso.Chamado.DataFinalizacao = null;

                    RegistrarTransicaoStatusChamado(resultadoAcesso.Chamado, statusAnterior, novoStatus, usuarioId.Value, false, "Atualização manual pelo historico");
                    RegistrarHistoricoAlteracaoStatus(resultadoAcesso.Chamado, statusAnterior, novoStatus, usuarioId.Value, "StatusManual");

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    return (IActionResult)new JsonResult(new
                    {
                        success = true,
                        dados = new
                        {
                            chamadoId = resultadoAcesso.Chamado.Id,
                            status = resultadoAcesso.Chamado.Status.ToString(),
                            statusFormatado = FormatarStatusTexto(resultadoAcesso.Chamado.Status),
                            removeFromHistory = !StatusFinais.Contains(resultadoAcesso.Chamado.Status),
                            publico = resultadoAcesso.Chamado.Publico,
                            dataFinalizacao = resultadoAcesso.Chamado.DataFinalizacao,
                            message = resultadoAcesso.Chamado.Status == StatusChamado.Reaberto
                                ? "Chamado reaberto e enviado para a tela de chamados ativos."
                                : "Status atualizado com sucesso."
                        }
                    });
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            });
        }
        catch (Exception)
        {
            return new JsonResult(new { success = false, message = "Não foi possível alterar o status do chamado." });
        }
    }

    private int? GetUsuarioLogadoId()
    {
        var claim = User.FindFirst("Id")?.Value;
        return int.TryParse(claim, out var id) ? id : null;
    }

    private async Task<GrupoMemberContext?> ValidarMembroAsync(int usuarioId, int grupoId)
    {
        if (grupoId <= 0)
            return null;

        return await _grupoAuthorizationService.ObterContextoMembroAsync(usuarioId, grupoId);
    }

    private async Task<ChamadoHistoricoAcessoResult> ObterChamadoHistoricoComAcessoAsync(int usuarioId, int grupoId, int chamadoId)
    {
        if (grupoId <= 0 || chamadoId <= 0)
            return ChamadoHistoricoAcessoResult.Falha("Chamado inválido.");

        var chamado = await _context.Chamados
            .FirstOrDefaultAsync(c => c.Id == chamadoId && c.GrupoId == grupoId && c.Status != StatusChamado.Excluido);

        if (chamado == null)
            return ChamadoHistoricoAcessoResult.Falha("Chamado não encontrado.");

        if (!StatusFinais.Contains(chamado.Status))
            return ChamadoHistoricoAcessoResult.Falha("O chamado não está mais no histórico.");

        var contextoMembro = await ValidarMembroAsync(usuarioId, grupoId);
        if (contextoMembro == null)
            return ChamadoHistoricoAcessoResult.Falha("Você não pertence a este grupo.");

        var estaPublicoNoHistorico = await ObterVisibilidadeHistoricoAsync(chamado);
        if (!GrupoPermissionService.PodeVerChamado(contextoMembro.Permissao, estaPublicoNoHistorico, usuarioId, chamado.CriadorChamadoId))
        {
            return ChamadoHistoricoAcessoResult.Falha("Você não tem permissão para visualizar este chamado.");
        }

        return ChamadoHistoricoAcessoResult.Sucesso(chamado, contextoMembro, estaPublicoNoHistorico);
    }

    private async Task<bool> ObterVisibilidadeHistoricoAsync(Chamado chamado)
    {
        var ultimoRegistroPublico = await _context.HistoricoAlteracoesChamado
            .AsNoTracking()
            .Where(h => h.ChamadoId == chamado.Id && h.CampoAlterado == "Publico")
            .OrderByDescending(h => h.DataAlteracao)
            .Select(h => h.ValorAlterado)
            .FirstOrDefaultAsync();

        return ultimoRegistroPublico switch
        {
            "True" or "true" or "1" => true,
            "False" or "false" or "0" => false,
            _ => chamado.Publico
        };
    }

    private static bool PodeGerenciarChamadoNoHistorico(PermissaoUsuario permissao) =>
        permissao == PermissaoUsuario.Administracao ||
        permissao == PermissaoUsuario.Tecnico;

    private static string FormatarStatusTexto(StatusChamado status) => status switch
    {
        StatusChamado.Concluido => "Concluido",
        StatusChamado.Fechado => "Fechado",
        StatusChamado.Cancelado => "Cancelado",
        StatusChamado.EmAndamento => "Em andamento",
        StatusChamado.Reaberto => "Reaberto",
        _ => status.ToString()
    };

    private void RegistrarTransicaoStatusChamado(
        Chamado chamado,
        StatusChamado statusAnterior,
        StatusChamado statusNovo,
        int? usuarioId,
        bool origemAutomatica,
        string? descricaoOrigem)
    {
        _context.HistoricoStatusChamados.Add(new HistoricoStatusChamado
        {
            ChamadoId = chamado.Id,
            StatusAnterior = ConverterStatusAnterior(statusAnterior),
            StatusNovo = ConverterStatusNovo(statusNovo),
            UsuarioId = usuarioId,
            OrigemAutomatica = origemAutomatica,
            DescricaoOrigem = descricaoOrigem,
            DataTransicao = DateTime.UtcNow
        });
    }

    private void RegistrarHistoricoAlteracaoStatus(
        Chamado chamado,
        StatusChamado statusAnterior,
        StatusChamado statusNovo,
        int usuarioId,
        string tipoAlteracao)
    {
        _context.HistoricoAlteracoesChamado.Add(new HistoricoAlteracaoChamado
        {
            ChamadoId = chamado.Id,
            GrupoId = chamado.GrupoId,
            UsuarioId = usuarioId,
            CampoAlterado = "Status",
            ValorAnterior = statusAnterior.ToString(),
            ValorAlterado = statusNovo.ToString(),
            TipoAlteracao = tipoAlteracao,
            DataAlteracao = DateTime.UtcNow
        });
    }

    private static StatusAnteriorChamado ConverterStatusAnterior(StatusChamado status) =>
        Enum.Parse<StatusAnteriorChamado>(status.ToString());

    private static StatusNovoChamado ConverterStatusNovo(StatusChamado status) =>
        Enum.Parse<StatusNovoChamado>(status.ToString());

    public class HistoricoChamadoVm
    {
        public int Id { get; set; }
        public int NumeroChamadoGrupo { get; set; }
        public string? Titulo { get; set; }
        public StatusChamado Status { get; set; }
        public DateTime DataCriacao { get; set; }
        public DateTime? DataFinal { get; set; }
        public string CriadoPor { get; set; } = string.Empty;
        public string FinalizadoPor { get; set; } = string.Empty;
        public bool TemComentariosNaoLidos { get; set; }
        public TimeSpan? TempoResolucao =>
            DataFinal.HasValue && DataFinal.Value >= DataCriacao
                ? DataFinal.Value - DataCriacao
                : null;
    }

    private class HistoricoFinalResumo
    {
        public int ChamadoId { get; set; }
        public DateTime DataTransicao { get; set; }
        public int? UsuarioId { get; set; }
        public bool OrigemAutomatica { get; set; }
    }

    public class ChamadoHistoricoRequest
    {
        public int ChamadoId { get; set; }
    }

    public class AdicionarComentarioHistoricoRequest
    {
        public int ChamadoId { get; set; }
        public string? Mensagem { get; set; }
    }

    public class AlterarStatusHistoricoRequest
    {
        public int ChamadoId { get; set; }
        public string? Status { get; set; }
    }

    private sealed class ChamadoHistoricoAcessoResult
    {
        public bool Success { get; private set; }
        public string Message { get; private set; } = string.Empty;
        public Chamado? Chamado { get; private set; }
        public GrupoMemberContext? ContextoMembro { get; private set; }
        public bool EstaPublicoNoHistorico { get; private set; }

        public static ChamadoHistoricoAcessoResult Falha(string message) =>
            new()
            {
                Success = false,
                Message = message
            };

        public static ChamadoHistoricoAcessoResult Sucesso(Chamado chamado, GrupoMemberContext contextoMembro, bool estaPublicoNoHistorico) =>
            new()
            {
                Success = true,
                Chamado = chamado,
                ContextoMembro = contextoMembro,
                EstaPublicoNoHistorico = estaPublicoNoHistorico
            };
    }
}
