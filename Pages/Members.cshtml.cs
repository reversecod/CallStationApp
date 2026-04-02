using CallStationApp.Data;
using CallStationApp.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace CallStationApp.Pages;

[Authorize]
public class MembersModel : PageModel
{
    private readonly AppDbContext _context;

    public MembersModel(AppDbContext context)
    {
        _context = context;
    }

    [BindProperty(SupportsGet = true)]
    public int GrupoId { get; set; }

    public Grupo? GrupoAtual { get; set; }
    public string? NomeUsuarioLogado { get; set; }

    public List<MembroGrupoViewModel> Membros { get; set; } = new();

    public async Task<IActionResult> OnGetAsync()
    {
        var idUsuario = GetUsuarioLogadoId();
        if (idUsuario == null)
            return RedirectToPage("/Login");

        if (GrupoId <= 0)
            return RedirectToPage("/Menu");

        var pertenceAoGrupo = await _context.UsuariosGrupos
            .AnyAsync(ug => ug.UsuarioId == idUsuario.Value && ug.GrupoId == GrupoId);

        if (!pertenceAoGrupo)
            return RedirectToPage("/Menu");

        GrupoAtual = await _context.Grupos
            .AsNoTracking()
            .FirstOrDefaultAsync(g => g.Id == GrupoId);

        if (GrupoAtual == null)
            return RedirectToPage("/Menu");

        NomeUsuarioLogado = await _context.Usuarios
            .AsNoTracking()
            .Where(u => u.Id == idUsuario.Value)
            .Select(u => u.NomeUsuario)
            .FirstOrDefaultAsync();

        Membros = await (
            from ug in _context.UsuariosGrupos.AsNoTracking()
            join u in _context.Usuarios.AsNoTracking()
                on ug.UsuarioId equals u.Id
            join iug in _context.InfoUsuariosGrupos.AsNoTracking()
                on new { ug.UsuarioId, ug.GrupoId } equals new { iug.UsuarioId, iug.GrupoId } into infoJoin
            from info in infoJoin.DefaultIfEmpty()
            where ug.GrupoId == GrupoId
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
            }
        ).ToListAsync();

        return Page();
    }

    private int? GetUsuarioLogadoId()
    {
        var claim = User.FindFirst("Id")?.Value;
        return int.TryParse(claim, out var id) ? id : null;
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
}