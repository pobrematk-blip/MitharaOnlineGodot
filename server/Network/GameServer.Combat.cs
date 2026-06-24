using LiteNetLib;
using LiteNetLib.Utils;
using Mithara.Server.Entities;
using Mithara.Server.Packets;
using Mithara.Server.World;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Mithara.Server.Network;

partial class GameServer
{
    private static long XpForNextLevel(int level)
    {
        if (level < 1) level = 1;
        return 80L + level * 15L + level * level * 2L;
    }

    private bool HandleMonsterAIAttack(Channel channel, MonsterEntity mob, Entity target, double gameTime)
    {
        int targetDefense = target switch
        {
            PlayerEntity p => p.CalculateDefense(),
            MonsterEntity m => m.CalculateDefense(),
            _ => 0,
        };
        if (target is PlayerEntity playerTarget
            && Random.Shared.NextDouble() * 100.0 < Math.Min(40f, playerTarget.EquipmentEvasion))
            return true;
        float defReduction = MathF.Min(0.80f, targetDefense / (targetDefense + 400f));
        bool isCrit = Random.Shared.Next(100) < mob.Destreza / 4;
        int rawDamage = mob.CalculateAttackDamage();
        int damage = Math.Max(1, (int)(rawDamage * (1f - defReduction)));
        if (isCrit) damage = (int)(damage * 1.5f);

        target.Health -= damage;
        if (target.Health < 0) target.Health = 0;

        if (target is PlayerEntity)
        {
            target.LastCombatTime = gameTime;
            BroadcastPartyMemberUpdateForEntity(target.Id);
        }

        var writer = PacketSerializer.WritePacket(PacketId.S2C_CombatResult);
        writer.Put(mob.Id);
        writer.Put(target.Id);
        writer.Put(damage);
        writer.Put(isCrit);
        writer.Put(target.Health);
        writer.Put(target.MaxHealth);

        var aoi = channel.GetEntitiesInAoi(mob.X, mob.Y);
        foreach (var eid in aoi)
        {
            var p = channel.GetPlayerPeer(eid);
            if (p != null)
            {
                p.Send(writer, DeliveryMethod.ReliableOrdered);
                writer = PacketSerializer.WritePacket(PacketId.S2C_CombatResult);
                writer.Put(mob.Id);
                writer.Put(target.Id);
                writer.Put(damage);
                writer.Put(isCrit);
                writer.Put(target.Health);
                writer.Put(target.MaxHealth);
            }
        }

        if (target.Health <= 0 && target is PlayerEntity player)
        {
            var session = _sessions.Values.FirstOrDefault(s => s.EntityId == target.Id);
            if (session != null)
                SendSystemMessage(session.Peer, "Você morreu!");
        }

        return target.Health <= 0;
    }

    private void HandleRespawn(NetPeer peer)
    {
        if (!_sessions.TryGetValue(peer, out var session)) return;
        var channel = _world.GetChannel(session.ChannelId);
        if (channel == null) return;
        var player = channel.GetEntity(session.EntityId) as PlayerEntity;
        if (player == null || player.Health > 0) return;

        player.Health = player.MaxHealth;
        player.LastCombatTime = _gameTime - 120.0;

        var character = session.SelectedCharacter;
        float spawnX = character?.PosX ?? 1000f;
        float spawnY = character?.PosY ?? 1000f;

        float oldX = player.X;
        float oldY = player.Y;
        channel.MoveEntity(player.Id, spawnX, spawnY);

        var writer = PacketSerializer.WritePacket(PacketId.S2C_Respawn);
        writer.Put(player.Id);
        writer.Put(spawnX);
        writer.Put(spawnY);
        writer.Put(player.Health);
        writer.Put(player.MaxHealth);

        var aoi = channel.GetEntitiesInAoi(spawnX, spawnY);
        aoi.UnionWith(channel.GetEntitiesInAoi(oldX, oldY));
        foreach (var eid in aoi)
        {
            var p = channel.GetPlayerPeer(eid);
            if (p != null)
            {
                p.Send(writer, DeliveryMethod.ReliableOrdered);
                writer = PacketSerializer.WritePacket(PacketId.S2C_Respawn);
                writer.Put(player.Id);
                writer.Put(spawnX);
                writer.Put(spawnY);
                writer.Put(player.Health);
                writer.Put(player.MaxHealth);
            }
        }
    }

