using Mithara.Server.Entities;
using Mithara.Server.Packets;
using LiteNetLib;
using LiteNetLib.Utils;

namespace Mithara.Server.Network;

public partial class GameServer
{
    public void SendVipStatus(NetPeer peer, DateTime expiry)
    {
        var writer = PacketSerializer.WritePacket(PacketId.S2C_VipStatus);
        writer.Put(expiry.ToBinary());
        peer.Send(writer, DeliveryMethod.ReliableOrdered);
    }

    public bool IsPlayerVip(PlayerEntity player)
    {
        return player.VipExpiry > DateTime.UtcNow;
    }
}
