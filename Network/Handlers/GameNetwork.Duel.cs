#nullable enable
using Godot;
using LiteNetLib.Utils;
using Mithara.Network;

partial class GameNetwork
{
    public void SendDuelRequest(string targetName)
    {
        _client?.SendPacket(PacketId.C2S_DuelRequest, w => w.Put(targetName));
        Log($"[DUEL] Sent duel request to {targetName}");
    }

    public void SendDuelAccept()
    {
        _client?.SendPacket(PacketId.C2S_DuelAccept, w => { });
        Log("[DUEL] Sent duel accept");
    }

    public void SendDuelDecline()
    {
        _client?.SendPacket(PacketId.C2S_DuelDecline, w => { });
        Log("[DUEL] Sent duel decline");
    }

    private void HandleDuelRequested(NetDataReader r)
    {
        string senderName = r.GetString();
        Log($"[DUEL] Received duel request from {senderName}");
        InvitePopupUI.ShowInvite("duel", senderName);
    }

    private void HandleDuelStart(NetDataReader r)
    {
        ulong opponentId = r.GetULong();
        string opponentName = r.GetString();
        Log($"[DUEL] Duel started vs {opponentName}");
        EmitSignal(SignalName.OnDuelStart, opponentId, opponentName);
    }

    private void HandleDuelEnd(NetDataReader r)
    {
        bool won = r.GetBool();
        Log($"[DUEL] Duel ended: {(won ? "won" : "lost")}");
        EmitSignal(SignalName.OnDuelEnd, won);
    }
}
