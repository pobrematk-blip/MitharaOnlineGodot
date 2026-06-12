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

    public void SendDeleteCharacter(int slotIndex)
    {
        _client?.SendPacket(PacketId.C2S_DeleteCharacter, w =>
        {
            w.Put(slotIndex);
        });
    }

    public void SendEnterWorld(string name, string characterClass, string race, float posX, float posY)
    {
        _client?.SendPacket(PacketId.C2S_EnterWorld, w =>
        {
            w.Put(0); // channelId
            w.Put(name);
            w.Put(characterClass);
            w.Put(race);
            w.Put(posX);
            w.Put(posY);
        });
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

    private void HandleCharacterDeleted(NetDataReader r)
    {
        int count = r.GetInt();
        Characters.Clear();
        var list = new Godot.Collections.Array<Godot.Collections.Dictionary>();
        for (int i = 0; i < count; i++)
        {
            int slot = r.GetInt();
            string name = r.GetString();
            string cls = r.GetString();
            string race = r.GetString();
            int level = r.GetInt();

            Characters.Add(new CharacterEntry
            {
                SlotIndex = slot,
                Name = name,
                Class = cls,
                Race = race,
                Level = level,
            });

            list.Add(new Godot.Collections.Dictionary
            {
                ["slot"] = slot,
                ["name"] = name,
                ["level"] = level,
                ["class_name"] = cls,
                ["race"] = race,
            });
        }
        Log($"[GAME] Lista de personagens atualizada apos exclusao: {count} restantes");
        EmitSignal(SignalName.OnCharacterList, list);
    }

    private void HandleEnterWorld(NetDataReader r)
    {
        Log($"HandleEnterWorld: lendo pacote ({r.AvailableBytes} bytes disponiveis)");
        LocalPlayerId = r.GetULong();
        LocalChannelId = r.GetInt();
        float x = r.GetFloat();
        float y = r.GetFloat();
        PendingPlayerSpawn = new Vector2(x, y);
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

        Log($"Entrando no mundo! ID={LocalPlayerId} Canal={LocalChannelId} Lv={level} Pos=({x:F0},{y:F0})");
        Log("Sinalizando enterWorldPending para _Process fazer a troca de cena");
        _enterWorldPending = true;
    }
}
