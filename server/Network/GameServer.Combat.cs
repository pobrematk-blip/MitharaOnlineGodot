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
    private const double DefaultBasicAttackCooldown = 1.2;
    private const double BerserkerBasicAttackCooldown = 1.5;
    private const double RangedBasicAttackCooldown = 1.1;
    private const int PetAttackSkillId = -100;
    private const float BasicAttackRange = 640f;
    private const float PetOwnerCommandRange = 32f * 12f;
    private const double PetAttackCooldown = 0.8;

    private static long XpForNextLevel(int level)
    {
        if (level < 1) level = 1;
        return 600L + level * 260L + level * level * 90L;
    }

    private static double GetBasicAttackCooldown(PlayerEntity player)
    {
        string classe = player.CharacterClass?.Trim().ToLowerInvariant() ?? "";
        return classe switch
        {
            "berseker" or "berserker" or "bárbaro" or "barbaro" => BerserkerBasicAttackCooldown,
            "arqueiro" or "mago" => RangedBasicAttackCooldown,
            _ => DefaultBasicAttackCooldown,
        };
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
        player.Mana = player.MaxMana;
        player.LastCombatTime = _gameTime - 120.0;

        var character = session.SelectedCharacter;
        float spawnX = MainSpawnX;
        float spawnY = MainSpawnY;

        float oldX = player.X;
        float oldY = player.Y;
        channel.MoveEntity(player.Id, spawnX, spawnY);
        session.CurrentMap = MainSceneName;

        if (character != null)
        {
            character.PosX = spawnX;
            character.PosY = spawnY;
            character.CurrentMap = MainSceneName;
            _db.SaveCharacterPosition(character.Id, spawnX, spawnY, MainSceneName);
        }

        var writer = PacketSerializer.WritePacket(PacketId.S2C_Respawn);
        writer.Put(player.Id);
        writer.Put(spawnX);
        writer.Put(spawnY);
        writer.Put(player.Health);
        writer.Put(player.MaxHealth);
        writer.Put(player.Mana);
        writer.Put(player.MaxMana);

        var aoi = channel.GetEntitiesInAoi(spawnX, spawnY);
        aoi.UnionWith(channel.GetEntitiesInAoi(oldX, oldY));
        BroadcastPartyMemberUpdateForEntity(player.Id);
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
                writer.Put(player.Mana);
                writer.Put(player.MaxMana);
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
        if (MathF.Sqrt(dx * dx + dy * dy) > 120f)
        {
            SendSystemMessage(peer, "Chegue mais perto para pegar este item.");
            return;
        }

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
        target.Mana = target.MaxMana;
        target.LastCombatTime = _gameTime - 120.0;

        var writer = PacketSerializer.WritePacket(PacketId.S2C_Respawn);
        writer.Put(target.Id);
        writer.Put(target.X);
        writer.Put(target.Y);
        writer.Put(target.Health);
        writer.Put(target.MaxHealth);
        writer.Put(target.Mana);
        writer.Put(target.MaxMana);

        var aoi = channel.GetEntitiesInAoi(target.X, target.Y);
        BroadcastPartyMemberUpdateForEntity(target.Id);
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
                writer.Put(target.Mana);
                writer.Put(target.MaxMana);
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

        if (!ServerTalentCatalog.IsSkillUnlockedForPlayer(caster.CharacterClass, caster.UnlockedTalents, skill.SkillId))
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
        if (effect == 2)
            return ApplyDashSkill(peer, channel, caster, session, skill, targetX, targetY);

        if (IsProjectileSkill(caster, skill))
            return ApplyProjectileSkill(peer, channel, caster, session, skill, targetX, targetY);

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

    private bool ApplyDashSkill(NetPeer peer, Channel channel, PlayerEntity caster, PlayerSession session, ServerSkillDefinition skill, float targetX, float targetY)
    {
        float dirX = caster.DirX;
        float dirY = caster.DirY;
        float len = MathF.Sqrt(dirX * dirX + dirY * dirY);
        if (len < 0.001f)
        {
            dirX = targetX - caster.X;
            dirY = targetY - caster.Y;
            len = MathF.Sqrt(dirX * dirX + dirY * dirY);
        }
        if (len < 0.001f)
        {
            dirX = 0f;
            dirY = 1f;
            len = 1f;
        }

        dirX /= len;
        dirY /= len;

        const float tileSize = 32f;
        float distance = skill.SkillId == 10002 ? tileSize * 5f : MathF.Max(tileSize * 2f, skill.Valor);
        float originX = caster.X + dirX * 42f;
        float originY = caster.Y + dirY * 42f;

        if (skill.SkillId == 10002)
            FireSkillProjectile(channel, caster, session, skill, originX, originY, dirX, dirY, notifyMiss: false, peer: peer);

        float newX = caster.X - dirX * distance;
        float newY = caster.Y - dirY * distance;
        caster.Moving = false;
        caster.Sprinting = false;
        channel.MoveEntity(caster.Id, newX, newY);
        BroadcastAuthoritativeMove(channel, caster, newX, newY, dirX, dirY);
        return true;
    }

    private void BroadcastAuthoritativeMove(Channel channel, PlayerEntity entity, float x, float y, float dirX, float dirY)
    {
        var aoi = channel.GetEntitiesInAoi(x, y);
        foreach (var eid in aoi)
        {
            var targetPeer = channel.GetPlayerPeer(eid);
            if (targetPeer == null)
                continue;

            var writer = PacketSerializer.WritePacket(PacketId.S2C_EntityMove);
            writer.Put(entity.Id);
            writer.Put(x);
            writer.Put(y);
            writer.Put(dirX);
            writer.Put(dirY);
            writer.Put(false);
            writer.Put(false);
            targetPeer.Send(writer, DeliveryMethod.ReliableOrdered);
        }
    }

    private bool ApplyProjectileSkill(NetPeer peer, Channel channel, PlayerEntity caster, PlayerSession session, ServerSkillDefinition skill, float targetX, float targetY)
    {
        float dirX = targetX - caster.X;
        float dirY = targetY - caster.Y;
        float len = MathF.Sqrt(dirX * dirX + dirY * dirY);
        if (len < 0.001f)
        {
            dirX = caster.DirX;
            dirY = caster.DirY;
            len = MathF.Sqrt(dirX * dirX + dirY * dirY);
        }
        if (len < 0.001f)
        {
            SendSystemMessage(peer, "Vire para uma direcao antes de usar esta habilidade.");
            return false;
        }

        dirX /= len;
        dirY /= len;

        const float originOffset = 42f;
        float originX = caster.X + dirX * originOffset;
        float originY = caster.Y + dirY * originOffset;

        return FireSkillProjectile(channel, caster, session, skill, originX, originY, dirX, dirY, notifyMiss: true, peer: peer);
    }

    private bool FireSkillProjectile(Channel channel, PlayerEntity caster, PlayerSession session, ServerSkillDefinition skill, float originX, float originY, float dirX, float dirY, bool notifyMiss, NetPeer? peer)
    {
        const float projectileRange = 760f;
        const float projectileRadius = 34f;
        const float projectileSpeed = 450f;

        BroadcastProjectileSpawn(channel, caster.Id, originX, originY, dirX, dirY, ProjectileTypeForSkill(caster, skill), includeCaster: true);

        var target = FindFirstProjectileHit(channel, caster, originX, originY, dirX, dirY, projectileRange, projectileRadius);
        if (target == null)
        {
            if (notifyMiss && peer != null)
                SendSystemMessage(peer, $"{skill.Nome}: projetil disparado, mas nao acertou nenhum alvo.");
            return true;
        }

        float along = ProjectileHitDistance(originX, originY, dirX, dirY, target.X, target.Y, projectileRange).Along;
        double impactAt = _gameTime + Math.Clamp(along / projectileSpeed, 0.05f, 1.8f);
        _pendingProjectileHits.Add(new PendingProjectileHit
        {
            ImpactAt = impactAt,
            ChannelId = session.ChannelId,
            CasterId = caster.Id,
            TargetId = target.Id,
            Skill = skill,
        });

        return true;
    }

    private void ProcessPendingProjectileHits()
    {
        for (int i = _pendingProjectileHits.Count - 1; i >= 0; i--)
        {
            var pending = _pendingProjectileHits[i];
            if (pending.ImpactAt > _gameTime)
                continue;

            _pendingProjectileHits.RemoveAt(i);
            var channel = _world.GetChannel(pending.ChannelId);
            if (channel == null)
                continue;

            if (channel.GetEntity(pending.CasterId) is not PlayerEntity caster || caster.Health <= 0)
                continue;
            var target = channel.GetEntity(pending.TargetId);
            if (target == null || target.Health <= 0)
                continue;

            var session = _sessions.Values.FirstOrDefault(s => s.EntityId == pending.CasterId);
            if (session == null || session.SelectedCharacter == null)
                continue;

            int damage = CalculateSkillDamage(caster, target, pending.Skill, out bool isCrit);
            target.Health = Math.Max(0, target.Health - damage);
            if (target is PlayerEntity playerTarget)
            {
                playerTarget.LastCombatTime = _gameTime;
                BroadcastPartyMemberUpdateForEntity(playerTarget.Id);
            }
            if (target is MonsterEntity hitMob)
                hitMob.TargetEntityId = caster.Id;

            BroadcastCombatResult(channel, caster.Id, target.Id, damage, isCrit, target.Health, target.MaxHealth, caster.X, caster.Y);
            ApplySkillDebuff(target, pending.Skill);

            if (target is MonsterEntity killedMob && target.Health <= 0)
                HandleMonsterDeath(channel, killedMob, caster, session, target.Id);
        }
    }

    private Entity? FindFirstProjectileHit(Channel channel, PlayerEntity caster, float originX, float originY, float dirX, float dirY, float range, float radius)
    {
        return channel.GetEntitiesInAoi(caster.X, caster.Y)
            .Select(id => channel.GetEntity(id))
            .Where(e => e != null && e.Health > 0 && e.Id != caster.Id)
            .Where(e => e is MonsterEntity mob && mob.FactionId != caster.FactionId || e is PlayerEntity p && p.FactionId != caster.FactionId)
            .Select(e => new { Entity = e!, Hit = ProjectileHitDistance(originX, originY, dirX, dirY, e!.X, e.Y, range) })
            .Where(x => x.Hit.Along >= 0f && x.Hit.Along <= range && x.Hit.Perpendicular <= radius)
            .OrderBy(x => x.Hit.Along)
            .Select(x => x.Entity)
            .FirstOrDefault();
    }

    private static (float Along, float Perpendicular) ProjectileHitDistance(float originX, float originY, float dirX, float dirY, float targetX, float targetY, float range)
    {
        float relX = targetX - originX;
        float relY = targetY - originY;
        float along = relX * dirX + relY * dirY;
        float clampedAlong = MathF.Max(0f, MathF.Min(range, along));
        float closestX = originX + dirX * clampedAlong;
        float closestY = originY + dirY * clampedAlong;
        float perpX = targetX - closestX;
        float perpY = targetY - closestY;
        return (along, MathF.Sqrt(perpX * perpX + perpY * perpY));
    }

    private static bool IsProjectileSkill(PlayerEntity caster, ServerSkillDefinition skill)
    {
        if (skill.EffectType is not (7 or 8 or 9 or 12 or 13 or 15 or 16 or 24 or 25))
            return false;

        string cls = caster.CharacterClass.ToLowerInvariant();
        if (cls.Contains("arqueiro") || cls.Contains("mago"))
            return !skill.IsArea;

        string skillClass = skill.ClasseRestrita.ToLowerInvariant();
        return (skillClass.Contains("arqueiro") || skillClass.Contains("mago")) && !skill.IsArea;
    }

    private static byte ProjectileTypeForSkill(PlayerEntity caster, ServerSkillDefinition skill)
    {
        string cls = caster.CharacterClass.ToLowerInvariant();
        return cls.Contains("mago") ? (byte)1 : (byte)0;
    }

    private void BroadcastProjectileSpawn(Channel channel, ulong entityId, float originX, float originY, float dirX, float dirY, byte projectileType, bool includeCaster)
    {
        var nearby = channel.GetEntitiesInAoi(originX, originY);
        foreach (var eid in nearby)
        {
            if (!includeCaster && eid == entityId)
                continue;

            var targetPeer = channel.GetPlayerPeer(eid);
            if (targetPeer == null) continue;

            var writer = PacketSerializer.WritePacket(PacketId.S2C_ProjectileSpawn);
            writer.Put(entityId);
            writer.Put(originX);
            writer.Put(originY);
            writer.Put(dirX);
            writer.Put(dirY);
            writer.Put(projectileType);
            targetPeer.Send(writer, DeliveryMethod.ReliableOrdered);
        }
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

        string buffId = $"skill:{skill.SkillId}";
        target.ActiveServerBuffs[buffId] = _gameTime + Math.Max(1f, skill.Duracao);
        if (skill.SkillId == 10202)
        {
            target.TemporaryPrecisionBonus = 15f;
            target.TemporaryCritChanceBonus = 15f;
        }

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
        float minMultiplier = skill.DamageMultiplierMin > 0 ? skill.DamageMultiplierMin : skill.DamageMultiplier;
        float maxMultiplier = skill.DamageMultiplierMax > 0 ? skill.DamageMultiplierMax : minMultiplier;
        if (maxMultiplier < minMultiplier)
            (minMultiplier, maxMultiplier) = (maxMultiplier, minMultiplier);
        float multiplier = maxMultiplier > minMultiplier
            ? minMultiplier + (float)Random.Shared.NextDouble() * (maxMultiplier - minMultiplier)
            : minMultiplier;
        if (multiplier <= 0f)
            multiplier = 1f;
        int rawDamage = Math.Max(1, (int)(baseDamage * multiplier) + skill.FlatPower);
        int targetDefense = target switch
        {
            PlayerEntity p => p.CalculateDefense(),
            MonsterEntity m => m.CalculateDefense(),
            _ => 0,
        };
        float defReduction = MathF.Min(0.80f, targetDefense / (targetDefense + 400f));
        isCrit = Random.Shared.NextDouble() * 100.0 < GetCritChance(caster);
        int damage = Math.Max(1, (int)(rawDamage * (1f - defReduction)));
        if (isCrit) damage = (int)(damage * 1.5f);
        return damage;
    }

    private void ApplySkillDebuff(Entity target, ServerSkillDefinition skill)
    {
        if (skill.Duracao <= 0) return;
        string debuff = skill.EffectType switch
        {
            8 => "stun",
            9 => "bleed",
            12 => "slow",
            13 => "root",
            15 => "burn",
            16 => "freeze",
            19 => "sleep",
            20 => "prison",
            24 => "poison",
            25 => "curse",
            _ => "",
        };
        if (debuff.Length == 0)
            return;

        if (target is PlayerEntity playerTarget)
            playerTarget.ActiveServerBuffs[debuff] = _gameTime + skill.Duracao;
        else if (target is MonsterEntity mobTarget)
            mobTarget.ActiveServerBuffs[debuff] = _gameTime + skill.Duracao;
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
        bool isPetAttack = skillId == PetAttackSkillId;
        float attackRange = isPetAttack ? PetOwnerCommandRange : BasicAttackRange;

        if (dist > attackRange) return;

        if (target is MonsterEntity mob && mob.FactionId == attacker.FactionId) return;
        if (isPetAttack && target is not MonsterEntity) return;

        if (isPetAttack)
        {
            if (_gameTime < attacker.NextPetAttackTime)
                return;

            attacker.NextPetAttackTime = _gameTime + PetAttackCooldown;
        }
        else if (skillId == 0)
        {
            if (_gameTime < attacker.NextBasicAttackTime)
                return;

            attacker.NextBasicAttackTime = _gameTime + GetBasicAttackCooldown(attacker);
        }
        else if (skillId < 0)
        {
            return;
        }

        int targetDefense = target switch
        {
            PlayerEntity p => p.CalculateDefense(),
            MonsterEntity m => m.CalculateDefense(),
            _ => 0,
        };
        float defReduction = MathF.Min(0.80f, targetDefense / (targetDefense + 400f));
        bool isCrit = Random.Shared.NextDouble() * 100.0 < GetCritChance(attacker);
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

    private float GetCritChance(PlayerEntity player)
    {
        PruneExpiredServerBuffs(player);
        return Math.Clamp((player.Destreza / 4f) + player.TemporaryCritChanceBonus, 0f, 75f);
    }

    private void PruneExpiredServerBuffs(PlayerEntity player)
    {
        if (player.ActiveServerBuffs.TryGetValue("skill:10202", out double miraApuradaUntil) && miraApuradaUntil > _gameTime)
            return;

        if (player.TemporaryPrecisionBonus != 0f || player.TemporaryCritChanceBonus != 0f)
        {
            player.TemporaryPrecisionBonus = 0f;
            player.TemporaryCritChanceBonus = 0f;
        }
    }

    private void HandleMonsterDeath(Channel channel, MonsterEntity mob, PlayerEntity killer, PlayerSession killerSession, ulong mobId)
    {
        int xpReward = mob.ExperienceReward;
        if (IsPlayerVip(killer))
            xpReward *= 2;
        int partyBonusPercent = GetPartyXpBonusPercent(killer);
        if (partyBonusPercent > 0)
            xpReward = Math.Max(1, (int)Math.Round(xpReward * (1.0 + partyBonusPercent / 100.0)));

        var writerDied = PacketSerializer.WritePacket(PacketId.S2C_EntityDied);
        writerDied.Put(mobId);
        writerDied.Put(killer.Id);

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

            RecalculatePlayerStats(killer);
            killer.Health = killer.MaxHealth;
            killer.Mana = killer.MaxMana;
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
            BroadcastSingleEntityUpdate(channel, killer);
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

    private int GetPartyXpBonusPercent(PlayerEntity player)
    {
        if (player.PartyId < 0)
            return 0;

        var party = _world.Parties.GetParty(player.PartyId);
        if (party == null)
            return 0;

        return Math.Clamp(party.Members.Count * 5, 0, 25);
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
        double equipmentDropChance = template.DropsEliteEquipment
            ? template.EliteDropChance
            : Math.Clamp(template.EliteDropChance, 0.0, 1.0);
        if (dropsEquipment && rng.NextDouble() < equipmentDropChance)
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

        if (goldAmount > 0 && rng.NextDouble() < template.GoldDropChance)
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

        if (!CanPickupLoot(player, loot))
        {
            SendSystemMessage(peer, "Este item pertence a outro jogador ou party.");
            return;
        }

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
                SendSystemMessage(peer, "Inventario cheio!");
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

    private bool CanPickupLoot(PlayerEntity player, LootEntity loot)
    {
        if (loot.OwnerId == player.Id)
            return true;

        var playerPartyId = _world.Parties.GetPlayerPartyId(player.Id);
        if (!playerPartyId.HasValue)
            return false;

        var ownerPartyId = _world.Parties.GetPlayerPartyId(loot.OwnerId);
        return ownerPartyId.HasValue && ownerPartyId.Value == playerPartyId.Value;
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
    public int SkillId { get; set; }
    public string Nome { get; set; } = "";
    public int Valor { get; set; }
    public int CustoMana { get; set; }
    public string ClasseRestrita { get; set; } = "";
    public float Cooldown { get; set; }
    public float Duracao { get; set; }
    public int TargetType { get; set; }
    public int EffectType { get; set; }
    public int NivelRequerido { get; set; } = 1;
    public string Escopo { get; set; } = "";
    public string Tipo { get; set; } = "";
    public string DanoEscala { get; set; } = "";
    public string BuffDebuff { get; set; } = "";
    public string EfeitoPrincipal { get; set; } = "";
    public string DuracaoTexto { get; set; } = "";
    public string Progressao { get; set; } = "";
    public string Observacoes { get; set; } = "";
    public bool IsArea { get; set; }
    public int MaxTargets { get; set; } = 1;
    public float DamageMultiplier { get; set; } = 1f;
    public float DamageMultiplierMin { get; set; } = 1f;
    public float DamageMultiplierMax { get; set; } = 1f;
    public int FlatPower { get; set; }
}

internal sealed class PendingProjectileHit
{
    public double ImpactAt { get; init; }
    public int ChannelId { get; init; }
    public ulong CasterId { get; init; }
    public ulong TargetId { get; init; }
    public ServerSkillDefinition Skill { get; init; } = null!;
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

                string escopo = DecodeTresText(GetString(text, "Escopo"));
                string tipo = DecodeTresText(GetString(text, "Tipo"));
                string efeito = DecodeTresText(GetString(text, "EfeitoPrincipal"));
                string danoEscala = DecodeTresText(GetString(text, "DanoEscala"));
                int valor = GetInt(text, "Valor");
                var multiplierRange = GetDamageMultiplierRange(danoEscala, valor);
                bool isArea = IsAreaSkill(escopo, tipo, efeito);

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
                    Escopo = escopo,
                    Tipo = tipo,
                    EfeitoPrincipal = efeito,
                    DanoEscala = danoEscala,
                    BuffDebuff = DecodeTresText(GetString(text, "BuffDebuff")),
                    DuracaoTexto = DecodeTresText(GetString(text, "DuracaoTexto")),
                    Progressao = DecodeTresText(GetString(text, "Progressao")),
                    Observacoes = DecodeTresText(GetString(text, "Observacoes")),
                    IsArea = isArea,
                    MaxTargets = isArea ? 6 : Math.Max(1, CountFromText(efeito)),
                    DamageMultiplier = (multiplierRange.Min + multiplierRange.Max) * 0.5f,
                    DamageMultiplierMin = multiplierRange.Min,
                    DamageMultiplierMax = multiplierRange.Max,
                    FlatPower = valor > 0 && multiplierRange.Max < 0.01f ? valor : 0,
                };
            }
            catch (Exception ex)
            {
                Logger.Info($"SkillCatalog: falha ao ler {path}: {ex.Message}");
            }
        }

        ApplyFallbacks(result);
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

    private static void ApplyFallbacks(Dictionary<int, ServerSkillDefinition> skills)
    {
        foreach (var group in skills.Values
            .Where(s => !string.IsNullOrWhiteSpace(s.Nome))
            .GroupBy(s => NormalizeName(s.Nome)))
        {
            var template = group
                .OrderByDescending(ScoreCompleteness)
                .FirstOrDefault();
            if (template == null)
                continue;

            foreach (var skill in group)
                FillMissingFields(skill, template);
        }
    }

    private static int ScoreCompleteness(ServerSkillDefinition skill)
    {
        int score = 0;
        if (skill.Cooldown > 0) score += 10;
        if (skill.CustoMana > 0) score += 10;
        if (skill.Valor != 0) score += 8;
        if (skill.Duracao > 0) score += 6;
        if (!string.IsNullOrWhiteSpace(skill.DanoEscala)) score += 6;
        if (!string.IsNullOrWhiteSpace(skill.EfeitoPrincipal)) score += 5;
        if (!string.IsNullOrWhiteSpace(skill.BuffDebuff)) score += 4;
        return score;
    }

    private static void FillMissingFields(ServerSkillDefinition skill, ServerSkillDefinition template)
    {
        if (ReferenceEquals(skill, template))
            return;

        if (skill.Cooldown <= 0f && template.Cooldown > 0f) skill.Cooldown = template.Cooldown;
        if (skill.CustoMana <= 0 && template.CustoMana > 0) skill.CustoMana = template.CustoMana;
        if (skill.Valor == 0 && template.Valor != 0) skill.Valor = template.Valor;
        if (skill.Duracao <= 0f && template.Duracao > 0f) skill.Duracao = template.Duracao;
        if (string.IsNullOrWhiteSpace(skill.Escopo) && !string.IsNullOrWhiteSpace(template.Escopo)) skill.Escopo = template.Escopo;
        if (string.IsNullOrWhiteSpace(skill.Tipo) && !string.IsNullOrWhiteSpace(template.Tipo)) skill.Tipo = template.Tipo;
        if (string.IsNullOrWhiteSpace(skill.EfeitoPrincipal) && !string.IsNullOrWhiteSpace(template.EfeitoPrincipal)) skill.EfeitoPrincipal = template.EfeitoPrincipal;
        if (string.IsNullOrWhiteSpace(skill.DanoEscala) && !string.IsNullOrWhiteSpace(template.DanoEscala)) skill.DanoEscala = template.DanoEscala;
        if (string.IsNullOrWhiteSpace(skill.BuffDebuff) && !string.IsNullOrWhiteSpace(template.BuffDebuff)) skill.BuffDebuff = template.BuffDebuff;
        if (string.IsNullOrWhiteSpace(skill.DuracaoTexto) && !string.IsNullOrWhiteSpace(template.DuracaoTexto)) skill.DuracaoTexto = template.DuracaoTexto;
        if (string.IsNullOrWhiteSpace(skill.Progressao) && !string.IsNullOrWhiteSpace(template.Progressao)) skill.Progressao = template.Progressao;
        if (string.IsNullOrWhiteSpace(skill.Observacoes) && !string.IsNullOrWhiteSpace(template.Observacoes)) skill.Observacoes = template.Observacoes;
        if (skill.DamageMultiplierMin <= 0f && template.DamageMultiplierMin > 0f) skill.DamageMultiplierMin = template.DamageMultiplierMin;
        if (skill.DamageMultiplierMax <= 0f && template.DamageMultiplierMax > 0f) skill.DamageMultiplierMax = template.DamageMultiplierMax;
        if (skill.DamageMultiplier <= 0f && template.DamageMultiplier > 0f) skill.DamageMultiplier = template.DamageMultiplier;

        skill.IsArea = skill.IsArea || IsAreaSkill(skill.Escopo, skill.Tipo, skill.EfeitoPrincipal);
        skill.MaxTargets = skill.IsArea ? Math.Max(skill.MaxTargets, 6) : Math.Max(1, skill.MaxTargets);
    }

    private static string NormalizeName(string value)
    {
        string formD = (value ?? "").Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(formD.Length);
        foreach (char c in formD)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(char.ToLowerInvariant(c));
        }

        return sb.ToString().Normalize(NormalizationForm.FormC).Replace(" ", "");
    }

    private static bool IsAreaSkill(string escopo, string tipo, string efeito)
    {
        string text = $"{escopo} {tipo} {efeito}".ToLowerInvariant();
        return text.Contains("Ã¡rea") || text.Contains("area") || text.Contains("multi") || text.Contains("chuva") || text.Contains("explos");
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

    private static (float Min, float Max) GetDamageMultiplierRange(string danoEscala, int valor)
    {
        var matches = Regex.Matches(danoEscala.Replace(',', '.'), @"(?<n>\d+(?:\.\d+)?)\s*%");
        if (matches.Count > 0)
        {
            var values = matches
                .Select(m => float.TryParse(m.Groups["n"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out float pct) ? pct / 100f : 0f)
                .Where(v => v > 0f)
                .ToArray();
            if (values.Length > 0)
                return (MathF.Max(0.05f, values.Min()), MathF.Max(0.05f, values.Max()));
        }

        float fallback = valor > 0 ? MathF.Max(0.05f, valor / 100f) : 1f;
        return (fallback, fallback);
    }

    private static float GetDamageMultiplier(string danoEscala, int valor)
    {
        var match = Regex.Match(danoEscala.Replace(',', '.'), @"(?<n>\d+(?:\.\d+)?)\s*%");
        if (match.Success && float.TryParse(match.Groups["n"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out float pct))
            return MathF.Max(0.05f, pct / 100f);
        return valor > 0 ? MathF.Max(0.05f, valor / 100f) : 1f;
    }
}
