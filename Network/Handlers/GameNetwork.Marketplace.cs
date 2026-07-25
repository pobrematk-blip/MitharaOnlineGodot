#nullable enable
using Godot;
using LiteNetLib.Utils;
using Mithara.Network;

public partial class GameNetwork
{
    public void SendMarketplaceListRequest(string search = "", int itemType = -1, bool ownOnly = false)
    {
        _client?.SendPacket(PacketId.C2S_MarketplaceListRequest, w =>
        {
            w.Put(search ?? "");
            w.Put(itemType);
            w.Put(ownOnly);
        });
    }

    public void SendMarketplaceCreateItemListing(int invSlot, int quantity, int currencyType, int pricePerUnitGold, int priceTotalCents, int durationHours)
    {
        _client?.SendPacket(PacketId.C2S_MarketplaceCreateItemListing, w =>
        {
            w.Put(invSlot);
            w.Put(quantity);
            w.Put(currencyType);
            w.Put(pricePerUnitGold);
            w.Put(priceTotalCents);
            w.Put(durationHours);
        });
    }

    public void SendMarketplaceCreateGoldListing(int goldAmount, int priceTotalCents, int durationHours)
    {
        _client?.SendPacket(PacketId.C2S_MarketplaceCreateGoldListing, w =>
        {
            w.Put(goldAmount);
            w.Put(priceTotalCents);
            w.Put(durationHours);
        });
    }

    public void SendMarketplaceCancelListing(long listingId)
    {
        _client?.SendPacket(PacketId.C2S_MarketplaceCancelListing, w => w.Put(listingId));
    }

    public void SendMarketplaceBuyListing(long listingId)
    {
        _client?.SendPacket(PacketId.C2S_MarketplaceBuyListing, w => w.Put(listingId));
    }

    public void SendMarketplaceClaimGold(long listingId)
    {
        _client?.SendPacket(PacketId.C2S_MarketplaceClaimGold, w => w.Put(listingId));
    }

    private void HandleOpenMarketplace(NetDataReader r)
    {
        string title = r.GetString();
        string rules = r.GetString();
        EmitSignal(SignalName.OnOpenMarketplace, title, rules);
    }

    private void HandleMarketplaceListResult(NetDataReader r)
    {
        int count = r.GetInt();
        var listings = new Godot.Collections.Array<Godot.Collections.Dictionary>();
        for (int i = 0; i < count; i++)
        {
            var dict = new Godot.Collections.Dictionary
            {
                ["listing_id"] = r.GetLong(),
                ["seller_name"] = r.GetString(),
                ["listing_type"] = r.GetInt(),
                ["currency_type"] = r.GetInt(),
                ["item_id"] = r.GetInt(),
                ["item_name"] = r.GetString(),
                ["item_type"] = r.GetInt(),
                ["quantity"] = r.GetInt(),
                ["price_per_unit_gold"] = r.GetInt(),
                ["price_total_cents"] = r.GetInt(),
                ["gold_amount"] = r.GetInt(),
                ["refine_level"] = r.GetInt(),
                ["roll_data"] = r.GetString(),
                ["status"] = r.GetString(),
                ["expires_at"] = r.AvailableBytes >= 8 ? r.GetLong() : 0L,
                ["is_mine"] = r.AvailableBytes >= 1 && r.GetBool(),
                ["proceeds_gold"] = r.AvailableBytes >= 4 ? r.GetInt() : 0,
                ["proceeds_claimed"] = r.AvailableBytes >= 1 && r.GetBool(),
                ["payment_status"] = r.AvailableBytes > 0 ? r.GetString() : "",
            };
            listings.Add(dict);
        }

        EmitSignal(SignalName.OnMarketplaceListResult, listings);
    }

    private void HandleMarketplaceActionResult(NetDataReader r)
    {
        bool success = r.GetBool();
        string message = r.GetString();
        EmitSignal(SignalName.OnMarketplaceActionResult, success, message);
    }
}
