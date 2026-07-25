namespace Mithara.Web.Models.ViewModels;

public sealed class MarketplaceIndexViewModel
{
    public string Search { get; set; } = "";
    public int ItemType { get; set; } = -1;
    public List<MarketplaceListingViewModel> Listings { get; set; } = new();
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
    public int RefineLevel { get; set; }
    public string Status { get; set; } = "";
    public string IconUrl { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}