    private void HandleRevivePlayer(NetPeer peer, NetDataReader reader)
    {
        if (!_sessions.TryGetValue(peer, out var session)) return;
        var channel = _world.GetChannel(session.ChannelId);
        if (channel == null) return;
        var healer = channel.GetEntity(session.EntityId) as PlayerEntity;
        if (healer == null || healer.Health <= 0) return;

        ulong targetId = reader.GetULong();
        var target = channel.GetEntity(targetId) as PlayerEntity;
        if (target == null || target.Health > 0) return;

        float dx = healer.X - target.X;
        float dy = healer.Y - target.Y;
        if (MathF.Sqrt(dx * dx + dy * dy) > 80f) return;

        var scroll = healer.Items.FirstOrDefault(i => i.ItemId == 101 && i.Quantity > 0);
        if (scroll == null)
        {
            SendSystemMessage(peer, "Voc? precisa de um Pergaminho de Ressurrei??o para reviver algu?m!");
            return;
        }

        scroll.Quantity--;
        if (scroll.Quantity <= 0)
        {
            healer.Items.Remove(scroll);
            var character = session.SelectedCharacter;
            if (character != null)
                _db.DeleteItem(character.Id, scroll.DbId);
        }
        else
        {
            var character = session.SelectedCharacter;
            if (character != null)
                _db.SaveItem(character.Id, scroll);
        }

        var wItemUpdate = PacketSerializer.WritePacket(PacketId.S2C_ItemUpdate);
        wItemUpdate.Put(scroll.Slot);
        wItemUpdate.Put(scroll.Quantity > 0 ? scroll.ItemId : 0);
        wItemUpdate.Put(scroll.Quantity > 0 ? scroll.Quantity : 0);
        wItemUpdate.Put(0);
        wItemUpdate.Put("");
        peer.Send(wItemUpdate, DeliveryMethod.ReliableOrdered);

        target.Health = target.MaxHealth;
        target.LastCombatTime = _gameTime - 120.0;

        var writer = PacketSerializer.WritePacket(PacketId.S2C_Respawn);
        writer.Put(target.Id);
        writer.Put(target.X);
        writer.Put(target.Y);
        writer.Put(target.Health);
        writer.Put(target.MaxHealth);

        var aoi = channel.GetEntitiesInAoi(target.X, target.Y);
        foreach (var eid in aoi)
        {
            var p = channel.GetPlayerPeer(eid);
            if (p != null)
            {
                p.Send(writer, DeliveryMethod.ReliableOrdered);
                writer = PacketSerializer.WritePacket(PacketId.S2C_Respawn);
                writer.Put(target.Id);
                writer.Put(target.X);
                writer.Put(target.Y);
                writer.Put(target.Health);
                writer.Put(target.MaxHealth);
            }
        }
    }

    private void HandleSkillUse(NetPeer peer, NetDataReader reader)
    {
        if (!_sessions.TryGetValue(peer, out var session)) return;
        var channel = _world.GetChannel(session.ChannelId);
        if (channel == null) return;
        var caster = channel.GetEntity(session.EntityId) as PlayerEntity;
        if (caster == null || caster.Health <= 0) return;

        caster.LastCombatTime = _gameTime;

        int skillSlot = reader.GetInt();
        int skillId = reader.AvailableBytes >= 12 ? reader.GetInt() : 0;
        float targetX = reader.GetFloat();
        float targetY = reader.GetFloat();

        if (skillId <= 0)
        {
            SendSystemMessage(peer, "Habilidade inválida. Reatribua a skill na barra.");
            return;
        }

        if (skillSlot < 0 || skillSlot >= caster.SkillBarSlots.Length || caster.SkillBarSlots[skillSlot] != skillId)
        {
            SendSystemMessage(peer, "Esta habilidade nÃ£o estÃ¡ autorizada neste slot da barra.");
            return;
        }

        var skill = ServerSkillCatalog.Get(skillId);
        if (skill == null)
        {
            SendSystemMessage(peer, $"Skill {skillId} não encontrada no catálogo do servidor.");
            return;
        }

        if (!ServerSkillCatalog.ClassMatches(caster.CharacterClass, skill.ClasseRestrita))
        {
            SendSystemMessage(peer, "Esta habilidade não pertence à sua classe.");
            return;
        }

        if (caster.Level < skill.NivelRequerido)
        {
            SendSystemMessage(peer, $"Nível {skill.NivelRequerido} necessário para usar {skill.Nome}.");
            return;
        }

        if (!ServerTalentCatalog.IsSkillUnlocked(caster.CharacterClass, caster.UnlockedTalents, skill.SkillId))
        {
            SendSystemMessage(peer, "Esta habilidade ainda nÃ£o foi desbloqueada na Ã¡rvore de talentos.");
            return;
        }

        if (caster.SkillCooldowns.TryGetValue(skill.SkillId, out double readyAt) && readyAt > _gameTime)
        {
            SendSystemMessage(peer, $"{skill.Nome} em recarga por {(readyAt - _gameTime):0.0}s.");
            return;
        }

        if (skill.CustoMana > 0 && caster.Mana < skill.CustoMana)
        {
            SendSystemMessage(peer, "Mana insuficiente.");
            return;
        }

        bool applied = ApplyServerSkill(peer, channel, caster, session, skill, targetX, targetY);
        if (!applied) return;

        if (skill.Cooldown > 0)
            caster.SkillCooldowns[skill.SkillId] = _gameTime + skill.Cooldown;
        if (skill.CustoMana > 0)
            caster.Mana = Math.Max(0, caster.Mana - skill.CustoMana);

        SendSystemMessage(peer, $"{skill.Nome} usada.");
    }

