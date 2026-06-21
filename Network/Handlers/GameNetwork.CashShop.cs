#nullable enable
using Godot;
using LiteNetLib.Utils;
using Mithara.Network;

partial class GameNetwork
{
    public void SendCashShopBuy(int itemId, int preco)
    {
        _client?.SendPacket(PacketId.C2S_CashShopBuy, w =>
        {
            w.Put(itemId);
            w.Put(preco);
        });
    }

    private void HandleCashShopResult(NetDataReader r)
    {
        bool success = r.GetBool();
        string message = r.GetString();
        Log($"[CASH SHOP] Result: {message}");
        EmitSignal(SignalName.OnCashShopResult, success, message);
    }
}
