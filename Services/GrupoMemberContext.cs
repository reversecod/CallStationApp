using CallStationApp.Models;

namespace CallStationApp.Authorization;

public class GrupoMemberContext
{
    public int UsuarioId { get; set; }
    public int GrupoId { get; set; }
    public PermissaoUsuario Permissao { get; set; }

    public bool EhAdministrador => Permissao == PermissaoUsuario.Administracao;
    public bool EhTecnico => Permissao == PermissaoUsuario.Tecnico;
    public bool EhColaborador => Permissao == PermissaoUsuario.Colaborador;
    public bool EhSemPermissao => Permissao == PermissaoUsuario.Nenhuma;
}