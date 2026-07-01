namespace Mithara.Web.Models;

public class ShopProduct
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Category { get; set; } = ""; // "cash" ou "founder"
    public decimal Price { get; set; }
    public int CashAmount { get; set; }
    public string Tier { get; set; } = ""; // "prata", "ouro", "diamante" ou vazio
    public string IconClass { get; set; } = ""; // classe CSS do ícone
    public string BadgeText { get; set; } = "";
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
