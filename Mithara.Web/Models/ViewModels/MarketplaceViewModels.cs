namespace Mithara.Web.Models.ViewModels;

public sealed class MarketplaceIndexViewModel
{
    public string Search { get; set; } = "";
    public int ItemType { get; set; } = -1;
    public List<MarketplaceListingViewModel> Listings { get; set; } = new();
    public List<MarketplaceCharacterOption> Characters { get; set; } = new();
    public bool IsAuthenticated { get; set; }
}

public sealed class MarketplaceCharacterOption
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public int Level { get; set; }
}

public sealed class MarketplaceListingViewModel
{
    public long Id { get; set; }
    public string SellerName { get; set; } = "";
    public int ListingType { get; set; }
    public int CurrencyType { get; set; }
    public int ItemId { get; set; }
    public string ItemName { get; set; } = "";
    public int ItemType { get; set; }
    public string ItemTypeName { get; set; } = "";
    public int Quantity { get; set; }
    public int PricePerUnitGold { get; set; }
    public int PriceTotalCents { get; set; }
    public int GoldAmount { get; set; }
    public int BuyerAccountId { get; set; }
    public int BuyerCharacterId { get; set; }
    public int RefineLevel { get; set; }
    public string Status { get; set; } = "";
    public string PaymentStatus { get; set; } = "";
    public string IconUrl { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }

    public bool IsPix => CurrencyType == 2;
    public bool IsGoldListing => ListingType == 2;
    public bool CanBuyWithPix => IsPix && (Status == "active" || Status == "pending_payment") && ExpiresAt > DateTime.UtcNow;
}
