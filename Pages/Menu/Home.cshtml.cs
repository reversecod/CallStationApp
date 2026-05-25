using CallStationApp.Authorization;
using CallStationApp.Data;
using CallStationApp.Models;
using CallStationApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Globalization;

namespace CallStationApp.Pages.Menu;

[Authorize]
public class HomeModel : PageModel
{
    private const string ReferenciaTipoComentarioChamado = "ComentarioChamado";
    private const string ReferenciaTipoComentarioHistorico = "ComentarioHistoricoChamado";
    private const int TamanhoPaginaComentarios = 30;
    private const int LimiteCaracteresComentario = 250;
    private const int TamanhoMinimoPesquisaVinculo = 2;
    private const int LimiteCandidatosVinculo = 12;
    private static readonly TimeZoneInfo FusoHorarioRegional = ObterFusoHorarioRegional();

    private static readonly StatusChamado[] StatusFinais =
    {
        StatusChamado.Concluido,
        StatusChamado.Fechado,
        StatusChamado.Cancelado
    };

    private static readonly StatusChamado[] StatusBloqueadosVinculo =
    {
        StatusChamado.Cancelado,
        StatusChamado.Excluido
    };

    private readonly AppDbContext _context;
    private readonly IWebHostEnvironment _environment;
    private readonly GrupoAuthorizationService _grupoAuthorizationService;
    private readonly NotificacaoService _notificacaoService;
    private readonly MencaoService _mencaoService;
    private readonly ILogger<HomeModel> _logger;
    private const int TamanhoPaginaChamados = 20;

    public HomeModel(
        AppDbContext context,
        IWebHostEnvironment environment,
        GrupoAuthorizationService grupoAuthorizationService,
        NotificacaoService notificacaoService,
        MencaoService mencaoService,
        ILogger<HomeModel> logger)
    {
        _context = context;
        _environment = environment;
        _grupoAuthorizationService = grupoAuthorizationService;
        _notificacaoService = notificacaoService;
        _mencaoService = mencaoService;
        _logger = logger;
    }

    [BindProperty(SupportsGet = true)]
    public int GrupoId { get; set; }

    [BindProperty(SupportsGet = true, Name = "page")]
    public int PaginaAtual { get; set; } = 1;

    public string? NomeUsuarioLogado { get; set; }
    public string? FotoUsuarioLogado { get; set; }
    public Grupo? GrupoAtual { get; set; }
    public GrupoConfiguracao? Configuracao { get; set; }
    public List<ChamadoListItemViewModel> Chamados { get; set; } = new();
    public List<Setor> SetoresDisponiveis { get; set; } = new();
    public List<OcorrenciaTipo> TiposOcorrenciaDisponiveis { get; set; } = new();
    public bool PodeCriarChamado { get; set; }
    public bool UsuarioLogadoEhAdministrador { get; set; }
    public PermissaoUsuario UsuarioLogadoPermissao { get; set; } = PermissaoUsuario.Nenhuma;
    public long ServerNowUnixMs { get; set; }
    public bool TemPaginaAnterior => PaginaAtual > 1;
    public bool TemProximaPagina { get; set; }

    public class ChamadoListItemViewModel
    {
        public int Id { get; set; }
        public int NumeroChamadoGrupo { get; set; }
        public int NumeroChamadoUsuario { get; set; }
        public string? Titulo { get; set; }
        public StatusChamado Status { get; set; }
        public DateTime DataCriacao { get; set; }
        public bool TemComentariosNaoLidos { get; set; }
        public bool TemComentarios { get; set; }
    }

    public async Task<IActionResult> OnGetAsync()
    {
        ServerNowUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        var idUsuario = GetUsuarioLogadoId();
        if (idUsuario == null)
            return RedirectToPage("/Auth/Login");

        if (GrupoId <= 0)
            return RedirectToPage("/Menu/Menu");

        var contextoMembro = await _grupoAuthorizationService
            .ObterContextoMembroAsync(idUsuario.Value, GrupoId);

        if (contextoMembro == null)
            return RedirectToPage("/Menu/Menu");

        await AtualizarUltimoAcessoGrupoAsync(idUsuario.Value, GrupoId);

        PodeCriarChamado = GrupoPermissionService.PodeCriarChamado(contextoMembro.Permissao);
        UsuarioLogadoEhAdministrador = GrupoPermissionService.PodeGerenciarGrupo(contextoMembro.Permissao);
        UsuarioLogadoPermissao = contextoMembro.Permissao;

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
                        !StatusFinais.Contains(c.Status) &&
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
                NumeroChamadoUsuario = c.NumeroChamadoUsuario,
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

        if (Chamados.Count > 0)
        {
            var chamadosIds = Chamados.Select(c => c.Id).ToList();
            var chamadosComComentariosNaoLidos = await _context.Notificacoes
                .AsNoTracking()
                .Where(n => n.UsuarioId == idUsuario.Value &&
                            n.GrupoId == GrupoId &&
                            !n.Lida &&
                            n.Tipo == TipoNotificacao.Chamado &&
                            (n.ReferenciaTipo == ReferenciaTipoComentarioChamado ||
                             n.ReferenciaTipo == ReferenciaTipoComentarioHistorico) &&
                            n.ReferenciaId.HasValue &&
                            chamadosIds.Contains(n.ReferenciaId.Value))
                .Select(n => n.ReferenciaId!.Value)
                .Distinct()
                .ToListAsync();

            var chamadosComComentariosNaoLidosSet = chamadosComComentariosNaoLidos.ToHashSet();

            var chamadosComComentarios = await _context.ComentariosChamados
                .AsNoTracking()
                .Where(c => chamadosIds.Contains(c.ChamadoId))
                .Select(c => c.ChamadoId)
                .Distinct()
                .ToListAsync();

            var chamadosComComentariosSet = chamadosComComentarios.ToHashSet();
            foreach (var chamado in Chamados)
            {
                chamado.TemComentariosNaoLidos = chamadosComComentariosNaoLidosSet.Contains(chamado.Id);
                chamado.TemComentarios = chamadosComComentariosSet.Contains(chamado.Id);
            }
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
        .FirstOrDefaultAsync(c => c.Id == id && c.GrupoId == GrupoId && c.Status != StatusChamado.Excluido);

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

        var config = await _context.GruposConfiguracoes
            .AsNoTracking()
            .Where(c => c.GrupoId == chamado.GrupoId)
            .Select(c => new
            {
                c.ExibirDataFinalizacaoModal,
                c.ExibirPrazoRespostaModal,
                c.ExibirPrazoConclusaoModal
            })
            .FirstOrDefaultAsync();

        var possuiSetor = await _context.Setores
            .AsNoTracking()
            .AnyAsync(s => s.GrupoId == chamado.GrupoId);

        var possuiTipoOcorrencia = await _context.OcorrenciasTipo
            .AsNoTracking()
            .AnyAsync(t => t.GrupoId == chamado.GrupoId);

        var possuiCategoria = await (
            from categoria in _context.OcorrenciasCategoria.AsNoTracking()
            join tipo in _context.OcorrenciasTipo.AsNoTracking() on categoria.TipoId equals tipo.Id
            where tipo.GrupoId == chamado.GrupoId
            select categoria.Id).AnyAsync();

        var possuiSubcategoria = await (
            from subcategoria in _context.OcorrenciasSubcategoria.AsNoTracking()
            join categoria in _context.OcorrenciasCategoria.AsNoTracking() on subcategoria.CategoriaId equals categoria.Id
            join tipo in _context.OcorrenciasTipo.AsNoTracking() on categoria.TipoId equals tipo.Id
            where tipo.GrupoId == chamado.GrupoId
            select subcategoria.Id).AnyAsync();

        var podeEditarSetor = possuiSetor &&
            GrupoPermissionService.PodeEditarCampoChamado(contextoMembro.Permissao, ChamadoCampoEditavel.SetorId, idUsuario.Value, chamado.CriadorChamadoId);
        var podeEditarTipo = possuiTipoOcorrencia &&
            GrupoPermissionService.PodeEditarCampoChamado(contextoMembro.Permissao, ChamadoCampoEditavel.OcorrenciaTipoId, idUsuario.Value, chamado.CriadorChamadoId);
        var podeEditarCategoria = possuiCategoria &&
            GrupoPermissionService.PodeEditarCampoChamado(contextoMembro.Permissao, ChamadoCampoEditavel.OcorrenciaCategoriaId, idUsuario.Value, chamado.CriadorChamadoId);
        var podeEditarSubcategoria = possuiSubcategoria &&
            GrupoPermissionService.PodeEditarCampoChamado(contextoMembro.Permissao, ChamadoCampoEditavel.OcorrenciaSubcategoriaId, idUsuario.Value, chamado.CriadorChamadoId);
        var podeEditarDataFinalizacao = (config?.ExibirDataFinalizacaoModal ?? true) &&
            GrupoPermissionService.PodeEditarCampoChamado(contextoMembro.Permissao, ChamadoCampoEditavel.DataFinalizacao, idUsuario.Value, chamado.CriadorChamadoId);
        var podeEditarPrazoResposta = (config?.ExibirPrazoRespostaModal ?? true) &&
            GrupoPermissionService.PodeEditarCampoChamado(contextoMembro.Permissao, ChamadoCampoEditavel.PrazoResposta, idUsuario.Value, chamado.CriadorChamadoId);
        var podeEditarPrazoConclusao = (config?.ExibirPrazoConclusaoModal ?? true) &&
            GrupoPermissionService.PodeEditarCampoChamado(contextoMembro.Permissao, ChamadoCampoEditavel.PrazoConclusao, idUsuario.Value, chamado.CriadorChamadoId);

        var temComentarios = await _context.ComentariosChamados
            .AsNoTracking()
            .AnyAsync(c => c.ChamadoId == chamado.Id);

        return new JsonResult(new
        {
            success = true,
            id = chamado.Id,
            numeroChamadoGrupo = chamado.NumeroChamadoGrupo,
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
            temComentarios,
            dataCriacao = ParaDataHoraRegionalIso(chamado.DataCriacao),
            dataFinalizacao = ParaDataHoraRegionalIso(chamado.DataFinalizacao),
            prazoResposta = ParaDataHoraRegionalIso(chamado.PrazoResposta),
            prazoConclusao = ParaDataHoraRegionalIso(chamado.PrazoConclusao),
            publico = chamado.Publico,
            criadorNomeUsuario,
            criadorPermissao,
            permissoes = new
            {
                podeEditarTitulo = GrupoPermissionService.PodeEditarCampoChamado(contextoMembro.Permissao, ChamadoCampoEditavel.Titulo, idUsuario.Value, chamado.CriadorChamadoId),
                podeEditarDescricao = GrupoPermissionService.PodeEditarCampoChamado(contextoMembro.Permissao, ChamadoCampoEditavel.Descricao, idUsuario.Value, chamado.CriadorChamadoId),
                podeEditarSolucao = GrupoPermissionService.PodeEditarCampoChamado(contextoMembro.Permissao, ChamadoCampoEditavel.Solucao, idUsuario.Value, chamado.CriadorChamadoId),
                podeEditarSetorId = podeEditarSetor,
                podeEditarOcorrenciaTipoId = podeEditarTipo,
                podeEditarOcorrenciaCategoriaId = podeEditarCategoria,
                podeEditarOcorrenciaSubcategoriaId = podeEditarSubcategoria,
                podeEditarAnexoChamado = GrupoPermissionService.PodeEditarCampoChamado(contextoMembro.Permissao, ChamadoCampoEditavel.AnexoChamado, idUsuario.Value, chamado.CriadorChamadoId),
                podeEditarPrioridade = GrupoPermissionService.PodeEditarCampoChamado(contextoMembro.Permissao, ChamadoCampoEditavel.Prioridade, idUsuario.Value, chamado.CriadorChamadoId),
                podeEditarCriticidade = GrupoPermissionService.PodeEditarCampoChamado(contextoMembro.Permissao, ChamadoCampoEditavel.Criticidade, idUsuario.Value, chamado.CriadorChamadoId),
                podeEditarUrgencia = GrupoPermissionService.PodeEditarCampoChamado(contextoMembro.Permissao, ChamadoCampoEditavel.Urgencia, idUsuario.Value, chamado.CriadorChamadoId),
                podeEditarStatus = GrupoPermissionService.PodeEditarCampoChamado(contextoMembro.Permissao, ChamadoCampoEditavel.Status, idUsuario.Value, chamado.CriadorChamadoId),
                podeEditarDataFinalizacao = podeEditarDataFinalizacao,
                podeEditarPrazoResposta = podeEditarPrazoResposta,
                podeEditarPrazoConclusao = podeEditarPrazoConclusao,
                podeEditarPublico = GrupoPermissionService.PodeEditarCampoChamado(contextoMembro.Permissao, ChamadoCampoEditavel.Publico, idUsuario.Value, chamado.CriadorChamadoId),
                podeAcessarComentariosChamado = PodeAcessarComentariosChamado(contextoMembro.Permissao),
                podeAcessarVinculosChamado = PodeAcessarVinculosChamado(contextoMembro.Permissao),
                podeExcluir = GrupoPermissionService.PodeExcluirChamado(contextoMembro.Permissao, idUsuario.Value, chamado.CriadorChamadoId)
            }
        });
    }

    public async Task<IActionResult> OnGetAnexoChamadoAsync(int chamadoId, int grupoId)
    {
        var idUsuario = GetUsuarioLogadoId();
        if (idUsuario == null)
            return Unauthorized();

        if (chamadoId <= 0 || grupoId <= 0)
            return NotFound();

        var contextoMembro = await _grupoAuthorizationService
            .ObterContextoMembroAsync(idUsuario.Value, grupoId);

        if (contextoMembro == null)
            return Forbid();

        var chamado = await _context.Chamados
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == chamadoId && c.GrupoId == grupoId && c.Status != StatusChamado.Excluido);

        if (chamado == null || string.IsNullOrWhiteSpace(chamado.AnexoChamado))
            return NotFound();

        if (!GrupoPermissionService.PodeVerChamado(
                contextoMembro.Permissao,
                chamado.Publico,
                idUsuario.Value,
                chamado.CriadorChamadoId))
        {
            return Forbid();
        }

        var nomeArquivo = Path.GetFileName(chamado.AnexoChamado);
        if (string.IsNullOrWhiteSpace(nomeArquivo))
            return NotFound();

        var caminhoFisico = Path.Combine(
            _environment.ContentRootPath,
            "uploads_privados",
            "chamados",
            nomeArquivo);

        if (!System.IO.File.Exists(caminhoFisico))
            return NotFound();

        return PhysicalFile(caminhoFisico, ObterContentTypeAnexo(nomeArquivo));
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
                        await _notificacaoService.CriarNotificacoesChamadoAbertoAsync(chamado, idUsuario.Value);

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
                {
                    _logger.LogError(ex, "Erro de banco ao criar chamado no grupo {GrupoId}.", grupoId);
                    return BadRequest(new { success = false, message = "Não foi possível criar o chamado no momento." });
                }
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Erro de banco ao criar chamado no grupo {GrupoId}.", grupoId);
                return BadRequest(new { success = false, message = "Não foi possível criar o chamado no momento." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao criar chamado no grupo {GrupoId}.", grupoId);
                return BadRequest(new { success = false, message = "Não foi possível criar o chamado no momento." });
            }
        }

        return BadRequest(new { success = false, message = "Não foi possível criar o chamado no momento." });
    }

