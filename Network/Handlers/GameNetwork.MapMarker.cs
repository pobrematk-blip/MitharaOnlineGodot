using LiteNetLib.Utils;
using Mithara.Network;

public partial class GameNetwork
{
    public void SendMapMarkerPlace(float worldX, float worldY, string playerName)
    {
        _client?.SendPacket(PacketId.C2S_MapMarkerPlace, w =>
        {
            w.Put(worldX);
            w.Put(worldY);
            w.Put(playerName);
        });
    }

    public void SendMapMarkerRemove(int markerId)
    {
        _client?.SendPacket(PacketId.C2S_MapMarkerRemove, w =>
        {
            w.Put(markerId);
        });
    }

    public void SendMapMarkerRequest()
    {
        _client?.SendPacket(PacketId.C2S_MapMarkerRequest, w => { });
    }

    private void HandleMapMarkerUpdate(NetDataReader r)
    {
        byte action = r.GetByte();
        if (action == 0)
        {
            int count = r.GetInt();
            WorldMapUI.ClearMarkers();
            for (int i = 0; i < count; i++)
            {
                int markerId = r.GetInt();
                float wx = r.GetFloat();
                float wy = r.GetFloat();
                string pName = r.GetString();
                WorldMapUI.AddOrUpdateMarker(markerId, wx, wy, pName);
            }
        }
        else if (action == 1)
        {
            int markerId = r.GetInt();
            float wx = r.GetFloat();
            float wy = r.GetFloat();
            string pName = r.GetString();
            WorldMapUI.AddOrUpdateMarker(markerId, wx, wy, pName);
        }
        else if (action == 2)
        {
            int markerId = r.GetInt();
            WorldMapUI.RemoveMarker(markerId);
        }
        else if (action == 3)
        {
            WorldMapUI.ClearMarkers();
        }
    }
}
