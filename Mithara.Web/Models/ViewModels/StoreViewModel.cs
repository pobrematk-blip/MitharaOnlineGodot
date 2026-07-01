using Mithara.Web.Models;

namespace Mithara.Web.Models.ViewModels;

public class StoreIndexViewModel
{
    public List<ShopProduct> CashProducts { get; set; } = new();
    public List<ShopProduct> FounderPacks { get; set; } = new();
}

public class SelectCharacterViewModel
{
    public ShopProduct Product { get; set; } = new();
    public int ProductId { get; set; }
    public List<CharacterInfo> Characters { get; set; } = new();
}
