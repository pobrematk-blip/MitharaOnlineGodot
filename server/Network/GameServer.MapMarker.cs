using LiteNetLib;
using LiteNetLib.Utils;
using Mithara.Server.Packets;
using Mithara.Server.World;

namespace Mithara.Server.Network;

public partial class GameServer
{
    private int _nextMarkerId = 1;

    private void HandleMapMarkerPlace(NetPeer peer, NetDataReader reader)
    {
        if (!TryGetPlayer(peer, out var player, out var channel)) return;

        float worldX = reader.GetFloat();
        float worldY = reader.GetFloat();
        string playerName = reader.GetString();

        int markerId = _nextMarkerId++;

        channel.AddMarker(new Channel.MapMarkerData
        {
            MarkerId = markerId,
            WorldX = worldX,
            WorldY = worldY,
            PlayerName = playerName,
            ChannelId = channel.Id,
        });

        foreach (var (eid, p) in channel.GetAllPlayerPeers())
        {
            var w = PacketSerializer.WritePacket(PacketId.S2C_MapMarkerUpdate);
            w.Put((byte)1);
            w.Put(markerId);
            w.Put(worldX);
            w.Put(worldY);
            w.Put(playerName);
            p.Send(w, DeliveryMethod.ReliableOrdered);
        }
    }

    private void HandleMapMarkerRemove(NetPeer peer, NetDataReader reader)
    {
        if (!TryGetPlayer(peer, out var player, out var channel)) return;

        int markerId = reader.GetInt();
        channel.RemoveMarker(markerId);

        foreach (var (eid, p) in channel.GetAllPlayerPeers())
        {
            var w = PacketSerializer.WritePacket(PacketId.S2C_MapMarkerUpdate);
            w.Put((byte)2);
            w.Put(markerId);
            p.Send(w, DeliveryMethod.ReliableOrdered);
        }
    }

    private void HandleMapMarkerRequest(NetPeer peer, NetDataReader reader)
    {
        if (!TryGetPlayer(peer, out var player, out var channel)) return;

        var markers = channel.GetAllMarkers();
        var w = PacketSerializer.WritePacket(PacketId.S2C_MapMarkerUpdate);
        w.Put((byte)0);
        w.Put(markers.Count);
        foreach (var m in markers)
        {
            w.Put(m.MarkerId);
            w.Put(m.WorldX);
            w.Put(m.WorldY);
            w.Put(m.PlayerName);
        }
        peer.Send(w, DeliveryMethod.ReliableOrdered);
    }
}
