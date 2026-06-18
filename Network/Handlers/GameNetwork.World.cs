#nullable enable
using Godot;
using LiteNetLib.Utils;
using Mithara.Network;

partial class GameNetwork
{
    public void SendPlayerMove(Vector2 position, Vector2 direction, bool moving)
    {
        _client?.SendPacketUnreliable(PacketId.C2S_PlayerMove, w =>
        {
            w.Put(position.X);
            w.Put(position.Y);
            w.Put(direction.X);
            w.Put(direction.Y);
            w.Put(moving);
        });
    }

    public void SendPlayerStop(Vector2 position)
    {
        _client?.SendPacketUnreliable(PacketId.C2S_PlayerStop, w =>
        {
            w.Put(position.X);
            w.Put(position.Y);
        });
    }

    public void SendSkillUse(int skillSlot, Vector2 targetPosition)
    {
        _client?.SendPacket(PacketId.C2S_SkillUse, w =>
        {
            w.Put(skillSlot);
            w.Put(targetPosition.X);
            w.Put(targetPosition.Y);
        });
    }

    public void SendChannelSwitch(int channelId)
    {
        _client?.SendPacket(PacketId.C2S_ChannelSwitch, w =>
        {
            w.Put(channelId);
        });
    }

    private void HandleSpawnEntity(NetDataReader r)
    {
        ulong entityId = r.GetULong();
        byte entityType = r.GetByte();
        string name = r.GetString();
        float x = r.GetFloat();
        float y = r.GetFloat();
        float speed = r.GetFloat();
        int level = r.GetInt();
        int health = r.GetInt();
        int maxHealth = r.GetInt();
        string factionId = r.GetString();

        string typeLabel;
        string extra1 = "";
        string extra2 = "";

        switch (entityType)
        {
            case 0:
                typeLabel = "player";
                extra1 = r.GetString(); // CharacterClass
                extra2 = r.GetString(); // Race
                break;
            case 1:
            case 2:
                typeLabel = entityType == 2 ? "boss" : "monster";
                bool isBoss = r.GetBool();
                int expReward = r.GetInt();
                extra1 = r.GetString(); // PrefabId for scene selection
                extra2 = name; // Keep original name
                bool passive = r.GetBool(); // not used client-side
                break;
            case 3:
                typeLabel = "npc";
                extra1 = r.GetString(); // DialogId
                extra2 = r.GetString(); // Race (spritesheet filename)
                string animPrefix = r.GetString(); // AnimPrefix
                EmitSignal(SignalName.OnEntitySpawned, entityId, typeLabel, name, x, y, level, health, maxHealth, extra1, extra2, animPrefix);
                return;
            default:
                typeLabel = "unknown";
                break;
        }

        GD.Print($"[GAME] Spawn {typeLabel}: {name} em ({x:F1}, {y:F1}) [HP={health}/{maxHealth}]");
        EmitSignal(SignalName.OnEntitySpawned, entityId, typeLabel, name, x, y, level, health, maxHealth, extra1, extra2, "");
    }

    private void HandleDespawnEntity(NetDataReader r)
    {
        ulong entityId = r.GetULong();
        RemoveEntity(entityId);
    }

    private void HandleEntityMove(NetDataReader r)
    {
        ulong entityId = r.GetULong();
        float x = r.GetFloat();
        float y = r.GetFloat();
        float dirX = r.GetFloat();
        float dirY = r.GetFloat();
        bool moving = r.GetBool();

        var em = GetNodeOrNull<EntityManager>("EntityManager");
        em?.PushRemotePosition(entityId, new Vector2(x, y), new Vector2(dirX, dirY), moving);
    }

    private void HandleEntityUpdate(NetDataReader r)
    {
        var em = GetNodeOrNull<EntityManager>("EntityManager");
        int count = r.GetInt();
        for (int i = 0; i < count; i++)
        {
            ulong entityId = r.GetULong();
            float x = r.GetFloat();
            float y = r.GetFloat();
            float dirX = r.GetFloat();
            float dirY = r.GetFloat();
            bool moving = r.GetBool();
            int health = r.GetInt();
            int maxHealth = r.GetInt();
            int mana = r.GetInt();
            int maxMana = r.GetInt();
            int level = r.GetInt();
            string name = r.GetString();
            string factionId = r.GetString();

            em?.PushRemotePosition(entityId, new Vector2(x, y), new Vector2(dirX, dirY), moving);
            EmitSignal(SignalName.OnEntityHealthUpdate, entityId, health, maxHealth);
        }
    }

    private void HandleChannelList(NetDataReader r)
    {
        int count = r.GetInt();
        GD.Print($"[GAME] Canais disponíveis: {count}");
    }
}
