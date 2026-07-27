using LiteNetLib;
using LiteNetLib.Utils;
using Mithara.Server.Entities;
using Mithara.Server.Packets;

namespace Mithara.Server.Network;

partial class GameServer
{
    private void HandleAllocateStat(NetPeer peer, NetDataReader reader)
    {
        if (!_sessions.TryGetValue(peer, out var session)) return;
        var channel = _world.GetChannel(session.ChannelId);
        if (channel == null) return;
        var entity = channel.GetEntity(session.EntityId);
        if (entity is not PlayerEntity player) return;

        string statName = reader.GetString();

        if (player.StatPoints <= 0)
        {
            SendSystemMessage(peer, "Você não tem pontos de atributo disponíveis!");
            return;
        }

        int baseForca = player.BaseForca;
        int baseAgilidade = player.BaseAgilidade;
        int baseDestreza = player.BaseDestreza;
        int baseInteligencia = player.BaseInteligencia;
        int baseVitalidade = player.BaseVitalidade;
        int baseSorte = player.BaseSorte;
        int statCap = GetAttributeCapForLevel(player.Level);

        switch (statName.ToLowerInvariant())
        {
            case "forca":
            case "força":
            case "str":
                if (!CanIncreaseAttribute(peer, player, "Forca", baseForca, statCap))
                    return;
                baseForca++;
                break;
            case "vitalidade":
            case "vit":
                if (!CanIncreaseAttribute(peer, player, "Vitalidade", baseVitalidade, statCap))
                    return;
                baseVitalidade++;
                break;
            case "agilidade":
            case "agi":
                if (!CanIncreaseAttribute(peer, player, "Agilidade", baseAgilidade, statCap))
                    return;
                baseAgilidade++;
                break;
            case "destreza":
            case "dex":
                if (!CanIncreaseAttribute(peer, player, "Destreza", baseDestreza, statCap))
                    return;
                baseDestreza++;
                break;
            case "inteligencia":
            case "inteligência":
            case "int":
                if (!CanIncreaseAttribute(peer, player, "Inteligencia", baseInteligencia, statCap))
                    return;
                baseInteligencia++;
                break;
            case "sorte":
            case "luk":
                if (!CanIncreaseAttribute(peer, player, "Sorte", baseSorte, statCap))
                    return;
                baseSorte++;
                break;
            default:
                SendSystemMessage(peer, $"Atributo desconhecido: {statName}");
                return;
        }

        player.BaseForca = baseForca;
        player.BaseAgilidade = baseAgilidade;
        player.BaseDestreza = baseDestreza;
        player.BaseInteligencia = baseInteligencia;
        player.BaseVitalidade = baseVitalidade;
        player.BaseSorte = baseSorte;
        player.StatPoints--;

        RecalculatePlayerStats(player);

        if (session.SelectedCharacter != null)
        {
            _db.SaveCharacterStats(session.SelectedCharacter.Id, baseForca, baseAgilidade, baseDestreza, baseInteligencia, baseVitalidade, baseSorte, player.StatPoints);
            session.SelectedCharacter.Forca = baseForca;
            session.SelectedCharacter.Agilidade = baseAgilidade;
            session.SelectedCharacter.Destreza = baseDestreza;
            session.SelectedCharacter.Inteligencia = baseInteligencia;
            session.SelectedCharacter.Vitalidade = baseVitalidade;
            session.SelectedCharacter.Sorte = baseSorte;
            session.SelectedCharacter.StatPoints = player.StatPoints;
        }

        SendStatUpdate(peer, player);

        Logger.Info($"[STATS] {player.Name} allocou {statName} (pts restantes: {player.StatPoints})");
    }

    private static int GetAttributeCapForLevel(int level)
    {
        return Math.Clamp(10 + Math.Max(0, level - 1), 10, 99);
    }

    private bool CanIncreaseAttribute(NetPeer peer, PlayerEntity player, string attributeName, int currentValue, int cap)
    {
        int classBase = GetClassBaseAttribute(player.CharacterClass, attributeName);
        int limit = Math.Min(99, classBase + cap);
        if (currentValue < limit)
            return true;

        SendSystemMessage(peer, $"{attributeName} atingiu o limite {limit} para o seu nivel. Distribua pontos em outro atributo.");
        return false;
    }

