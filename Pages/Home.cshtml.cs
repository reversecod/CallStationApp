using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using CallStationApp.Data;
using CallStationApp.Models;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace CallStationApp.Pages;

[Authorize]
public class HomeModel : PageModel
{
    private readonly AppDbContext _context;

    public HomeModel(AppDbContext context)
    {
        _context = context;
    }

    public List<Chamado> Chamados { get; set; } = new();

    public async Task OnGetAsync()
    {
        // Tenta recuperar o ID do usuário autenticado via Claims
        var idUsuarioLogadoClaim = User.FindFirst("Id")?.Value;

        if (string.IsNullOrEmpty(idUsuarioLogadoClaim) || !int.TryParse(idUsuarioLogadoClaim, out var idUsuarioLogado))
        {
            // Protege contra execução sem usuário autenticado
            Chamados = new(); // Retorna lista vazia se não autenticado
            return;
        }

        //Busca apenas os chamados abertos do usuário autenticado
        Chamados = await _context.Chamados
            .Where(c => c.Status == StatusChamado.Aberto && c.CriadorChamadoId == idUsuarioLogado)
            .OrderBy(c => c.DataCriacao)
            .ToListAsync();
    }

    public async Task<IActionResult> OnPostNovoChamadoAsync()
    {
        var idUsuarioLogadoClaim = User.FindFirst("Id")?.Value;

        if (string.IsNullOrEmpty(idUsuarioLogadoClaim) || !int.TryParse(idUsuarioLogadoClaim, out var idUsuarioLogado))
        {
            return new JsonResult(new { success = false, message = "Usuário não autenticado." }) 
            {
                StatusCode = StatusCodes.Status401Unauthorized
            };
        }

        var usuario = await _context.Usuarios.FirstOrDefaultAsync(u => u.Id == idUsuarioLogado);
        if (usuario is null)
        {
            return NotFound("Usuário logado não encontrado.");
        }

        var chamado = new Chamado
        {
            Titulo = "Novo chamado",
            Status = StatusChamado.Aberto,
            DataCriacao = DateTime.Now,
            CriadorChamadoId = usuario.Id,
            Usuario = usuario
        };

        _context.Chamados.Add(chamado);
        await _context.SaveChangesAsync();

        return new JsonResult(new 
        { 
            success = true, 
            chamadoId = chamado.Id, 
            criadoEm = chamado.DataCriacao,
            status = chamado.Status.ToString()  // Adicione isso!
        });
    }
}