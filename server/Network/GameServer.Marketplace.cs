using System.Text.Json;
using LiteNetLib;
using LiteNetLib.Utils;
using Mithara.Server.Entities;
using Mithara.Server.Packets;

namespace Mithara.Server.Network;

partial class GameServer
{
    private const int MarketplaceMaxGoldPricePerUnit = 1_000_000_000;
    private const int MarketplaceMaxPixCents = 10_000_000;

    private void HandleMarketplaceListRequest(NetPeer peer, NetDataReader reader)
    {
        string search = reader.GetString();
        int itemType = reader.GetInt();
        SendMarketplaceList(peer, search, itemType);
    }

    private void HandleMarketplaceCreateItemListing(NetPeer peer, NetDataReader reader)
    {
        if (!TryGetPlayer(peer, out var player, out _)) return;
        if (!_sessions.TryGetValue(peer, out var session) || session.SelectedCharacter == null) return;

        int invSlot = reader.GetInt();
        int quantity = Math.Clamp(reader.GetInt(), 1, 9999);
        var currencyType = (MarketplaceCurrencyType)Math.Clamp(reader.GetInt(), 1, 2);
        int pricePerUnitGold = Math.Clamp(reader.GetInt(), 0, MarketplaceMaxGoldPricePerUnit);
        int priceTotalCents = Math.Clamp(reader.GetInt(), 0, MarketplaceMaxPixCents);

        var item = player.Items.FirstOrDefault(i => i.Slot == invSlot);
        if (item == null)
        {
            SendMarketplaceActionResult(peer, false, "Item nao encontrado no inventario.");
            return;
        }

        if (player.Equipment.Values.Any(e => ReferenceEquals(e, item) || (item.DbId > 0 && e.DbId == item.DbId)))
        {
            SendMarketplaceActionResult(peer, false, "Desequipe o item antes de anunciar.");
            return;
        }

        bool ok = _db.TryCreateMarketplaceItemListing(
            session.AccountId,
            session.SelectedCharacter.Id,
            player.Name,
            invSlot,
            quantity,
            currencyType,
            pricePerUnitGold,
            priceTotalCents,
            out _,
            out string message);

        if (ok)
        {
            int removeQty = Math.Min(quantity, item.Quantity);
            item.Quantity -= removeQty;
            if (item.Quantity <= 0)
                player.Items.Remove(item);
            SendInventoryData(peer, player);
            SendMarketplaceList(peer);
        }

        SendMarketplaceActionResult(peer, ok, message);
    }

    private void HandleMarketplaceCreateGoldListing(NetPeer peer, NetDataReader reader)
    {
        if (!TryGetPlayer(peer, out var player, out _)) return;
        if (!_sessions.TryGetValue(peer, out var session) || session.SelectedCharacter == null) return;

        int goldAmount = Math.Clamp(reader.GetInt(), 1, 1_000_000_000);
        int priceTotalCents = Math.Clamp(reader.GetInt(), 100, MarketplaceMaxPixCents);

        bool ok = _db.TryCreateMarketplaceGoldListing(
            session.AccountId,
            session.SelectedCharacter.Id,
            player.Name,
            goldAmount,
            priceTotalCents,
            out _,
            out int remainingGold,
            out string message);

        if (ok)
        {
            player.Gold = remainingGold;
            session.SelectedCharacter.Gold = remainingGold;
            SendGoldUpdate(peer, player.Gold);
            SendMarketplaceList(peer);
        }

        SendMarketplaceActionResult(peer, ok, message);
    }

    private void HandleMarketplaceCancelListing(NetPeer peer, NetDataReader reader)
    {
        if (!TryGetPlayer(peer, out var player, out _)) return;
        if (!_sessions.TryGetValue(peer, out var session) || session.SelectedCharacter == null) return;

        long listingId = reader.GetLong();
        int returnSlot = FindFreeInventorySlot(player);
        if (returnSlot < 0)
        {
            SendMarketplaceActionResult(peer, false, "Inventario cheio. Libere espaco antes de cancelar.");
            return;
        }

        bool ok = _db.TryCancelMarketplaceListing(session.AccountId, session.SelectedCharacter.Id, listingId, returnSlot, out var listing, out string message);
        if (ok && listing != null)
        {
            if (listing.ListingType == MarketplaceListingType.Gold)
            {
                player.Gold += listing.GoldAmount;
                session.SelectedCharacter.Gold = player.Gold;
                SendGoldUpdate(peer, player.Gold);
            }
            else
            {
                var item = new ItemInstance
                {
                    Slot = returnSlot,
                    ItemId = listing.ItemId,
                    Quantity = listing.Quantity,
                    RefineLevel = listing.RefineLevel,
                    Roll = DeserializeRoll(listing.RollData),
                };
                player.Items.Add(item);
                SendInventoryData(peer, player);
            }
            SendMarketplaceList(peer);
        }

        SendMarketplaceActionResult(peer, ok, message);
    }

