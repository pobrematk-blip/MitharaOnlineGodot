#nullable enable
using Godot;
using LiteNetLib.Utils;
using Mithara.Network;

partial class GameNetwork
{
    public void SendRefineItem(int slot, int itemId, int protectionSlot = -1)
    {
        _client?.SendPacket(PacketId.C2S_RefineItem, w =>
        {
            w.Put(slot);
            w.Put(itemId);
            w.Put(protectionSlot);
        });
    }

    private void HandleRefineResult(NetDataReader r)
    {
        bool success = r.GetBool();
        int newLevel = r.GetInt();
        string message = r.GetString();
        Log($"[REFINE] Result: {message} (new level: +{newLevel})");
        EmitSignal(SignalName.OnRefineResult, success, newLevel, message);
    }

    private void HandleOpenRefine()
    {
        Log("[REFINE] Abertura autorizada pelo servidor.");
        EmitSignal(SignalName.OnOpenRefine);
    }
}
