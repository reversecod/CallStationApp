using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using CallStationApp.Data;
using CallStationApp.Models;

namespace CallStationApp.Pages.Menu;

[Authorize]
public class MenuModel : PageModel
{
    private readonly AppDbContext _context;

    public MenuModel(AppDbContext context)
    {
        _context = context;
    }

    public Usuario? UsuarioLogado { get; set; }
    public List<GrupoViewModel> Grupos { get; set; } = new();
    public int TotalNotificacoesNaoLidas { get; set; }
    public List<NotificacaoViewModel> Notificacoes { get; set; } = new();

    public class NotificacaoViewModel
    {
        public int Id { get; set; }
        public string Tipo { get; set; } = string.Empty;
        public string Titulo { get; set; } = string.Empty;
        public string Mensagem { get; set; } = string.Empty;
        public bool Lida { get; set; }
        public DateTime DataCriacao { get; set; }
        public string? LinkDestino { get; set; }
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

        if (ModelState.IsValid && UsuarioLogado != null)
{
    TotalNotificacoesNaoLidas = await _context.Notificacoes
        .AsNoTracking()
        .CountAsync(n => n.UsuarioId == UsuarioLogado.Id && !n.Lida);

    Notificacoes = await _context.Notificacoes
        .AsNoTracking()
        .Where(n => n.UsuarioId == UsuarioLogado.Id)
        .OrderByDescending(n => n.DataCriacao)
        .Take(20)
        .Select(n => new NotificacaoViewModel
        {
            Id = n.Id,
            Tipo = n.Tipo.ToString(),
            Titulo = n.Titulo,
            Mensagem = n.Mensagem,
            Lida = n.Lida,
            DataCriacao = n.DataCriacao,
            LinkDestino = n.LinkDestino
        })
        .ToListAsync();
}

        var vinculosUsuario = await _context.UsuariosGrupos
            .AsNoTracking()
            .Where(ug => ug.UsuarioId == idUsuario.Value)
            .Include(ug => ug.Grupo)
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
            .Where(ug => grupoIds.Contains(ug.GrupoId))
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
            .Where(ug => ug.Grupo != null)
            .Select(ug =>
            {
                dictChamados.TryGetValue(ug.GrupoId, out var chamadoInfo);
                dictMembros.TryGetValue(ug.GrupoId, out var membroInfo);
                dictInfos.TryGetValue(ug.GrupoId, out var infoUsuario);

                return new GrupoViewModel
                {
                    GrupoId = ug.GrupoId,
                    Nome = ug.Grupo!.Nome,
                    Descricao = ug.Grupo.DescricaoGrupo,
                    FotoGrupo = ug.Grupo.FotoGrupo,
                    EtiquetaCor = ug.Grupo.EtiquetaCor,
                    Permissao = ug.Permissao,
                    DataEntrada = ug.DataAdicao,
                    TotalChamados = chamadoInfo?.TotalChamados ?? 0,
                    ChamadosAbertos = chamadoInfo?.ChamadosAbertos ?? 0,
                    TotalMembros = membroInfo?.TotalMembros ?? 0,
                    ApelidoNoGrupo = infoUsuario?.Apelido,
                    CriadorId = ug.Grupo.CriadorId
                };
            })
            .OrderByDescending(g => g.CriadorId == idUsuario.Value)
            .ThenBy(g => g.Nome)
            .ToList();

        return Page();
    }

    public async Task<IActionResult> OnPostCriarGrupoAsync(
        [FromForm] string nome,
        [FromForm] string? descricao,
        [FromForm] string? etiquetaCor)
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

        // Como Grupo.EtiquetaCor não é nullable, define padrão
        var corConvertida = EtiquetaCor.branco;

        if (!string.IsNullOrWhiteSpace(etiquetaCor) &&
            Enum.TryParse<EtiquetaCor>(etiquetaCor.Trim(), true, out var corEnum))
        {
            corConvertida = corEnum;
        }

        var novoGrupo = new Grupo
        {
            Nome = nome,
            DescricaoGrupo = descricao,
            EtiquetaCor = corConvertida,
            CriadorId = idUsuario.Value,
            Usuario = UsuarioLogado ?? await _context.Usuarios.FirstAsync(u => u.Id == idUsuario.Value),
            DataCriacao = DateTime.Now
        };

        _context.Grupos.Add(novoGrupo);
        await _context.SaveChangesAsync();

        var usuarioGrupo = new UsuarioGrupo
        {
            UsuarioId = idUsuario.Value,
            GrupoId = novoGrupo.Id,
            Permissao = PermissaoUsuario.Administracao,
            DataAdicao = DateTime.Now,
            Usuario = UsuarioLogado ?? await _context.Usuarios.FirstAsync(u => u.Id == idUsuario.Value),
            Grupo = novoGrupo
        };

        _context.UsuariosGrupos.Add(usuarioGrupo);

        var infoUsuarioGrupo = new InfoUsuarioGrupo
        {
            UsuarioId = idUsuario.Value,
            GrupoId = novoGrupo.Id,
            DataAtualizacaoRegistro = DateTime.Now,
            Usuario = UsuarioLogado ?? await _context.Usuarios.FirstAsync(u => u.Id == idUsuario.Value),
            Grupo = novoGrupo
        };

        _context.InfoUsuariosGrupos.Add(infoUsuarioGrupo);

        await _context.SaveChangesAsync();

        return new JsonResult(new
        {
            success = true,
            grupoId = novoGrupo.Id,
            redirectUrl = Url.Page("/Menu/Home", new { grupoId = novoGrupo.Id })
        });
    }

    public async Task<IActionResult> OnGetListarNotificacoesAsync()
    {
        var idUsuario = GetUsuarioLogadoId();
        if (idUsuario == null)
            return new JsonResult(new { success = false, message = "Usuário não autenticado." });

        var notificacoes = await _context.Notificacoes
            .AsNoTracking()
            .Where(n => n.UsuarioId == idUsuario.Value)
            .OrderByDescending(n => n.DataCriacao)
            .Take(30)
            .Select(n => new
            {
                id = n.Id,
                tipo = n.Tipo.ToString(),
                titulo = n.Titulo,
                mensagem = n.Mensagem,
                lida = n.Lida,
                dataCriacao = n.DataCriacao,
                linkDestino = n.LinkDestino
            })
            .ToListAsync();

        var naoLidas = notificacoes.Count(x => !x.lida);

        return new JsonResult(new
        {
            success = true,
            notificacoes,
            totalNaoLidas = naoLidas
        });
    }

    public async Task<IActionResult> OnPostMarcarNotificacoesComoLidasAsync()
    {
        var idUsuario = GetUsuarioLogadoId();
        if (idUsuario == null)
            return new JsonResult(new { success = false, message = "Usuário não autenticado." });

        var notificacoesNaoLidas = await _context.Notificacoes
            .Where(n => n.UsuarioId == idUsuario.Value && !n.Lida)
            .ToListAsync();

        foreach (var notificacao in notificacoesNaoLidas)
        {
            notificacao.Lida = true;
            notificacao.DataLeitura = DateTime.Now;
        }

        await _context.SaveChangesAsync();

        return new JsonResult(new { success = true });
    }

    private int? GetUsuarioLogadoId()
    {
        var claim = User.FindFirst("Id")?.Value;
        return int.TryParse(claim, out var id) ? id : null;
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