    private bool ApplyServerSkill(NetPeer peer, Channel channel, PlayerEntity caster, PlayerSession session, ServerSkillDefinition skill, float targetX, float targetY)
    {
        int effect = skill.EffectType;
        bool supportSkill = effect is 1 or 3 or 10;
        if (supportSkill)
            return ApplySupportSkill(peer, channel, caster, skill, targetX, targetY);

        float range = skill.TargetType == 0 ? 0f : 720f;
        float dxTarget = targetX - caster.X;
        float dyTarget = targetY - caster.Y;
        if (range > 0 && MathF.Sqrt(dxTarget * dxTarget + dyTarget * dyTarget) > range)
        {
            SendSystemMessage(peer, "Alvo fora do alcance.");
            return false;
        }

        float radius = skill.IsArea ? 180f : 80f;
        var targets = channel.GetEntitiesInAoi(caster.X, caster.Y)
            .Select(id => channel.GetEntity(id))
            .Where(e => e != null && e.Health > 0 && e.Id != caster.Id)
            .Where(e => e is MonsterEntity mob && mob.FactionId != caster.FactionId || e is PlayerEntity p && p.FactionId != caster.FactionId)
            .Select(e => e!)
            .Where(e =>
            {
                float dx = e.X - targetX;
                float dy = e.Y - targetY;
                return MathF.Sqrt(dx * dx + dy * dy) <= radius;
            })
            .OrderBy(e =>
            {
                float dx = e.X - targetX;
                float dy = e.Y - targetY;
                return dx * dx + dy * dy;
            })
            .Take(skill.IsArea ? Math.Max(1, skill.MaxTargets) : 1)
            .ToList();

        if (targets.Count == 0)
        {
            SendSystemMessage(peer, "Nenhum alvo válido para a habilidade.");
            return false;
        }

        foreach (var target in targets)
        {
            int damage = CalculateSkillDamage(caster, target, skill, out bool isCrit);
            target.Health = Math.Max(0, target.Health - damage);
            if (target is PlayerEntity playerTarget)
            {
                playerTarget.LastCombatTime = _gameTime;
                BroadcastPartyMemberUpdateForEntity(playerTarget.Id);
            }
            if (target is MonsterEntity hitMob)
                hitMob.TargetEntityId = caster.Id;

            BroadcastCombatResult(channel, caster.Id, target.Id, damage, isCrit, target.Health, target.MaxHealth, caster.X, caster.Y);
            ApplySkillDebuff(target, skill);

            if (target is MonsterEntity killedMob && target.Health <= 0)
                HandleMonsterDeath(channel, killedMob, caster, session, target.Id);
        }

        return true;
    }

    private bool ApplySupportSkill(NetPeer peer, Channel channel, PlayerEntity caster, ServerSkillDefinition skill, float targetX, float targetY)
    {
        PlayerEntity target = caster;
        if (skill.TargetType == 1)
        {
            target = channel.GetEntitiesInAoi(caster.X, caster.Y)
                .Select(id => channel.GetEntity(id) as PlayerEntity)
                .Where(p => p != null && p.Health > 0 && p.FactionId == caster.FactionId)
                .OrderBy(p =>
                {
                    float dx = p!.X - targetX;
                    float dy = p.Y - targetY;
                    return dx * dx + dy * dy;
                })
                .FirstOrDefault() ?? caster;
        }

        int amount = Math.Max(1, skill.Valor);
        if (skill.EffectType == 1)
        {
            int oldHealth = target.Health;
            target.Health = Math.Min(target.MaxHealth, target.Health + amount);
            BroadcastCombatResult(channel, caster.Id, target.Id, -(target.Health - oldHealth), false, target.Health, target.MaxHealth, caster.X, caster.Y);
            BroadcastPartyMemberUpdateForEntity(target.Id);
            return true;
        }

        string buffId = skill.BuffDebuff.Length > 0 ? skill.BuffDebuff : skill.Nome;
        target.ActiveServerBuffs[buffId] = _gameTime + Math.Max(1f, skill.Duracao);
        if (skill.EffectType == 10)
        {
            int oldHealth = target.Health;
            target.Health = Math.Min(target.MaxHealth, target.Health + amount);
            BroadcastCombatResult(channel, caster.Id, target.Id, -(target.Health - oldHealth), false, target.Health, target.MaxHealth, caster.X, caster.Y);
        }
        SendSystemMessage(peer, $"{skill.Nome}: efeito aplicado.");
        return true;
    }

