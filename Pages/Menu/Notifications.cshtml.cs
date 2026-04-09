using CallStationApp.Data;
using CallStationApp.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace CallStationApp.Pages.Menu;

[Authorize]
public class NotificationsModel : PageModel
{
    private readonly AppDbContext _context;

    public NotificationsModel(AppDbContext context)
    {
        _context = context;
    }

    public Usuario? UsuarioLogado { get; set; }
    public List<NotificacaoViewModel> Notificacoes { get; set; } = new();
    public int TotalNaoLidas { get; set; }

    public class NotificacaoViewModel
    {
        public int Id { get; set; }
        public string Tipo { get; set; } = string.Empty;
        public string Titulo { get; set; } = string.Empty;
        public string Mensagem { get; set; } = string.Empty;
        public bool Lida { get; set; }
        public DateTime DataCriacao { get; set; }
        public DateTime? DataLeitura { get; set; }
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
                Mensagem = n.Mensagem,
                Lida = n.Lida,
                DataCriacao = n.DataCriacao,
                DataLeitura = n.DataLeitura,
                LinkDestino = n.LinkDestino,
                ReferenciaId = n.ReferenciaId,
                ReferenciaTipo = n.ReferenciaTipo
            })
            .ToListAsync();

        foreach (var notificacao in Notificacoes)
        {
            notificacao.PodeResponderConvite =
                notificacao.ReferenciaId.HasValue &&
                string.Equals(notificacao.ReferenciaTipo, "ConviteGrupo", StringComparison.Ordinal) &&
                convitesPendentesSet.Contains(notificacao.ReferenciaId.Value);
        }

        TotalNaoLidas = Notificacoes.Count(n => !n.Lida);

        return Page();
    }

    public async Task<IActionResult> OnPostMarcarTodasComoLidasAsync()
    {
        var idUsuario = GetUsuarioLogadoId();
        if (idUsuario == null)
        {
            return new JsonResult(new { success = false, message = "UsuÃ¡rio nÃ£o autenticado." })
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
                message = "NÃ£o hÃ¡ notificaÃ§Ãµes pendentes."
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
            message = "NotificaÃ§Ãµes marcadas como lidas com sucesso.",
            totalNaoLidas = 0
        });
    }

    public async Task<IActionResult> OnPostAlternarLeituraAsync([FromBody] AlterarLeituraNotificacaoRequest request)
    {
        var idUsuario = GetUsuarioLogadoId();
        if (idUsuario == null)
        {
            return new JsonResult(new { success = false, message = "UsuÃ¡rio nÃ£o autenticado." })
            {
                StatusCode = StatusCodes.Status401Unauthorized
            };
        }

        if (request == null || request.NotificacaoId <= 0)
        {
            return new JsonResult(new { success = false, message = "NotificaÃ§Ã£o invÃ¡lida." })
            {
                StatusCode = StatusCodes.Status400BadRequest
            };
        }

        var notificacao = await _context.Notificacoes
            .FirstOrDefaultAsync(n => n.Id == request.NotificacaoId && n.UsuarioId == idUsuario.Value);

        if (notificacao == null)
        {
            return new JsonResult(new { success = false, message = "NotificaÃ§Ã£o nÃ£o encontrada." })
            {
                StatusCode = StatusCodes.Status404NotFound
            };
        }

        notificacao.Lida = !notificacao.Lida;
        notificacao.DataLeitura = notificacao.Lida ? DateTime.UtcNow : null;

        await _context.SaveChangesAsync();

        var totalNaoLidas = await _context.Notificacoes
            .AsNoTracking()
            .CountAsync(n => n.UsuarioId == idUsuario.Value && !n.Lida);

        return new JsonResult(new
        {
            success = true,
            lida = notificacao.Lida,
            dataLeitura = notificacao.DataLeitura,
            totalNaoLidas,
            message = notificacao.Lida
                ? "NotificaÃ§Ã£o marcada como lida."
                : "NotificaÃ§Ã£o marcada como nÃ£o lida."
        });
    }

    public async Task<IActionResult> OnPostAceitarConviteAsync([FromBody] ResponderConviteRequest request)
    {
        var idUsuario = GetUsuarioLogadoId();
        if (idUsuario == null)
            return new JsonResult(new { success = false, message = "UsuÃ¡rio nÃ£o autenticado." });

        if (request == null || request.ConviteId <= 0)
            return new JsonResult(new { success = false, message = "Convite invÃ¡lido." });

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
                        return (IActionResult)new JsonResult(new { success = false, message = "Convite nÃ£o encontrado." });

                    if (convite.Status != StatusConviteGrupo.Pendente)
                        return (IActionResult)new JsonResult(new { success = false, message = "Este convite nÃ£o estÃ¡ mais pendente." });

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
                                n.ReferenciaId == convite.Id &&
                                n.ReferenciaTipo == "ConviteGrupo");

                        if (notificacaoExistenteAtivo != null)
                        {
                            notificacaoExistenteAtivo.Titulo = "Convite cancelado";
                            notificacaoExistenteAtivo.Mensagem = "Este convite foi cancelado porque vocÃª jÃ¡ faz parte deste grupo.";
                            notificacaoExistenteAtivo.ReferenciaTipo = "ConviteGrupoCancelado";
                            notificacaoExistenteAtivo.Lida = true;
                            notificacaoExistenteAtivo.DataLeitura = DateTime.UtcNow;
                        }

                        await _context.SaveChangesAsync();
                        await transaction.CommitAsync();

                        return (IActionResult)new JsonResult(new
                        {
                            success = false,
                            message = "VocÃª jÃ¡ faz parte deste grupo."
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
                            n.ReferenciaId == convite.Id &&
                            n.ReferenciaTipo == "ConviteGrupo");

                    if (notificacao != null)
                    {
                        notificacao.Titulo = "Convite aceito";
                        notificacao.Mensagem = "VocÃª aceitou o convite para participar deste grupo.";
                        notificacao.ReferenciaTipo = "ConviteGrupoRespondidoAceito";
                        notificacao.Lida = true;
                        notificacao.DataLeitura = DateTime.UtcNow;
                    }

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
            return new JsonResult(new
            {
                success = false,
                message = ex.InnerException?.Message ?? ex.Message
            });
        }
        catch (Exception ex)
        {
            return new JsonResult(new
            {
                success = false,
                message = ex.InnerException?.Message ?? ex.Message
            });
        }
    }

    public async Task<IActionResult> OnPostRecusarConviteAsync([FromBody] ResponderConviteRequest request)
    {
        var idUsuario = GetUsuarioLogadoId();
        if (idUsuario == null)
            return new JsonResult(new { success = false, message = "UsuÃ¡rio nÃ£o autenticado." });

        var convite = await _context.ConvitesGrupo
            .FirstOrDefaultAsync(c => c.Id == request.ConviteId && c.DestinatarioUsuarioId == idUsuario.Value);

        if (convite == null)
            return new JsonResult(new { success = false, message = "Convite nÃ£o encontrado." });

        if (convite.Status != StatusConviteGrupo.Pendente)
            return new JsonResult(new { success = false, message = "Este convite nÃ£o estÃ¡ mais pendente." });

        convite.Status = StatusConviteGrupo.Recusado;
        convite.DataResposta = DateTime.UtcNow;

        var notificacao = await _context.Notificacoes
        .FirstOrDefaultAsync(n =>
            n.UsuarioId == idUsuario.Value &&
            n.ReferenciaId == convite.Id &&
            n.ReferenciaTipo == "ConviteGrupo");

        if (notificacao != null)
        {
            notificacao.Titulo = "Convite recusado";
            notificacao.Mensagem = "VocÃª recusou o convite para participar deste grupo.";
            notificacao.ReferenciaTipo = "ConviteGrupoRespondidoRecusado";
            notificacao.Lida = true;
            notificacao.DataLeitura = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();

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

    private int? GetUsuarioLogadoId()
    {
        var claim = User.FindFirst("Id")?.Value;
        return int.TryParse(claim, out var id) ? id : null;
    }
}


