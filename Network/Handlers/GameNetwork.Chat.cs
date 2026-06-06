#nullable enable
using Godot;
using LiteNetLib.Utils;
using Mithara.Network;

partial class GameNetwork
{
    public void SendChat(byte channel, string targetName, string message, string language)
    {
        _client?.SendPacket(PacketId.C2S_Chat, w =>
        {
            w.Put(channel);
            w.Put(targetName);
            w.Put(message);
            w.Put(language);
        });
    }

    private void HandleChat(NetDataReader r)
    {
        byte channel = r.GetByte();
        string senderName = r.GetString();
        string message = r.GetString();
        string language = r.GetString();
        EmitSignal(SignalName.OnChatMessage, channel, senderName, message, language);
    }
}