    private int CalculateSkillDamage(PlayerEntity caster, Entity target, ServerSkillDefinition skill, out bool isCrit)
    {
        int baseDamage = caster.CalculateAttackDamage();
        float multiplier = skill.DamageMultiplier <= 0 ? 1f : skill.DamageMultiplier;
        int rawDamage = Math.Max(1, (int)(baseDamage * multiplier) + skill.FlatPower);
        int targetDefense = target switch
        {
            PlayerEntity p => p.CalculateDefense(),
            MonsterEntity m => m.CalculateDefense(),
            _ => 0,
        };
        float defReduction = MathF.Min(0.80f, targetDefense / (targetDefense + 400f));
        isCrit = Random.Shared.Next(100) < caster.Destreza / 4;
        int damage = Math.Max(1, (int)(rawDamage * (1f - defReduction)));
        if (isCrit) damage = (int)(damage * 1.5f);
        return damage;
    }

    private void ApplySkillDebuff(Entity target, ServerSkillDefinition skill)
    {
        if (target is not PlayerEntity playerTarget || skill.Duracao <= 0) return;
        string debuff = skill.EffectType switch
        {
            8 => "stun",
            9 => "bleed",
            12 => "slow",
            13 => "root",
            15 => "burn",
            16 => "freeze",
            24 => "poison",
            25 => "curse",
            _ => "",
        };
        if (debuff.Length > 0)
            playerTarget.ActiveServerBuffs[debuff] = _gameTime + skill.Duracao;
    }

    private void BroadcastCombatResult(Channel channel, ulong attackerId, ulong targetId, int damage, bool isCrit, int targetHealth, int targetMaxHealth, float x, float y)
    {
        var writer = PacketSerializer.WritePacket(PacketId.S2C_CombatResult);
        writer.Put(attackerId);
        writer.Put(targetId);
        writer.Put(damage);
        writer.Put(isCrit);
        writer.Put(targetHealth);
        writer.Put(targetMaxHealth);

        var aoi = channel.GetEntitiesInAoi(x, y);
        foreach (var eid in aoi)
        {
            var p = channel.GetPlayerPeer(eid);
            if (p == null) continue;
            p.Send(writer, DeliveryMethod.ReliableOrdered);
            writer = PacketSerializer.WritePacket(PacketId.S2C_CombatResult);
            writer.Put(attackerId);
            writer.Put(targetId);
            writer.Put(damage);
            writer.Put(isCrit);
            writer.Put(targetHealth);
            writer.Put(targetMaxHealth);
        }
    }

    private void HandleAttack(NetPeer peer, NetDataReader reader)
    {
        if (!_sessions.TryGetValue(peer, out var session)) return;
        var channel = _world.GetChannel(session.ChannelId);
        if (channel == null) return;

        ulong targetId = reader.GetULong();
        int skillId = reader.GetInt();

        var attacker = channel.GetEntity(session.EntityId) as PlayerEntity;
        if (attacker == null || attacker.Health <= 0) return;

        var target = channel.GetEntity(targetId);
        if (target == null || target.Health <= 0) return;

        float dx = target.X - attacker.X;
        float dy = target.Y - attacker.Y;
        float dist = MathF.Sqrt(dx * dx + dy * dy);
        float attackRange = 640f;

        if (dist > attackRange) return;

        if (target is MonsterEntity mob && mob.FactionId == attacker.FactionId) return;

        int targetDefense = target switch
        {
            PlayerEntity p => p.CalculateDefense(),
            MonsterEntity m => m.CalculateDefense(),
            _ => 0,
        };
        float defReduction = MathF.Min(0.80f, targetDefense / (targetDefense + 400f));
        bool isCrit = Random.Shared.Next(100) < attacker.Destreza / 4;
        int rawDamage = attacker.CalculateAttackDamage();
        int damage = Math.Max(1, (int)(rawDamage * (1f - defReduction)));
        if (isCrit) damage = (int)(damage * 1.5f);

        target.Health -= damage;
        if (target.Health < 0) target.Health = 0;

        attacker.LastCombatTime = _gameTime;
        if (target is PlayerEntity)
        {
            target.LastCombatTime = _gameTime;
            BroadcastPartyMemberUpdateForEntity(target.Id);
        }

        if (target is MonsterEntity hitMob)
        {
            hitMob.TargetEntityId = session.EntityId;
        }

        var writerCombat = PacketSerializer.WritePacket(PacketId.S2C_CombatResult);
        writerCombat.Put(session.EntityId);
        writerCombat.Put(targetId);
        writerCombat.Put(damage);
        writerCombat.Put(isCrit);
        writerCombat.Put(target.Health);
        writerCombat.Put(target.MaxHealth);

        var aoi = channel.GetEntitiesInAoi(attacker.X, attacker.Y);
        foreach (var eid in aoi)
        {
            var p = channel.GetPlayerPeer(eid);
            if (p != null)
            {
                p.Send(writerCombat, DeliveryMethod.ReliableOrdered);
                writerCombat = PacketSerializer.WritePacket(PacketId.S2C_CombatResult);
                writerCombat.Put(session.EntityId);
                writerCombat.Put(targetId);
                writerCombat.Put(damage);
                writerCombat.Put(isCrit);
                writerCombat.Put(target.Health);
                writerCombat.Put(target.MaxHealth);
            }
        }

        if (target is MonsterEntity killedMob && target.Health <= 0)
        {
            HandleMonsterDeath(channel, killedMob, attacker, session, targetId);
        }
    }

