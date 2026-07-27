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

    public void SendBossLootRollChoice(int rollId, bool wantDrop)
    {
        _client?.SendPacket(PacketId.C2S_BossLootRollChoice, w =>
        {
            w.Put(rollId);
            w.Put(wantDrop);
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
        int skillId = r.AvailableBytes >= 4 ? r.GetInt() : 0;
        EmitSignal(SignalName.OnCombatResult, attackerId, targetId, damage, isCrit, targetHealth, targetMaxHealth, skillId);
    }

    private void HandleSkillAreaEffect(NetDataReader r)
    {
        int skillId = r.GetInt();
        float x = r.GetFloat();
        float y = r.GetFloat();
        float radius = r.GetFloat();
        float duration = r.GetFloat();
        EmitSignal(SignalName.OnSkillAreaEffect, skillId, x, y, radius, duration);
    }

    private void HandleSkillVisualEffect(NetDataReader r)
    {
        ulong casterId = r.GetULong();
        ulong targetId = r.GetULong();
        int skillId = r.GetInt();
        float x = r.GetFloat();
        float y = r.GetFloat();
        float duration = r.GetFloat();
        EmitSignal(SignalName.OnSkillVisualEffect, casterId, targetId, skillId, x, y, duration);
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

    private void HandleBossLootRollStart(NetDataReader r)
    {
        int rollId = r.GetInt();
        int itemId = r.GetInt();
        int quantity = r.GetInt();
        string itemName = r.GetString();
        string rarity = r.GetString();
        float seconds = r.GetFloat();

        EmitSignal(SignalName.OnBossLootRollStart, rollId, itemId, quantity, itemName, rarity, seconds);
        CallDeferred(nameof(OpenBossLootRollUi), rollId, itemId, quantity, itemName, rarity, seconds);
    }

    private void HandleBossLootRollResult(NetDataReader r)
    {
        int rollId = r.GetInt();
        bool won = r.GetBool();
        string message = r.GetString();
        string winnerName = r.GetString();
        int winningRoll = r.GetInt();
        int myRoll = r.GetInt();

        EmitSignal(SignalName.OnBossLootRollResult, rollId, won, message, winnerName, winningRoll, myRoll);
        CallDeferred(nameof(ApplyBossLootRollResult), rollId, won, message, winnerName, winningRoll, myRoll);
    }

    private void OpenBossLootRollUi(int rollId, int itemId, int quantity, string itemName, string rarity, float seconds)
    {
        var parent = GetTree()?.Root;
        if (parent == null) return;

        var ui = parent.FindChild("BossLootRollUI", true, false) as BossLootRollUI;
        if (ui == null)
        {
            ui = new BossLootRollUI { Name = "BossLootRollUI" };
            parent.AddChild(ui);
        }

        ui.AddRoll(rollId, itemId, quantity, itemName, rarity, seconds, this);
    }

    private void ApplyBossLootRollResult(int rollId, bool won, string message, string winnerName, int winningRoll, int myRoll)
    {
        var ui = GetTree()?.Root?.FindChild("BossLootRollUI", true, false) as BossLootRollUI;
        ui?.ShowResult(rollId, won, message, winnerName, winningRoll, myRoll);
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
        int defesaFisica = r.AvailableBytes >= 4 ? r.GetInt() : 0;
        int defesaMagica = r.AvailableBytes >= 4 ? r.GetInt() : 0;
        float chanceCritica = r.AvailableBytes >= 4 ? r.GetFloat() : 0f;
        float danoCritico = r.AvailableBytes >= 4 ? r.GetFloat() : 1.5f;
        float evasao = r.AvailableBytes >= 4 ? r.GetFloat() : 0f;
        float velocidadeMovimento = r.AvailableBytes >= 4 ? r.GetFloat() : 1f;
        float velocidadeAtaque = r.AvailableBytes >= 4 ? r.GetFloat() : 1f;
        float precisao = r.AvailableBytes >= 4 ? r.GetFloat() : 75f;
        float tenacidade = r.AvailableBytes >= 4 ? r.GetFloat() : 0f;
        float penetracaoArmadura = r.AvailableBytes >= 4 ? r.GetFloat() : 0f;
        float regeneracaoVida = r.AvailableBytes >= 4 ? r.GetFloat() : 0f;
        float regeneracaoMana = r.AvailableBytes >= 4 ? r.GetFloat() : 0f;
        float rouboVida = r.AvailableBytes >= 4 ? r.GetFloat() : 0f;
        float rouboMana = r.AvailableBytes >= 4 ? r.GetFloat() : 0f;
        float reducaoCooldown = r.AvailableBytes >= 4 ? r.GetFloat() : 0f;
        int danoPvp = r.AvailableBytes >= 4 ? r.GetInt() : 0;
        int defesaPvp = r.AvailableBytes >= 4 ? r.GetInt() : 0;
        float bonusExperiencia = r.AvailableBytes >= 4 ? r.GetFloat() : 0f;
        float reflexaoDano = r.AvailableBytes >= 4 ? r.GetFloat() : 0f;
        float resistenciaControle = r.AvailableBytes >= 4 ? r.GetFloat() : 0f;
        int baseVitalidade = r.AvailableBytes >= 4 ? r.GetInt() : 5;
        int baseSorte = r.AvailableBytes >= 4 ? r.GetInt() : 5;
        int totalVitalidade = r.AvailableBytes >= 4 ? r.GetInt() : baseVitalidade;
        int totalSorte = r.AvailableBytes >= 4 ? r.GetInt() : baseSorte;
        int danoFisicoMin = r.AvailableBytes >= 4 ? r.GetInt() : 0;
        int danoFisicoMax = r.AvailableBytes >= 4 ? r.GetInt() : 0;
        int danoMagicoMin = r.AvailableBytes >= 4 ? r.GetInt() : 0;
        int danoMagicoMax = r.AvailableBytes >= 4 ? r.GetInt() : 0;
        _pendingBaseVitalidade = baseVitalidade;
        _pendingBaseSorte = baseSorte;
        EmitSignal(SignalName.OnStatUpdate, baseForca, baseAgilidade, baseDestreza, baseInteligencia, statPoints, totalForca, totalAgilidade, totalDestreza, totalInteligencia, maxHealth, maxMana, defesaFisica, defesaMagica, chanceCritica, danoCritico, evasao, velocidadeMovimento, velocidadeAtaque, precisao, tenacidade, penetracaoArmadura, regeneracaoVida, regeneracaoMana, rouboVida, rouboMana, reducaoCooldown, danoPvp, defesaPvp, bonusExperiencia, reflexaoDano, resistenciaControle, baseVitalidade, baseSorte, totalVitalidade, totalSorte, danoFisicoMin, danoFisicoMax, danoMagicoMin, danoMagicoMax);
    }
}
