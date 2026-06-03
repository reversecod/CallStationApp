using CallStationApp.Data;
using CallStationApp.Models;
using CallStationApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace CallStationApp.Pages.Menu;

[Authorize]
public class MenuModel : PageModel
{
    private readonly AppDbContext _context;
    private readonly ILogger<MenuModel> _logger;
    private readonly FotoGrupoUploadService _fotoGrupoUploadService;

    public MenuModel(AppDbContext context, ILogger<MenuModel> logger, FotoGrupoUploadService fotoGrupoUploadService)
    {
        _context = context;
        _logger = logger;
        _fotoGrupoUploadService = fotoGrupoUploadService;
    }

    public Usuario? UsuarioLogado { get; set; }
    public List<GrupoViewModel> Grupos { get; set; } = new();
    public int TotalNotificacoesNaoLidas { get; set; }

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

        TotalNotificacoesNaoLidas = await _context.Notificacoes
            .AsNoTracking()
            .CountAsync(n => n.UsuarioId == UsuarioLogado.Id && !n.Lida);

        var vinculosUsuario = await _context.UsuariosGrupos
            .AsNoTracking()
            .Where(ug => ug.UsuarioId == idUsuario.Value && ug.Ativo)
            .Select(ug => new
            {
                ug.GrupoId,
                ug.Permissao,
                ug.DataAdicao,
                ug.DataUltimoAcesso,
                ug.GrupoFixado,
                ug.OrdemGrupoFixado,
                Grupo = new
                {
                    ug.Grupo!.Nome,
                    ug.Grupo.DescricaoGrupo,
                    ug.Grupo.FotoGrupo,
                    ug.Grupo.EtiquetaCor,
                    ug.Grupo.CriadorId
                }
            })
            .ToListAsync();

        if (!vinculosUsuario.Any())
            return Page();

        var grupoIds = vinculosUsuario
            .Select(ug => ug.GrupoId)
            .Distinct()
            .ToList();

        var chamadosPorGrupo = await _context.Chamados
            .AsNoTracking()
            .Where(c =>
                grupoIds.Contains(c.GrupoId) &&
                c.Status != StatusChamado.Cancelado &&
                c.Status != StatusChamado.Excluido)
            .GroupBy(c => c.GrupoId)
            .Select(g => new
            {
                GrupoId = g.Key,
                TotalChamados = g.Count(),
                ChamadosAbertos = g.Count(c => c.Status == StatusChamado.Aberto)
            })
            .ToListAsync();

        var membrosPorGrupo = await _context.UsuariosGrupos
            .AsNoTracking()
            .Where(ug => grupoIds.Contains(ug.GrupoId) && ug.Ativo)
            .GroupBy(ug => ug.GrupoId)
            .Select(g => new
            {
                GrupoId = g.Key,
                TotalMembros = g.Count()
            })
            .ToListAsync();

        var infosUsuarioGrupo = await _context.InfoUsuariosGrupos
            .AsNoTracking()
            .Where(i => i.UsuarioId == idUsuario.Value && grupoIds.Contains(i.GrupoId))
            .ToListAsync();

        var dictChamados = chamadosPorGrupo.ToDictionary(x => x.GrupoId);
        var dictMembros = membrosPorGrupo.ToDictionary(x => x.GrupoId);
        var dictInfos = infosUsuarioGrupo.ToDictionary(x => x.GrupoId);