    private void HandleMonsterDeath(Channel channel, MonsterEntity mob, PlayerEntity killer, PlayerSession killerSession, ulong mobId)
    {
        int xpReward = mob.ExperienceReward;
        if (IsPlayerVip(killer))
            xpReward *= 2;

        var writerDied = PacketSerializer.WritePacket(PacketId.S2C_EntityDied);
        writerDied.Put(mobId);
        writerDied.Put(killer.Id);

        var writerGainExp = PacketSerializer.WritePacket(PacketId.S2C_GainExp);
        writerGainExp.Put(killer.Id);
        writerGainExp.Put(xpReward);
        writerGainExp.Put(killer.Experience);

        var aoi = channel.GetEntitiesInAoi(mob.X, mob.Y);
        foreach (var eid in aoi)
        {
            var p = channel.GetPlayerPeer(eid);
            if (p != null)
            {
                p.Send(writerDied, DeliveryMethod.ReliableOrdered);
                writerDied = PacketSerializer.WritePacket(PacketId.S2C_EntityDied);
                writerDied.Put(mobId);
                writerDied.Put(killer.Id);
            }
        }

        killer.Experience += xpReward;
        var writerExp = PacketSerializer.WritePacket(PacketId.S2C_GainExp);
        writerExp.Put(killer.Id);
        writerExp.Put(xpReward);
        writerExp.Put(killer.Experience);
        foreach (var eid in aoi)
        {
            var p = channel.GetPlayerPeer(eid);
            if (p != null)
            {
                p.Send(writerExp, DeliveryMethod.ReliableOrdered);
                writerExp = PacketSerializer.WritePacket(PacketId.S2C_GainExp);
                writerExp.Put(killer.Id);
                writerExp.Put(xpReward);
                writerExp.Put(killer.Experience);
            }
        }

        long xpForNextLevel = XpForNextLevel(killer.Level);
        while (killer.Experience >= xpForNextLevel)
        {
            killer.Experience -= xpForNextLevel;
            killer.Level++;
            xpForNextLevel = XpForNextLevel(killer.Level);

            killer.MaxHealth = 80 + killer.Forca * 2 + killer.Level * 10;
            killer.Health = killer.MaxHealth;
            killer.StatPoints += 5;

            var writerLevelUp = PacketSerializer.WritePacket(PacketId.S2C_LevelUp);
            writerLevelUp.Put(killer.Id);
            writerLevelUp.Put(killer.Level);
            writerLevelUp.Put((int)Math.Min(int.MaxValue, killer.Experience));
            foreach (var eid in aoi)
            {
                var p = channel.GetPlayerPeer(eid);
                if (p != null)
                {
                    p.Send(writerLevelUp, DeliveryMethod.ReliableOrdered);
                    writerLevelUp = PacketSerializer.WritePacket(PacketId.S2C_LevelUp);
                    writerLevelUp.Put(killer.Id);
                    writerLevelUp.Put(killer.Level);
                    writerLevelUp.Put((int)Math.Min(int.MaxValue, killer.Experience));
                }
            }

            var peer = channel.GetPlayerPeer(killer.Id);
            if (peer != null)
            {
                SendSystemMessage(peer, $"Parabéns! Você alcançou o nível {killer.Level}!");
                SendStatUpdate(peer, killer);
                SendTalentData(peer, killer);
            }
        }

        _db.SaveCharacterXp(killerSession.SelectedCharacter!.Id, killer.Experience);
        _db.SaveCharacterLevel(killerSession.SelectedCharacter.Id, killer.Level);
        _db.SaveCharacterStats(killerSession.SelectedCharacter.Id, killer.BaseForca, killer.BaseAgilidade, killer.BaseDestreza, killer.BaseInteligencia, killer.StatPoints);
        killerSession.SelectedCharacter.Xp = killer.Experience;
        killerSession.SelectedCharacter.Level = killer.Level;
        killerSession.SelectedCharacter.StatPoints = killer.StatPoints;

        UpdateQuestKillProgress(killer, mob.PrefabId);

        SpawnMonsterLoot(channel, mob, killer, aoi);

        // Libera primeiro a vaga no spot. Assim um elite gerado pelo contador
        // de mortes ocupa essa vaga sem ultrapassar o MaxCount do spot.
        channel.RemoveEntity(mobId);
        channel.HandleMonsterKilled(mob, _gameTime);

        if (mob.IsBoss && string.Equals(mob.PrefabId, "slimeBoss", StringComparison.OrdinalIgnoreCase))
        {
            SendGlobalChat(
                "Sistema",
                $"{killer.Name} derrotou o Boss Slime! Ele nascerá novamente em 1 hora.");
        }

    }

