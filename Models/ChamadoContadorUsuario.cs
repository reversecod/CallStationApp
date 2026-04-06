namespace CallStationApp.Models;

public class ChamadoContadorUsuario
{
    public int UsuarioId { get; set; }
    public int UltimoNumero { get; set; }

    public Usuario Usuario { get; set; } = null!;
}