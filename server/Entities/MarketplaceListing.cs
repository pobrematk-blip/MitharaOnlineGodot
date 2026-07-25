namespace Mithara.Server.Entities;

public enum MarketplaceListingType : short
{
    Item = 1,
    Gold = 2,
}

public enum MarketplaceCurrencyType : short
{
    Gold = 1,
    PixReal = 2,
}

public static class MarketplaceStatus
{
    public const string Active = "active";
    public const string PendingPayment = "pending_payment";
    public const string Sold = "sold";
    public const string Cancelled = "cancelled";
}

public sealed class MarketplaceListing
{
    public long Id { get; set; }
    public int SellerAccountId { get; set; }
    public int SellerCharacterId { get; set; }
    public string SellerName { get; set; } = "";
    public MarketplaceListingType ListingType { get; set; }
    public MarketplaceCurrencyType CurrencyType { get; set; }
    public int? ItemDbId { get; set; }
    public int ItemId { get; set; }
    public string ItemName { get; set; } = "";
    public int ItemType { get; set; }
    public int Quantity { get; set; }
    public int PricePerUnitGold { get; set; }
    public int PriceTotalCents { get; set; }
    public int GoldAmount { get; set; }
    public int RefineLevel { get; set; }
    public string RollData { get; set; } = "";
    public string Status { get; set; } = MarketplaceStatus.Active;
    public DateTime CreatedAt { get; set; }
}