    private void SpawnMonsterLoot(Channel channel, MonsterEntity mob, PlayerEntity killer, HashSet<ulong> aoi)
    {
        var template = channel.Spawner.GetTemplate(mob.PrefabId);
        if (template == null) return;

        var rng = Random.Shared;
        bool vipDrop = IsPlayerVip(killer);

        int goldAmount = 0;
        if (template.GoldMax > 0)
            goldAmount = rng.Next(template.GoldMin, template.GoldMax + 1);

        var spawnedLoot = new List<LootEntity>();

        foreach (var entry in template.LootTable)
        {
            double chance = vipDrop ? Math.Min(1.0, entry.DropChance * 2.0) : entry.DropChance;
            if (rng.NextDouble() >= chance) continue;
            int qty = entry.MinQuantity == entry.MaxQuantity
                ? entry.MinQuantity
                : rng.Next(entry.MinQuantity, entry.MaxQuantity + 1);

            var offsetX = (float)(rng.NextDouble() - 0.5) * 40f;
            var offsetY = (float)(rng.NextDouble() - 0.5) * 40f;

            var loot = new LootEntity(mob.X + offsetX, mob.Y + offsetY, entry.ItemId, qty, killer.Id, _gameTime);
            spawnedLoot.Add(loot);
            channel.AddLoot(loot);
        }

        bool dropsEquipment = template.DropsNormalEquipment || template.DropsEliteEquipment;
        if (dropsEquipment)
        {
            int equipmentLevel = mob.Level < 10 ? 1 : Math.Min(100, (mob.Level / 10) * 10);
            bool eliteItem = template.DropsEliteEquipment;
            var equipmentPool = ItemDefinitions.GetAll()
                .Where(def => def.IsElite == eliteItem
                    && def.RequiredLevel == equipmentLevel
                    && def.Type is >= ItemType.Helmet and <= ItemType.Shield)
                .ToArray();

            if (equipmentPool.Length > 0)
            {
                var equipment = equipmentPool[rng.Next(equipmentPool.Length)];
                var loot = new LootEntity(
                    mob.X + (float)(rng.NextDouble() - 0.5) * 40f,
                    mob.Y + (float)(rng.NextDouble() - 0.5) * 40f,
                    equipment.Id,
                    1,
                    killer.Id,
                    _gameTime);
                spawnedLoot.Add(loot);
                channel.AddLoot(loot);
                Logger.Info($"Drop: {mob.Name} gerou {equipment.Name} ({(eliteItem ? "Elite" : "Normal")}, nivel {equipmentLevel}).");
            }
            else
            {
                Logger.Info($"Drop: nenhum equipamento {(eliteItem ? "Elite" : "Normal")} de nivel {equipmentLevel} encontrado para {mob.Name}.");
            }
        }

        if (goldAmount > 0)
        {
            var goldLoot = new LootEntity(mob.X, mob.Y, 0, goldAmount, killer.Id, _gameTime);
            spawnedLoot.Add(goldLoot);
            channel.AddLoot(goldLoot);
        }

        foreach (var loot in spawnedLoot)
        {
            var w = PacketSerializer.WritePacket(PacketId.S2C_LootSpawn);
            w.Put(loot.Id);
            w.Put(loot.X);
            w.Put(loot.Y);
            w.Put(loot.ItemId);
            w.Put(loot.Quantity);

            foreach (var eid in aoi)
            {
                var p = channel.GetPlayerPeer(eid);
                p?.Send(w, DeliveryMethod.ReliableOrdered);
                w = PacketSerializer.WritePacket(PacketId.S2C_LootSpawn);
                w.Put(loot.Id);
                w.Put(loot.X);
                w.Put(loot.Y);
                w.Put(loot.ItemId);
                w.Put(loot.Quantity);
            }
        }
    }

    private void HandleLootPickup(NetPeer peer, NetDataReader reader)
    {
        if (!_sessions.TryGetValue(peer, out var session) || session.ChannelId < 0) return;
        var character = session.SelectedCharacter;
        if (character == null) return;

        ulong lootId = reader.GetULong();
        var channel = _world.GetChannel(session.ChannelId);
        if (channel == null) return;

        var player = channel.GetEntity(session.EntityId) as PlayerEntity;
        if (player == null) return;

        var loot = channel.GetLoot(lootId);
        if (loot == null || loot.PickedUp) return;

        float dx = player.X - loot.X;
        float dy = player.Y - loot.Y;
        if (MathF.Sqrt(dx * dx + dy * dy) > 80f) return;

        if (loot.ItemId == 0)
        {
            player.Gold += loot.Quantity;
            if (session.SelectedCharacter != null)
                _db.SaveCharacterGold(session.SelectedCharacter.Id, player.Gold);
            SendGoldUpdate(peer, player.Gold);
        }
        else
        {
            bool itemAdded = TryAddItemToInventory(player, character.Id, loot.ItemId, loot.Quantity);

            if (!itemAdded)
            {
                SendSystemMessage(peer, "Invent?rio cheio!");
                SendInventoryData(peer, player);
                return;
            }
            /* else if (false)
            {
                var newItem = new ItemInstance { Slot = slot, ItemId = loot.ItemId, Quantity = loot.Quantity };
                player.Items.Add(newItem);
                _db.SaveItem(character.Id, newItem);
                var wUpdate = PacketSerializer.WritePacket(PacketId.S2C_ItemUpdate);
                wUpdate.Put(slot);
                wUpdate.Put(loot.ItemId);
                wUpdate.Put(loot.Quantity);
                wUpdate.Put(0);
                peer.Send(wUpdate, DeliveryMethod.ReliableOrdered);
            }
            else if (false)
            {
                SendSystemMessage(peer, "Inventário cheio!");
                return;
            }
            */
        }

        SendInventoryData(peer, player);

        loot.PickedUp = true;
        channel.RemoveLoot(lootId);

        var aoi = channel.GetEntitiesInAoi(loot.X, loot.Y);
        var wDespawn = PacketSerializer.WritePacket(PacketId.S2C_LootDespawn);
        wDespawn.Put(lootId);
        foreach (var eid in aoi)
        {
            var p = channel.GetPlayerPeer(eid);
            if (p != null)
            {
                p.Send(wDespawn, DeliveryMethod.ReliableOrdered);
                wDespawn = PacketSerializer.WritePacket(PacketId.S2C_LootDespawn);
                wDespawn.Put(lootId);
            }
        }
    }

