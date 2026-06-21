using System.Collections.Generic;

namespace Mithara.Server.Entities;

public class TradeSession
{
    public int Id { get; set; }
    public ulong PlayerA { get; set; }
    public ulong PlayerB { get; set; }
    public List<TradeOfferItem> PlayerAOffers { get; set; } = new();
    public List<TradeOfferItem> PlayerBOffers { get; set; } = new();
    public bool PlayerAConfirmed { get; set; }
    public bool PlayerBConfirmed { get; set; }
}

public class TradeOfferItem
{
    public int TradeSlot { get; set; }
    public int InventorySlot { get; set; }
    public int ItemId { get; set; }
    public int Quantity { get; set; }
    public int DbId { get; set; }
}
