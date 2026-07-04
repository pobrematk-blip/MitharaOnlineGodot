using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Mithara.Web.Models.ViewModels;
using Mithara.Web.Services;

namespace Mithara.Web.Controllers;

public class AccountController : Controller
{
    private readonly GameDbService _gameDb;

    public AccountController(GameDbService gameDb)
    {
        _gameDb = gameDb;
    }

    public IActionResult Login()
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction("Index", "Home");
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
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
