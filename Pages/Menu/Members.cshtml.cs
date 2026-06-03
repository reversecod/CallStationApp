using CallStationApp.Data;
using CallStationApp.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace CallStationApp.Pages.Menu;

[Authorize]
public class MembersModel : PageModel
{
    private readonly AppDbContext _context;
    private readonly ILogger<MembersModel> _logger;

    public MembersModel(AppDbContext context, ILogger<MembersModel> logger)
    {
        _context = context;
        _logger = logger;
    }

    [BindProperty(SupportsGet = true)]
    public int GrupoId { get; set; }

    public Grupo? GrupoAtual { get; set; }
    public GrupoConfiguracao? Configuracao { get; set; }
    public string? NomeUsuarioLogado { get; set; }
    public string? FotoUsuarioLogado { get; set; }
    public int UsuarioLogadoId { get; set; }
    public bool UsuarioLogadoEhAdministrador { get; set; }
    public PermissaoUsuario UsuarioLogadoPermissao { get; set; } = PermissaoUsuario.Nenhuma;
    public int TotalAdministradores { get; set; }
    public List<MembroGrupoViewModel> Membros { get; set; } = new();

    public async Task<IActionResult> OnGetAsync()
    {
        var idUsuario = GetUsuarioLogadoId();
        if (idUsuario == null)
            return RedirectToPage("/Auth/Login");

        if (GrupoId <= 0)
            return RedirectToPage("/Menu/Menu");

        var usuarioNoGrupo = await _context.UsuariosGrupos
            .AsNoTracking()
            .FirstOrDefaultAsync(ug => ug.UsuarioId == idUsuario.Value && ug.GrupoId == GrupoId && ug.Ativo);

        if (usuarioNoGrupo == null)
            return RedirectToPage("/Menu/Menu");

        UsuarioLogadoId = idUsuario.Value;
        UsuarioLogadoEhAdministrador = usuarioNoGrupo.Permissao == PermissaoUsuario.Administracao;
        UsuarioLogadoPermissao = usuarioNoGrupo.Permissao;

        GrupoAtual = await _context.Grupos
            .AsNoTracking()
            .FirstOrDefaultAsync(g => g.Id == GrupoId);

        if (GrupoAtual == null)
            return RedirectToPage("/Menu/Menu");

        Configuracao = await _context.GruposConfiguracoes
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.GrupoId == GrupoId);

        var usuarioLogado = await _context.Usuarios
            .AsNoTracking()
            .Where(u => u.Id == idUsuario.Value)
            .Select(u => new { u.NomeUsuario, u.FotoUsuario })
            .FirstOrDefaultAsync();

        NomeUsuarioLogado = usuarioLogado?.NomeUsuario;
        FotoUsuarioLogado = usuarioLogado?.FotoUsuario;

        IQueryable<MembroGrupoViewModel> membrosQuery =
            from ug in _context.UsuariosGrupos.AsNoTracking()
            join u in _context.Usuarios.AsNoTracking()
                on ug.UsuarioId equals u.Id
            join iug in _context.InfoUsuariosGrupos.AsNoTracking()
                on new { ug.UsuarioId, ug.GrupoId } equals new { iug.UsuarioId, iug.GrupoId } into infoJoin
            from info in infoJoin.DefaultIfEmpty()
            where ug.GrupoId == GrupoId && ug.Ativo
            orderby u.NomeCompleto
            select new MembroGrupoViewModel
            {
                UsuarioId = u.Id,
                GrupoId = ug.GrupoId,
                NomeCompleto = u.NomeCompleto,
                NomeUsuario = u.NomeUsuario,
                Email = u.Email,
                FotoUsuario = u.FotoUsuario,
                Permissao = ug.Permissao,
                DataAdicao = ug.DataAdicao,
                Apelido = info != null ? info.Apelido : null,
                DescricaoAtivo = info != null ? info.DescricaoAtivo : null,
                IdentificadorInterno = info != null ? info.IdentificadorInterno : null,
                Observacao = info != null ? info.Observacao : null,
                DataAtualizacaoAtivo = info != null ? info.DataAtualizacaoAtivo : null,
                DataAtualizacaoRegistro = info != null ? info.DataAtualizacaoRegistro : null
            };

        if (usuarioNoGrupo.Permissao == PermissaoUsuario.Nenhuma)
            membrosQuery = membrosQuery.Where(m => m.UsuarioId == idUsuario.Value);

        Membros = await membrosQuery.ToListAsync();