    private static int GetClassBaseAttribute(string className, string attributeName)
    {
        string cls = NormalizeClassAlias(className);
        return (cls, attributeName) switch
        {
            ("arqueiro", "Forca") => 12,
            ("arqueiro", "Agilidade") => 16,
            ("arqueiro", "Destreza") => 14,
            ("arqueiro", "Inteligencia") => 8,

            ("ladino", "Forca") => 10,
            ("ladino", "Agilidade") => 18,
            ("ladino", "Destreza") => 16,
            ("ladino", "Inteligencia") => 8,

            ("berseker", "Forca") => 20,
            ("berseker", "Agilidade") => 12,
            ("berseker", "Destreza") => 8,
            ("berseker", "Inteligencia") => 6,

            ("guardiao", "Forca") => 16,
            ("guardiao", "Agilidade") => 8,
            ("guardiao", "Destreza") => 8,
            ("guardiao", "Inteligencia") => 8,

            ("mago", "Forca") => 8,
            ("mago", "Agilidade") => 5,
            ("mago", "Destreza") => 5,
            ("mago", "Inteligencia") => 18,

            ("prist", "Forca") => 8,
            ("prist", "Agilidade") => 10,
            ("prist", "Destreza") => 10,
            ("prist", "Inteligencia") => 16,

            (_, "Vitalidade") => 5,
            (_, "Sorte") => 5,
            _ => 5,
        };
    }

    private void SendStatUpdate(NetPeer peer, PlayerEntity player)
    {
        RefreshTemporarySkillBonuses(player);
        var (danoFisicoMin, danoFisicoMax) = GetDisplayedDamageRange(player, magicDamage: false);
        var (danoMagicoMin, danoMagicoMax) = GetDisplayedDamageRange(player, magicDamage: true);

        var writer = PacketSerializer.WritePacket(PacketId.S2C_StatUpdate);
        writer.Put(player.BaseForca);
        writer.Put(player.BaseAgilidade);
        writer.Put(player.BaseDestreza);
        writer.Put(player.BaseInteligencia);
        writer.Put(player.StatPoints);
        writer.Put(player.Forca);
        writer.Put(player.Agilidade);
        writer.Put(player.Destreza);
        writer.Put(player.Inteligencia);
        writer.Put(player.MaxHealth);
        writer.Put(player.MaxMana);
        writer.Put(player.CalculateDefense());
        writer.Put(player.CalculateMagicDefense());
        writer.Put(player.CalculateCritChance());
        writer.Put(player.CalculateCritMultiplier());
        writer.Put(player.CalculateEvasion());
        writer.Put(player.CalculateMovementSpeedMultiplier());
        writer.Put(player.CalculateAttackSpeedMultiplier());
        writer.Put(player.CalculatePrecision());
        writer.Put(player.CalculateTenacity());
        writer.Put(player.ArmorPenetration);
        writer.Put(player.HealthRegenBonus);
        writer.Put(player.ManaRegenBonus);
        writer.Put(player.LifeSteal);
        writer.Put(player.ManaSteal);
        writer.Put(player.CooldownReduction);
        writer.Put(player.PvpDamageBonus);
        writer.Put(player.PvpDefenseBonus);
        writer.Put(player.BonusExperience);
        writer.Put(player.DamageReflect);
        writer.Put(player.ControlResistance);
        writer.Put(player.BaseVitalidade);
        writer.Put(player.BaseSorte);
        writer.Put(player.Vitalidade);
        writer.Put(player.Sorte);
        writer.Put(danoFisicoMin);
        writer.Put(danoFisicoMax);
        writer.Put(danoMagicoMin);
        writer.Put(danoMagicoMax);
        peer.Send(writer, DeliveryMethod.ReliableOrdered);
    }

    private static (int Min, int Max) GetDisplayedDamageRange(PlayerEntity player, bool magicDamage)
    {
        int rawDamage = magicDamage ? player.CalculateMagicAttackDamage() : player.CalculateAttackDamage();
        if (player.TemporaryDamageBonus > 0f)
            rawDamage = Math.Max(1, (int)MathF.Round(rawDamage * (1f + player.TemporaryDamageBonus)));

        int min = Math.Max(1, (int)MathF.Floor(rawDamage * 0.85f));
        int max = Math.Max(min, (int)MathF.Ceiling(rawDamage * 1.15f));
        if (rawDamage >= 3 && max - min < 2)
        {
            min = Math.Max(1, rawDamage - 1);
            max = rawDamage + 1;
        }

        return (min, max);
    }
}
