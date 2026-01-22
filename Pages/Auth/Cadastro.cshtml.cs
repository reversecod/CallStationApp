using Microsoft.AspNetCore.Mvc.RazorPages;
using CallStationApp.Data;
using CallStationApp.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CallStationApp.Pages.Auth;

public class CadastroModel : PageModel
{
    public readonly AppDbContext Context;
    private readonly PasswordHasher<Usuario> _passwordHasher;
    public CadastroModel(AppDbContext context)
    {
        Context = context;
        _passwordHasher = new PasswordHasher<Usuario>();
    }
    
    [BindProperty]
    public string? NomeCompleto { get; set; }
    
    [BindProperty]
    public string? NomeUsuario { get; set; }
    
    [BindProperty]
    public string? Email { get; set; }
    
    [BindProperty]
    public string? Senha { get; set; }
    
    [BindProperty]
    public string? ConfirmarSenha { get; set; }
    
    public string? ErrorMessage { get; set; }
    public string? SuccessMessage { get; set; }
    
    public void OnGet(){}

    public async Task<IActionResult> OnPostAsync()
    {
        if (string.IsNullOrWhiteSpace(NomeCompleto) ||
            string.IsNullOrWhiteSpace(NomeUsuario) ||
            string.IsNullOrWhiteSpace(Senha) ||
            string.IsNullOrWhiteSpace(ConfirmarSenha))
        {
            ErrorMessage = "Todos os campos obrigatórios devem ser preenchidos.";
            return Page();
        }
        
        if (Senha.Length < 8)
        {
            ErrorMessage = "A senha deve ter pelo menos 8 caracteres.";
            return Page();
        }
        
        if (Senha != ConfirmarSenha)
        {
            ErrorMessage = "As senhas não coincidem.";
            return Page();
        }
        
        
        // Verifica se já existe nome de usuário ou email
        bool nomeUsuarioExiste = await Context.Usuarios.AnyAsync(u => u.NomeUsuario == NomeUsuario);
        if (nomeUsuarioExiste)
        {
            ErrorMessage = "Nome de usuário já está em uso.";
            return Page();
        }

        if (!string.IsNullOrWhiteSpace(Email))
        {
            bool emailExiste = await Context.Usuarios.AnyAsync(u => u.Email == Email);
            if (emailExiste)
            {
                ErrorMessage = "Email já está em uso.";
                return Page();
            }
        }
        
        // Cria novo usuário com senha criptografada
        var novoUsuario = new Usuario
        {
            NomeCompleto = NomeCompleto,
            NomeUsuario = NomeUsuario,
            Email = string.IsNullOrWhiteSpace(Email) ? null : Email,
            DataCriacao = DateTime.Now
        };
        
        novoUsuario.Senha = _passwordHasher.HashPassword(novoUsuario, Senha);
        
        Context.Usuarios.Add(novoUsuario);
        await Context.SaveChangesAsync();
        Console.WriteLine("Usuário salvo no banco");
        
        //Sucesso
        SuccessMessage = "Cadastro realizado com sucesso!";
        
        
        // Limpa os campos preenchidos
        ModelState.Clear();
        NomeCompleto = NomeUsuario = Email = Senha = ConfirmarSenha = null;

        return Page();
    }
}