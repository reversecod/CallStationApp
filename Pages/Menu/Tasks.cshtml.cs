using CallStationApp.Authorization;
using CallStationApp.Data;
using CallStationApp.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace CallStationApp.Pages.Menu;

[Authorize]
public class TasksModel : PageModel
{
    private const decimal OrdemBase = 1024m;
    private const string PrefixoColunaArquivoSistema = "__callstation_archive__";
    private const int LimiteCaracteresComentario = 250;
    private readonly AppDbContext _context;
    private readonly GrupoAuthorizationService _grupoAuthorizationService;
    private readonly ILogger<TasksModel> _logger;

    public TasksModel(AppDbContext context, GrupoAuthorizationService grupoAuthorizationService, ILogger<TasksModel> logger)
    {
        _context = context;
        _grupoAuthorizationService = grupoAuthorizationService;
        _logger = logger;
    }

    [BindProperty(SupportsGet = true)]
    public int GrupoId { get; set; }

    public string? NomeUsuarioLogado { get; set; }
    public string? FotoUsuarioLogado { get; set; }
    public bool UsuarioLogadoEhAdministrador { get; set; }
    public PermissaoUsuario UsuarioLogadoPermissao { get; set; } = PermissaoUsuario.Nenhuma;
    public Grupo? GrupoAtual { get; set; }
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
                            cartao.ColunaId = colunaArquivo.Id;

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

        await DesvincularchamadosSemPermissaoAsync(cartao.Id, grupoId, membros, usuarioId.Value);