        Grupos = vinculosUsuario
            .Select(ug =>
            {
                dictChamados.TryGetValue(ug.GrupoId, out var chamadoInfo);
                dictMembros.TryGetValue(ug.GrupoId, out var membroInfo);
                dictInfos.TryGetValue(ug.GrupoId, out var infoUsuario);

                return new GrupoViewModel
                {
                    GrupoId = ug.GrupoId,
                    Nome = ug.Grupo.Nome,
                    Descricao = ug.Grupo.DescricaoGrupo,
                    FotoGrupo = ug.Grupo.FotoGrupo,
                    EtiquetaCor = ug.Grupo.EtiquetaCor,
                    Permissao = ug.Permissao,
                    DataEntrada = ug.DataAdicao,
                    DataUltimoAcesso = ug.DataUltimoAcesso,
                    GrupoFixado = ug.GrupoFixado,
                    OrdemGrupoFixado = ug.OrdemGrupoFixado,
                    TotalChamados = chamadoInfo?.TotalChamados ?? 0,
                    ChamadosAbertos = chamadoInfo?.ChamadosAbertos ?? 0,
                    TotalMembros = membroInfo?.TotalMembros ?? 0,
                    ApelidoNoGrupo = infoUsuario?.Apelido,
                    CriadorId = ug.Grupo.CriadorId
                };
            })
            .OrderByDescending(g => g.GrupoFixado)
            .ThenBy(g => g.GrupoFixado ? g.OrdemGrupoFixado ?? int.MaxValue : int.MaxValue)
            .ThenByDescending(g => g.CriadorId == idUsuario.Value)
            .ThenBy(g => g.Nome)
            .ToList();

