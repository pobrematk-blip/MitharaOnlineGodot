#nullable enable
using Godot;
using LiteNetLib.Utils;
using Mithara.Network;

partial class GameNetwork
{
    public void SendPlayerAction(byte actionType, Vector2 direction)
    {
        _client?.SendPacket(PacketId.C2S_PlayerAction, w =>
        {
            w.Put(actionType);
            w.Put(direction.X);
            w.Put(direction.Y);
        });
    }

    public void SendPlayerMove(Vector2 position, Vector2 direction, bool moving, bool sprinting)
    {
        _client?.SendPacketUnreliable(PacketId.C2S_PlayerMove, w =>
        {
            w.Put(position.X);
            w.Put(position.Y);
            w.Put(direction.X);
            w.Put(direction.Y);
            w.Put(moving);
            w.Put(sprinting);
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

    public void SendSkillUse(int skillSlot, int skillId, Vector2 targetPosition)
    {
        _client?.SendPacket(PacketId.C2S_SkillUse, w =>
        {
            w.Put(skillSlot);
            w.Put(skillId);
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
        string extra3 = "";

        switch (entityType)
        {
            case 0:
                typeLabel = "player";
                extra1 = r.GetString(); // CharacterClass
                extra2 = r.GetString(); // Race
                extra3 = Json.Stringify(new Godot.Collections.Dictionary
                {
                    ["xp"] = r.GetLong(),
                    ["xp_max"] = r.GetLong(),
                    ["guild_name"] = r.GetString(),
                    ["guild_tag"] = r.GetString(),
                    ["guild_emblem"] = r.GetInt(),
                });
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
        EmitSignal(SignalName.OnEntitySpawned, entityId, typeLabel, name, x, y, level, health, maxHealth, extra1, extra2, extra3);
    }

    private void HandleDespawnEntity(NetDataReader r)
    {
        ulong entityId = r.GetULong();
        GetNodeOrNull<EntityManager>("EntityManager")?.RemoveNetworkEntity(entityId);
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
        bool sprinting = r.GetBool();

        if (entityId == LocalPlayerId)
        {
            var localPlayer = GetTree()?.CurrentScene?.FindChild("Player", true, false) as Player;
            localPlayer?.ApplyServerPosition(x, y, animated: true);
            return;
        }

        var em = GetNodeOrNull<EntityManager>("EntityManager");
        em?.PushRemotePosition(entityId, new Vector2(x, y), new Vector2(dirX, dirY), moving, sprinting);
    }

    private void HandlePlayerAction(NetDataReader r)
    {
        ulong entityId = r.GetULong();
        byte actionType = r.GetByte();
        var direction = new Vector2(r.GetFloat(), r.GetFloat());

        if (entityId == LocalPlayerId) return;
        GetNodeOrNull<EntityManager>("EntityManager")?.HandleRemoteAction(entityId, actionType, direction);
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
            bool sprinting = r.GetBool();
            int health = r.GetInt();
            int maxHealth = r.GetInt();
            int mana = r.GetInt();
            int maxMana = r.GetInt();
            int level = r.GetInt();
            string name = r.GetString();
            string factionId = r.GetString();
            byte aiState = r.AvailableBytes > 0 ? r.GetByte() : (byte)0;
            long xp = r.GetLong();
            long xpMax = r.GetLong();
            string guildName = r.GetString();
            string guildTag = r.GetString();
            int guildEmblem = r.GetInt();

			if (entityId != LocalPlayerId)
				em?.PushRemotePosition(entityId, new Vector2(x, y), new Vector2(dirX, dirY), moving, sprinting, aiState);
			em?.AtualizarOverheadRemoto(entityId, name, guildName, guildTag, guildEmblem, xp, xpMax);
			EmitSignal(SignalName.OnEntityHealthUpdate, entityId, health, maxHealth);
			EmitSignal(SignalName.OnEntityManaUpdate, entityId, mana, maxMana);
        }
    }

    private void HandleChannelList(NetDataReader r)
    {
        int count = r.GetInt();
        GD.Print($"[GAME] Canais disponíveis: {count}");
    }
}
