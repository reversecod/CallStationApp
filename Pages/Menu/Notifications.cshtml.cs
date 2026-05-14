using CallStationApp.Authorization;
using CallStationApp.Data;
using CallStationApp.Models;
using CallStationApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace CallStationApp.Pages.Menu;

[Authorize]
public class NotificationsModel : PageModel
{
    private static readonly TimeZoneInfo FusoHorarioRegional = ObterFusoHorarioRegional();

    private readonly AppDbContext _context;
    private readonly GrupoAuthorizationService _grupoAuthorizationService;
    private readonly NotificacaoService _notificacaoService;
    private readonly ILogger<NotificationsModel> _logger;

    public NotificationsModel(
        AppDbContext context,
        GrupoAuthorizationService grupoAuthorizationService,
        NotificacaoService notificacaoService,
        ILogger<NotificationsModel> logger)
    {
        _context = context;
        _grupoAuthorizationService = grupoAuthorizationService;
        _notificacaoService = notificacaoService;
        _logger = logger;
    }

    [BindProperty(SupportsGet = true)]
    public int GrupoId { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? NotificacaoId { get; set; }

    public Usuario? UsuarioLogado { get; set; }
    public List<NotificacaoViewModel> Notificacoes { get; set; } = new();
    public int TotalNaoLidas { get; set; }

    public class NotificacaoViewModel
    {
        public int Id { get; set; }
        public string Tipo { get; set; } = string.Empty;
        public string Titulo { get; set; } = string.Empty;
        public string NomeGrupo { get; set; } = string.Empty;
        public string Mensagem { get; set; } = string.Empty;
        public string MensagemModal { get; set; } = string.Empty;
        public bool Lida { get; set; }
        public DateTime DataCriacao { get; set; }
        public DateTime? DataLeitura { get; set; }
        public string DataCriacaoTexto { get; set; } = string.Empty;
        public string DataLeituraTexto { get; set; } = string.Empty;
        public string? LinkDestino { get; set; }
        public int? ReferenciaId { get; set; }
        public string? ReferenciaTipo { get; set; }
        public bool PodeResponderConvite { get; set; }
    }

    public class AcaoConviteRequest
    {
        public int NotificacaoId { get; set; }
        public int ConviteId { get; set; }
    }

    public class AlterarLeituraNotificacaoRequest
    {
        public int NotificacaoId { get; set; }
    }

    public class AbrirNotificacaoRequest
    {
        public int NotificacaoId { get; set; }
    }

    public async Task<IActionResult> OnGetAsync()
    {
        var idUsuario = GetUsuarioLogadoId();
        if (idUsuario == null)
            return RedirectToPage("/Auth/Login");

        UsuarioLogado = await _context.Usuarios
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == idUsuario.Value);

        if (UsuarioLogado == null)
            return RedirectToPage("/Auth/Login");

        if (GrupoId <= 0)
        {
            var grupoNotificacao = await _context.Notificacoes
                .AsNoTracking()
                .Where(n => n.UsuarioId == idUsuario.Value)
                .OrderByDescending(n => !n.Lida)
                .ThenByDescending(n => n.DataCriacao)
                .Select(n => (int?)n.GrupoId)
                .FirstOrDefaultAsync();

            var grupoAcesso = grupoNotificacao ?? await _context.UsuariosGrupos
                .AsNoTracking()
                .Where(ug => ug.UsuarioId == idUsuario.Value && ug.Ativo)
                .OrderByDescending(ug => ug.DataUltimoAcesso ?? ug.DataAdicao)
                .Select(ug => (int?)ug.GrupoId)
                .FirstOrDefaultAsync();

            if (!grupoAcesso.HasValue)
                return Page();

            GrupoId = grupoAcesso.Value;
        }

        var contextoMembro = await _grupoAuthorizationService.ObterContextoMembroAsync(idUsuario.Value, GrupoId);
        if (contextoMembro == null)
        {
            if (!await GrupoEstaAtivoAsync(GrupoId))
                return RedirectToPage("/Menu/Menu");

            var possuiNotificacaoNoGrupo = await _context.Notificacoes
                .AsNoTracking()
                .AnyAsync(n => n.UsuarioId == idUsuario.Value && n.GrupoId == GrupoId);

            if (!possuiNotificacaoNoGrupo)
                return RedirectToPage("/Menu/Menu");
        }

        var convitesPendentesIds = await _context.ConvitesGrupo
            .AsNoTracking()
            .Where(c =>
                c.DestinatarioUsuarioId == idUsuario.Value &&
                c.Status == StatusConviteGrupo.Pendente)
            .Select(c => c.Id)
            .ToListAsync();

        var convitesPendentesSet = convitesPendentesIds.ToHashSet();

        Notificacoes = await _context.Notificacoes
            .AsNoTracking()
            .Where(n => n.UsuarioId == idUsuario.Value)
            .OrderByDescending(n => n.DataCriacao)
            .Select(n => new NotificacaoViewModel
            {
                Id = n.Id,
                Tipo = n.Tipo.ToString(),
                Titulo = n.Titulo,
                NomeGrupo = n.Grupo != null ? n.Grupo.Nome : string.Empty,
                Mensagem = n.Mensagem,
                Lida = n.Lida,
                DataCriacao = n.DataCriacao,
                DataLeitura = n.DataLeitura,
                DataCriacaoTexto = ParaDataHoraRegionalDisplay(n.DataCriacao),
                DataLeituraTexto = ParaDataHoraRegionalDisplay(n.DataLeitura),
                LinkDestino = n.LinkDestino,
                ReferenciaId = n.ReferenciaId,
                ReferenciaTipo = n.ReferenciaTipo
            })
            .ToListAsync();

        var conviteIds = Notificacoes
            .Where(n => n.ReferenciaId.HasValue &&
                        string.Equals(n.ReferenciaTipo, "ConviteGrupo", StringComparison.Ordinal))
            .Select(n => n.ReferenciaId!.Value)
            .Distinct()
            .ToList();

        var mensagensConvitePorId = conviteIds.Count == 0
            ? new Dictionary<int, string?>()
            : await _context.ConvitesGrupo
                .AsNoTracking()
                .Where(c => conviteIds.Contains(c.Id))
                .ToDictionaryAsync(c => c.Id, c => c.Mensagem);

        foreach (var notificacao in Notificacoes)
        {
            notificacao.PodeResponderConvite =
                notificacao.ReferenciaId.HasValue &&
                string.Equals(notificacao.ReferenciaTipo, "ConviteGrupo", StringComparison.Ordinal) &&
                convitesPendentesSet.Contains(notificacao.ReferenciaId.Value);

            notificacao.MensagemModal = notificacao.Mensagem;

            if (notificacao.ReferenciaId.HasValue &&
                string.Equals(notificacao.ReferenciaTipo, "ConviteGrupo", StringComparison.Ordinal) &&
                mensagensConvitePorId.TryGetValue(notificacao.ReferenciaId.Value, out var mensagemConvite) &&
                !string.IsNullOrWhiteSpace(mensagemConvite))
            {
                notificacao.MensagemModal = mensagemConvite;
            }
        }

        TotalNaoLidas = Notificacoes.Count(n => !n.Lida);

        return Page();
    }

