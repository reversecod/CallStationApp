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
    private static readonly StatusChamado[] StatusFinais =
    {
        StatusChamado.Concluido,
        StatusChamado.Fechado,
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

        if (contextoMembro.Permissao == PermissaoUsuario.Colaborador)
            query = query.Where(c => c.Publico || c.CriadorChamadoId == usuarioId.Value);
        else if (contextoMembro.Permissao == PermissaoUsuario.Nenhuma)
            query = query.Where(c => c.Publico);

        Chamados = await query
            .OrderByDescending(c =>
                _context.HistoricoStatusChamados
                    .Where(h => h.ChamadoId == c.Id &&
                                (h.StatusNovo == StatusNovoChamado.Concluido ||
                                 h.StatusNovo == StatusNovoChamado.Fechado ||
                                 h.StatusNovo == StatusNovoChamado.Cancelado))
                    .Select(h => (DateTime?)h.DataTransicao)
                    .OrderByDescending(x => x)
                    .FirstOrDefault() ?? c.DataFinalizacao ?? c.DataCriacao)
            .Skip((Math.Max(PaginaAtual, 1) - 1) * TamanhoPagina)
            .Take(TamanhoPagina + 1)
            .Select(c => new HistoricoChamadoVm
            {
                Id = c.Id,
                NumeroChamadoGrupo = c.NumeroChamadoGrupo,
                Titulo = c.Titulo,
                Status = c.Status,
                DataCriacao = c.DataCriacao,
                DataFinal = _context.HistoricoStatusChamados
                    .Where(h => h.ChamadoId == c.Id &&
                                (h.StatusNovo == StatusNovoChamado.Concluido ||
                                 h.StatusNovo == StatusNovoChamado.Fechado ||
                                 h.StatusNovo == StatusNovoChamado.Cancelado))
                    .OrderByDescending(h => h.DataTransicao)
                    .Select(h => (DateTime?)h.DataTransicao)
                    .FirstOrDefault() ?? c.DataFinalizacao,
                CriadoPor = (
                    from usuarioGrupo in _context.UsuariosGrupos.AsNoTracking()
                    join usuarioCriador in _context.Usuarios.AsNoTracking() on usuarioGrupo.UsuarioId equals usuarioCriador.Id
                    join info in _context.InfoUsuariosGrupos.AsNoTracking()
                        on new { usuarioGrupo.UsuarioId, usuarioGrupo.GrupoId } equals new { info.UsuarioId, info.GrupoId } into infoJoin
                    from infoUsuario in infoJoin.DefaultIfEmpty()
                    where usuarioGrupo.UsuarioId == c.CriadorChamadoId && usuarioGrupo.GrupoId == c.GrupoId
                    select infoUsuario != null && !string.IsNullOrWhiteSpace(infoUsuario.Apelido)
                        ? infoUsuario.Apelido!
                        : usuarioCriador.NomeUsuario
                ).FirstOrDefault() ?? "Nao registrado",
                FinalizadoPor = (
                    from historico in _context.HistoricoStatusChamados.AsNoTracking()
                    join usuarioFinal in _context.Usuarios.AsNoTracking() on historico.UsuarioId equals usuarioFinal.Id into usuarioJoin
                    from usuarioFinal in usuarioJoin.DefaultIfEmpty()
                    where historico.ChamadoId == c.Id &&
                          (historico.StatusNovo == StatusNovoChamado.Concluido ||
                           historico.StatusNovo == StatusNovoChamado.Fechado ||
                           historico.StatusNovo == StatusNovoChamado.Cancelado)
                    orderby historico.DataTransicao descending
                    select historico.OrigemAutomatica
                        ? "Sistema"
                        : (usuarioFinal != null ? usuarioFinal.NomeUsuario : "Nao registrado")
                ).FirstOrDefault() ?? "Nao registrado"
            })
            .ToListAsync();

        TemProximaPagina = Chamados.Count > TamanhoPagina;
        if (TemProximaPagina)
            Chamados.RemoveAt(Chamados.Count - 1);

        return Page();
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
    }
}
