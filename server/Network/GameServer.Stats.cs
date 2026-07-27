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

        switch (statName.ToLowerInvariant())
        {
            case "forca":
            case "força":
            case "str":
                baseForca++;
                break;
            case "vitalidade":
            case "vit":
                baseVitalidade++;
                break;
            case "agilidade":
            case "agi":
                baseAgilidade++;
                break;
            case "destreza":
            case "dex":
                baseDestreza++;
                break;
            case "inteligencia":
            case "inteligência":
            case "int":
                baseInteligencia++;
                break;
            case "sorte":
            case "luk":
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

    private void SendStatUpdate(NetPeer peer, PlayerEntity player)
    {
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
        peer.Send(writer, DeliveryMethod.ReliableOrdered);
    }
}