    public async Task<IActionResult> OnGetContarNaoLidasAsync(int grupoId)
    {
        var idUsuario = GetUsuarioLogadoId();
        if (idUsuario == null)
            return Unauthorized();

        var totalNaoLidas = await _context.Notificacoes
            .AsNoTracking()
            .CountAsync(n => n.UsuarioId == idUsuario.Value && !n.Lida);

        return new JsonResult(new
        {
            success = true,
            dados = new { totalNaoLidas }
        });
    }

    public async Task<IActionResult> OnPostMarcarTodasComoLidasAsync()
    {
        var idUsuario = GetUsuarioLogadoId();
        if (idUsuario == null)
        {
            return new JsonResult(new { success = false, message = "Usuário não autenticado." })
            {
                StatusCode = StatusCodes.Status401Unauthorized
            };
        }

        var notificacoes = await _context.Notificacoes
            .Where(n => n.UsuarioId == idUsuario.Value && !n.Lida)
            .ToListAsync();

        if (!notificacoes.Any())
        {
            return new JsonResult(new
            {
                success = true,
                message = "Não há notificacoes pendentes."
            });
        }

        var agora = DateTime.UtcNow;

        foreach (var notificacao in notificacoes)
        {
            notificacao.Lida = true;
            notificacao.DataLeitura = agora;
        }

        await _context.SaveChangesAsync();

        return new JsonResult(new
        {
            success = true,
            message = "Notificacoes marcadas como lidas com sucesso.",
            totalNaoLidas = 0,
            dataLeituraTexto = ParaDataHoraRegionalDisplay(agora)
        });
    }