        return Page();
    }

    public async Task<IActionResult> OnPostCriarGrupoAsync(
        [FromForm] string nome,
        [FromForm] string? descricao,
        [FromForm] string? etiquetaCor,
        [FromForm] IFormFile? fotoGrupo)
    {
        var idUsuario = GetUsuarioLogadoId();
        if (idUsuario == null)
            return Unauthorized();

        nome = nome?.Trim() ?? string.Empty;
        descricao = descricao?.Trim();

        if (string.IsNullOrWhiteSpace(nome))
        {
            return new JsonResult(new
            {
                success = false,
                message = "Nome do grupo é obrigatório."
            });
        }

        var grupoJaExiste = await _context.Grupos
            .AnyAsync(g => g.CriadorId == idUsuario.Value && g.Nome == nome);

        if (grupoJaExiste)
        {
            return new JsonResult(new
            {
                success = false,
                message = "Você já possui um grupo com esse nome."
            });
        }

        var corConvertida = EtiquetaCor.branco;

        if (!string.IsNullOrWhiteSpace(etiquetaCor) &&
            Enum.TryParse<EtiquetaCor>(etiquetaCor.Trim(), true, out var corEnum))
        {
            corConvertida = corEnum;
        }

        if (fotoGrupo is { Length: > 0 } && !await _fotoGrupoUploadService.FotoGrupoValidaAsync(fotoGrupo))
        {
            return new JsonResult(new
            {
                success = false,
                message = FotoGrupoUploadService.MensagemArquivoInvalido
            });
        }

        var strategy = _context.Database.CreateExecutionStrategy();
        string? caminhoFotoSalva = null;

        try
        {
            return await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _context.Database.BeginTransactionAsync();

                try
                {
                    var usuario = UsuarioLogado ?? await _context.Usuarios.FirstAsync(u => u.Id == idUsuario.Value);
                    if (fotoGrupo is { Length: > 0 })
                    {
                        caminhoFotoSalva = await _fotoGrupoUploadService.SalvarFotoGrupoAsync(fotoGrupo, idUsuario.Value);
                    }

                    var novoGrupo = new Grupo
                    {
                        Nome = nome,
                        DescricaoGrupo = descricao,
                        FotoGrupo = caminhoFotoSalva,
                        EtiquetaCor = corConvertida,
                        CriadorId = idUsuario.Value,
                        Usuario = usuario,
                        DataCriacao = DateTime.UtcNow
                    };

                    _context.Grupos.Add(novoGrupo);

                    _context.UsuariosGrupos.Add(new UsuarioGrupo
                    {
                        UsuarioId = idUsuario.Value,
                        Grupo = novoGrupo,
                        Permissao = PermissaoUsuario.Administracao,
                        DataAdicao = DateTime.UtcNow,
                        Usuario = usuario
                    });

                    _context.InfoUsuariosGrupos.Add(new InfoUsuarioGrupo
                    {
                        UsuarioId = idUsuario.Value,
                        Grupo = novoGrupo,
                        DataAtualizacaoRegistro = DateTime.UtcNow,
                        Usuario = usuario
                    });

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    return (IActionResult)new JsonResult(new
                    {
                        success = true,
                        grupoId = novoGrupo.Id,
                        redirectUrl = Url.Page("/Menu/Home", new { grupoId = novoGrupo.Id })
                    });
                }
                catch
                {
                    await transaction.RollbackAsync();
                    _fotoGrupoUploadService.RemoverFotoGrupoSeExistir(caminhoFotoSalva);
                    throw;
                }
            });
        }
        catch (DbUpdateException ex) when (EhErroDuplicidade(ex))
        {
            return new JsonResult(new
            {
                success = false,
                message = "Você já possui um grupo com esse nome."
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao criar grupo para o usuario {UsuarioId}.", idUsuario.Value);
            _fotoGrupoUploadService.RemoverFotoGrupoSeExistir(caminhoFotoSalva);
            return new JsonResult(new
            {
                success = false,
                message = "Não foi possível criar o grupo no momento."
            });
        }
    }

    public async Task<IActionResult> OnGetListarNotificacoesAsync()
    {
        var idUsuario = GetUsuarioLogadoId();
        if (idUsuario == null)
            return new JsonResult(new { success = false, message = "Usuário não autenticado." });

        var notificacoes = await _context.Notificacoes
            .AsNoTracking()
            .Where(n => n.UsuarioId == idUsuario.Value && !n.Lida)
            .OrderByDescending(n => n.DataCriacao)
            .Take(12)
            .Select(n => new
            {
                id = n.Id,
                tipo = n.Tipo.ToString(),
                titulo = n.Titulo,
                mensagem = n.Mensagem,
                nomeGrupo = n.Grupo != null ? n.Grupo.Nome : string.Empty,
                etiquetaCor = n.Grupo != null ? n.Grupo.EtiquetaCor.ToString() : string.Empty,
                lida = n.Lida,
                dataCriacao = n.DataCriacao,
                grupoId = n.GrupoId,
                linkDestino = n.LinkDestino
            })
            .ToListAsync();

        var naoLidas = await _context.Notificacoes
            .AsNoTracking()
            .CountAsync(n => n.UsuarioId == idUsuario.Value && !n.Lida);

        return new JsonResult(new
        {
            success = true,
            notificacoes,
            totalNaoLidas = naoLidas
        });
    }

    public async Task<IActionResult> OnGetAbrirNotificacoesAsync()
    {
        var idUsuario = GetUsuarioLogadoId();
        if (idUsuario == null)
            return RedirectToPage("/Auth/Login");

        var grupoId = await _context.Notificacoes
            .AsNoTracking()
            .Where(n => n.UsuarioId == idUsuario.Value)
            .OrderByDescending(n => !n.Lida)
            .ThenByDescending(n => n.DataCriacao)
            .Select(n => (int?)n.GrupoId)
            .FirstOrDefaultAsync();

        if (!grupoId.HasValue)
        {
            grupoId = await _context.UsuariosGrupos
                .AsNoTracking()
                .Where(ug => ug.UsuarioId == idUsuario.Value && ug.Ativo)
                .OrderByDescending(ug => ug.DataUltimoAcesso ?? ug.DataAdicao)
                .Select(ug => (int?)ug.GrupoId)
                .FirstOrDefaultAsync();
        }

        if (!grupoId.HasValue)
            return RedirectToPage("/Menu/Notifications");

        var possuiAcesso = await _context.UsuariosGrupos
            .AsNoTracking()
            .AnyAsync(ug => ug.UsuarioId == idUsuario.Value && ug.GrupoId == grupoId.Value && ug.Ativo);

        var possuiNotificacaoNoGrupo = await _context.Notificacoes
            .AsNoTracking()
            .AnyAsync(n => n.UsuarioId == idUsuario.Value && n.GrupoId == grupoId.Value);

        if (!possuiAcesso && !possuiNotificacaoNoGrupo)
            return RedirectToPage("/Menu/Menu");

        return RedirectToPage("/Menu/Notifications", new { grupoId = grupoId.Value });
    }

    public async Task<IActionResult> OnGetListarGruposRecentesAsync()
    {
        var idUsuario = GetUsuarioLogadoId();
        if (idUsuario == null)
            return Unauthorized();

        var gruposBase = await _context.UsuariosGrupos
            .AsNoTracking()
            .Where(ug => ug.UsuarioId == idUsuario.Value && ug.Ativo)
            .OrderByDescending(ug => ug.GrupoFixado)
            .ThenBy(ug => ug.GrupoFixado ? ug.OrdemGrupoFixado ?? int.MaxValue : int.MaxValue)
            .ThenByDescending(ug => ug.DataUltimoAcesso ?? ug.DataAdicao)
            .ThenBy(ug => ug.Grupo.Nome)
            .Take(20)
            .Select(ug => new
            {
                id = ug.GrupoId,
                nome = ug.Grupo.Nome,
                etiquetaCor = ug.Grupo.EtiquetaCor,
                dataUltimoAcesso = ug.DataUltimoAcesso ?? ug.DataAdicao,
                permissao = ug.Permissao.ToString(),
                fixado = ug.GrupoFixado,
                ordemFixada = ug.OrdemGrupoFixado
            })
            .ToListAsync();

        var gruposIds = gruposBase.Select(g => g.id).ToList();
        var chamadosAtivosPorGrupo = gruposIds.Count == 0
            ? new Dictionary<int, int>()
            : await (
                from chamado in _context.Chamados.AsNoTracking()
                join usuarioGrupo in _context.UsuariosGrupos.AsNoTracking()
                    on chamado.GrupoId equals usuarioGrupo.GrupoId
                where usuarioGrupo.UsuarioId == idUsuario.Value &&
                      usuarioGrupo.Ativo &&
                      gruposIds.Contains(chamado.GrupoId) &&
                      (chamado.Status == StatusChamado.Aberto ||
                       chamado.Status == StatusChamado.EmAndamento ||
                       chamado.Status == StatusChamado.EmAtraso ||
                       chamado.Status == StatusChamado.Pendente) &&
                      (
                          usuarioGrupo.Permissao == PermissaoUsuario.Administracao ||
                          usuarioGrupo.Permissao == PermissaoUsuario.Tecnico ||
                          (usuarioGrupo.Permissao == PermissaoUsuario.Colaborador &&
                              (chamado.Publico || chamado.CriadorChamadoId == idUsuario.Value)) ||
                          (usuarioGrupo.Permissao == PermissaoUsuario.Nenhuma && chamado.Publico)
                      )
                group chamado by chamado.GrupoId into grupo
                select new
                {
                    GrupoId = grupo.Key,
                    Total = grupo.Count()
                })
                .ToDictionaryAsync(x => x.GrupoId, x => x.Total);

        var grupos = gruposBase.Select(grupo => new
        {
            grupo.id,
            grupo.nome,
            grupo.etiquetaCor,
            grupo.dataUltimoAcesso,
            grupo.permissao,
            grupo.fixado,
            grupo.ordemFixada,
            chamadosAtivos = chamadosAtivosPorGrupo.GetValueOrDefault(grupo.id)
        }).ToList();

        return new JsonResult(new
        {
            success = true,
            dados = new { grupos }
        });
    }

    public async Task<IActionResult> OnPostAlternarGrupoFixadoAsync([FromBody] GrupoFixadoRequest request)
    {
        var idUsuario = GetUsuarioLogadoId();
        if (idUsuario == null)
            return Unauthorized();

        if (request == null || request.GrupoId <= 0)
            return BadRequest(new { success = false, message = "Grupo inválido." });

        var strategy = _context.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync<IActionResult>(async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var vinculo = await _context.UsuariosGrupos
                    .FirstOrDefaultAsync(ug => ug.UsuarioId == idUsuario.Value && ug.GrupoId == request.GrupoId && ug.Ativo);

                if (vinculo == null)
                    return NotFound(new { success = false, message = "Organização não encontrada para este usuário." });

                if (vinculo.GrupoFixado)
                {
                    vinculo.GrupoFixado = false;
                    vinculo.OrdemGrupoFixado = null;
                }
                else
                {
                    var proximaOrdem = await _context.UsuariosGrupos
                        .Where(ug => ug.UsuarioId == idUsuario.Value && ug.Ativo && ug.GrupoFixado)
                        .MaxAsync(ug => (int?)ug.OrdemGrupoFixado) ?? 0;

                    vinculo.GrupoFixado = true;
                    vinculo.OrdemGrupoFixado = proximaOrdem + 1;
                }

                await _context.SaveChangesAsync();
                await NormalizarOrdemGruposFixadosAsync(idUsuario.Value);
                await transaction.CommitAsync();

                return new JsonResult(new { success = true, message = vinculo.GrupoFixado ? "Organização fixada." : "Organização desafixada." });
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        });
    }

    public async Task<IActionResult> OnGetAcessarGrupoAsync(int grupoId)
    {
        var idUsuario = GetUsuarioLogadoId();
        if (idUsuario == null)
            return RedirectToPage("/Auth/Login");

        if (grupoId <= 0)
            return RedirectToPage("/Menu/Menu");

        var vinculo = await _context.UsuariosGrupos
            .FirstOrDefaultAsync(ug => ug.UsuarioId == idUsuario.Value && ug.GrupoId == grupoId && ug.Ativo);

        if (vinculo == null)
            return RedirectToPage("/Menu/Menu");

        var strategy = _context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                vinculo.DataUltimoAcesso = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        });

        return RedirectToPage("/Menu/Home", new { grupoId });
    }

    public async Task<IActionResult> OnPostMarcarNotificacoesComoLidasAsync()
    {
        var idUsuario = GetUsuarioLogadoId();
        if (idUsuario == null)
            return new JsonResult(new { success = false, message = "Usuário não autenticado." });

        var strategy = _context.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync<IActionResult>(async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var notificacoesNaoLidas = await _context.Notificacoes
                    .Where(n => n.UsuarioId == idUsuario.Value && !n.Lida)
                    .ToListAsync();

                foreach (var notificacao in notificacoesNaoLidas)
                {
                    notificacao.Lida = true;
                    notificacao.DataLeitura = DateTime.UtcNow;
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return new JsonResult(new { success = true });
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        });
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

    private async Task NormalizarOrdemGruposFixadosAsync(int usuarioId)
    {
        var fixados = await _context.UsuariosGrupos
            .Where(ug => ug.UsuarioId == usuarioId && ug.Ativo && ug.GrupoFixado)
            .OrderBy(ug => ug.OrdemGrupoFixado ?? int.MaxValue)
            .ThenBy(ug => ug.Grupo.Nome)
            .ToListAsync();

        for (var i = 0; i < fixados.Count; i++)
            fixados[i].OrdemGrupoFixado = i + 1;

        await _context.SaveChangesAsync();
    }

    public class GrupoFixadoRequest
    {
        public int GrupoId { get; set; }
    }

}

