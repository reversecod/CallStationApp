using CallStationApp.Authorization;
using CallStationApp.Data;
using CallStationApp.Models;
using CallStationApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace CallStationApp.Pages.Menu;

[Authorize]
public class TasksModel : PageModel
{
    private const decimal OrdemBase = 1024m;
    private const string PrefixoColunaArquivoSistema = "__callstation_archive__";
    private const int LimiteCaracteresComentario = 250;
    private const int LimiteOpcoesChamadosTarefa = 200;
    private static readonly Regex CorHexRegex = new("^#[0-9a-fA-F]{6}$", RegexOptions.Compiled);
    private static readonly TimeZoneInfo FusoHorarioRegional = ObterFusoHorarioRegional();
    private readonly AppDbContext _context;
    private readonly GrupoAuthorizationService _grupoAuthorizationService;
    private readonly MencaoService _mencaoService;
    private readonly SlaPausaService _slaPausaService;
    private readonly AnexoUploadService _anexoUploadService;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<TasksModel> _logger;

    public TasksModel(AppDbContext context, GrupoAuthorizationService grupoAuthorizationService, MencaoService mencaoService, SlaPausaService slaPausaService, AnexoUploadService anexoUploadService, IWebHostEnvironment environment, ILogger<TasksModel> logger)
    {
        _context = context;
        _grupoAuthorizationService = grupoAuthorizationService;
        _mencaoService = mencaoService;
        _slaPausaService = slaPausaService;
        _anexoUploadService = anexoUploadService;
        _environment = environment;
        _logger = logger;
    }

    [BindProperty(SupportsGet = true)]
    public int GrupoId { get; set; }

    public string? NomeUsuarioLogado { get; set; }
    public string? FotoUsuarioLogado { get; set; }
    public bool UsuarioLogadoEhAdministrador { get; set; }
    public PermissaoUsuario UsuarioLogadoPermissao { get; set; } = PermissaoUsuario.Nenhuma;
    public Grupo? GrupoAtual { get; set; }
    public GrupoConfiguracao? Configuracao { get; set; }
    public QuadroTarefa Quadro { get; set; } = null!;
    public List<ColunaBoardViewModel> Colunas { get; set; } = new();
    public List<MembroViewModel> Membros { get; set; } = new();
    public List<ChamadoOpcaoViewModel> ChamadosDisponiveis { get; set; } = new();

    public async Task<IActionResult> OnGetAsync()
    {
        var usuarioId = GetUsuarioLogadoId();
        if (usuarioId == null)
            return RedirectToPage("/Auth/Login");

        if (GrupoId <= 0)
            return RedirectToPage("/Menu/Menu");

        var contexto = await _grupoAuthorizationService.ObterContextoMembroAsync(usuarioId.Value, GrupoId);
        if (contexto == null)
            return RedirectToPage("/Menu/Menu");

        UsuarioLogadoEhAdministrador = GrupoPermissionService.PodeGerenciarGrupo(contexto.Permissao);
        UsuarioLogadoPermissao = contexto.Permissao;

        if (contexto.Permissao == PermissaoUsuario.Nenhuma)
            return Forbid();

        GrupoAtual = await _context.Grupos.AsNoTracking().FirstOrDefaultAsync(g => g.Id == GrupoId);
        if (GrupoAtual == null)
            return RedirectToPage("/Menu/Menu");

        Configuracao = await _context.GruposConfiguracoes
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.GrupoId == GrupoId);

        var usuario = await _context.Usuarios
            .AsNoTracking()
            .Where(u => u.Id == usuarioId.Value)
            .Select(u => new { u.NomeUsuario, u.FotoUsuario })
            .FirstOrDefaultAsync();

        NomeUsuarioLogado = usuario?.NomeUsuario;
        FotoUsuarioLogado = usuario?.FotoUsuario;

        Quadro = await GarantirQuadroPadraoAsync(GrupoId, usuarioId.Value);
        await CarregarDadosAsync(usuarioId.Value, contexto.Permissao);