    public async Task<IActionResult> OnPostAlternarLeituraAsync([FromBody] AlterarLeituraNotificacaoRequest request)
    {
        var idUsuario = GetUsuarioLogadoId();
        if (idUsuario == null)
        {
            return new JsonResult(new { success = false, message = "Usuário não autenticado." })
            {
                StatusCode = StatusCodes.Status401Unauthorized
            };
        }

        if (request == null || request.NotificacaoId <= 0)
        {
            return new JsonResult(new { success = false, message = "Notificacao inválida." })
            {
                StatusCode = StatusCodes.Status400BadRequest
            };
        }

        var notificacao = await _context.Notificacoes
            .FirstOrDefaultAsync(n =>
                n.Id == request.NotificacaoId &&
                n.UsuarioId == idUsuario.Value);

        if (notificacao == null)
        {
            return new JsonResult(new { success = false, message = "Notificacao não encontrada." })
            {
                StatusCode = StatusCodes.Status404NotFound
            };
        }

        var contextoMembro = await _grupoAuthorizationService.ObterContextoMembroAsync(idUsuario.Value, notificacao.GrupoId);
        if (contextoMembro == null)
        {
            if (!await GrupoEstaAtivoAsync(notificacao.GrupoId))
            {
                return new JsonResult(new { success = false, message = "Este grupo não está mais disponível." })
                {
                    StatusCode = StatusCodes.Status403Forbidden
                };
            }

            var possuiNotificacaoNoGrupo = await _context.Notificacoes
                .AsNoTracking()
                .AnyAsync(n => n.UsuarioId == idUsuario.Value && n.GrupoId == notificacao.GrupoId);

            if (!possuiNotificacaoNoGrupo)
            {
                return new JsonResult(new { success = false, message = "Você não pertence a este grupo." })
                {
                    StatusCode = StatusCodes.Status403Forbidden
                };
            }
        }

        notificacao.Lida = !notificacao.Lida;
        notificacao.DataLeitura = notificacao.Lida ? DateTime.UtcNow : null;

        var strategy = _context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        });

        var totalNaoLidas = await _context.Notificacoes
            .AsNoTracking()
            .CountAsync(n => n.UsuarioId == idUsuario.Value && !n.Lida);

        return new JsonResult(new
        {
            success = true,
            lida = notificacao.Lida,
            dataLeitura = notificacao.DataLeitura,
            dataLeituraTexto = ParaDataHoraRegionalDisplay(notificacao.DataLeitura),
            totalNaoLidas,
            message = notificacao.Lida
                ? "Notificacao marcada como lida."
                : "Notificacao marcada como não lida."
        });
    }

    public async Task<IActionResult> OnPostAbrirNotificacaoAsync([FromBody] AbrirNotificacaoRequest request)
    {
        var idUsuario = GetUsuarioLogadoId();
        if (idUsuario == null)
        {
            return new JsonResult(new { success = false, message = "Usuário não autenticado." })
            {
                StatusCode = StatusCodes.Status401Unauthorized
            };
        }

        if (request == null || request.NotificacaoId <= 0)
        {
            return new JsonResult(new { success = false, message = "Notificação inválida." })
            {
                StatusCode = StatusCodes.Status400BadRequest
            };
        }

        var notificacao = await _context.Notificacoes
            .FirstOrDefaultAsync(n =>
                n.Id == request.NotificacaoId &&
                n.UsuarioId == idUsuario.Value);

        if (notificacao == null)
        {
            return new JsonResult(new { success = false, message = "Notificação não encontrada." })
            {
                StatusCode = StatusCodes.Status404NotFound
            };
        }

        var contextoMembro = await _grupoAuthorizationService.ObterContextoMembroAsync(idUsuario.Value, notificacao.GrupoId);
        if (contextoMembro == null)
        {
            if (!await GrupoEstaAtivoAsync(notificacao.GrupoId))
            {
                return new JsonResult(new { success = false, message = "Este grupo não está mais disponível." })
                {
                    StatusCode = StatusCodes.Status403Forbidden
                };
            }

            var possuiNotificacaoNoGrupo = await _context.Notificacoes
                .AsNoTracking()
                .AnyAsync(n => n.UsuarioId == idUsuario.Value && n.GrupoId == notificacao.GrupoId);

            if (!possuiNotificacaoNoGrupo)
            {
                return new JsonResult(new { success = false, message = "Você não pertence a este grupo." })
                {
                    StatusCode = StatusCodes.Status403Forbidden
                };
            }
        }

        var destino = ResolverDestinoNotificacao(notificacao);
        var strategy = _context.Database.CreateExecutionStrategy();

        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                if (!notificacao.Lida)
                {
                    notificacao.Lida = true;
                    notificacao.DataLeitura = DateTime.UtcNow;
                    await _context.SaveChangesAsync();
                }

                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        });

        var totalNaoLidas = await _context.Notificacoes
            .AsNoTracking()
            .CountAsync(n => n.UsuarioId == idUsuario.Value && !n.Lida);

        return new JsonResult(new
        {
            success = true,
            destino,
            totalNaoLidas
        });
    }

    public async Task<IActionResult> OnPostAceitarConviteAsync([FromBody] ResponderConviteRequest request)
    {
        var idUsuario = GetUsuarioLogadoId();
        if (idUsuario == null)
            return new JsonResult(new { success = false, message = "Usuário não autenticado." });

        if (request == null || request.ConviteId <= 0)
            return new JsonResult(new { success = false, message = "Convite inválido." });

        var strategy = _context.Database.CreateExecutionStrategy();

        try
        {
            return await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _context.Database.BeginTransactionAsync();

                try
                {
                    var convite = await _context.ConvitesGrupo
                        .FirstOrDefaultAsync(c => c.Id == request.ConviteId &&
                                                  c.DestinatarioUsuarioId == idUsuario.Value);

                    if (convite == null)
                        return (IActionResult)new JsonResult(new { success = false, message = "Convite não encontrado." });

                    if (convite.Status != StatusConviteGrupo.Pendente)
                        return (IActionResult)new JsonResult(new { success = false, message = "Este convite não está mais pendente." });

                    var usuarioDestinatario = await _context.Usuarios
                        .AsNoTracking()
                        .Where(u => u.Id == idUsuario.Value)
                        .Select(u => u.NomeUsuario)
                        .FirstOrDefaultAsync();

                    var nomeGrupo = await _context.Grupos
                        .AsNoTracking()
                        .Where(g => g.Id == convite.GrupoId)
                        .Select(g => g.Nome)
                        .FirstOrDefaultAsync();

                    var vinculoExistente = await _context.UsuariosGrupos
                        .FirstOrDefaultAsync(ug => ug.UsuarioId == idUsuario.Value &&
                                                   ug.GrupoId == convite.GrupoId);

                    if (vinculoExistente != null && vinculoExistente.Ativo)
                    {
                        convite.Status = StatusConviteGrupo.Cancelado;
                        convite.DataResposta = DateTime.UtcNow;

                        var notificacaoExistenteAtivo = await _context.Notificacoes
                            .FirstOrDefaultAsync(n =>
                                n.UsuarioId == idUsuario.Value &&
                                n.GrupoId == convite.GrupoId &&
                                n.ReferenciaId == convite.Id &&
                                n.ReferenciaTipo == "ConviteGrupo");

                        if (notificacaoExistenteAtivo != null)
                        {
                            notificacaoExistenteAtivo.Titulo = "Convite cancelado";
                            notificacaoExistenteAtivo.Mensagem = "Este convite foi cancelado porque você já faz parte deste grupo.";
                            notificacaoExistenteAtivo.ReferenciaTipo = "ConviteGrupoCancelado";
                            notificacaoExistenteAtivo.Lida = true;
                            notificacaoExistenteAtivo.DataLeitura = DateTime.UtcNow;
                        }

                        await _context.SaveChangesAsync();
                        await transaction.CommitAsync();

                        return (IActionResult)new JsonResult(new
                        {
                            success = false,
                            message = "Você já faz parte deste grupo."
                        });
                    }

                    if (vinculoExistente != null && !vinculoExistente.Ativo)
                    {
                        vinculoExistente.Ativo = true;
                        vinculoExistente.Permissao = PermissaoUsuario.Nenhuma;
                        vinculoExistente.DataRemocao = null;
                        vinculoExistente.RemovidoPorUsuarioId = null;
                        vinculoExistente.DataAdicao = DateTime.UtcNow;
                    }
                    else if (vinculoExistente == null)
                    {
                        var vinculo = new UsuarioGrupo
                        {
                            UsuarioId = idUsuario.Value,
                            GrupoId = convite.GrupoId,
                            Permissao = PermissaoUsuario.Nenhuma,
                            DataAdicao = DateTime.UtcNow,
                            Ativo = true
                        };

                        _context.UsuariosGrupos.Add(vinculo);
                    }

                    convite.Status = StatusConviteGrupo.Aceito;
                    convite.DataResposta = DateTime.UtcNow;

                    var notificacao = await _context.Notificacoes
                        .FirstOrDefaultAsync(n =>
                            n.UsuarioId == idUsuario.Value &&
                            n.GrupoId == convite.GrupoId &&
                            n.ReferenciaId == convite.Id &&
                            n.ReferenciaTipo == "ConviteGrupo");

                    if (notificacao != null)
                    {
                        notificacao.Titulo = "Convite aceito";
                        notificacao.Mensagem = "Você aceitou o convite para participar deste grupo.";
                        notificacao.ReferenciaTipo = "ConviteGrupoRespondidoAceito";
                        notificacao.Lida = true;
                        notificacao.DataLeitura = DateTime.UtcNow;
                    }

                    _context.Notificacoes.Add(new Notificacao
                    {
                        UsuarioId = convite.RemetenteUsuarioId,
                        GrupoId = convite.GrupoId,
                        Tipo = TipoNotificacao.ConviteGrupo,
                        Titulo = "Convite aceito",
                        Mensagem = $"{usuarioDestinatario ?? "Um usuário"} aceitou seu convite para o grupo {nomeGrupo ?? "selecionado"}.",
                        Lida = false,
                        DataCriacao = DateTime.UtcNow,
                        ReferenciaId = convite.Id,
                        ReferenciaTipo = "ConviteGrupoRespondidoAceito",
                        LinkDestino = $"/Menu/Notifications?grupoId={convite.GrupoId}"
                    });

                    await _notificacaoService.CriarNotificacoesEntradaGrupoPorConviteAsync(convite.GrupoId, idUsuario.Value);

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    return (IActionResult)new JsonResult(new
                    {
                        success = true,
                        message = "Convite aceito com sucesso."
                    });
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            });
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Erro de banco ao aceitar convite {ConviteId}.", request.ConviteId);
            return new JsonResult(new
            {
                success = false,
                message = "Não foi possível aceitar o convite no momento."
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao aceitar convite {ConviteId}.", request.ConviteId);
            return new JsonResult(new
            {
                success = false,
                message = "Não foi possível aceitar o convite no momento."
            });
        }
    }

    public async Task<IActionResult> OnPostRecusarConviteAsync([FromBody] ResponderConviteRequest request)
    {
        var idUsuario = GetUsuarioLogadoId();
        if (idUsuario == null)
            return new JsonResult(new { success = false, message = "Usuário não autenticado." });

        if (request == null || request.ConviteId <= 0)
            return new JsonResult(new { success = false, message = "Convite inválido." });

        var convite = await _context.ConvitesGrupo
            .FirstOrDefaultAsync(c => c.Id == request.ConviteId && c.DestinatarioUsuarioId == idUsuario.Value);

        if (convite == null)
            return new JsonResult(new { success = false, message = "Convite não encontrado." });

        if (convite.Status != StatusConviteGrupo.Pendente)
            return new JsonResult(new { success = false, message = "Este convite não está mais pendente." });

        var usuarioDestinatario = await _context.Usuarios
            .AsNoTracking()
            .Where(u => u.Id == idUsuario.Value)
            .Select(u => u.NomeUsuario)
            .FirstOrDefaultAsync();

        var nomeGrupo = await _context.Grupos
            .AsNoTracking()
            .Where(g => g.Id == convite.GrupoId)
            .Select(g => g.Nome)
            .FirstOrDefaultAsync();

        convite.Status = StatusConviteGrupo.Recusado;
        convite.DataResposta = DateTime.UtcNow;

        var notificacao = await _context.Notificacoes
        .FirstOrDefaultAsync(n =>
            n.UsuarioId == idUsuario.Value &&
            n.GrupoId == convite.GrupoId &&
            n.ReferenciaId == convite.Id &&
            n.ReferenciaTipo == "ConviteGrupo");

        if (notificacao != null)
        {
            notificacao.Titulo = "Convite recusado";
            notificacao.Mensagem = "Você recusou o convite para participar deste grupo.";
            notificacao.ReferenciaTipo = "ConviteGrupoRespondidoRecusado";
            notificacao.Lida = true;
            notificacao.DataLeitura = DateTime.UtcNow;
        }

        _context.Notificacoes.Add(new Notificacao
        {
            UsuarioId = convite.RemetenteUsuarioId,
            GrupoId = convite.GrupoId,
            Tipo = TipoNotificacao.ConviteGrupo,
            Titulo = "Convite recusado",
            Mensagem = $"{usuarioDestinatario ?? "Um usuário"} recusou seu convite para o grupo {nomeGrupo ?? "selecionado"}.",
            Lida = false,
            DataCriacao = DateTime.UtcNow,
            ReferenciaId = convite.Id,
            ReferenciaTipo = "ConviteGrupoRespondidoRecusado",
            LinkDestino = $"/Menu/Notifications?grupoId={convite.GrupoId}"
        });

        var strategy = _context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        });

        return new JsonResult(new
        {
            success = true,
            message = "Convite recusado com sucesso."
        });
    }

    public class ResponderConviteRequest
    {
        public int ConviteId { get; set; }
    }

    private static string ResolverDestinoNotificacao(Notificacao notificacao)
    {
        if (notificacao.Tipo == TipoNotificacao.Chamado && notificacao.ReferenciaId.HasValue)
            return $"/Menu/Home?grupoId={notificacao.GrupoId}&chamadoId={notificacao.ReferenciaId.Value}";

        if (string.Equals(notificacao.ReferenciaTipo, "EntradaGrupoConvite", StringComparison.Ordinal) &&
            notificacao.ReferenciaId.HasValue)
        {
            return $"/Menu/Members?grupoId={notificacao.GrupoId}&membroId={notificacao.ReferenciaId.Value}";
        }

        if (!string.IsNullOrWhiteSpace(notificacao.LinkDestino) &&
            Uri.TryCreate(notificacao.LinkDestino, UriKind.Relative, out _))
        {
            return notificacao.LinkDestino;
        }

        return $"/Menu/Notifications?grupoId={notificacao.GrupoId}&notificacaoId={notificacao.Id}#notificacao-{notificacao.Id}";
    }

    private int? GetUsuarioLogadoId()
    {
        var claim = User.FindFirst("Id")?.Value;
        return int.TryParse(claim, out var id) ? id : null;
    }

    private async Task<bool> GrupoEstaAtivoAsync(int grupoId)
    {
        var ativo = await _context.GruposConfiguracoes
            .AsNoTracking()
            .Where(c => c.GrupoId == grupoId)
            .Select(c => (bool?)c.Ativo)
            .FirstOrDefaultAsync();

        return ativo ?? true;
    }

    private static string ParaDataHoraRegionalDisplay(DateTime dataUtc) =>
        ParaDataHoraRegionalDisplay((DateTime?)dataUtc);

    private static string ParaDataHoraRegionalDisplay(DateTime? dataUtc)
    {
        if (!dataUtc.HasValue)
            return string.Empty;

        var utc = DateTime.SpecifyKind(dataUtc.Value, DateTimeKind.Utc);
        var regional = TimeZoneInfo.ConvertTimeFromUtc(utc, FusoHorarioRegional);
        return regional.ToString("dd/MM/yyyy HH:mm", CultureInfo.GetCultureInfo("pt-BR"));
    }

    private static TimeZoneInfo ObterFusoHorarioRegional()
    {
        foreach (var id in new[] { "E. South America Standard Time", "America/Sao_Paulo" })
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(id);
            }
            catch (TimeZoneNotFoundException)
            {
            }
            catch (InvalidTimeZoneException)
            {
            }
        }

        return TimeZoneInfo.Local;
    }
}


