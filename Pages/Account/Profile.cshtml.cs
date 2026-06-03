using System.Security.Claims;
using CallStationApp.Data;
using CallStationApp.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace CallStationApp.Pages.Account;

[Authorize]
public class ProfileModel : PageModel
{
    private static readonly HashSet<string> ExtensoesPermitidas = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg",
        ".jpeg",
        ".png",
        ".webp"
    };

    private readonly AppDbContext _context;
    private readonly IWebHostEnvironment _environment;
    private readonly PasswordHasher<Usuario> _passwordHasher = new();

    public ProfileModel(AppDbContext context, IWebHostEnvironment environment)
    {
        _context = context;
        _environment = environment;
    }

    [BindProperty]
    public string NomeCompleto { get; set; } = string.Empty;

    [BindProperty]
    public string NomeUsuario { get; set; } = string.Empty;

    [BindProperty]
    public string? Email { get; set; }

    [BindProperty]
    public IFormFile? FotoPerfil { get; set; }

    [BindProperty]
    public string? SenhaAtual { get; set; }

    [BindProperty]
    public string? NovaSenha { get; set; }

    [BindProperty]
    public string? ConfirmarNovaSenha { get; set; }

    public string? FotoUsuario { get; set; }
    public DateTime DataCriacao { get; set; }
    public string? SuccessMessage { get; set; }
    public string? ErrorMessage { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var usuario = await ObterUsuarioLogadoAsync();
        if (usuario == null)
            return RedirectToPage("/Auth/Login");

        CarregarFormulario(usuario);
        SuccessMessage = TempData["ProfileSuccess"] as string;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var usuario = await ObterUsuarioLogadoAsync();
        if (usuario == null)
            return RedirectToPage("/Auth/Login");

        NomeCompleto = NomeCompleto.Trim();
        NomeUsuario = NomeUsuario.Trim();
        Email = string.IsNullOrWhiteSpace(Email) ? null : Email.Trim();

        if (!await ValidarAsync(usuario))
        {
            FotoUsuario = usuario.FotoUsuario;
            DataCriacao = usuario.DataCriacao;
            return Page();
        }

        usuario.NomeCompleto = NomeCompleto;
        usuario.NomeUsuario = NomeUsuario;
        usuario.Email = Email;

        if (FotoPerfil is { Length: > 0 })
            usuario.FotoUsuario = await SalvarFotoAsync(FotoPerfil, usuario.Id);

        if (!string.IsNullOrWhiteSpace(NovaSenha))
            usuario.Senha = _passwordHasher.HashPassword(usuario, NovaSenha);

        var strategy = _context.Database.CreateExecutionStrategy();
        try
        {
            await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            });
        }
        catch (DbUpdateException ex) when (EhErroDuplicidade(ex))
        {
            ErrorMessage = "Nome de usuário ou e-mail ja esta em uso.";
            FotoUsuario = usuario.FotoUsuario;
            DataCriacao = usuario.DataCriacao;
            return Page();
        }

        await AtualizarCookieAsync(usuario);
        TempData["ProfileSuccess"] = "Perfil atualizado com sucesso.";
        return RedirectToPage();
    }

    private async Task<bool> ValidarAsync(Usuario usuario)
    {
        if (string.IsNullOrWhiteSpace(NomeCompleto) || NomeCompleto.Length > 100)
        {
            ErrorMessage = "Informe um nome completo com ate 100 caracteres.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(NomeUsuario) || NomeUsuario.Length > 20)
        {
            ErrorMessage = "Informe um nome de usuário com ate 20 caracteres.";
            return false;
        }

        if (!string.IsNullOrWhiteSpace(Email) && Email.Length > 100)
        {
            ErrorMessage = "Informe um e-mail com ate 100 caracteres.";
            return false;
        }

        if (await _context.Usuarios.AnyAsync(u => u.Id != usuario.Id && u.NomeUsuario == NomeUsuario))
        {
            ErrorMessage = "Nome de usuário ja esta em uso.";
            return false;
        }

        if (!string.IsNullOrWhiteSpace(Email) &&
            await _context.Usuarios.AnyAsync(u => u.Id != usuario.Id && u.Email == Email))
        {
            ErrorMessage = "E-mail ja esta em uso.";
            return false;
        }

        if (FotoPerfil is { Length: > 0 } && !FotoValida(FotoPerfil))
        {
            ErrorMessage = "Envie uma foto JPG, PNG ou WEBP com no maximo 2 MB.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(NovaSenha) &&
            string.IsNullOrWhiteSpace(SenhaAtual) &&
            string.IsNullOrWhiteSpace(ConfirmarNovaSenha))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(SenhaAtual))
        {
            ErrorMessage = "Informe a senha atual para alterar a senha.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(NovaSenha) || NovaSenha.Length < 8)
        {
            ErrorMessage = "A nova senha deve ter pelo menos 8 caracteres.";
            return false;
        }

        if (NovaSenha != ConfirmarNovaSenha)
        {
            ErrorMessage = "A confirmacao da nova senha nao confere.";
            return false;
        }

        var resultado = _passwordHasher.VerifyHashedPassword(usuario, usuario.Senha, SenhaAtual);
        if (resultado == PasswordVerificationResult.Success)
            return true;

        ErrorMessage = "Senha atual incorreta.";
        return false;
    }

    private async Task<Usuario?> ObterUsuarioLogadoAsync()
    {
        var claim = User.FindFirst("Id")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!int.TryParse(claim, out var usuarioId))
            return null;

        return await _context.Usuarios.FirstOrDefaultAsync(u => u.Id == usuarioId);
    }

    private void CarregarFormulario(Usuario usuario)
    {
        NomeCompleto = usuario.NomeCompleto;
        NomeUsuario = usuario.NomeUsuario;
        Email = usuario.Email;
        FotoUsuario = usuario.FotoUsuario;
        DataCriacao = usuario.DataCriacao;
    }

    private async Task<string> SalvarFotoAsync(IFormFile arquivo, int usuarioId)
    {
        var extensao = Path.GetExtension(arquivo.FileName).ToLowerInvariant();
        var pasta = Path.Combine(_environment.WebRootPath, "uploads", "perfis");
        Directory.CreateDirectory(pasta);

        var nomeArquivo = $"usuario-{usuarioId}-{Guid.NewGuid():N}{extensao}";
        var caminho = Path.Combine(pasta, nomeArquivo);

        await using var stream = new FileStream(caminho, FileMode.Create);
        await arquivo.CopyToAsync(stream);

        return $"/uploads/perfis/{nomeArquivo}";
    }

    private static bool FotoValida(IFormFile arquivo)
    {
        var extensao = Path.GetExtension(arquivo.FileName);
        return arquivo.Length <= 2 * 1024 * 1024 && ExtensoesPermitidas.Contains(extensao);
    }

    private async Task AtualizarCookieAsync(Usuario usuario)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, usuario.Id.ToString()),
            new("Id", usuario.Id.ToString()),
            new(ClaimTypes.Name, usuario.NomeUsuario),
            new(ClaimTypes.Email, usuario.Email ?? string.Empty),
            new("FullName", usuario.NomeCompleto)
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
    }

    private static bool EhErroDuplicidade(DbUpdateException ex)
    {
        var mensagem = ex.InnerException?.Message ?? ex.Message;
        return mensagem.Contains("Duplicate", StringComparison.OrdinalIgnoreCase) ||
               mensagem.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase);
    }
}
