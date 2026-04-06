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

    public async Task<IActionResult> OnGetAsync()
    {
        var idUsuario = GetUsuarioLogadoId();
        if (idUsuario == null)
            return RedirectToPage("/Login");

        UsuarioLogado = await _context.Usuarios
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == idUsuario.Value);

        if (UsuarioLogado == null)
            return RedirectToPage("/Login");

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
                message = "Não há notificações pendentes."
            });
        }

        var agora = DateTime.Now;

        foreach (var notificacao in notificacoes)
        {
            notificacao.Lida = true;
            notificacao.DataLeitura = agora;
        }

        await _context.SaveChangesAsync();

        return new JsonResult(new
        {
            success = true,
            message = "Notificações marcadas como lidas com sucesso."
        });
    }

    public async Task<IActionResult> OnPostAceitarConviteAsync([FromBody] AcaoConviteRequest? request)
    {
        var idUsuario = GetUsuarioLogadoId();
        if (idUsuario == null)
        {
            return new JsonResult(new { success = false, message = "Usuário não autenticado." })
            {
                StatusCode = StatusCodes.Status401Unauthorized
            };
        }

        if (request == null || request.NotificacaoId <= 0 || request.ConviteId <= 0)
            return BadRequest(new { success = false, message = "Dados inválidos." });

        try
        {
            var notificacao = await _context.Notificacoes
                .FirstOrDefaultAsync(n => n.Id == request.NotificacaoId && n.UsuarioId == idUsuario.Value);

            if (notificacao == null)
                return NotFound(new { success = false, message = "Notificação não encontrada." });

            var convite = await _context.ConvitesGrupo
                .FirstOrDefaultAsync(c =>
                    c.Id == request.ConviteId &&
                    c.DestinatarioUsuarioId == idUsuario.Value);

            if (convite == null)
                return NotFound(new { success = false, message = "Convite não encontrado." });

            if (convite.Status != StatusConviteGrupo.Pendente)
                return BadRequest(new { success = false, message = "Esse convite já foi respondido." });

            var usuarioDestino = await _context.Usuarios
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == idUsuario.Value);

            var grupo = await _context.Grupos
                .AsNoTracking()
                .FirstOrDefaultAsync(g => g.Id == convite.GrupoId);

            var jaEstaNoGrupo = await _context.UsuariosGrupos
                .AnyAsync(ug => ug.UsuarioId == idUsuario.Value && ug.GrupoId == convite.GrupoId);

            var agora = DateTime.Now;

            if (!jaEstaNoGrupo)
            {
                _context.UsuariosGrupos.Add(new UsuarioGrupo
                {
                    UsuarioId = idUsuario.Value,
                    GrupoId = convite.GrupoId,
                    Permissao = PermissaoUsuario.Nenhuma,
                    DataAdicao = agora
                });

                var jaExisteInfoUsuarioGrupo = await _context.InfoUsuariosGrupos
                    .AnyAsync(iug => iug.UsuarioId == idUsuario.Value && iug.GrupoId == convite.GrupoId);

                if (!jaExisteInfoUsuarioGrupo)
                {
                    _context.InfoUsuariosGrupos.Add(new InfoUsuarioGrupo
                    {
                        UsuarioId = idUsuario.Value,
                        GrupoId = convite.GrupoId,
                        DataAtualizacaoRegistro = agora
                    });
                }
            }

            convite.Status = StatusConviteGrupo.Aceito;
            convite.DataResposta = agora;

            notificacao.Titulo = "Convite aceito";
            notificacao.Mensagem = $"Você aceitou o convite para participar do grupo {(grupo?.Nome ?? "Grupo")}.";
            notificacao.Lida = true;
            notificacao.DataLeitura = agora;
            notificacao.LinkDestino = null;
            notificacao.ReferenciaTipo = "ConviteGrupoRespondidoAceito";

            if (convite.RemetenteUsuarioId > 0 && convite.RemetenteUsuarioId != idUsuario.Value)
            {
                _context.Notificacoes.Add(new Notificacao
                {
                    UsuarioId = convite.RemetenteUsuarioId,
                    Tipo = TipoNotificacao.Sistema,
                    Titulo = "Convite aceito",
                    Mensagem = $"{(usuarioDestino?.NomeCompleto ?? usuarioDestino?.NomeUsuario ?? "O usuário")} aceitou o convite para participar do grupo {(grupo?.Nome ?? "Grupo")}.",
                    Lida = false,
                    DataCriacao = agora,
                    DataLeitura = null,
                    LinkDestino = "/Menu/Notifications",
                    ReferenciaId = convite.Id,
                    ReferenciaTipo = "RespostaConviteGrupoAceito"
                });
            }

            await _context.SaveChangesAsync();

            return new JsonResult(new
            {
                success = true,
                message = "Convite aceito com sucesso."
            });
        }
        catch (Exception ex)
        {
            var detalhe = ex.InnerException?.Message ?? ex.Message;

            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                success = false,
                message = $"Ocorreu um erro interno ao aceitar o convite. Detalhe: {detalhe}"
            });
        }
    }

    public async Task<IActionResult> OnPostRecusarConviteAsync([FromBody] AcaoConviteRequest? request)
    {
        var idUsuario = GetUsuarioLogadoId();
        if (idUsuario == null)
        {
            return new JsonResult(new { success = false, message = "Usuário não autenticado." })
            {
                StatusCode = StatusCodes.Status401Unauthorized
            };
        }

        if (request == null || request.NotificacaoId <= 0 || request.ConviteId <= 0)
            return BadRequest(new { success = false, message = "Dados inválidos." });

        try
        {
            var notificacao = await _context.Notificacoes
                .FirstOrDefaultAsync(n => n.Id == request.NotificacaoId && n.UsuarioId == idUsuario.Value);

            if (notificacao == null)
                return NotFound(new { success = false, message = "Notificação não encontrada." });

            var convite = await _context.ConvitesGrupo
                .FirstOrDefaultAsync(c =>
                    c.Id == request.ConviteId &&
                    c.DestinatarioUsuarioId == idUsuario.Value);

            if (convite == null)
                return NotFound(new { success = false, message = "Convite não encontrado." });

            if (convite.Status != StatusConviteGrupo.Pendente)
                return BadRequest(new { success = false, message = "Esse convite já foi respondido." });

            var usuarioDestino = await _context.Usuarios
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == idUsuario.Value);

            var grupo = await _context.Grupos
                .AsNoTracking()
                .FirstOrDefaultAsync(g => g.Id == convite.GrupoId);

            var agora = DateTime.Now;

            convite.Status = StatusConviteGrupo.Recusado;
            convite.DataResposta = agora;

            notificacao.Titulo = "Convite recusado";
            notificacao.Mensagem = $"Você recusou o convite para participar do grupo {(grupo?.Nome ?? "Grupo")}.";
            notificacao.Lida = true;
            notificacao.DataLeitura = agora;
            notificacao.LinkDestino = null;
            notificacao.ReferenciaTipo = "ConviteGrupoRespondidoRecusado";

            if (convite.RemetenteUsuarioId > 0 && convite.RemetenteUsuarioId != idUsuario.Value)
            {
                _context.Notificacoes.Add(new Notificacao
                {
                    UsuarioId = convite.RemetenteUsuarioId,
                    Tipo = TipoNotificacao.Sistema,
                    Titulo = "Convite recusado",
                    Mensagem = $"{(usuarioDestino?.NomeCompleto ?? usuarioDestino?.NomeUsuario ?? "O usuário")} recusou o convite para participar do grupo {(grupo?.Nome ?? "Grupo")}.",
                    Lida = false,
                    DataCriacao = agora,
                    DataLeitura = null,
                    LinkDestino = "/Menu/Notifications",
                    ReferenciaId = convite.Id,
                    ReferenciaTipo = "RespostaConviteGrupoRecusado"
                });
            }

            await _context.SaveChangesAsync();

            return new JsonResult(new
            {
                success = true,
                message = "Convite recusado com sucesso."
            });
        }
        catch (Exception ex)
        {
            var detalhe = ex.InnerException?.Message ?? ex.Message;

            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                success = false,
                message = $"Ocorreu um erro interno ao recusar o convite. Detalhe: {detalhe}"
            });
        }
    }

    private int? GetUsuarioLogadoId()
    {
        var claim = User.FindFirst("Id")?.Value;
        return int.TryParse(claim, out var id) ? id : null;
    }
}