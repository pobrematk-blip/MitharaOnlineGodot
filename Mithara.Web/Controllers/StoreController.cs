using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Mithara.Web.Models.ViewModels;
using Mithara.Web.Services;

namespace Mithara.Web.Controllers;

public class StoreController : Controller
{
    private readonly StoreService _storeService;
    private readonly GameDbService _gameDb;

    public StoreController(StoreService storeService, GameDbService gameDb)
    {
        _storeService = storeService;
        _gameDb = gameDb;
    }

    public async Task<IActionResult> Index()
    {
        var cashProducts = await _storeService.GetCashProductsAsync();
        var founderPacks = await _storeService.GetFounderPacksAsync();

        return View(new StoreIndexViewModel
        {
            CashProducts = cashProducts,
            FounderPacks = founderPacks,
        });
    }

    [HttpGet]
    public async Task<IActionResult> SelectCharacter(int productId)
    {
        var product = await _storeService.GetProductAsync(productId);
        if (product == null)
            return NotFound();

        if (User.Identity?.IsAuthenticated != true)
            return RedirectToAction("Login", "Account");

        var accountId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var account = _gameDb.GetAccountInfo(accountId);

        return View(new SelectCharacterViewModel
        {
            Product = product,
            ProductId = productId,
            Characters = account?.Characters ?? new(),
        });
    }

    [HttpPost]
    public async Task<IActionResult> ConfirmPurchase(int productId, int? characterId)
    {
        if (User.Identity?.IsAuthenticated != true)
            return RedirectToAction("Login", "Account");

        var product = await _storeService.GetProductAsync(productId);
        if (product == null)
            return NotFound();

        var accountId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var username = User.Identity.Name ?? "Desconhecido";

        string? targetName = null;
        if (characterId.HasValue)
        {
            var account = _gameDb.GetAccountInfo(accountId);
            var character = account?.Characters.FirstOrDefault(c => c.Id == characterId.Value);
            targetName = character?.Name;
        }

        await _storeService.CreateOrderAsync(accountId, username, productId, characterId, targetName);

        TempData["Success"] = $"Compra de \"{product.Name}\" registrada! Em breve você receberá seus itens.";
        return RedirectToAction("Index");
    }
}