    private void SpawnTestPotion(NetPeer peer, Channel channel, Entity sender)
    {
        var offsetX = (float)(Random.Shared.NextDouble() - 0.5) * 60f;
        var offsetY = (float)(Random.Shared.NextDouble() - 0.5) * 60f;
        var loot = new LootEntity(sender.X + offsetX, sender.Y + offsetY, 1, 1, sender.Id, _gameTime);
        channel.AddLoot(loot);

        var aoi = channel.GetEntitiesInAoi(loot.X, loot.Y);
        var w = PacketSerializer.WritePacket(PacketId.S2C_LootSpawn);
        w.Put(loot.Id);
        w.Put(loot.X);
        w.Put(loot.Y);
        w.Put(loot.ItemId);
        w.Put(loot.Quantity);
        foreach (var eid in aoi)
        {
            var p = channel.GetPlayerPeer(eid);
            p?.Send(w, DeliveryMethod.ReliableOrdered);
            w = PacketSerializer.WritePacket(PacketId.S2C_LootSpawn);
            w.Put(loot.Id);
            w.Put(loot.X);
            w.Put(loot.Y);
            w.Put(loot.ItemId);
            w.Put(loot.Quantity);
        }

        SendSystemMessage(peer, "Poção de vida spawnada! Aproxime e aperte F para pegar.");
    }
}

internal sealed class ServerSkillDefinition
{
    public int SkillId { get; init; }
    public string Nome { get; init; } = "";
    public int Valor { get; init; }
    public int CustoMana { get; init; }
    public string ClasseRestrita { get; init; } = "";
    public float Cooldown { get; init; }
    public float Duracao { get; init; }
    public int TargetType { get; init; }
    public int EffectType { get; init; }
    public int NivelRequerido { get; init; } = 1;
    public string Tipo { get; init; } = "";
    public string DanoEscala { get; init; } = "";
    public string BuffDebuff { get; init; } = "";
    public string EfeitoPrincipal { get; init; } = "";
    public bool IsArea { get; init; }
    public int MaxTargets { get; init; } = 1;
    public float DamageMultiplier { get; init; } = 1f;
    public int FlatPower { get; init; }
}

internal static class ServerSkillCatalog
{
    private static readonly Lazy<Dictionary<int, ServerSkillDefinition>> Skills = new(LoadAll);

    public static ServerSkillDefinition? Get(int skillId)
    {
        Skills.Value.TryGetValue(skillId, out var skill);
        return skill;
    }

    public static bool ClassMatches(string playerClass, string requiredClass)
    {
        if (string.IsNullOrWhiteSpace(requiredClass) || requiredClass.Equals("Todas", StringComparison.OrdinalIgnoreCase))
            return true;

        static string Normalize(string value)
        {
            string formD = value.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(formD.Length);
            foreach (char c in formD)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                    sb.Append(char.ToLowerInvariant(c));
            }
            return sb.ToString().Normalize(NormalizationForm.FormC).Replace(" ", "");
        }