public class GrupoViewModel
{
    public int GrupoId { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string? Descricao { get; set; }
    public string? FotoGrupo { get; set; }
    public EtiquetaCor EtiquetaCor { get; set; }
    public PermissaoUsuario Permissao { get; set; }
    public DateTime DataEntrada { get; set; }
    public DateTime? DataUltimoAcesso { get; set; }
    public bool GrupoFixado { get; set; }
    public int? OrdemGrupoFixado { get; set; }
    public int TotalChamados { get; set; }
    public int ChamadosAbertos { get; set; }
    public int TotalMembros { get; set; }
    public string? ApelidoNoGrupo { get; set; }
    public int CriadorId { get; set; }

    public string CorCss => EtiquetaCor switch
    {
        EtiquetaCor.vermelho => "#ef4444",
        EtiquetaCor.laranja => "#f97316",
        EtiquetaCor.amarelo => "#eab308",
        EtiquetaCor.verde => "#22c55e",
        EtiquetaCor.azul => "#3b82f6",
        EtiquetaCor.roxo => "#a855f7",
        EtiquetaCor.rosa => "#ec4899",
        EtiquetaCor.preto => "#1f2937",
        EtiquetaCor.branco => "#e5e7eb",
        _ => "#6b7280"
    };

    public string PermissaoIcone => Permissao switch
    {
        PermissaoUsuario.Administracao => "bi-shield-fill",
        PermissaoUsuario.Tecnico => "bi-tools",
        PermissaoUsuario.Colaborador => "bi-person-fill",
        _ => "bi-eye"
    };

    public string PermissaoLabel => Permissao switch
    {
        PermissaoUsuario.Administracao => "Administrador",
        PermissaoUsuario.Tecnico => "Técnico",
        PermissaoUsuario.Colaborador => "Colaborador",
        _ => "Sem permissão"
    };
}
