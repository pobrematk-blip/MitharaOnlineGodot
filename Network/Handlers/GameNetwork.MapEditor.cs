#nullable enable
using Godot;
using LiteNetLib.Utils;
using Mithara.Network;

partial class GameNetwork
{
    [Signal] public delegate void OnMapEditorTileDataEventHandler(string sceneName, Godot.Collections.Array<Godot.Collections.Dictionary> tiles);
    [Signal] public delegate void OnMapEditorTileUpdateEventHandler(string sceneName, int tileX, int tileY, byte type, bool removed);

    public void SendMapEditorPlaceTile(string sceneName, int tileX, int tileY, byte type, bool remove)
    {
        _client?.SendPacket(PacketId.C2S_MapEditorPlaceTile, w =>
        {
            w.Put(sceneName);
            w.Put(tileX);
            w.Put(tileY);
            w.Put(type);
            w.Put(remove);
        });
    }

    public void SendMapEditorRequestTiles(string sceneName)
    {
        _client?.SendPacket(PacketId.C2S_MapEditorRequestTiles, w =>
        {
            w.Put(sceneName);
        });
    }

    private void HandleMapEditorTileData(NetDataReader r)
    {
        string sceneName = r.GetString();
        int count = r.GetInt();
        var tiles = new Godot.Collections.Array<Godot.Collections.Dictionary>();

        for (int i = 0; i < count; i++)
        {
            int tileX = r.GetInt();
            int tileY = r.GetInt();
            byte type = r.GetByte();
            var dict = new Godot.Collections.Dictionary
            {
                { "tileX", tileX },
                { "tileY", tileY },
                { "type", (int)type }
            };
            tiles.Add(dict);
        }

        EmitSignal(SignalName.OnMapEditorTileData, sceneName, tiles);
    }

    private void HandleMapEditorTileUpdate(NetDataReader r)
    {
        string sceneName = r.GetString();
        int tileX = r.GetInt();
        int tileY = r.GetInt();
        byte type = r.GetByte();
        bool removed = r.GetBool();

        EmitSignal(SignalName.OnMapEditorTileUpdate, sceneName, tileX, tileY, type, removed);
    }
}