        string player = Normalize(playerClass);
        string required = Normalize(requiredClass);
        if (player == required) return true;
        if (player == "prist" && required == "clerigo") return true;
        if (player == "clerigo" && required == "prist") return true;
        if (player == "berseker" && required == "berserker") return true;
        if (player == "berserker" && required == "berseker") return true;
        if (player == "assassino" && required == "ladino") return true;
        return false;
    }

    private static Dictionary<int, ServerSkillDefinition> LoadAll()
    {
        var result = new Dictionary<int, ServerSkillDefinition>();
        string? root = FindProjectRoot();
        if (root == null)
        {
            Logger.Info("SkillCatalog: raiz do projeto não encontrada; catálogo vazio.");
            return result;
        }

        string skillsDir = Path.Combine(root, "skills", "habilidades");
        if (!Directory.Exists(skillsDir))
        {
            Logger.Info($"SkillCatalog: pasta não encontrada: {skillsDir}");
            return result;
        }

        foreach (string path in Directory.EnumerateFiles(skillsDir, "*.tres", SearchOption.AllDirectories))
        {
            try
            {
                string text = File.ReadAllText(path);
                int skillId = GetInt(text, "SkillId");
                if (skillId <= 0) continue;

                string tipo = DecodeTresText(GetString(text, "Tipo"));
                string efeito = DecodeTresText(GetString(text, "EfeitoPrincipal"));
                string danoEscala = DecodeTresText(GetString(text, "DanoEscala"));
                int valor = GetInt(text, "Valor");
                float multiplier = GetDamageMultiplier(danoEscala, valor);
                bool isArea = IsAreaSkill(tipo, efeito);

                result[skillId] = new ServerSkillDefinition
                {
                    SkillId = skillId,
                    Nome = DecodeTresText(GetString(text, "Nome")),
                    Valor = valor,
                    CustoMana = GetInt(text, "CustoMana"),
                    ClasseRestrita = DecodeTresText(GetString(text, "ClasseRestrita")),
                    Cooldown = GetFloat(text, "Cooldown"),
                    Duracao = GetFloat(text, "Duracao"),
                    TargetType = GetInt(text, "TargetType"),
                    EffectType = GetInt(text, "EffectType"),
                    NivelRequerido = Math.Max(1, GetInt(text, "NivelRequerido")),
                    Tipo = tipo,
                    EfeitoPrincipal = efeito,
                    DanoEscala = danoEscala,
                    BuffDebuff = DecodeTresText(GetString(text, "BuffDebuff")),
                    IsArea = isArea,
                    MaxTargets = isArea ? 6 : Math.Max(1, CountFromText(efeito)),
                    DamageMultiplier = multiplier,
                    FlatPower = valor > 0 && multiplier < 0.01f ? valor : 0,
                };
            }
            catch (Exception ex)
            {
                Logger.Info($"SkillCatalog: falha ao ler {path}: {ex.Message}");
            }
        }

        Logger.Info($"SkillCatalog: {result.Count} skills carregadas de {skillsDir}.");
        return result;
    }

    private static string? FindProjectRoot()
    {
        var candidates = new[]
        {
            Directory.GetCurrentDirectory(),
            AppContext.BaseDirectory,
        };

        foreach (string start in candidates)
        {
            var dir = new DirectoryInfo(start);
            while (dir != null)
            {
                if (Directory.Exists(Path.Combine(dir.FullName, "skills", "habilidades")))
                    return dir.FullName;
                dir = dir.Parent;
            }
        }

        return null;
    }

    private static string GetString(string text, string key)
    {
        var match = Regex.Match(text, $@"(?m)^{Regex.Escape(key)}\s*=\s*""(?<v>(?:\\""|[^""])*)""");
        return match.Success ? match.Groups["v"].Value.Replace("\\\"", "\"") : "";
    }

    private static int GetInt(string text, string key)
    {
        var match = Regex.Match(text, $@"(?m)^{Regex.Escape(key)}\s*=\s*(?<v>-?\d+)");
        return match.Success && int.TryParse(match.Groups["v"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)
            ? value
            : 0;
    }

    private static float GetFloat(string text, string key)
    {
        var match = Regex.Match(text, $@"(?m)^{Regex.Escape(key)}\s*=\s*(?<v>-?\d+(?:\.\d+)?)");
        return match.Success && float.TryParse(match.Groups["v"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out float value)
            ? value
            : 0f;
    }

    private static string DecodeTresText(string text)
    {
        if (string.IsNullOrEmpty(text)) return "";
        try
        {
            byte[] bytes = Encoding.GetEncoding(1252).GetBytes(text);
            string decoded = Encoding.UTF8.GetString(bytes);
            return decoded.Contains('\uFFFD') ? text : decoded;
        }
        catch
        {
            return text;
        }
    }

    private static bool IsAreaSkill(string tipo, string efeito)
    {
        string text = $"{tipo} {efeito}".ToLowerInvariant();
        return text.Contains("área") || text.Contains("area") || text.Contains("multi") || text.Contains("chuva") || text.Contains("explos");
    }

    private static int CountFromText(string text)
    {
        var match = Regex.Match(text, @"\b(?<n>[2-9])\b");
        return match.Success ? int.Parse(match.Groups["n"].Value, CultureInfo.InvariantCulture) : 1;
    }

    private static float GetDamageMultiplier(string danoEscala, int valor)
    {
        var match = Regex.Match(danoEscala.Replace(',', '.'), @"(?<n>\d+(?:\.\d+)?)\s*%");
        if (match.Success && float.TryParse(match.Groups["n"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out float pct))
            return MathF.Max(0.05f, pct / 100f);
        return valor > 0 ? MathF.Max(0.05f, valor / 100f) : 1f;
    }
}
