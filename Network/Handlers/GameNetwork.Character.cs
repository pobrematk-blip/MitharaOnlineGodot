#nullable enable
using Godot;
using LiteNetLib.Utils;
using Mithara.Network;

partial class GameNetwork
{
    public void SendCreateCharacter(string name, string className, string race)
    {
        _client?.SendPacket(PacketId.C2S_CreateCharacter, w =>
        {
            w.Put(name);
            w.Put(className);
            w.Put(race);
        });
    }

    public void SendSelectCharacter(int slotIndex)
    {
        _client?.SendPacket(PacketId.C2S_SelectCharacter, w =>
        {
            w.Put(slotIndex);
        });
    }

    public void SendEnterWorld()
    {
        _client?.SendPacket(PacketId.C2S_EnterWorld, w => { });
    }

    private void HandleCharacterList(NetDataReader r)
    {
        int count = r.GetInt();
        var list = new Godot.Collections.Array<Godot.Collections.Dictionary>();
        for (int i = 0; i < count; i++)
        {
            var entry = new Godot.Collections.Dictionary
            {
                ["slot"] = r.GetInt(),
                ["name"] = r.GetString(),
                ["level"] = r.GetInt(),
                ["class_name"] = r.GetString(),
            };
            list.Add(entry);
        }
        EmitSignal(SignalName.OnCharacterList, list);
    }

    private void HandleEnterWorld(NetDataReader r)
    {
        LocalPlayerId = r.GetULong();
        LocalChannelId = r.GetInt();
        float x = r.GetFloat();
        float y = r.GetFloat();
        int level = r.GetInt();
        long xp = r.GetLong();
        int forca = r.GetInt();
        int agilidade = r.GetInt();
        int destreza = r.GetInt();
        int inteligencia = r.GetInt();
        int health = r.GetInt();
        int maxHealth = r.GetInt();
        int mana = r.GetInt();
        int maxMana = r.GetInt();
        int baseAttack = r.GetInt();
        int defense = r.GetInt();

        GD.Print($"[GAME] Entrando no mundo! ID={LocalPlayerId} Canal={LocalChannelId} Lv={level} HP={health}/{maxHealth}");
        GetTree().ChangeSceneToFile(SceneConstants.MAIN);
        EmitSignal(SignalName.OnEnterWorld);
    }
}
