#nullable enable
using Godot;
using LiteNetLib.Utils;
using Mithara.Network;

partial class GameNetwork
{
    public void SendAllocateStat(string statName)
    {
        _client?.SendPacket(PacketId.C2S_AllocateStat, w =>
        {
            w.Put(statName);
        });
    }

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
        int remainingXp = r.GetInt();
        EmitSignal(SignalName.OnLevelUp, entityId, newLevel, remainingXp);
    }

    private void HandleRespawn(NetDataReader r)
    {
        ulong entityId = r.GetULong();
        float x = r.GetFloat();
        float y = r.GetFloat();
        int health = r.GetInt();
        int maxHealth = r.GetInt();
        int mana = r.AvailableBytes >= 8 ? r.GetInt() : 0;
        int maxMana = r.AvailableBytes >= 4 ? r.GetInt() : 0;
        EmitSignal(SignalName.OnRespawn, entityId, x, y, health, maxHealth, mana, maxMana);
    }

    private void HandleTeleport(NetDataReader r)
    {
        ulong entityId = r.GetULong();
        float x = r.GetFloat();
        float y = r.GetFloat();
        EmitSignal(SignalName.OnTeleport, entityId, x, y);
    }

    private void HandleSceneChange(NetDataReader r)
    {
        string sceneName = r.GetString();
        float x = r.GetFloat();
        float y = r.GetFloat();
        Log($"[SCENE] Mudança de cena: {sceneName} -> ({x:F1}, {y:F1})");
        EmitSignal(SignalName.OnSceneChange, sceneName, x, y);
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

    private void HandleStatUpdate(NetDataReader r)
    {
        int baseForca = r.GetInt();
        int baseAgilidade = r.GetInt();
        int baseDestreza = r.GetInt();
        int baseInteligencia = r.GetInt();
        int statPoints = r.GetInt();
        int totalForca = r.GetInt();
        int totalAgilidade = r.GetInt();
        int totalDestreza = r.GetInt();
        int totalInteligencia = r.GetInt();
        int maxHealth = r.GetInt();
        int maxMana = r.GetInt();
        EmitSignal(SignalName.OnStatUpdate, baseForca, baseAgilidade, baseDestreza, baseInteligencia, statPoints, totalForca, totalAgilidade, totalDestreza, totalInteligencia, maxHealth, maxMana);
    }
}