        var chamados = await (
            from vinculo in _context.CartoesTarefasChamados.AsNoTracking()
            join chamado in _context.Chamados.AsNoTracking()
                on vinculo.ChamadoId equals chamado.Id
            where vinculo.CartaoTarefaId == id && vinculo.Ativo
            orderby chamado.NumeroChamadoGrupo descending
            select new ChamadoOpcaoViewModel
            {
                Id = chamado.Id,
                NumeroChamadoGrupo = chamado.NumeroChamadoGrupo,
                Titulo = chamado.Titulo ?? "Chamado"
            })
            .ToListAsync();

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
            .ToList();

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
            chamados = chamados.Select(c => c.Id).ToList(),
            chamadosVinculados = chamados,
            chamadosOpcoes,
            podeEditar = PodeEditarCartao(cartao, usuarioId.Value, membros),
            podeSairVinculo = cartao.CriadorId != usuarioId.Value && membros.Contains(usuarioId.Value),
            atividade
        });
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

    public async Task<IActionResult> OnGetChamadosVisivelParaTodosAsync(int cartaoId, int grupoId)
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

        var usuariosComAcesso = await ObterUsuariosComAcessoCartaoAsync(cartao);
        var chamadosPermitidos = await ObterChamadosPermitidosAsync(grupoId, usuariosComAcesso, limitar: false);
        var chamadosIds = chamadosPermitidos.Select(c => c.Id).ToList();

        var vinculados = chamadosIds.Any()
            ? await _context.CartoesTarefasChamados
                .AsNoTracking()
                .Where(x => x.CartaoTarefaId == cartao.Id && x.Ativo && chamadosIds.Contains(x.ChamadoId))
                .Select(x => x.ChamadoId)
                .ToListAsync()
            : new List<int>();

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
                            urgencia);

                        await transaction.CommitAsync();

                        return (IActionResult)new JsonResult(new { success = true, id = cartao.Id });
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

                    RegistrarHistorico(cartao.Id, usuarioId.Value, "Membros atualizados");
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
            _logger.LogError(ex, "Erro ao salvar membros do cartao {CartaoId} no grupo {GrupoId}.", request.CartaoId, request.GrupoId);
            return BadRequest(new { success = false, message = "Não foi possível salvar os membros no momento." });
        }
    }

    public async Task<IActionResult> OnPostSalvarChamadosCartaoAsync([FromBody] SalvarChamadosCartaoRequest request)
    {
        var usuarioId = GetUsuarioLogadoId();
        if (usuarioId == null)
            return Unauthorized();

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
            _logger.LogError(ex, "Erro ao salvar chamados do cartao {CartaoId} no grupo {GrupoId}.", request.CartaoId, request.GrupoId);
            return BadRequest(new { success = false, message = "Não foi possível salvar os chamados no momento." });
        }
    }

    public async Task<IActionResult> OnPostReordenarCartoesAsync([FromBody] ReordenarCartoesRequest request)
    {
        var usuarioId = GetUsuarioLogadoId();
        if (usuarioId == null)
            return Unauthorized();

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

        foreach (var colunaRequest in request.Colunas)
        {
            if (!colunasValidasSet.Contains(colunaRequest.ColunaId))
                return BadRequest(new { success = false, message = "Lista invalida." });

            for (var i = 0; i < colunaRequest.CartoesIds.Count; i++)
            {
                var cartaoId = colunaRequest.CartoesIds[i];
                if (!cartoes.TryGetValue(cartaoId, out var cartao))
                    continue;

                membrosPorCartao.TryGetValue(cartao.Id, out var membrosAtuais);
                membrosAtuais ??= new List<int>();

                if (!PodeVerCartao(cartao, usuarioId.Value, membrosAtuais))
                    return Forbid();
                if (!PodeEditarCartao(cartao, usuarioId.Value, membrosAtuais))
                    return Forbid();

                cartao.ColunaId = colunaRequest.ColunaId;
                cartao.OrdemColuna = (i + 1) * OrdemBase;
                cartao.DataAtualizacao = DateTime.UtcNow;
            }
        }

        await _context.SaveChangesAsync();
        return new JsonResult(new { success = true });
    }

    public async Task<IActionResult> OnPostAdicionarComentarioAsync([FromBody] AdicionarComentarioRequest request)
    {
        var usuarioId = GetUsuarioLogadoId();
        if (usuarioId == null)
            return Unauthorized();

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

        _context.ComentariosTarefas.Add(new ComentarioTarefa
        {
            CartaoTarefaId = cartao.Id,
            UsuarioId = usuarioId.Value,
            Mensagem = texto,
            DataCriacao = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();
        return new JsonResult(new { success = true });
    }

    public async Task<IActionResult> OnPostReordenarListasAsync([FromBody] ReordenarListasRequest request)
    {
        var usuarioId = GetUsuarioLogadoId();
        if (usuarioId == null)
            return Unauthorized();

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

        var concluido = cartao.Status != StatusCartaoTarefa.Concluida;
        cartao.Status = concluido ? StatusCartaoTarefa.Concluida : StatusCartaoTarefa.Ativa;
        cartao.DataConclusao = concluido ? DateTime.UtcNow : null;
        cartao.PercentualConclusao = concluido ? 100m : 0m;
        cartao.DataAtualizacao = DateTime.UtcNow;

        RegistrarHistorico(cartao.Id, usuarioId.Value, concluido ? "Cartao concluído" : "Cartao reaberto");
        await _context.SaveChangesAsync();

        return new JsonResult(new { success = true, concluido });
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
            .AnyAsync(t => t.GrupoId == request.GrupoId && t.Ativo && t.Nome == nome);

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

                    return (IActionResult)new JsonResult(new { success = true, colunaId = coluna.Id, colunaNome = coluna.Nome });
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
            .AnyAsync(t => t.GrupoId == request.GrupoId && t.Ativo && t.Nome == nome);

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
            .AnyAsync(t => t.GrupoId == request.GrupoId && t.Ativo && t.Id != request.TemplateId && t.Nome == nome);

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
                    template.Ativo = false;
                    template.DataAtualizacao = DateTime.UtcNow;
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
        await _context.SaveChangesAsync();

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

                    cartao.ColunaId = colunaArquivo.Id;
                    cartao.DataAtualizacao = agora;
                    RegistrarHistorico(cartao.Id, usuarioId, "Cartao arquivado por limpeza de lista excluída");
                }
            }

            _context.ColunasQuadro.Remove(coluna);
        }

        await _context.SaveChangesAsync();
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
                Titulo = c.Titulo,
                CorCapa = c.CorCapa,
                DataVencimento = c.DataVencimento,
                Privado = c.Privado,
                Concluido = c.Status == StatusCartaoTarefa.Concluida
            })
            .ToListAsync();

        var cartaoIds = cartoes.Select(c => c.Id).ToList();
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

        var chamadosPorCartao = chamados
            .Where(c => UsuarioPodeVerChamado(permissao, usuarioId, c.Publico, c.CriadorChamadoId))
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
        UrgenciaChamado? urgencia)
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
        cartao.CorCapa = NormalizarCor(request.CorCapa);
        cartao.Privado = !request.CompartilharGrupo;
        cartao.DataAtualizacao = DateTime.UtcNow;

        if (novo)
            await _context.SaveChangesAsync();

        var usuariosComAcesso = await SincronizarMembrosAsync(cartao.Id, request.GrupoId, request.MembrosIds, usuarioId, cartao.CriadorId);
        await SincronizarChamadosAsync(cartao, request.ChamadosIds, request.GrupoId, usuarioId, usuariosComAcesso);
        RegistrarHistorico(cartao.Id, usuarioId, novo ? "Cartao criado" : "Cartao atualizado");
        await _context.SaveChangesAsync();

        return cartao;
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

    private async Task<List<ChamadoOpcaoViewModel>> ObterChamadosPermitidosAsync(int grupoId, List<int> usuariosIds, IEnumerable<int>? chamadosIds = null, bool limitar = true)
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
        var query = _context.Chamados.AsNoTracking()
            .Where(c => c.GrupoId == grupoId &&
                        c.Status != StatusChamado.Cancelado &&
                        c.Status != StatusChamado.Excluido);

        if (idsFiltro is { Count: > 0 })
            query = query.Where(c => idsFiltro.Contains(c.Id));

        var membrosRestritos = membros
            .Where(m => m.Permissao is PermissaoUsuario.Nenhuma or PermissaoUsuario.Colaborador)
            .Select(m => m.UsuarioId)
            .ToList();

        if (membrosRestritos.Count > 0)
            query = query.Where(c => c.Publico || membrosRestritos.Contains(c.CriadorChamadoId));

        IQueryable<Chamado> chamadosQuery = query.OrderByDescending(c => c.DataCriacao);

        if (idsFiltro is { Count: > 0 })
            chamadosQuery = chamadosQuery.Take(idsFiltro.Count);
        else if (limitar)
            chamadosQuery = chamadosQuery.Take(200);

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
                Permissao = usuarioGrupo.Permissao.ToString()
            })
            .ToListAsync();
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
        public string Titulo { get; set; } = string.Empty;
        public string? CorCapa { get; set; }
        public DateTime? DataVencimento { get; set; }
        public bool Privado { get; set; }
        public bool Concluido { get; set; }
        public List<ChamadoOpcaoViewModel> Chamados { get; set; } = new();
    }

    public class MembroViewModel
    {
        public int UsuarioId { get; set; }
        public string NomeExibicao { get; set; } = string.Empty;
        public string Permissao { get; set; } = string.Empty;
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
        public DateTime? DataInicio { get; set; }
        public DateTime? DataVencimento { get; set; }
        public string? CorCapa { get; set; }
        public bool CompartilharGrupo { get; set; }
        public List<int> MembrosIds { get; set; } = new();
        public List<int> ChamadosIds { get; set; } = new();
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
