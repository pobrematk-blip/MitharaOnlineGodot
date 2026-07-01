using LiteNetLib;
using LiteNetLib.Utils;
using Mithara.Server.Packets;

namespace Mithara.Server.Network;

public partial class GameServer
{
    private void SendSceneChange(NetPeer peer, string sceneName, float x, float y)
    {
        var writer = PacketSerializer.WritePacket(PacketId.S2C_SceneChange);
        writer.Put(sceneName);
        writer.Put(x);
        writer.Put(y);
        peer.Send(writer, DeliveryMethod.ReliableOrdered);
    }
}
