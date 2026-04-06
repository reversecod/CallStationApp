using CallStationApp.Authorization;
using CallStationApp.Data;
using CallStationApp.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace CallStationApp.Pages.Menu;

[Authorize]
public class HomeModel : PageModel
{
    private readonly AppDbContext _context;
    private readonly IWebHostEnvironment _environment;
    private readonly GrupoAuthorizationService _grupoAuthorizationService;

    public HomeModel(
        AppDbContext context,
        IWebHostEnvironment environment,
        GrupoAuthorizationService grupoAuthorizationService)
    {
        _context = context;
        _environment = environment;
        _grupoAuthorizationService = grupoAuthorizationService;
    }

    [BindProperty(SupportsGet = true)]
    public int GrupoId { get; set; }

    public Grupo? GrupoAtual { get; set; }
    public List<Chamado> Chamados { get; set; } = new();
    public List<Setor> SetoresDisponiveis { get; set; } = new();
    public List<OcorrenciaTipo> TiposOcorrenciaDisponiveis { get; set; } = new();
    public bool PodeCriarChamado { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var idUsuario = GetUsuarioLogadoId();
        if (idUsuario == null)
            return RedirectToPage("/Login");

        if (GrupoId <= 0)
            return RedirectToPage("/Menu/Menu");

        var contextoMembro = await _grupoAuthorizationService
            .ObterContextoMembroAsync(idUsuario.Value, GrupoId);

        if (contextoMembro == null)
            return RedirectToPage("/Menu/Menu");

        PodeCriarChamado = GrupoPermissionService.PodeCriarChamado(contextoMembro.Permissao);

        GrupoAtual = await _context.Grupos
            .AsNoTracking()
            .FirstOrDefaultAsync(g => g.Id == GrupoId);

        if (GrupoAtual == null)
            return RedirectToPage("/Menu/Menu");

        IQueryable<Chamado> queryChamados = _context.Chamados
            .AsNoTracking()
            .Where(c => c.GrupoId == GrupoId &&
                        c.Status != StatusChamado.Cancelado &&
                        c.Status != StatusChamado.Excluido);

        if (contextoMembro.Permissao == PermissaoUsuario.Colaborador)
        {
            queryChamados = queryChamados.Where(c =>
                c.Publico || c.CriadorChamadoId == idUsuario.Value);
        }
        else if (contextoMembro.Permissao == PermissaoUsuario.Nenhuma)
        {
            queryChamados = queryChamados.Where(c => c.Publico);
        }

        Chamados = await queryChamados
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

        var contextoMembro = await _grupoAuthorizationService
            .ObterContextoMembroAsync(idUsuario.Value, chamado.GrupoId);

        if (contextoMembro == null)
            return new JsonResult(new { success = false, message = "Você não tem acesso a este chamado." });

        if (!GrupoPermissionService.PodeVerChamado(
                contextoMembro.Permissao,
                chamado.Publico,
                idUsuario.Value,
                chamado.CriadorChamadoId))
        {
            return new JsonResult(new
            {
                success = false,
                message = "Você não tem permissão para visualizar este chamado."
            });
        }

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
            dataCriacao = chamado.DataCriacao,
            dataFinalizacao = chamado.DataFinalizacao,
            prazoResposta = chamado.PrazoResposta,
            prazoConclusao = chamado.PrazoConclusao,
            publico = chamado.Publico,
            permissoes = new
            {
                podeEditarTitulo = GrupoPermissionService.PodeEditarCampoChamado(contextoMembro.Permissao, ChamadoCampoEditavel.Titulo, idUsuario.Value, chamado.CriadorChamadoId),
                podeEditarDescricao = GrupoPermissionService.PodeEditarCampoChamado(contextoMembro.Permissao, ChamadoCampoEditavel.Descricao, idUsuario.Value, chamado.CriadorChamadoId),
                podeEditarSolucao = GrupoPermissionService.PodeEditarCampoChamado(contextoMembro.Permissao, ChamadoCampoEditavel.Solucao, idUsuario.Value, chamado.CriadorChamadoId),
                podeEditarSetorId = GrupoPermissionService.PodeEditarCampoChamado(contextoMembro.Permissao, ChamadoCampoEditavel.SetorId, idUsuario.Value, chamado.CriadorChamadoId),
                podeEditarOcorrenciaTipoId = GrupoPermissionService.PodeEditarCampoChamado(contextoMembro.Permissao, ChamadoCampoEditavel.OcorrenciaTipoId, idUsuario.Value, chamado.CriadorChamadoId),
                podeEditarOcorrenciaCategoriaId = GrupoPermissionService.PodeEditarCampoChamado(contextoMembro.Permissao, ChamadoCampoEditavel.OcorrenciaCategoriaId, idUsuario.Value, chamado.CriadorChamadoId),
                podeEditarOcorrenciaSubcategoriaId = GrupoPermissionService.PodeEditarCampoChamado(contextoMembro.Permissao, ChamadoCampoEditavel.OcorrenciaSubcategoriaId, idUsuario.Value, chamado.CriadorChamadoId),
                podeEditarAnexoChamado = GrupoPermissionService.PodeEditarCampoChamado(contextoMembro.Permissao, ChamadoCampoEditavel.AnexoChamado, idUsuario.Value, chamado.CriadorChamadoId),
                podeEditarPrioridade = GrupoPermissionService.PodeEditarCampoChamado(contextoMembro.Permissao, ChamadoCampoEditavel.Prioridade, idUsuario.Value, chamado.CriadorChamadoId),
                podeEditarCriticidade = GrupoPermissionService.PodeEditarCampoChamado(contextoMembro.Permissao, ChamadoCampoEditavel.Criticidade, idUsuario.Value, chamado.CriadorChamadoId),
                podeEditarUrgencia = GrupoPermissionService.PodeEditarCampoChamado(contextoMembro.Permissao, ChamadoCampoEditavel.Urgencia, idUsuario.Value, chamado.CriadorChamadoId),
                podeEditarStatus = GrupoPermissionService.PodeEditarCampoChamado(contextoMembro.Permissao, ChamadoCampoEditavel.Status, idUsuario.Value, chamado.CriadorChamadoId),
                podeEditarDataFinalizacao = GrupoPermissionService.PodeEditarCampoChamado(contextoMembro.Permissao, ChamadoCampoEditavel.DataFinalizacao, idUsuario.Value, chamado.CriadorChamadoId),
                podeEditarPrazoResposta = GrupoPermissionService.PodeEditarCampoChamado(contextoMembro.Permissao, ChamadoCampoEditavel.PrazoResposta, idUsuario.Value, chamado.CriadorChamadoId),
                podeEditarPrazoConclusao = GrupoPermissionService.PodeEditarCampoChamado(contextoMembro.Permissao, ChamadoCampoEditavel.PrazoConclusao, idUsuario.Value, chamado.CriadorChamadoId),
                podeEditarPublico = GrupoPermissionService.PodeEditarCampoChamado(contextoMembro.Permissao, ChamadoCampoEditavel.Publico, idUsuario.Value, chamado.CriadorChamadoId),
                podeExcluir = GrupoPermissionService.PodeExcluirChamado(contextoMembro.Permissao)
            }
        });
    }

    public async Task<IActionResult> OnPostNovoChamadoAsync(int grupoId)
    {
        var idUsuario = GetUsuarioLogadoId();
        if (idUsuario == null)
            return Unauthorized();

        if (grupoId <= 0)
            return BadRequest(new { success = false, message = "Grupo inválido." });

        var contextoMembro = await _grupoAuthorizationService
            .ObterContextoMembroAsync(idUsuario.Value, grupoId);

        if (contextoMembro == null)
            return BadRequest(new { success = false, message = "Usuário não pertence ao grupo." });

        if (!GrupoPermissionService.PodeCriarChamado(contextoMembro.Permissao))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                success = false,
                message = "Você não tem permissão para criar chamados neste grupo."
            });
        }

        var strategy = _context.Database.CreateExecutionStrategy();

        try
        {
            return await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _context.Database.BeginTransactionAsync();

                try
                {
                    var contadorGrupo = await _context.ChamadosContadorGrupo
                        .FirstOrDefaultAsync(x => x.GrupoId == grupoId);

                    if (contadorGrupo == null)
                    {
                        contadorGrupo = new ChamadoContadorGrupo
                        {
                            GrupoId = grupoId,
                            UltimoNumero = 0
                        };

                        _context.ChamadosContadorGrupo.Add(contadorGrupo);
                    }

                    contadorGrupo.UltimoNumero++;
                    var numeroGrupo = contadorGrupo.UltimoNumero;

                    var contadorUsuario = await _context.ChamadosContadorUsuario
                        .FirstOrDefaultAsync(x => x.UsuarioId == idUsuario.Value);

                    if (contadorUsuario == null)
                    {
                        contadorUsuario = new ChamadoContadorUsuario
                        {
                            UsuarioId = idUsuario.Value,
                            UltimoNumero = 0
                        };

                        _context.ChamadosContadorUsuario.Add(contadorUsuario);
                    }

                    contadorUsuario.UltimoNumero++;
                    var numeroUsuario = contadorUsuario.UltimoNumero;

                    var contadorUsuarioGrupo = await _context.ChamadosContadorUsuarioGrupo
                        .FirstOrDefaultAsync(x => x.UsuarioId == idUsuario.Value && x.GrupoId == grupoId);

                    if (contadorUsuarioGrupo == null)
                    {
                        contadorUsuarioGrupo = new ChamadoContadorUsuarioGrupo
                        {
                            UsuarioId = idUsuario.Value,
                            GrupoId = grupoId,
                            UltimoNumero = 0
                        };

                        _context.ChamadosContadorUsuarioGrupo.Add(contadorUsuarioGrupo);
                    }

                    contadorUsuarioGrupo.UltimoNumero++;
                    var numeroUsuarioGrupo = contadorUsuarioGrupo.UltimoNumero;

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

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    return (IActionResult)new JsonResult(new
                    {
                        success = true,
                        id = chamado.Id,
                        numeroGrupo = chamado.NumeroChamadoGrupo,
                        criadoEm = chamado.DataCriacao
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
            return BadRequest(new
            {
                success = false,
                message = ex.InnerException?.Message ?? ex.Message
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new
            {
                success = false,
                message = ex.InnerException?.Message ?? ex.Message
            });
        }
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

        var contextoMembro = await _grupoAuthorizationService
            .ObterContextoMembroAsync(idUsuario.Value, chamado.GrupoId);

        if (contextoMembro == null)
            return new JsonResult(new { success = false, message = "Você não tem acesso a este chamado." });

        if (!GrupoPermissionService.PodeVerChamado(
                contextoMembro.Permissao,
                chamado.Publico,
                idUsuario.Value,
                chamado.CriadorChamadoId))
        {
            return new JsonResult(new { success = false, message = "Você não tem permissão para visualizar este chamado." });
        }

        if (!GrupoPermissionService.PodeExcluirChamado(contextoMembro.Permissao))
        {
            return new JsonResult(new { success = false, message = "Você não tem permissão para excluir este chamado." });
        }

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

        var contextoMembro = await _grupoAuthorizationService
            .ObterContextoMembroAsync(idUsuario.Value, chamado.GrupoId);

        if (contextoMembro == null)
            return new JsonResult(new { success = false, message = "Você não tem acesso a este chamado." });

        if (!GrupoPermissionService.PodeVerChamado(
                contextoMembro.Permissao,
                chamado.Publico,
                idUsuario.Value,
                chamado.CriadorChamadoId))
        {
            return new JsonResult(new { success = false, message = "Você não tem permissão para visualizar este chamado." });
        }

        var houveAlteracao = false;

        if (GrupoPermissionService.PodeEditarCampoChamado(contextoMembro.Permissao, ChamadoCampoEditavel.Titulo, idUsuario.Value, chamado.CriadorChamadoId)
            && chamado.Titulo != request.Titulo)
        {
            chamado.Titulo = request.Titulo;
            houveAlteracao = true;
        }

        if (GrupoPermissionService.PodeEditarCampoChamado(contextoMembro.Permissao, ChamadoCampoEditavel.Descricao, idUsuario.Value, chamado.CriadorChamadoId)
            && chamado.Descricao != request.Descricao)
        {
            chamado.Descricao = request.Descricao;
            houveAlteracao = true;
        }

        if (GrupoPermissionService.PodeEditarCampoChamado(contextoMembro.Permissao, ChamadoCampoEditavel.Solucao, idUsuario.Value, chamado.CriadorChamadoId)
            && chamado.Solucao != request.Solucao)
        {
            chamado.Solucao = request.Solucao;
            houveAlteracao = true;
        }

        if (GrupoPermissionService.PodeEditarCampoChamado(contextoMembro.Permissao, ChamadoCampoEditavel.SetorId, idUsuario.Value, chamado.CriadorChamadoId)
            && chamado.SetorId != request.SetorId)
        {
            chamado.SetorId = request.SetorId;
            houveAlteracao = true;
        }

        if (GrupoPermissionService.PodeEditarCampoChamado(contextoMembro.Permissao, ChamadoCampoEditavel.OcorrenciaTipoId, idUsuario.Value, chamado.CriadorChamadoId)
            && chamado.OcorrenciaTipoId != request.OcorrenciaTipoId)
        {
            chamado.OcorrenciaTipoId = request.OcorrenciaTipoId;
            houveAlteracao = true;
        }

        if (GrupoPermissionService.PodeEditarCampoChamado(contextoMembro.Permissao, ChamadoCampoEditavel.OcorrenciaCategoriaId, idUsuario.Value, chamado.CriadorChamadoId)
            && chamado.OcorrenciaCategoriaId != request.OcorrenciaCategoriaId)
        {
            chamado.OcorrenciaCategoriaId = request.OcorrenciaCategoriaId;
            houveAlteracao = true;
        }

        if (GrupoPermissionService.PodeEditarCampoChamado(contextoMembro.Permissao, ChamadoCampoEditavel.OcorrenciaSubcategoriaId, idUsuario.Value, chamado.CriadorChamadoId)
            && chamado.OcorrenciaSubcategoriaId != request.OcorrenciaSubcategoriaId)
        {
            chamado.OcorrenciaSubcategoriaId = request.OcorrenciaSubcategoriaId;
            houveAlteracao = true;
        }

        var novaPrioridade = ParseNullableEnum<PrioridadeChamado>(request.Prioridade);
        if (GrupoPermissionService.PodeEditarCampoChamado(contextoMembro.Permissao, ChamadoCampoEditavel.Prioridade, idUsuario.Value, chamado.CriadorChamadoId)
            && chamado.Prioridade != novaPrioridade)
        {
            chamado.Prioridade = novaPrioridade;
            houveAlteracao = true;
        }

        var novaCriticidade = ParseNullableEnum<CriticidadeChamado>(request.Criticidade);
        if (GrupoPermissionService.PodeEditarCampoChamado(contextoMembro.Permissao, ChamadoCampoEditavel.Criticidade, idUsuario.Value, chamado.CriadorChamadoId)
            && chamado.Criticidade != novaCriticidade)
        {
            chamado.Criticidade = novaCriticidade;
            houveAlteracao = true;
        }

        var novaUrgencia = ParseNullableEnum<UrgenciaChamado>(request.Urgencia);
        if (GrupoPermissionService.PodeEditarCampoChamado(contextoMembro.Permissao, ChamadoCampoEditavel.Urgencia, idUsuario.Value, chamado.CriadorChamadoId)
            && chamado.Urgencia != novaUrgencia)
        {
            chamado.Urgencia = novaUrgencia;
            houveAlteracao = true;
        }

        if (GrupoPermissionService.PodeEditarCampoChamado(contextoMembro.Permissao, ChamadoCampoEditavel.Status, idUsuario.Value, chamado.CriadorChamadoId)
            && !string.IsNullOrWhiteSpace(request.Status)
            && Enum.TryParse<StatusChamado>(request.Status, out var novoStatus)
            && chamado.Status != novoStatus)
        {
            chamado.Status = novoStatus;
            houveAlteracao = true;
        }

        if (GrupoPermissionService.PodeEditarCampoChamado(contextoMembro.Permissao, ChamadoCampoEditavel.DataFinalizacao, idUsuario.Value, chamado.CriadorChamadoId)
            && chamado.DataFinalizacao != request.DataFinalizacao)
        {
            chamado.DataFinalizacao = request.DataFinalizacao;
            houveAlteracao = true;
        }

        if (GrupoPermissionService.PodeEditarCampoChamado(contextoMembro.Permissao, ChamadoCampoEditavel.PrazoResposta, idUsuario.Value, chamado.CriadorChamadoId)
            && chamado.PrazoResposta != request.PrazoResposta)
        {
            chamado.PrazoResposta = request.PrazoResposta;
            houveAlteracao = true;
        }

        if (GrupoPermissionService.PodeEditarCampoChamado(contextoMembro.Permissao, ChamadoCampoEditavel.PrazoConclusao, idUsuario.Value, chamado.CriadorChamadoId)
            && chamado.PrazoConclusao != request.PrazoConclusao)
        {
            chamado.PrazoConclusao = request.PrazoConclusao;
            houveAlteracao = true;
        }

        if (GrupoPermissionService.PodeEditarCampoChamado(contextoMembro.Permissao, ChamadoCampoEditavel.Publico, idUsuario.Value, chamado.CriadorChamadoId)
            && chamado.Publico != request.Publico)
        {
            chamado.Publico = request.Publico;
            houveAlteracao = true;
        }

        if (GrupoPermissionService.PodeEditarCampoChamado(contextoMembro.Permissao, ChamadoCampoEditavel.AnexoChamado, idUsuario.Value, chamado.CriadorChamadoId)
            && request.AnexoArquivo != null
            && request.AnexoArquivo.Length > 0)
        {
            var uploadsRoot = Path.Combine(_environment.WebRootPath, "uploads", "chamados");
            Directory.CreateDirectory(uploadsRoot);

            var extensao = Path.GetExtension(request.AnexoArquivo.FileName);
            var nomeArquivo = $"{Guid.NewGuid()}{extensao}";
            var caminhoFisico = Path.Combine(uploadsRoot, nomeArquivo);

            await using var stream = new FileStream(caminhoFisico, FileMode.Create);
            await request.AnexoArquivo.CopyToAsync(stream);

            chamado.AnexoChamado = $"/uploads/chamados/{nomeArquivo}";
            houveAlteracao = true;
        }

        if (!houveAlteracao)
        {
            return new JsonResult(new
            {
                success = false,
                message = "Nenhuma alteração permitida foi identificada."
            });
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
        public DateTime? DataFinalizacao { get; set; }
        public DateTime? PrazoResposta { get; set; }
        public DateTime? PrazoConclusao { get; set; }
        public bool Publico { get; set; }
        public IFormFile? AnexoArquivo { get; set; }
    }
}