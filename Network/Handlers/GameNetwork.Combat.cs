#nullable enable
using Godot;
using LiteNetLib.Utils;
using Mithara.Network;

partial class GameNetwork
{
    public void SendAttack(ulong targetId, int skillId = 0)
    {
        _client?.SendPacket(PacketId.C2S_Attack, w =>
        {
            w.Put(targetId);
            w.Put(skillId);
        });
    }

    public void SendRespawn()
    {
        _client?.SendPacket(PacketId.C2S_Respawn, static w => { });
    }

    public void SendRevivePlayer(ulong targetId)
    {
        _client?.SendPacket(PacketId.C2S_RevivePlayer, w =>
        {
            w.Put(targetId);
        });
    }

    public void SendLootPickup(ulong lootId)
    {
        _client?.SendPacket(PacketId.C2S_LootPickup, w =>
        {
            w.Put(lootId);
        });
    }

    private void HandleCombatResult(NetDataReader r)
    {
        ulong attackerId = r.GetULong();
        ulong targetId = r.GetULong();
        int damage = r.GetInt();
        bool isCrit = r.GetBool();
        int targetHealth = r.GetInt();
        int targetMaxHealth = r.GetInt();
        EmitSignal(SignalName.OnCombatResult, attackerId, targetId, damage, isCrit, targetHealth, targetMaxHealth);
    }

    private void HandleEntityDied(NetDataReader r)
    {
        ulong entityId = r.GetULong();
        ulong killerId = r.GetULong();
        EmitSignal(SignalName.OnEntityDied, entityId, killerId);
    }

    private void HandleGainExp(NetDataReader r)
    {
        ulong entityId = r.GetULong();
        int amount = r.GetInt();
        long totalExp = r.GetLong();
        EmitSignal(SignalName.OnGainExp, entityId, amount, totalExp);
    }

    private void HandleLevelUp(NetDataReader r)
    {
        ulong entityId = r.GetULong();
        int newLevel = r.GetInt();
        EmitSignal(SignalName.OnLevelUp, entityId, newLevel);
    }

    private void HandleRespawn(NetDataReader r)
    {
        ulong entityId = r.GetULong();
        float x = r.GetFloat();
        float y = r.GetFloat();
        int health = r.GetInt();
        int maxHealth = r.GetInt();
        EmitSignal(SignalName.OnRespawn, entityId, x, y, health, maxHealth);
    }

    private void HandleLootSpawn(NetDataReader r)
    {
        ulong lootId = r.GetULong();
        float x = r.GetFloat();
        float y = r.GetFloat();
        int itemId = r.GetInt();
        int quantity = r.GetInt();
        EmitSignal(SignalName.OnLootSpawn, lootId, x, y, itemId, quantity);
    }

    private void HandleLootDespawn(NetDataReader r)
    {
        ulong lootId = r.GetULong();
        EmitSignal(SignalName.OnLootDespawn, lootId);
    }
}
