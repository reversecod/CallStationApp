namespace CallStationApp.Models;

public class ChamadoContadorUsuarioGrupo
{
    public int UsuarioId { get; set; }
    public int GrupoId { get; set; }
    public int UltimoNumero { get; set; }

    public Usuario Usuario { get; set; } = null!;
    public Grupo Grupo { get; set; } = null!;
}