using CallStationApp.Authorization;
using CallStationApp.Data;
using CallStationApp.Models;
using Microsoft.EntityFrameworkCore;

namespace CallStationApp.Services;

public class NotificacaoService
{
    private readonly AppDbContext _context;

    public NotificacaoService(AppDbContext context)
    {
        _context = context;
    }

    public async Task CriarNotificacoesEntradaGrupoPorConviteAsync(int grupoId, int novoMembroUsuarioId)
    {
        var novoMembroNome = await _context.Usuarios
            .AsNoTracking()
            .Where(u => u.Id == novoMembroUsuarioId)
            .Select(u => u.NomeCompleto)
            .FirstOrDefaultAsync();

        var administradoresIds = await _context.UsuariosGrupos
            .AsNoTracking()
            .Where(ug =>
                ug.GrupoId == grupoId &&
                ug.Ativo &&
                ug.Permissao == PermissaoUsuario.Administracao &&
                ug.UsuarioId != novoMembroUsuarioId)
            .Select(ug => ug.UsuarioId)
            .ToListAsync();

        AdicionarNotificacoes(
            administradoresIds,
            grupoId,
            TipoNotificacao.ConviteGrupo,
            "Novo membro no grupo",
            $"{novoMembroNome ?? "Um usuario"} entrou no grupo por convite.",
            "EntradaGrupoConvite",
            novoMembroUsuarioId,
            $"/Menu/Members?grupoId={grupoId}");
    }

    public async Task CriarNotificacoesChamadoAbertoAsync(Chamado chamado, int autorUsuarioId)
    {
        var destinatariosIds = await _context.UsuariosGrupos
            .AsNoTracking()
            .Where(ug =>
                ug.GrupoId == chamado.GrupoId &&
                ug.Ativo &&
                ug.UsuarioId != autorUsuarioId &&
                (ug.Permissao == PermissaoUsuario.Administracao ||
                 ug.Permissao == PermissaoUsuario.Tecnico))
            .Select(ug => ug.UsuarioId)
            .ToListAsync();

        AdicionarNotificacoes(
            destinatariosIds,
            chamado.GrupoId,
            TipoNotificacao.Chamado,
            "Novo chamado aberto",
            $"Chamado #{chamado.NumeroChamadoGrupo} foi aberto no grupo.",
            "ChamadoAberto",
            chamado.Id,
            $"/Menu/Home?grupoId={chamado.GrupoId}");
    }

    public void CriarNotificacaoChamadoAlteradoParaDono(Chamado chamado, int autorUsuarioId, string acao)
    {
        if (chamado.CriadorChamadoId == autorUsuarioId)
            return;

        _context.Notificacoes.Add(new Notificacao
        {
            UsuarioId = chamado.CriadorChamadoId,
            GrupoId = chamado.GrupoId,
            Tipo = TipoNotificacao.Chamado,
            Titulo = "Chamado atualizado",
            Mensagem = $"Seu chamado #{chamado.NumeroChamadoGrupo} recebeu uma atualizacao: {acao}.",
            Lida = false,
            DataCriacao = DateTime.UtcNow,
            ReferenciaId = chamado.Id,
            ReferenciaTipo = "ChamadoAlterado",
            LinkDestino = $"/Menu/Home?grupoId={chamado.GrupoId}"
        });
    }

    public async Task CriarNotificacoesComentarioChamadoAsync(Chamado chamado, int autorUsuarioId, string referenciaTipo)
    {
        var membrosGrupo = await _context.UsuariosGrupos
            .AsNoTracking()
            .Where(ug =>
                ug.GrupoId == chamado.GrupoId &&
                ug.Ativo &&
                ug.UsuarioId != autorUsuarioId)
            .Select(ug => new { ug.UsuarioId, ug.Permissao })
            .ToListAsync();

        var destinatariosIds = membrosGrupo
            .Where(membro => GrupoPermissionService.PodeVerChamado(
                membro.Permissao,
                chamado.Publico,
                membro.UsuarioId,
                chamado.CriadorChamadoId))
            .Select(membro => membro.UsuarioId)
            .ToList();

        AdicionarNotificacoes(
            destinatariosIds,
            chamado.GrupoId,
            TipoNotificacao.Chamado,
            "Novo comentario em chamado",
            $"Chamado #{chamado.NumeroChamadoGrupo}: novo comentario registrado.",
            referenciaTipo,
            chamado.Id,
            $"/Menu/Home?grupoId={chamado.GrupoId}");
    }

    public async Task CriarNotificacoesChamadoPublicoAdicionadoAsync(Chamado chamado, int autorUsuarioId)
    {
        if (!chamado.Publico)
            return;

        var destinatariosIds = await _context.UsuariosGrupos
            .AsNoTracking()
            .Where(ug =>
                ug.GrupoId == chamado.GrupoId &&
                ug.Ativo &&
                ug.UsuarioId != autorUsuarioId &&
                (ug.Permissao == PermissaoUsuario.Colaborador ||
                 ug.Permissao == PermissaoUsuario.Nenhuma))
            .Select(ug => ug.UsuarioId)
            .ToListAsync();

        AdicionarNotificacoes(
            destinatariosIds,
            chamado.GrupoId,
            TipoNotificacao.Chamado,
            "Novo chamado publico",
            $"Um novo chamado publico foi adicionado ao grupo.",
            "ChamadoPublicoAdicionado",
            chamado.Id,
            $"/Menu/Home?grupoId={chamado.GrupoId}");
    }

    private void AdicionarNotificacoes(
        IEnumerable<int> destinatariosIds,
        int grupoId,
        TipoNotificacao tipo,
        string titulo,
        string mensagem,
        string referenciaTipo,
        int? referenciaId,
        string linkDestino)
    {
        var agora = DateTime.UtcNow;
        var notificacoes = destinatariosIds
            .Distinct()
            .Select(usuarioId => new Notificacao
            {
                UsuarioId = usuarioId,
                GrupoId = grupoId,
                Tipo = tipo,
                Titulo = titulo,
                Mensagem = mensagem,
                Lida = false,
                DataCriacao = agora,
                ReferenciaId = referenciaId,
                ReferenciaTipo = referenciaTipo,
                LinkDestino = linkDestino
            })
            .ToList();

        if (notificacoes.Count > 0)
            _context.Notificacoes.AddRange(notificacoes);
    }
}