    public async Task<IActionResult> OnPostCriarTarefaDeChamadoAsync([FromBody] CriarTarefaDeChamadoRequest request)
    {
        var idUsuario = GetUsuarioLogadoId();
        if (idUsuario == null)
            return Unauthorized();

        if (request == null || request.GrupoId <= 0 || request.ChamadoId <= 0)
            return BadRequest(new { success = false, message = "Parâmetros inválidos." });

        var contextoMembro = await _grupoAuthorizationService
            .ObterContextoMembroAsync(idUsuario.Value, request.GrupoId);

        if (contextoMembro == null)
            return Forbid();

        var chamado = await _context.Chamados
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == request.ChamadoId && c.GrupoId == request.GrupoId);

        if (chamado == null)
            return NotFound(new { success = false, message = "Chamado não encontrado." });

        if (!GrupoPermissionService.PodeVerChamado(
                contextoMembro.Permissao,
                chamado.Publico,
                idUsuario.Value,
                chamado.CriadorChamadoId))
        {
            return Forbid();
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
                        var quadro = await GarantirQuadroTarefasPadraoAsync(request.GrupoId, idUsuario.Value);
                        var coluna = await ObterColunaTarefasDestinoAsync(quadro.Id, request.ColunaId);

                        if (coluna == null)
                        {
                            return (IActionResult)BadRequest(new
                            {
                                success = false,
                                message = "Crie uma lista em Tarefas antes de arrastar chamados."
                            });
                        }

                        var contador = await _context.CartaoTarefaContadorGrupo.FirstOrDefaultAsync(c => c.GrupoId == request.GrupoId);

                        if (contador == null)
                        {
                            contador = new CartaoTarefaContadorGrupo
                            {
                                GrupoId = request.GrupoId,
                                UltimoNumero = 0
                            };

                            _context.CartaoTarefaContadorGrupo.Add(contador);
                        }

                        contador.UltimoNumero++;

                        var ultimaOrdem = await _context.CartoesTarefas
                            .Where(c => c.ColunaId == coluna.Id)
                            .MaxAsync(c => (decimal?)c.OrdemColuna) ?? 0m;

                        var tituloChamado = string.IsNullOrWhiteSpace(chamado.Titulo) ||
                                             string.Equals(chamado.Titulo, "Novo chamado", StringComparison.OrdinalIgnoreCase)
                            ? $"Chamado {chamado.NumeroChamadoGrupo}"
                            : chamado.Titulo;

                        var cartao = new CartaoTarefa
                        {
                            QuadroId = quadro.Id,
                            ColunaId = coluna.Id,
                            GrupoId = request.GrupoId,
                            NumeroCartaoGrupo = contador.UltimoNumero,
                            Titulo = tituloChamado,
                            Descricao = chamado.Descricao,
                            CriadorId = idUsuario.Value,
                            Prioridade = chamado.Prioridade,
                            Criticidade = chamado.Criticidade,
                            Urgencia = chamado.Urgencia,
                            Status = StatusCartaoTarefa.Ativa,
                            OrdemColuna = ultimaOrdem + 1024m,
                            PercentualConclusao = 0m,
                            Privado = true,
                            DataCriacao = DateTime.UtcNow,
                            DataAtualizacao = DateTime.UtcNow
                        };

                        _context.CartoesTarefas.Add(cartao);

                        _context.CartoesTarefasUsuarios.Add(new CartaoTarefaUsuario
                        {
                            CartaoTarefa = cartao,
                            UsuarioId = idUsuario.Value,
                            TipoParticipacao = TipoParticipacaoCartaoTarefa.Participante,
                            Permissao = PermissaoCartaoTarefa.Editor,
                            DataAdicao = DateTime.UtcNow,
                            AdicionadoPorUsuarioId = idUsuario.Value
                        });

                        _context.CartoesTarefasChamados.Add(new CartaoTarefaChamado
                        {
                            CartaoTarefa = cartao,
                            ChamadoId = chamado.Id,
                            TipoRelacao = TipoRelacaoCartaoChamado.Origem,
                            Ativo = true,
                            DataVinculo = DateTime.UtcNow,
                            VinculadoPorUsuarioId = idUsuario.Value
                        });

                        _context.HistoricoTarefas.Add(new HistoricoTarefa
                        {
                            CartaoTarefa = cartao,
                            UsuarioId = idUsuario.Value,
                            TipoAcao = "Cartão criado a partir de chamado",
                            CampoAlterado = "chamado_id",
                            ValorNovo = chamado.Id.ToString(),
                            DataAcao = DateTime.UtcNow
                        });

                        await _context.SaveChangesAsync();
                        await transaction.CommitAsync();

                        return (IActionResult)new JsonResult(new
                        {
                            success = true,
                            cartaoId = cartao.Id,
                            redirectUrl = Url.Page("/Menu/Tasks", new { grupoId = request.GrupoId })
                        });
                    }
                    catch
                    {
                        await transaction.RollbackAsync();
                        throw;
                    }
                });
            }
            catch (DbUpdateException ex) when (tentativa < maxTentativas && EhErroDuplicidade(ex))
            {
                _context.ChangeTracker.Clear();
            }
            catch (DbUpdateException ex) when (EhErroDuplicidade(ex))
            {
                _logger.LogWarning(ex, "Duplicidade ao criar tarefa do chamado {ChamadoId} no grupo {GrupoId}.", request.ChamadoId, request.GrupoId);
                return BadRequest(new { success = false, message = "Não foi possível criar a tarefa no momento. Tente novamente." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao criar tarefa do chamado {ChamadoId} no grupo {GrupoId}.", request.ChamadoId, request.GrupoId);
                return BadRequest(new { success = false, message = "Não foi possível criar a tarefa no momento." });
            }
        }

        return BadRequest(new { success = false, message = "Não foi possível criar a tarefa no momento. Tente novamente." });
    }
    public async Task<IActionResult> OnGetListasTarefasAsync(int grupoId)
    {
        var idUsuario = GetUsuarioLogadoId();
        if (idUsuario == null)
            return Unauthorized();

        if (grupoId <= 0)
            return BadRequest(new { success = false, message = "Grupo inválido." });

        var contextoMembro = await _grupoAuthorizationService
            .ObterContextoMembroAsync(idUsuario.Value, grupoId);

        if (contextoMembro == null)
            return Forbid();

        var quadro = await _context.QuadrosTarefas
            .AsNoTracking()
            .FirstOrDefaultAsync(q => q.GrupoId == grupoId && q.Ativo);

        if (quadro == null)
            return new JsonResult(new { success = true, listas = Array.Empty<object>() });

        var listas = await _context.ColunasQuadro
            .AsNoTracking()
            .Where(c => c.QuadroId == quadro.Id && c.Ativa)
            .OrderBy(c => c.Posicao)
            .Select(c => new
            {
                id = c.Id,
                nome = c.Nome
            })
            .ToListAsync();

        return new JsonResult(new { success = true, listas });
    }

    public async Task<IActionResult> OnPostExcluirChamadoAsync([FromBody] ExcluirChamadoRequest request)
    {
        var idUsuario = GetUsuarioLogadoId();
        if (idUsuario == null)
            return new JsonResult(new { success = false, message = "Usuário não autenticado." });

        if (request == null || request.Id <= 0)
            return new JsonResult(new { success = false, message = "Chamado inválido." });

        var chamado = await _context.Chamados.FirstOrDefaultAsync(c => c.Id == request.Id && c.Status != StatusChamado.Excluido);
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
            return new JsonResult(new { success = false, message = "Você não tem permissão para cancelar este chamado." });
        }

        if (chamado.Status == StatusChamado.Cancelado)
            return new JsonResult(new { success = false, message = "O chamado já está cancelado." });

        var strategy = _context.Database.CreateExecutionStrategy();

        try
        {
            return await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _context.Database.BeginTransactionAsync();

                try
                {
                    var statusAnterior = chamado.Status;
                    var agora = DateTime.UtcNow;

                    chamado.Status = StatusChamado.Cancelado;
                    chamado.DataFinalizacao = agora;

                    RegistrarTransicaoStatusChamado(chamado, statusAnterior, StatusChamado.Cancelado, idUsuario.Value, false, "Cancelamento manual");
                    RegistrarHistoricoAlteracaoStatus(chamado, statusAnterior, StatusChamado.Cancelado, idUsuario.Value, "StatusManual");
                    _notificacaoService.CriarNotificacaoChamadoAlteradoParaDono(chamado, idUsuario.Value, "cancelamento");

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    return (IActionResult)new JsonResult(new { success = true, message = "Chamado cancelado com sucesso." });
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
            _logger.LogError(ex, "Erro ao cancelar chamado {ChamadoId}.", request.Id);
            return new JsonResult(new
            {
                success = false,
                message = "Não foi possível cancelar o chamado no momento."
            });
        }
    }

    public async Task<IActionResult> OnPostAvancarStatusChamadoAsync([FromBody] AvancarStatusChamadoRequest request)
    {
        var idUsuario = GetUsuarioLogadoId();
        if (idUsuario == null)
            return Unauthorized();

        if (request == null || request.Id <= 0 || request.GrupoId <= 0)
            return new JsonResult(new { success = false, message = "Chamado inválido." });

        var chamado = await _context.Chamados
            .FirstOrDefaultAsync(c => c.Id == request.Id && c.GrupoId == request.GrupoId && c.Status != StatusChamado.Excluido);

        if (chamado == null)
            return new JsonResult(new { success = false, message = "Chamado não encontrado." });

        var contextoMembro = await _grupoAuthorizationService.ObterContextoMembroAsync(idUsuario.Value, chamado.GrupoId);
        if (contextoMembro == null)
            return new JsonResult(new { success = false, message = "Você não tem acesso a este chamado." });

        if (!GrupoPermissionService.PodeVerChamado(contextoMembro.Permissao, chamado.Publico, idUsuario.Value, chamado.CriadorChamadoId))
            return new JsonResult(new { success = false, message = "Você não tem permissão para visualizar este chamado." });

        if (!GrupoPermissionService.PodeEditarCampoChamado(contextoMembro.Permissao, ChamadoCampoEditavel.Status, idUsuario.Value, chamado.CriadorChamadoId))
            return new JsonResult(new { success = false, message = "Você não tem permissão para alterar o status deste chamado." });

        var novoStatus = chamado.Status switch
        {
            StatusChamado.Aberto => StatusChamado.EmAndamento,
            StatusChamado.EmAndamento => StatusChamado.Concluido,
            _ => (StatusChamado?)null
        };

        if (!novoStatus.HasValue)
            return new JsonResult(new { success = false, message = "Este chamado não possui próximo status rápido." });

        var config = await _context.GruposConfiguracoes
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.GrupoId == chamado.GrupoId);

        if (config?.ExigirSolucaoParaConcluir == true &&
            novoStatus.Value == StatusChamado.Concluido &&
            string.IsNullOrWhiteSpace(chamado.Solucao))
        {
            return new JsonResult(new { success = false, message = "Informe a solução antes de concluir o chamado." });
        }

        var strategy = _context.Database.CreateExecutionStrategy();

        try
        {
            return await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _context.Database.BeginTransactionAsync();

                try
                {
                    var statusAnterior = chamado.Status;
                    chamado.Status = novoStatus.Value;

                    if (chamado.Status == StatusChamado.EmAndamento && chamado.DataInicioAtendimento == null)
                        chamado.DataInicioAtendimento = DateTime.UtcNow;

                    if (StatusFinais.Contains(chamado.Status) && chamado.DataFinalizacao == null)
                        chamado.DataFinalizacao = DateTime.UtcNow;

                    RegistrarTransicaoStatusChamado(chamado, statusAnterior, chamado.Status, idUsuario.Value, false, "Atualizacao rapida");
                    RegistrarHistoricoAlteracaoStatus(chamado, statusAnterior, chamado.Status, idUsuario.Value, "StatusRapido");
                    _notificacaoService.CriarNotificacaoChamadoAlteradoParaDono(chamado, idUsuario.Value, "alteracao");

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    return (IActionResult)new JsonResult(new
                    {
                        success = true,
                        message = $"Status alterado para {FormatarStatusChamado(chamado.Status)}.",
                        dados = new
                        {
                            chamado.Id,
                            status = chamado.Status.ToString(),
                            statusTexto = FormatarStatusChamado(chamado.Status)
                        }
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
            _logger.LogError(ex, "Erro ao avancar status do chamado {ChamadoId}.", request.Id);
            return new JsonResult(new { success = false, message = "Não foi possível avançar o status do chamado." });
        }
    }

    public async Task<IActionResult> OnPostSalvarChamadoAsync([FromForm] EditarChamadoRequest request)
    {
        var idUsuario = GetUsuarioLogadoId();
        if (idUsuario == null)
            return new JsonResult(new { success = false, message = "Usuário não autenticado." });

        if (request == null || request.Id <= 0)
            return new JsonResult(new { success = false, message = "Chamado inválido." });

        var chamado = await _context.Chamados
            .FirstOrDefaultAsync(c => c.Id == request.Id && c.Status != StatusChamado.Excluido);
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

            if (!await AssinaturaArquivoPermitidaAsync(request.AnexoArquivo, extensao))
                return new JsonResult(new { success = false, message = "Conteúdo do arquivo não permitido." });
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

        if (!TryNormalizarDataHoraNullable(request.DataFinalizacao, out var dataFinalizacao))
            return new JsonResult(new { success = false, message = "Data de finalizacao invalida." });

        if (!TryNormalizarDataHoraNullable(request.PrazoResposta, out var prazoResposta))
            return new JsonResult(new { success = false, message = "Prazo de resposta inválido." });

        if (!TryNormalizarDataHoraNullable(request.PrazoConclusao, out var prazoConclusao))
            return new JsonResult(new { success = false, message = "Prazo de conclusão inválido." });

        if (!request.OcorrenciaTipoId.HasValue)
        {
            request.OcorrenciaCategoriaId = null;
            request.OcorrenciaSubcategoriaId = null;
        }

        if (!request.OcorrenciaCategoriaId.HasValue)
        {
            request.OcorrenciaSubcategoriaId = null;
        }

        if (request.SetorId.HasValue)
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

        if (request.OcorrenciaTipoId.HasValue)
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

        if (request.OcorrenciaCategoriaId.HasValue)
        {
            if (!request.OcorrenciaTipoId.HasValue)
            {
                return new JsonResult(new
                {
                    success = false,
                    message = "A categoria informada exige um tipo de ocorrência válido."
                });
            }

            var categoriaValida = await (
                from categoria in _context.OcorrenciasCategoria.AsNoTracking()
                join tipo in _context.OcorrenciasTipo.AsNoTracking()
                    on categoria.TipoId equals tipo.Id
                where categoria.Id == request.OcorrenciaCategoriaId.Value &&
                      categoria.TipoId == request.OcorrenciaTipoId.Value &&
                      tipo.GrupoId == chamado.GrupoId
                select categoria.Id
            ).AnyAsync();

            if (!categoriaValida)
            {
                return new JsonResult(new
                {
                    success = false,
                    message = "A categoria informada não pertence ao tipo de ocorrência selecionado."
                });
            }
        }

        if (request.OcorrenciaSubcategoriaId.HasValue)
        {
            if (!request.OcorrenciaCategoriaId.HasValue)
            {
                return new JsonResult(new
                {
                    success = false,
                    message = "A subcategoria informada exige uma categoria válida."
                });
            }

            var subcategoriaValida = await (
                from subcategoria in _context.OcorrenciasSubcategoria.AsNoTracking()
                join categoria in _context.OcorrenciasCategoria.AsNoTracking()
                    on subcategoria.CategoriaId equals categoria.Id
                join tipo in _context.OcorrenciasTipo.AsNoTracking()
                    on categoria.TipoId equals tipo.Id
                where subcategoria.Id == request.OcorrenciaSubcategoriaId.Value &&
                      subcategoria.CategoriaId == request.OcorrenciaCategoriaId.Value &&
                      tipo.GrupoId == chamado.GrupoId
                select subcategoria.Id
            ).AnyAsync();

            if (!subcategoriaValida)
            {
                return new JsonResult(new
                {
                    success = false,
                    message = "A subcategoria informada não pertence à categoria selecionada."
                });
            }
        }

        var statusPermitidosEmEdicao = new HashSet<StatusChamado>
        {
            StatusChamado.Aberto,
            StatusChamado.EmAndamento,
            StatusChamado.Pendente,
            StatusChamado.Concluido,
            StatusChamado.Fechado,
            StatusChamado.Reaberto,
            StatusChamado.Cancelado
        };

        StatusChamado? novoStatus = null;
        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            if (!Enum.TryParse<StatusChamado>(request.Status, out var statusConvertido))
                return new JsonResult(new { success = false, message = "Status inválido." });

            if (chamado.Status != statusConvertido && !statusPermitidosEmEdicao.Contains(statusConvertido))
            {
                return new JsonResult(new
                {
                    success = false,
                    message = "Status inv\u00e1lido para edi\u00e7\u00e3o manual."
                });
            }

            novoStatus = statusConvertido;
        }

        var config = await _context.GruposConfiguracoes
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.GrupoId == chamado.GrupoId);

        var publicoSolicitado = request.Publico;
        if (config != null)
        {
            if (config.ObrigarSetor && request.SetorId == null)
                return new JsonResult(new { success = false, message = "O setor \u00e9 obrigat\u00f3rio para este grupo." });

            if (config.ObrigarTipoOcorrencia && request.OcorrenciaTipoId == null)
                return new JsonResult(new { success = false, message = "O tipo de ocorr\u00eancia \u00e9 obrigat\u00f3rio para este grupo." });

            if (config.ObrigarCategoria && request.OcorrenciaCategoriaId == null)
                return new JsonResult(new { success = false, message = "A categoria \u00e9 obrigat\u00f3ria para este grupo." });

            if (config.ObrigarSubcategoria && request.OcorrenciaSubcategoriaId == null)
                return new JsonResult(new { success = false, message = "A subcategoria \u00e9 obrigat\u00f3ria para este grupo." });

            if (!config.PermitirChamadoPublico && publicoSolicitado)
                publicoSolicitado = false;

            var podeEditarStatus = GrupoPermissionService.PodeEditarCampoChamado(
                contextoMembro.Permissao,
                ChamadoCampoEditavel.Status,
                idUsuario.Value,
                chamado.CriadorChamadoId);

            var statusAposSalvar = podeEditarStatus && novoStatus.HasValue ? novoStatus.Value : chamado.Status;
            if (config.ExigirSolucaoParaConcluir &&
                statusAposSalvar is StatusChamado.Concluido or StatusChamado.Fechado &&
                string.IsNullOrWhiteSpace(request.Solucao))
            {
                return new JsonResult(new { success = false, message = "Informe a solu\u00e7\u00e3o antes de concluir o chamado." });
            }
        }

        var possuiSetor = await _context.Setores
            .AsNoTracking()
            .AnyAsync(s => s.GrupoId == chamado.GrupoId);

        var possuiTipoOcorrencia = await _context.OcorrenciasTipo
            .AsNoTracking()
            .AnyAsync(t => t.GrupoId == chamado.GrupoId);

        var possuiCategoria = await (
            from categoria in _context.OcorrenciasCategoria.AsNoTracking()
            join tipo in _context.OcorrenciasTipo.AsNoTracking() on categoria.TipoId equals tipo.Id
            where tipo.GrupoId == chamado.GrupoId
            select categoria.Id).AnyAsync();

        var possuiSubcategoria = await (
            from subcategoria in _context.OcorrenciasSubcategoria.AsNoTracking()
            join categoria in _context.OcorrenciasCategoria.AsNoTracking() on subcategoria.CategoriaId equals categoria.Id
            join tipo in _context.OcorrenciasTipo.AsNoTracking() on categoria.TipoId equals tipo.Id
            where tipo.GrupoId == chamado.GrupoId
            select subcategoria.Id).AnyAsync();

        var houveAlteracao = false;
        var descricaoAlterada = false;
        var solucaoAlterada = false;
        var statusAnteriorOriginal = chamado.Status;
        var publicoAnteriorOriginal = chamado.Publico;

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
            descricaoAlterada = true;
        }

        if (GrupoPermissionService.PodeEditarCampoChamado(contextoMembro.Permissao, ChamadoCampoEditavel.Solucao, idUsuario.Value, chamado.CriadorChamadoId)
            && chamado.Solucao != request.Solucao)
        {
            chamado.Solucao = string.IsNullOrWhiteSpace(request.Solucao) ? null : request.Solucao.Trim();
            houveAlteracao = true;
            solucaoAlterada = true;
        }

        if (possuiSetor &&
            GrupoPermissionService.PodeEditarCampoChamado(contextoMembro.Permissao, ChamadoCampoEditavel.SetorId, idUsuario.Value, chamado.CriadorChamadoId)
            && chamado.SetorId != request.SetorId)
        {
            chamado.SetorId = request.SetorId;
            houveAlteracao = true;
        }

        if (possuiTipoOcorrencia &&
            GrupoPermissionService.PodeEditarCampoChamado(contextoMembro.Permissao, ChamadoCampoEditavel.OcorrenciaTipoId, idUsuario.Value, chamado.CriadorChamadoId)
            && chamado.OcorrenciaTipoId != request.OcorrenciaTipoId)
        {
            chamado.OcorrenciaTipoId = request.OcorrenciaTipoId;
            houveAlteracao = true;
        }

        if (possuiCategoria &&
            GrupoPermissionService.PodeEditarCampoChamado(contextoMembro.Permissao, ChamadoCampoEditavel.OcorrenciaCategoriaId, idUsuario.Value, chamado.CriadorChamadoId)
            && chamado.OcorrenciaCategoriaId != request.OcorrenciaCategoriaId)
        {
            chamado.OcorrenciaCategoriaId = request.OcorrenciaCategoriaId;
            houveAlteracao = true;
        }

        if (possuiSubcategoria &&
            GrupoPermissionService.PodeEditarCampoChamado(contextoMembro.Permissao, ChamadoCampoEditavel.OcorrenciaSubcategoriaId, idUsuario.Value, chamado.CriadorChamadoId)
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
            && novoStatus.HasValue
            && chamado.Status != novoStatus.Value)
        {
            chamado.Status = novoStatus.Value;
            houveAlteracao = true;
        }

        var statusFinalAtual = StatusFinais.Contains(chamado.Status);
        var statusAnteriorEraFinal = StatusFinais.Contains(statusAnteriorOriginal);
        var dataFinalizacaoFoiAjustadaAutomaticamente = false;

        if (chamado.Status != statusAnteriorOriginal && statusFinalAtual && dataFinalizacao == null)
        {
            dataFinalizacao = DateTime.UtcNow;
            dataFinalizacaoFoiAjustadaAutomaticamente = true;
        }

        if (chamado.Status != statusAnteriorOriginal &&
            !statusFinalAtual &&
            statusAnteriorEraFinal &&
            dataFinalizacao == null)
        {
            dataFinalizacao = null;
            dataFinalizacaoFoiAjustadaAutomaticamente = true;
        }

        var podeEditarDataFinalizacao = GrupoPermissionService.PodeEditarCampoChamado(
            contextoMembro.Permissao,
            ChamadoCampoEditavel.DataFinalizacao,
            idUsuario.Value,
            chamado.CriadorChamadoId) &&
            (config?.ExibirDataFinalizacaoModal ?? true);

        if ((podeEditarDataFinalizacao || dataFinalizacaoFoiAjustadaAutomaticamente)
            && chamado.DataFinalizacao != dataFinalizacao)
        {
            chamado.DataFinalizacao = dataFinalizacao;
            houveAlteracao = true;
        }

        if ((config?.ExibirPrazoRespostaModal ?? true) &&
            GrupoPermissionService.PodeEditarCampoChamado(contextoMembro.Permissao, ChamadoCampoEditavel.PrazoResposta, idUsuario.Value, chamado.CriadorChamadoId)
            && chamado.PrazoResposta != prazoResposta)
        {
            chamado.PrazoResposta = prazoResposta;
            houveAlteracao = true;
        }

        if ((config?.ExibirPrazoConclusaoModal ?? true) &&
            GrupoPermissionService.PodeEditarCampoChamado(contextoMembro.Permissao, ChamadoCampoEditavel.PrazoConclusao, idUsuario.Value, chamado.CriadorChamadoId)
            && chamado.PrazoConclusao != prazoConclusao)
        {
            chamado.PrazoConclusao = prazoConclusao;
            houveAlteracao = true;
        }

        if (GrupoPermissionService.PodeEditarCampoChamado(contextoMembro.Permissao, ChamadoCampoEditavel.Publico, idUsuario.Value, chamado.CriadorChamadoId)
            && chamado.Publico != publicoSolicitado)
        {
            chamado.Publico = publicoSolicitado;
            houveAlteracao = true;
        }

        if (GrupoPermissionService.PodeEditarCampoChamado(contextoMembro.Permissao, ChamadoCampoEditavel.AnexoChamado, idUsuario.Value, chamado.CriadorChamadoId)
            && request.AnexoArquivo != null
            && request.AnexoArquivo.Length > 0)
        {
            var uploadsRoot = Path.Combine(_environment.ContentRootPath, "uploads_privados", "chamados");
            Directory.CreateDirectory(uploadsRoot);

            var extensao = Path.GetExtension(request.AnexoArquivo.FileName).ToLowerInvariant();
            var extensoesPermitidas = new[] { ".jpg", ".jpeg", ".png", ".pdf" };
            if (!extensoesPermitidas.Contains(extensao))
                return new JsonResult(new { success = false, message = "Tipo de arquivo não permitido." });

            if (!await AssinaturaArquivoPermitidaAsync(request.AnexoArquivo, extensao))
                return new JsonResult(new { success = false, message = "Conteúdo do arquivo não permitido." });

            var nomeArquivo = $"{Guid.NewGuid()}{extensao}";
            var caminhoFisico = Path.Combine(uploadsRoot, nomeArquivo);

            await using var stream = new FileStream(caminhoFisico, FileMode.Create);
            await request.AnexoArquivo.CopyToAsync(stream);

            chamado.AnexoChamado = nomeArquivo;
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

        var strategy = _context.Database.CreateExecutionStrategy();

        try
        {
            return await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _context.Database.BeginTransactionAsync();

                try
                {
                    if (chamado.Status != statusAnteriorOriginal)
                    {
                        RegistrarTransicaoStatusChamado(chamado, statusAnteriorOriginal, chamado.Status, idUsuario.Value, false, "Atualizacao manual");
                        RegistrarHistoricoAlteracaoStatus(chamado, statusAnteriorOriginal, chamado.Status, idUsuario.Value, "StatusManual");
                    }

                    _notificacaoService.CriarNotificacaoChamadoAlteradoParaDono(chamado, idUsuario.Value, "alteracao");

                    if (!publicoAnteriorOriginal && chamado.Publico)
                        await _notificacaoService.CriarNotificacoesChamadoPublicoAdicionadoAsync(chamado, idUsuario.Value);

                    var usuariosPermitidosMencao = await ObterUsuariosPermitidosMencaoChamadoAsync(chamado, chamado.Publico);
                    if (descricaoAlterada)
                    {
                        await _mencaoService.SincronizarMencoesAsync(
                            chamado.GrupoId,
                            idUsuario.Value,
                            "Chamado",
                            chamado.Id,
                            "Descricao",
                            chamado.Descricao,
                            TipoNotificacao.Chamado,
                            "Voce foi mencionado em um chamado",
                            $"descricao do chamado #{chamado.NumeroChamadoGrupo}",
                            $"/Menu/Home?grupoId={chamado.GrupoId}",
                            usuariosPermitidosMencao);
                    }

                    if (solucaoAlterada)
                    {
                        await _mencaoService.SincronizarMencoesAsync(
                            chamado.GrupoId,
                            idUsuario.Value,
                            "Chamado",
                            chamado.Id,
                            "Solucao",
                            chamado.Solucao,
                            TipoNotificacao.Chamado,
                            "Voce foi mencionado em um chamado",
                            $"solucao do chamado #{chamado.NumeroChamadoGrupo}",
                            $"/Menu/Home?grupoId={chamado.GrupoId}",
                            usuariosPermitidosMencao);
                    }

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    return (IActionResult)new JsonResult(new
                    {
                        success = true,
                        message = "Chamado salvo com sucesso."
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
            _logger.LogError(ex, "Erro ao salvar chamado {ChamadoId}.", request.Id);
            return new JsonResult(new
            {
                success = false,
                message = "Não foi possível salvar o chamado no momento."
            });
        }
    }

    public async Task<IActionResult> OnGetMembrosMencaoAsync(int grupoId, int? chamadoId, string? termo)
    {
        var idUsuario = GetUsuarioLogadoId();
        if (idUsuario == null)
            return Unauthorized();

        var contexto = await _grupoAuthorizationService.ObterContextoMembroAsync(idUsuario.Value, grupoId);
        if (contexto == null)
            return new JsonResult(new { success = false, message = "Voce nao pertence a este grupo." });

        IEnumerable<int>? usuariosPermitidos = null;
        if (chamadoId.HasValue && chamadoId.Value > 0)
        {
            var acesso = await ObterChamadoComAcessoAsync(idUsuario.Value, grupoId, chamadoId.Value);
            if (!acesso.Success)
                return new JsonResult(new { success = false, message = acesso.Message });

            usuariosPermitidos = await ObterUsuariosPermitidosMencaoChamadoAsync(acesso.Chamado!, acesso.Chamado!.Publico);
        }

        var membros = await _mencaoService.BuscarMembrosAsync(grupoId, idUsuario.Value, termo, usuariosPermitidos);
        return new JsonResult(new { success = true, dados = membros });
    }

    public async Task<IActionResult> OnGetComentariosChamadoAsync(int chamadoId, int page = 1)
    {
        var idUsuario = GetUsuarioLogadoId();
        if (idUsuario == null)
            return Unauthorized();

        var acesso = await ObterChamadoComAcessoAsync(idUsuario.Value, GrupoId, chamadoId);
        if (!acesso.Success)
            return new JsonResult(new { success = false, message = acesso.Message });
        if (!PodeAcessarComentariosChamado(acesso.ContextoMembro!.Permissao))
            return new JsonResult(new { success = false, message = "Você não tem permissão para acessar comentários." });

        var chamado = acesso.Chamado!;
        var pagina = Math.Max(page, 1);
        var comentariosBase = await (
            from comentario in _context.ComentariosChamados.AsNoTracking()
            join usuario in _context.Usuarios.AsNoTracking() on comentario.UsuarioId equals usuario.Id
            join info in _context.InfoUsuariosGrupos.AsNoTracking()
                on new { UsuarioId = comentario.UsuarioId, GrupoId = chamado.GrupoId }
                equals new { info.UsuarioId, info.GrupoId } into infoJoin
            from infoUsuario in infoJoin.DefaultIfEmpty()
            where comentario.ChamadoId == chamado.Id
            orderby comentario.DataComentario descending, comentario.Id descending
            select new
            {
                comentario.Id,
                comentario.UsuarioId,
                Autor = infoUsuario != null && !string.IsNullOrWhiteSpace(infoUsuario.Apelido)
                    ? infoUsuario.Apelido
                    : usuario.NomeUsuario,
                Texto = comentario.Mensagem,
                comentario.DataComentario,
                comentario.AnexoComentario
            })
            .Skip((pagina - 1) * TamanhoPaginaComentarios)
            .Take(TamanhoPaginaComentarios + 1)
            .ToListAsync();

        var temMais = comentariosBase.Count > TamanhoPaginaComentarios;
        if (temMais)
            comentariosBase.RemoveAt(comentariosBase.Count - 1);

        var comentarios = comentariosBase.Select(comentario => new
        {
            id = comentario.Id,
            usuarioId = comentario.UsuarioId,
            autor = comentario.Autor,
            texto = comentario.Texto,
            dataComentario = ParaDataHoraRegionalIso(comentario.DataComentario),
            podeEditar = comentario.UsuarioId == idUsuario.Value,
            podeExcluir = comentario.UsuarioId == idUsuario.Value,
            anexoUrl = string.IsNullOrWhiteSpace(comentario.AnexoComentario)
                ? null
                : Url.Page("/Menu/Home", "AnexoComentarioChamado", new
                {
                    grupoId = chamado.GrupoId,
                    chamadoId = chamado.Id,
                    comentarioId = comentario.Id
                })
        })
        .Reverse()
        .ToList();

        return new JsonResult(new { success = true, dados = new { chamadoId = chamado.Id, comentarios, pagina, temMais } });
    }

    public async Task<IActionResult> OnPostAdicionarComentarioChamadoAsync([FromForm] AdicionarComentarioChamadoRequest request)
    {
        var idUsuario = GetUsuarioLogadoId();
        if (idUsuario == null)
            return Unauthorized();

        if (request == null || request.ChamadoId <= 0)
            return new JsonResult(new { success = false, message = "Chamado inválido." });

        var mensagem = (request.Mensagem ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(mensagem) && request.AnexoImagem == null)
            return new JsonResult(new { success = false, message = "Informe um comentário ou selecione uma imagem." });

        if (mensagem.Length > LimiteCaracteresComentario)
            return new JsonResult(new { success = false, message = $"O comentário não pode exceder {LimiteCaracteresComentario} caracteres." });

        var acesso = await ObterChamadoComAcessoAsync(idUsuario.Value, GrupoId, request.ChamadoId);
        if (!acesso.Success)
            return new JsonResult(new { success = false, message = acesso.Message });
        if (!PodeAcessarComentariosChamado(acesso.ContextoMembro!.Permissao))
            return new JsonResult(new { success = false, message = "Você não tem permissão para comentar neste chamado." });

        string? nomeArquivo = null;
        string? caminhoArquivoSalvo = null;
        if (request.AnexoImagem != null && request.AnexoImagem.Length > 0)
        {
            if (request.AnexoImagem.Length > 5 * 1024 * 1024)
                return new JsonResult(new { success = false, message = "A imagem do comentário deve ter no máximo 5 MB." });

            var extensao = Path.GetExtension(request.AnexoImagem.FileName).ToLowerInvariant();
            var extensoesPermitidas = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
            if (!extensoesPermitidas.Contains(extensao))
                return new JsonResult(new { success = false, message = "Formato de imagem não permitido." });

            if (!await AssinaturaArquivoPermitidaAsync(request.AnexoImagem, extensao))
                return new JsonResult(new { success = false, message = "Arquivo de imagem inválido." });

            nomeArquivo = $"{Guid.NewGuid():N}{extensao}";
            var uploadsRoot = Path.Combine(_environment.ContentRootPath, "uploads_privados", "chamados", "comentarios");
            Directory.CreateDirectory(uploadsRoot);
            caminhoArquivoSalvo = Path.Combine(uploadsRoot, nomeArquivo);
            await using var stream = System.IO.File.Create(caminhoArquivoSalvo);
            await request.AnexoImagem.CopyToAsync(stream);
        }

        var strategy = _context.Database.CreateExecutionStrategy();

        try
        {
            return await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _context.Database.BeginTransactionAsync();

                try
                {
                    var usuario = await _context.Usuarios.FirstAsync(u => u.Id == idUsuario.Value);
                    var comentario = new ComentarioChamado
                    {
                        ChamadoId = acesso.Chamado!.Id,
                        Chamado = acesso.Chamado,
                        UsuarioId = idUsuario.Value,
                        Usuario = usuario,
                        Mensagem = string.IsNullOrWhiteSpace(mensagem) ? "Imagem anexada." : mensagem,
                        AnexoComentario = nomeArquivo,
                        DataComentario = DateTime.UtcNow
                    };

                    _context.ComentariosChamados.Add(comentario);
                    _context.HistoricoAlteracoesChamado.Add(new HistoricoAlteracaoChamado
                    {
                        ChamadoId = acesso.Chamado.Id,
                        GrupoId = acesso.Chamado.GrupoId,
                        UsuarioId = idUsuario.Value,
                        CampoAlterado = "Comentario",
                        ValorAlterado = nomeArquivo == null ? comentario.Mensagem : $"{comentario.Mensagem} [anexo]",
                        TipoAlteracao = "ComentarioChamado",
                        DataAlteracao = DateTime.UtcNow
                    });
                    await _notificacaoService.CriarNotificacoesComentarioChamadoAsync(acesso.Chamado, idUsuario.Value, ReferenciaTipoComentarioChamado);
                    await _context.SaveChangesAsync();
                    await _mencaoService.SincronizarMencoesAsync(
                        acesso.Chamado.GrupoId,
                        idUsuario.Value,
                        "ComentarioChamado",
                        comentario.Id,
                        "Comentario",
                        comentario.Mensagem,
                        TipoNotificacao.Chamado,
                        "Voce foi mencionado em um comentario",
                        $"comentario do chamado #{acesso.Chamado.NumeroChamadoGrupo}",
                        $"/Menu/Home?grupoId={acesso.Chamado.GrupoId}",
                        await ObterUsuariosPermitidosMencaoChamadoAsync(acesso.Chamado, acesso.Chamado.Publico));

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    return (IActionResult)new JsonResult(new
                    {
                        success = true,
                        dados = new
                        {
                            chamadoId = acesso.Chamado.Id,
                            comentarioId = comentario.Id,
                            message = "Comentário adicionado com sucesso."
                        }
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
            if (!string.IsNullOrWhiteSpace(caminhoArquivoSalvo) && System.IO.File.Exists(caminhoArquivoSalvo))
                System.IO.File.Delete(caminhoArquivoSalvo);

            _logger.LogError(ex, "Erro ao adicionar comentario ao chamado {ChamadoId}.", request.ChamadoId);
            return new JsonResult(new { success = false, message = "Não foi possível adicionar o comentário." });
        }
    }

    public async Task<IActionResult> OnPostEditarComentarioChamadoAsync([FromBody] EditarComentarioChamadoRequest request)
    {
        var idUsuario = GetUsuarioLogadoId();
        if (idUsuario == null)
            return Unauthorized();

        if (request == null || request.ChamadoId <= 0 || request.ComentarioId <= 0)
            return new JsonResult(new { success = false, message = "Comentário inválido." });

        var mensagem = (request.Mensagem ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(mensagem))
            return new JsonResult(new { success = false, message = "Informe o comentário." });
        if (mensagem.Length > LimiteCaracteresComentario)
            return new JsonResult(new { success = false, message = $"O comentário não pode exceder {LimiteCaracteresComentario} caracteres." });

        var acesso = await ObterChamadoComAcessoAsync(idUsuario.Value, GrupoId, request.ChamadoId);
        if (!acesso.Success)
            return new JsonResult(new { success = false, message = acesso.Message });
        if (!PodeAcessarComentariosChamado(acesso.ContextoMembro!.Permissao))
            return new JsonResult(new { success = false, message = "Você não tem permissão para acessar comentários." });

        var strategy = _context.Database.CreateExecutionStrategy();

        try
        {
            return await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _context.Database.BeginTransactionAsync();

                try
                {
                    var comentario = await _context.ComentariosChamados
                        .FirstOrDefaultAsync(c => c.Id == request.ComentarioId && c.ChamadoId == request.ChamadoId);

                    if (comentario == null)
                        return (IActionResult)new JsonResult(new { success = false, message = "Comentário não encontrado." });
                    if (comentario.UsuarioId != idUsuario.Value)
                        return (IActionResult)new JsonResult(new { success = false, message = "Você só pode editar comentários próprios." });

                    if (string.Equals(comentario.Mensagem, mensagem, StringComparison.Ordinal))
                        return (IActionResult)new JsonResult(new { success = true, dados = new { chamadoId = request.ChamadoId, message = "Nenhuma alteração feita." } });

                    var mensagemAnterior = comentario.Mensagem;
                    comentario.Mensagem = mensagem;
                    _context.HistoricoAlteracoesChamado.Add(new HistoricoAlteracaoChamado
                    {
                        ChamadoId = acesso.Chamado!.Id,
                        GrupoId = acesso.Chamado.GrupoId,
                        UsuarioId = idUsuario.Value,
                        CampoAlterado = "Comentario",
                        ValorAnterior = mensagemAnterior,
                        ValorAlterado = mensagem,
                        TipoAlteracao = "ComentarioEditado",
                        DataAlteracao = DateTime.UtcNow
                    });
                    await _mencaoService.SincronizarMencoesAsync(
                        acesso.Chamado!.GrupoId,
                        idUsuario.Value,
                        "ComentarioChamado",
                        comentario.Id,
                        "Comentario",
                        comentario.Mensagem,
                        TipoNotificacao.Chamado,
                        "Voce foi mencionado em um comentario",
                        $"comentario do chamado #{acesso.Chamado.NumeroChamadoGrupo}",
                        $"/Menu/Home?grupoId={acesso.Chamado.GrupoId}",
                        await ObterUsuariosPermitidosMencaoChamadoAsync(acesso.Chamado, acesso.Chamado.Publico));

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    return (IActionResult)new JsonResult(new { success = true, dados = new { chamadoId = request.ChamadoId, message = "Comentário atualizado com sucesso." } });
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
            _logger.LogError(ex, "Erro ao editar comentário {ComentarioId} do chamado {ChamadoId}.", request.ComentarioId, request.ChamadoId);
            return new JsonResult(new { success = false, message = "Não foi possível editar o comentário." });
        }
    }

    public async Task<IActionResult> OnPostExcluirComentarioChamadoAsync([FromBody] ExcluirComentarioChamadoRequest request)
    {
        var idUsuario = GetUsuarioLogadoId();
        if (idUsuario == null)
            return Unauthorized();

        if (request == null || request.ChamadoId <= 0 || request.ComentarioId <= 0)
            return new JsonResult(new { success = false, message = "Comentário inválido." });

        var acesso = await ObterChamadoComAcessoAsync(idUsuario.Value, GrupoId, request.ChamadoId);
        if (!acesso.Success)
            return new JsonResult(new { success = false, message = acesso.Message });
        if (!PodeAcessarComentariosChamado(acesso.ContextoMembro!.Permissao))
            return new JsonResult(new { success = false, message = "Você não tem permissão para acessar comentários." });

        var strategy = _context.Database.CreateExecutionStrategy();
        string? anexoRemover = null;

        try
        {
            var resultado = await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _context.Database.BeginTransactionAsync();

                try
                {
                    var comentario = await _context.ComentariosChamados
                        .FirstOrDefaultAsync(c => c.Id == request.ComentarioId && c.ChamadoId == request.ChamadoId);

                    if (comentario == null)
                        return (IActionResult)new JsonResult(new { success = false, message = "Comentário não encontrado." });
                    if (comentario.UsuarioId != idUsuario.Value)
                        return (IActionResult)new JsonResult(new { success = false, message = "Você só pode excluir comentários próprios." });

                    anexoRemover = comentario.AnexoComentario;
                    await _mencaoService.SincronizarMencoesAsync(
                        acesso.Chamado!.GrupoId,
                        idUsuario.Value,
                        "ComentarioChamado",
                        comentario.Id,
                        "Comentario",
                        null,
                        TipoNotificacao.Chamado,
                        "Voce foi mencionado em um comentario",
                        $"comentario do chamado #{acesso.Chamado.NumeroChamadoGrupo}",
                        $"/Menu/Home?grupoId={acesso.Chamado.GrupoId}",
                        await ObterUsuariosPermitidosMencaoChamadoAsync(acesso.Chamado, acesso.Chamado.Publico));
                    _context.ComentariosChamados.Remove(comentario);
                    _context.HistoricoAlteracoesChamado.Add(new HistoricoAlteracaoChamado
                    {
                        ChamadoId = acesso.Chamado!.Id,
                        GrupoId = acesso.Chamado.GrupoId,
                        UsuarioId = idUsuario.Value,
                        CampoAlterado = "Comentario",
                        ValorAnterior = comentario.Mensagem,
                        TipoAlteracao = "ComentarioExcluido",
                        DataAlteracao = DateTime.UtcNow
                    });

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    return (IActionResult)new JsonResult(new { success = true, dados = new { chamadoId = request.ChamadoId, message = "Comentário excluído com sucesso." } });
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            });

            RemoverAnexoComentarioSeExistir(anexoRemover);
            return resultado;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao excluir comentário {ComentarioId} do chamado {ChamadoId}.", request.ComentarioId, request.ChamadoId);
            return new JsonResult(new { success = false, message = "Não foi possível excluir o comentário." });
        }
    }

    public async Task<IActionResult> OnGetAnexoComentarioChamadoAsync(int chamadoId, int comentarioId)
    {
        var idUsuario = GetUsuarioLogadoId();
        if (idUsuario == null)
            return Unauthorized();

        var acesso = await ObterChamadoComAcessoAsync(idUsuario.Value, GrupoId, chamadoId);
        if (!acesso.Success)
            return Forbid();
        if (!PodeAcessarComentariosChamado(acesso.ContextoMembro!.Permissao))
            return Forbid();

        var comentario = await _context.ComentariosChamados
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == comentarioId && c.ChamadoId == chamadoId);

        if (comentario == null || string.IsNullOrWhiteSpace(comentario.AnexoComentario))
            return NotFound();

        var caminho = Path.Combine(_environment.ContentRootPath, "uploads_privados", "chamados", "comentarios", comentario.AnexoComentario);
        if (!System.IO.File.Exists(caminho))
            return NotFound();

        return PhysicalFile(caminho, ObterContentTypeAnexo(comentario.AnexoComentario));
    }

    public async Task<IActionResult> OnPostMarcarComentariosVisualizadosAsync([FromBody] MarcarComentariosVisualizadosRequest request)
    {
        var idUsuario = GetUsuarioLogadoId();
        if (idUsuario == null)
            return Unauthorized();

        if (request == null || request.ChamadoId <= 0)
            return new JsonResult(new { success = false, message = "Chamado inválido." });

        var acesso = await ObterChamadoComAcessoAsync(idUsuario.Value, GrupoId, request.ChamadoId);
        if (!acesso.Success)
            return new JsonResult(new { success = false, message = acesso.Message });
        if (!PodeAcessarComentariosChamado(acesso.ContextoMembro!.Permissao))
            return new JsonResult(new { success = false, message = "Você não tem permissão para acessar comentários." });

        var strategy = _context.Database.CreateExecutionStrategy();

        try
        {
            return await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _context.Database.BeginTransactionAsync();

                try
                {
                    var notificacoes = await _context.Notificacoes
                        .Where(n => n.UsuarioId == idUsuario.Value &&
                                    n.GrupoId == GrupoId &&
                                    !n.Lida &&
                                    n.Tipo == TipoNotificacao.Chamado &&
                                    (n.ReferenciaTipo == ReferenciaTipoComentarioChamado ||
                                     n.ReferenciaTipo == ReferenciaTipoComentarioHistorico) &&
                                    n.ReferenciaId == request.ChamadoId)
                        .ToListAsync();

                    var agora = DateTime.UtcNow;
                    foreach (var notificacao in notificacoes)
                    {
                        notificacao.Lida = true;
                        notificacao.DataLeitura = agora;
                    }

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    return (IActionResult)new JsonResult(new
                    {
                        success = true,
                        dados = new { chamadoId = request.ChamadoId }
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
            _logger.LogError(ex, "Erro ao marcar comentarios visualizados do chamado {ChamadoId}.", request.ChamadoId);
            return new JsonResult(new { success = false, message = "Não foi possível atualizar a visualização dos comentários." });
        }
    }

    public async Task<IActionResult> OnGetVinculosChamadoAsync(int chamadoId)
    {
        var idUsuario = GetUsuarioLogadoId();
        if (idUsuario == null)
            return Unauthorized();

        var acesso = await ObterChamadoComAcessoAsync(idUsuario.Value, GrupoId, chamadoId);
        if (!acesso.Success)
            return new JsonResult(new { success = false, message = acesso.Message });
        if (!PodeAcessarVinculosChamado(acesso.ContextoMembro!.Permissao))
            return new JsonResult(new { success = false, message = "Você não tem permissão para acessar vínculos." });

        var vinculos = await ObterVinculosVisiveisAsync(idUsuario.Value, GrupoId, chamadoId, acesso.ContextoMembro!.Permissao);
        return new JsonResult(new { success = true, dados = new { chamadoId, vinculos } });
    }

    public async Task<IActionResult> OnGetOpcoesVinculoChamadoAsync(int chamadoId)
    {
        var idUsuario = GetUsuarioLogadoId();
        if (idUsuario == null)
            return Unauthorized();

        var acesso = await ObterChamadoComAcessoAsync(idUsuario.Value, GrupoId, chamadoId);
        if (!acesso.Success)
            return new JsonResult(new { success = false, message = acesso.Message });
        if (!PodeAcessarVinculosChamado(acesso.ContextoMembro!.Permissao))
            return new JsonResult(new { success = false, message = "Você não tem permissão para acessar vínculos." });

        var idsVinculados = await _context.ChamadosVinculos
            .AsNoTracking()
            .Where(v => v.GrupoId == GrupoId && (v.ChamadoIdMenor == chamadoId || v.ChamadoIdMaior == chamadoId))
            .Select(v => v.ChamadoIdMenor == chamadoId ? v.ChamadoIdMaior : v.ChamadoIdMenor)
            .ToListAsync();

        var vinculadosSet = idsVinculados.ToHashSet();
        var chamados = await ConstruirQueryChamadosPermitidos(idUsuario.Value, GrupoId, acesso.ContextoMembro!.Permissao)
            .Where(c => c.Id != chamadoId && !StatusBloqueadosVinculo.Contains(c.Status))
            .OrderByDescending(c => c.DataCriacao)
            .ThenBy(c => c.Id)
            .Select(c => new
            {
                c.Id,
                c.NumeroChamadoGrupo,
                c.Titulo,
                c.Status
            })
            .ToListAsync();

        return new JsonResult(new
        {
            success = true,
            dados = chamados.Select(c => new
            {
                c.Id,
                c.NumeroChamadoGrupo,
                titulo = c.Titulo,
                status = FormatarStatusChamado(c.Status),
                vinculado = vinculadosSet.Contains(c.Id)
            })
        });
    }

    public async Task<IActionResult> OnGetCandidatosVinculoChamadoAsync(int chamadoId, string? termo)
    {
        var idUsuario = GetUsuarioLogadoId();
        if (idUsuario == null)
            return Unauthorized();

        var termoNormalizado = (termo ?? string.Empty).Trim();
        if (termoNormalizado.Length < TamanhoMinimoPesquisaVinculo)
            return new JsonResult(new { success = true, dados = Array.Empty<object>() });

        var acesso = await ObterChamadoComAcessoAsync(idUsuario.Value, GrupoId, chamadoId);
        if (!acesso.Success)
            return new JsonResult(new { success = false, message = acesso.Message });
        if (!PodeAcessarVinculosChamado(acesso.ContextoMembro!.Permissao))
            return new JsonResult(new { success = false, message = "Você não tem permissão para acessar vínculos." });

        var query = ConstruirQueryChamadosPermitidos(idUsuario.Value, GrupoId, acesso.ContextoMembro!.Permissao)
            .Where(c => c.Id != chamadoId && !StatusBloqueadosVinculo.Contains(c.Status));

        var padrao = $"%{EscaparLike(termoNormalizado)}%";
        query = query.Where(c =>
            EF.Functions.Like(c.Titulo ?? string.Empty, padrao, "\\") ||
            EF.Functions.Like(c.Descricao ?? string.Empty, padrao, "\\") ||
            EF.Functions.Like(c.Solucao ?? string.Empty, padrao, "\\"));

        var candidatos = await query
            .OrderByDescending(c => c.DataCriacao)
            .ThenBy(c => c.Id)
            .Select(c => new
            {
                c.Id,
                c.NumeroChamadoGrupo,
                c.Titulo,
                c.Status
            })
            .Take(LimiteCandidatosVinculo)
            .ToListAsync();

        return new JsonResult(new
        {
            success = true,
            dados = candidatos.Select(c => new
            {
                c.Id,
                c.NumeroChamadoGrupo,
                titulo = c.Titulo,
                status = FormatarStatusChamado(c.Status)
            })
        });
    }

    public async Task<IActionResult> OnPostVincularChamadoAsync([FromBody] VinculoChamadoRequest request)
    {
        var idUsuario = GetUsuarioLogadoId();
        if (idUsuario == null)
            return Unauthorized();

        if (request == null || request.ChamadoId <= 0 || request.ChamadoVinculadoId <= 0)
            return new JsonResult(new { success = false, message = "Chamados inválidos para vínculo." });

        if (request.ChamadoId == request.ChamadoVinculadoId)
            return new JsonResult(new { success = false, message = "Um chamado não pode ser vinculado a ele mesmo." });

        var acessoOrigem = await ObterChamadoComAcessoAsync(idUsuario.Value, GrupoId, request.ChamadoId);
        if (!acessoOrigem.Success)
            return new JsonResult(new { success = false, message = acessoOrigem.Message });
        if (!PodeAcessarVinculosChamado(acessoOrigem.ContextoMembro!.Permissao))
            return new JsonResult(new { success = false, message = "Você não tem permissão para vincular chamados." });

        var acessoDestino = await ObterChamadoComAcessoAsync(idUsuario.Value, GrupoId, request.ChamadoVinculadoId);
        if (!acessoDestino.Success || StatusBloqueadosVinculo.Contains(acessoDestino.Chamado!.Status))
            return new JsonResult(new { success = false, message = "Você não tem permissão para vincular este chamado." });

        var menor = Math.Min(request.ChamadoId, request.ChamadoVinculadoId);
        var maior = Math.Max(request.ChamadoId, request.ChamadoVinculadoId);
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
                        var existe = await _context.ChamadosVinculos
                            .AsNoTracking()
                            .AnyAsync(v => v.ChamadoIdMenor == menor && v.ChamadoIdMaior == maior);

                        if (!existe)
                        {
                            _context.ChamadosVinculos.Add(new ChamadoVinculo
                            {
                                ChamadoIdMenor = menor,
                                ChamadoIdMaior = maior,
                                GrupoId = GrupoId,
                                DataVinculo = DateTime.UtcNow,
                                VinculadoPorUsuarioId = idUsuario.Value
                            });

                            await _context.SaveChangesAsync();
                        }

                        await transaction.CommitAsync();
                        var vinculos = await ObterVinculosVisiveisAsync(idUsuario.Value, GrupoId, request.ChamadoId, acessoOrigem.ContextoMembro!.Permissao);

                        return (IActionResult)new JsonResult(new
                        {
                            success = true,
                            dados = new
                            {
                                chamadoId = request.ChamadoId,
                                vinculos,
                                message = existe ? "Este chamado já estava vinculado." : "Chamado vinculado com sucesso."
                            }
                        });
                    }
                    catch
                    {
                        await transaction.RollbackAsync();
                        throw;
                    }
                });
            }
            catch (DbUpdateException ex) when (tentativa < maxTentativas && EhErroDuplicidade(ex))
            {
                _context.ChangeTracker.Clear();
            }
            catch (DbUpdateException ex) when (EhErroDuplicidade(ex))
            {
                _context.ChangeTracker.Clear();
                var vinculos = await ObterVinculosVisiveisAsync(idUsuario.Value, GrupoId, request.ChamadoId, acessoOrigem.ContextoMembro!.Permissao);
                return new JsonResult(new
                {
                    success = true,
                    dados = new
                    {
                        chamadoId = request.ChamadoId,
                        vinculos,
                        message = "Este chamado já estava vinculado."
                    }
                });
            }
        }

        return new JsonResult(new { success = false, message = "Não foi possível vincular o chamado." });
    }

    public async Task<IActionResult> OnPostRemoverVinculoChamadoAsync([FromBody] VinculoChamadoRequest request)
    {
        var idUsuario = GetUsuarioLogadoId();
        if (idUsuario == null)
            return Unauthorized();

        if (request == null || request.ChamadoId <= 0 || request.ChamadoVinculadoId <= 0)
            return new JsonResult(new { success = false, message = "Vínculo inválido." });

        var acessoOrigem = await ObterChamadoComAcessoAsync(idUsuario.Value, GrupoId, request.ChamadoId);
        if (!acessoOrigem.Success)
            return new JsonResult(new { success = false, message = acessoOrigem.Message });
        if (!PodeAcessarVinculosChamado(acessoOrigem.ContextoMembro!.Permissao))
            return new JsonResult(new { success = false, message = "Você não tem permissão para remover vínculos." });

        var acessoDestino = await ObterChamadoComAcessoAsync(idUsuario.Value, GrupoId, request.ChamadoVinculadoId);
        if (!acessoDestino.Success)
            return new JsonResult(new { success = false, message = "Você não tem permissão para remover este vínculo." });

        var menor = Math.Min(request.ChamadoId, request.ChamadoVinculadoId);
        var maior = Math.Max(request.ChamadoId, request.ChamadoVinculadoId);
        var strategy = _context.Database.CreateExecutionStrategy();

        try
        {
            return await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _context.Database.BeginTransactionAsync();

                try
                {
                    var vinculo = await _context.ChamadosVinculos
                        .FirstOrDefaultAsync(v => v.ChamadoIdMenor == menor && v.ChamadoIdMaior == maior && v.GrupoId == GrupoId);

                    if (vinculo != null)
                    {
                        _context.ChamadosVinculos.Remove(vinculo);
                        await _context.SaveChangesAsync();
                    }

                    await transaction.CommitAsync();
                    var vinculos = await ObterVinculosVisiveisAsync(idUsuario.Value, GrupoId, request.ChamadoId, acessoOrigem.ContextoMembro!.Permissao);

                    return (IActionResult)new JsonResult(new
                    {
                        success = true,
                        dados = new
                        {
                            chamadoId = request.ChamadoId,
                            vinculos,
                            message = "Vínculo removido com sucesso."
                        }
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
            _logger.LogError(ex, "Erro ao remover vínculo do chamado {ChamadoId}.", request.ChamadoId);
            return new JsonResult(new { success = false, message = "Não foi possível remover o vínculo." });
        }
    }

    public async Task<IActionResult> OnPostSalvarVinculosChamadoAsync([FromBody] SalvarVinculosChamadoRequest request)
    {
        var idUsuario = GetUsuarioLogadoId();
        if (idUsuario == null)
            return Unauthorized();

        if (request == null || request.ChamadoId <= 0)
            return new JsonResult(new { success = false, message = "Chamado inválido para vínculos." });

        var chamadosIds = request.ChamadosIds?
            .Where(id => id > 0)
            .Distinct()
            .ToList() ?? new List<int>();

        if (chamadosIds.Contains(request.ChamadoId))
            return new JsonResult(new { success = false, message = "Um chamado não pode ser vinculado a ele mesmo." });

        var acessoOrigem = await ObterChamadoComAcessoAsync(idUsuario.Value, GrupoId, request.ChamadoId);
        if (!acessoOrigem.Success)
            return new JsonResult(new { success = false, message = acessoOrigem.Message });
        if (!PodeAcessarVinculosChamado(acessoOrigem.ContextoMembro!.Permissao))
            return new JsonResult(new { success = false, message = "Você não tem permissão para salvar vínculos." });

        var idsGerenciaveis = await ConstruirQueryChamadosPermitidos(idUsuario.Value, GrupoId, acessoOrigem.ContextoMembro!.Permissao)
            .Where(c => c.Id != request.ChamadoId && !StatusBloqueadosVinculo.Contains(c.Status))
            .Select(c => c.Id)
            .ToListAsync();

        var idsGerenciaveisSet = idsGerenciaveis.ToHashSet();
        if (chamadosIds.Any(id => !idsGerenciaveisSet.Contains(id)))
            return new JsonResult(new { success = false, message = "Um ou mais chamados não podem ser vinculados." });

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
                        var atuais = await _context.ChamadosVinculos
                            .Where(v => v.GrupoId == GrupoId &&
                                        (v.ChamadoIdMenor == request.ChamadoId || v.ChamadoIdMaior == request.ChamadoId))
                            .ToListAsync();

                        var desejados = chamadosIds.ToHashSet();
                        var atuaisPorOutroId = atuais.ToDictionary(
                            v => v.ChamadoIdMenor == request.ChamadoId ? v.ChamadoIdMaior : v.ChamadoIdMenor,
                            v => v);

                        _context.ChamadosVinculos.RemoveRange(
                            atuaisPorOutroId
                                .Where(par => idsGerenciaveisSet.Contains(par.Key) && !desejados.Contains(par.Key))
                                .Select(par => par.Value));

                        foreach (var chamadoVinculadoId in desejados.Where(id => !atuaisPorOutroId.ContainsKey(id)))
                        {
                            var menor = Math.Min(request.ChamadoId, chamadoVinculadoId);
                            var maior = Math.Max(request.ChamadoId, chamadoVinculadoId);

                            _context.ChamadosVinculos.Add(new ChamadoVinculo
                            {
                                ChamadoIdMenor = menor,
                                ChamadoIdMaior = maior,
                                GrupoId = GrupoId,
                                DataVinculo = DateTime.UtcNow,
                                VinculadoPorUsuarioId = idUsuario.Value
                            });
                        }

                        await _context.SaveChangesAsync();
                        await transaction.CommitAsync();

                        var vinculos = await ObterVinculosVisiveisAsync(idUsuario.Value, GrupoId, request.ChamadoId, acessoOrigem.ContextoMembro!.Permissao);
                        return (IActionResult)new JsonResult(new
                        {
                            success = true,
                            dados = new
                            {
                                chamadoId = request.ChamadoId,
                                vinculos,
                                message = "Vínculos atualizados com sucesso."
                            }
                        });
                    }
                    catch
                    {
                        await transaction.RollbackAsync();
                        throw;
                    }
                });
            }
            catch (DbUpdateException ex) when (tentativa < maxTentativas && EhErroDuplicidade(ex))
            {
                _context.ChangeTracker.Clear();
            }
            catch (DbUpdateException ex) when (EhErroDuplicidade(ex))
            {
                _context.ChangeTracker.Clear();
                var vinculos = await ObterVinculosVisiveisAsync(idUsuario.Value, GrupoId, request.ChamadoId, acessoOrigem.ContextoMembro!.Permissao);
                return new JsonResult(new
                {
                    success = true,
                    dados = new
                    {
                        chamadoId = request.ChamadoId,
                        vinculos,
                        message = "Vínculos atualizados com sucesso."
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao salvar vínculos do chamado {ChamadoId}.", request.ChamadoId);
                return new JsonResult(new { success = false, message = "Não foi possível salvar os vínculos." });
            }
        }

        return new JsonResult(new { success = false, message = "Não foi possível salvar os vínculos." });
    }

    private async Task<ChamadoAcessoResultado> ObterChamadoComAcessoAsync(int usuarioId, int grupoId, int chamadoId)
    {
        if (grupoId <= 0 || chamadoId <= 0)
            return ChamadoAcessoResultado.Fail("Parâmetros inválidos.");

        var chamado = await _context.Chamados
            .FirstOrDefaultAsync(c => c.Id == chamadoId && c.GrupoId == grupoId && c.Status != StatusChamado.Excluido);

        if (chamado == null)
            return ChamadoAcessoResultado.Fail("Chamado não encontrado.");

        var contextoMembro = await _grupoAuthorizationService.ObterContextoMembroAsync(usuarioId, grupoId);
        if (contextoMembro == null)
            return ChamadoAcessoResultado.Fail("Você não pertence a este grupo.");

        if (!GrupoPermissionService.PodeVerChamado(contextoMembro.Permissao, chamado.Publico, usuarioId, chamado.CriadorChamadoId))
            return ChamadoAcessoResultado.Fail("Você não tem permissão para visualizar este chamado.");

        return ChamadoAcessoResultado.Ok(chamado, contextoMembro);
    }

    private IQueryable<Chamado> ConstruirQueryChamadosPermitidos(int usuarioId, int grupoId, PermissaoUsuario permissao)
    {
        var query = _context.Chamados
            .AsNoTracking()
            .Where(c => c.GrupoId == grupoId && c.Status != StatusChamado.Excluido);

        if (permissao == PermissaoUsuario.Colaborador)
        {
            query = query.Where(c => c.Publico || c.CriadorChamadoId == usuarioId);
        }
        else if (permissao == PermissaoUsuario.Nenhuma)
        {
            query = query.Where(c => c.Publico);
        }

        return query;
    }

    private static bool PodeAcessarVinculosChamado(PermissaoUsuario permissao) =>
        permissao is PermissaoUsuario.Administracao or PermissaoUsuario.Tecnico;

    private static bool PodeAcessarComentariosChamado(PermissaoUsuario permissao) =>
        permissao != PermissaoUsuario.Nenhuma;

    private async Task<List<int>> ObterUsuariosPermitidosMencaoChamadoAsync(Chamado chamado, bool chamadoPublico)
    {
        var membros = await _context.UsuariosGrupos
            .AsNoTracking()
            .Where(ug => ug.GrupoId == chamado.GrupoId && ug.Ativo)
            .Select(ug => new { ug.UsuarioId, ug.Permissao })
            .ToListAsync();

        return membros
            .Where(membro => GrupoPermissionService.PodeVerChamado(
                membro.Permissao,
                chamadoPublico,
                membro.UsuarioId,
                chamado.CriadorChamadoId))
            .Select(membro => membro.UsuarioId)
            .ToList();
    }

    private async Task<List<ChamadoVinculoDto>> ObterVinculosVisiveisAsync(int usuarioId, int grupoId, int chamadoId, PermissaoUsuario permissao)
    {
        var idsVinculados = await _context.ChamadosVinculos
            .AsNoTracking()
            .Where(v => v.GrupoId == grupoId && (v.ChamadoIdMenor == chamadoId || v.ChamadoIdMaior == chamadoId))
            .Select(v => v.ChamadoIdMenor == chamadoId ? v.ChamadoIdMaior : v.ChamadoIdMenor)
            .ToListAsync();

        if (idsVinculados.Count == 0)
            return new List<ChamadoVinculoDto>();

        var vinculos = await ConstruirQueryChamadosPermitidos(usuarioId, grupoId, permissao)
            .Where(c => idsVinculados.Contains(c.Id))
            .OrderByDescending(c => c.DataCriacao)
            .ThenBy(c => c.Id)
            .Select(c => new
            {
                c.Id,
                c.NumeroChamadoGrupo,
                c.Titulo,
                c.Status,
                c.DataCriacao
            })
            .ToListAsync();

        return vinculos.Select(v => new ChamadoVinculoDto
            {
                Id = v.Id,
                NumeroChamadoGrupo = v.NumeroChamadoGrupo,
                Titulo = v.Titulo,
                Status = FormatarStatusChamado(v.Status),
                DataCriacao = ParaDataHoraRegionalIso(v.DataCriacao)
            })
            .ToList();
    }

    private static string EscaparLike(string valor) =>
        valor
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);

    private static string FormatarStatusChamado(StatusChamado status) => status switch
    {
        StatusChamado.EmAndamento => "Em andamento",
        StatusChamado.Concluido => "Concluido",
        _ => status.ToString()
    };

    private void RegistrarTransicaoStatusChamado(
        Chamado chamado,
        StatusChamado statusAnterior,
        StatusChamado statusNovo,
        int? usuarioId,
        bool origemAutomatica,
        string? descricaoOrigem)
    {
        _context.HistoricoStatusChamados.Add(new HistoricoStatusChamado
        {
            ChamadoId = chamado.Id,
            StatusAnterior = ConverterStatusAnterior(statusAnterior),
            StatusNovo = ConverterStatusNovo(statusNovo),
            UsuarioId = usuarioId,
            OrigemAutomatica = origemAutomatica,
            DescricaoOrigem = descricaoOrigem,
            DataTransicao = DateTime.UtcNow
        });
    }

    private void RegistrarHistoricoAlteracaoStatus(
        Chamado chamado,
        StatusChamado statusAnterior,
        StatusChamado statusNovo,
        int usuarioId,
        string tipoAlteracao)
    {
        _context.HistoricoAlteracoesChamado.Add(new HistoricoAlteracaoChamado
        {
            ChamadoId = chamado.Id,
            GrupoId = chamado.GrupoId,
            UsuarioId = usuarioId,
            CampoAlterado = "Status",
            ValorAnterior = statusAnterior.ToString(),
            ValorAlterado = statusNovo.ToString(),
            TipoAlteracao = tipoAlteracao,
            DataAlteracao = DateTime.UtcNow
        });
    }

    private static StatusAnteriorChamado ConverterStatusAnterior(StatusChamado status) =>
        Enum.Parse<StatusAnteriorChamado>(status.ToString());

    private static StatusNovoChamado ConverterStatusNovo(StatusChamado status) =>
        Enum.Parse<StatusNovoChamado>(status.ToString());

    private int? GetUsuarioLogadoId()
    {
        var claim = User.FindFirst("Id")?.Value;
        return int.TryParse(claim, out var id) ? id : null;
    }

    private async Task AtualizarUltimoAcessoGrupoAsync(int usuarioId, int grupoId)
    {
        var strategy = _context.Database.CreateExecutionStrategy();

        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var vinculo = await _context.UsuariosGrupos
                    .FirstOrDefaultAsync(ug => ug.UsuarioId == usuarioId && ug.GrupoId == grupoId && ug.Ativo);

                if (vinculo != null)
                {
                    vinculo.DataUltimoAcesso = DateTime.UtcNow;
                    await _context.SaveChangesAsync();
                }

                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        });
    }

    private static string ObterContentTypeAnexo(string nomeArquivo)
    {
        return Path.GetExtension(nomeArquivo).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".pdf" => "application/pdf",
            _ => "application/octet-stream"
        };
    }

    private void RemoverAnexoComentarioSeExistir(string? nomeArquivo)
    {
        if (string.IsNullOrWhiteSpace(nomeArquivo))
            return;

        var caminho = Path.Combine(_environment.ContentRootPath, "uploads_privados", "chamados", "comentarios", nomeArquivo);
        if (!System.IO.File.Exists(caminho))
            return;

        try
        {
            System.IO.File.Delete(caminho);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Não foi possível remover o anexo do comentário {Arquivo}.", nomeArquivo);
        }
    }

    private static async Task<bool> AssinaturaArquivoPermitidaAsync(IFormFile arquivo, string extensao)
    {
        var buffer = new byte[12];
        await using var stream = arquivo.OpenReadStream();
        var bytesLidos = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length));

        return extensao switch
        {
            ".jpg" or ".jpeg" => bytesLidos >= 3 &&
                                 buffer[0] == 0xFF &&
                                 buffer[1] == 0xD8 &&
                                 buffer[2] == 0xFF,
            ".png" => bytesLidos >= 8 &&
                      buffer[0] == 0x89 &&
                      buffer[1] == 0x50 &&
                      buffer[2] == 0x4E &&
                      buffer[3] == 0x47 &&
                      buffer[4] == 0x0D &&
                      buffer[5] == 0x0A &&
                      buffer[6] == 0x1A &&
                      buffer[7] == 0x0A,
            ".pdf" => bytesLidos >= 4 &&
                      buffer[0] == 0x25 &&
                      buffer[1] == 0x50 &&
                      buffer[2] == 0x44 &&
                      buffer[3] == 0x46,
            ".gif" => bytesLidos >= 6 &&
                      buffer[0] == 0x47 &&
                      buffer[1] == 0x49 &&
                      buffer[2] == 0x46 &&
                      buffer[3] == 0x38 &&
                      (buffer[4] == 0x37 || buffer[4] == 0x39) &&
                      buffer[5] == 0x61,
            ".webp" => bytesLidos >= 12 &&
                       buffer[0] == 0x52 &&
                       buffer[1] == 0x49 &&
                       buffer[2] == 0x46 &&
                       buffer[3] == 0x46 &&
                       buffer[8] == 0x57 &&
                       buffer[9] == 0x45 &&
                       buffer[10] == 0x42 &&
                       buffer[11] == 0x50,
            _ => false
        };
    }

    public async Task<IActionResult> OnGetCategoriasPorTipoAsync(int grupoId, int tipoId)
    {
        var idUsuario = GetUsuarioLogadoId();
        if (idUsuario == null)
            return new JsonResult(new { success = false, message = "Usuário não autenticado." });

        if (grupoId <= 0 || tipoId <= 0)
            return new JsonResult(new { success = false, message = "Parâmetros inválidos." });

        var contextoMembro = await _grupoAuthorizationService.ObterContextoMembroAsync(idUsuario.Value, grupoId);
        if (contextoMembro == null)
            return new JsonResult(new { success = false, message = "Você não pertence a este grupo." });

        var tipoValido = await _context.OcorrenciasTipo
            .AsNoTracking()
            .AnyAsync(t => t.Id == tipoId && t.GrupoId == grupoId);

        if (!tipoValido)
            return new JsonResult(new { success = false, message = "Tipo de ocorrência inválido." });

        var categorias = await ObterCategoriasPorTipoAsync(tipoId);

        return new JsonResult(new { success = true, categorias });
    }

    public async Task<IActionResult> OnGetSubcategoriasPorCategoriaAsync(int grupoId, int categoriaId)
    {
        var idUsuario = GetUsuarioLogadoId();
        if (idUsuario == null)
            return new JsonResult(new { success = false, message = "Usuário não autenticado." });

        if (grupoId <= 0 || categoriaId <= 0)
            return new JsonResult(new { success = false, message = "Parâmetros inválidos." });

        var contextoMembro = await _grupoAuthorizationService.ObterContextoMembroAsync(idUsuario.Value, grupoId);
        if (contextoMembro == null)
            return new JsonResult(new { success = false, message = "Você não pertence a este grupo." });

        var categoriaValida = await (
            from categoria in _context.OcorrenciasCategoria.AsNoTracking()
            join tipo in _context.OcorrenciasTipo.AsNoTracking()
                on categoria.TipoId equals tipo.Id
            where categoria.Id == categoriaId && tipo.GrupoId == grupoId
            select categoria.Id
        ).AnyAsync();

        if (!categoriaValida)
            return new JsonResult(new { success = false, message = "Categoria invalida." });

        var subcategorias = await ObterSubcategoriasPorCategoriaAsync(categoriaId);

        return new JsonResult(new { success = true, subcategorias });
    }

    private Task<List<Setor>> ObterSetoresDisponiveisAsync(int grupoId) =>
        _context.Setores
            .AsNoTracking()
            .Where(s => s.GrupoId == grupoId)
            .OrderBy(s => s.NomeSetor)
            .ToListAsync();

    private Task<List<OcorrenciaTipo>> ObterTiposOcorrenciaDisponiveisAsync(int grupoId) =>
        _context.OcorrenciasTipo
            .AsNoTracking()
            .Where(t => t.GrupoId == grupoId)
            .OrderBy(t => t.TipoOcorrencia)
            .ToListAsync();

    private Task<List<object>> ObterCategoriasPorTipoAsync(int tipoId) =>
        _context.OcorrenciasCategoria
            .AsNoTracking()
            .Where(c => c.TipoId == tipoId)
            .OrderBy(c => c.CategoriaOcorrencia)
            .Select(c => (object)new { id = c.Id, nome = c.CategoriaOcorrencia })
            .ToListAsync();

    private Task<List<object>> ObterSubcategoriasPorCategoriaAsync(int categoriaId) =>
        _context.OcorrenciasSubcategoria
            .AsNoTracking()
            .Where(sc => sc.CategoriaId == categoriaId)
            .OrderBy(sc => sc.SubcategoriaOcorrencia)
            .Select(sc => (object)new { id = sc.Id, nome = sc.SubcategoriaOcorrencia })
            .ToListAsync();

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

    private static bool TryNormalizarDataHoraNullable(string? valor, out DateTime? resultado)
    {
        resultado = null;

        if (string.IsNullOrWhiteSpace(valor))
            return true;

        var texto = valor.Trim();
        if (texto.EndsWith(" --:--", StringComparison.Ordinal))
            texto = texto.Replace(" --:--", " 00:00", StringComparison.Ordinal);

        if (texto.Length == 10 &&
            DateTime.TryParseExact(texto, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var somenteData))
        {
            resultado = DeDataHoraRegionalParaUtc(somenteData.Date);
            return true;
        }

        if (texto.EndsWith("T", StringComparison.Ordinal))
            texto += "00:00";
        else if (texto.Length == 13 &&
                 DateTime.TryParseExact(texto, "yyyy-MM-dd'T'HH", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dataHoraSemMinuto))
        {
            resultado = DeDataHoraRegionalParaUtc(dataHoraSemMinuto);
            return true;
        }

        var formatos = new[]
        {
            "yyyy-MM-dd'T'HH:mm",
            "yyyy-MM-dd'T'HH:mm:ss",
            "yyyy-MM-dd HH:mm",
            "yyyy-MM-dd HH:mm:ss",
            "dd/MM/yyyy HH:mm",
            "dd/MM/yyyy HH:mm:ss",
            "dd/MM/yyyy"
        };

        if (DateTime.TryParseExact(texto, formatos, CultureInfo.GetCultureInfo("pt-BR"), DateTimeStyles.None, out var dataHora))
        {
            resultado = DeDataHoraRegionalParaUtc(dataHora);
            return true;
        }

        if (DateTime.TryParse(texto, CultureInfo.GetCultureInfo("pt-BR"), DateTimeStyles.None, out dataHora))
        {
            resultado = DeDataHoraRegionalParaUtc(dataHora);
            return true;
        }

        return false;
    }

    private static string? ParaDataHoraRegionalIso(DateTime? dataUtc) =>
        dataUtc.HasValue ? ParaDataHoraRegionalIso(dataUtc.Value) : null;

    private static string ParaDataHoraRegionalIso(DateTime dataUtc)
    {
        var utc = DateTime.SpecifyKind(dataUtc, DateTimeKind.Utc);
        var regional = TimeZoneInfo.ConvertTimeFromUtc(utc, FusoHorarioRegional);
        return regional.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture);
    }

    private static DateTime DeDataHoraRegionalParaUtc(DateTime dataRegional)
    {
        var localRegional = DateTime.SpecifyKind(dataRegional, DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(localRegional, FusoHorarioRegional);
    }

    private static TimeZoneInfo ObterFusoHorarioRegional()
    {
        foreach (var id in new[] { "E. South America Standard Time", "America/Sao_Paulo" })
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(id);
            }
            catch (TimeZoneNotFoundException)
            {
            }
            catch (InvalidTimeZoneException)
            {
            }
        }

        return TimeZoneInfo.Local;
    }

    private static bool EhErroDuplicidade(DbUpdateException ex)
    {
        var mensagem = ex.InnerException?.Message ?? ex.Message;
        return mensagem.Contains("Duplicate", StringComparison.OrdinalIgnoreCase) ||
               mensagem.Contains("duplicate", StringComparison.OrdinalIgnoreCase) ||
               mensagem.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<QuadroTarefa> GarantirQuadroTarefasPadraoAsync(int grupoId, int usuarioId)
    {
        var quadro = await _context.QuadrosTarefas
            .FirstOrDefaultAsync(q => q.GrupoId == grupoId && q.Ativo);

        if (quadro != null)
            return quadro;

        if (_context.Database.CurrentTransaction != null)
        {
            try
            {
                quadro = new QuadroTarefa
                {
                    GrupoId = grupoId,
                    Nome = "Tarefas",
                    Descricao = "Quadro principal de tarefas",
                    Ativo = true,
                    CriadoPorUsuarioId = usuarioId,
                    DataCriacao = DateTime.UtcNow
                };

                _context.QuadrosTarefas.Add(quadro);
                await _context.SaveChangesAsync();

                return quadro;
            }
            catch (DbUpdateException ex) when (EhErroDuplicidade(ex))
            {
                if (quadro != null)
                    _context.Entry(quadro).State = EntityState.Detached;

                return await _context.QuadrosTarefas
                    .FirstAsync(q => q.GrupoId == grupoId && q.Ativo);
            }
        }

        var strategy = _context.Database.CreateExecutionStrategy();

        try
        {
            return await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _context.Database.BeginTransactionAsync();

                try
                {
                    var quadroExistente = await _context.QuadrosTarefas
                        .FirstOrDefaultAsync(q => q.GrupoId == grupoId && q.Ativo);

                    if (quadroExistente != null)
                    {
                        await transaction.CommitAsync();
                        return quadroExistente;
                    }

                    quadro = new QuadroTarefa
                    {
                        GrupoId = grupoId,
                        Nome = "Tarefas",
                        Descricao = "Quadro principal de tarefas",
                        Ativo = true,
                        CriadoPorUsuarioId = usuarioId,
                        DataCriacao = DateTime.UtcNow
                    };

                    _context.QuadrosTarefas.Add(quadro);
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    return quadro;
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
            if (quadro != null)
                _context.Entry(quadro).State = EntityState.Detached;

            return await _context.QuadrosTarefas
                .FirstAsync(q => q.GrupoId == grupoId && q.Ativo);
        }
    }

    private async Task<ColunaQuadro?> ObterColunaTarefasDestinoAsync(int quadroId, int? colunaId)
    {
        if (colunaId.HasValue && colunaId.Value > 0)
        {
            return await _context.ColunasQuadro
                .FirstOrDefaultAsync(c => c.Id == colunaId.Value && c.QuadroId == quadroId && c.Ativa);
        }

        return await _context.ColunasQuadro
            .Where(c => c.QuadroId == quadroId && c.Ativa)
            .OrderBy(c => c.Posicao)
            .FirstOrDefaultAsync();
    }

    public class ExcluirChamadoRequest
    {
        public int Id { get; set; }
    }

    public class AvancarStatusChamadoRequest
    {
        public int Id { get; set; }
        public int GrupoId { get; set; }
    }

    public class CriarTarefaDeChamadoRequest
    {
        public int GrupoId { get; set; }
        public int ChamadoId { get; set; }
        public int? ColunaId { get; set; }
    }

    public class AdicionarComentarioChamadoRequest
    {
        public int ChamadoId { get; set; }
        public string? Mensagem { get; set; }
        public IFormFile? AnexoImagem { get; set; }
    }

    public class EditarComentarioChamadoRequest
    {
        public int ChamadoId { get; set; }
        public int ComentarioId { get; set; }
        public string? Mensagem { get; set; }
    }

    public class ExcluirComentarioChamadoRequest
    {
        public int ChamadoId { get; set; }
        public int ComentarioId { get; set; }
    }

    public class MarcarComentariosVisualizadosRequest
    {
        public int ChamadoId { get; set; }
    }

    public class VinculoChamadoRequest
    {
        public int ChamadoId { get; set; }
        public int ChamadoVinculadoId { get; set; }
    }

    public class SalvarVinculosChamadoRequest
    {
        public int ChamadoId { get; set; }
        public List<int> ChamadosIds { get; set; } = new();
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
        public string? DataFinalizacao { get; set; }
        public string? PrazoResposta { get; set; }
        public string? PrazoConclusao { get; set; }
        public bool Publico { get; set; }
        public IFormFile? AnexoArquivo { get; set; }
    }

    private class ChamadoAcessoResultado
    {
        public bool Success { get; init; }
        public string? Message { get; init; }
        public Chamado? Chamado { get; init; }
        public GrupoMemberContext? ContextoMembro { get; init; }

        public static ChamadoAcessoResultado Ok(Chamado chamado, GrupoMemberContext contextoMembro) =>
            new() { Success = true, Chamado = chamado, ContextoMembro = contextoMembro };

        public static ChamadoAcessoResultado Fail(string message) =>
            new() { Success = false, Message = message };
    }

    private sealed class ChamadoVinculoDto
    {
        public int Id { get; set; }
        public int NumeroChamadoGrupo { get; set; }
        public string? Titulo { get; set; }
        public string Status { get; set; } = string.Empty;
        public string DataCriacao { get; set; } = string.Empty;
    }
}
