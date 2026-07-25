using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Mithara.Web.Models.ViewModels;
using Mithara.Web.Services;

namespace Mithara.Web.Controllers;

public sealed class MarketplaceController : Controller
{
    private readonly MarketplaceService _marketplace;
    private readonly MercadoPagoCheckoutService _mercadoPago;

    public MarketplaceController(MarketplaceService marketplace, MercadoPagoCheckoutService mercadoPago)
    {
        _marketplace = marketplace;
        _mercadoPago = mercadoPago;
    }

    public async Task<IActionResult> Index(string search = "", int type = -1, string payment = "")
    {
        var listings = await _marketplace.GetListingsAsync(search, type);
        int accountId = User.Identity?.IsAuthenticated == true
            ? int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!)
            : 0;
        var characters = accountId > 0
            ? await _marketplace.GetAccountCharactersAsync(accountId)
            : new List<MarketplaceCharacterOption>();

        if (payment == "success")
            TempData["MarketplaceInfo"] = "Pagamento recebido pelo Mercado Pago. A entrega so ocorre apos confirmacao segura.";
        else if (payment == "pending")
            TempData["MarketplaceInfo"] = "Pagamento pendente. Aguarde a confirmacao do Mercado Pago.";
        else if (payment == "failure")
            TempData["MarketplaceError"] = "Pagamento nao aprovado pelo Mercado Pago.";

        return View(new MarketplaceIndexViewModel
        {
            Search = search,
            ItemType = type,
            Listings = listings,
            Characters = characters,
            IsAuthenticated = accountId > 0,
        });
    }

    [HttpGet("Marketplace/ItemIcon/{itemId:int}")]
    [ResponseCache(Duration = 86400, Location = ResponseCacheLocation.Client)]
    public IActionResult ItemIcon(int itemId)
    {
        string? path = _marketplace.FindItemIconPath(itemId);
        if (string.IsNullOrWhiteSpace(path) || !System.IO.File.Exists(path))
            return NotFound();

        return PhysicalFile(path, GetImageContentType(path));
    }

    [Authorize]
    [HttpPost]
    public async Task<IActionResult> BuyPix(long id, int characterId, CancellationToken cancellationToken)
    {
        int accountId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        if (characterId <= 0 || !await _marketplace.CharacterBelongsToAccountAsync(accountId, characterId))
        {
            TempData["MarketplaceError"] = "Selecione um personagem valido da sua conta para receber a compra.";
            return RedirectToAction(nameof(Index));
        }

        var listing = await _marketplace.GetPixListingAsync(id);
        if (listing == null)
        {
            TempData["MarketplaceError"] = "Anuncio PIX nao encontrado ou expirado.";
            return RedirectToAction(nameof(Index));
        }

        listing.BuyerAccountId = accountId;
        listing.BuyerCharacterId = characterId;
        string baseUrl = $"{Request.Scheme}://{Request.Host}";
        var preference = await _mercadoPago.CreatePreferenceAsync(listing, baseUrl, cancellationToken);
        if (!preference.Success)
        {
            TempData["MarketplaceError"] = preference.Message;
            return RedirectToAction(nameof(Index));
        }

        await _marketplace.MarkMercadoPagoPreferenceAsync(id, preference.PreferenceId, "preference_created", accountId, characterId);
        return Redirect(preference.InitPoint);
    }

    [HttpPost("Marketplace/Webhook/MercadoPago")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> WebhookMercadoPago(CancellationToken cancellationToken)
    {
        string paymentId = await ReadMercadoPagoPaymentIdAsync(Request);
        if (string.IsNullOrWhiteSpace(paymentId))
            return Ok(new { ignored = true, reason = "missing_payment_id" });

        var payment = await _mercadoPago.GetPaymentAsync(paymentId, cancellationToken);
        if (!payment.Success)
            return Ok(new { ignored = true, reason = payment.Message });

        string result = await _marketplace.CompletePixPaymentAsync(
            payment.PaymentId,
            payment.PreferenceId,
            payment.ListingId,
            payment.BuyerAccountId,
            payment.BuyerCharacterId,
            payment.Status,
            payment.RawJson);

        return Ok(new { processed = true, result });
    }

    private static async Task<string> ReadMercadoPagoPaymentIdAsync(HttpRequest request)
    {
        string id = request.Query["data.id"].FirstOrDefault()
            ?? request.Query["id"].FirstOrDefault()
            ?? "";
        if (!string.IsNullOrWhiteSpace(id))
            return id;

        if (!request.HasJsonContentType())
            return "";

        using var doc = await JsonDocument.ParseAsync(request.Body);
        var root = doc.RootElement;
        if (root.TryGetProperty("data", out var data) && data.TryGetProperty("id", out var dataId))
            return dataId.ToString();
        if (root.TryGetProperty("id", out var rootId))
            return rootId.ToString();
        return "";
    }

    private static string GetImageContentType(string path)
    {
        return Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".webp" => "image/webp",
            ".jpg" or ".jpeg" => "image/jpeg",
            _ => "image/png",
        };
    }
}
