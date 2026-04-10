using CallStationApp.Authorization;
using CallStationApp.Data;
using CallStationApp.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace CallStationApp.Pages.Menu;

[Authorize]
public class HomeModel : PageModel
{
    private readonly AppDbContext _context;
    private readonly IWebHostEnvironment _environment;
    private readonly GrupoAuthorizationService _grupoAuthorizationService;
    private readonly IMemoryCache _memoryCache;
    private const int TamanhoPaginaChamados = 20;
    private static readonly TimeSpan CacheCatalogoTtl = TimeSpan.FromMinutes(10);

    public HomeModel(
        AppDbContext context,
        IWebHostEnvironment environment,
        GrupoAuthorizationService grupoAuthorizationService,
        IMemoryCache memoryCache)
    {
        _context = context;
        _environment = environment;
        _grupoAuthorizationService = grupoAuthorizationService;
        _memoryCache = memoryCache;
    }

    [BindProperty(SupportsGet = true)]
    public int GrupoId { get; set; }

    [BindProperty(SupportsGet = true, Name = "page")]
    public int PaginaAtual { get; set; } = 1;

    public string? NomeUsuarioLogado { get; set; }
    public string? FotoUsuarioLogado { get; set; }
    public Grupo? GrupoAtual { get; set; }
    public List<ChamadoListItemViewModel> Chamados { get; set; } = new();
    public List<Setor> SetoresDisponiveis { get; set; } = new();
    public List<OcorrenciaTipo> TiposOcorrenciaDisponiveis { get; set; } = new();
    public bool PodeCriarChamado { get; set; }
    public bool TemPaginaAnterior => PaginaAtual > 1;
    public bool TemProximaPagina { get; set; }

    public class ChamadoListItemViewModel
    {
        public int Id { get; set; }
        public int NumeroChamadoGrupo { get; set; }
        public string? Titulo { get; set; }
        public StatusChamado Status { get; set; }
        public DateTime DataCriacao { get; set; }
    }

    public async Task<IActionResult> OnGetAsync()
    {
        var idUsuario = GetUsuarioLogadoId();
        if (idUsuario == null)
            return RedirectToPage("/Auth/Login");

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

        var usuarioLogado = await _context.Usuarios
            .AsNoTracking()
            .Where(u => u.Id == idUsuario.Value)
            .Select(u => new
            {
                u.NomeUsuario,
                u.FotoUsuario
            })
            .FirstOrDefaultAsync();

        NomeUsuarioLogado = usuarioLogado?.NomeUsuario;
        FotoUsuarioLogado = usuarioLogado?.FotoUsuario;

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
            .OrderByDescending(c => c.DataCriacao)
            .Skip((Math.Max(PaginaAtual, 1) - 1) * TamanhoPaginaChamados)
            .Take(TamanhoPaginaChamados + 1)
            .Select(c => new ChamadoListItemViewModel
            {
                Id = c.Id,
                NumeroChamadoGrupo = c.NumeroChamadoGrupo,
                Titulo = c.Titulo,
                Status = c.Status,
                DataCriacao = c.DataCriacao
            })
            .ToListAsync();

        TemProximaPagina = Chamados.Count > TamanhoPaginaChamados;
        if (TemProximaPagina)
        {
            Chamados.RemoveAt(Chamados.Count - 1);
        }

        SetoresDisponiveis = await ObterSetoresDisponiveisAsync(GrupoId);
        TiposOcorrenciaDisponiveis = await ObterTiposOcorrenciaDisponiveisAsync(GrupoId);

        return Page();
    }

