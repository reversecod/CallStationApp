using CallStationApp.Authorization;
using CallStationApp.Data;
using CallStationApp.Models;
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

    public GroupModel(AppDbContext context, GrupoAuthorizationService auth, IMemoryCache memoryCache, ILogger<GroupModel> logger)
    {
        _context = context;
        _auth = auth;
        _memoryCache = memoryCache;
        _logger = logger;
    }

    [BindProperty(SupportsGet = true)]
    public int GrupoId { get; set; }

    public Grupo GrupoAtual { get; set; } = null!;
    public GrupoConfiguracao Configuracao { get; set; } = null!;
    public string? NomeUsuarioLogado { get; set; }
    public string? FotoUsuarioLogado { get; set; }
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

        var bloqueio = await BloquearSeNaoAdminAsync(usuarioId.Value);
        if (bloqueio != null)
            return bloqueio;

        var grupo = await _context.Grupos.FirstOrDefaultAsync(g => g.Id == GrupoId);
        if (grupo == null)
            return NotFound(new { success = false, message = "Grupo nao encontrado." });

        var nome = Limpar(request.Nome);
        var descricao = Limpar(request.Descricao);
        var slug = Slug(request.Slug);

        if (string.IsNullOrWhiteSpace(nome) || nome.Length > 35)
            return BadRequest(new { success = false, message = "Nome obrigatorio com ate 35 caracteres." });

        if (descricao.Length > 200)
            return BadRequest(new { success = false, message = "Descricao deve ter ate 200 caracteres." });

        if (!Enum.TryParse<EtiquetaCor>(request.EtiquetaCor, true, out var cor))
            return BadRequest(new { success = false, message = "Cor invalida." });

        var config = await ObterOuCriarConfigAsync(usuarioId.Value);

        if (!string.IsNullOrWhiteSpace(slug) &&
            await _context.GruposConfiguracoes.AnyAsync(c => c.GrupoId != GrupoId && c.Slug == slug))
        {
            return BadRequest(new { success = false, message = "Identificador ja esta em uso." });
        }

        try
        {
            AuditarSeMudou(usuarioId.Value, "Grupo", grupo.Id, "Nome", grupo.Nome, nome);
            AuditarSeMudou(usuarioId.Value, "Grupo", grupo.Id, "Descricao", grupo.DescricaoGrupo, descricao);
            AuditarSeMudou(usuarioId.Value, "Grupo", grupo.Id, "Cor", grupo.EtiquetaCor.ToString(), cor.ToString());
            AuditarSeMudou(usuarioId.Value, "GrupoConfiguracao", GrupoId, "Slug", config.Slug, slug);
            AuditarSeMudou(usuarioId.Value, "GrupoConfiguracao", GrupoId, "Ativo", config.Ativo.ToString(), request.Ativo.ToString());

            grupo.Nome = nome;
            grupo.DescricaoGrupo = string.IsNullOrWhiteSpace(descricao) ? null : descricao;
            grupo.EtiquetaCor = cor;
            config.Slug = string.IsNullOrWhiteSpace(slug) ? null : slug;
            config.Ativo = request.Ativo;
            config.AtualizadoPorUsuarioId = usuarioId.Value;
            config.DataAtualizacao = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return new JsonResult(new { success = true, message = "Grupo salvo com sucesso." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao salvar grupo {GrupoId}.", GrupoId);
            return StatusCode(500, new { success = false, message = "Nao foi possivel salvar o grupo." });
        }
    }

    public async Task<IActionResult> OnPostSalvarRegrasAsync([FromBody] SalvarRegrasRequest request)
    {
        var usuarioId = GetUsuarioLogadoId();
        if (usuarioId == null)
            return Unauthorized();

        var bloqueio = await BloquearSeNaoAdminAsync(usuarioId.Value);
        if (bloqueio != null)
            return bloqueio;

        if (request.DiasParaFechamentoAutomatico is < 0 or > 365)
            return BadRequest(new { success = false, message = "Dias deve ficar entre 0 e 365." });

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

        return new JsonResult(new { success = true, message = "Regras salvas." });
    }

    public async Task<IActionResult> OnPostSalvarSlaAsync([FromBody] SalvarSlaRequest request)
    {
        var usuarioId = GetUsuarioLogadoId();
        if (usuarioId == null)
            return Unauthorized();

        var bloqueio = await BloquearSeNaoAdminAsync(usuarioId.Value);
        if (bloqueio != null)
            return bloqueio;

        if (request.HorasAposVencimentoParaPendente is < 0 or > 720 ||
            request.HorasAntesPrazoParaAlerta is < 0 or > 720)
        {
            return BadRequest(new { success = false, message = "Horas deve ficar entre 0 e 720." });
        }

        var config = await ObterOuCriarConfigAsync(usuarioId.Value);
        config.AutomatizarPendentePorPrazoConclusao = request.AutomatizarPendentePorPrazoConclusao;
        config.HorasAposVencimentoParaPendente = request.HorasAposVencimentoParaPendente;
        config.HorasAntesPrazoParaAlerta = request.HorasAntesPrazoParaAlerta;
        config.NotificarAdministradoresSla = request.NotificarAdministradoresSla;
        config.AtualizadoPorUsuarioId = usuarioId.Value;
        config.DataAtualizacao = DateTime.UtcNow;
        Auditar(usuarioId.Value, "Atualizar", "GrupoConfiguracao", GrupoId, "SLA", null, "SLA atualizado");
        await _context.SaveChangesAsync();

        return new JsonResult(new { success = true, message = "SLA salvo." });
    }

    public async Task<IActionResult> OnPostCriarSetorAsync([FromBody] NomeRequest request)
    {
        var usuarioId = await ValidarPostAdminAsync();
        if (usuarioId.Result != null)
            return usuarioId.Result;

        var nome = NomeValido(request.Nome, 50);
        if (await _context.Setores.AnyAsync(s => s.GrupoId == GrupoId && s.NomeSetor == nome))
            return BadRequest(new { success = false, message = "Setor duplicado." });

        var usuario = await _context.Usuarios.FirstAsync(u => u.Id == usuarioId.Id);
        var setor = new Setor { GrupoId = GrupoId, UsuarioId = usuarioId.Id, Usuario = usuario, NomeSetor = nome };
        _context.Setores.Add(setor);
        await _context.SaveChangesAsync();
        Auditar(usuarioId.Id, "Criar", "Setor", setor.Id, null, null, nome);
        await _context.SaveChangesAsync();
        InvalidarCacheCatalogoGrupo();
        return new JsonResult(new { success = true, message = "Setor criado." });
    }

    public async Task<IActionResult> OnPostCriarTipoOcorrenciaAsync([FromBody] NomeRequest request)
    {
        var usuarioId = await ValidarPostAdminAsync();
        if (usuarioId.Result != null)
            return usuarioId.Result;

        var nome = NomeValido(request.Nome, 50);
        if (await _context.OcorrenciasTipo.AnyAsync(t => t.GrupoId == GrupoId && t.TipoOcorrencia == nome))
            return BadRequest(new { success = false, message = "Tipo duplicado." });

        var tipo = new OcorrenciaTipo { GrupoId = GrupoId, UsuarioId = usuarioId.Id, TipoOcorrencia = nome };
        _context.OcorrenciasTipo.Add(tipo);
        await _context.SaveChangesAsync();
        Auditar(usuarioId.Id, "Criar", "OcorrenciaTipo", tipo.Id, null, null, nome);
        await _context.SaveChangesAsync();
        InvalidarCacheCatalogoGrupo();
        return new JsonResult(new { success = true, message = "Tipo de ocorrencia criado." });
    }

    public async Task<IActionResult> OnPostCriarCategoriaAsync([FromBody] CategoriaRequest request)
    {
        var usuarioId = await ValidarPostAdminAsync();
        if (usuarioId.Result != null)
            return usuarioId.Result;

        var tipo = await _context.OcorrenciasTipo.FirstOrDefaultAsync(t => t.Id == request.TipoId && t.GrupoId == GrupoId);
        if (tipo == null)
            return BadRequest(new { success = false, message = "Tipo invalido." });

        var nome = NomeValido(request.Nome, 50);
        if (await _context.OcorrenciasCategoria.AnyAsync(c => c.TipoId == request.TipoId && c.CategoriaOcorrencia == nome))
            return BadRequest(new { success = false, message = "Categoria duplicada." });

        var categoria = new OcorrenciaCategoria { TipoId = tipo.Id, OcorrenciaTipo = tipo, CategoriaOcorrencia = nome };
        _context.OcorrenciasCategoria.Add(categoria);
        await _context.SaveChangesAsync();
        Auditar(usuarioId.Id, "Criar", "OcorrenciaCategoria", categoria.Id, null, null, nome);
        await _context.SaveChangesAsync();
        InvalidarCacheCatalogoTipo(tipo.Id);
        return new JsonResult(new { success = true, message = "Categoria criada." });
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

        var nome = NomeValido(request.Nome, 100);
        if (await _context.OcorrenciasSubcategoria.AnyAsync(s => s.CategoriaId == categoria.Id && s.SubcategoriaOcorrencia == nome))
            return BadRequest(new { success = false, message = "Subcategoria duplicada." });

        var subcategoria = new OcorrenciaSubcategoria { CategoriaId = categoria.Id, OcorrenciaCategoria = categoria, SubcategoriaOcorrencia = nome };
        _context.OcorrenciasSubcategoria.Add(subcategoria);
        await _context.SaveChangesAsync();
        Auditar(usuarioId.Id, "Criar", "OcorrenciaSubcategoria", subcategoria.Id, null, null, nome);
        await _context.SaveChangesAsync();
        InvalidarCacheCatalogoCategoria(categoria.Id);
        return new JsonResult(new { success = true, message = "Subcategoria criada." });
    }

    public async Task<IActionResult> OnPostCriarTipoChamadoAsync([FromBody] TipoChamadoRequest request)
    {
        var usuarioId = await ValidarPostAdminAsync();
        if (usuarioId.Result != null)
            return usuarioId.Result;

        var nome = NomeValido(request.Nome, 50);
        var descricao = Limpar(request.Descricao);
        if (descricao.Length > 160)
            return BadRequest(new { success = false, message = "Descricao deve ter ate 160 caracteres." });

        if (await _context.GruposTiposChamados.AnyAsync(t => t.GrupoId == GrupoId && t.Nome == nome))
            return BadRequest(new { success = false, message = "Tipo de chamado duplicado." });

        var posicao = (await _context.GruposTiposChamados.Where(t => t.GrupoId == GrupoId).MaxAsync(t => (int?)t.Posicao) ?? 0) + 1;
        var tipo = new GrupoTipoChamado { GrupoId = GrupoId, Nome = nome, Descricao = descricao, Posicao = posicao, CriadoPorUsuarioId = usuarioId.Id };
        _context.GruposTiposChamados.Add(tipo);
        await _context.SaveChangesAsync();
        Auditar(usuarioId.Id, "Criar", "GrupoTipoChamado", tipo.Id, null, null, nome);
        await _context.SaveChangesAsync();
        return new JsonResult(new { success = true, message = "Tipo de chamado criado." });
    }

    public async Task<IActionResult> OnPostArquivarTipoChamadoAsync([FromBody] IdRequest request)
    {
        var usuarioId = await ValidarPostAdminAsync();
        if (usuarioId.Result != null)
            return usuarioId.Result;

        var tipo = await _context.GruposTiposChamados.FirstOrDefaultAsync(t => t.Id == request.Id && t.GrupoId == GrupoId);
        if (tipo == null)
            return NotFound(new { success = false, message = "Tipo de chamado nao encontrado." });

        tipo.Ativo = false;
        tipo.ArquivadoPorUsuarioId = usuarioId.Id;
        tipo.DataArquivamento = DateTime.UtcNow;
        Auditar(usuarioId.Id, "Arquivar", "GrupoTipoChamado", tipo.Id, null, tipo.Nome, null);
        await _context.SaveChangesAsync();

        return new JsonResult(new { success = true, message = "Tipo de chamado arquivado." });
    }

    public async Task<IActionResult> OnPostExcluirSetorAsync([FromBody] IdRequest request)
    {
        var usuarioId = await ValidarPostAdminAsync();
        if (usuarioId.Result != null)
            return usuarioId.Result;

        var emUso = await _context.Chamados.AnyAsync(c => c.GrupoId == GrupoId && c.SetorId == request.Id);
        if (emUso)
            return BadRequest(new { success = false, message = "Setor vinculado a chamados. Exclusao fisica bloqueada." });

        var setor = await _context.Setores.FirstOrDefaultAsync(s => s.Id == request.Id && s.GrupoId == GrupoId);
        if (setor == null)
            return NotFound(new { success = false, message = "Setor nao encontrado." });

        _context.Setores.Remove(setor);
        Auditar(usuarioId.Id, "Excluir", "Setor", setor.Id, null, setor.NomeSetor, null);
        await _context.SaveChangesAsync();
        InvalidarCacheCatalogoGrupo();

        return new JsonResult(new { success = true, message = "Setor excluido." });
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

    private async Task CarregarAsync(int usuarioId)
    {
        GrupoAtual = await _context.Grupos.AsNoTracking().FirstAsync(g => g.Id == GrupoId);
        Configuracao = await ObterOuCriarConfigAsync(usuarioId);

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

        Setores = await _context.Setores.AsNoTracking()
            .Where(s => s.GrupoId == GrupoId)
            .OrderBy(s => s.NomeSetor)
            .Select(s => new ItemVm(s.Id, s.NomeSetor, _context.Chamados.Count(c => c.GrupoId == GrupoId && c.SetorId == s.Id)))
            .ToListAsync();

        TiposOcorrencia = await _context.OcorrenciasTipo.AsNoTracking()
            .Where(t => t.GrupoId == GrupoId)
            .OrderBy(t => t.TipoOcorrencia)
            .Select(t => new ItemVm(t.Id, t.TipoOcorrencia, _context.Chamados.Count(c => c.GrupoId == GrupoId && c.OcorrenciaTipoId == t.Id)))
            .ToListAsync();

        Categorias = await (from c in _context.OcorrenciasCategoria.AsNoTracking()
                            join t in _context.OcorrenciasTipo.AsNoTracking() on c.TipoId equals t.Id
                            where t.GrupoId == GrupoId
                            orderby t.TipoOcorrencia, c.CategoriaOcorrencia
                            select new CategoriaVm(c.Id, c.TipoId, t.TipoOcorrencia, c.CategoriaOcorrencia,
                                _context.OcorrenciasSubcategoria.Count(s => s.CategoriaId == c.Id),
                                _context.Chamados.Count(ch => ch.GrupoId == GrupoId && ch.OcorrenciaCategoriaId == c.Id))).ToListAsync();

        Subcategorias = await (from s in _context.OcorrenciasSubcategoria.AsNoTracking()
                               join c in _context.OcorrenciasCategoria.AsNoTracking() on s.CategoriaId equals c.Id
                               join t in _context.OcorrenciasTipo.AsNoTracking() on c.TipoId equals t.Id
                               where t.GrupoId == GrupoId
                               orderby t.TipoOcorrencia, c.CategoriaOcorrencia, s.SubcategoriaOcorrencia
                               select new SubcategoriaVm(s.Id, c.Id, t.TipoOcorrencia, c.CategoriaOcorrencia, s.SubcategoriaOcorrencia,
                                   _context.Chamados.Count(ch => ch.GrupoId == GrupoId && ch.OcorrenciaSubcategoriaId == s.Id))).ToListAsync();

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

    private async Task<GrupoConfiguracao> ObterOuCriarConfigAsync(int usuarioId)
    {
        var config = await _context.GruposConfiguracoes.FirstOrDefaultAsync(c => c.GrupoId == GrupoId);
        if (config != null)
            return config;

        config = new GrupoConfiguracao
        {
            GrupoId = GrupoId,
            Slug = Slug(await _context.Grupos.Where(g => g.Id == GrupoId).Select(g => g.Nome).FirstAsync()),
            AtualizadoPorUsuarioId = usuarioId,
            DataAtualizacao = DateTime.UtcNow
        };
        _context.GruposConfiguracoes.Add(config);
        await _context.SaveChangesAsync();
        return config;
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

    private static string NomeValido(string? valor, int limite)
    {
        var nome = Limpar(valor);
        if (string.IsNullOrWhiteSpace(nome) || nome.Length > limite)
            throw new ArgumentException($"Nome obrigatorio com ate {limite} caracteres.");
        return nome;
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

        return sb.ToString().Trim('-');
    }

    private int? GetUsuarioLogadoId()
    {
        var claim = User.FindFirst("Id")?.Value;
        return int.TryParse(claim, out var id) ? id : null;
    }

    public record SalvarGeralRequest(string? Nome, string? Descricao, string? Slug, string? EtiquetaCor, bool Ativo);
    public record SalvarRegrasRequest(bool ObrigarSetor, bool ObrigarTipoOcorrencia, bool ObrigarCategoria, bool ObrigarSubcategoria, bool PermitirChamadoPublico, bool ExigirSolucaoParaConcluir, int? DiasParaFechamentoAutomatico);
    public record SalvarSlaRequest(bool AutomatizarPendentePorPrazoConclusao, int? HorasAposVencimentoParaPendente, int? HorasAntesPrazoParaAlerta, bool NotificarAdministradoresSla);
    public record NomeRequest(string? Nome);
    public record TipoChamadoRequest(string? Nome, string? Descricao);
    public record CategoriaRequest(int TipoId, string? Nome);
    public record SubcategoriaRequest(int CategoriaId, string? Nome);
    public record IdRequest(int Id);
    public record ItemVm(int Id, string Nome, int Vinculos);
    public record CategoriaVm(int Id, int TipoId, string TipoNome, string Nome, int Subcategorias, int Vinculos);
    public record SubcategoriaVm(int Id, int CategoriaId, string TipoNome, string CategoriaNome, string Nome, int Vinculos);
    public record TipoChamadoVm(int Id, string Nome, string? Descricao, bool Ativo, int Posicao);
    public record AdminVm(string Nome, string Usuario);
    public record AuditoriaVm(DateTime Data, string Usuario, string Acao, string Entidade, string? Campo, string? Anterior, string? Novo);
}
