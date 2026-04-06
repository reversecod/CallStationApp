using CallStationApp.Data;
using CallStationApp.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace CallStationApp.Pages.Menu;

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

    public async Task<IActionResult> OnGetBuscarUsuariosAsync(string termo)
    {
        var idUsuario = GetUsuarioLogadoId();
        if (idUsuario == null)
            return new JsonResult(new { success = false, message = "Usuário não autenticado." });

        if (GrupoId <= 0)
            return new JsonResult(new { success = false, message = "Grupo inválido." });

        termo = (termo ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(termo) || termo.Length < 2)
            return new JsonResult(new { success = true, usuarios = new List<object>() });

        var usuarios = await _context.Usuarios
            .AsNoTracking()
            .Where(u =>
                (u.NomeCompleto.Contains(termo) ||
                 u.NomeUsuario.Contains(termo) ||
                 (u.Email != null && u.Email.Contains(termo)))
                &&
                u.Id != idUsuario.Value
                &&
                !_context.UsuariosGrupos.Any(ug => ug.UsuarioId == u.Id && ug.GrupoId == GrupoId)
                &&
                !_context.ConvitesGrupo.Any(c =>
                    c.GrupoId == GrupoId &&
                    c.DestinatarioUsuarioId == u.Id &&
                    c.Status == StatusConviteGrupo.Pendente)
            )
            .OrderBy(u => u.NomeCompleto)
            .Take(10)
            .Select(u => new
            {
                id = u.Id,
                nomeCompleto = u.NomeCompleto,
                nomeUsuario = u.NomeUsuario,
                email = u.Email,
                fotoUsuario = u.FotoUsuario
            })
            .ToListAsync();

        return new JsonResult(new { success = true, usuarios });
    }

    public async Task<IActionResult> OnPostEnviarConviteAsync([FromBody] EnviarConviteRequest request)
    {
        var idUsuario = GetUsuarioLogadoId();
        if (idUsuario == null)
            return new JsonResult(new { success = false, message = "Usuário não autenticado." });

        if (request == null || request.DestinatarioUsuarioId <= 0)
            return new JsonResult(new { success = false, message = "Usuário inválido." });

        if (GrupoId <= 0)
            return new JsonResult(new { success = false, message = "Grupo inválido." });

        var solicitanteNoGrupo = await _context.UsuariosGrupos
            .FirstOrDefaultAsync(ug => ug.UsuarioId == idUsuario.Value && ug.GrupoId == GrupoId);

        if (solicitanteNoGrupo == null)
            return new JsonResult(new { success = false, message = "Você não pertence a este grupo." });

        if (solicitanteNoGrupo.Permissao != PermissaoUsuario.Administracao)
            return new JsonResult(new { success = false, message = "Somente administradores podem enviar convites." });

        if (idUsuario.Value == request.DestinatarioUsuarioId)
            return new JsonResult(new { success = false, message = "Você não pode enviar convite para si mesmo." });

        var usuarioJaNoGrupo = await _context.UsuariosGrupos
            .AnyAsync(ug => ug.UsuarioId == request.DestinatarioUsuarioId && ug.GrupoId == GrupoId);

        if (usuarioJaNoGrupo)
            return new JsonResult(new { success = false, message = "Este usuário já faz parte do grupo." });

        var convitePendente = await _context.ConvitesGrupo
            .AnyAsync(c =>
                c.GrupoId == GrupoId &&
                c.DestinatarioUsuarioId == request.DestinatarioUsuarioId &&
                c.Status == StatusConviteGrupo.Pendente);

        if (convitePendente)
            return new JsonResult(new { success = false, message = "Já existe uma solicitação pendente para este usuário." });

        var grupo = await _context.Grupos
            .AsNoTracking()
            .FirstOrDefaultAsync(g => g.Id == GrupoId);

        var remetente = await _context.Usuarios
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == idUsuario.Value);

        var destinatario = await _context.Usuarios
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == request.DestinatarioUsuarioId);

        if (grupo == null || remetente == null || destinatario == null)
            return new JsonResult(new { success = false, message = "Dados do convite inválidos." });

        var convite = new ConviteGrupo
        {
            GrupoId = GrupoId,
            RemetenteUsuarioId = idUsuario.Value,
            DestinatarioUsuarioId = request.DestinatarioUsuarioId,
            Status = StatusConviteGrupo.Pendente,
            Mensagem = request.Mensagem,
            DataCriacao = DateTime.Now
        };

        _context.ConvitesGrupo.Add(convite);
        await _context.SaveChangesAsync();

        var notificacao = new Notificacao
        {
            UsuarioId = destinatario.Id,
            Tipo = TipoNotificacao.ConviteGrupo,
            Titulo = "Novo convite de grupo",
            Mensagem = $"{remetente.NomeCompleto} convidou você para participar do grupo {grupo.Nome}.",
            Lida = false,
            DataCriacao = DateTime.Now,
            ReferenciaId = convite.Id,
            ReferenciaTipo = "ConviteGrupo",
            LinkDestino = $"/Notificacoes"
        };

        _context.Notificacoes.Add(notificacao);
        await _context.SaveChangesAsync();

        return new JsonResult(new
        {
            success = true,
            message = "Solicitação enviada com sucesso."
        });
    }

    public class EnviarConviteRequest
    {
        public int DestinatarioUsuarioId { get; set; }
        public string? Mensagem { get; set; }
    }
}