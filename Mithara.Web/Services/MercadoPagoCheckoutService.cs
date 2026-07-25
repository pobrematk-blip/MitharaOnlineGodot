using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Mithara.Web.Models.ViewModels;

namespace Mithara.Web.Services;

public sealed class MercadoPagoOptions
{
    public string AccessToken { get; set; } = "";
    public string PublicKey { get; set; } = "";
    public string WebhookSecret { get; set; } = "";
}

public sealed record MercadoPagoPreferenceResult(bool Success, string PreferenceId, string InitPoint, string Message)
{
    public static MercadoPagoPreferenceResult Fail(string message) => new(false, "", "", message);
}

public sealed record MercadoPagoPaymentResult(
    bool Success,
    string PaymentId,
    string PreferenceId,
    string Status,
    long ListingId,
    int BuyerAccountId,
    int BuyerCharacterId,
    string RawJson,
    string Message)
{
    public static MercadoPagoPaymentResult Fail(string message) => new(false, "", "", "", 0, 0, 0, "", message);
}

public sealed class MercadoPagoCheckoutService
{
    private const string PreferencesUrl = "https://api.mercadopago.com/checkout/preferences";
    private const string PaymentsUrl = "https://api.mercadopago.com/v1/payments/";
    private readonly HttpClient _http;
    private readonly MercadoPagoOptions _options;

    public MercadoPagoCheckoutService(HttpClient http, IOptions<MercadoPagoOptions> options)
    {
        _http = http;
        _options = options.Value;
    }

    public async Task<MercadoPagoPreferenceResult> CreatePreferenceAsync(
        MarketplaceListingViewModel listing,
        string publicBaseUrl,
        CancellationToken cancellationToken)
    {
        string accessToken = Environment.GetEnvironmentVariable("MERCADO_PAGO_ACCESS_TOKEN") ?? _options.AccessToken;
        if (string.IsNullOrWhiteSpace(accessToken))
            return MercadoPagoPreferenceResult.Fail("Mercado Pago ainda nao esta configurado no servidor.");

        decimal unitPrice = Math.Round(listing.PriceTotalCents / 100m, 2);
        string title = listing.IsGoldListing
            ? $"{listing.GoldAmount:N0} Gold - Mithara Online"
            : $"{listing.ItemName} - Mithara Online";

        var payload = new
        {
            items = new[]
            {
                new
                {
                    id = listing.Id.ToString(),
                    title,
                    quantity = 1,
                    currency_id = "BRL",
                    unit_price = unitPrice
                }
            },
            external_reference = $"marketplace:{listing.Id}",
            notification_url = $"{publicBaseUrl.TrimEnd('/')}/Marketplace/Webhook/MercadoPago",
            back_urls = new
            {
                success = $"{publicBaseUrl.TrimEnd('/')}/Marketplace?payment=success",
                failure = $"{publicBaseUrl.TrimEnd('/')}/Marketplace?payment=failure",
                pending = $"{publicBaseUrl.TrimEnd('/')}/Marketplace?payment=pending"
            },
            auto_return = "approved",
            statement_descriptor = "MITHARA ONLINE",
            metadata = new
            {
                marketplace_listing_id = listing.Id,
                buyer_account_id = listing.BuyerAccountId,
                buyer_character_id = listing.BuyerCharacterId,
                seller = listing.SellerName,
                fee_percent = 20
            }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, PreferencesUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        using var response = await _http.SendAsync(request, cancellationToken);
        string json = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            return MercadoPagoPreferenceResult.Fail($"Mercado Pago recusou a preferencia: {(int)response.StatusCode}");

        using var doc = JsonDocument.Parse(json);
        string preferenceId = doc.RootElement.TryGetProperty("id", out var idProp) ? idProp.GetString() ?? "" : "";
        string initPoint = doc.RootElement.TryGetProperty("init_point", out var initProp) ? initProp.GetString() ?? "" : "";
        if (string.IsNullOrWhiteSpace(preferenceId) || string.IsNullOrWhiteSpace(initPoint))
            return MercadoPagoPreferenceResult.Fail("Mercado Pago nao retornou o link de pagamento.");

        return new MercadoPagoPreferenceResult(true, preferenceId, initPoint, "Preferencia criada.");
    }

    public async Task<MercadoPagoPaymentResult> GetPaymentAsync(string paymentId, CancellationToken cancellationToken)
    {
        string accessToken = Environment.GetEnvironmentVariable("MERCADO_PAGO_ACCESS_TOKEN") ?? _options.AccessToken;
        if (string.IsNullOrWhiteSpace(accessToken))
            return MercadoPagoPaymentResult.Fail("Mercado Pago ainda nao esta configurado no servidor.");

        using var request = new HttpRequestMessage(HttpMethod.Get, PaymentsUrl + Uri.EscapeDataString(paymentId));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await _http.SendAsync(request, cancellationToken);
        string json = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            return MercadoPagoPaymentResult.Fail($"Falha ao consultar pagamento no Mercado Pago: {(int)response.StatusCode}");

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        string status = root.TryGetProperty("status", out var statusProp) ? statusProp.GetString() ?? "" : "";
        string preferenceId = root.TryGetProperty("preference_id", out var prefProp) ? prefProp.GetString() ?? "" : "";
        string external = root.TryGetProperty("external_reference", out var extProp) ? extProp.GetString() ?? "" : "";

        long listingId = ParseListingId(external);
        int buyerAccountId = 0;
        int buyerCharacterId = 0;
        if (root.TryGetProperty("metadata", out var metadata))
        {
            buyerAccountId = ReadInt(metadata, "buyer_account_id");
            buyerCharacterId = ReadInt(metadata, "buyer_character_id");
            if (listingId <= 0)
                listingId = ReadLong(metadata, "marketplace_listing_id");
        }

        if (listingId <= 0 || buyerAccountId <= 0 || buyerCharacterId <= 0)
            return MercadoPagoPaymentResult.Fail("Pagamento nao possui referencia segura do leilao.");

        string actualPaymentId = root.TryGetProperty("id", out var idProp) ? idProp.ToString() : paymentId;
        return new MercadoPagoPaymentResult(true, actualPaymentId, preferenceId, status, listingId, buyerAccountId, buyerCharacterId, json, "Pagamento consultado.");
    }

    private static long ParseListingId(string externalReference)
    {
        const string prefix = "marketplace:";
        if (externalReference.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            && long.TryParse(externalReference[prefix.Length..], out long id))
            return id;
        return 0;
    }

    private static int ReadInt(JsonElement element, string name)
    {
        return element.TryGetProperty(name, out var prop) && prop.TryGetInt32(out int value) ? value : 0;
    }

    private static long ReadLong(JsonElement element, string name)
    {
        return element.TryGetProperty(name, out var prop) && prop.TryGetInt64(out long value) ? value : 0;
    }
}