        return Page();
    }

    public async Task<IActionResult> OnPostCriarListaAsync([FromBody] CriarListaRequest request)
    {
        var usuarioId = GetUsuarioLogadoId();
        if (usuarioId == null)
            return Unauthorized();

        if (request == null || request.GrupoId <= 0)
            return BadRequest(new { success = false, message = "Lista invalida." });

        var contexto = await ValidarMembroComPermissaoAsync(usuarioId.Value, request.GrupoId);
        if (contexto == null)
            return Forbid();

        var nome = request.Nome?.Trim();
        if (string.IsNullOrWhiteSpace(nome) || nome.Length > 60)
            return BadRequest(new { success = false, message = "Nome da lista inválido." });

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
                        var quadro = await GarantirQuadroPadraoAsync(request.GrupoId, usuarioId.Value);
                        await RemoverColunasInativasComMesmoNomeAsync(quadro.Id, request.GrupoId, nome, usuarioId.Value);

                        var existe = await _context.ColunasQuadro
                            .AnyAsync(c => c.QuadroId == quadro.Id && c.Nome == nome);

                        if (existe)
                            return (IActionResult)BadRequest(new { success = false, message = "Ja existe uma lista com este nome." });

                        var ultimaPosicao = await _context.ColunasQuadro
                            .Where(c => c.QuadroId == quadro.Id)
                            .MaxAsync(c => (decimal?)c.Posicao) ?? 0m;

                        var coluna = new ColunaQuadro
                        {
                            QuadroId = quadro.Id,
                            Nome = nome,
                            Posicao = ultimaPosicao + OrdemBase,
                            Ativa = true,
                            DataCriacao = DateTime.UtcNow
                        };

                        _context.ColunasQuadro.Add(coluna);
                        await _context.SaveChangesAsync();
                        await transaction.CommitAsync();

                        return (IActionResult)new JsonResult(new { success = true, id = coluna.Id, nome = coluna.Nome });
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
                _context.ChangeTracker.Clear();

                var quadro = await _context.QuadrosTarefas
                    .AsNoTracking()
                    .FirstOrDefaultAsync(q => q.GrupoId == request.GrupoId && q.Ativo);

                if (quadro != null && await _context.ColunasQuadro.AsNoTracking().AnyAsync(c => c.QuadroId == quadro.Id && c.Nome == nome))
                    return BadRequest(new { success = false, message = "Ja existe uma lista com este nome." });

                if (tentativa == maxTentativas)
                    return BadRequest(new { success = false, message = "Não foi possível criar a lista no momento." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao criar lista no grupo {GrupoId}.", request.GrupoId);
                return BadRequest(new { success = false, message = "Não foi possível criar a lista no momento." });
            }
        }

        return BadRequest(new { success = false, message = "Não foi possível criar a lista no momento." });
    }

    public async Task<IActionResult> OnPostRenomearListaAsync([FromBody] RenomearListaRequest request)
    {
        var usuarioId = GetUsuarioLogadoId();
        if (usuarioId == null)
            return Unauthorized();

        if (request == null)
            return BadRequest(new { success = false, message = "Requisição inválida." });

        var contexto = await ValidarMembroComPermissaoAsync(usuarioId.Value, request.GrupoId);
        if (contexto == null)
            return Forbid();

        var nome = request.Nome?.Trim();
        if (request.ColunaId <= 0 || string.IsNullOrWhiteSpace(nome) || nome.Length > 60)
            return BadRequest(new { success = false, message = "Nome da lista inválido." });

        var strategy = _context.Database.CreateExecutionStrategy();

        try
        {
            return await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _context.Database.BeginTransactionAsync();

                try
                {
                    var coluna = await _context.ColunasQuadro
                        .FirstOrDefaultAsync(c => c.Id == request.ColunaId && c.Ativa && c.Quadro.GrupoId == request.GrupoId);

                    if (coluna == null)
                        return (IActionResult)NotFound(new { success = false, message = "Lista nao encontrada." });

                    var existe = await _context.ColunasQuadro
                        .AnyAsync(c => c.QuadroId == coluna.QuadroId && c.Id != coluna.Id && c.Nome == nome);

                    if (existe)
                        return (IActionResult)BadRequest(new { success = false, message = "Ja existe uma lista com este nome." });

                    coluna.Nome = nome;
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    return (IActionResult)new JsonResult(new { success = true, id = coluna.Id, nome = coluna.Nome });
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
            return BadRequest(new { success = false, message = "Ja existe uma lista com este nome." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao renomear lista {ColunaId} no grupo {GrupoId}.", request.ColunaId, request.GrupoId);
            return BadRequest(new { success = false, message = "Não foi possível renomear a lista no momento." });
        }
    }

    public async Task<IActionResult> OnPostArquivarCartoesListaAsync([FromBody] ListaRequest request)
    {
        var usuarioId = GetUsuarioLogadoId();
        if (usuarioId == null)
            return Unauthorized();

        var contexto = await ValidarMembroComPermissaoAsync(usuarioId.Value, request.GrupoId);
        if (contexto == null)
            return Forbid();

        var strategy = _context.Database.CreateExecutionStrategy();

        try
        {
            return await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _context.Database.BeginTransactionAsync();

                try
                {
                    var coluna = await _context.ColunasQuadro
                        .AsNoTracking()
                        .FirstOrDefaultAsync(c => c.Id == request.ColunaId && c.Ativa && c.Quadro.GrupoId == request.GrupoId);

                    if (coluna == null)
                        return (IActionResult)NotFound(new { success = false, message = "Lista nao encontrada." });

                    var cartoes = await _context.CartoesTarefas
                        .Where(c => c.ColunaId == request.ColunaId && c.GrupoId == request.GrupoId && c.Status != StatusCartaoTarefa.Arquivada)
                        .ToListAsync();

                    var agora = DateTime.UtcNow;
                    foreach (var cartao in cartoes)
                    {
                        cartao.Status = StatusCartaoTarefa.Arquivada;
                        cartao.DataAtualizacao = agora;
                        RegistrarHistorico(cartao.Id, usuarioId.Value, "Cartao arquivado pela lista");
                    }

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    return (IActionResult)new JsonResult(new
                    {
                        success = true,
                        cartoesIds = cartoes.Select(c => c.Id).ToList()
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
            _logger.LogError(ex, "Erro ao arquivar cartoes da lista {ColunaId} no grupo {GrupoId}.", request.ColunaId, request.GrupoId);
            return BadRequest(new { success = false, message = "Não foi possível arquivar os cartoes da lista no momento." });
        }
    }

    public async Task<IActionResult> OnPostExcluirListaAsync([FromBody] ListaRequest request)
    {
        var usuarioId = GetUsuarioLogadoId();
        if (usuarioId == null)
            return Unauthorized();

        var contexto = await ValidarMembroComPermissaoAsync(usuarioId.Value, request.GrupoId);
        if (contexto == null)
            return Forbid();

        var strategy = _context.Database.CreateExecutionStrategy();

        try
        {
            return await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _context.Database.BeginTransactionAsync();

                try
                {
                    var coluna = await _context.ColunasQuadro
                        .FirstOrDefaultAsync(c => c.Id == request.ColunaId && c.Ativa && c.Quadro.GrupoId == request.GrupoId);

                    if (coluna == null)
                        return (IActionResult)NotFound(new { success = false, message = "Lista nao encontrada." });

                    var cartoes = await _context.CartoesTarefas
                        .Where(c => c.ColunaId == request.ColunaId && c.GrupoId == request.GrupoId)
                        .ToListAsync();

                    var colunaArquivo = cartoes.Count > 0
                        ? await ObterOuCriarColunaArquivoSistemaAsync(coluna.QuadroId)
                        : null;

                    var agora = DateTime.UtcNow;
                    foreach (var cartao in cartoes)
                    {
                        if (cartao.Status != StatusCartaoTarefa.Arquivada)
                            cartao.Status = StatusCartaoTarefa.Arquivada;

                        if (colunaArquivo != null)
                            cartao.Coluna = colunaArquivo;

                        cartao.DataAtualizacao = agora;
                        RegistrarHistorico(cartao.Id, usuarioId.Value, "Cartao arquivado por exclusao da lista");
                    }

                    _context.ColunasQuadro.Remove(coluna);
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    return (IActionResult)new JsonResult(new
                    {
                        success = true,
                        cartoesIds = cartoes.Select(c => c.Id).ToList()
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
            _logger.LogError(ex, "Erro ao excluir lista {ColunaId} no grupo {GrupoId}.", request.ColunaId, request.GrupoId);
            return BadRequest(new { success = false, message = "Não foi possível excluir a lista no momento." });
        }
    }

    public async Task<IActionResult> OnGetCartaoAsync(int id, int grupoId)
    {
        var usuarioId = GetUsuarioLogadoId();
        if (usuarioId == null)
            return Unauthorized();

        var contexto = await ValidarMembroAsync(usuarioId.Value, grupoId);
        if (contexto == null)
            return Forbid();

        var cartao = await _context.CartoesTarefas
            .FirstOrDefaultAsync(c => c.Id == id && c.GrupoId == grupoId);

        if (cartao == null)
            return NotFound(new { success = false, message = "Cartao não encontrado." });

        if (!await PodeVerCartaoAsync(cartao, usuarioId.Value))
            return Forbid();

        var membros = await _context.CartoesTarefasUsuarios
            .AsNoTracking()
            .Where(x => x.CartaoTarefaId == id)
            .Select(x => x.UsuarioId)
            .ToListAsync();

        var chamados = await ObterChamadosVinculadosVisiveisAsync(cartao);

        var chamadosOpcoes = await ObterChamadosPermitidosParaCartaoAsync(cartao);

        var comentarios = await (
            from comentario in _context.ComentariosTarefas.AsNoTracking()
            join usuario in _context.Usuarios.AsNoTracking()
                on comentario.UsuarioId equals usuario.Id
            where comentario.CartaoTarefaId == id
            orderby comentario.DataCriacao descending
            select new
            {
                tipo = "comentario",
                usuario = usuario.NomeUsuario,
                texto = comentario.Mensagem,
                data = comentario.DataCriacao
            })
            .Take(20)
            .ToListAsync();

        var historico = await (
            from item in _context.HistoricoTarefas.AsNoTracking()
            join usuario in _context.Usuarios.AsNoTracking()
                on item.UsuarioId equals usuario.Id
            where item.CartaoTarefaId == id
            orderby item.DataAcao descending
            select new
            {
                tipo = "historico",
                usuario = usuario.NomeUsuario,
                texto = item.TipoAcao,
                data = item.DataAcao
            })
            .Take(20)
            .ToListAsync();

        var atividade = comentarios
            .Concat(historico)
            .OrderByDescending(x => x.data)
            .Take(30)
            .Select(x => new
            {
                x.tipo,
                x.usuario,
                x.texto,
                data = ParaDataHoraRegionalIso(x.data)
            })
            .ToList();

        var etiquetasDisponiveis = await ObterEtiquetasUsuarioAsync(grupoId, usuarioId.Value);
        var etiquetasAplicadas = await ObterEtiquetasAplicadasAsync(id, usuarioId.Value);
        var checklists = await ObterChecklistsCartaoAsync(id);
        var anexos = await ObterAnexosCartaoAsync(id);
        var podeEditar = PodeEditarCartao(cartao, usuarioId.Value, membros);
        var membrosVisiveis = await ObterMembrosVisiveisParaCardAsync(grupoId, cartao.Privado, membros.Append(cartao.CriadorId));

        return new JsonResult(new
        {
            success = true,
            id = cartao.Id,
            colunaId = cartao.ColunaId,
            titulo = cartao.Titulo,
            descricao = cartao.Descricao,
            prioridade = cartao.Prioridade?.ToString(),
            criticidade = cartao.Criticidade?.ToString(),
            urgencia = cartao.Urgencia?.ToString(),
            dataInicio = cartao.DataInicio,
            dataVencimento = cartao.DataVencimento,
            corCapa = cartao.CorCapa,
            concluido = cartao.Status == StatusCartaoTarefa.Concluida,
            status = cartao.Status.ToString(),
            arquivado = cartao.Status == StatusCartaoTarefa.Arquivada,
            compartilharGrupo = !cartao.Privado,
            membros,
            membrosVisiveis,
            chamados = chamados.Select(c => c.Id).ToList(),
            chamadosVinculados = chamados,
            chamadosOpcoes,
            etiquetasDisponiveis,
            etiquetasAplicadas,
            checklists,
            anexos,
            podeEditar,
            podeSairVinculo = cartao.CriadorId != usuarioId.Value && membros.Contains(usuarioId.Value),
            atividade
        });
    }

    public async Task<IActionResult> OnGetEtiquetasTarefaUsuarioAsync(int grupoId)
    {
        var usuarioId = GetUsuarioLogadoId();
        if (usuarioId == null)
            return Unauthorized();

        var contexto = await ValidarMembroAsync(usuarioId.Value, grupoId);
        if (contexto == null)
            return Forbid();

        var etiquetas = await ObterEtiquetasUsuarioAsync(grupoId, usuarioId.Value);
        return new JsonResult(new { success = true, dados = etiquetas });
    }

    public async Task<IActionResult> OnPostCriarEtiquetaTarefaAsync([FromBody] SalvarEtiquetaTarefaRequest request)
    {
        var usuarioId = GetUsuarioLogadoId();
        if (usuarioId == null)
            return Unauthorized();

        var contexto = await ValidarMembroAsync(usuarioId.Value, request.GrupoId);
        if (contexto == null)
            return Forbid();

        var nome = request.Nome?.Trim();
        var cor = request.Cor?.Trim();
        if (string.IsNullOrWhiteSpace(nome) || nome.Length > 50)
            return BadRequest(new { success = false, message = "Nome da etiqueta invalido." });
        if (string.IsNullOrWhiteSpace(cor) || !CorHexRegex.IsMatch(cor))
            return BadRequest(new { success = false, message = "Cor da etiqueta invalida." });

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
                        var existe = await _context.EtiquetasTarefas
                            .AnyAsync(e => e.GrupoId == request.GrupoId && e.UsuarioId == usuarioId.Value && e.Nome == nome);
                        if (existe)
                            return (IActionResult)BadRequest(new { success = false, message = "Ja existe uma etiqueta com este nome." });

                        var etiqueta = new EtiquetaTarefa
                        {
                            GrupoId = request.GrupoId,
                            UsuarioId = usuarioId.Value,
                            Nome = nome,
                            Cor = cor,
                            DataCriacao = DateTime.UtcNow
                        };
                        _context.EtiquetasTarefas.Add(etiqueta);
                        await _context.SaveChangesAsync();
                        await transaction.CommitAsync();

                        return (IActionResult)new JsonResult(new { success = true, dados = new EtiquetaTarefaDto(etiqueta.Id, etiqueta.Nome, etiqueta.Cor) });
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
                _context.ChangeTracker.Clear();
                if (tentativa == maxTentativas)
                    return BadRequest(new { success = false, message = "Ja existe uma etiqueta com este nome." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao criar etiqueta no grupo {GrupoId}.", request.GrupoId);
                return BadRequest(new { success = false, message = "Nao foi possivel criar a etiqueta." });
            }
        }

        return BadRequest(new { success = false, message = "Nao foi possivel criar a etiqueta." });
    }

    public async Task<IActionResult> OnPostEditarEtiquetaTarefaAsync([FromBody] EditarEtiquetaTarefaRequest request)
    {
        var usuarioId = GetUsuarioLogadoId();
        if (usuarioId == null)
            return Unauthorized();

        var contexto = await ValidarMembroAsync(usuarioId.Value, request.GrupoId);
        if (contexto == null)
            return Forbid();

        var nome = request.Nome?.Trim();
        var cor = request.Cor?.Trim();
        if (request.EtiquetaId <= 0 || string.IsNullOrWhiteSpace(nome) || nome.Length > 50)
            return BadRequest(new { success = false, message = "Etiqueta invalida." });
        if (string.IsNullOrWhiteSpace(cor) || !CorHexRegex.IsMatch(cor))
            return BadRequest(new { success = false, message = "Cor da etiqueta invalida." });

        var strategy = _context.Database.CreateExecutionStrategy();
        try
        {
            return await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    var etiqueta = await _context.EtiquetasTarefas
                        .FirstOrDefaultAsync(e => e.Id == request.EtiquetaId && e.GrupoId == request.GrupoId && e.UsuarioId == usuarioId.Value);
                    if (etiqueta == null)
                        return (IActionResult)NotFound(new { success = false, message = "Etiqueta nao encontrada." });

                    var existe = await _context.EtiquetasTarefas
                        .AnyAsync(e => e.Id != etiqueta.Id && e.GrupoId == request.GrupoId && e.UsuarioId == usuarioId.Value && e.Nome == nome);
                    if (existe)
                        return (IActionResult)BadRequest(new { success = false, message = "Ja existe uma etiqueta com este nome." });

                    etiqueta.Nome = nome;
                    etiqueta.Cor = cor;
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    return (IActionResult)new JsonResult(new { success = true, dados = new EtiquetaTarefaDto(etiqueta.Id, etiqueta.Nome, etiqueta.Cor) });
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
            return BadRequest(new { success = false, message = "Ja existe uma etiqueta com este nome." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao editar etiqueta {EtiquetaId}.", request.EtiquetaId);
            return BadRequest(new { success = false, message = "Nao foi possivel editar a etiqueta." });
        }
    }

    public async Task<IActionResult> OnPostExcluirEtiquetaTarefaAsync([FromBody] EtiquetaTarefaRequest request)
    {
        var usuarioId = GetUsuarioLogadoId();
        if (usuarioId == null)
            return Unauthorized();

        var contexto = await ValidarMembroAsync(usuarioId.Value, request.GrupoId);
        if (contexto == null)
            return Forbid();

        var strategy = _context.Database.CreateExecutionStrategy();
        try
        {
            return await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    var etiqueta = await _context.EtiquetasTarefas
                        .FirstOrDefaultAsync(e => e.Id == request.EtiquetaId && e.GrupoId == request.GrupoId && e.UsuarioId == usuarioId.Value);
                    if (etiqueta == null)
                        return (IActionResult)NotFound(new { success = false, message = "Etiqueta nao encontrada." });

                    var vinculos = await _context.CartoesTarefasEtiquetas
                        .Where(v => v.EtiquetaId == etiqueta.Id)
                        .ToListAsync();
                    _context.CartoesTarefasEtiquetas.RemoveRange(vinculos);
                    _context.EtiquetasTarefas.Remove(etiqueta);
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    return (IActionResult)new JsonResult(new { success = true });
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
            _logger.LogError(ex, "Erro ao excluir etiqueta {EtiquetaId}.", request.EtiquetaId);
            return BadRequest(new { success = false, message = "Nao foi possivel excluir a etiqueta." });
        }
    }

    public async Task<IActionResult> OnPostSalvarEtiquetasCartaoAsync([FromBody] SalvarEtiquetasCartaoRequest request)
    {
        var usuarioId = GetUsuarioLogadoId();
        if (usuarioId == null)
            return Unauthorized();

        var acesso = await ObterCartaoComAcessoAsync(request.CartaoId, request.GrupoId, usuarioId.Value, exigirEdicao: false);
        if (acesso.Resultado != null)
            return acesso.Resultado;

        var etiquetasIds = request.EtiquetasIds?.Where(id => id > 0).Distinct().ToList() ?? new List<int>();
        var etiquetasPermitidas = etiquetasIds.Count == 0
            ? new List<int>()
            : await _context.EtiquetasTarefas.AsNoTracking()
                .Where(e => etiquetasIds.Contains(e.Id) && e.GrupoId == request.GrupoId && e.UsuarioId == usuarioId.Value)
                .Select(e => e.Id)
                .ToListAsync();

        if (etiquetasPermitidas.Count != etiquetasIds.Count)
            return BadRequest(new { success = false, message = "Etiqueta invalida para esta tarefa." });

        var strategy = _context.Database.CreateExecutionStrategy();
        try
        {
            return await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    var atuais = await _context.CartoesTarefasEtiquetas
                        .Where(v => v.CartaoTarefaId == request.CartaoId)
                        .Join(_context.EtiquetasTarefas.Where(e => e.UsuarioId == usuarioId.Value && e.GrupoId == request.GrupoId),
                            v => v.EtiquetaId,
                            e => e.Id,
                            (v, e) => v)
                        .ToListAsync();

                    var atuaisIds = atuais.Select(v => v.EtiquetaId).ToHashSet();
                    var novosIds = etiquetasPermitidas.ToHashSet();
                    _context.CartoesTarefasEtiquetas.RemoveRange(atuais.Where(v => !novosIds.Contains(v.EtiquetaId)));
                    foreach (var etiquetaId in novosIds.Where(id => !atuaisIds.Contains(id)))
                    {
                        _context.CartoesTarefasEtiquetas.Add(new CartaoTarefaEtiqueta
                        {
                            CartaoTarefaId = request.CartaoId,
                            EtiquetaId = etiquetaId
                        });
                    }

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    var aplicadas = await ObterEtiquetasAplicadasAsync(request.CartaoId, usuarioId.Value);
                    return (IActionResult)new JsonResult(new { success = true, dados = aplicadas });
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
            _logger.LogError(ex, "Erro ao salvar etiquetas da tarefa {CartaoId}.", request.CartaoId);
            return BadRequest(new { success = false, message = "Nao foi possivel salvar as etiquetas." });
        }
    }

    public async Task<IActionResult> OnPostCriarChecklistTarefaAsync([FromBody] SalvarChecklistTarefaRequest request)
    {
        var usuarioId = GetUsuarioLogadoId();
        if (usuarioId == null)
            return Unauthorized();

        var acesso = await ObterCartaoComAcessoAsync(request.CartaoId, request.GrupoId, usuarioId.Value, exigirEdicao: true);
        if (acesso.Resultado != null)
            return acesso.Resultado;

        var titulo = request.Titulo?.Trim();
        if (string.IsNullOrWhiteSpace(titulo) || titulo.Length > 120)
            return BadRequest(new { success = false, message = "Titulo do checklist invalido." });

        var strategy = _context.Database.CreateExecutionStrategy();
        try
        {
            return await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    var ultimaPosicao = await _context.ChecklistsTarefas
                        .Where(c => c.CartaoTarefaId == request.CartaoId)
                        .MaxAsync(c => (decimal?)c.Posicao) ?? 0m;

                    var checklist = new ChecklistTarefa
                    {
                        CartaoTarefaId = request.CartaoId,
                        Titulo = titulo,
                        Posicao = ultimaPosicao + OrdemBase,
                        DataCriacao = DateTime.UtcNow
                    };
                    _context.ChecklistsTarefas.Add(checklist);
                    RegistrarHistorico(request.CartaoId, usuarioId.Value, "Checklist criado");
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    var checklists = await ObterChecklistsCartaoAsync(request.CartaoId);
                    return (IActionResult)new JsonResult(new { success = true, dados = checklists });
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
            _logger.LogError(ex, "Erro ao criar checklist na tarefa {CartaoId}.", request.CartaoId);
            return BadRequest(new { success = false, message = "Nao foi possivel criar o checklist." });
        }
    }

    public async Task<IActionResult> OnPostEditarChecklistTarefaAsync([FromBody] EditarChecklistTarefaRequest request)
    {
        var usuarioId = GetUsuarioLogadoId();
        if (usuarioId == null)
            return Unauthorized();

        var titulo = request.Titulo?.Trim();
        if (request.ChecklistId <= 0 || string.IsNullOrWhiteSpace(titulo) || titulo.Length > 120)
            return BadRequest(new { success = false, message = "Checklist invalido." });

        var checklist = await _context.ChecklistsTarefas
            .Include(c => c.CartaoTarefa)
            .FirstOrDefaultAsync(c => c.Id == request.ChecklistId && c.CartaoTarefa.GrupoId == request.GrupoId);
        if (checklist == null)
            return NotFound(new { success = false, message = "Checklist nao encontrado." });

        var acesso = await ObterCartaoComAcessoAsync(checklist.CartaoTarefaId, request.GrupoId, usuarioId.Value, exigirEdicao: true);
        if (acesso.Resultado != null)
            return acesso.Resultado;

        var strategy = _context.Database.CreateExecutionStrategy();
        try
        {
            return await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    checklist.Titulo = titulo;
                    RegistrarHistorico(checklist.CartaoTarefaId, usuarioId.Value, "Checklist editado");
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    var checklists = await ObterChecklistsCartaoAsync(checklist.CartaoTarefaId);
                    return (IActionResult)new JsonResult(new { success = true, dados = checklists });
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
            _logger.LogError(ex, "Erro ao editar checklist {ChecklistId}.", request.ChecklistId);
            return BadRequest(new { success = false, message = "Nao foi possivel editar o checklist." });
        }
    }

    public async Task<IActionResult> OnPostExcluirChecklistTarefaAsync([FromBody] ChecklistTarefaRequest request)
    {
        var usuarioId = GetUsuarioLogadoId();
        if (usuarioId == null)
            return Unauthorized();

        var checklist = await _context.ChecklistsTarefas
            .Include(c => c.CartaoTarefa)
            .FirstOrDefaultAsync(c => c.Id == request.ChecklistId && c.CartaoTarefa.GrupoId == request.GrupoId);
        if (checklist == null)
            return NotFound(new { success = false, message = "Checklist nao encontrado." });

        var acesso = await ObterCartaoComAcessoAsync(checklist.CartaoTarefaId, request.GrupoId, usuarioId.Value, exigirEdicao: true);
        if (acesso.Resultado != null)
            return acesso.Resultado;

        var strategy = _context.Database.CreateExecutionStrategy();
        try
        {
            return await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    var itens = await _context.ChecklistItensTarefas
                        .Where(i => i.ChecklistId == checklist.Id)
                        .ToListAsync();
                    _context.ChecklistItensTarefas.RemoveRange(itens);
                    _context.ChecklistsTarefas.Remove(checklist);
                    RegistrarHistorico(checklist.CartaoTarefaId, usuarioId.Value, "Checklist excluido");
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    var checklists = await ObterChecklistsCartaoAsync(checklist.CartaoTarefaId);
                    return (IActionResult)new JsonResult(new { success = true, dados = checklists });
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
            _logger.LogError(ex, "Erro ao excluir checklist {ChecklistId}.", request.ChecklistId);
            return BadRequest(new { success = false, message = "Nao foi possivel excluir o checklist." });
        }
    }

    public async Task<IActionResult> OnPostCriarItemChecklistTarefaAsync([FromBody] SalvarItemChecklistTarefaRequest request)
    {
        var usuarioId = GetUsuarioLogadoId();
        if (usuarioId == null)
            return Unauthorized();

        var descricao = request.Descricao?.Trim();
        if (request.ChecklistId <= 0 || string.IsNullOrWhiteSpace(descricao) || descricao.Length > 255)
            return BadRequest(new { success = false, message = "Item invalido." });

        var checklist = await _context.ChecklistsTarefas
            .Include(c => c.CartaoTarefa)
            .FirstOrDefaultAsync(c => c.Id == request.ChecklistId && c.CartaoTarefa.GrupoId == request.GrupoId);
        if (checklist == null)
            return NotFound(new { success = false, message = "Checklist nao encontrado." });

        var acesso = await ObterCartaoComAcessoAsync(checklist.CartaoTarefaId, request.GrupoId, usuarioId.Value, exigirEdicao: true);
        if (acesso.Resultado != null)
            return acesso.Resultado;

        var strategy = _context.Database.CreateExecutionStrategy();
        try
        {
            return await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    var ultimaPosicao = await _context.ChecklistItensTarefas
                        .Where(i => i.ChecklistId == request.ChecklistId)
                        .MaxAsync(i => (decimal?)i.Posicao) ?? 0m;
                    _context.ChecklistItensTarefas.Add(new ChecklistItemTarefa
                    {
                        ChecklistId = request.ChecklistId,
                        Descricao = descricao,
                        Concluido = false,
                        Posicao = ultimaPosicao + OrdemBase
                    });
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    var checklists = await ObterChecklistsCartaoAsync(checklist.CartaoTarefaId);
                    return (IActionResult)new JsonResult(new { success = true, dados = checklists });
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
            _logger.LogError(ex, "Erro ao criar item no checklist {ChecklistId}.", request.ChecklistId);
            return BadRequest(new { success = false, message = "Nao foi possivel criar o item." });
        }
    }

    public async Task<IActionResult> OnPostEditarItemChecklistTarefaAsync([FromBody] EditarItemChecklistTarefaRequest request)
    {
        return await AtualizarItemChecklistAsync(request.ItemId, request.GrupoId, request.Descricao, null);
    }

    public async Task<IActionResult> OnPostAlternarItemChecklistTarefaAsync([FromBody] AlternarItemChecklistTarefaRequest request)
    {
        return await AtualizarItemChecklistAsync(request.ItemId, request.GrupoId, null, request.Concluido);
    }

    public async Task<IActionResult> OnPostExcluirItemChecklistTarefaAsync([FromBody] ItemChecklistTarefaRequest request)
    {
        var usuarioId = GetUsuarioLogadoId();
        if (usuarioId == null)
            return Unauthorized();

        var item = await _context.ChecklistItensTarefas
            .Include(i => i.Checklist)
            .ThenInclude(c => c.CartaoTarefa)
            .FirstOrDefaultAsync(i => i.Id == request.ItemId && i.Checklist.CartaoTarefa.GrupoId == request.GrupoId);
        if (item == null)
            return NotFound(new { success = false, message = "Item nao encontrado." });

        var cartaoId = item.Checklist.CartaoTarefaId;
        var acesso = await ObterCartaoComAcessoAsync(cartaoId, request.GrupoId, usuarioId.Value, exigirEdicao: true);
        if (acesso.Resultado != null)
            return acesso.Resultado;

        var strategy = _context.Database.CreateExecutionStrategy();
        try
        {
            return await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    _context.ChecklistItensTarefas.Remove(item);
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();
                    var checklists = await ObterChecklistsCartaoAsync(cartaoId);
                    return (IActionResult)new JsonResult(new { success = true, dados = checklists });
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
            _logger.LogError(ex, "Erro ao excluir item {ItemId}.", request.ItemId);
            return BadRequest(new { success = false, message = "Nao foi possivel excluir o item." });
        }
    }

    public async Task<IActionResult> OnPostEnviarAnexoTarefaAsync([FromForm] EnviarAnexoTarefaRequest request)
    {
        var usuarioId = GetUsuarioLogadoId();
        if (usuarioId == null)
            return Unauthorized();

        if (request.Arquivo == null || request.Arquivo.Length == 0)
            return BadRequest(new { success = false, message = "Selecione um arquivo." });

        var acesso = await ObterCartaoComAcessoAsync(request.CartaoId, request.GrupoId, usuarioId.Value, exigirEdicao: true);
        if (acesso.Resultado != null)
            return acesso.Resultado;

        var uploadsRoot = Path.Combine(_environment.ContentRootPath, "uploads_privados", "tarefas", "anexos");
        var anexoSalvo = await _anexoUploadService.SalvarAsync(
            request.Arquivo,
            request.ArquivoCompactado,
            uploadsRoot,
            HttpContext.RequestAborted);

        if (!anexoSalvo.Sucesso)
            return BadRequest(new { success = false, message = anexoSalvo.Mensagem ?? "Arquivo invalido." });

        var nomeArquivo = anexoSalvo.NomeArquivo!;
        var caminhoFisico = anexoSalvo.CaminhoFisico!;
        var caminhoRelativo = Path.Combine("tarefas", "anexos", nomeArquivo).Replace('\\', '/');

        try
        {
            var strategy = _context.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    var anexo = new AnexoTarefa
                    {
                        CartaoTarefaId = request.CartaoId,
                        UsuarioId = usuarioId.Value,
                        NomeOriginal = anexoSalvo.NomeOriginal!,
                        NomeArquivo = nomeArquivo,
                        CaminhoArquivo = caminhoRelativo,
                        TipoArquivo = anexoSalvo.ContentType,
                        Extensao = anexoSalvo.Extensao,
                        TamanhoBytes = anexoSalvo.TamanhoBytes,
                        EhImagem = anexoSalvo.EhImagem,
                        EhCapa = false,
                        DataUpload = DateTime.UtcNow
                    };
                    _context.AnexosTarefas.Add(anexo);
                    RegistrarHistorico(request.CartaoId, usuarioId.Value, "Anexo adicionado");
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    var anexos = await ObterAnexosCartaoAsync(request.CartaoId);
                    return (IActionResult)new JsonResult(new { success = true, dados = anexos });
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            });
        }
        catch (OperationCanceledException) when (HttpContext.RequestAborted.IsCancellationRequested)
        {
            if (System.IO.File.Exists(caminhoFisico))
                System.IO.File.Delete(caminhoFisico);

            return new EmptyResult();
        }
        catch (Exception ex)
        {
            if (System.IO.File.Exists(caminhoFisico))
                System.IO.File.Delete(caminhoFisico);

            _logger.LogError(ex, "Erro ao anexar arquivo na tarefa {CartaoId}.", request.CartaoId);
            return BadRequest(new { success = false, message = "Nao foi possivel anexar o arquivo." });
        }
    }

    public async Task<IActionResult> OnGetBaixarAnexoTarefaAsync(int anexoId, int grupoId)
    {
        var acesso = await ObterAnexoComAcessoAsync(anexoId, grupoId, exigirEdicao: false);
        if (acesso.Resultado != null)
            return acesso.Resultado;

        var caminho = ObterCaminhoFisicoAnexoTarefa(acesso.Anexo!.CaminhoArquivo);
        if (!System.IO.File.Exists(caminho))
            return NotFound();

        _anexoUploadService.AplicarCabecalhosDownloadSeguro(Response, inline: false);
        return new PhysicalFileResult(caminho, acesso.Anexo.TipoArquivo ?? "application/octet-stream")
        {
            FileDownloadName = acesso.Anexo.NomeOriginal,
            EnableRangeProcessing = true
        };
    }

    public async Task<IActionResult> OnGetVisualizarAnexoTarefaAsync(int anexoId, int grupoId)
    {
        var acesso = await ObterAnexoComAcessoAsync(anexoId, grupoId, exigirEdicao: false);
        if (acesso.Resultado != null)
            return acesso.Resultado;

        var regra = _anexoUploadService.ObterRegra(acesso.Anexo!.Extensao);
        if (regra == null || !regra.PermiteVisualizacao)
            return BadRequest(new { success = false, message = "Este anexo nao possui visualizacao segura." });

        var caminho = ObterCaminhoFisicoAnexoTarefa(acesso.Anexo.CaminhoArquivo);
        if (!System.IO.File.Exists(caminho))
            return NotFound();

        _anexoUploadService.AplicarCabecalhosDownloadSeguro(Response, inline: true);
        return new PhysicalFileResult(caminho, regra.ContentType)
        {
            EnableRangeProcessing = true
        };
    }

    public async Task<IActionResult> OnPostExcluirAnexoTarefaAsync([FromBody] AnexoTarefaRequest request)
    {
        var acesso = await ObterAnexoComAcessoAsync(request.AnexoId, request.GrupoId, exigirEdicao: true);
        if (acesso.Resultado != null)
            return acesso.Resultado;

        var usuarioId = GetUsuarioLogadoId();
        if (usuarioId == null)
            return Unauthorized();

        var caminho = ObterCaminhoFisicoAnexoTarefa(acesso.Anexo!.CaminhoArquivo);
        var cartaoId = acesso.Anexo.CartaoTarefaId;
        var strategy = _context.Database.CreateExecutionStrategy();

        try
        {
            return await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    _context.AnexosTarefas.Remove(acesso.Anexo);
                    RegistrarHistorico(cartaoId, usuarioId.Value, "Anexo excluido");
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    if (System.IO.File.Exists(caminho))
                        System.IO.File.Delete(caminho);

                    var anexos = await ObterAnexosCartaoAsync(cartaoId);
                    return (IActionResult)new JsonResult(new { success = true, dados = anexos });
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
            _logger.LogError(ex, "Erro ao excluir anexo {AnexoId}.", request.AnexoId);
            return BadRequest(new { success = false, message = "Nao foi possivel excluir o anexo." });
        }
    }

    public async Task<IActionResult> OnPostSairCartaoAsync([FromBody] SairCartaoRequest request)
    {
        var usuarioId = GetUsuarioLogadoId();
        if (usuarioId == null)
            return Unauthorized();

        if (request == null || request.CartaoId <= 0 || request.GrupoId <= 0)
            return BadRequest(new { success = false, message = "Tarefa invalida." });

        var contexto = await ValidarMembroAsync(usuarioId.Value, request.GrupoId);
        if (contexto == null)
            return Forbid();

        var strategy = _context.Database.CreateExecutionStrategy();

        try
        {
            return await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _context.Database.BeginTransactionAsync();

                try
                {
                    var cartao = await _context.CartoesTarefas
                        .FirstOrDefaultAsync(c => c.Id == request.CartaoId && c.GrupoId == request.GrupoId);

                    if (cartao == null)
                        return (IActionResult)NotFound(new { success = false, message = "Tarefa nao encontrada." });

                    if (cartao.CriadorId == usuarioId.Value)
                        return (IActionResult)BadRequest(new { success = false, message = "O criador não pode sair da própria tarefa." });

                    var vinculo = await _context.CartoesTarefasUsuarios
                        .FirstOrDefaultAsync(x => x.CartaoTarefaId == cartao.Id && x.UsuarioId == usuarioId.Value);

                    if (vinculo == null)
                        return (IActionResult)BadRequest(new { success = false, message = "Você não está vinculado diretamente a esta tarefa." });

                    _context.CartoesTarefasUsuarios.Remove(vinculo);
                    cartao.DataAtualizacao = DateTime.UtcNow;
                    RegistrarHistorico(cartao.Id, usuarioId.Value, "Usuario saiu da tarefa");

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    return (IActionResult)new JsonResult(new
                    {
                        success = true,
                        message = "Você saiu da tarefa."
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
            _logger.LogError(ex, "Erro ao sair da tarefa {CartaoId} no grupo {GrupoId}.", request.CartaoId, request.GrupoId);
            return BadRequest(new { success = false, message = "Não foi possível sair da tarefa no momento." });
        }
    }

    public async Task<IActionResult> OnGetChamadosPermitidosAsync(int grupoId, int? cartaoId, string? membrosIds, bool compartilharGrupo)
    {
        var usuarioId = GetUsuarioLogadoId();
        if (usuarioId == null)
            return Unauthorized();

        var contexto = await ValidarMembroAsync(usuarioId.Value, grupoId);
        if (contexto == null)
            return Forbid();

        List<int> usuariosComAcesso;

        if (cartaoId is > 0)
        {
            var cartaoIdValor = cartaoId.Value;
            var cartao = await _context.CartoesTarefas
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == cartaoIdValor && c.GrupoId == grupoId);

            if (cartao == null)
                return NotFound(new { success = false, message = "Cartao não encontrado." });

            var membrosAtuais = await _context.CartoesTarefasUsuarios
                .AsNoTracking()
                .Where(x => x.CartaoTarefaId == cartao.Id)
                .Select(x => x.UsuarioId)
                .ToListAsync();

            if (!PodeVerCartao(cartao, usuarioId.Value, membrosAtuais))
                return Forbid();
        }

        if (compartilharGrupo)
        {
            usuariosComAcesso = await _context.UsuariosGrupos
                .AsNoTracking()
                .Where(x => x.GrupoId == grupoId && x.Ativo)
                .Select(x => x.UsuarioId)
                .ToListAsync();
        }
        else
        {
            usuariosComAcesso = ParseIds(membrosIds).Append(usuarioId.Value).Distinct().ToList();
        }

        var chamados = await ObterChamadosPermitidosAsync(grupoId, usuariosComAcesso);
        return new JsonResult(new { success = true, chamados });
    }

    public async Task<IActionResult> OnGetMembrosCartaoAsync(int grupoId)
    {
        var usuarioId = GetUsuarioLogadoId();
        if (usuarioId == null)
            return Unauthorized();

        var contexto = await ValidarMembroAsync(usuarioId.Value, grupoId);
        if (contexto == null)
            return Forbid();

        var membros = await ObterMembrosAsync(grupoId);
        return new JsonResult(new { success = true, membros });
    }

    public async Task<IActionResult> OnGetMembrosMencaoAsync(int grupoId, int? cartaoId, string? termo)
    {
        var usuarioId = GetUsuarioLogadoId();
        if (usuarioId == null)
            return Unauthorized();

        var contexto = await ValidarMembroAsync(usuarioId.Value, grupoId);
        if (contexto == null)
            return new JsonResult(new { success = false, message = "Voce nao pertence a este grupo." });

        IEnumerable<int>? usuariosPermitidos = null;
        if (cartaoId.HasValue && cartaoId.Value > 0)
        {
            var cartao = await _context.CartoesTarefas
                .FirstOrDefaultAsync(c => c.Id == cartaoId.Value && c.GrupoId == grupoId);

            if (cartao == null || !await PodeVerCartaoAsync(cartao, usuarioId.Value))
                return new JsonResult(new { success = false, message = "Voce nao tem permissao para acessar esta tarefa." });

            usuariosPermitidos = await ObterUsuariosPermitidosMencaoCartaoAsync(cartao);
        }

        var membros = await _mencaoService.BuscarMembrosAsync(grupoId, usuarioId.Value, termo, usuariosPermitidos);
        return new JsonResult(new { success = true, dados = membros });
    }

    public async Task<IActionResult> OnGetChamadosVisivelParaTodosAsync(int cartaoId, int grupoId, string? termo)
    {
        var usuarioId = GetUsuarioLogadoId();
        if (usuarioId == null)
            return Unauthorized();

        var contexto = await ValidarMembroAsync(usuarioId.Value, grupoId);
        if (contexto == null)
            return Forbid();

        var cartao = await _context.CartoesTarefas
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == cartaoId && c.GrupoId == grupoId);

        if (cartao == null)
            return NotFound(new { success = false, message = "Cartao não encontrado." });

        var membrosAtuais = await _context.CartoesTarefasUsuarios
            .AsNoTracking()
            .Where(x => x.CartaoTarefaId == cartao.Id)
            .Select(x => x.UsuarioId)
            .ToListAsync();

        if (!PodeVerCartao(cartao, usuarioId.Value, membrosAtuais))
            return Forbid();

        var vinculados = await _context.CartoesTarefasChamados
            .AsNoTracking()
            .Where(x => x.CartaoTarefaId == cartao.Id && x.Ativo)
            .Select(x => x.ChamadoId)
            .ToListAsync();

        var usuariosComAcesso = await ObterUsuariosComAcessoCartaoAsync(cartao);
        var chamadosPermitidos = await ObterChamadosPermitidosAsync(
            grupoId,
            usuariosComAcesso,
            limitar: true,
            termo: termo,
            idsSempreIncluir: vinculados);

        var vinculadosSet = vinculados.ToHashSet();
        var chamados = chamadosPermitidos.Select(c => new
        {
            c.Id,
            c.NumeroChamadoGrupo,
            c.Titulo,
            vinculado = vinculadosSet.Contains(c.Id)
        });

        return new JsonResult(new { success = true, chamados });
    }

    public async Task<IActionResult> OnPostSalvarCartaoAsync([FromBody] SalvarCartaoRequest request)
    {
        var usuarioId = GetUsuarioLogadoId();
        if (usuarioId == null)
            return Unauthorized();

        if (request == null || request.GrupoId <= 0)
            return BadRequest(new { success = false, message = "Tarefa invalida." });

        var contexto = await ValidarMembroComPermissaoAsync(usuarioId.Value, request.GrupoId);
        if (contexto == null)
            return Forbid();

        var titulo = request.Titulo?.Trim();
        if (string.IsNullOrWhiteSpace(titulo) || titulo.Length > 150)
            return BadRequest(new { success = false, message = "Título inválido." });

        if (!TryParseNullableEnum<PrioridadeChamado>(request.Prioridade, out var prioridade))
            return BadRequest(new { success = false, message = "Prioridade invalida." });

        if (!TryParseNullableEnum<CriticidadeChamado>(request.Criticidade, out var criticidade))
            return BadRequest(new { success = false, message = "Criticidade invalida." });

        if (!TryParseNullableEnum<UrgenciaChamado>(request.Urgencia, out var urgencia))
            return BadRequest(new { success = false, message = "Urgência inválida." });

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
                        var cartao = await SalvarCartaoTransacionalAsync(
                            request,
                            usuarioId.Value,
                            titulo,
                            prioridade,
                            criticidade,
                            urgencia,
                            Enum.TryParse<StatusCartaoTarefa>(request.Status, out var statusSolicitado)
                                ? statusSolicitado
                                : null);

                        await transaction.CommitAsync();

                        var membrosVisiveis = await ObterMembrosVisiveisParaCardAsync(
                            request.GrupoId,
                            cartao.Privado,
                            (request.MembrosIds ?? new List<int>()).Append(cartao.CriadorId));

                        return (IActionResult)new JsonResult(new
                        {
                            success = true,
                            id = cartao.Id,
                            membrosVisiveis
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
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Erro de banco ao salvar cartao no grupo {GrupoId}.", request.GrupoId);
                return BadRequest(new { success = false, message = "Não foi possível salvar o cartão no momento." });
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao salvar cartao no grupo {GrupoId}.", request.GrupoId);
                return BadRequest(new { success = false, message = "Não foi possível salvar o cartão no momento." });
            }
        }

        return BadRequest(new { success = false, message = "Não foi possível salvar o cartão no momento." });
    }

    public async Task<IActionResult> OnPostSalvarMembrosCartaoAsync([FromBody] SalvarMembrosCartaoRequest request)
    {
        var usuarioId = GetUsuarioLogadoId();
        if (usuarioId == null)
            return Unauthorized();

        if (request == null)
            return BadRequest(new { success = false, message = "Requisição inválida." });

        var contexto = await ValidarMembroComPermissaoAsync(usuarioId.Value, request.GrupoId);
        if (contexto == null)
            return Forbid();

        var membrosIds = request.MembrosIds?.Distinct().ToList() ?? new List<int>();
        var strategy = _context.Database.CreateExecutionStrategy();

        try
        {
            return await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _context.Database.BeginTransactionAsync();

                try
                {
                    var cartao = await _context.CartoesTarefas
                        .FirstOrDefaultAsync(c => c.Id == request.CartaoId && c.GrupoId == request.GrupoId);

                    if (cartao == null)
                        return (IActionResult)NotFound(new { success = false, message = "Cartao não encontrado." });

                    var atuais = await _context.CartoesTarefasUsuarios
                        .Where(x => x.CartaoTarefaId == cartao.Id)
                        .ToListAsync();

                    var membrosAtuais = atuais.Select(x => x.UsuarioId).ToList();
                    if (!PodeEditarCartao(cartao, usuarioId.Value, membrosAtuais))
                        return (IActionResult)Forbid();

                    var idsSolicitados = request.GrupoTodo ? new List<int>() : membrosIds;
                    var idsValidos = idsSolicitados.Any()
                        ? await _context.UsuariosGrupos
                            .AsNoTracking()
                            .Where(x => x.Ativo && x.GrupoId == request.GrupoId && idsSolicitados.Contains(x.UsuarioId))
                            .Select(x => x.UsuarioId)
                            .Distinct()
                            .ToListAsync()
                        : new List<int>();

                    if (idsValidos.Count != idsSolicitados.Count)
                        return (IActionResult)BadRequest(new { success = false, message = "Um ou mais membros são inválidos." });

                    var idsDesejados = idsValidos
                        .Append(cartao.CriadorId)
                        .Distinct()
                        .ToHashSet();

                    cartao.Privado = !request.GrupoTodo;
                    cartao.DataAtualizacao = DateTime.UtcNow;

                    _context.CartoesTarefasUsuarios.RemoveRange(
                        atuais.Where(x => x.UsuarioId != cartao.CriadorId && !idsDesejados.Contains(x.UsuarioId)));

                    foreach (var id in idsDesejados.Where(id => atuais.All(x => x.UsuarioId != id)))
                    {
                        _context.CartoesTarefasUsuarios.Add(new CartaoTarefaUsuario
                        {
                            CartaoTarefaId = cartao.Id,
                            UsuarioId = id,
                            TipoParticipacao = TipoParticipacaoCartaoTarefa.Participante,
                            Permissao = PermissaoCartaoTarefa.Editor,
                            DataAdicao = DateTime.UtcNow,
                            AdicionadoPorUsuarioId = usuarioId.Value
                        });
                    }

                    await DesvincularchamadosSemPermissaoAsync(cartao.Id, request.GrupoId, idsDesejados.ToList(), usuarioId.Value);
                    RegistrarHistorico(cartao.Id, usuarioId.Value, "Membros atualizados");
                    await _context.SaveChangesAsync();
                    var membrosVisiveis = await ObterMembrosVisiveisParaCardAsync(
                        request.GrupoId,
                        cartao.Privado,
                        idsDesejados);
                    var chamadosVinculados = await ObterChamadosVinculadosVisiveisAsync(cartao);
                    await transaction.CommitAsync();

                    return (IActionResult)new JsonResult(new
                    {
                        success = true,
                        membrosVisiveis,
                        chamadosVinculados
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
            _logger.LogError(ex, "Erro ao salvar membros do cartao {CartaoId} no grupo {GrupoId}.", request.CartaoId, request.GrupoId);
            return BadRequest(new { success = false, message = "Não foi possível salvar os membros no momento." });
        }
    }

    public async Task<IActionResult> OnPostSalvarChamadosCartaoAsync([FromBody] SalvarChamadosCartaoRequest request)
    {
        var usuarioId = GetUsuarioLogadoId();
        if (usuarioId == null)
            return Unauthorized();

        if (request == null)
            return BadRequest(new { success = false, message = "Requisição inválida." });

        var contexto = await ValidarMembroComPermissaoAsync(usuarioId.Value, request.GrupoId);
        if (contexto == null)
            return Forbid();

        var chamadosIds = request.ChamadosIds?.Distinct().ToList() ?? new List<int>();
        var strategy = _context.Database.CreateExecutionStrategy();

        try
        {
            return await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _context.Database.BeginTransactionAsync();

                try
                {
                    var cartao = await _context.CartoesTarefas
                        .FirstOrDefaultAsync(c => c.Id == request.CartaoId && c.GrupoId == request.GrupoId);

                    if (cartao == null)
                        return (IActionResult)NotFound(new { success = false, message = "Cartao não encontrado." });

                    var membrosAtuais = await _context.CartoesTarefasUsuarios
                        .AsNoTracking()
                        .Where(x => x.CartaoTarefaId == cartao.Id)
                        .Select(x => x.UsuarioId)
                        .ToListAsync();

                    if (!PodeEditarCartao(cartao, usuarioId.Value, membrosAtuais))
                        return (IActionResult)Forbid();

                    var usuariosComAcesso = await ObterUsuariosComAcessoCartaoAsync(cartao);
                    var chamadosPermitidos = chamadosIds.Any()
                        ? (await ObterChamadosPermitidosAsync(request.GrupoId, usuariosComAcesso, chamadosIds)).Select(c => c.Id).ToHashSet()
                        : new HashSet<int>();

                    if (chamadosPermitidos.Count != chamadosIds.Count)
                        return (IActionResult)BadRequest(new { success = false, message = "Um ou mais chamados não podem ser vinculados a esta tarefa." });

                    var atuais = await _context.CartoesTarefasChamados
                        .Where(x => x.CartaoTarefaId == cartao.Id)
                        .ToListAsync();

                    foreach (var vinculo in atuais)
                    {
                        var deveFicarAtivo = chamadosPermitidos.Contains(vinculo.ChamadoId);
                        if (vinculo.Ativo != deveFicarAtivo)
                        {
                            vinculo.Ativo = deveFicarAtivo;
                            vinculo.DataDesvinculo = deveFicarAtivo ? null : DateTime.UtcNow;
                            vinculo.DesvinculadoPorUsuarioId = deveFicarAtivo ? null : usuarioId.Value;
                        }
                    }

                    foreach (var chamadoId in chamadosPermitidos.Where(id => atuais.All(x => x.ChamadoId != id)))
                    {
                        _context.CartoesTarefasChamados.Add(new CartaoTarefaChamado
                        {
                            CartaoTarefaId = cartao.Id,
                            ChamadoId = chamadoId,
                            TipoRelacao = TipoRelacaoCartaoChamado.Relacionada,
                            Ativo = true,
                            DataVinculo = DateTime.UtcNow,
                            VinculadoPorUsuarioId = usuarioId.Value
                        });
                    }

                    RegistrarHistorico(cartao.Id, usuarioId.Value, "Chamados vinculados atualizados");
                    await _context.SaveChangesAsync();
                    var chamadosVinculados = await ObterChamadosVinculadosVisiveisAsync(cartao);
                    await transaction.CommitAsync();

                    return (IActionResult)new JsonResult(new
                    {
                        success = true,
                        chamadosVinculados
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
            _logger.LogError(ex, "Erro ao salvar chamados do cartao {CartaoId} no grupo {GrupoId}.", request.CartaoId, request.GrupoId);
            return BadRequest(new { success = false, message = "Não foi possível salvar os chamados no momento." });
        }
    }

    public async Task<IActionResult> OnPostReordenarCartoesAsync([FromBody] ReordenarCartoesRequest request)
    {
        var usuarioId = GetUsuarioLogadoId();
        if (usuarioId == null)
            return Unauthorized();

        if (request == null || request.GrupoId <= 0 || request.Colunas == null)
            return BadRequest(new { success = false, message = "Ordem dos cartoes invalida." });
        if (request.Colunas.Any(c => c == null || c.CartoesIds == null))
            return BadRequest(new { success = false, message = "Ordem dos cartoes invalida." });

        var contexto = await ValidarMembroComPermissaoAsync(usuarioId.Value, request.GrupoId);
        if (contexto == null)
            return Forbid();

        var colunaIds = request.Colunas.Select(c => c.ColunaId).Distinct().ToList();
        var colunasValidas = await _context.ColunasQuadro
            .AsNoTracking()
            .Where(c => colunaIds.Contains(c.Id) && c.Ativa && c.Quadro.GrupoId == request.GrupoId)
            .Select(c => c.Id)
            .ToListAsync();

        if (colunasValidas.Count != colunaIds.Count)
            return BadRequest(new { success = false, message = "Lista invalida." });

        var colunasValidasSet = colunasValidas.ToHashSet();
        var ids = request.Colunas.SelectMany(c => c.CartoesIds).Distinct().ToList();
        if (!ids.Any())
            return new JsonResult(new { success = true });

        var cartoes = await _context.CartoesTarefas
            .Where(c => ids.Contains(c.Id) && c.GrupoId == request.GrupoId)
            .ToDictionaryAsync(c => c.Id);

        var membrosCartoes = await _context.CartoesTarefasUsuarios
            .AsNoTracking()
            .Where(x => ids.Contains(x.CartaoTarefaId))
            .Select(x => new { x.CartaoTarefaId, x.UsuarioId })
            .ToListAsync();

        var membrosPorCartao = membrosCartoes
            .GroupBy(x => x.CartaoTarefaId)
            .ToDictionary(
                x => x.Key,
                x => x.Select(m => m.UsuarioId).ToList());

        var strategy = _context.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                foreach (var colunaRequest in request.Colunas)
                {
                    if (!colunasValidasSet.Contains(colunaRequest.ColunaId))
                        return (IActionResult)BadRequest(new { success = false, message = "Lista invalida." });

                    for (var i = 0; i < colunaRequest.CartoesIds.Count; i++)
                    {
                        var cartaoId = colunaRequest.CartoesIds[i];
                        if (!cartoes.TryGetValue(cartaoId, out var cartao))
                            continue;

                        membrosPorCartao.TryGetValue(cartao.Id, out var membrosAtuais);
                        membrosAtuais ??= new List<int>();

                        if (!PodeVerCartao(cartao, usuarioId.Value, membrosAtuais))
                            return (IActionResult)Forbid();
                        if (!PodeEditarCartao(cartao, usuarioId.Value, membrosAtuais))
                            return (IActionResult)Forbid();

                        cartao.ColunaId = colunaRequest.ColunaId;
                        cartao.OrdemColuna = (i + 1) * OrdemBase;
                        cartao.DataAtualizacao = DateTime.UtcNow;
                    }
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return (IActionResult)new JsonResult(new { success = true });
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        });
    }

    public async Task<IActionResult> OnPostAdicionarComentarioAsync([FromBody] AdicionarComentarioRequest request)
    {
        var usuarioId = GetUsuarioLogadoId();
        if (usuarioId == null)
            return Unauthorized();

        if (request == null || request.CartaoId <= 0 || request.GrupoId <= 0)
            return BadRequest(new { success = false, message = "Comentario invalido." });

        var cartao = await _context.CartoesTarefas
            .FirstOrDefaultAsync(c => c.Id == request.CartaoId && c.GrupoId == request.GrupoId);

        if (cartao == null)
            return NotFound(new { success = false, message = "Cartao não encontrado." });

        var contexto = await ValidarMembroComPermissaoAsync(usuarioId.Value, cartao.GrupoId);
        if (contexto == null || !await PodeVerCartaoAsync(cartao, usuarioId.Value))
            return Forbid();

        var texto = request.Mensagem?.Trim();
        if (string.IsNullOrWhiteSpace(texto) || texto.Length > LimiteCaracteresComentario)
            return BadRequest(new { success = false, message = $"Comentário inválido. Use até {LimiteCaracteresComentario} caracteres." });

        var strategy = _context.Database.CreateExecutionStrategy();

        try
        {
            return await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _context.Database.BeginTransactionAsync();

                try
                {
                    var comentario = new ComentarioTarefa
                    {
                        CartaoTarefaId = cartao.Id,
                        UsuarioId = usuarioId.Value,
                        Mensagem = texto,
                        DataCriacao = DateTime.UtcNow
                    };

                    _context.ComentariosTarefas.Add(comentario);
                    await _context.SaveChangesAsync();
                    await _mencaoService.SincronizarMencoesAsync(
                        cartao.GrupoId,
                        usuarioId.Value,
                        "ComentarioTarefa",
                        comentario.Id,
                        "Comentario",
                        comentario.Mensagem,
                        TipoNotificacao.Tarefa,
                        "Voce foi mencionado em um comentario",
                        $"comentario da tarefa #{cartao.NumeroCartaoGrupo}",
                        $"/Menu/Tasks?grupoId={cartao.GrupoId}",
                        await ObterUsuariosPermitidosMencaoCartaoAsync(cartao));

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    return (IActionResult)new JsonResult(new { success = true });
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
            _logger.LogError(ex, "Erro ao adicionar comentario na tarefa {CartaoId}.", request.CartaoId);
            return BadRequest(new { success = false, message = "Nao foi possivel adicionar o comentario." });
        }
    }

    public async Task<IActionResult> OnPostReordenarListasAsync([FromBody] ReordenarListasRequest request)
    {
        var usuarioId = GetUsuarioLogadoId();
        if (usuarioId == null)
            return Unauthorized();

        if (request == null)
            return BadRequest(new { success = false, message = "Requisição inválida." });

        var contexto = await ValidarMembroComPermissaoAsync(usuarioId.Value, request.GrupoId);
        if (contexto == null)
            return Forbid();

        var colunasIds = request.ColunasIds
            .Where(id => id > 0)
            .Distinct()
            .ToList();

        if (colunasIds.Count != request.ColunasIds.Count || colunasIds.Count == 0)
            return BadRequest(new { success = false, message = "Ordem das listas invalida." });

        var colunas = await _context.ColunasQuadro
            .Where(c => colunasIds.Contains(c.Id) && c.Ativa && c.Quadro.GrupoId == request.GrupoId)
            .ToListAsync();

        if (colunas.Count != colunasIds.Count)
            return BadRequest(new { success = false, message = "Lista invalida." });

        var quadroIds = colunas.Select(c => c.QuadroId).Distinct().ToList();
        if (quadroIds.Count != 1)
            return BadRequest(new { success = false, message = "As listas informadas não pertencem ao mesmo quadro." });

        var quadroId = quadroIds[0];
        var totalColunasAtivas = await _context.ColunasQuadro
            .CountAsync(c => c.QuadroId == quadroId && c.Ativa);

        if (totalColunasAtivas != colunasIds.Count)
            return BadRequest(new { success = false, message = "A lista de ordenacao esta desatualizada." });

        var colunasPorId = colunas.ToDictionary(c => c.Id);
        var strategy = _context.Database.CreateExecutionStrategy();

        try
        {
            return await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _context.Database.BeginTransactionAsync();

                try
                {
                    var maiorPosicao = await _context.ColunasQuadro
                        .Where(c => c.QuadroId == quadroId)
                        .MaxAsync(c => (decimal?)c.Posicao) ?? 0m;

                    for (var i = 0; i < colunasIds.Count; i++)
                    {
                        colunasPorId[colunasIds[i]].Posicao = maiorPosicao + ((i + 1) * OrdemBase);
                    }

                    await _context.SaveChangesAsync();

                    for (var i = 0; i < colunasIds.Count; i++)
                    {
                        colunasPorId[colunasIds[i]].Posicao = maiorPosicao + ((colunasIds.Count + i + 1) * OrdemBase);
                    }

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    return (IActionResult)new JsonResult(new { success = true });
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
            _logger.LogError(ex, "Erro ao reordenar listas no grupo {GrupoId}.", request.GrupoId);
            return BadRequest(new { success = false, message = "Não foi possível salvar a ordem das listas." });
        }
    }

    public async Task<IActionResult> OnPostAlternarConcluidoAsync([FromBody] AlternarConcluidoRequest request)
    {
        var usuarioId = GetUsuarioLogadoId();
        if (usuarioId == null)
            return Unauthorized();

        if (request == null || request.CartaoId <= 0 || request.GrupoId <= 0)
            return BadRequest(new { success = false, message = "Tarefa invalida." });

        var cartao = await _context.CartoesTarefas
            .FirstOrDefaultAsync(c => c.Id == request.CartaoId && c.GrupoId == request.GrupoId);

        if (cartao == null)
            return NotFound(new { success = false, message = "Cartao não encontrado." });

        var contexto = await ValidarMembroComPermissaoAsync(usuarioId.Value, cartao.GrupoId);
        if (contexto == null)
            return Forbid();

        var membrosAtuais = await _context.CartoesTarefasUsuarios
            .AsNoTracking()
            .Where(x => x.CartaoTarefaId == cartao.Id)
            .Select(x => x.UsuarioId)
            .ToListAsync();

        if (!PodeVerCartao(cartao, usuarioId.Value, membrosAtuais) ||
            !PodeEditarCartao(cartao, usuarioId.Value, membrosAtuais))
        {
            return Forbid();
        }

        var strategy = _context.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var statusAnterior = cartao.Status;
                var concluido = cartao.Status != StatusCartaoTarefa.Concluida;
                cartao.Status = concluido ? StatusCartaoTarefa.Concluida : StatusCartaoTarefa.Ativa;
                cartao.DataConclusao = concluido ? DateTime.UtcNow : null;
                cartao.PercentualConclusao = concluido ? 100m : 0m;
                cartao.DataAtualizacao = DateTime.UtcNow;
                await _slaPausaService.RegistrarTransicaoCartaoAsync(cartao, statusAnterior, cartao.Status, usuarioId.Value, DateTime.UtcNow);

                RegistrarHistorico(cartao.Id, usuarioId.Value, concluido ? "Cartao concluído" : "Cartao reaberto");
                await _context.SaveChangesAsync();

                await transaction.CommitAsync();

                return (IActionResult)new JsonResult(new { success = true, concluido });
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        });
    }

    public async Task<IActionResult> OnPostSalvarComoTemplateAsync([FromBody] SalvarComoTemplateRequest request)
    {
        var usuarioId = GetUsuarioLogadoId();
        if (usuarioId == null)
            return Unauthorized();

        var contexto = await ValidarMembroComPermissaoAsync(usuarioId.Value, request.GrupoId);
        if (contexto == null)
            return Forbid();

        var nome = request.NomeTemplate?.Trim();
        if (string.IsNullOrWhiteSpace(nome) || nome.Length > 100)
            return BadRequest(new { success = false, message = "Nome do template inválido." });

        var cartao = await _context.CartoesTarefas
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == request.CartaoId && c.GrupoId == request.GrupoId);

        if (cartao == null)
            return NotFound(new { success = false, message = "Cartao não encontrado." });

        if (!await PodeVerCartaoAsync(cartao, usuarioId.Value))
            return Forbid();

        var existe = await _context.TemplatesCartoesTarefas
            .AsNoTracking()
            .AnyAsync(t => t.GrupoId == request.GrupoId && t.Nome == nome);

        if (existe)
            return BadRequest(new { success = false, message = "Ja existe um template com este nome." });

        var template = new TemplateCartaoTarefa
        {
            GrupoId = request.GrupoId,
            CriadoPorUsuarioId = usuarioId.Value,
            Nome = nome,
            Descricao = cartao.Descricao,
            Prioridade = cartao.Prioridade,
            Criticidade = cartao.Criticidade,
            Urgencia = cartao.Urgencia,
            CorCapa = cartao.CorCapa,
            DataCriacao = DateTime.UtcNow,
            DataAtualizacao = DateTime.UtcNow,
            Ativo = true
        };

        var strategy = _context.Database.CreateExecutionStrategy();

        try
        {
            return await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _context.Database.BeginTransactionAsync();

                try
                {
                    _context.TemplatesCartoesTarefas.Add(template);
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    return (IActionResult)new JsonResult(new { success = true, id = template.Id, nome = template.Nome });
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
            return BadRequest(new { success = false, message = "Ja existe um template com este nome." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao salvar cartao {CartaoId} como template no grupo {GrupoId}.", request.CartaoId, request.GrupoId);
            return BadRequest(new { success = false, message = "Não foi possível salvar o template no momento." });
        }
    }

    public async Task<IActionResult> OnPostArquivarCartaoAsync([FromBody] ArquivarCartaoRequest request)
    {
        var usuarioId = GetUsuarioLogadoId();
        if (usuarioId == null)
            return Unauthorized();

        var contexto = await ValidarMembroComPermissaoAsync(usuarioId.Value, request.GrupoId);
        if (contexto == null)
            return Forbid();

        var strategy = _context.Database.CreateExecutionStrategy();

        try
        {
            return await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _context.Database.BeginTransactionAsync();

                try
                {
                    var cartao = await _context.CartoesTarefas
                        .FirstOrDefaultAsync(c => c.Id == request.CartaoId && c.GrupoId == request.GrupoId);

                    if (cartao == null)
                        return (IActionResult)NotFound(new { success = false, message = "Cartao não encontrado." });

                    var membrosAtuais = await _context.CartoesTarefasUsuarios
                        .AsNoTracking()
                        .Where(x => x.CartaoTarefaId == cartao.Id)
                        .Select(x => x.UsuarioId)
                        .ToListAsync();

                    if (!PodeEditarCartao(cartao, usuarioId.Value, membrosAtuais))
                        return (IActionResult)Forbid();

                    cartao.Status = StatusCartaoTarefa.Arquivada;
                    cartao.DataAtualizacao = DateTime.UtcNow;

                    RegistrarHistorico(cartao.Id, usuarioId.Value, "Cartao arquivado");
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    return (IActionResult)new JsonResult(new { success = true });
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
            _logger.LogError(ex, "Erro ao arquivar cartao {CartaoId} no grupo {GrupoId}.", request.CartaoId, request.GrupoId);
            return BadRequest(new { success = false, message = "Não foi possível arquivar o cartão no momento." });
        }
    }

    public async Task<IActionResult> OnPostRestaurarCartaoAsync([FromBody] ArquivarCartaoRequest request)
    {
        var usuarioId = GetUsuarioLogadoId();
        if (usuarioId == null)
            return Unauthorized();

        var contexto = await ValidarMembroComPermissaoAsync(usuarioId.Value, request.GrupoId);
        if (contexto == null)
            return Forbid();

        var strategy = _context.Database.CreateExecutionStrategy();

        try
        {
            return await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _context.Database.BeginTransactionAsync();

                try
                {
                    var cartao = await _context.CartoesTarefas
                        .FirstOrDefaultAsync(c => c.Id == request.CartaoId && c.GrupoId == request.GrupoId);

                    if (cartao == null)
                        return (IActionResult)NotFound(new { success = false, message = "Cartao não encontrado." });

                    var membrosAtuais = await _context.CartoesTarefasUsuarios
                        .AsNoTracking()
                        .Where(x => x.CartaoTarefaId == cartao.Id)
                        .Select(x => x.UsuarioId)
                        .ToListAsync();

                    if (!PodeEditarCartao(cartao, usuarioId.Value, membrosAtuais))
                        return (IActionResult)Forbid();

                    var coluna = await _context.ColunasQuadro
                        .FirstOrDefaultAsync(c => c.Id == cartao.ColunaId && c.Ativa && c.Quadro.GrupoId == request.GrupoId);

                    if (coluna == null)
                    {
                        coluna = await _context.ColunasQuadro
                            .Where(c => c.QuadroId == cartao.QuadroId && c.Ativa && c.Quadro.GrupoId == request.GrupoId)
                            .OrderBy(c => c.Posicao)
                            .FirstOrDefaultAsync();

                        if (coluna == null)
                        {
                            return (IActionResult)BadRequest(new
                            {
                                success = false,
                                message = "Crie uma lista ativa antes de restaurar este cartão."
                            });
                        }
                    }

                    cartao.ColunaId = coluna.Id;
                    cartao.Status = StatusCartaoTarefa.Ativa;
                    cartao.DataAtualizacao = DateTime.UtcNow;

                    RegistrarHistorico(cartao.Id, usuarioId.Value, "Cartao restaurado");
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    var membrosVisiveis = await ObterMembrosVisiveisParaCardAsync(
                        request.GrupoId,
                        cartao.Privado,
                        membrosAtuais.Append(cartao.CriadorId));
                    var chamadosVinculados = await ObterChamadosVinculadosVisiveisAsync(cartao);

                    return (IActionResult)new JsonResult(new
                    {
                        success = true,
                        colunaId = coluna.Id,
                        colunaNome = coluna.Nome,
                        membrosVisiveis,
                        chamadosVinculados
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
            _logger.LogError(ex, "Erro ao restaurar cartao {CartaoId} no grupo {GrupoId}.", request.CartaoId, request.GrupoId);
            return BadRequest(new { success = false, message = "Não foi possível restaurar o cartão no momento." });
        }
    }

    public async Task<IActionResult> OnGetCartoesArquivadosAsync(int grupoId)
    {
        var usuarioId = GetUsuarioLogadoId();
        if (usuarioId == null)
            return Unauthorized();

        var contexto = await ValidarMembroAsync(usuarioId.Value, grupoId);
        if (contexto == null)
            return Forbid();

        var cartoes = await (
            from cartao in _context.CartoesTarefas.AsNoTracking()
            join coluna in _context.ColunasQuadro.AsNoTracking()
                on cartao.ColunaId equals coluna.Id
            where cartao.GrupoId == grupoId &&
                  cartao.Status == StatusCartaoTarefa.Arquivada &&
                  (cartao.CriadorId == usuarioId.Value ||
                   !cartao.Privado ||
                   _context.CartoesTarefasUsuarios.Any(x => x.CartaoTarefaId == cartao.Id && x.UsuarioId == usuarioId.Value))
            orderby cartao.DataAtualizacao descending
            select new
            {
                id = cartao.Id,
                titulo = cartao.Titulo,
                nomeColuna = coluna.Ativa ? coluna.Nome : "Lista excluida",
                corCapa = cartao.CorCapa,
                dataArquivamento = cartao.DataAtualizacao
            })
            .Take(100)
            .ToListAsync();

        return new JsonResult(new { success = true, cartoes });
    }

    public async Task<IActionResult> OnGetTemplatesAsync(int grupoId)
    {
        var usuarioId = GetUsuarioLogadoId();
        if (usuarioId == null)
            return Unauthorized();

        var contexto = await ValidarMembroAsync(usuarioId.Value, grupoId);
        if (contexto == null)
            return Forbid();

        var templates = await _context.TemplatesCartoesTarefas
            .AsNoTracking()
            .Where(t => t.GrupoId == grupoId && t.Ativo)
            .OrderBy(t => t.Nome)
            .Select(t => new
            {
                id = t.Id,
                nome = t.Nome,
                corCapa = t.CorCapa,
                descricao = t.Descricao,
                prioridade = t.Prioridade != null ? t.Prioridade.ToString() : null,
                criticidade = t.Criticidade != null ? t.Criticidade.ToString() : null,
                urgencia = t.Urgencia != null ? t.Urgencia.ToString() : null
            })
            .ToListAsync();

        return new JsonResult(new { success = true, templates });
    }

    public async Task<IActionResult> OnPostCriarCartaoDeTemplateAsync([FromBody] CriarCartaoDeTemplateRequest request)
    {
        var usuarioId = GetUsuarioLogadoId();
        if (usuarioId == null)
            return Unauthorized();

        var contexto = await ValidarMembroComPermissaoAsync(usuarioId.Value, request.GrupoId);
        if (contexto == null)
            return Forbid();

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
                        var coluna = await _context.ColunasQuadro
                            .FirstOrDefaultAsync(c => c.Id == request.ColunaId && c.Ativa && c.Quadro.GrupoId == request.GrupoId);

                        if (coluna == null)
                            return (IActionResult)BadRequest(new { success = false, message = "Lista invalida." });

                        var template = await _context.TemplatesCartoesTarefas
                            .AsNoTracking()
                            .FirstOrDefaultAsync(t => t.Id == request.TemplateId && t.GrupoId == request.GrupoId && t.Ativo);

                        if (template == null)
                            return (IActionResult)NotFound(new { success = false, message = "Template não encontrado." });

                        var cartao = await CriarCartaoBaseAsync(coluna.QuadroId, coluna.Id, request.GrupoId, usuarioId.Value, template.Nome);
                        cartao.Descricao = template.Descricao;
                        cartao.Prioridade = template.Prioridade;
                        cartao.Criticidade = template.Criticidade;
                        cartao.Urgencia = template.Urgencia;
                        cartao.CorCapa = template.CorCapa;

                        _context.CartoesTarefas.Add(cartao);
                        _context.CartoesTarefasUsuarios.Add(new CartaoTarefaUsuario
                        {
                            CartaoTarefa = cartao,
                            UsuarioId = usuarioId.Value,
                            TipoParticipacao = TipoParticipacaoCartaoTarefa.Participante,
                            Permissao = PermissaoCartaoTarefa.Editor,
                            DataAdicao = DateTime.UtcNow,
                            AdicionadoPorUsuarioId = usuarioId.Value
                        });
                        _context.HistoricoTarefas.Add(new HistoricoTarefa
                        {
                            CartaoTarefa = cartao,
                            UsuarioId = usuarioId.Value,
                            TipoAcao = "Cartao criado a partir de template",
                            DataAcao = DateTime.UtcNow
                        });
                        await _context.SaveChangesAsync();
                        await transaction.CommitAsync();

                        return (IActionResult)new JsonResult(new
                        {
                            success = true,
                            id = cartao.Id,
                            numeroCartaoGrupo = cartao.NumeroCartaoGrupo,
                            titulo = cartao.Titulo,
                            colunaId = cartao.ColunaId,
                            corCapa = cartao.CorCapa,
                            compartilharGrupo = !cartao.Privado
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
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Erro de banco ao criar cartao pelo template {TemplateId} no grupo {GrupoId}.", request.TemplateId, request.GrupoId);
                return BadRequest(new { success = false, message = "Não foi possível criar o cartão pelo template no momento." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao criar cartao pelo template {TemplateId} no grupo {GrupoId}.", request.TemplateId, request.GrupoId);
                return BadRequest(new { success = false, message = "Não foi possível criar o cartão pelo template no momento." });
            }
        }

        return BadRequest(new { success = false, message = "Não foi possível criar o cartão pelo template no momento." });
    }

    public async Task<IActionResult> OnPostCriarTemplateAsync([FromBody] EditarTemplateRequest request)
    {
        var usuarioId = GetUsuarioLogadoId();
        if (usuarioId == null)
            return Unauthorized();

        if (request == null)
            return BadRequest(new { success = false, message = "Requisição inválida." });

        var contexto = await ValidarMembroComPermissaoAsync(usuarioId.Value, request.GrupoId);
        if (contexto == null)
            return Forbid();

        var nome = request.Nome?.Trim();
        if (string.IsNullOrWhiteSpace(nome) || nome.Length > 100)
            return BadRequest(new { success = false, message = "Nome do template inválido." });

        if (!TryParseNullableEnum<PrioridadeChamado>(request.Prioridade, out var prioridade) ||
            !TryParseNullableEnum<CriticidadeChamado>(request.Criticidade, out var criticidade) ||
            !TryParseNullableEnum<UrgenciaChamado>(request.Urgencia, out var urgencia))
            return BadRequest(new { success = false, message = "Dados do template inválidos." });

        var existe = await _context.TemplatesCartoesTarefas
            .AsNoTracking()
            .AnyAsync(t => t.GrupoId == request.GrupoId && t.Nome == nome);

        if (existe)
            return BadRequest(new { success = false, message = "Ja existe um template com este nome." });

        var template = new TemplateCartaoTarefa
        {
            GrupoId = request.GrupoId,
            CriadoPorUsuarioId = usuarioId.Value,
            Nome = nome,
            Descricao = string.IsNullOrWhiteSpace(request.Descricao) ? null : request.Descricao.Trim(),
            Prioridade = prioridade,
            Criticidade = criticidade,
            Urgencia = urgencia,
            CorCapa = NormalizarCor(request.CorCapa),
            DataCriacao = DateTime.UtcNow,
            DataAtualizacao = DateTime.UtcNow,
            Ativo = true
        };

        var strategy = _context.Database.CreateExecutionStrategy();

        try
        {
            return await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _context.Database.BeginTransactionAsync();

                try
                {
                    _context.TemplatesCartoesTarefas.Add(template);
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    return (IActionResult)new JsonResult(new { success = true, id = template.Id, nome = template.Nome });
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
            return BadRequest(new { success = false, message = "Ja existe um template com este nome." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao criar template no grupo {GrupoId}.", request.GrupoId);
            return BadRequest(new { success = false, message = "Não foi possível salvar o template no momento." });
        }
    }

    public async Task<IActionResult> OnPostEditarTemplateAsync([FromBody] EditarTemplateRequest request)
    {
        var usuarioId = GetUsuarioLogadoId();
        if (usuarioId == null)
            return Unauthorized();

        var contexto = await ValidarMembroComPermissaoAsync(usuarioId.Value, request.GrupoId);
        if (contexto == null)
            return Forbid();

        var nome = request.Nome?.Trim();
        if (request.TemplateId <= 0 || string.IsNullOrWhiteSpace(nome) || nome.Length > 100)
            return BadRequest(new { success = false, message = "Nome do template inválido." });

        if (!TryParseNullableEnum<PrioridadeChamado>(request.Prioridade, out var prioridade) ||
            !TryParseNullableEnum<CriticidadeChamado>(request.Criticidade, out var criticidade) ||
            !TryParseNullableEnum<UrgenciaChamado>(request.Urgencia, out var urgencia))
            return BadRequest(new { success = false, message = "Dados do template inválidos." });

        var template = await _context.TemplatesCartoesTarefas
            .FirstOrDefaultAsync(t => t.Id == request.TemplateId && t.GrupoId == request.GrupoId && t.Ativo);

        if (template == null)
            return NotFound(new { success = false, message = "Template não encontrado." });

        var existe = await _context.TemplatesCartoesTarefas
            .AsNoTracking()
            .AnyAsync(t => t.GrupoId == request.GrupoId && t.Id != request.TemplateId && t.Nome == nome);

        if (existe)
            return BadRequest(new { success = false, message = "Ja existe um template com este nome." });

        template.Nome = nome;
        template.Descricao = string.IsNullOrWhiteSpace(request.Descricao) ? null : request.Descricao.Trim();
        template.Prioridade = prioridade;
        template.Criticidade = criticidade;
        template.Urgencia = urgencia;
        template.CorCapa = NormalizarCor(request.CorCapa);
        template.DataAtualizacao = DateTime.UtcNow;

        var strategy = _context.Database.CreateExecutionStrategy();

        try
        {
            return await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _context.Database.BeginTransactionAsync();

                try
                {
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    return (IActionResult)new JsonResult(new { success = true });
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
            return BadRequest(new { success = false, message = "Ja existe um template com este nome." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao editar template {TemplateId} no grupo {GrupoId}.", request.TemplateId, request.GrupoId);
            return BadRequest(new { success = false, message = "Não foi possível salvar o template no momento." });
        }
    }

    public async Task<IActionResult> OnPostExcluirTemplateAsync([FromBody] ExcluirTemplateRequest request)
    {
        var usuarioId = GetUsuarioLogadoId();
        if (usuarioId == null)
            return Unauthorized();

        if (request == null)
            return BadRequest(new { success = false, message = "Requisição inválida." });

        var contexto = await ValidarMembroComPermissaoAsync(usuarioId.Value, request.GrupoId);
        if (contexto == null)
            return Forbid();

        var template = await _context.TemplatesCartoesTarefas
            .FirstOrDefaultAsync(t => t.Id == request.TemplateId && t.GrupoId == request.GrupoId);

        if (template == null)
            return NotFound(new { success = false, message = "Template não encontrado." });

        var strategy = _context.Database.CreateExecutionStrategy();

        try
        {
            return await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _context.Database.BeginTransactionAsync();

                try
                {
                    _context.TemplatesCartoesTarefas.Remove(template);
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    return (IActionResult)new JsonResult(new { success = true });
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
            _logger.LogError(ex, "Erro ao excluir template {TemplateId} no grupo {GrupoId}.", request.TemplateId, request.GrupoId);
            return BadRequest(new { success = false, message = "Não foi possível excluir o template no momento." });
        }
    }

    private async Task<IActionResult> AtualizarItemChecklistAsync(int itemId, int grupoId, string? descricao, bool? concluido)
    {
        var usuarioId = GetUsuarioLogadoId();
        if (usuarioId == null)
            return Unauthorized();

        var item = await _context.ChecklistItensTarefas
            .Include(i => i.Checklist)
            .ThenInclude(c => c.CartaoTarefa)
            .FirstOrDefaultAsync(i => i.Id == itemId && i.Checklist.CartaoTarefa.GrupoId == grupoId);
        if (item == null)
            return NotFound(new { success = false, message = "Item nao encontrado." });

        var cartaoId = item.Checklist.CartaoTarefaId;
        var acesso = await ObterCartaoComAcessoAsync(cartaoId, grupoId, usuarioId.Value, exigirEdicao: true);
        if (acesso.Resultado != null)
            return acesso.Resultado;

        var texto = descricao?.Trim();
        if (descricao != null && (string.IsNullOrWhiteSpace(texto) || texto.Length > 255))
            return BadRequest(new { success = false, message = "Item invalido." });

        var strategy = _context.Database.CreateExecutionStrategy();
        try
        {
            return await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    if (descricao != null)
                        item.Descricao = texto!;
                    if (concluido.HasValue)
                    {
                        item.Concluido = concluido.Value;
                        item.ConcluidoPorUsuarioId = concluido.Value ? usuarioId.Value : null;
                        item.DataConclusao = concluido.Value ? DateTime.UtcNow : null;
                    }

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    var checklists = await ObterChecklistsCartaoAsync(cartaoId);
                    return (IActionResult)new JsonResult(new { success = true, dados = checklists });
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
            _logger.LogError(ex, "Erro ao atualizar item {ItemId}.", itemId);
            return BadRequest(new { success = false, message = "Nao foi possivel atualizar o item." });
        }
    }

    private async Task<(CartaoTarefa? Cartao, IActionResult? Resultado)> ObterCartaoComAcessoAsync(int cartaoId, int grupoId, int usuarioId, bool exigirEdicao)
    {
        if (cartaoId <= 0 || grupoId <= 0)
            return (null, BadRequest(new { success = false, message = "Tarefa invalida." }));

        var contexto = exigirEdicao
            ? await ValidarMembroComPermissaoAsync(usuarioId, grupoId)
            : await ValidarMembroAsync(usuarioId, grupoId);
        if (contexto == null)
            return (null, Forbid());

        var cartao = await _context.CartoesTarefas
            .FirstOrDefaultAsync(c => c.Id == cartaoId && c.GrupoId == grupoId);
        if (cartao == null)
            return (null, NotFound(new { success = false, message = "Tarefa nao encontrada." }));

        var membros = await _context.CartoesTarefasUsuarios
            .AsNoTracking()
            .Where(x => x.CartaoTarefaId == cartao.Id)
            .Select(x => x.UsuarioId)
            .ToListAsync();

        if (!PodeVerCartao(cartao, usuarioId, membros))
            return (null, Forbid());
        if (exigirEdicao && !PodeEditarCartao(cartao, usuarioId, membros))
            return (null, Forbid());

        return (cartao, null);
    }

    private async Task<(AnexoTarefa? Anexo, IActionResult? Resultado)> ObterAnexoComAcessoAsync(int anexoId, int grupoId, bool exigirEdicao)
    {
        var usuarioId = GetUsuarioLogadoId();
        if (usuarioId == null)
            return (null, Unauthorized());

        var anexo = await _context.AnexosTarefas
            .Include(a => a.CartaoTarefa)
            .FirstOrDefaultAsync(a => a.Id == anexoId && a.CartaoTarefa.GrupoId == grupoId);
        if (anexo == null)
            return (null, NotFound(new { success = false, message = "Anexo nao encontrado." }));

        var acesso = await ObterCartaoComAcessoAsync(anexo.CartaoTarefaId, grupoId, usuarioId.Value, exigirEdicao);
        if (acesso.Resultado != null)
            return (null, acesso.Resultado);

        return (anexo, null);
    }

    private async Task<List<EtiquetaTarefaDto>> ObterEtiquetasUsuarioAsync(int grupoId, int usuarioId)
    {
        return await _context.EtiquetasTarefas
            .AsNoTracking()
            .Where(e => e.GrupoId == grupoId && e.UsuarioId == usuarioId)
            .OrderBy(e => e.Nome)
            .Select(e => new EtiquetaTarefaDto(e.Id, e.Nome, e.Cor))
            .ToListAsync();
    }

    private async Task<List<EtiquetaTarefaDto>> ObterEtiquetasAplicadasAsync(int cartaoId, int usuarioId)
    {
        return await (
            from vinculo in _context.CartoesTarefasEtiquetas.AsNoTracking()
            join etiqueta in _context.EtiquetasTarefas.AsNoTracking()
                on vinculo.EtiquetaId equals etiqueta.Id
            where vinculo.CartaoTarefaId == cartaoId && etiqueta.UsuarioId == usuarioId
            orderby etiqueta.Nome
            select new EtiquetaTarefaDto(etiqueta.Id, etiqueta.Nome, etiqueta.Cor))
            .ToListAsync();
    }

    private async Task<List<ChecklistTarefaDto>> ObterChecklistsCartaoAsync(int cartaoId)
    {
        var checklists = await _context.ChecklistsTarefas
            .AsNoTracking()
            .Where(c => c.CartaoTarefaId == cartaoId)
            .OrderBy(c => c.Posicao)
            .Select(c => new ChecklistTarefaDto
            {
                Id = c.Id,
                Titulo = c.Titulo,
                Itens = new List<ChecklistItemTarefaDto>()
            })
            .ToListAsync();

        var checklistIds = checklists.Select(c => c.Id).ToList();
        if (checklistIds.Count == 0)
            return checklists;

        var itens = await _context.ChecklistItensTarefas
            .AsNoTracking()
            .Where(i => checklistIds.Contains(i.ChecklistId))
            .OrderBy(i => i.Posicao)
            .Select(i => new ChecklistItemTarefaDto
            {
                Id = i.Id,
                ChecklistId = i.ChecklistId,
                Descricao = i.Descricao,
                Concluido = i.Concluido
            })
            .ToListAsync();

        var itensPorChecklist = itens.GroupBy(i => i.ChecklistId).ToDictionary(g => g.Key, g => g.ToList());
        foreach (var checklist in checklists)
        {
            checklist.Itens = itensPorChecklist.TryGetValue(checklist.Id, out var itensChecklist)
                ? itensChecklist
                : new List<ChecklistItemTarefaDto>();
        }

        return checklists;
    }

    private async Task<List<AnexoTarefaDto>> ObterAnexosCartaoAsync(int cartaoId)
    {
        var anexos = await (
            from anexo in _context.AnexosTarefas.AsNoTracking()
            join usuario in _context.Usuarios.AsNoTracking()
                on anexo.UsuarioId equals usuario.Id
            where anexo.CartaoTarefaId == cartaoId
            orderby anexo.DataUpload descending
            select new AnexoTarefaDto
            {
                Id = anexo.Id,
                NomeOriginal = anexo.NomeOriginal,
                TipoArquivo = anexo.TipoArquivo,
                Extensao = anexo.Extensao,
                TamanhoBytes = anexo.TamanhoBytes,
                EhImagem = anexo.EhImagem,
                Usuario = usuario.NomeUsuario,
                DataUpload = ParaDataHoraRegionalIso(anexo.DataUpload)
            })
            .ToListAsync();

        foreach (var anexo in anexos)
        {
            var regra = _anexoUploadService.ObterRegra(anexo.Extensao);
            anexo.TipoVisualizacao = regra?.TipoVisualizacao ?? "download";
            anexo.PodeVisualizar = regra?.PermiteVisualizacao ?? false;
        }

        return anexos;
    }

    private string ObterCaminhoFisicoAnexoTarefa(string caminhoRelativo)
    {
        var partes = caminhoRelativo
            .Replace('\\', '/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(parte => Path.GetFileName(parte) ?? string.Empty)
            .Where(parte => !string.IsNullOrWhiteSpace(parte))
            .ToArray();

        return Path.Combine(new[] { _environment.ContentRootPath, "uploads_privados" }.Concat(partes).ToArray());
    }

    private async Task<ColunaQuadro> ObterOuCriarColunaArquivoSistemaAsync(int quadroId)
    {
        var colunaArquivo = await _context.ColunasQuadro
            .FirstOrDefaultAsync(c => c.QuadroId == quadroId &&
                                      !c.Ativa &&
                                      c.Nome.StartsWith(PrefixoColunaArquivoSistema));

        if (colunaArquivo != null)
            return colunaArquivo;

        var ultimaPosicao = await _context.ColunasQuadro
            .Where(c => c.QuadroId == quadroId)
            .MaxAsync(c => (decimal?)c.Posicao) ?? 0m;

        colunaArquivo = new ColunaQuadro
        {
            QuadroId = quadroId,
            Nome = $"{PrefixoColunaArquivoSistema}_{Guid.NewGuid():N}",
            Posicao = ultimaPosicao + OrdemBase,
            Ativa = false,
            DataCriacao = DateTime.UtcNow
        };

        _context.ColunasQuadro.Add(colunaArquivo);

        return colunaArquivo;
    }

    private async Task RemoverColunasInativasComMesmoNomeAsync(int quadroId, int grupoId, string nome, int usuarioId)
    {
        var colunasInativas = await _context.ColunasQuadro
            .Where(c => c.QuadroId == quadroId &&
                        !c.Ativa &&
                        c.Nome == nome &&
                        !c.Nome.StartsWith(PrefixoColunaArquivoSistema))
            .ToListAsync();

        if (colunasInativas.Count == 0)
            return;

        var colunaIds = colunasInativas.Select(c => c.Id).ToList();
        var cartoesPorColuna = await _context.CartoesTarefas
            .Where(c => colunaIds.Contains(c.ColunaId) && c.GrupoId == grupoId)
            .GroupBy(c => c.ColunaId)
            .ToDictionaryAsync(g => g.Key, g => g.ToList());

        ColunaQuadro? colunaArquivo = null;
        var agora = DateTime.UtcNow;

        foreach (var coluna in colunasInativas)
        {
            var cartoes = cartoesPorColuna.TryGetValue(coluna.Id, out var cartoesColuna)
                ? cartoesColuna
                : new List<CartaoTarefa>();

            if (cartoes.Count > 0)
            {
                colunaArquivo ??= await ObterOuCriarColunaArquivoSistemaAsync(quadroId);

                foreach (var cartao in cartoes)
                {
                    if (cartao.Status != StatusCartaoTarefa.Arquivada)
                        cartao.Status = StatusCartaoTarefa.Arquivada;

                    cartao.Coluna = colunaArquivo;
                    cartao.DataAtualizacao = agora;
                    RegistrarHistorico(cartao.Id, usuarioId, "Cartao arquivado por limpeza de lista excluída");
                }
            }

            _context.ColunasQuadro.Remove(coluna);
        }
    }

    private async Task CarregarDadosAsync(int usuarioId, PermissaoUsuario permissao)
    {
        Membros = await ObterMembrosAsync(GrupoId);

        var colunas = await _context.ColunasQuadro
            .AsNoTracking()
            .Where(c => c.QuadroId == Quadro.Id && c.Ativa)
            .OrderBy(c => c.Posicao)
            .Select(c => new ColunaBoardViewModel
            {
                Id = c.Id,
                Nome = c.Nome
            })
            .ToListAsync();

        var cartoes = await _context.CartoesTarefas
            .AsNoTracking()
            .Where(c =>
                c.QuadroId == Quadro.Id &&
                c.GrupoId == GrupoId &&
                c.Status != StatusCartaoTarefa.Arquivada &&
                (!c.Privado ||
                 _context.CartoesTarefasUsuarios.Any(u => u.CartaoTarefaId == c.Id && u.UsuarioId == usuarioId)))
            .OrderBy(c => c.OrdemColuna)
            .Select(c => new CartaoBoardViewModel
            {
                Id = c.Id,
                ColunaId = c.ColunaId,
                CriadorId = c.CriadorId,
                Titulo = c.Titulo,
                CorCapa = c.CorCapa,
                DataVencimento = c.DataVencimento,
                Privado = c.Privado,
                Concluido = c.Status == StatusCartaoTarefa.Concluida
            })
            .ToListAsync();

        var cartaoIds = cartoes.Select(c => c.Id).ToList();
        var membrosPorId = Membros
            .GroupBy(m => m.UsuarioId)
            .ToDictionary(g => g.Key, g => g.First());
        var todosMembrosGrupo = Membros
            .OrderBy(m => m.NomeExibicao)
            .ToList();

        var membrosCartoes = cartaoIds.Count == 0
            ? new List<BoardMembroCartaoViewModel>()
            : await _context.CartoesTarefasUsuarios
                .AsNoTracking()
                .Where(vinculo => cartaoIds.Contains(vinculo.CartaoTarefaId))
                .Select(vinculo => new BoardMembroCartaoViewModel
                {
                    CartaoTarefaId = vinculo.CartaoTarefaId,
                    UsuarioId = vinculo.UsuarioId
                })
                .ToListAsync();

        var membrosIdsPorCartao = membrosCartoes
            .GroupBy(m => m.CartaoTarefaId)
            .ToDictionary(g => g.Key, g => g.Select(m => m.UsuarioId).Distinct().ToList());

        foreach (var cartao in cartoes)
        {
            if (!cartao.Privado)
            {
                cartao.MembrosVisiveis = todosMembrosGrupo;
                continue;
            }

            var ids = membrosIdsPorCartao.TryGetValue(cartao.Id, out var idsCartao)
                ? idsCartao
                : new List<int>();

            cartao.MembrosVisiveis = ids
                .Append(cartao.CriadorId)
                .Distinct()
                .Select(id => membrosPorId.TryGetValue(id, out var membro) ? membro : null)
                .Where(membro => membro != null)
                .Select(membro => membro!)
                .OrderBy(membro => membro.NomeExibicao)
                .ToList();
        }

        var chamados = cartaoIds.Count == 0
            ? new List<BoardChamadoVinculadoViewModel>()
            : await (
                from vinculo in _context.CartoesTarefasChamados.AsNoTracking()
                join chamado in _context.Chamados.AsNoTracking()
                    on vinculo.ChamadoId equals chamado.Id
                where cartaoIds.Contains(vinculo.CartaoTarefaId) &&
                      vinculo.Ativo &&
                      chamado.GrupoId == GrupoId &&
                      chamado.Status != StatusChamado.Cancelado &&
                      chamado.Status != StatusChamado.Excluido
                select new BoardChamadoVinculadoViewModel
                {
                    CartaoTarefaId = vinculo.CartaoTarefaId,
                    Id = chamado.Id,
                    NumeroChamadoGrupo = chamado.NumeroChamadoGrupo,
                    Titulo = chamado.Titulo,
                    Publico = chamado.Publico,
                    CriadorChamadoId = chamado.CriadorChamadoId
                })
                .ToListAsync();

        var cartoesPorId = cartoes.ToDictionary(c => c.Id);
        var chamadosPorCartao = chamados
            .Where(chamado =>
                cartoesPorId.TryGetValue(chamado.CartaoTarefaId, out var cartao) &&
                MembrosPodemVerChamado(
                    cartao.Privado ? cartao.MembrosVisiveis : todosMembrosGrupo,
                    chamado.Publico,
                    chamado.CriadorChamadoId))
            .GroupBy(c => c.CartaoTarefaId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(c => new ChamadoOpcaoViewModel
                {
                    Id = c.Id,
                    NumeroChamadoGrupo = c.NumeroChamadoGrupo,
                    Titulo = c.Titulo ?? "Chamado"
                }).ToList());

        foreach (var cartao in cartoes)
        {
            cartao.Chamados = chamadosPorCartao.TryGetValue(cartao.Id, out var chamadosCartao)
                ? chamadosCartao
                : new List<ChamadoOpcaoViewModel>();
        }

        var etiquetas = cartaoIds.Count == 0
            ? new List<BoardEtiquetaTarefaViewModel>()
            : await (
                from vinculo in _context.CartoesTarefasEtiquetas.AsNoTracking()
                join etiqueta in _context.EtiquetasTarefas.AsNoTracking()
                    on vinculo.EtiquetaId equals etiqueta.Id
                where cartaoIds.Contains(vinculo.CartaoTarefaId) &&
                      etiqueta.GrupoId == GrupoId &&
                      etiqueta.UsuarioId == usuarioId
                orderby etiqueta.Nome
                select new BoardEtiquetaTarefaViewModel
                {
                    CartaoTarefaId = vinculo.CartaoTarefaId,
                    Id = etiqueta.Id,
                    Nome = etiqueta.Nome,
                    Cor = etiqueta.Cor
                })
                .ToListAsync();

        var etiquetasPorCartao = etiquetas
            .GroupBy(e => e.CartaoTarefaId)
            .ToDictionary(g => g.Key, g => g.Select(e => new EtiquetaTarefaDto(e.Id, e.Nome, e.Cor)).ToList());

        foreach (var cartao in cartoes)
        {
            cartao.Etiquetas = etiquetasPorCartao.TryGetValue(cartao.Id, out var etiquetasCartao)
                ? etiquetasCartao
                : new List<EtiquetaTarefaDto>();
        }

        var progressoChecklists = cartaoIds.Count == 0
            ? new List<BoardChecklistProgressoViewModel>()
            : await _context.ChecklistItensTarefas
                .AsNoTracking()
                .Where(i => cartaoIds.Contains(i.Checklist.CartaoTarefaId))
                .GroupBy(i => i.Checklist.CartaoTarefaId)
                .Select(g => new BoardChecklistProgressoViewModel
                {
                    CartaoTarefaId = g.Key,
                    Total = g.Count(),
                    Concluidos = g.Count(i => i.Concluido)
                })
                .ToListAsync();

        var progressoChecklistsPorCartao = progressoChecklists.ToDictionary(p => p.CartaoTarefaId);
        foreach (var cartao in cartoes)
        {
            if (!progressoChecklistsPorCartao.TryGetValue(cartao.Id, out var progresso))
                continue;

            cartao.ChecklistItensTotal = progresso.Total;
            cartao.ChecklistItensConcluidos = progresso.Concluidos;
        }

        var cartoesPorColuna = cartoes
            .GroupBy(c => c.ColunaId)
            .ToDictionary(g => g.Key, g => g.ToList());

        foreach (var coluna in colunas)
        {
            coluna.Cartoes = cartoesPorColuna.TryGetValue(coluna.Id, out var cartoesColuna)
                ? cartoesColuna
                : new List<CartaoBoardViewModel>();
        }

        Colunas = colunas;
    }
    private async Task<QuadroTarefa> GarantirQuadroPadraoAsync(int grupoId, int usuarioId)
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

    private async Task<CartaoTarefa> CriarCartaoBaseAsync(int quadroId, int colunaId, int grupoId, int usuarioId, string titulo)
    {
        var contador = await _context.CartaoTarefaContadorGrupo.FirstOrDefaultAsync(c => c.GrupoId == grupoId);
        if (contador == null)
        {
            contador = new CartaoTarefaContadorGrupo { GrupoId = grupoId, UltimoNumero = 0 };
            _context.CartaoTarefaContadorGrupo.Add(contador);
        }

        contador.UltimoNumero++;

        var ultimaOrdem = await _context.CartoesTarefas
            .Where(c => c.ColunaId == colunaId)
            .MaxAsync(c => (decimal?)c.OrdemColuna) ?? 0m;

        return new CartaoTarefa
        {
            QuadroId = quadroId,
            ColunaId = colunaId,
            GrupoId = grupoId,
            NumeroCartaoGrupo = contador.UltimoNumero,
            Titulo = titulo,
            CriadorId = usuarioId,
            Status = StatusCartaoTarefa.Ativa,
            OrdemColuna = ultimaOrdem + OrdemBase,
            PercentualConclusao = 0m,
            Privado = true,
            DataCriacao = DateTime.UtcNow,
            DataAtualizacao = DateTime.UtcNow
        };
    }

    private async Task<CartaoTarefa> SalvarCartaoTransacionalAsync(
        SalvarCartaoRequest request,
        int usuarioId,
        string titulo,
        PrioridadeChamado? prioridade,
        CriticidadeChamado? criticidade,
        UrgenciaChamado? urgencia,
        StatusCartaoTarefa? statusSolicitado)
    {
        var coluna = await _context.ColunasQuadro
            .FirstOrDefaultAsync(c => c.Id == request.ColunaId && c.Ativa);

        if (coluna == null)
            throw new InvalidOperationException("Lista invalida.");

        var quadro = await _context.QuadrosTarefas
            .FirstOrDefaultAsync(q => q.Id == coluna.QuadroId && q.GrupoId == request.GrupoId && q.Ativo);

        if (quadro == null)
            throw new InvalidOperationException("Quadro inválido.");

        CartaoTarefa cartao;
        var novo = request.Id.GetValueOrDefault() <= 0;
        var statusAnterior = StatusCartaoTarefa.Ativa;
        DateTime? dataVencimentoAnterior = null;

        if (novo)
        {
            cartao = await CriarCartaoBaseAsync(quadro.Id, coluna.Id, request.GrupoId, usuarioId, titulo);
            _context.CartoesTarefas.Add(cartao);
        }
        else
        {
            cartao = await _context.CartoesTarefas
                .FirstOrDefaultAsync(c => c.Id == request.Id && c.GrupoId == request.GrupoId)
                ?? throw new InvalidOperationException("Cartao não encontrado.");

            var membrosAtuais = await _context.CartoesTarefasUsuarios
                .AsNoTracking()
                .Where(x => x.CartaoTarefaId == cartao.Id)
                .Select(x => x.UsuarioId)
                .ToListAsync();

            if (!PodeVerCartao(cartao, usuarioId, membrosAtuais) ||
                !PodeEditarCartao(cartao, usuarioId, membrosAtuais))
            {
                throw new UnauthorizedAccessException("Você não tem permissão para editar este cartão.");
            }

            statusAnterior = cartao.Status;
            dataVencimentoAnterior = cartao.DataVencimento;
            cartao.Titulo = titulo;
            cartao.ColunaId = coluna.Id;
            cartao.QuadroId = quadro.Id;
        }

        cartao.Descricao = string.IsNullOrWhiteSpace(request.Descricao) ? null : request.Descricao.Trim();
        cartao.Prioridade = prioridade;
        cartao.Criticidade = criticidade;
        cartao.Urgencia = urgencia;
        cartao.DataInicio = request.DataInicio;
        cartao.DataVencimento = request.DataVencimento;
        if (novo || cartao.DataVencimentoOperacional == null || cartao.DataVencimentoOperacional == dataVencimentoAnterior)
            cartao.DataVencimentoOperacional = request.DataVencimento;
        cartao.CorCapa = NormalizarCor(request.CorCapa);
        cartao.Privado = !request.CompartilharGrupo;
        cartao.DataAtualizacao = DateTime.UtcNow;

        var statusNovo = statusSolicitado ?? cartao.Status;
        if (statusNovo != cartao.Status)
        {
            cartao.Status = statusNovo;
            cartao.DataConclusao = statusNovo == StatusCartaoTarefa.Concluida ? DateTime.UtcNow : null;
            cartao.PercentualConclusao = statusNovo == StatusCartaoTarefa.Concluida ? 100m : 0m;
        }

        if (novo)
            await _context.SaveChangesAsync();

        if (statusAnterior != cartao.Status)
        {
            await _slaPausaService.RegistrarTransicaoCartaoAsync(
                cartao,
                statusAnterior,
                cartao.Status,
                usuarioId,
                DateTime.UtcNow,
                request.ObservacaoPendenteEntrada,
                request.ObservacaoPendenteSaida);
            RegistrarHistorico(cartao.Id, usuarioId, $"Status alterado de {statusAnterior} para {cartao.Status}");
        }

        var usuariosComAcesso = await SincronizarMembrosAsync(cartao.Id, request.GrupoId, request.MembrosIds, usuarioId, cartao.CriadorId);
        await SincronizarChamadosAsync(cartao, request.ChamadosIds, request.GrupoId, usuarioId, usuariosComAcesso);
        await SincronizarEtiquetasUsuarioCartaoAsync(cartao.Id, request.GrupoId, usuarioId, request.EtiquetasIds);
        RegistrarHistorico(cartao.Id, usuarioId, novo ? "Cartao criado" : "Cartao atualizado");
        await _mencaoService.SincronizarMencoesAsync(
            cartao.GrupoId,
            usuarioId,
            "Tarefa",
            cartao.Id,
            "Descricao",
            cartao.Descricao,
            TipoNotificacao.Tarefa,
            "Voce foi mencionado em uma tarefa",
            $"descricao da tarefa #{cartao.NumeroCartaoGrupo}",
            $"/Menu/Tasks?grupoId={cartao.GrupoId}",
            await ObterUsuariosPermitidosMencaoCartaoAsync(cartao));
        await _context.SaveChangesAsync();

        return cartao;
    }

    private async Task SincronizarEtiquetasUsuarioCartaoAsync(int cartaoId, int grupoId, int usuarioId, List<int>? etiquetasIds)
    {
        etiquetasIds ??= new List<int>();
        var idsSolicitados = etiquetasIds.Where(id => id > 0).Distinct().ToList();

        var idsValidos = idsSolicitados.Count == 0
            ? new List<int>()
            : await _context.EtiquetasTarefas
                .AsNoTracking()
                .Where(e => idsSolicitados.Contains(e.Id) && e.GrupoId == grupoId && e.UsuarioId == usuarioId)
                .Select(e => e.Id)
                .ToListAsync();

        if (idsValidos.Count != idsSolicitados.Count)
            throw new UnauthorizedAccessException("Uma ou mais etiquetas nao pertencem ao usuario neste grupo.");

        var atuais = await _context.CartoesTarefasEtiquetas
            .Where(v => v.CartaoTarefaId == cartaoId)
            .Join(_context.EtiquetasTarefas.Where(e => e.UsuarioId == usuarioId && e.GrupoId == grupoId),
                v => v.EtiquetaId,
                e => e.Id,
                (v, e) => v)
            .ToListAsync();

        var atuaisIds = atuais.Select(v => v.EtiquetaId).ToHashSet();
        var novosIds = idsValidos.ToHashSet();

        _context.CartoesTarefasEtiquetas.RemoveRange(atuais.Where(v => !novosIds.Contains(v.EtiquetaId)));

        foreach (var etiquetaId in novosIds.Where(id => !atuaisIds.Contains(id)))
        {
            _context.CartoesTarefasEtiquetas.Add(new CartaoTarefaEtiqueta
            {
                CartaoTarefaId = cartaoId,
                EtiquetaId = etiquetaId
            });
        }
    }

    private async Task<List<int>> SincronizarMembrosAsync(int cartaoId, int grupoId, List<int> membrosIds, int usuarioId, int criadorId)
    {
        membrosIds ??= new List<int>();

        var idsSolicitados = membrosIds
            .Append(criadorId)
            .Distinct()
            .ToList();

        var idsValidos = await _context.UsuariosGrupos
            .AsNoTracking()
            .Where(x => x.Ativo && x.GrupoId == grupoId && idsSolicitados.Contains(x.UsuarioId))
            .Select(x => x.UsuarioId)
            .Distinct()
            .ToListAsync();

        var atuais = await _context.CartoesTarefasUsuarios
            .Where(x => x.CartaoTarefaId == cartaoId)
            .ToListAsync();

        _context.CartoesTarefasUsuarios.RemoveRange(atuais.Where(x => !idsValidos.Contains(x.UsuarioId)));

        foreach (var id in idsValidos.Where(id => atuais.All(x => x.UsuarioId != id)))
        {
            _context.CartoesTarefasUsuarios.Add(new CartaoTarefaUsuario
            {
                CartaoTarefaId = cartaoId,
                UsuarioId = id,
                TipoParticipacao = TipoParticipacaoCartaoTarefa.Participante,
                Permissao = PermissaoCartaoTarefa.Editor,
                DataAdicao = DateTime.UtcNow,
                AdicionadoPorUsuarioId = usuarioId
            });
        }

        return idsValidos;
    }

    private async Task SincronizarChamadosAsync(CartaoTarefa cartao, List<int> chamadosIds, int grupoId, int usuarioId, List<int>? usuariosComAcesso = null)
    {
        chamadosIds ??= new List<int>();

        usuariosComAcesso ??= await ObterUsuariosComAcessoCartaoAsync(cartao);
        var idsValidos = chamadosIds.Any()
            ? (await ObterChamadosPermitidosAsync(grupoId, usuariosComAcesso, chamadosIds)).Select(c => c.Id).ToList()
            : new List<int>();

        if (idsValidos.Count != chamadosIds.Distinct().Count())
            throw new UnauthorizedAccessException("Um ou mais chamados não podem ser vinculados a esta tarefa.");

        var atuais = await _context.CartoesTarefasChamados
            .Where(x => x.CartaoTarefaId == cartao.Id)
            .ToListAsync();

        foreach (var vinculo in atuais)
        {
            var deveFicarAtivo = idsValidos.Contains(vinculo.ChamadoId);
            if (vinculo.Ativo != deveFicarAtivo)
            {
                vinculo.Ativo = deveFicarAtivo;
                vinculo.DataDesvinculo = deveFicarAtivo ? null : DateTime.UtcNow;
                vinculo.DesvinculadoPorUsuarioId = deveFicarAtivo ? null : usuarioId;
            }
        }

        foreach (var chamadoId in idsValidos.Where(id => atuais.All(x => x.ChamadoId != id)))
        {
            _context.CartoesTarefasChamados.Add(new CartaoTarefaChamado
            {
                CartaoTarefaId = cartao.Id,
                ChamadoId = chamadoId,
                TipoRelacao = TipoRelacaoCartaoChamado.Relacionada,
                Ativo = true,
                DataVinculo = DateTime.UtcNow,
                VinculadoPorUsuarioId = usuarioId
            });
        }
    }

    private void RegistrarHistorico(int cartaoId, int usuarioId, string acao)
    {
        _context.HistoricoTarefas.Add(new HistoricoTarefa
        {
            CartaoTarefaId = cartaoId,
            UsuarioId = usuarioId,
            TipoAcao = acao,
            DataAcao = DateTime.UtcNow
        });
    }

    private async Task<List<int>> ObterUsuariosPermitidosMencaoCartaoAsync(CartaoTarefa cartao)
    {
        if (!cartao.Privado)
        {
            return await _context.UsuariosGrupos
                .AsNoTracking()
                .Where(ug => ug.GrupoId == cartao.GrupoId && ug.Ativo)
                .Select(ug => ug.UsuarioId)
                .ToListAsync();
        }

        var membros = await _context.CartoesTarefasUsuarios
            .AsNoTracking()
            .Where(x => x.CartaoTarefaId == cartao.Id)
            .Select(x => x.UsuarioId)
            .ToListAsync();

        return membros.Append(cartao.CriadorId).Distinct().ToList();
    }

    private async Task<bool> PodeVerCartaoAsync(CartaoTarefa cartao, int usuarioId)
    {
        if (!cartao.Privado)
            return true;

        return await _context.CartoesTarefasUsuarios
            .AsNoTracking()
            .AnyAsync(x => x.CartaoTarefaId == cartao.Id && x.UsuarioId == usuarioId);
    }

    private static bool PodeVerCartao(CartaoTarefa cartao, int usuarioId, List<int> membrosAtuais)
    {
        return !cartao.Privado ||
               membrosAtuais.Contains(usuarioId);
    }

    private static bool PodeEditarCartao(CartaoTarefa cartao, int usuarioId, List<int> membrosAtuais)
    {
        return membrosAtuais.Contains(usuarioId);
    }

    private async Task<List<int>> ObterUsuariosComAcessoCartaoAsync(CartaoTarefa cartao)
    {
        if (!cartao.Privado)
        {
            return await _context.UsuariosGrupos
                .AsNoTracking()
                .Where(x => x.GrupoId == cartao.GrupoId && x.Ativo)
                .Select(x => x.UsuarioId)
                .ToListAsync();
        }

        var usuarios = await _context.CartoesTarefasUsuarios
            .AsNoTracking()
            .Where(x => x.CartaoTarefaId == cartao.Id)
            .Select(x => x.UsuarioId)
            .Distinct()
            .ToListAsync();

        return usuarios.Append(cartao.CriadorId).Distinct().ToList();
    }

    private async Task<List<ChamadoOpcaoViewModel>> ObterChamadosPermitidosParaCartaoAsync(CartaoTarefa cartao)
    {
        var usuariosComAcesso = await ObterUsuariosComAcessoCartaoAsync(cartao);
        return await ObterChamadosPermitidosAsync(cartao.GrupoId, usuariosComAcesso);
    }

    private async Task<List<ChamadoOpcaoViewModel>> ObterChamadosVinculadosVisiveisAsync(CartaoTarefa cartao)
    {
        var chamadosIds = await _context.CartoesTarefasChamados
            .AsNoTracking()
            .Where(vinculo => vinculo.CartaoTarefaId == cartao.Id && vinculo.Ativo)
            .Select(vinculo => vinculo.ChamadoId)
            .ToListAsync();

        if (chamadosIds.Count == 0)
            return new List<ChamadoOpcaoViewModel>();

        var usuariosComAcesso = await ObterUsuariosComAcessoCartaoAsync(cartao);
        return await ObterChamadosPermitidosAsync(cartao.GrupoId, usuariosComAcesso, chamadosIds, limitar: false);
    }

    private async Task<List<ChamadoOpcaoViewModel>> ObterChamadosPermitidosAsync(
        int grupoId,
        List<int> usuariosIds,
        IEnumerable<int>? chamadosIds = null,
        bool limitar = true,
        string? termo = null,
        IEnumerable<int>? idsSempreIncluir = null)
    {
        usuariosIds = usuariosIds.Distinct().ToList();
        if (!usuariosIds.Any())
            return new List<ChamadoOpcaoViewModel>();

        var membros = await _context.UsuariosGrupos
            .AsNoTracking()
            .Where(x => x.GrupoId == grupoId && x.Ativo && usuariosIds.Contains(x.UsuarioId))
            .Select(x => new { x.UsuarioId, x.Permissao })
            .ToListAsync();

        if (membros.Count != usuariosIds.Count)
            return new List<ChamadoOpcaoViewModel>();

        var idsFiltro = chamadosIds?.Distinct().ToList();
        var idsFixos = idsSempreIncluir?.Distinct().ToList() ?? new List<int>();
        var termoNormalizado = (termo ?? string.Empty).Trim();
        var termoNumero = termoNormalizado.StartsWith("#", StringComparison.Ordinal)
            ? termoNormalizado[1..].Trim()
            : termoNormalizado;
        var pesquisarNumeroChamado = int.TryParse(termoNumero, out var numeroChamadoPesquisa);
        var query = _context.Chamados.AsNoTracking()
            .Where(c => c.GrupoId == grupoId &&
                        c.Status != StatusChamado.Cancelado &&
                        c.Status != StatusChamado.Excluido);

        if (idsFiltro is { Count: > 0 })
            query = query.Where(c => idsFiltro.Contains(c.Id));
        else if (!string.IsNullOrWhiteSpace(termoNormalizado))
        {
            var padrao = $"%{EscaparLike(termoNormalizado)}%";
            query = query.Where(c =>
                idsFixos.Contains(c.Id) ||
                EF.Functions.Like(c.Titulo ?? string.Empty, padrao, "\\") ||
                EF.Functions.Like(c.Descricao ?? string.Empty, padrao, "\\") ||
                EF.Functions.Like(c.Solucao ?? string.Empty, padrao, "\\") ||
                (pesquisarNumeroChamado && c.NumeroChamadoGrupo == numeroChamadoPesquisa));
        }

        var membrosRestritos = membros
            .Where(m => m.Permissao is PermissaoUsuario.Nenhuma or PermissaoUsuario.Colaborador)
            .Select(m => m.UsuarioId)
            .ToList();

        if (membrosRestritos.Count > 0)
            query = query.Where(c => c.Publico || membrosRestritos.Contains(c.CriadorChamadoId));

        IQueryable<Chamado> chamadosQuery = query
            .OrderByDescending(c => idsFixos.Contains(c.Id))
            .ThenByDescending(c => c.DataCriacao)
            .ThenBy(c => c.Id);

        if (idsFiltro is { Count: > 0 })
            chamadosQuery = chamadosQuery.Take(idsFiltro.Count);
        else if (limitar)
            chamadosQuery = chamadosQuery.Take(LimiteOpcoesChamadosTarefa + idsFixos.Count);

        var chamados = await chamadosQuery
            .Select(c => new
            {
                c.Id,
                c.NumeroChamadoGrupo,
                c.Titulo,
                c.Publico,
                c.CriadorChamadoId
            })
            .ToListAsync();

        return chamados
            .Where(chamado => membros.All(membro => GrupoPermissionService.PodeVerChamado(membro.Permissao, chamado.Publico, membro.UsuarioId, chamado.CriadorChamadoId)))
            .Select(chamado => new ChamadoOpcaoViewModel
            {
                Id = chamado.Id,
                NumeroChamadoGrupo = chamado.NumeroChamadoGrupo,
                Titulo = chamado.Titulo ?? "Chamado"
            })
            .ToList();
    }

    private async Task DesvincularchamadosSemPermissaoAsync(int cartaoId, int grupoId, List<int> membros, int usuarioId)
    {
        var cartao = await _context.CartoesTarefas
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == cartaoId && c.GrupoId == grupoId);

        if (cartao == null)
            return;

        var vinculos = await _context.CartoesTarefasChamados
            .Where(x => x.CartaoTarefaId == cartaoId && x.Ativo)
            .ToListAsync();

        if (!vinculos.Any())
            return;

        var usuariosComAcesso = cartao.Privado
            ? membros.Append(cartao.CriadorId).Distinct().ToList()
            : await _context.UsuariosGrupos
                .AsNoTracking()
                .Where(x => x.GrupoId == grupoId && x.Ativo)
                .Select(x => x.UsuarioId)
                .ToListAsync();

        if (!usuariosComAcesso.Any())
            return;

        var idsPermitidos = (await ObterChamadosPermitidosAsync(
                grupoId,
                usuariosComAcesso,
                vinculos.Select(x => x.ChamadoId)))
            .Select(x => x.Id)
            .ToHashSet();

        var alterou = false;
        foreach (var vinculo in vinculos.Where(x => !idsPermitidos.Contains(x.ChamadoId)))
        {
            vinculo.Ativo = false;
            vinculo.DataDesvinculo = DateTime.UtcNow;
            vinculo.DesvinculadoPorUsuarioId = usuarioId;
            alterou = true;
        }

        if (alterou)
        {
            RegistrarHistorico(cartaoId, usuarioId, "Chamados desvinculados por permissão");
            await _context.SaveChangesAsync();
        }
    }

    private async Task DesvincularChamadosSemPermissaoAsync(CartaoTarefa cartao, int usuarioId)
    {
        var membros = await _context.CartoesTarefasUsuarios
            .AsNoTracking()
            .Where(x => x.CartaoTarefaId == cartao.Id)
            .Select(x => x.UsuarioId)
            .ToListAsync();

        await DesvincularchamadosSemPermissaoAsync(cartao.Id, cartao.GrupoId, membros, usuarioId);
    }

    private static bool UsuarioPodeVerChamado(PermissaoUsuario permissao, int usuarioId, bool chamadoPublico, int criadorChamadoId)
    {
        return permissao switch
        {
            PermissaoUsuario.Administracao => true,
            PermissaoUsuario.Tecnico => true,
            PermissaoUsuario.Colaborador => chamadoPublico || criadorChamadoId == usuarioId,
            _ => chamadoPublico
        };
    }

    private static bool MembrosPodemVerChamado(IEnumerable<MembroViewModel> membros, bool chamadoPublico, int criadorChamadoId)
    {
        var membrosLista = membros.ToList();
        return membrosLista.Count > 0 &&
               membrosLista.All(membro =>
                   Enum.TryParse<PermissaoUsuario>(membro.Permissao, out var permissao) &&
                   GrupoPermissionService.PodeVerChamado(
                       permissao,
                       chamadoPublico,
                       membro.UsuarioId,
                       criadorChamadoId));
    }

    private static List<int> ParseIds(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
            return new List<int>();

        return valor
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => int.TryParse(x, out var id) ? id : 0)
            .Where(id => id > 0)
            .Distinct()
            .ToList();
    }

    private async Task<GrupoMemberContext?> ValidarMembroAsync(int usuarioId, int grupoId)
    {
        if (grupoId <= 0)
            return null;

        return await _grupoAuthorizationService.ObterContextoMembroAsync(usuarioId, grupoId);
    }

    private async Task<GrupoMemberContext?> ValidarMembroComPermissaoAsync(int usuarioId, int grupoId)
    {
        var contexto = await ValidarMembroAsync(usuarioId, grupoId);
        return contexto?.Permissao == PermissaoUsuario.Nenhuma ? null : contexto;
    }

    private async Task<List<MembroViewModel>> ObterMembrosAsync(int grupoId)
    {
        return await (
            from usuarioGrupo in _context.UsuariosGrupos.AsNoTracking()
            join usuario in _context.Usuarios.AsNoTracking()
                on usuarioGrupo.UsuarioId equals usuario.Id
            join info in _context.InfoUsuariosGrupos.AsNoTracking()
                on new { usuarioGrupo.UsuarioId, usuarioGrupo.GrupoId }
                equals new { info.UsuarioId, info.GrupoId } into infoJoin
            from infoUsuario in infoJoin.DefaultIfEmpty()
            where usuarioGrupo.GrupoId == grupoId && usuarioGrupo.Ativo
            orderby usuario.NomeUsuario
            select new MembroViewModel
            {
                UsuarioId = usuario.Id,
                NomeExibicao = infoUsuario != null && !string.IsNullOrWhiteSpace(infoUsuario.Apelido)
                    ? infoUsuario.Apelido
                    : usuario.NomeUsuario,
                Permissao = usuarioGrupo.Permissao.ToString(),
                FotoUsuario = usuario.FotoUsuario
            })
            .ToListAsync();
    }

    private async Task<List<MembroViewModel>> ObterMembrosVisiveisParaCardAsync(int grupoId, bool privado, IEnumerable<int> membrosIds)
    {
        var membrosGrupo = await ObterMembrosAsync(grupoId);
        if (!privado)
            return membrosGrupo.OrderBy(membro => membro.NomeExibicao).ToList();

        var ids = membrosIds.Distinct().ToHashSet();
        return membrosGrupo
            .Where(membro => ids.Contains(membro.UsuarioId))
            .OrderBy(membro => membro.NomeExibicao)
            .ToList();
    }

    private async Task<List<ChamadoOpcaoViewModel>> ObterChamadosDisponiveisAsync(int grupoId, int usuarioId, PermissaoUsuario permissao)
    {
        IQueryable<Chamado> query = _context.Chamados.AsNoTracking()
            .Where(c => c.GrupoId == grupoId &&
                        c.Status != StatusChamado.Cancelado &&
                        c.Status != StatusChamado.Excluido);

        if (permissao == PermissaoUsuario.Colaborador)
            query = query.Where(c => c.Publico || c.CriadorChamadoId == usuarioId);
        else if (permissao == PermissaoUsuario.Nenhuma)
            query = query.Where(c => c.Publico);

        return await query
            .OrderByDescending(c => c.DataCriacao)
            .Take(200)
            .Select(c => new ChamadoOpcaoViewModel
            {
                Id = c.Id,
                NumeroChamadoGrupo = c.NumeroChamadoGrupo,
                Titulo = c.Titulo ?? "Chamado"
            })
            .ToListAsync();
    }

    private int? GetUsuarioLogadoId()
    {
        var claim = User.FindFirst("Id")?.Value;
        return int.TryParse(claim, out var id) ? id : null;
    }

    private static string ParaDataHoraRegionalIso(DateTime dataUtc)
    {
        var dataOrigem = dataUtc.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(dataUtc, DateTimeKind.Utc)
            : dataUtc.ToUniversalTime();

        return TimeZoneInfo.ConvertTimeFromUtc(dataOrigem, FusoHorarioRegional).ToString("yyyy-MM-ddTHH:mm:ss");
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

    private static string? NormalizarCor(string? cor)
    {
        if (string.IsNullOrWhiteSpace(cor))
            return null;

        cor = cor.Trim();
        return cor.Length <= 20 ? cor : null;
    }

    private static bool EhErroDuplicidade(DbUpdateException ex)
    {
        var mensagem = ex.InnerException?.Message ?? ex.Message;
        return mensagem.Contains("Duplicate", StringComparison.OrdinalIgnoreCase) ||
               mensagem.Contains("duplicate", StringComparison.OrdinalIgnoreCase) ||
               mensagem.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase);
    }

    private static string EscaparLike(string valor) =>
        valor
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);

    public class ColunaBoardViewModel
    {
        public int Id { get; set; }
        public string Nome { get; set; } = string.Empty;
        public List<CartaoBoardViewModel> Cartoes { get; set; } = new();
    }

    public class CartaoBoardViewModel
    {
        public int Id { get; set; }
        public int ColunaId { get; set; }
        public int CriadorId { get; set; }
        public string Titulo { get; set; } = string.Empty;
        public string? CorCapa { get; set; }
        public DateTime? DataVencimento { get; set; }
        public bool Privado { get; set; }
        public bool Concluido { get; set; }
        public int ChecklistItensTotal { get; set; }
        public int ChecklistItensConcluidos { get; set; }
        public List<ChamadoOpcaoViewModel> Chamados { get; set; } = new();
        public List<EtiquetaTarefaDto> Etiquetas { get; set; } = new();
        public List<MembroViewModel> MembrosVisiveis { get; set; } = new();
    }

    public class MembroViewModel
    {
        public int UsuarioId { get; set; }
        public string NomeExibicao { get; set; } = string.Empty;
        public string Permissao { get; set; } = string.Empty;
        public string? FotoUsuario { get; set; }
    }

    public class ChamadoOpcaoViewModel
    {
        public int Id { get; set; }
        public int NumeroChamadoGrupo { get; set; }
        public string Titulo { get; set; } = string.Empty;
    }

    private class BoardChamadoVinculadoViewModel
    {
        public int CartaoTarefaId { get; set; }
        public int Id { get; set; }
        public int NumeroChamadoGrupo { get; set; }
        public string? Titulo { get; set; }
        public bool Publico { get; set; }
        public int CriadorChamadoId { get; set; }
    }

    private class BoardEtiquetaTarefaViewModel
    {
        public int CartaoTarefaId { get; set; }
        public int Id { get; set; }
        public string Nome { get; set; } = string.Empty;
        public string Cor { get; set; } = string.Empty;
    }

    private class BoardChecklistProgressoViewModel
    {
        public int CartaoTarefaId { get; set; }
        public int Total { get; set; }
        public int Concluidos { get; set; }
    }

    private class BoardMembroCartaoViewModel
    {
        public int CartaoTarefaId { get; set; }
        public int UsuarioId { get; set; }
    }

    public record EtiquetaTarefaDto(int Id, string Nome, string Cor);

    public class ChecklistTarefaDto
    {
        public int Id { get; set; }
        public string Titulo { get; set; } = string.Empty;
        public List<ChecklistItemTarefaDto> Itens { get; set; } = new();
    }

    public class ChecklistItemTarefaDto
    {
        public int Id { get; set; }
        public int ChecklistId { get; set; }
        public string Descricao { get; set; } = string.Empty;
        public bool Concluido { get; set; }
    }

    public class AnexoTarefaDto
    {
        public int Id { get; set; }
        public string NomeOriginal { get; set; } = string.Empty;
        public string? TipoArquivo { get; set; }
        public string? Extensao { get; set; }
        public long? TamanhoBytes { get; set; }
        public bool EhImagem { get; set; }
        public string TipoVisualizacao { get; set; } = "download";
        public bool PodeVisualizar { get; set; }
        public string Usuario { get; set; } = string.Empty;
        public string? DataUpload { get; set; }
    }

    public class CriarListaRequest
    {
        public int GrupoId { get; set; }
        public string? Nome { get; set; }
    }

    public class RenomearListaRequest
    {
        public int GrupoId { get; set; }
        public int ColunaId { get; set; }
        public string? Nome { get; set; }
    }

    public class ListaRequest
    {
        public int GrupoId { get; set; }
        public int ColunaId { get; set; }
    }

    public class SalvarCartaoRequest
    {
        public int? Id { get; set; }
        public int GrupoId { get; set; }
        public int ColunaId { get; set; }
        public string? Titulo { get; set; }
        public string? Descricao { get; set; }
        public string? Prioridade { get; set; }
        public string? Criticidade { get; set; }
        public string? Urgencia { get; set; }
        public string? Status { get; set; }
        public string? ObservacaoPendenteEntrada { get; set; }
        public string? ObservacaoPendenteSaida { get; set; }
        public DateTime? DataInicio { get; set; }
        public DateTime? DataVencimento { get; set; }
        public string? CorCapa { get; set; }
        public bool CompartilharGrupo { get; set; }
        public List<int> MembrosIds { get; set; } = new();
        public List<int> ChamadosIds { get; set; } = new();
        public List<int> EtiquetasIds { get; set; } = new();
    }

    public class SalvarMembrosCartaoRequest
    {
        public int CartaoId { get; set; }
        public int GrupoId { get; set; }
        public List<int> MembrosIds { get; set; } = new();
        public bool GrupoTodo { get; set; }
    }

    public class SalvarChamadosCartaoRequest
    {
        public int CartaoId { get; set; }
        public int GrupoId { get; set; }
        public List<int> ChamadosIds { get; set; } = new();
    }

    public class SairCartaoRequest
    {
        public int CartaoId { get; set; }
        public int GrupoId { get; set; }
    }

    public class SalvarEtiquetaTarefaRequest
    {
        public int GrupoId { get; set; }
        public string? Nome { get; set; }
        public string? Cor { get; set; }
    }

    public class EditarEtiquetaTarefaRequest : SalvarEtiquetaTarefaRequest
    {
        public int EtiquetaId { get; set; }
    }

    public class EtiquetaTarefaRequest
    {
        public int GrupoId { get; set; }
        public int EtiquetaId { get; set; }
    }

    public class SalvarEtiquetasCartaoRequest
    {
        public int GrupoId { get; set; }
        public int CartaoId { get; set; }
        public List<int> EtiquetasIds { get; set; } = new();
    }

    public class SalvarChecklistTarefaRequest
    {
        public int GrupoId { get; set; }
        public int CartaoId { get; set; }
        public string? Titulo { get; set; }
    }

    public class EditarChecklistTarefaRequest
    {
        public int GrupoId { get; set; }
        public int ChecklistId { get; set; }
        public string? Titulo { get; set; }
    }

    public class ChecklistTarefaRequest
    {
        public int GrupoId { get; set; }
        public int ChecklistId { get; set; }
    }

    public class SalvarItemChecklistTarefaRequest
    {
        public int GrupoId { get; set; }
        public int ChecklistId { get; set; }
        public string? Descricao { get; set; }
    }

    public class EditarItemChecklistTarefaRequest
    {
        public int GrupoId { get; set; }
        public int ItemId { get; set; }
        public string? Descricao { get; set; }
    }

    public class AlternarItemChecklistTarefaRequest
    {
        public int GrupoId { get; set; }
        public int ItemId { get; set; }
        public bool Concluido { get; set; }
    }

    public class ItemChecklistTarefaRequest
    {
        public int GrupoId { get; set; }
        public int ItemId { get; set; }
    }

    public class EnviarAnexoTarefaRequest
    {
        public int GrupoId { get; set; }
        public int CartaoId { get; set; }
        public IFormFile? Arquivo { get; set; }
        public bool ArquivoCompactado { get; set; }
    }

    public class AnexoTarefaRequest
    {
        public int GrupoId { get; set; }
        public int AnexoId { get; set; }
    }

    public class ReordenarCartoesRequest
    {
        public int GrupoId { get; set; }
        public List<ColunaOrdemRequest> Colunas { get; set; } = new();
    }

    public class ReordenarListasRequest
    {
        public int GrupoId { get; set; }
        public List<int> ColunasIds { get; set; } = new();
    }

    public class ColunaOrdemRequest
    {
        public int ColunaId { get; set; }
        public List<int> CartoesIds { get; set; } = new();
    }

    public class AdicionarComentarioRequest
    {
        public int GrupoId { get; set; }
        public int CartaoId { get; set; }
        public string? Mensagem { get; set; }
    }

    public class AlternarConcluidoRequest
    {
        public int GrupoId { get; set; }
        public int CartaoId { get; set; }
    }

    public class SalvarComoTemplateRequest
    {
        public int CartaoId { get; set; }
        public int GrupoId { get; set; }
        public string? NomeTemplate { get; set; }
    }

    public class ArquivarCartaoRequest
    {
        public int CartaoId { get; set; }
        public int GrupoId { get; set; }
    }

    public class CriarCartaoDeTemplateRequest
    {
        public int TemplateId { get; set; }
        public int ColunaId { get; set; }
        public int GrupoId { get; set; }
    }

    public class EditarTemplateRequest
    {
        public int TemplateId { get; set; }
        public int GrupoId { get; set; }
        public string? Nome { get; set; }
        public string? Descricao { get; set; }
        public string? Prioridade { get; set; }
        public string? Criticidade { get; set; }
        public string? Urgencia { get; set; }
        public string? CorCapa { get; set; }
    }

    public class ExcluirTemplateRequest
    {
        public int TemplateId { get; set; }
        public int GrupoId { get; set; }
    }
}
