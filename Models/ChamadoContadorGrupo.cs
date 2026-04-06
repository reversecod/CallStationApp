namespace CallStationApp.Models;

public class ChamadoContadorGrupo
{
    public int GrupoId { get; set; }
    public int UltimoNumero { get; set; }

    public Grupo Grupo { get; set; } = null!;
}