using CallStationApp.Authorization;
using CallStationApp.Data;
using CallStationApp.Models;
using CallStationApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.Text;

namespace CallStationApp.Pages.Menu;

[Authorize]
public class GroupModel : PageModel
{
    private readonly AppDbContext _context;
    private readonly GrupoAuthorizationService _auth;
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<GroupModel> _logger;
    private readonly IWebHostEnvironment _environment;
    private readonly FotoGrupoUploadService _fotoGrupoUploadService;
    private static readonly HashSet<string> ExtensoesFotoGrupoPermitidas = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg",
        ".jpeg",
        ".png",
        ".webp"
    };
    private static readonly HashSet<string> TiposAparenciaPermitidos = new(StringComparer.OrdinalIgnoreCase)
    {
        "padrao",
        "cor",
        "preset",
        "upload"
    };
    private static readonly HashSet<string> PresetsAparenciaPermitidos = new(StringComparer.OrdinalIgnoreCase)
    {
        "aurora",
        "oceano",
        "grafite",
        "folha",
        "nevoa",
        "ameixa"
    };

    public GroupModel(AppDbContext context, GrupoAuthorizationService auth, IMemoryCache memoryCache, ILogger<GroupModel> logger, IWebHostEnvironment environment, FotoGrupoUploadService fotoGrupoUploadService)
    {
        _context = context;
        _auth = auth;
        _memoryCache = memoryCache;
        _logger = logger;
        _environment = environment;
        _fotoGrupoUploadService = fotoGrupoUploadService;
    }

    [BindProperty(SupportsGet = true)]
    public int GrupoId { get; set; }

    public Grupo GrupoAtual { get; set; } = null!;
    public GrupoConfiguracao Configuracao { get; set; } = null!;
    public string? NomeUsuarioLogado { get; set; }
    public string? FotoUsuarioLogado { get; set; }
    public bool UsuarioLogadoEhAdministrador { get; set; }
    public PermissaoUsuario UsuarioLogadoPermissao { get; set; } = PermissaoUsuario.Administracao;
    public List<ItemVm> Setores { get; set; } = new();
    public List<ItemVm> TiposOcorrencia { get; set; } = new();
    public List<CategoriaVm> Categorias { get; set; } = new();
    public List<SubcategoriaVm> Subcategorias { get; set; } = new();
    public List<TipoChamadoVm> TiposChamado { get; set; } = new();
    public List<AdminVm> Administradores { get; set; } = new();
    public List<AuditoriaVm> Auditorias { get; set; } = new();
    public int TotalChamados { get; set; }
    public int TotalMembros { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var usuarioId = GetUsuarioLogadoId();
        if (usuarioId == null)
            return RedirectToPage("/Auth/Login");

        var bloqueio = await BloquearSeNaoAdminAsync(usuarioId.Value);
        if (bloqueio != null)
            return bloqueio;

        await CarregarAsync(usuarioId.Value);
        return Page();
    }

    public async Task<IActionResult> OnPostSalvarGeralAsync([FromBody] SalvarGeralRequest request)
    {
        var usuarioId = GetUsuarioLogadoId();
        if (usuarioId == null)
            return Unauthorized();

        if (request == null)
            return BadRequest(new { success = false, message = "Dados do grupo invalidos." });

        var bloqueio = await BloquearSeNaoAdminAsync(usuarioId.Value);
        if (bloqueio != null)
            return bloqueio;

        var grupo = await _context.Grupos.FirstOrDefaultAsync(g => g.Id == GrupoId);
        if (grupo == null)
        {
            return NotFound(new { success = false, message = "Grupo não encontrado." });
        }

        var nome = Limpar(request.Nome);
        var descricao = Limpar(request.Descricao);
        var slug = Slug(request.Slug);

        if (string.IsNullOrWhiteSpace(nome) || nome.Length > 35)
            return BadRequest(new { success = false, message = "Nome obrigatório com até 35 caracteres." });

        if (descricao.Length > 200)
            return BadRequest(new { success = false, message = "Descrição deve ter até 200 caracteres." });

        if (!Enum.TryParse<EtiquetaCor>(request.EtiquetaCor, true, out var cor))
            return BadRequest(new { success = false, message = "Cor invalida." });

        var config = await ObterOuCriarConfigAsync(usuarioId.Value, false);

        if (!string.IsNullOrWhiteSpace(slug) &&
            await _context.GruposConfiguracoes.AnyAsync(c => c.GrupoId != GrupoId && c.Slug == slug))
        {
            return BadRequest(new { success = false, message = "Identificador já está em uso." });
        }

        var strategy = _context.Database.CreateExecutionStrategy();

        try
        {
            return await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    AuditarSeMudou(usuarioId.Value, "Grupo", grupo.Id, "Nome", grupo.Nome, nome);
                    AuditarSeMudou(usuarioId.Value, "Grupo", grupo.Id, "Descricao", grupo.DescricaoGrupo, descricao);
                    AuditarSeMudou(usuarioId.Value, "Grupo", grupo.Id, "Cor", grupo.EtiquetaCor.ToString(), cor.ToString());
                    AuditarSeMudou(usuarioId.Value, "GrupoConfiguracao", GrupoId, "Slug", config.Slug, slug);

                    grupo.Nome = nome;
                    grupo.DescricaoGrupo = string.IsNullOrWhiteSpace(descricao) ? null : descricao;
                    grupo.EtiquetaCor = cor;
                    config.Slug = string.IsNullOrWhiteSpace(slug) ? null : slug;
                    config.AtualizadoPorUsuarioId = usuarioId.Value;
                    config.DataAtualizacao = DateTime.UtcNow;

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();
                    return (IActionResult)new JsonResult(new { success = true, message = "Grupo salvo com sucesso." });
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
            return new JsonResult(new { success = false, message = "Este identificador (slug) já está em uso." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao salvar grupo {GrupoId}.", GrupoId);
            return StatusCode(500, new { success = false, message = "Não foi possível salvar o grupo." });
        }
    }

    public async Task<IActionResult> OnPostSalvarRegrasAsync([FromBody] SalvarRegrasRequest request)
    {
        var usuarioId = GetUsuarioLogadoId();
        if (usuarioId == null)
            return Unauthorized();

        if (request == null)
            return BadRequest(new { success = false, message = "Regras invalidas." });

        var bloqueio = await BloquearSeNaoAdminAsync(usuarioId.Value);
        if (bloqueio != null)
            return bloqueio;

        if (request.DiasParaFechamentoAutomatico is < 0 or > 365)
            return BadRequest(new { success = false, message = "Dias deve ficar entre 0 e 365." });

        var strategy = _context.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var config = await ObterOuCriarConfigAsync(usuarioId.Value);
                config.ObrigarSetor = request.ObrigarSetor;
                config.ObrigarTipoOcorrencia = request.ObrigarTipoOcorrencia;
                config.ObrigarCategoria = request.ObrigarCategoria;
                config.ObrigarSubcategoria = request.ObrigarSubcategoria;
                config.PermitirChamadoPublico = request.PermitirChamadoPublico;
                config.ExigirSolucaoParaConcluir = request.ExigirSolucaoParaConcluir;
                config.DiasParaFechamentoAutomatico = request.DiasParaFechamentoAutomatico;
                config.AtualizadoPorUsuarioId = usuarioId.Value;
                config.DataAtualizacao = DateTime.UtcNow;
                Auditar(usuarioId.Value, "Atualizar", "GrupoConfiguracao", GrupoId, "Regras", null, "Regras operacionais atualizadas");
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return (IActionResult)new JsonResult(new { success = true, message = "Regras salvas." });
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        });
    }

    public async Task<IActionResult> OnPostAlterarFotoGrupoAsync([FromForm] IFormFile? fotoGrupo)
    {
        var usuarioId = GetUsuarioLogadoId();
        if (usuarioId == null)
            return Unauthorized();

        var bloqueio = await BloquearSeNaoAdminAsync(usuarioId.Value);
        if (bloqueio != null)
            return bloqueio;

        if (fotoGrupo is not { Length: > 0 })
            return BadRequest(new { success = false, message = "Selecione uma imagem para o grupo." });

        if (!await _fotoGrupoUploadService.FotoGrupoValidaAsync(fotoGrupo))
            return BadRequest(new { success = false, message = FotoGrupoUploadService.MensagemArquivoInvalido });

        var grupo = await _context.Grupos.FirstOrDefaultAsync(g => g.Id == GrupoId);
        if (grupo == null)
            return NotFound(new { success = false, message = "Grupo não encontrado." });

        string? novaFoto = null;
        var fotoAnterior = grupo.FotoGrupo;

        try
        {
            novaFoto = await _fotoGrupoUploadService.SalvarFotoGrupoAsync(fotoGrupo, GrupoId);
            var strategy = _context.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _context.Database.BeginTransactionAsync();

                try
                {
                    AuditarSeMudou(usuarioId.Value, "Grupo", grupo.Id, "Foto", fotoAnterior, novaFoto);
                    grupo.FotoGrupo = novaFoto;
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            });

            _fotoGrupoUploadService.RemoverFotoGrupoSeExistir(fotoAnterior);

            return new JsonResult(new
            {
                success = true,
                message = "Foto do grupo atualizada.",
                dados = new { fotoGrupo = novaFoto }
            });
        }
        catch (Exception ex)
        {
            _fotoGrupoUploadService.RemoverFotoGrupoSeExistir(novaFoto);
            _logger.LogError(ex, "Erro ao alterar foto do grupo {GrupoId}.", GrupoId);
            return StatusCode(500, new { success = false, message = "Não foi possível alterar a foto do grupo." });
        }
    }

    public async Task<IActionResult> OnPostSalvarAparenciaAsync([FromBody] SalvarAparenciaRequest request)
    {
        var usuarioId = GetUsuarioLogadoId();
        if (usuarioId == null)
            return Unauthorized();

        if (request == null)
            return BadRequest(new { success = false, message = "Requisição inválida." });

        var bloqueio = await BloquearSeNaoAdminAsync(usuarioId.Value);
        if (bloqueio != null)
            return bloqueio;

        var validacao = ValidarAparencia(request.TelaTipo, request.TelaValor, request.SidebarTipo, request.SidebarValor, request.MenuAtivoCor, request.SidebarTextoFundoCor);
        if (validacao != null)
            return validacao;

        var strategy = _context.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var config = await ObterOuCriarConfigAsync(usuarioId.Value);

                AuditarSeMudou(usuarioId.Value, "GrupoConfiguracao", GrupoId, "AparenciaTelaTipo", config.AparenciaTelaTipo, NormalizarTipoAparencia(request.TelaTipo));
                AuditarSeMudou(usuarioId.Value, "GrupoConfiguracao", GrupoId, "AparenciaTelaValor", config.AparenciaTelaValor, NormalizarValorAparencia(request.TelaTipo, request.TelaValor));
                AuditarSeMudou(usuarioId.Value, "GrupoConfiguracao", GrupoId, "AparenciaSidebarTipo", config.AparenciaSidebarTipo, NormalizarTipoAparencia(request.SidebarTipo));
                AuditarSeMudou(usuarioId.Value, "GrupoConfiguracao", GrupoId, "AparenciaSidebarValor", config.AparenciaSidebarValor, NormalizarValorAparencia(request.SidebarTipo, request.SidebarValor));
                AuditarSeMudou(usuarioId.Value, "GrupoConfiguracao", GrupoId, "AparenciaMenuAtivoCor", config.AparenciaMenuAtivoCor, NormalizarCorMenuAtivo(request.MenuAtivoCor));
                AuditarSeMudou(usuarioId.Value, "GrupoConfiguracao", GrupoId, "AparenciaSidebarTextoFundoCor", config.AparenciaSidebarTextoFundoCor, NormalizarCorTextoFundo(request.SidebarTextoFundoCor));

                config.AparenciaTelaTipo = NormalizarTipoAparencia(request.TelaTipo);
                config.AparenciaTelaValor = NormalizarValorAparencia(request.TelaTipo, request.TelaValor);
                config.AparenciaSidebarTipo = NormalizarTipoAparencia(request.SidebarTipo);
                config.AparenciaSidebarValor = NormalizarValorAparencia(request.SidebarTipo, request.SidebarValor);
                config.AparenciaMenuAtivoCor = NormalizarCorMenuAtivo(request.MenuAtivoCor);
                config.AparenciaSidebarTextoFundoCor = NormalizarCorTextoFundo(request.SidebarTextoFundoCor);
                config.AtualizadoPorUsuarioId = usuarioId.Value;
                config.DataAtualizacao = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return (IActionResult)new JsonResult(new { success = true, message = "Aparência salva." });
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        });
    }

    public async Task<IActionResult> OnPostUploadAparenciaAsync([FromForm] string? alvo, [FromForm] IFormFile? imagem)
    {
        var usuarioId = GetUsuarioLogadoId();
        if (usuarioId == null)
            return Unauthorized();

        var bloqueio = await BloquearSeNaoAdminAsync(usuarioId.Value);
        if (bloqueio != null)
            return bloqueio;

        var alvoNormalizado = (alvo ?? string.Empty).Trim().ToLowerInvariant();
        if (alvoNormalizado is not ("tela" or "sidebar"))
            return BadRequest(new { success = false, message = "Área de aparência inválida." });

        if (imagem is not { Length: > 0 })
            return BadRequest(new { success = false, message = "Selecione uma imagem." });

        if (!ImagemAparenciaValida(imagem) || !await AssinaturaImagemValidaAsync(imagem))
            return BadRequest(new { success = false, message = "Envie uma imagem válida JPG, PNG ou WEBP com no máximo 3 MB." });

        string? novaImagem = null;
        string? imagemAnterior = null;

        try
        {
            novaImagem = await SalvarImagemAparenciaAsync(imagem, GrupoId);
            var strategy = _context.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    var config = await ObterOuCriarConfigAsync(usuarioId.Value);

                    if (alvoNormalizado == "tela")
                    {
                        imagemAnterior = config.AparenciaTelaTipo == "upload" ? config.AparenciaTelaValor : null;
                        AuditarSeMudou(usuarioId.Value, "GrupoConfiguracao", GrupoId, "AparenciaTelaTipo", config.AparenciaTelaTipo, "upload");
                        AuditarSeMudou(usuarioId.Value, "GrupoConfiguracao", GrupoId, "AparenciaTelaValor", config.AparenciaTelaValor, novaImagem);
                        config.AparenciaTelaTipo = "upload";
                        config.AparenciaTelaValor = novaImagem;
                    }
                    else
                    {
                        imagemAnterior = config.AparenciaSidebarTipo == "upload" ? config.AparenciaSidebarValor : null;
                        AuditarSeMudou(usuarioId.Value, "GrupoConfiguracao", GrupoId, "AparenciaSidebarTipo", config.AparenciaSidebarTipo, "upload");
                        AuditarSeMudou(usuarioId.Value, "GrupoConfiguracao", GrupoId, "AparenciaSidebarValor", config.AparenciaSidebarValor, novaImagem);
                        config.AparenciaSidebarTipo = "upload";
                        config.AparenciaSidebarValor = novaImagem;
                    }

                    config.AtualizadoPorUsuarioId = usuarioId.Value;
                    config.DataAtualizacao = DateTime.UtcNow;

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            });

            RemoverImagemAparenciaSeExistir(imagemAnterior);
            return new JsonResult(new
            {
                success = true,
                message = "Imagem de aparência salva.",
                dados = new { alvo = alvoNormalizado, url = novaImagem }
            });
        }
        catch (Exception ex)
        {
            RemoverImagemAparenciaSeExistir(novaImagem);
            _logger.LogError(ex, "Erro ao salvar imagem de aparência do grupo {GrupoId}.", GrupoId);
            return StatusCode(500, new { success = false, message = "Não foi possível salvar a imagem de aparência." });
        }
    }

    public async Task<IActionResult> OnPostSalvarSlaAsync([FromBody] SalvarSlaRequest request)
    {
        var usuarioId = GetUsuarioLogadoId();
        if (usuarioId == null)
            return Unauthorized();

        if (request == null)
            return BadRequest(new { success = false, message = "SLA invalido." });

        var bloqueio = await BloquearSeNaoAdminAsync(usuarioId.Value);
        if (bloqueio != null)
            return bloqueio;

        if (request.HorasAposVencimentoParaPendente is < 0 or > 720 ||
            request.HorasAntesPrazoParaAlerta is < 0 or > 720)
        {
            return BadRequest(new { success = false, message = "Horas deve ficar entre 0 e 720." });
        }

        var strategy = _context.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var config = await ObterOuCriarConfigAsync(usuarioId.Value);
                config.AutomatizarPendentePorPrazoConclusao = request.AutomatizarPendentePorPrazoConclusao;
                config.HorasAposVencimentoParaPendente = request.HorasAposVencimentoParaPendente;
                config.HorasAntesPrazoParaAlerta = request.HorasAntesPrazoParaAlerta;
                config.NotificarAdministradoresSla = request.NotificarAdministradoresSla;
                config.ExibirDataFinalizacaoModal = request.ExibirDataFinalizacaoModal;
                config.ExibirPrazoRespostaModal = request.ExibirPrazoRespostaModal;
                config.ExibirPrazoConclusaoModal = request.ExibirPrazoConclusaoModal;
                config.AtualizadoPorUsuarioId = usuarioId.Value;
                config.DataAtualizacao = DateTime.UtcNow;
                Auditar(usuarioId.Value, "Atualizar", "GrupoConfiguracao", GrupoId, "SLA", null, "SLA atualizado");
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return (IActionResult)new JsonResult(new { success = true, message = "SLA salvo." });
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        });
    }

    public async Task<IActionResult> OnPostCriarSetorAsync([FromBody] NomeRequest request)
    {
        var usuarioId = await ValidarPostAdminAsync();
        if (usuarioId.Result != null)
            return usuarioId.Result;

        if (request == null)
            return BadRequest(new { success = false, message = "Requisição inválida." });

        var nome = Limpar(request.Nome);
        if (!NomeValido(nome, 50))
            return BadRequest(new { success = false, message = "Nome inválido ou muito longo." });

        var strategy = _context.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                if (await _context.Setores.AnyAsync(s => s.GrupoId == GrupoId && s.NomeSetor == nome))
                    return (IActionResult)BadRequest(new { success = false, message = "Ja existe um item com este nome." });

                var usuario = await _context.Usuarios.FirstAsync(u => u.Id == usuarioId.Id);
                var setor = new Setor { GrupoId = GrupoId, UsuarioId = usuarioId.Id, Usuario = usuario, NomeSetor = nome };
                _context.Setores.Add(setor);
                Auditar(usuarioId.Id, "Criar", "Setor", null, null, null, nome);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                InvalidarCacheCatalogoGrupo();
                return (IActionResult)new JsonResult(new { success = true, message = "Setor criado." });
            }
            catch (DbUpdateException ex) when (EhErroDuplicidade(ex))
            {
                await transaction.RollbackAsync();
                return BadRequest(new { success = false, message = "Ja existe um item com este nome." });
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        });
    }

    public async Task<IActionResult> OnPostCriarTipoOcorrenciaAsync([FromBody] NomeRequest request)
    {
        var usuarioId = await ValidarPostAdminAsync();
        if (usuarioId.Result != null)
            return usuarioId.Result;

        var nome = Limpar(request.Nome);
        if (!NomeValido(nome, 50))
            return BadRequest(new { success = false, message = "Nome inválido ou muito longo." });

        var strategy = _context.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                if (await _context.OcorrenciasTipo.AnyAsync(t => t.GrupoId == GrupoId && t.TipoOcorrencia == nome))
                    return (IActionResult)BadRequest(new { success = false, message = "Ja existe um item com este nome." });

                var tipo = new OcorrenciaTipo { GrupoId = GrupoId, UsuarioId = usuarioId.Id, TipoOcorrencia = nome };
                _context.OcorrenciasTipo.Add(tipo);
                Auditar(usuarioId.Id, "Criar", "OcorrenciaTipo", null, null, null, nome);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                InvalidarCacheCatalogoGrupo();
                return (IActionResult)new JsonResult(new { success = true, message = "Tipo de ocorrencia criado." });
            }
            catch (DbUpdateException ex) when (EhErroDuplicidade(ex))
            {
                await transaction.RollbackAsync();
                return BadRequest(new { success = false, message = "Ja existe um item com este nome." });
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        });
    }

    public async Task<IActionResult> OnPostCriarCategoriaAsync([FromBody] CategoriaRequest request)
    {
        var usuarioId = await ValidarPostAdminAsync();
        if (usuarioId.Result != null)
            return usuarioId.Result;

        if (request == null)
            return BadRequest(new { success = false, message = "Requisição inválida." });

        var tipo = await _context.OcorrenciasTipo.FirstOrDefaultAsync(t => t.Id == request.TipoId && t.GrupoId == GrupoId);
        if (tipo == null)
            return BadRequest(new { success = false, message = "Tipo inválido." });

        var nome = Limpar(request.Nome);
        if (!NomeValido(nome, 50))
            return BadRequest(new { success = false, message = "Nome inválido ou muito longo." });

        var strategy = _context.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                if (await _context.OcorrenciasCategoria.AnyAsync(c => c.TipoId == request.TipoId && c.CategoriaOcorrencia == nome))
                    return (IActionResult)BadRequest(new { success = false, message = "Ja existe um item com este nome." });

                var categoria = new OcorrenciaCategoria { TipoId = tipo.Id, OcorrenciaTipo = tipo, CategoriaOcorrencia = nome };
                _context.OcorrenciasCategoria.Add(categoria);
                Auditar(usuarioId.Id, "Criar", "OcorrenciaCategoria", null, null, null, nome);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                InvalidarCacheCatalogoTipo(tipo.Id);
                return (IActionResult)new JsonResult(new { success = true, message = "Categoria criada." });
            }
            catch (DbUpdateException ex) when (EhErroDuplicidade(ex))
            {
                await transaction.RollbackAsync();
                return BadRequest(new { success = false, message = "Ja existe um item com este nome." });
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        });
    }

    public async Task<IActionResult> OnPostCriarSubcategoriaAsync([FromBody] SubcategoriaRequest request)
    {
        var usuarioId = await ValidarPostAdminAsync();
        if (usuarioId.Result != null)
            return usuarioId.Result;

        var categoria = await (from c in _context.OcorrenciasCategoria
                               join t in _context.OcorrenciasTipo on c.TipoId equals t.Id
                               where c.Id == request.CategoriaId && t.GrupoId == GrupoId
                               select c).FirstOrDefaultAsync();
        if (categoria == null)
            return BadRequest(new { success = false, message = "Categoria invalida." });

        var nome = Limpar(request.Nome);
        if (!NomeValido(nome, 100))
            return BadRequest(new { success = false, message = "Nome inválido ou muito longo." });

        var strategy = _context.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                if (await _context.OcorrenciasSubcategoria.AnyAsync(s => s.CategoriaId == categoria.Id && s.SubcategoriaOcorrencia == nome))
                    return (IActionResult)BadRequest(new { success = false, message = "Ja existe um item com este nome." });

                var subcategoria = new OcorrenciaSubcategoria { CategoriaId = categoria.Id, OcorrenciaCategoria = categoria, SubcategoriaOcorrencia = nome };
                _context.OcorrenciasSubcategoria.Add(subcategoria);
                Auditar(usuarioId.Id, "Criar", "OcorrenciaSubcategoria", null, null, null, nome);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                InvalidarCacheCatalogoCategoria(categoria.Id);
                return (IActionResult)new JsonResult(new { success = true, message = "Subcategoria criada." });
            }
            catch (DbUpdateException ex) when (EhErroDuplicidade(ex))
            {
                await transaction.RollbackAsync();
                return BadRequest(new { success = false, message = "Ja existe um item com este nome." });
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        });
    }

    public async Task<IActionResult> OnPostCriarTipoChamadoAsync([FromBody] TipoChamadoRequest request)
    {
        var usuarioId = await ValidarPostAdminAsync();
        if (usuarioId.Result != null)
            return usuarioId.Result;

        if (request == null)
            return BadRequest(new { success = false, message = "Tipo de chamado invalido." });

        var nome = Limpar(request.Nome);
        if (!NomeValido(nome, 50))
            return BadRequest(new { success = false, message = "Nome inválido ou muito longo." });

        var descricao = Limpar(request.Descricao);
        if (descricao.Length > 160)
            return BadRequest(new { success = false, message = "Descrição deve ter até 160 caracteres." });

        var strategy = _context.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                if (await _context.GruposTiposChamados.AnyAsync(t => t.GrupoId == GrupoId && t.Nome == nome))
                    return (IActionResult)BadRequest(new { success = false, message = "Tipo de chamado duplicado." });

                var posicao = (await _context.GruposTiposChamados.Where(t => t.GrupoId == GrupoId).MaxAsync(t => (int?)t.Posicao) ?? 0) + 1;
                var tipo = new GrupoTipoChamado { GrupoId = GrupoId, Nome = nome, Descricao = descricao, Posicao = posicao, CriadoPorUsuarioId = usuarioId.Id };
                _context.GruposTiposChamados.Add(tipo);
                Auditar(usuarioId.Id, "Criar", "GrupoTipoChamado", null, null, null, nome);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return (IActionResult)new JsonResult(new { success = true, message = "Tipo de chamado criado." });
            }
            catch (DbUpdateException ex) when (EhErroDuplicidade(ex))
            {
                await transaction.RollbackAsync();
                return BadRequest(new { success = false, message = "Tipo de chamado duplicado." });
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        });
    }

    public async Task<IActionResult> OnPostArquivarTipoChamadoAsync([FromBody] IdRequest request)
    {
        var usuarioId = await ValidarPostAdminAsync();
        if (usuarioId.Result != null)
            return usuarioId.Result;

        if (request == null || request.Id <= 0)
            return BadRequest(new { success = false, message = "Tipo de chamado invalido." });

        var tipo = await _context.GruposTiposChamados.FirstOrDefaultAsync(t => t.Id == request.Id && t.GrupoId == GrupoId);
        if (tipo == null)
            return NotFound(new { success = false, message = "Tipo de chamado não encontrado." });

        var strategy = _context.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                tipo.Ativo = false;
                tipo.ArquivadoPorUsuarioId = usuarioId.Id;
                tipo.DataArquivamento = DateTime.UtcNow;
                Auditar(usuarioId.Id, "Arquivar", "GrupoTipoChamado", tipo.Id, null, tipo.Nome, null);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return (IActionResult)new JsonResult(new { success = true, message = "Tipo de chamado arquivado." });
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        });
    }

    public Task<IActionResult> OnPostEditarSetorAsync([FromBody] EditarNomeRequest request) =>
        ExecutarCatalogoAsync(async usuarioId =>
        {
            var setor = await _context.Setores.FirstOrDefaultAsync(s => s.Id == request.Id && s.GrupoId == GrupoId);
            if (setor == null)
                return NotFound(new { success = false, message = "Setor não encontrado." });

            var nome = Limpar(request.Nome);
            if (!NomeValido(nome, 50))
                return BadRequest(new { success = false, message = "Nome inválido ou muito longo." });

            if (await _context.Setores.AnyAsync(s => s.GrupoId == GrupoId && s.Id != request.Id && s.NomeSetor == nome))
                return BadRequest(new { success = false, message = "Ja existe um item com este nome." });

            AuditarSeMudou(usuarioId, "Setor", setor.Id, "Nome", setor.NomeSetor, nome);
            setor.NomeSetor = nome;
            await _context.SaveChangesAsync();
            InvalidarCacheCatalogoGrupo();
            return new JsonResult(new { success = true, message = "Setor atualizado." });
        });

    public Task<IActionResult> OnPostEditarTipoOcorrenciaAsync([FromBody] EditarNomeRequest request) =>
        ExecutarCatalogoAsync(async usuarioId =>
        {
            var tipo = await _context.OcorrenciasTipo.FirstOrDefaultAsync(t => t.Id == request.Id && t.GrupoId == GrupoId);
            if (tipo == null)
                return NotFound(new { success = false, message = "Tipo de ocorrência não encontrado." });

            var nome = Limpar(request.Nome);
            if (!NomeValido(nome, 50))
                return BadRequest(new { success = false, message = "Nome inválido ou muito longo." });

            if (await _context.OcorrenciasTipo.AnyAsync(t => t.GrupoId == GrupoId && t.Id != request.Id && t.TipoOcorrencia == nome))
                return BadRequest(new { success = false, message = "Ja existe um item com este nome." });

            AuditarSeMudou(usuarioId, "OcorrenciaTipo", tipo.Id, "Nome", tipo.TipoOcorrencia, nome);
            tipo.TipoOcorrencia = nome;
            await _context.SaveChangesAsync();
            InvalidarCacheCatalogoGrupo();
            return new JsonResult(new { success = true, message = "Tipo de ocorrência atualizado." });
        });

    public Task<IActionResult> OnPostEditarCategoriaAsync([FromBody] EditarNomeRequest request) =>
        ExecutarCatalogoAsync(async usuarioId =>
        {
            var categoria = await (from c in _context.OcorrenciasCategoria
                                   join t in _context.OcorrenciasTipo on c.TipoId equals t.Id
                                   where c.Id == request.Id && t.GrupoId == GrupoId
                                   select c).FirstOrDefaultAsync();
            if (categoria == null)
                return NotFound(new { success = false, message = "Categoria não encontrada." });

            var nome = Limpar(request.Nome);
            if (!NomeValido(nome, 50))
                return BadRequest(new { success = false, message = "Nome inválido ou muito longo." });

            if (await _context.OcorrenciasCategoria.AnyAsync(c => c.TipoId == categoria.TipoId && c.Id != request.Id && c.CategoriaOcorrencia == nome))
                return BadRequest(new { success = false, message = "Ja existe um item com este nome." });

            AuditarSeMudou(usuarioId, "OcorrenciaCategoria", categoria.Id, "Nome", categoria.CategoriaOcorrencia, nome);
            categoria.CategoriaOcorrencia = nome;
            await _context.SaveChangesAsync();
            InvalidarCacheCatalogoTipo(categoria.TipoId);
            return new JsonResult(new { success = true, message = "Categoria atualizada." });
        });

    public Task<IActionResult> OnPostEditarSubcategoriaAsync([FromBody] EditarNomeRequest request) =>
        ExecutarCatalogoAsync(async usuarioId =>
        {
            var subcategoria = await (from s in _context.OcorrenciasSubcategoria
                                      join c in _context.OcorrenciasCategoria on s.CategoriaId equals c.Id
                                      join t in _context.OcorrenciasTipo on c.TipoId equals t.Id
                                      where s.Id == request.Id && t.GrupoId == GrupoId
                                      select s).FirstOrDefaultAsync();
            if (subcategoria == null)
                return NotFound(new { success = false, message = "Subcategoria não encontrada." });

            var nome = Limpar(request.Nome);
            if (!NomeValido(nome, 100))
                return BadRequest(new { success = false, message = "Nome inválido ou muito longo." });

            if (await _context.OcorrenciasSubcategoria.AnyAsync(s => s.CategoriaId == subcategoria.CategoriaId && s.Id != request.Id && s.SubcategoriaOcorrencia == nome))
                return BadRequest(new { success = false, message = "Ja existe um item com este nome." });

            AuditarSeMudou(usuarioId, "OcorrenciaSubcategoria", subcategoria.Id, "Nome", subcategoria.SubcategoriaOcorrencia, nome);
            subcategoria.SubcategoriaOcorrencia = nome;
            await _context.SaveChangesAsync();
            InvalidarCacheCatalogoCategoria(subcategoria.CategoriaId);
            return new JsonResult(new { success = true, message = "Subcategoria atualizada." });
        });

    public Task<IActionResult> OnPostEditarTipoChamadoAsync([FromBody] EditarTipoChamadoRequest request) =>
        ExecutarCatalogoAsync(async usuarioId =>
        {
            var tipo = await _context.GruposTiposChamados.FirstOrDefaultAsync(t => t.Id == request.Id && t.GrupoId == GrupoId);
            if (tipo == null)
                return NotFound(new { success = false, message = "Tipo de chamado não encontrado." });

            var nome = Limpar(request.Nome);
            var descricao = Limpar(request.Descricao);
            if (!NomeValido(nome, 50))
                return BadRequest(new { success = false, message = "Nome inválido ou muito longo." });
            if (descricao.Length > 160)
                return BadRequest(new { success = false, message = "Descrição deve ter até 160 caracteres." });

            if (await _context.GruposTiposChamados.AnyAsync(t => t.GrupoId == GrupoId && t.Id != request.Id && t.Nome == nome))
                return BadRequest(new { success = false, message = "Tipo de chamado duplicado." });

            AuditarSeMudou(usuarioId, "GrupoTipoChamado", tipo.Id, "Nome", tipo.Nome, nome);
            AuditarSeMudou(usuarioId, "GrupoTipoChamado", tipo.Id, "Descricao", tipo.Descricao, descricao);
            tipo.Nome = nome;
            tipo.Descricao = string.IsNullOrWhiteSpace(descricao) ? null : descricao;
            await _context.SaveChangesAsync();
            return new JsonResult(new { success = true, message = "Tipo de chamado atualizado." });
        });

    public Task<IActionResult> OnPostExcluirTipoOcorrenciaAsync([FromBody] IdRequest request) =>
        ExecutarCatalogoAsync(async usuarioId =>
        {
            var tipo = await _context.OcorrenciasTipo.FirstOrDefaultAsync(t => t.Id == request.Id && t.GrupoId == GrupoId);
            if (tipo == null)
                return NotFound(new { success = false, message = "Tipo de ocorrência não encontrado." });
            if (await _context.Chamados.AnyAsync(c => c.GrupoId == GrupoId && c.OcorrenciaTipoId == request.Id))
                return BadRequest(new { success = false, message = "Tipo vinculado a chamados. Exclusão bloqueada." });
            if (await _context.OcorrenciasCategoria.AnyAsync(c => c.TipoId == request.Id))
                return BadRequest(new { success = false, message = "Tipo possui categorias. Exclua as categorias antes." });

            _context.OcorrenciasTipo.Remove(tipo);
            Auditar(usuarioId, "Excluir", "OcorrenciaTipo", tipo.Id, null, tipo.TipoOcorrencia, null);
            await _context.SaveChangesAsync();
            InvalidarCacheCatalogoGrupo();
            return new JsonResult(new { success = true, message = "Tipo de ocorrência excluído." });
        });

    public Task<IActionResult> OnPostExcluirCategoriaAsync([FromBody] IdRequest request) =>
        ExecutarCatalogoAsync(async usuarioId =>
        {
            var categoria = await (from c in _context.OcorrenciasCategoria
                                   join t in _context.OcorrenciasTipo on c.TipoId equals t.Id
                                   where c.Id == request.Id && t.GrupoId == GrupoId
                                   select c).FirstOrDefaultAsync();
            if (categoria == null)
                return NotFound(new { success = false, message = "Categoria não encontrada." });
            if (await _context.Chamados.AnyAsync(c => c.GrupoId == GrupoId && c.OcorrenciaCategoriaId == request.Id))
                return BadRequest(new { success = false, message = "Categoria vinculada a chamados. Exclusão bloqueada." });
            if (await _context.OcorrenciasSubcategoria.AnyAsync(s => s.CategoriaId == request.Id))
                return BadRequest(new { success = false, message = "Categoria possui subcategorias. Exclua as subcategorias antes." });

            _context.OcorrenciasCategoria.Remove(categoria);
            Auditar(usuarioId, "Excluir", "OcorrenciaCategoria", categoria.Id, null, categoria.CategoriaOcorrencia, null);
            await _context.SaveChangesAsync();
            InvalidarCacheCatalogoTipo(categoria.TipoId);
            return new JsonResult(new { success = true, message = "Categoria excluída." });
        });

    public Task<IActionResult> OnPostExcluirSubcategoriaAsync([FromBody] IdRequest request) =>
        ExecutarCatalogoAsync(async usuarioId =>
        {
            var subcategoria = await (from s in _context.OcorrenciasSubcategoria
                                      join c in _context.OcorrenciasCategoria on s.CategoriaId equals c.Id
                                      join t in _context.OcorrenciasTipo on c.TipoId equals t.Id
                                      where s.Id == request.Id && t.GrupoId == GrupoId
                                      select s).FirstOrDefaultAsync();
            if (subcategoria == null)
                return NotFound(new { success = false, message = "Subcategoria não encontrada." });
            if (await _context.Chamados.AnyAsync(c => c.GrupoId == GrupoId && c.OcorrenciaSubcategoriaId == request.Id))
                return BadRequest(new { success = false, message = "Subcategoria vinculada a chamados. Exclusão bloqueada." });

            _context.OcorrenciasSubcategoria.Remove(subcategoria);
            Auditar(usuarioId, "Excluir", "OcorrenciaSubcategoria", subcategoria.Id, null, subcategoria.SubcategoriaOcorrencia, null);
            await _context.SaveChangesAsync();
            InvalidarCacheCatalogoCategoria(subcategoria.CategoriaId);
            return new JsonResult(new { success = true, message = "Subcategoria excluída." });
        });

    public Task<IActionResult> OnPostExcluirTipoChamadoAsync([FromBody] IdRequest request) =>
        ExecutarCatalogoAsync(async usuarioId =>
        {
            var tipo = await _context.GruposTiposChamados.FirstOrDefaultAsync(t => t.Id == request.Id && t.GrupoId == GrupoId);
            if (tipo == null)
                return NotFound(new { success = false, message = "Tipo de chamado não encontrado." });

            _context.GruposTiposChamados.Remove(tipo);
            Auditar(usuarioId, "Excluir", "GrupoTipoChamado", tipo.Id, null, tipo.Nome, null);
            await _context.SaveChangesAsync();
            return new JsonResult(new { success = true, message = "Tipo de chamado excluído." });
        });

    public Task<IActionResult> OnPostExcluirSetorAsync([FromBody] IdRequest request) =>
        ExecutarCatalogoAsync(async usuarioId =>
        {
            var emUso = await _context.Chamados.AnyAsync(c => c.GrupoId == GrupoId && c.SetorId == request.Id);
            if (emUso)
                return BadRequest(new { success = false, message = "Setor vinculado a chamados. Exclusão bloqueada." });

            var setor = await _context.Setores.FirstOrDefaultAsync(s => s.Id == request.Id && s.GrupoId == GrupoId);
            if (setor == null)
                return NotFound(new { success = false, message = "Setor não encontrado." });

            _context.Setores.Remove(setor);
            Auditar(usuarioId, "Excluir", "Setor", setor.Id, null, setor.NomeSetor, null);
            await _context.SaveChangesAsync();
            InvalidarCacheCatalogoGrupo();
            return new JsonResult(new { success = true, message = "Setor excluído." });
        });

    public async Task<IActionResult> OnPostExcluirGrupoAsync([FromBody] ExcluirGrupoRequest request)
    {
        var usuarioId = await ValidarPostAdminAsync();
        if (usuarioId.Result != null)
            return usuarioId.Result;

        if (request == null)
            return BadRequest(new { success = false, message = "Requisição inválida." });

        var grupo = await _context.Grupos.FirstOrDefaultAsync(g => g.Id == GrupoId);
        if (grupo == null)
            return NotFound(new { success = false, message = "Grupo não encontrado." });

        var nomeConfirmacao = Limpar(request.NomeConfirmacao);
        if (!string.Equals(nomeConfirmacao, grupo.Nome, StringComparison.Ordinal))
            return BadRequest(new { success = false, message = "Digite o nome do grupo exatamente como exibido para confirmar." });

        var strategy = _context.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var agora = DateTime.UtcNow;
                var config = await ObterOuCriarConfigAsync(usuarioId.Id);
                var membrosAtivos = await _context.UsuariosGrupos
                    .Where(ug => ug.GrupoId == GrupoId && ug.Ativo)
                    .ToListAsync();
                config.Ativo = false;
                config.AtualizadoPorUsuarioId = usuarioId.Id;
                config.DataAtualizacao = agora;

                foreach (var membro in membrosAtivos)
                {
                    membro.Ativo = false;
                    membro.DataRemocao = agora;
                    membro.RemovidoPorUsuarioId = usuarioId.Id;
                }

                Auditar(usuarioId.Id, "Excluir", "Grupo", grupo.Id, "Acesso", "Ativo", "Inativo para todos os membros");
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                InvalidarCacheCatalogoGrupo();
                return (IActionResult)new JsonResult(new
                {
                    success = true,
                    message = "Grupo excluído.",
                    redirectUrl = Url.Page("/Menu/Menu")
                });
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        });
    }

    private void InvalidarCacheCatalogoGrupo()
    {
        _memoryCache.Remove($"catalogo:setores:{GrupoId}");
        _memoryCache.Remove($"catalogo:tipos:{GrupoId}");
    }

    private void InvalidarCacheCatalogoTipo(int tipoId) =>
        _memoryCache.Remove($"catalogo:categorias:{tipoId}");

    private void InvalidarCacheCatalogoCategoria(int categoriaId) =>
        _memoryCache.Remove($"catalogo:subcategorias:{categoriaId}");

    private async Task<IActionResult> ExecutarCatalogoAsync(Func<int, Task<IActionResult>> operacao)
    {
        var usuarioId = await ValidarPostAdminAsync();
        if (usuarioId.Result != null)
            return usuarioId.Result;

        var strategy = _context.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var resultado = await operacao(usuarioId.Id);
                await transaction.CommitAsync();
                return resultado;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        });
    }

    private async Task CarregarAsync(int usuarioId)
    {
        GrupoAtual = await _context.Grupos.AsNoTracking().FirstAsync(g => g.Id == GrupoId);
        Configuracao = await ObterOuCriarConfigAsync(usuarioId);
        UsuarioLogadoEhAdministrador = true;
        UsuarioLogadoPermissao = PermissaoUsuario.Administracao;

        var usuario = await _context.Usuarios.AsNoTracking()
            .Where(u => u.Id == usuarioId)
            .Select(u => new { u.NomeUsuario, u.FotoUsuario })
            .FirstOrDefaultAsync();

        NomeUsuarioLogado = usuario?.NomeUsuario;
        FotoUsuarioLogado = usuario?.FotoUsuario;
        TotalChamados = await _context.Chamados.AsNoTracking().CountAsync(c => c.GrupoId == GrupoId);
        TotalMembros = await _context.UsuariosGrupos.AsNoTracking().CountAsync(ug => ug.GrupoId == GrupoId && ug.Ativo);

        Administradores = await (from ug in _context.UsuariosGrupos.AsNoTracking()
                                 join u in _context.Usuarios.AsNoTracking() on ug.UsuarioId equals u.Id
                                 where ug.GrupoId == GrupoId && ug.Ativo && ug.Permissao == PermissaoUsuario.Administracao
                                 orderby u.NomeCompleto
                                 select new AdminVm(u.NomeCompleto, u.NomeUsuario)).ToListAsync();

        var contagemChamadosPorSetor = await _context.Chamados
            .AsNoTracking()
            .Where(c => c.GrupoId == GrupoId && c.SetorId.HasValue)
            .GroupBy(c => c.SetorId!.Value)
            .Select(g => new { SetorId = g.Key, Total = g.Count() })
            .ToDictionaryAsync(x => x.SetorId, x => x.Total);

        var contagemChamadosPorTipo = await _context.Chamados
            .AsNoTracking()
            .Where(c => c.GrupoId == GrupoId && c.OcorrenciaTipoId.HasValue)
            .GroupBy(c => c.OcorrenciaTipoId!.Value)
            .Select(g => new { TipoId = g.Key, Total = g.Count() })
            .ToDictionaryAsync(x => x.TipoId, x => x.Total);

        var contagemChamadosPorCategoria = await _context.Chamados
            .AsNoTracking()
            .Where(c => c.GrupoId == GrupoId && c.OcorrenciaCategoriaId.HasValue)
            .GroupBy(c => c.OcorrenciaCategoriaId!.Value)
            .Select(g => new { CategoriaId = g.Key, Total = g.Count() })
            .ToDictionaryAsync(x => x.CategoriaId, x => x.Total);

        var contagemChamadosPorSubcategoria = await _context.Chamados
            .AsNoTracking()
            .Where(c => c.GrupoId == GrupoId && c.OcorrenciaSubcategoriaId.HasValue)
            .GroupBy(c => c.OcorrenciaSubcategoriaId!.Value)
            .Select(g => new { SubcategoriaId = g.Key, Total = g.Count() })
            .ToDictionaryAsync(x => x.SubcategoriaId, x => x.Total);

        var contagemSubcategoriasPorCategoria = await (
                from s in _context.OcorrenciasSubcategoria.AsNoTracking()
                join c in _context.OcorrenciasCategoria.AsNoTracking() on s.CategoriaId equals c.Id
                join t in _context.OcorrenciasTipo.AsNoTracking() on c.TipoId equals t.Id
                where t.GrupoId == GrupoId
                group s by s.CategoriaId
                into g
                select new { CategoriaId = g.Key, Total = g.Count() })
            .ToDictionaryAsync(x => x.CategoriaId, x => x.Total);

        var setores = await _context.Setores.AsNoTracking()
            .Where(s => s.GrupoId == GrupoId)
            .OrderBy(s => s.NomeSetor)
            .ToListAsync();

        Setores = setores
            .Select(s => new ItemVm(s.Id, s.NomeSetor, contagemChamadosPorSetor.GetValueOrDefault(s.Id)))
            .ToList();

        var tiposOcorrencia = await _context.OcorrenciasTipo.AsNoTracking()
            .Where(t => t.GrupoId == GrupoId)
            .OrderBy(t => t.TipoOcorrencia)
            .ToListAsync();

        TiposOcorrencia = tiposOcorrencia
            .Select(t => new ItemVm(t.Id, t.TipoOcorrencia, contagemChamadosPorTipo.GetValueOrDefault(t.Id)))
            .ToList();

        var categorias = await (from c in _context.OcorrenciasCategoria.AsNoTracking()
                                join t in _context.OcorrenciasTipo.AsNoTracking() on c.TipoId equals t.Id
                                where t.GrupoId == GrupoId
                                orderby t.TipoOcorrencia, c.CategoriaOcorrencia
                                select new { c.Id, c.TipoId, TipoNome = t.TipoOcorrencia, Nome = c.CategoriaOcorrencia })
            .ToListAsync();

        Categorias = categorias
            .Select(c => new CategoriaVm(
                c.Id,
                c.TipoId,
                c.TipoNome,
                c.Nome,
                contagemSubcategoriasPorCategoria.GetValueOrDefault(c.Id),
                contagemChamadosPorCategoria.GetValueOrDefault(c.Id)))
            .ToList();

        var subcategorias = await (from s in _context.OcorrenciasSubcategoria.AsNoTracking()
                                   join c in _context.OcorrenciasCategoria.AsNoTracking() on s.CategoriaId equals c.Id
                                   join t in _context.OcorrenciasTipo.AsNoTracking() on c.TipoId equals t.Id
                                   where t.GrupoId == GrupoId
                                   orderby t.TipoOcorrencia, c.CategoriaOcorrencia, s.SubcategoriaOcorrencia
                                   select new
                                   {
                                       SubcategoriaId = s.Id,
                                       CategoriaId = c.Id,
                                       TipoNome = t.TipoOcorrencia,
                                       CategoriaNome = c.CategoriaOcorrencia,
                                       Nome = s.SubcategoriaOcorrencia
                                   })
            .ToListAsync();

        Subcategorias = subcategorias
            .Select(s => new SubcategoriaVm(
                s.SubcategoriaId,
                s.CategoriaId,
                s.TipoNome,
                s.CategoriaNome,
                s.Nome,
                contagemChamadosPorSubcategoria.GetValueOrDefault(s.SubcategoriaId)))
            .ToList();

        TiposChamado = await _context.GruposTiposChamados.AsNoTracking()
            .Where(t => t.GrupoId == GrupoId)
            .OrderBy(t => t.Ativo ? 0 : 1)
            .ThenBy(t => t.Posicao)
            .Select(t => new TipoChamadoVm(t.Id, t.Nome, t.Descricao, t.Ativo, t.Posicao))
            .ToListAsync();

        Auditorias = await (from a in _context.GruposAuditorias.AsNoTracking()
                            join u in _context.Usuarios.AsNoTracking() on a.UsuarioId equals u.Id
                            where a.GrupoId == GrupoId
                            orderby a.DataAcao descending
                            select new AuditoriaVm(a.DataAcao, u.NomeUsuario, a.TipoAcao, a.Entidade, a.CampoAlterado, a.ValorAnterior, a.ValorNovo))
            .Take(30)
            .ToListAsync();
    }

    private async Task<IActionResult?> BloquearSeNaoAdminAsync(int usuarioId)
    {
        if (GrupoId <= 0)
            return RedirectToPage("/Menu/Menu");

        var contexto = await _auth.ObterContextoMembroAsync(usuarioId, GrupoId);
        if (contexto == null)
            return NotFound();

        if (!GrupoPermissionService.PodeGerenciarGrupo(contexto.Permissao))
            return Forbid();

        return null;
    }

    private async Task<(int Id, IActionResult? Result)> ValidarPostAdminAsync()
    {
        var usuarioId = GetUsuarioLogadoId();
        if (usuarioId == null)
            return (0, Unauthorized());

        var bloqueio = await BloquearSeNaoAdminAsync(usuarioId.Value);
        return bloqueio == null ? (usuarioId.Value, null) : (0, bloqueio);
    }

    private async Task<GrupoConfiguracao> ObterOuCriarConfigAsync(int usuarioId, bool salvarImediatamente = true)
    {
        var config = await _context.GruposConfiguracoes.FirstOrDefaultAsync(c => c.GrupoId == GrupoId);
        if (config != null)
            return config;

        config = new GrupoConfiguracao
        {
            GrupoId = GrupoId,
            Slug = await GerarSlugUnicoAsync(await _context.Grupos.Where(g => g.Id == GrupoId).Select(g => g.Nome).FirstAsync()),
            AtualizadoPorUsuarioId = usuarioId,
            DataAtualizacao = DateTime.UtcNow
        };
        _context.GruposConfiguracoes.Add(config);
        if (salvarImediatamente && _context.Database.CurrentTransaction == null)
            await _context.SaveChangesAsync();
        return config;
    }

    private async Task<string?> GerarSlugUnicoAsync(string? valor)
    {
        var slugBase = Slug(valor);
        if (string.IsNullOrWhiteSpace(slugBase))
            return null;

        var prefixoBusca = slugBase.Length > 50 ? slugBase[..50].TrimEnd('-') : slugBase;
        var slugsEncontrados = await _context.GruposConfiguracoes
            .AsNoTracking()
            .Where(c => c.GrupoId != GrupoId &&
                        c.Slug != null &&
                        (c.Slug == slugBase || c.Slug.StartsWith(prefixoBusca)))
            .Select(c => c.Slug!)
            .ToListAsync();
        var slugsUsados = slugsEncontrados.ToHashSet(StringComparer.Ordinal);

        if (!slugsUsados.Contains(slugBase))
            return slugBase;

        for (var sufixo = 2; sufixo < 10_000; sufixo++)
        {
            var textoSufixo = $"-{sufixo}";
            var tamanhoBase = Math.Min(slugBase.Length, 60 - textoSufixo.Length);
            var candidato = $"{slugBase[..tamanhoBase].TrimEnd('-')}{textoSufixo}";

            if (!slugsUsados.Contains(candidato))
                return candidato;
        }

        return null;
    }

    private void AuditarSeMudou(int usuarioId, string entidade, int? entidadeId, string campo, string? anterior, string? novo)
    {
        if (!string.Equals(anterior ?? "", novo ?? "", StringComparison.Ordinal))
            Auditar(usuarioId, "Atualizar", entidade, entidadeId, campo, anterior, novo);
    }

    private void Auditar(int usuarioId, string acao, string entidade, int? entidadeId, string? campo, string? anterior, string? novo)
    {
        _context.GruposAuditorias.Add(new GrupoAuditoria
        {
            GrupoId = GrupoId,
            UsuarioId = usuarioId,
            TipoAcao = acao,
            Entidade = entidade,
            EntidadeId = entidadeId,
            CampoAlterado = campo,
            ValorAnterior = Cortar(anterior, 500),
            ValorNovo = Cortar(novo, 500),
            IpOrigem = HttpContext.Connection.RemoteIpAddress?.ToString(),
            UserAgent = Cortar(Request.Headers.UserAgent.ToString(), 255),
            DataAcao = DateTime.UtcNow
        });
    }

    private static bool NomeValido(string nome, int limite = 50) =>
        !string.IsNullOrWhiteSpace(nome) && nome.Trim().Length <= limite;

    private static bool EhErroDuplicidade(DbUpdateException ex)
    {
        var mensagem = ex.InnerException?.Message ?? ex.Message;
        return mensagem.Contains("Duplicate", StringComparison.OrdinalIgnoreCase) ||
               mensagem.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase);
    }

    private IActionResult? ValidarAparencia(string? telaTipo, string? telaValor, string? sidebarTipo, string? sidebarValor, string? menuAtivoCor, string? sidebarTextoFundoCor)
    {
        var validacaoTela = ValidarAlvoAparencia(telaTipo, telaValor, "tela");
        if (validacaoTela != null)
            return validacaoTela;

        var validacaoSidebar = ValidarAlvoAparencia(sidebarTipo, sidebarValor, "barra lateral");
        if (validacaoSidebar != null)
            return validacaoSidebar;

        if (!EhCorHexValida(NormalizarCorMenuAtivo(menuAtivoCor)))
            return BadRequest(new { success = false, message = "Cor do campo selecionado inválida." });

        var corTextoFundo = NormalizarCorTextoFundo(sidebarTextoFundoCor);
        if (!string.IsNullOrWhiteSpace(corTextoFundo) && !EhCorHexValida(corTextoFundo))
            return BadRequest(new { success = false, message = "Cor de fundo dos textos inválida." });

        return null;
    }

    private IActionResult? ValidarAlvoAparencia(string? tipo, string? valor, string nome)
    {
        var tipoNormalizado = NormalizarTipoAparencia(tipo);
        var valorNormalizado = NormalizarValorAparencia(tipo, valor);

        if (!TiposAparenciaPermitidos.Contains(tipoNormalizado))
            return BadRequest(new { success = false, message = $"Tipo de aparência da {nome} inválido." });

        if (tipoNormalizado == "cor" && !EhCorHexValida(valorNormalizado))
            return BadRequest(new { success = false, message = $"Cor da {nome} inválida." });

        if (tipoNormalizado == "preset" && !PresetsAparenciaPermitidos.Contains(valorNormalizado ?? string.Empty))
            return BadRequest(new { success = false, message = $"Imagem pré-definida da {nome} inválida." });

        if (tipoNormalizado == "upload" && !EhCaminhoUploadAparenciaValido(valorNormalizado))
            return BadRequest(new { success = false, message = $"Imagem da {nome} inválida." });

        return null;
    }

    private static string NormalizarTipoAparencia(string? tipo)
    {
        var tipoNormalizado = (tipo ?? "padrao").Trim().ToLowerInvariant();
        return string.IsNullOrWhiteSpace(tipoNormalizado) ? "padrao" : tipoNormalizado;
    }

    private static string? NormalizarValorAparencia(string? tipo, string? valor)
    {
        var tipoNormalizado = NormalizarTipoAparencia(tipo);
        var valorNormalizado = (valor ?? string.Empty).Trim();
        if (tipoNormalizado == "padrao")
            return null;

        return string.IsNullOrWhiteSpace(valorNormalizado) ? null : valorNormalizado;
    }

    private static string NormalizarCorMenuAtivo(string? valor) =>
        EhCorHexValida((valor ?? string.Empty).Trim()) ? valor!.Trim() : "#0d6efd";

    private static string? NormalizarCorTextoFundo(string? valor)
    {
        var cor = (valor ?? string.Empty).Trim();
        return EhCorHexValida(cor) ? cor : null;
    }

    private static bool EhCorHexValida(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor) || valor.Length != 7 || valor[0] != '#')
            return false;

        return valor.Skip(1).All(Uri.IsHexDigit);
    }

    private static bool EhCaminhoUploadAparenciaValido(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
            return false;

        if (!valor.StartsWith("/uploads/grupos/backgrounds/", StringComparison.OrdinalIgnoreCase))
            return false;

        var nomeArquivo = Path.GetFileName(valor);
        return !string.IsNullOrWhiteSpace(nomeArquivo) &&
               string.Equals(valor, $"/uploads/grupos/backgrounds/{nomeArquivo}", StringComparison.OrdinalIgnoreCase) &&
               ExtensoesFotoGrupoPermitidas.Contains(Path.GetExtension(nomeArquivo));
    }

    private static bool ImagemAparenciaValida(IFormFile arquivo)
    {
        var extensao = Path.GetExtension(arquivo.FileName);
        return arquivo.Length <= 3 * 1024 * 1024 && ExtensoesFotoGrupoPermitidas.Contains(extensao);
    }

    private static async Task<bool> AssinaturaImagemValidaAsync(IFormFile arquivo)
    {
        var buffer = new byte[12];
        await using var stream = arquivo.OpenReadStream();
        var bytesLidos = await stream.ReadAsync(buffer);
        if (bytesLidos < 12)
            return false;

        var extensao = Path.GetExtension(arquivo.FileName).ToLowerInvariant();
        return extensao switch
        {
            ".jpg" or ".jpeg" => buffer[0] == 0xFF && buffer[1] == 0xD8 && buffer[2] == 0xFF,
            ".png" => buffer[0] == 0x89 &&
                      buffer[1] == 0x50 &&
                      buffer[2] == 0x4E &&
                      buffer[3] == 0x47 &&
                      buffer[4] == 0x0D &&
                      buffer[5] == 0x0A &&
                      buffer[6] == 0x1A &&
                      buffer[7] == 0x0A,
            ".webp" => buffer[0] == 0x52 &&
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

    private async Task<string> SalvarImagemAparenciaAsync(IFormFile arquivo, int grupoId)
    {
        var extensao = Path.GetExtension(arquivo.FileName).ToLowerInvariant();
        var pasta = Path.Combine(_environment.WebRootPath, "uploads", "grupos", "backgrounds");
        Directory.CreateDirectory(pasta);

        var nomeArquivo = $"grupo-bg-{grupoId}-{Guid.NewGuid():N}{extensao}";
        var caminho = Path.Combine(pasta, nomeArquivo);

        await using var stream = new FileStream(caminho, FileMode.Create);
        await arquivo.CopyToAsync(stream);

        return $"/uploads/grupos/backgrounds/{nomeArquivo}";
    }

    private void RemoverImagemAparenciaSeExistir(string? caminhoRelativo)
    {
        if (string.IsNullOrWhiteSpace(caminhoRelativo) ||
            !caminhoRelativo.StartsWith("/uploads/grupos/backgrounds/", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var nomeArquivo = Path.GetFileName(caminhoRelativo);
        if (string.IsNullOrWhiteSpace(nomeArquivo))
            return;

        var caminho = Path.Combine(_environment.WebRootPath, "uploads", "grupos", "backgrounds", nomeArquivo);
        if (System.IO.File.Exists(caminho))
            System.IO.File.Delete(caminho);
    }

    private static string Limpar(string? valor) => (valor ?? string.Empty).Trim();

    private static string? Cortar(string? valor, int limite) =>
        string.IsNullOrWhiteSpace(valor) ? null : valor.Length <= limite ? valor : valor[..limite];

    private static string? Slug(string? valor)
    {
        var texto = Limpar(valor).ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(texto))
            return null;

        var sb = new StringBuilder();
        var separador = false;
        foreach (var c in texto.Normalize(NormalizationForm.FormD))
        {
            var categoria = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c);
            if (categoria == System.Globalization.UnicodeCategory.NonSpacingMark)
                continue;
            if (char.IsLetterOrDigit(c))
            {
                sb.Append(c);
                separador = false;
            }
            else if (!separador)
            {
                sb.Append('-');
                separador = true;
            }
        }

        var slug = sb.ToString().Trim('-');
        return slug.Length <= 60 ? slug : slug[..60].TrimEnd('-');
    }

    private int? GetUsuarioLogadoId()
    {
        var claim = User.FindFirst("Id")?.Value;
        return int.TryParse(claim, out var id) ? id : null;
    }

    public record SalvarGeralRequest(string? Nome, string? Descricao, string? Slug, string? EtiquetaCor);
    public record SalvarAparenciaRequest(string? TelaTipo, string? TelaValor, string? SidebarTipo, string? SidebarValor, string? MenuAtivoCor, string? SidebarTextoFundoCor);
    public record SalvarRegrasRequest(bool ObrigarSetor, bool ObrigarTipoOcorrencia, bool ObrigarCategoria, bool ObrigarSubcategoria, bool PermitirChamadoPublico, bool ExigirSolucaoParaConcluir, int? DiasParaFechamentoAutomatico);
    public record SalvarSlaRequest(bool AutomatizarPendentePorPrazoConclusao, int? HorasAposVencimentoParaPendente, int? HorasAntesPrazoParaAlerta, bool NotificarAdministradoresSla, bool ExibirDataFinalizacaoModal = true, bool ExibirPrazoRespostaModal = true, bool ExibirPrazoConclusaoModal = true);
    public record NomeRequest(string? Nome);
    public record EditarNomeRequest(int Id, string? Nome);
    public record TipoChamadoRequest(string? Nome, string? Descricao);
    public record EditarTipoChamadoRequest(int Id, string? Nome, string? Descricao);
    public record CategoriaRequest(int TipoId, string? Nome);
    public record SubcategoriaRequest(int CategoriaId, string? Nome);
    public record IdRequest(int Id);
    public record ExcluirGrupoRequest(string? NomeConfirmacao);
    public record ItemVm(int Id, string Nome, int Vinculos);
    public record CategoriaVm(int Id, int TipoId, string TipoNome, string Nome, int Subcategorias, int Vinculos);
    public record SubcategoriaVm(int Id, int CategoriaId, string TipoNome, string CategoriaNome, string Nome, int Vinculos);
    public record TipoChamadoVm(int Id, string Nome, string? Descricao, bool Ativo, int Posicao);
    public record AdminVm(string Nome, string Usuario);
    public record AuditoriaVm(DateTime Data, string Usuario, string Acao, string Entidade, string? Campo, string? Anterior, string? Novo);
}