    public async Task<IActionResult> OnGetCarregarChamadoAsync(int id)
    {
        var idUsuario = GetUsuarioLogadoId();
        if (idUsuario == null)
            return new JsonResult(new { success = false, message = "Usuário não autenticado." });

         var chamado = await _context.Chamados
        .AsNoTracking()
        .FirstOrDefaultAsync(c => c.Id == id && c.GrupoId == GrupoId);

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

        string? criadorNomeUsuario = null;
        string? criadorPermissao = null;

        if (contextoMembro.Permissao != PermissaoUsuario.Nenhuma)
        {
            var dadosCriador = await (
                from usuario in _context.Usuarios.AsNoTracking()
                join usuarioGrupo in _context.UsuariosGrupos.AsNoTracking()
                    on new { UsuarioId = usuario.Id, GrupoId = chamado.GrupoId }
                    equals new { usuarioGrupo.UsuarioId, usuarioGrupo.GrupoId }
                join infoUsuarioGrupo in _context.InfoUsuariosGrupos.AsNoTracking()
                    on new { UsuarioId = usuario.Id, GrupoId = chamado.GrupoId }
                    equals new { infoUsuarioGrupo.UsuarioId, infoUsuarioGrupo.GrupoId } into infoJoin
                from info in infoJoin.DefaultIfEmpty()
                where usuario.Id == chamado.CriadorChamadoId
                select new
                {
                    NomeExibicao = info != null && !string.IsNullOrWhiteSpace(info.Apelido)
                        ? info.Apelido
                        : usuario.NomeUsuario,
                    Permissao = usuarioGrupo.Permissao
                }
            ).FirstOrDefaultAsync();

            criadorNomeUsuario = dadosCriador?.NomeExibicao;
            criadorPermissao = dadosCriador?.Permissao.ToString();
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
            criadorNomeUsuario,
            criadorPermissao,
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
                podeExcluir = GrupoPermissionService.PodeExcluirChamado(contextoMembro.Permissao, idUsuario.Value, chamado.CriadorChamadoId)
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

        const int maxTentativas = 3;

        for (var tentativa = 1; tentativa <= maxTentativas; tentativa++)
        {
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
                            DataCriacao = DateTime.UtcNow,
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
            catch (DbUpdateException ex) when (tentativa < maxTentativas)
            {
                _context.ChangeTracker.Clear();

                if (!EhErroDuplicidade(ex))
                    return BadRequest(new { success = false, message = ex.InnerException?.Message ?? ex.Message });
            }
            catch (DbUpdateException ex)
            {
                return BadRequest(new { success = false, message = ex.InnerException?.Message ?? ex.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.InnerException?.Message ?? ex.Message });
            }
        }

        return BadRequest(new { success = false, message = "Nao foi possivel criar o chamado no momento." });
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

        if (!GrupoPermissionService.PodeExcluirChamado(
        contextoMembro.Permissao,
        idUsuario.Value,
        chamado.CriadorChamadoId))
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

        if (request.AnexoArquivo != null && request.AnexoArquivo.Length > 0)
        {
            var contentTypesPermitidos = new[]
            {
                "image/jpeg",
                "image/png",
                "application/pdf"
            };

            if (!contentTypesPermitidos.Contains(request.AnexoArquivo.ContentType))
            {
                return new JsonResult(new { success = false, message = "Conteúdo do arquivo não permitido." });
            }

            var extensoesPermitidas = new[] { ".jpg", ".jpeg", ".png", ".pdf" };
            var extensao = Path.GetExtension(request.AnexoArquivo.FileName).ToLowerInvariant();

            if (!extensoesPermitidas.Contains(extensao))
                return new JsonResult(new { success = false, message = "Tipo de arquivo não permitido." });

            if (request.AnexoArquivo.Length > 5 * 1024 * 1024)
                return new JsonResult(new { success = false, message = "Arquivo excede o limite de 5 MB." });
        }

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

        if (request.GrupoId.HasValue && request.GrupoId.Value != chamado.GrupoId)
        {
            return new JsonResult(new
            {
                success = false,
                message = "Não é permitido alterar o grupo do chamado."
            });
        }

        var setorFoiAlterado = chamado.SetorId != request.SetorId;
        var tipoFoiAlterado = chamado.OcorrenciaTipoId != request.OcorrenciaTipoId;
        var categoriaFoiAlterada = chamado.OcorrenciaCategoriaId != request.OcorrenciaCategoriaId;
        var subcategoriaFoiAlterada = chamado.OcorrenciaSubcategoriaId != request.OcorrenciaSubcategoriaId;

        if (setorFoiAlterado && request.SetorId.HasValue)
        {
            var setorValido = await _context.Setores
                .AsNoTracking()
                .AnyAsync(s => s.Id == request.SetorId.Value && s.GrupoId == chamado.GrupoId);

            if (!setorValido)
            {
                return new JsonResult(new
                {
                    success = false,
                    message = "O setor informado não pertence ao grupo do chamado."
                });
            }
        }

        if (tipoFoiAlterado && request.OcorrenciaTipoId.HasValue)
        {
            var tipoValido = await _context.OcorrenciasTipo
                .AsNoTracking()
                .AnyAsync(t => t.Id == request.OcorrenciaTipoId.Value && t.GrupoId == chamado.GrupoId);

            if (!tipoValido)
            {
                return new JsonResult(new
                {
                    success = false,
                    message = "O tipo de ocorrência informado não pertence ao grupo do chamado."
                });
            }
        }

        if ((categoriaFoiAlterada || tipoFoiAlterado) && request.OcorrenciaCategoriaId.HasValue)
        {
            if (!request.OcorrenciaTipoId.HasValue)
            {
                return new JsonResult(new
                {
                    success = false,
                    message = "A categoria informada exige um tipo de ocorrência válido."
                });
            }

            var categoriaValida = await _context.OcorrenciasCategoria
                .AsNoTracking()
                .AnyAsync(c =>
                    c.Id == request.OcorrenciaCategoriaId.Value &&
                    c.TipoId == request.OcorrenciaTipoId.Value);

            if (!categoriaValida)
            {
                return new JsonResult(new
                {
                    success = false,
                    message = "A categoria informada não pertence ao tipo de ocorrência selecionado."
                });
            }
        }

        if ((subcategoriaFoiAlterada || categoriaFoiAlterada) && request.OcorrenciaSubcategoriaId.HasValue)
        {
            if (!request.OcorrenciaCategoriaId.HasValue)
            {
                return new JsonResult(new
                {
                    success = false,
                    message = "A subcategoria informada exige uma categoria válida."
                });
            }

            var subcategoriaValida = await _context.OcorrenciasSubcategoria
                .AsNoTracking()
                .AnyAsync(sc =>
                    sc.Id == request.OcorrenciaSubcategoriaId.Value &&
                    sc.CategoriaId == request.OcorrenciaCategoriaId.Value);

            if (!subcategoriaValida)
            {
                return new JsonResult(new
                {
                    success = false,
                    message = "A subcategoria informada não pertence à categoria selecionada."
                });
            }
        }

        var houveAlteracao = false;

        if (GrupoPermissionService.PodeEditarCampoChamado(contextoMembro.Permissao, ChamadoCampoEditavel.Titulo, idUsuario.Value, chamado.CriadorChamadoId)
            && chamado.Titulo != request.Titulo)
        {
            chamado.Titulo = string.IsNullOrWhiteSpace(request.Titulo) ? null : request.Titulo.Trim();
            houveAlteracao = true;
        }

        if (GrupoPermissionService.PodeEditarCampoChamado(contextoMembro.Permissao, ChamadoCampoEditavel.Descricao, idUsuario.Value, chamado.CriadorChamadoId)
            && chamado.Descricao != request.Descricao)
        {
            chamado.Descricao = string.IsNullOrWhiteSpace(request.Descricao) ? null : request.Descricao.Trim();
            houveAlteracao = true;
        }

        if (GrupoPermissionService.PodeEditarCampoChamado(contextoMembro.Permissao, ChamadoCampoEditavel.Solucao, idUsuario.Value, chamado.CriadorChamadoId)
            && chamado.Solucao != request.Solucao)
        {
            chamado.Solucao = string.IsNullOrWhiteSpace(request.Solucao) ? null : request.Solucao.Trim();
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

        if (!TryParseNullableEnum<PrioridadeChamado>(request.Prioridade, out var novaPrioridade))
            return new JsonResult(new { success = false, message = "Prioridade invalida." });

        if (GrupoPermissionService.PodeEditarCampoChamado(contextoMembro.Permissao, ChamadoCampoEditavel.Prioridade, idUsuario.Value, chamado.CriadorChamadoId)
            && chamado.Prioridade != novaPrioridade)
        {
            chamado.Prioridade = novaPrioridade;
            houveAlteracao = true;
        }

        if (!TryParseNullableEnum<CriticidadeChamado>(request.Criticidade, out var novaCriticidade))
            return new JsonResult(new { success = false, message = "Criticidade invalida." });

        if (GrupoPermissionService.PodeEditarCampoChamado(contextoMembro.Permissao, ChamadoCampoEditavel.Criticidade, idUsuario.Value, chamado.CriadorChamadoId)
            && chamado.Criticidade != novaCriticidade)
        {
            chamado.Criticidade = novaCriticidade;
            houveAlteracao = true;
        }

        if (!TryParseNullableEnum<UrgenciaChamado>(request.Urgencia, out var novaUrgencia))
            return new JsonResult(new { success = false, message = "Urgencia invalida." });

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
            return new JsonResult(new
            {
                success = true,
                message = "Chamado salvo com sucesso."
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

    private int? GetUsuarioLogadoId()
    {
        var claim = User.FindFirst("Id")?.Value;
        return int.TryParse(claim, out var id) ? id : null;
    }

    public async Task<IActionResult> OnGetCategoriasPorTipoAsync(int tipoId)
    {
        var idUsuario = GetUsuarioLogadoId();
        if (idUsuario == null)
            return new JsonResult(new { success = false, message = "Usuario nao autenticado." });

        if (GrupoId <= 0 || tipoId <= 0)
            return new JsonResult(new { success = false, message = "Parametros invalidos." });

        var contextoMembro = await _grupoAuthorizationService.ObterContextoMembroAsync(idUsuario.Value, GrupoId);
        if (contextoMembro == null)
            return new JsonResult(new { success = false, message = "Voce nao pertence a este grupo." });

        var tipoValido = await _context.OcorrenciasTipo
            .AsNoTracking()
            .AnyAsync(t => t.Id == tipoId && t.GrupoId == GrupoId);

        if (!tipoValido)
            return new JsonResult(new { success = false, message = "Tipo de ocorrencia invalido." });

        var categorias = await ObterCategoriasPorTipoAsync(tipoId);

        return new JsonResult(new { success = true, categorias });
    }

    public async Task<IActionResult> OnGetSubcategoriasPorCategoriaAsync(int categoriaId)
    {
        var idUsuario = GetUsuarioLogadoId();
        if (idUsuario == null)
            return new JsonResult(new { success = false, message = "Usuario nao autenticado." });

        if (GrupoId <= 0 || categoriaId <= 0)
            return new JsonResult(new { success = false, message = "Parametros invalidos." });

        var contextoMembro = await _grupoAuthorizationService.ObterContextoMembroAsync(idUsuario.Value, GrupoId);
        if (contextoMembro == null)
            return new JsonResult(new { success = false, message = "Voce nao pertence a este grupo." });

        var categoriaValida = await (
            from categoria in _context.OcorrenciasCategoria.AsNoTracking()
            join tipo in _context.OcorrenciasTipo.AsNoTracking()
                on categoria.TipoId equals tipo.Id
            where categoria.Id == categoriaId && tipo.GrupoId == GrupoId
            select categoria.Id
        ).AnyAsync();

        if (!categoriaValida)
            return new JsonResult(new { success = false, message = "Categoria invalida." });

        var subcategorias = await ObterSubcategoriasPorCategoriaAsync(categoriaId);

        return new JsonResult(new { success = true, subcategorias });
    }

    private Task<List<Setor>> ObterSetoresDisponiveisAsync(int grupoId)
    {
        return _memoryCache.GetOrCreateAsync(
            $"catalogo:setores:{grupoId}",
            async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = CacheCatalogoTtl;
                return await _context.Setores
                    .AsNoTracking()
                    .Where(s => s.GrupoId == grupoId)
                    .OrderBy(s => s.NomeSetor)
                    .ToListAsync();
            })!;
    }

    private Task<List<OcorrenciaTipo>> ObterTiposOcorrenciaDisponiveisAsync(int grupoId)
    {
        return _memoryCache.GetOrCreateAsync(
            $"catalogo:tipos:{grupoId}",
            async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = CacheCatalogoTtl;
                return await _context.OcorrenciasTipo
                    .AsNoTracking()
                    .Where(t => t.GrupoId == grupoId)
                    .OrderBy(t => t.TipoOcorrencia)
                    .ToListAsync();
            })!;
    }

    private Task<List<object>> ObterCategoriasPorTipoAsync(int tipoId)
    {
        return _memoryCache.GetOrCreateAsync(
            $"catalogo:categorias:{tipoId}",
            async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = CacheCatalogoTtl;
                return await _context.OcorrenciasCategoria
                    .AsNoTracking()
                    .Where(c => c.TipoId == tipoId)
                    .OrderBy(c => c.CategoriaOcorrencia)
                    .Select(c => (object)new { id = c.Id, nome = c.CategoriaOcorrencia })
                    .ToListAsync();
            })!;
    }

    private Task<List<object>> ObterSubcategoriasPorCategoriaAsync(int categoriaId)
    {
        return _memoryCache.GetOrCreateAsync(
            $"catalogo:subcategorias:{categoriaId}",
            async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = CacheCatalogoTtl;
                return await _context.OcorrenciasSubcategoria
                    .AsNoTracking()
                    .Where(sc => sc.CategoriaId == categoriaId)
                    .OrderBy(sc => sc.SubcategoriaOcorrencia)
                    .Select(sc => (object)new { id = sc.Id, nome = sc.SubcategoriaOcorrencia })
                    .ToListAsync();
            })!;
    }

    private static bool TryParseNullableEnum<TEnum>(string? valor, out TEnum? resultado) where TEnum : struct
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            resultado = null;
            return true;
        }

        if (Enum.TryParse<TEnum>(valor, out var valorEnum))
        {
            resultado = valorEnum;
            return true;
        }

        resultado = null;
        return false;
    }

    private static bool EhErroDuplicidade(DbUpdateException ex)
    {
        var mensagem = ex.InnerException?.Message ?? ex.Message;
        return mensagem.Contains("Duplicate", StringComparison.OrdinalIgnoreCase) ||
               mensagem.Contains("duplicate", StringComparison.OrdinalIgnoreCase) ||
               mensagem.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase);
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
