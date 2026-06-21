#nullable enable
using Godot;
using LiteNetLib.Utils;
using Mithara.Network;

partial class GameNetwork
{
    public void SendTradeRequest(string targetName)
    {
        _client?.SendPacket(PacketId.C2S_TradeRequest, w => w.Put(targetName));
    }

    public void SendTradeAccept()
    {
        _client?.SendPacket(PacketId.C2S_TradeAccept, w => { });
    }

    public void SendTradeDecline()
    {
        _client?.SendPacket(PacketId.C2S_TradeDecline, w => { });
    }

    public void SendTradeUpdateOffer(int inventorySlot, int quantity)
    {
        _client?.SendPacket(PacketId.C2S_TradeUpdateOffer, w =>
        {
            w.Put(inventorySlot);
            w.Put(quantity);
        });
    }

    public void SendTradeRemoveOffer(int tradeSlot)
    {
        _client?.SendPacket(PacketId.C2S_TradeRemoveOffer, w => w.Put(tradeSlot));
    }

    public void SendTradeConfirm()
    {
        _client?.SendPacket(PacketId.C2S_TradeConfirm, w => { });
    }

    public void SendTradeCancel()
    {
        _client?.SendPacket(PacketId.C2S_TradeCancel, w => { });
    }

    private void HandleTradeRequested(NetDataReader r)
    {
        string senderName = r.GetString();
        Log($"[TRADE] Trade request from {senderName}");
        InvitePopupUI.ShowInvite("trade", senderName);
    }

    private void HandleTradeStart(NetDataReader r)
    {
        ulong partnerId = r.GetULong();
        string partnerName = r.GetString();
        Log($"[TRADE] Trade started with {partnerName}");
        EmitSignal(SignalName.OnTradeStart, partnerId, partnerName);
    }

    private void HandleTradeOfferUpdate(NetDataReader r)
    {
        ulong playerSide = r.GetULong();
        byte count = r.GetByte();
        var offers = new Godot.Collections.Array<Godot.Collections.Dictionary>();
        for (int i = 0; i < count; i++)
        {
            byte tradeSlot = r.GetByte();
            int itemId = r.GetInt();
            int quantity = r.GetInt();
            var entry = new Godot.Collections.Dictionary
            {
                ["slot"] = (int)tradeSlot,
                ["item_id"] = itemId,
                ["quantity"] = quantity,
            };
            offers.Add(entry);
        }
        EmitSignal(SignalName.OnTradeOfferUpdate, playerSide, offers);
    }

    private void HandleTradePartnerConfirm(NetDataReader r)
    {
        ulong playerSide = r.GetULong();
        bool confirmed = r.GetBool();
        EmitSignal(SignalName.OnTradePartnerConfirm, playerSide, confirmed);
    }

    private void HandleTradeEnd(NetDataReader r)
    {
        bool success = r.GetBool();
        Log(success ? "[TRADE] Trade completed successfully!" : "[TRADE] Trade ended.");
        EmitSignal(SignalName.OnTradeEnd, success);
    }
}
