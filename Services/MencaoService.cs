using System.Text.RegularExpressions;
using CallStationApp.Authorization;
using CallStationApp.Data;
using CallStationApp.Models;
using Microsoft.EntityFrameworkCore;

namespace CallStationApp.Services;

public class MencaoService
{
    private static readonly Regex TokenMencaoRegex = new(@"\@\[([^\]\r\n]{1,100})\]\(usuario:(\d{1,10})\)", RegexOptions.Compiled);
    private static readonly Regex TokenTodosRegex = new(@"\@\[todos\]\(todos\)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private readonly AppDbContext _context;

    public MencaoService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<MembroMencaoDto>> BuscarMembrosAsync(int grupoId, int usuarioAutorId, string? termo, IEnumerable<int>? usuariosPermitidosIds = null)
    {
        var termoNormalizado = (termo ?? string.Empty).Trim();
        var permitidos = usuariosPermitidosIds?.Distinct().ToList();

        var query =
            from usuarioGrupo in _context.UsuariosGrupos.AsNoTracking()
            join usuario in _context.Usuarios.AsNoTracking() on usuarioGrupo.UsuarioId equals usuario.Id
            join info in _context.InfoUsuariosGrupos.AsNoTracking()
                on new { usuarioGrupo.UsuarioId, usuarioGrupo.GrupoId }
                equals new { info.UsuarioId, info.GrupoId } into infoJoin
            from infoUsuario in infoJoin.DefaultIfEmpty()
            where usuarioGrupo.GrupoId == grupoId &&
                  usuarioGrupo.Ativo &&
                  usuarioGrupo.UsuarioId != usuarioAutorId
            select new
            {
                UsuarioId = usuario.Id,
                usuario.NomeUsuario,
                usuario.NomeCompleto,
                Apelido = infoUsuario == null ? null : infoUsuario.Apelido
            };

        if (permitidos is { Count: > 0 })
            query = query.Where(u => permitidos.Contains(u.UsuarioId));

        if (!string.IsNullOrWhiteSpace(termoNormalizado))
        {
            var padrao = $"%{EscaparLike(termoNormalizado)}%";
            query = query.Where(u =>
                EF.Functions.Like(u.NomeUsuario, padrao, "\\") ||
                EF.Functions.Like(u.NomeCompleto, padrao, "\\") ||
                EF.Functions.Like(u.Apelido ?? string.Empty, padrao, "\\"));
        }

        var membros = await query
            .OrderBy(u => u.Apelido ?? u.NomeUsuario)
            .ThenBy(u => u.UsuarioId)
            .Take(8)
            .Select(u => new MembroMencaoDto
            {
                UsuarioId = u.UsuarioId,
                NomeExibicao = string.IsNullOrWhiteSpace(u.Apelido) ? u.NomeUsuario : u.Apelido!,
                NomeUsuario = u.NomeUsuario
            })
            .ToListAsync();

        if ("todos".StartsWith(termoNormalizado, StringComparison.OrdinalIgnoreCase))
        {
            membros.Insert(0, new MembroMencaoDto
            {
                UsuarioId = 0,
                NomeExibicao = "todos",
                NomeUsuario = "todos que podem ser mencionados",
                EhTodos = true
            });
        }

        return membros;
    }

    public async Task SincronizarMencoesAsync(
        int grupoId,
        int usuarioAutorId,
        string entidadeTipo,
        int entidadeId,
        string campoOrigem,
        string? texto,
        TipoNotificacao tipoNotificacao,
        string tituloNotificacao,
        string contextoMensagem,
        string linkDestino,
        IEnumerable<int>? usuariosPermitidosIds = null)
    {
        var mencoesTexto = ExtrairMencoes(texto).ToList();
        var mencoesTodos = ExtrairMencoesTodos(texto).ToList();
        var idsMencionados = mencoesTexto.Select(m => m.UsuarioMencionadoId).Distinct().ToList();
        var idsPermitidos = usuariosPermitidosIds?.Distinct().ToHashSet();

        var queryUsuariosValidos = _context.UsuariosGrupos
            .AsNoTracking()
            .Where(ug =>
                ug.GrupoId == grupoId &&
                ug.Ativo &&
                ug.UsuarioId != usuarioAutorId);

        if (idsPermitidos != null)
            queryUsuariosValidos = queryUsuariosValidos.Where(ug => idsPermitidos.Contains(ug.UsuarioId));

        var usuariosValidos = idsMencionados.Count == 0
            ? new List<int>()
            : await queryUsuariosValidos
                .Where(ug => idsMencionados.Contains(ug.UsuarioId))
                .Select(ug => ug.UsuarioId)
                .ToListAsync();

        var usuariosValidosSet = usuariosValidos.ToHashSet();
        if (usuariosValidos.Count != idsMencionados.Count)
        {
            mencoesTexto = mencoesTexto
                .Where(m => usuariosValidosSet.Contains(m.UsuarioMencionadoId))
                .ToList();
        }

        if (mencoesTodos.Count > 0)
        {
            var usuariosTodos = await queryUsuariosValidos
                .Select(ug => ug.UsuarioId)
                .Distinct()
                .ToListAsync();

            foreach (var mencaoTodos in mencoesTodos)
            {
                mencoesTexto.AddRange(usuariosTodos.Select(usuarioId => new MencaoExtraida
                {
                    UsuarioMencionadoId = usuarioId,
                    TextoExibido = "todos",
                    PosicaoInicio = mencaoTodos.PosicaoInicio,
                    PosicaoFim = mencaoTodos.PosicaoFim
                }));
            }
        }

        var existentes = await _context.MencoesTextos
            .Where(m =>
                m.GrupoId == grupoId &&
                m.EntidadeTipo == entidadeTipo &&
                m.EntidadeId == entidadeId &&
                m.CampoOrigem == campoOrigem)
            .ToListAsync();

        var novasChaves = mencoesTexto
            .Select(m => CriarChave(m.UsuarioMencionadoId, m.PosicaoInicio, m.PosicaoFim, m.TextoExibido))
            .ToHashSet(StringComparer.Ordinal);

        var existentesChaves = existentes
            .Select(m => CriarChave(m.UsuarioMencionadoId, m.PosicaoInicio, m.PosicaoFim, m.TextoExibido))
            .ToHashSet(StringComparer.Ordinal);

        var remover = existentes
            .Where(m => !novasChaves.Contains(CriarChave(m.UsuarioMencionadoId, m.PosicaoInicio, m.PosicaoFim, m.TextoExibido)))
            .ToList();

        if (remover.Count > 0)
        {
            var removerIds = remover.Select(m => m.Id).ToList();
            var notificacoesVinculadas = await _context.Notificacoes
                .Where(n => n.MencaoId.HasValue && removerIds.Contains(n.MencaoId.Value))
                .ToListAsync();

            foreach (var notificacao in notificacoesVinculadas)
                notificacao.MencaoId = null;

            _context.MencoesTextos.RemoveRange(remover);
        }

        var novas = mencoesTexto
            .Where(m => !existentesChaves.Contains(CriarChave(m.UsuarioMencionadoId, m.PosicaoInicio, m.PosicaoFim, m.TextoExibido)))
            .Select(m => new MencaoTexto
            {
                GrupoId = grupoId,
                UsuarioMencionadoId = m.UsuarioMencionadoId,
                UsuarioAutorId = usuarioAutorId,
                EntidadeTipo = entidadeTipo,
                EntidadeId = entidadeId,
                CampoOrigem = campoOrigem,
                TextoExibido = m.TextoExibido,
                PosicaoInicio = m.PosicaoInicio,
                PosicaoFim = m.PosicaoFim,
                CriadoEm = DateTime.UtcNow
            })
            .ToList();

        if (novas.Count == 0)
            return;

        _context.MencoesTextos.AddRange(novas);

        var autor = await _context.Usuarios
            .AsNoTracking()
            .Where(u => u.Id == usuarioAutorId)
            .Select(u => u.NomeUsuario)
            .FirstOrDefaultAsync();

        var grupo = await _context.Grupos
            .AsNoTracking()
            .Where(g => g.Id == grupoId)
            .Select(g => g.Nome)
            .FirstOrDefaultAsync();

        var agora = DateTime.UtcNow;
        var notificacoes = novas
            .Select(m => new Notificacao
            {
                UsuarioId = m.UsuarioMencionadoId,
                UsuarioOrigemId = usuarioAutorId,
                GrupoId = grupoId,
                Tipo = tipoNotificacao,
                Titulo = tituloNotificacao,
                Mensagem = $"{autor ?? "Um usuario"} mencionou voce em {contextoMensagem} no grupo {grupo ?? "atual"}.",
                Lida = false,
                DataCriacao = agora,
                ReferenciaId = entidadeId,
                ReferenciaTipo = $"Mencao{entidadeTipo}{campoOrigem}",
                LinkDestino = linkDestino,
                Mencao = m
            })
            .ToList();

        if (notificacoes.Count > 0)
            _context.Notificacoes.AddRange(notificacoes);
    }

    public static string TextoRenderizavel(string? texto)
    {
        if (string.IsNullOrEmpty(texto))
            return string.Empty;

        return TokenTodosRegex.Replace(
            TokenMencaoRegex.Replace(texto, match => "@" + match.Groups[1].Value),
            "@todos");
    }

    private static IEnumerable<MencaoExtraida> ExtrairMencoes(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
            yield break;

        foreach (Match match in TokenMencaoRegex.Matches(texto))
        {
            if (!int.TryParse(match.Groups[2].Value, out var usuarioId) || usuarioId <= 0)
                continue;

            yield return new MencaoExtraida
            {
                UsuarioMencionadoId = usuarioId,
                TextoExibido = match.Groups[1].Value.Trim(),
                PosicaoInicio = match.Index,
                PosicaoFim = match.Index + match.Length
            };
        }
    }

    private static IEnumerable<MencaoTodosExtraida> ExtrairMencoesTodos(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
            yield break;

        foreach (Match match in TokenTodosRegex.Matches(texto))
        {
            yield return new MencaoTodosExtraida
            {
                PosicaoInicio = match.Index,
                PosicaoFim = match.Index + match.Length
            };
        }
    }

    private static string CriarChave(int usuarioId, int inicio, int fim, string texto) =>
        $"{usuarioId}:{inicio}:{fim}:{texto}";

    private static string EscaparLike(string valor) =>
        valor.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");

    private sealed class MencaoExtraida
    {
        public int UsuarioMencionadoId { get; set; }
        public string TextoExibido { get; set; } = string.Empty;
        public int PosicaoInicio { get; set; }
        public int PosicaoFim { get; set; }
    }

    private sealed class MencaoTodosExtraida
    {
        public int PosicaoInicio { get; set; }
        public int PosicaoFim { get; set; }
    }
}

public class MembroMencaoDto
{
    public int UsuarioId { get; set; }
    public string NomeExibicao { get; set; } = string.Empty;
    public string NomeUsuario { get; set; } = string.Empty;
    public bool EhTodos { get; set; }
}
