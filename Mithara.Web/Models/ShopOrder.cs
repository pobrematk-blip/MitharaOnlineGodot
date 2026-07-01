namespace Mithara.Web.Models;

public class ShopOrder
{
    public int Id { get; set; }
    public int AccountId { get; set; }
    public string Username { get; set; } = "";
    public int ProductId { get; set; }
    public string ProductName { get; set; } = "";
    public decimal Amount { get; set; }
    public int? TargetCharacterId { get; set; }
    public string? TargetCharacterName { get; set; }
    public string Status { get; set; } = "pending"; // pending, completed, cancelled
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
}
