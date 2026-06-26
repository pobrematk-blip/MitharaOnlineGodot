using LiteNetLib;
using LiteNetLib.Utils;
using Mithara.Server.Entities;
using Mithara.Server.Packets;

namespace Mithara.Server.Network;

public partial class GameServer
{
    private void HandleSceneTeleport(NetPeer peer, NetDataReader reader)
    {
        if (!_sessions.TryGetValue(peer, out var session) || session.EntityId == 0)
            return;

        string targetScene = reader.GetString();
        string teleportId = reader.GetString();
        float targetX = reader.GetFloat();
        float targetY = reader.GetFloat();
        float entryX = reader.GetFloat();
        float entryY = reader.GetFloat();

        var channel = _world.GetChannel(session.ChannelId);
        if (channel == null) return;

        var player = channel.GetEntity(session.EntityId) as PlayerEntity;
        if (player == null) return;

        session.TeleportEntryX = entryX;
        session.TeleportEntryY = entryY;

        float destX, destY;

        if (targetX != 0f || targetY != 0f)
        {
            destX = targetX;
            destY = targetY;
        }
        else
        {
            switch (teleportId)
            {
                case "alfaiataria_sair":
                    if (session.TeleportEntryX != 0f || session.TeleportEntryY != 0f)
                    {
                        destX = session.TeleportEntryX;
                        destY = session.TeleportEntryY + 48f;
                    }
                    else
                    {
                        destX = 148f;
                        destY = 292f;
                    }
                    break;
                default:
                    destX = 100000f;
                    destY = 100000f;
                    break;
            }
        }

        channel.MoveEntity(player.Id, destX, destY);
        session.CurrentMap = targetScene;
        SendSceneChange(peer, targetScene, destX, destY);

        if (session.SelectedCharacter != null)
        {
            session.SelectedCharacter.PosX = destX;
            session.SelectedCharacter.PosY = destY;
            _db.SaveCharacterPosition(session.SelectedCharacter.Id, destX, destY, session.CurrentMap);
        }

        Logger.Info($"[TELEPORT] {player.Name} -> cena={targetScene}, id={teleportId}, pos=({destX:F1}, {destY:F1})");
    }

    private void SendSceneChange(NetPeer peer, string sceneName, float x, float y)
    {
        var writer = PacketSerializer.WritePacket(PacketId.S2C_SceneChange);
        writer.Put(sceneName);
        writer.Put(x);
        writer.Put(y);
        peer.Send(writer, DeliveryMethod.ReliableOrdered);
    }
}
