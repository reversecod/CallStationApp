using CallStationApp.Data;
using Microsoft.EntityFrameworkCore;

namespace CallStationApp.Authorization;

public class GrupoAuthorizationService
{
    private readonly AppDbContext _context;

    public GrupoAuthorizationService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<GrupoMemberContext?> ObterContextoMembroAsync(int usuarioId, int grupoId)
    {
        var membro = await _context.UsuariosGrupos
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.UsuarioId == usuarioId && x.GrupoId == grupoId);

        if (membro == null)
            return null;

        return new GrupoMemberContext
        {
            UsuarioId = membro.UsuarioId,
            GrupoId = membro.GrupoId,
            Permissao = membro.Permissao
        };
    }
}