    private void HandleMarketplaceBuyListing(NetPeer peer, NetDataReader reader)
    {
        if (!TryGetPlayer(peer, out var player, out _)) return;
        if (!_sessions.TryGetValue(peer, out var session) || session.SelectedCharacter == null) return;

        long listingId = reader.GetLong();
        int destinationSlot = FindFreeInventorySlot(player);
        if (destinationSlot < 0)
        {
            SendMarketplaceActionResult(peer, false, "Inventario cheio.");
            return;
        }

        bool ok = _db.TryBuyMarketplaceListingWithGold(
            session.AccountId,
            session.SelectedCharacter.Id,
            player.Name,
            listingId,
            destinationSlot,
            out var listing,
            out int buyerGold,
            out string message);

        if (ok && listing != null)
        {
            player.Gold = buyerGold;
            session.SelectedCharacter.Gold = buyerGold;
            player.Items.Add(new ItemInstance
            {
                Slot = destinationSlot,
                ItemId = listing.ItemId,
                Quantity = listing.Quantity,
                RefineLevel = listing.RefineLevel,
                Roll = DeserializeRoll(listing.RollData),
            });
            SendGoldUpdate(peer, player.Gold);
            SendInventoryData(peer, player);
            SendMarketplaceList(peer);
        }

        SendMarketplaceActionResult(peer, ok, message);
    }

    private void SendOpenMarketplace(NetPeer peer)
    {
        var writer = PacketSerializer.WritePacket(PacketId.S2C_OpenMarketplace);
        writer.Put("Mercado de Jogadores");
        writer.Put("Anuncios por PIX sao exclusivos para VIP. Compras por PIX nao possuem reembolso e so entregam o item ou gold apos confirmacao segura.");
        peer.Send(writer, DeliveryMethod.ReliableOrdered);
    }

    private void SendMarketplaceList(NetPeer peer, string search = "", int itemType = -1)
    {
        var listings = _db.LoadMarketplaceListings(search, itemType);
        var writer = PacketSerializer.WritePacket(PacketId.S2C_MarketplaceListResult);
        writer.Put(listings.Count);
        foreach (var listing in listings)
            WriteMarketplaceListing(writer, listing);
        peer.Send(writer, DeliveryMethod.ReliableOrdered);
    }

    private static void WriteMarketplaceListing(NetDataWriter writer, MarketplaceListing listing)
    {
        writer.Put(listing.Id);
        writer.Put(listing.SellerName);
        writer.Put((int)listing.ListingType);
        writer.Put((int)listing.CurrencyType);
        writer.Put(listing.ItemId);
        writer.Put(listing.ItemName);
        writer.Put(listing.ItemType);
        writer.Put(listing.Quantity);
        writer.Put(listing.PricePerUnitGold);
        writer.Put(listing.PriceTotalCents);
        writer.Put(listing.GoldAmount);
        writer.Put(listing.RefineLevel);
        writer.Put(listing.RollData);
        writer.Put(listing.Status);
    }

    private void SendMarketplaceActionResult(NetPeer peer, bool success, string message)
    {
        var writer = PacketSerializer.WritePacket(PacketId.S2C_MarketplaceActionResult);
        writer.Put(success);
        writer.Put(message);
        peer.Send(writer, DeliveryMethod.ReliableOrdered);
        SendSystemMessage(peer, message);
    }

    private int FindFreeInventorySlot(PlayerEntity player)
    {
        var used = new HashSet<int>(player.Items.Select(i => i.Slot));
        int limit = GetInventorySlotLimit(player);
        for (int slot = 0; slot < limit; slot++)
            if (!used.Contains(slot))
                return slot;
        return -1;
    }

    private static ItemRoll DeserializeRoll(string rollData)
    {
        if (string.IsNullOrWhiteSpace(rollData))
            return new ItemRoll();

        try
        {
            return JsonSerializer.Deserialize<ItemRoll>(rollData) ?? new ItemRoll();
        }
        catch
        {
            return new ItemRoll();
        }
    }
}
