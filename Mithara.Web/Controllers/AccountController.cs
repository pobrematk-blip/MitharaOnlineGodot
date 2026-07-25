using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Mithara.Web.Models.ViewModels;
using Mithara.Web.Services;

namespace Mithara.Web.Controllers;

public class AccountController : Controller
{
    private readonly GameDbService _gameDb;
    private readonly EmailService _emailService;
    private readonly ILogger<AccountController> _logger;

    public AccountController(GameDbService gameDb, EmailService emailService, ILogger<AccountController> logger)
    {
        _gameDb = gameDb;
        _emailService = emailService;
        _logger = logger;
    }

    public IActionResult Login()
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction("Index", "Home");
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var (id, error) = _gameDb.LoginAccount(model.Username, model.Password);
        if (id == null)
        {
            ModelState.AddModelError("", error ?? "Erro ao fazer login.");
            return View(model);
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, id.Value.ToString()),
            new(ClaimTypes.Name, model.Username),
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal,
            new AuthenticationProperties { IsPersistent = model.RememberMe });

        _gameDb.UpdateLastSeen(id.Value);

        return RedirectToAction("Index", "Home");
    }

    public IActionResult Register()
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction("Index", "Home");
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var (id, error) = _gameDb.CreateAccount(model.Username, model.Email, model.Password);
        if (id == null)
        {
            ModelState.AddModelError("", error ?? "Erro ao criar conta.");
            return View(model);
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, id.Value.ToString()),
            new(ClaimTypes.Name, model.Username),
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal,
            new AuthenticationProperties { IsPersistent = false });

        _gameDb.UpdateLastSeen(id.Value);

        TempData["Success"] = "Conta criada com sucesso! Bem-vindo ao Mithara Online.";
        return RedirectToAction("Index", "Home");
    }

    public IActionResult ForgotPassword()
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction("Profile");
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var request = _gameDb.CreatePasswordResetRequest(model.UsernameOrEmail);
        if (request != null)
        {
            string resetLink = Url.Action("ResetPassword", "Account", new { token = request.Value.token }, Request.Scheme)
                ?? $"{Request.Scheme}://{Request.Host}/Account/ResetPassword?token={Uri.EscapeDataString(request.Value.token)}";
            try
            {
                await _emailService.SendPasswordResetAsync(request.Value.email, request.Value.username, resetLink);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Falha ao enviar e-mail de recuperacao para {Email}", request.Value.email);
            }
        }

        TempData["Success"] = "Se a conta existir, enviaremos um link de recuperacao para o e-mail cadastrado.";
        return RedirectToAction("ForgotPassword");
    }

    public IActionResult ResetPassword(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return RedirectToAction("Login");

        return View(new ResetPasswordViewModel { Token = token });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("auth")]
    public IActionResult ResetPassword(ResetPasswordViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        if (!_gameDb.ResetPasswordWithToken(model.Token, model.Password))
        {
            ModelState.AddModelError("", "Link invalido, expirado ou ja utilizado.");
            return View(model);
        }

        TempData["Success"] = "Senha alterada com sucesso. Voce ja pode entrar no site e no jogo.";
        return RedirectToAction("Login");
    }

    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Index", "Home");
    }

    public IActionResult Profile()
    {
        if (User.Identity?.IsAuthenticated != true)
            return RedirectToAction("Login");

        var accountId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var account = _gameDb.GetAccountInfo(accountId);
        if (account == null)
            return RedirectToAction("Logout");

        return View(account);
    }
}