        TotalAdministradores = Membros.Count(m => m.Permissao == PermissaoUsuario.Administracao);

        return Page();
    }

    public async Task<IActionResult> OnGetBuscarUsuariosAsync(string termo)
    {
        var idUsuario = GetUsuarioLogadoId();
        if (idUsuario == null)
            return new JsonResult(new { success = false, message = "Usuário não autenticado." });

        if (GrupoId <= 0)
            return new JsonResult(new { success = false, message = "Grupo inválido." });

        var solicitanteNoGrupo = await _context.UsuariosGrupos
            .AsNoTracking()
            .FirstOrDefaultAsync(ug => ug.UsuarioId == idUsuario.Value && ug.GrupoId == GrupoId && ug.Ativo);

        if (solicitanteNoGrupo == null)
            return new JsonResult(new { success = false, message = "Você não pertence a este grupo." });

        if (solicitanteNoGrupo.Permissao != PermissaoUsuario.Administracao)
            return new JsonResult(new { success = false, message = "Somente administradores podem buscar usuários." });

        termo = (termo ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(termo) || termo.Length < 2)
            return new JsonResult(new { success = true, usuarios = new List<object>() });

        var usuarios = await _context.Usuarios
            .AsNoTracking()
            .Where(u =>
                (u.NomeCompleto.Contains(termo) ||
                 u.NomeUsuario.Contains(termo) ||
                 (u.Email != null && u.Email.Contains(termo)))
                && u.Id != idUsuario.Value
                && !_context.UsuariosGrupos.Any(ug => ug.UsuarioId == u.Id && ug.GrupoId == GrupoId && ug.Ativo)
                && !_context.ConvitesGrupo.Any(c =>
                    c.GrupoId == GrupoId &&
                    c.DestinatarioUsuarioId == u.Id &&
                    c.Status == StatusConviteGrupo.Pendente)
            )
            .OrderBy(u => u.NomeCompleto)
            .Take(10)
            .Select(u => new
            {
                id = u.Id,
                nomeCompleto = u.NomeCompleto,
                nomeUsuario = u.NomeUsuario,
                email = u.Email,
                fotoUsuario = u.FotoUsuario
            })
            .ToListAsync();

        return new JsonResult(new { success = true, usuarios });
    }

    public async Task<IActionResult> OnPostEnviarConviteAsync([FromBody] EnviarConviteRequest request)
    {
        var idUsuario = GetUsuarioLogadoId();
        if (idUsuario == null)
            return new JsonResult(new { success = false, message = "Usuário não autenticado." });

        if (request == null || request.DestinatarioUsuarioId <= 0)
            return new JsonResult(new { success = false, message = "Usuário inválido." });

        if (GrupoId <= 0)
            return new JsonResult(new { success = false, message = "Grupo inválido." });

        var solicitanteNoGrupo = await _context.UsuariosGrupos
            .FirstOrDefaultAsync(ug => ug.UsuarioId == idUsuario.Value && ug.GrupoId == GrupoId && ug.Ativo);

        if (solicitanteNoGrupo == null)
            return new JsonResult(new { success = false, message = "Você não pertence a este grupo." });

        if (solicitanteNoGrupo.Permissao != PermissaoUsuario.Administracao)
            return new JsonResult(new { success = false, message = "Somente administradores podem enviar convites." });

        if (idUsuario.Value == request.DestinatarioUsuarioId)
            return new JsonResult(new { success = false, message = "Você não pode enviar convite para si mesmo." });

        var usuarioJaNoGrupo = await _context.UsuariosGrupos
            .AnyAsync(ug => ug.UsuarioId == request.DestinatarioUsuarioId && ug.GrupoId == GrupoId && ug.Ativo);

        if (usuarioJaNoGrupo)
            return new JsonResult(new { success = false, message = "Este usuário já faz parte do grupo." });

        var convitePendente = await _context.ConvitesGrupo
            .AnyAsync(c =>
                c.GrupoId == GrupoId &&
                c.DestinatarioUsuarioId == request.DestinatarioUsuarioId &&
                c.Status == StatusConviteGrupo.Pendente);

        if (convitePendente)
            return new JsonResult(new { success = false, message = "Já existe uma solicitação pendente para este usuário." });

        var grupo = await _context.Grupos
            .AsNoTracking()
            .FirstOrDefaultAsync(g => g.Id == GrupoId);

        var remetente = await _context.Usuarios
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == idUsuario.Value);

        var destinatario = await _context.Usuarios
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == request.DestinatarioUsuarioId);

        if (grupo == null || remetente == null || destinatario == null)
            return new JsonResult(new { success = false, message = "Dados do convite inválidos." });

        var agora = DateTime.UtcNow;
        var strategy = _context.Database.CreateExecutionStrategy();

        try
        {
            return await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _context.Database.BeginTransactionAsync();

                try
                {
                    var convite = new ConviteGrupo
                    {
                        GrupoId = GrupoId,
                        RemetenteUsuarioId = idUsuario.Value,
                        DestinatarioUsuarioId = request.DestinatarioUsuarioId,
                        Status = StatusConviteGrupo.Pendente,
                        Mensagem = string.IsNullOrWhiteSpace(request.Mensagem)
                            ? null
                            : request.Mensagem.Trim()[..Math.Min(request.Mensagem.Trim().Length, 255)],
                        DataCriacao = agora
                    };

                    _context.ConvitesGrupo.Add(convite);
                    await _context.SaveChangesAsync();

                    var notificacao = new Notificacao
                    {
                        UsuarioId = destinatario.Id,
                        GrupoId = GrupoId,
                        Tipo = TipoNotificacao.ConviteGrupo,
                        Titulo = "Novo convite de grupo",
                        Mensagem = $"{remetente.NomeCompleto} convidou você para participar do grupo {grupo.Nome}.",
                        Lida = false,
                        DataCriacao = agora,
                        ReferenciaId = convite.Id,
                        ReferenciaTipo = "ConviteGrupo",
                        LinkDestino = $"/Menu/Notifications?grupoId={GrupoId}"
                    };

                    _context.Notificacoes.Add(notificacao);
                    await _context.SaveChangesAsync();

                    await transaction.CommitAsync();

                    return (IActionResult)new JsonResult(new
                    {
                        success = true,
                        message = "Solicitação enviada com sucesso."
                    });
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            });
        }
        catch (DbUpdateException ex) when (EhErroDuplicidade(ex))
        {
            return new JsonResult(new
            {
                success = false,
                message = "Já existe uma solicitação pendente para este usuário."
            });
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Erro de banco ao enviar convite no grupo {GrupoId}.", GrupoId);
            return new JsonResult(new
            {
                success = false,
                message = "Não foi possível enviar o convite no momento."
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao enviar convite no grupo {GrupoId}.", GrupoId);
            return new JsonResult(new
            {
                success = false,
                message = "Não foi possível enviar o convite no momento."
            });
        }
    }

    public async Task<IActionResult> OnPostEditarApelidoAsync([FromBody] EditarApelidoRequest request)
    {
        var idUsuario = GetUsuarioLogadoId();
        if (idUsuario == null)
            return new JsonResult(new { success = false, message = "Usuário não autenticado." });

        if (GrupoId <= 0)
            return new JsonResult(new { success = false, message = "Grupo inválido." });

        if (request == null || request.UsuarioId <= 0)
            return new JsonResult(new { success = false, message = "Usuário inválido." });

        var strategy = _context.Database.CreateExecutionStrategy();
        try
        {
            return await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    var solicitanteNoGrupo = await _context.UsuariosGrupos
                        .FirstOrDefaultAsync(ug => ug.UsuarioId == idUsuario.Value && ug.GrupoId == GrupoId && ug.Ativo);

                    if (solicitanteNoGrupo == null)
                        return (IActionResult)new JsonResult(new { success = false, message = "Você não pertence a este grupo." });

                    if (solicitanteNoGrupo.Permissao != PermissaoUsuario.Administracao)
                        return (IActionResult)new JsonResult(new { success = false, message = "Somente administradores podem editar apelidos." });

                    var membroExiste = await _context.UsuariosGrupos
                        .AnyAsync(ug => ug.UsuarioId == request.UsuarioId && ug.GrupoId == GrupoId && ug.Ativo);

                    if (!membroExiste)
                        return (IActionResult)new JsonResult(new { success = false, message = "Membro não encontrado no grupo." });

                    var info = await _context.InfoUsuariosGrupos
                        .FirstOrDefaultAsync(i => i.UsuarioId == request.UsuarioId && i.GrupoId == GrupoId);

                    var apelidoNormalizado = string.IsNullOrWhiteSpace(request.Apelido)
                        ? null
                        : request.Apelido.Trim();

                    if (info == null)
                    {
                        info = new InfoUsuarioGrupo
                        {
                            UsuarioId = request.UsuarioId,
                            GrupoId = GrupoId,
                            Apelido = apelidoNormalizado,
                            DataAtualizacaoRegistro = DateTime.UtcNow
                        };

                        _context.InfoUsuariosGrupos.Add(info);
                    }
                    else
                    {
                        info.Apelido = apelidoNormalizado;
                        info.DataAtualizacaoRegistro = DateTime.UtcNow;
                    }

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    return (IActionResult)new JsonResult(new
                    {
                        success = true,
                        message = "Apelido atualizado com sucesso."
                    });
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            });
        }
        catch (DbUpdateException ex) when (EhErroDuplicidade(ex))
        {
            _logger.LogWarning(ex, "Duplicidade ao editar apelido do usuario {UsuarioAlvoId} no grupo {GrupoId}.", request.UsuarioId, GrupoId);
            return new JsonResult(new { success = false, message = "O registro do membro foi alterado por outra operação. Tente novamente." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao editar apelido do usuario {UsuarioAlvoId} no grupo {GrupoId}.", request.UsuarioId, GrupoId);
            return new JsonResult(new { success = false, message = "Não foi possível atualizar o apelido no momento." });
        }
    }

    public async Task<IActionResult> OnPostAlterarPermissaoAsync([FromBody] AlterarPermissaoRequest request)
    {
        var idUsuario = GetUsuarioLogadoId();
        if (idUsuario == null)
            return new JsonResult(new { success = false, message = "Usuário não autenticado." });

        if (request == null || request.UsuarioId <= 0)
            return new JsonResult(new { success = false, message = "Usuário inválido." });

        if (GrupoId <= 0)
            return new JsonResult(new { success = false, message = "Grupo inválido." });

        if (!Enum.TryParse<PermissaoUsuario>(request.NovaPermissao, out var novaPermissao))
            return new JsonResult(new { success = false, message = "Permissão inválida." });

        var strategy = _context.Database.CreateExecutionStrategy();

        try
        {
            return await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);

                try
                {
                    var solicitanteNoGrupo = await _context.UsuariosGrupos
                        .FirstOrDefaultAsync(ug => ug.UsuarioId == idUsuario.Value && ug.GrupoId == GrupoId && ug.Ativo);

                    if (solicitanteNoGrupo == null)
                        return (IActionResult)new JsonResult(new { success = false, message = "Você não pertence a este grupo." });

                    if (solicitanteNoGrupo.Permissao != PermissaoUsuario.Administracao)
                        return (IActionResult)new JsonResult(new { success = false, message = "Somente administradores podem alterar permissões." });

                    var membroAlvo = await _context.UsuariosGrupos
                        .FirstOrDefaultAsync(ug => ug.UsuarioId == request.UsuarioId && ug.GrupoId == GrupoId && ug.Ativo);

                    if (membroAlvo == null)
                        return (IActionResult)new JsonResult(new { success = false, message = "Membro não encontrado no grupo." });

                    if (membroAlvo.UsuarioId == idUsuario.Value && novaPermissao != PermissaoUsuario.Administracao)
                    {
                        return (IActionResult)new JsonResult(new
                        {
                            success = false,
                            message = "Você não pode remover sua própria permissão de administrador."
                        });
                    }

                    if (membroAlvo.Permissao == novaPermissao)
                    {
                        return (IActionResult)new JsonResult(new
                        {
                            success = false,
                            message = "O membro já possui essa permissão."
                        });
                    }

                    var totalAdministradores = await _context.UsuariosGrupos
                        .CountAsync(ug => ug.GrupoId == GrupoId && ug.Ativo && ug.Permissao == PermissaoUsuario.Administracao);

                    if (membroAlvo.Permissao == PermissaoUsuario.Administracao &&
                        novaPermissao != PermissaoUsuario.Administracao &&
                        totalAdministradores <= 1)
                    {
                        return (IActionResult)new JsonResult(new
                        {
                            success = false,
                            message = "O grupo precisa ter pelo menos um administrador."
                        });
                    }

                    membroAlvo.Permissao = novaPermissao;
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    return (IActionResult)new JsonResult(new
                    {
                        success = true,
                        message = "Permissão alterada com sucesso."
                    });
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao alterar permissao do usuario {UsuarioAlvoId} no grupo {GrupoId}.", request.UsuarioId, GrupoId);
            return new JsonResult(new { success = false, message = "Não foi possível alterar a permissão no momento." });
        }
    }
    public async Task<IActionResult> OnPostRemoverMembroAsync([FromBody] RemoverMembroRequest request)
    {
        var idUsuario = GetUsuarioLogadoId();
        if (idUsuario == null)
            return new JsonResult(new { success = false, message = "Usuário não autenticado." });

        if (GrupoId <= 0)
            return new JsonResult(new { success = false, message = "Grupo inválido." });

        if (request == null || request.UsuarioId <= 0)
            return new JsonResult(new { success = false, message = "Usuário inválido." });

        var strategy = _context.Database.CreateExecutionStrategy();

        try
        {
            return await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);

                try
                {
                    var solicitanteNoGrupo = await _context.UsuariosGrupos
                        .FirstOrDefaultAsync(ug => ug.UsuarioId == idUsuario.Value && ug.GrupoId == GrupoId && ug.Ativo);

                    if (solicitanteNoGrupo == null)
                        return (IActionResult)new JsonResult(new { success = false, message = "Você não pertence a este grupo." });

                    var membroAlvo = await _context.UsuariosGrupos
                        .FirstOrDefaultAsync(ug => ug.UsuarioId == request.UsuarioId && ug.GrupoId == GrupoId && ug.Ativo);

                    if (membroAlvo == null)
                        return (IActionResult)new JsonResult(new { success = false, message = "Membro não encontrado no grupo." });

                    var saidaPropria = membroAlvo.UsuarioId == idUsuario.Value;
                    if (!saidaPropria && solicitanteNoGrupo.Permissao != PermissaoUsuario.Administracao)
                        return (IActionResult)new JsonResult(new { success = false, message = "Somente administradores podem remover outros membros." });

                    var totalAdministradores = await _context.UsuariosGrupos
                        .CountAsync(ug => ug.GrupoId == GrupoId && ug.Ativo && ug.Permissao == PermissaoUsuario.Administracao);

                    if (membroAlvo.Permissao == PermissaoUsuario.Administracao && totalAdministradores <= 1)
                    {
                        var mensagem = saidaPropria
                            ? "Você é o último administrador. Para sair, primeiro promova outro administrador ou exclua o grupo."
                            : "Não é possível remover o último administrador do grupo.";

                        return (IActionResult)new JsonResult(new { success = false, message = mensagem });
                    }

                    membroAlvo.Ativo = false;
                    membroAlvo.Permissao = PermissaoUsuario.Nenhuma;
                    membroAlvo.DataRemocao = DateTime.UtcNow;
                    membroAlvo.RemovidoPorUsuarioId = idUsuario.Value;

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    return (IActionResult)new JsonResult(new
                    {
                        success = true,
                        message = saidaPropria ? "Você saiu do grupo." : "Membro removido do grupo com sucesso.",
                        redirectUrl = saidaPropria ? Url.Page("/Menu/Menu") : null
                    });
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao remover usuario {UsuarioAlvoId} do grupo {GrupoId}.", request.UsuarioId, GrupoId);
            return new JsonResult(new { success = false, message = "Não foi possível remover o membro no momento." });
        }
    }
    private int? GetUsuarioLogadoId()
    {
        var claim = User.FindFirst("Id")?.Value;
        return int.TryParse(claim, out var id) ? id : null;
    }

    private static bool EhErroDuplicidade(DbUpdateException ex)
    {
        var mensagem = ex.InnerException?.Message ?? ex.Message;
        return mensagem.Contains("Duplicate", StringComparison.OrdinalIgnoreCase) ||
               mensagem.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase);
    }

    public class MembroGrupoViewModel
    {
        public int UsuarioId { get; set; }
        public int GrupoId { get; set; }
        public string NomeCompleto { get; set; } = string.Empty;
        public string NomeUsuario { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string? FotoUsuario { get; set; }
        public PermissaoUsuario Permissao { get; set; }
        public DateTime DataAdicao { get; set; }
        public string? Apelido { get; set; }
        public string? DescricaoAtivo { get; set; }
        public string? IdentificadorInterno { get; set; }
        public string? Observacao { get; set; }
        public DateTime? DataAtualizacaoAtivo { get; set; }
        public DateTime? DataAtualizacaoRegistro { get; set; }
    }

    public class EnviarConviteRequest
    {
        public int DestinatarioUsuarioId { get; set; }
        public string? Mensagem { get; set; }
    }

    public class EditarApelidoRequest
    {
        public int UsuarioId { get; set; }
        public string? Apelido { get; set; }
    }

    public class AlterarPermissaoRequest
    {
        public int UsuarioId { get; set; }
        public string NovaPermissao { get; set; } = string.Empty;
    }

    public class RemoverMembroRequest
    {
        public int UsuarioId { get; set; }
    }
}
