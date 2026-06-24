namespace Mithara.Server.Entities;

public class LojinhaItem
{
    public int Slot { get; set; }
    public int ItemId { get; set; }
    public int Quantity { get; set; }
    public int PricePerUnit { get; set; }
    public string RollData { get; set; } = "";
    public int DbId { get; set; }
}
