using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using CallStationApp.Data;
using CallStationApp.Models;
using Microsoft.EntityFrameworkCore;

namespace CallStationApp.Pages;

[Authorize]
public class HomeModel : PageModel
{
    private readonly AppDbContext _context;

    public HomeModel(AppDbContext context)
    {
        _context = context;
    }

    // =========================
    // CONTEXTO ATIVO
    // =========================
    [BindProperty(SupportsGet = true)]
    public int GrupoId { get; set; }

    // =========================
    // DADOS DA TELA
    // =========================
    public List<Chamado> Chamados { get; set; } = new();
    public List<Setor> SetoresDisponiveis { get; set; } = new();
    public List<OcorrenciaTipo> TiposOcorrenciaDisponiveis { get; set; } = new();

    // =========================
    // GET
    // =========================
    public async Task<IActionResult> OnGetAsync()
    {
        var idUsuario = GetUsuarioLogadoId();
        if (idUsuario == null)
            return Unauthorized();

        // 🔒 Valida se o usuário pertence ao grupo
        var pertenceAoGrupo = await _context.UsuariosGrupos
            .AnyAsync(ug => ug.UsuarioId == idUsuario && ug.GrupoId == GrupoId);

        if (!pertenceAoGrupo)
            return Forbid();

        // ===== CHAMADOS DO GRUPO =====
        Chamados = await _context.Chamados
            .Where(c => c.GrupoId == GrupoId && c.Status != StatusChamado.Cancelado)
            .OrderBy(c => c.DataCriacao)
            .ToListAsync();

        // ===== LISTAS DO GRUPO =====
        SetoresDisponiveis = await _context.Setores
            .Where(s => s.GrupoId == GrupoId)
            .OrderBy(s => s.NomeSetor)
            .ToListAsync();

        TiposOcorrenciaDisponiveis = await _context.OcorrenciasTipo
            .Where(t => t.GrupoId == GrupoId)
            .OrderBy(t => t.TipoOcorrencia)
            .ToListAsync();

        return Page();
    }

    // =========================
    // NOVO CHAMADO
    // =========================
    public async Task<IActionResult> OnPostNovoChamadoAsync()
    {
        var idUsuario = GetUsuarioLogadoId();
        if (idUsuario == null)
            return Unauthorized();

        // 🔒 valida vínculo
        var pertenceAoGrupo = await _context.UsuariosGrupos
            .AnyAsync(ug => ug.UsuarioId == idUsuario && ug.GrupoId == GrupoId);

        if (!pertenceAoGrupo)
            return Forbid();

        // ===== NUMERAÇÃO =====
        var numeroGrupo = await _context.Chamados
            .Where(c => c.GrupoId == GrupoId)
            .CountAsync() + 1;

        var numeroUsuario = await _context.Chamados
            .Where(c => c.CriadorChamadoId == idUsuario)
            .CountAsync() + 1;

        var numeroUsuarioGrupo = await _context.Chamados
            .Where(c => c.CriadorChamadoId == idUsuario && c.GrupoId == GrupoId)
            .CountAsync() + 1;

        var chamado = new Chamado
        {
            Titulo = "Novo chamado",
            GrupoId = GrupoId,
            CriadorChamadoId = idUsuario.Value,

            NumeroChamadoGrupo = numeroGrupo,
            NumeroChamadoUsuario = numeroUsuario,
            NumeroChamadoUsuarioGrupo = numeroUsuarioGrupo,

            Status = StatusChamado.Aberto,
            DataCriacao = DateTime.Now
        };

        _context.Chamados.Add(chamado);
        await _context.SaveChangesAsync();

        return new JsonResult(new
        {
            success = true,
            id = chamado.Id,
            numeroGrupo = chamado.NumeroChamadoGrupo,
            criadoEm = chamado.DataCriacao
        });
    }

    // =========================
    // HELPERS
    // =========================
    private int? GetUsuarioLogadoId()
    {
        var claim = User.FindFirst("Id")?.Value;
        return int.TryParse(claim, out var id) ? id : null;
    }
}