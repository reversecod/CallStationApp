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
    private readonly IWebHostEnvironment _environment;

    public HomeModel(AppDbContext context, IWebHostEnvironment environment)
    {
        _context = context;
        _environment = environment;
    }

    [BindProperty(SupportsGet = true)]
    public int GrupoId { get; set; }

    public Grupo? GrupoAtual { get; set; }
    public List<Chamado> Chamados { get; set; } = new();
    public List<Setor> SetoresDisponiveis { get; set; } = new();
    public List<OcorrenciaTipo> TiposOcorrenciaDisponiveis { get; set; } = new();

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

        Chamados = await _context.Chamados
            .AsNoTracking()
            .Where(c => c.GrupoId == GrupoId &&
                        c.Status != StatusChamado.Cancelado &&
                        c.Status != StatusChamado.Excluido)
            .OrderBy(c => c.DataCriacao)
            .ToListAsync();

        SetoresDisponiveis = await _context.Setores
            .AsNoTracking()
            .Where(s => s.GrupoId == GrupoId)
            .OrderBy(s => s.NomeSetor)
            .ToListAsync();

        TiposOcorrenciaDisponiveis = await _context.OcorrenciasTipo
            .AsNoTracking()
            .Where(t => t.GrupoId == GrupoId)
            .OrderBy(t => t.TipoOcorrencia)
            .ToListAsync();

        return Page();
    }

    public async Task<IActionResult> OnPostNovoChamadoAsync(int grupoId)
    {
        var idUsuario = GetUsuarioLogadoId();
        if (idUsuario == null)
            return Unauthorized();

        if (grupoId <= 0)
            return BadRequest(new { success = false, message = "Grupo inválido." });

        var pertenceAoGrupo = await _context.UsuariosGrupos
            .AnyAsync(ug => ug.UsuarioId == idUsuario.Value && ug.GrupoId == grupoId);

        if (!pertenceAoGrupo)
            return BadRequest(new { success = false, message = "Usuário não pertence ao grupo." });

        var numeroGrupo = await _context.Chamados
            .Where(c => c.GrupoId == grupoId)
            .CountAsync() + 1;

        var numeroUsuario = await _context.Chamados
            .Where(c => c.CriadorChamadoId == idUsuario.Value)
            .CountAsync() + 1;

        var numeroUsuarioGrupo = await _context.Chamados
            .Where(c => c.CriadorChamadoId == idUsuario.Value && c.GrupoId == grupoId)
            .CountAsync() + 1;

        var chamado = new Chamado
        {
            Titulo = "Novo chamado",
            GrupoId = grupoId,
            CriadorChamadoId = idUsuario.Value,
            NumeroChamadoGrupo = numeroGrupo,
            NumeroChamadoUsuario = numeroUsuario,
            NumeroChamadoUsuarioGrupo = numeroUsuarioGrupo,
            Status = StatusChamado.Aberto,
            DataCriacao = DateTime.Now,
            Publico = false
        };

        _context.Chamados.Add(chamado);

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            return BadRequest(new
            {
                success = false,
                message = ex.InnerException?.Message ?? ex.Message
            });
        }

        return new JsonResult(new
        {
            success = true,
            id = chamado.Id,
            numeroGrupo = chamado.NumeroChamadoGrupo,
            criadoEm = chamado.DataCriacao
        });
    }

    public async Task<IActionResult> OnGetCarregarChamadoAsync(int id)
    {
        var idUsuario = GetUsuarioLogadoId();
        if (idUsuario == null)
            return new JsonResult(new { success = false, message = "Usuário não autenticado." });

        var chamado = await _context.Chamados
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id);

        if (chamado == null)
            return new JsonResult(new { success = false, message = "Chamado não encontrado." });

        var pertenceAoGrupo = await _context.UsuariosGrupos
            .AnyAsync(ug => ug.UsuarioId == idUsuario.Value && ug.GrupoId == chamado.GrupoId);

        if (!pertenceAoGrupo)
            return new JsonResult(new { success = false, message = "Você não tem acesso a este chamado." });

        return new JsonResult(new
        {
            success = true,
            id = chamado.Id,
            titulo = chamado.Titulo,
            descricao = chamado.Descricao,
            solucao = chamado.Solucao,
            grupoId = chamado.GrupoId,
            setorId = chamado.SetorId,
            ocorrenciaTipoId = chamado.OcorrenciaTipoId,
            ocorrenciaCategoriaId = chamado.OcorrenciaCategoriaId,
            ocorrenciaSubcategoriaId = chamado.OcorrenciaSubcategoriaId,
            anexoChamado = chamado.AnexoChamado,
            prioridade = chamado.Prioridade?.ToString(),
            criticidade = chamado.Criticidade?.ToString(),
            urgencia = chamado.Urgencia?.ToString(),
            status = chamado.Status.ToString(),
            dataInicioAtendimento = chamado.DataInicioAtendimento,
            dataCriacao = chamado.DataCriacao,
            dataFinalizacao = chamado.DataFinalizacao,
            prazoResposta = chamado.PrazoResposta,
            prazoConclusao = chamado.PrazoConclusao,
            publico = chamado.Publico
        });
    }

    public async Task<IActionResult> OnPostExcluirChamadoAsync([FromBody] ExcluirChamadoRequest request)
    {
        var idUsuario = GetUsuarioLogadoId();
        if (idUsuario == null)
            return new JsonResult(new { success = false, message = "Usuário não autenticado." });

        if (request == null || request.Id <= 0)
            return new JsonResult(new { success = false, message = "Chamado inválido." });

        var chamado = await _context.Chamados.FirstOrDefaultAsync(c => c.Id == request.Id);
        if (chamado == null)
            return new JsonResult(new { success = false, message = "Chamado não encontrado." });

        var pertenceAoGrupo = await _context.UsuariosGrupos
            .AnyAsync(ug => ug.UsuarioId == idUsuario.Value && ug.GrupoId == chamado.GrupoId);

        if (!pertenceAoGrupo)
            return new JsonResult(new { success = false, message = "Você não tem acesso a este chamado." });

        chamado.Status = StatusChamado.Cancelado;

        try
        {
            await _context.SaveChangesAsync();
            return new JsonResult(new { success = true, message = "Chamado cancelado com sucesso." });
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

    public async Task<IActionResult> OnPostSalvarChamadoAsync([FromForm] EditarChamadoRequest request)
    {
        var idUsuario = GetUsuarioLogadoId();
        if (idUsuario == null)
            return new JsonResult(new { success = false, message = "Usuário não autenticado." });

        if (request == null || request.Id <= 0)
            return new JsonResult(new { success = false, message = "Chamado inválido." });

        var chamado = await _context.Chamados.FirstOrDefaultAsync(c => c.Id == request.Id);
        if (chamado == null)
            return new JsonResult(new { success = false, message = "Chamado não encontrado." });

        var pertenceAoGrupo = await _context.UsuariosGrupos
            .AnyAsync(ug => ug.UsuarioId == idUsuario.Value && ug.GrupoId == chamado.GrupoId);

        if (!pertenceAoGrupo)
            return new JsonResult(new { success = false, message = "Você não tem acesso a este chamado." });

        chamado.Titulo = request.Titulo;
        chamado.Descricao = request.Descricao;
        chamado.Solucao = request.Solucao;
        chamado.SetorId = request.SetorId;
        chamado.OcorrenciaTipoId = request.OcorrenciaTipoId;
        chamado.OcorrenciaCategoriaId = request.OcorrenciaCategoriaId;
        chamado.OcorrenciaSubcategoriaId = request.OcorrenciaSubcategoriaId;
        chamado.Prioridade = ParseNullableEnum<PrioridadeChamado>(request.Prioridade);
        chamado.Criticidade = ParseNullableEnum<CriticidadeChamado>(request.Criticidade);
        chamado.Urgencia = ParseNullableEnum<UrgenciaChamado>(request.Urgencia);

        if (!string.IsNullOrWhiteSpace(request.Status) &&
            Enum.TryParse<StatusChamado>(request.Status, out var status))
        {
            chamado.Status = status;
        }

        chamado.DataInicioAtendimento = request.DataInicioAtendimento;
        chamado.DataCriacao = request.DataCriacao ?? chamado.DataCriacao;
        chamado.DataFinalizacao = request.DataFinalizacao;
        chamado.PrazoResposta = request.PrazoResposta;
        chamado.PrazoConclusao = request.PrazoConclusao;
        chamado.Publico = request.Publico;

        if (request.AnexoArquivo != null && request.AnexoArquivo.Length > 0)
        {
            var uploadsRoot = Path.Combine(_environment.WebRootPath, "uploads", "chamados");
            Directory.CreateDirectory(uploadsRoot);

            var extensao = Path.GetExtension(request.AnexoArquivo.FileName);
            var nomeArquivo = $"{Guid.NewGuid()}{extensao}";
            var caminhoFisico = Path.Combine(uploadsRoot, nomeArquivo);

            await using var stream = new FileStream(caminhoFisico, FileMode.Create);
            await request.AnexoArquivo.CopyToAsync(stream);

            chamado.AnexoChamado = $"/uploads/chamados/{nomeArquivo}";
        }

        try
        {
            await _context.SaveChangesAsync();
            return new JsonResult(new { success = true, message = "Chamado salvo com sucesso." });
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

    private int? GetUsuarioLogadoId()
    {
        var claim = User.FindFirst("Id")?.Value;
        return int.TryParse(claim, out var id) ? id : null;
    }

    private static TEnum? ParseNullableEnum<TEnum>(string? valor) where TEnum : struct
    {
        if (string.IsNullOrWhiteSpace(valor))
            return null;

        return Enum.TryParse<TEnum>(valor, out var resultado) ? resultado : null;
    }

    public class ExcluirChamadoRequest
    {
        public int Id { get; set; }
    }

    public class EditarChamadoRequest
    {
        public int Id { get; set; }
        public string? Titulo { get; set; }
        public string? Descricao { get; set; }
        public string? Solucao { get; set; }
        public int? GrupoId { get; set; }
        public int? SetorId { get; set; }
        public int? OcorrenciaTipoId { get; set; }
        public int? OcorrenciaCategoriaId { get; set; }
        public int? OcorrenciaSubcategoriaId { get; set; }
        public string? Prioridade { get; set; }
        public string? Criticidade { get; set; }
        public string? Urgencia { get; set; }
        public string? Status { get; set; }
        public DateTime? DataInicioAtendimento { get; set; }
        public DateTime? DataCriacao { get; set; }
        public DateTime? DataFinalizacao { get; set; }
        public DateTime? PrazoResposta { get; set; }
        public DateTime? PrazoConclusao { get; set; }
        public bool Publico { get; set; }
        public IFormFile? AnexoArquivo { get; set; }
    }
}