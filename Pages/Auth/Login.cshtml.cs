using Microsoft.AspNetCore.Mvc.RazorPages;
using CallStationApp.Data;
using CallStationApp.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace CallStationApp.Pages.Auth
{
    public class LoginModel : PageModel
    {   
        public readonly AppDbContext Context;
        private readonly PasswordHasher<Usuario> _passwordHasher;

        public LoginModel(AppDbContext context)
        {
            Context = context;
            _passwordHasher = new PasswordHasher<Usuario>();
        }
        
        [BindProperty]
        public string? UsernameOrEmail { get; set; } 
        
        [BindProperty]
        public string? Password { get; set; }
        
        [BindProperty]
        public bool RememberMe { get; set; }
        
        public string? ErrorMessage { get; set; }
        
        public void OnGet(){}

        public async Task<IActionResult> OnPostAsync()
        {
            if (string.IsNullOrWhiteSpace(UsernameOrEmail) || string.IsNullOrWhiteSpace(Password))
            {
                ErrorMessage = "Preencha todos os campos.";
                return Page();
            }
            
            var user = await Context.Usuarios
                .FirstOrDefaultAsync(u =>
                    u.NomeUsuario == UsernameOrEmail || u.Email == UsernameOrEmail);
            
            if (user == null)
            {
                ErrorMessage = "Usuário ou senha inválidos.";
                return Page();
            }
            
            var result = _passwordHasher.VerifyHashedPassword(user, user.Senha, Password);

            if (result != PasswordVerificationResult.Success)
            {
                ErrorMessage = "Usuário ou senha inválidos.";
                return Page();
            }
            
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.NomeUsuario),
                new Claim(ClaimTypes.Email, user.Email ?? ""),
                new Claim("FullName", user.NomeCompleto),
            };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                principal,
                new AuthenticationProperties
                {
                    IsPersistent = RememberMe,
                    ExpiresUtc = RememberMe
                        ? DateTimeOffset.UtcNow.AddDays(30)  // persistente por 30 dias
                        : DateTimeOffset.UtcNow.AddHours(8) // sessão normal
                }
            );

            return RedirectToPage("/Home");
        }
    }
}