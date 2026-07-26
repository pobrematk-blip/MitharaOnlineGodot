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
    private enum PvpAreaKind
    {
        Normal,
        Safe,
        Arena,
        Dungeon,
    }

    private const double DefaultBasicAttackCooldown = 1.2;
    private const double BerserkerBasicAttackCooldown = 1.35;
    private const double RangedBasicAttackCooldown = 1.1;
    private const int PetAttackSkillId = -100;
    private const float BasicAttackRange = 640f;
    private const float PetOwnerCommandRange = 32f * 12f;
    private const float PetLootPickupRange = 32f * 20f;
    private const int GolpeSombrioSkillId = 12201;
    private const int PassoSombrioSkillId = 12102;
    private const int InvisibilidadeProfundaSkillId = 12202;
    private const int SaltoNasCostasSkillId = 12203;
    private const int GolpeAtordoanteSkillId = 12204;
    private const int PunhaladaNasCostasSkillId = 12205;
    private const int ChuteNaVirilhaSkillId = 12210;
    private const int ExecucaoFinalSkillId = 12209;
    private const int MarcaDaMorteAssassinoSkillId = 12206;
    private const int MarcaDaMorteSlayerSkillId = 13202;
    private const int LancaDeGeloSkillId = 11202;
    private const int TornadoSkillId = 11204;
    private const int SonoArcanoSkillId = 11210;
    private const int TeleporteArcanoSkillId = 11002;
    private const int ChuvaMeteorosSkillId = 11209;
    private const int EscudoArcanoSkillId = 11003;
    private const int FuriaElementalSkillId = 11208;
    private const int BencaoDivinaSkillId = 15003;
    private const int PassoDivinoSkillId = 15002;
    private const int CuraMenorSkillId = 15101;
    private const int MarteloDesolacaoSkillId = 15102;
    private const int CuraEmAreaSkillId = 15103;
    private const int PurificacaoSkillId = 15104;
    private const int RenovacaoSkillId = 15105;
    private const int LuzRestauradoraSkillId = 15107;
    private const int RessurreicaoSkillId = 15108;
    private const int MilagreDivinoSkillId = 15109;
    private const int SegundoFolegoUniversalSkillId = 9001;
    private const double RenovacaoTickInterval = 1.0;
    private const int FuriaBerserkerSkillId = 13101;
    private const int SangramentoMortalSkillId = 13001;
    private const int FrenesiBerserkerSkillId = 13106;
    private const int InvestidaBrutalSkillId = 13002;
    private const int SedeDeSangueSkillId = 13103;
    private const int ForcaBrutalSkillId = 13104;
    private const int GolpesFreneticosSkillId = 13102;
    private const int MachadoGiratorioSkillId = 13105;
    private const int BerserkSkillId = 13108;
    private const int DeusDaGuerraSkillId = 13109;
    private const int CarnificinaSkillId = 13107;
    private const int SangueDeFerroSkillId = 13003;
    private const int EstocadaSkillId = 14102;
    private const int GolpeDeEscudoSkillId = 14103;
    private const int EspinhosSkillId = 14105;
    private const int ProvocacaoSkillId = 14101;
    private const int DesafioSkillId = 14106;
    private const int FortificacaoSkillId = 14107;
    private const int BastiaoSkillId = 14108;
    private const int MuralhaInabalavelSkillId = 14109;
    private const float MeleeSkillRange = 96f;
    private const float TileSize = 32f;
    private const float MarcaDaMorteRange = TileSize * 10f;
    private const float SacerdoteDanoRange = TileSize * 8f;
    private const float SangramentoMortalRange = TileSize * 10f;
    private const float CuraEmAreaRadius = TileSize * 10f;
    private const float MilagreDivinoRadius = TileSize * 10f;
    private const float RessurreicaoRange = TileSize * 10f;
    private const float RessurreicaoTargetRadius = TileSize * 3.75f;
    private const float LancaDeGeloRange = TileSize * 22.5f;
    private const float LancaDeGeloRadius = TileSize * 3.75f;
    private const float TornadoRange = TileSize * 22.5f;
    private const float TornadoRadius = TileSize * 4f;
    private const float TornadoPullInnerRadius = TileSize * 1.25f;
    private const float ChuvaMeteorosRange = TileSize * 24f;
    private const float ChuvaMeteorosRadius = TileSize * 5.5f;
    private const float MachadoGiratorioRadius = TileSize * 3.0f;
    private const int MachadoGiratorioTickCount = 4;
    private const double MachadoGiratorioTickInterval = 0.5;
    private const float GolpeDeEscudoRange = TileSize * 10f;
    private const float ProvocacaoRadius = TileSize * 10f;
    private const float DesafioRange = TileSize * 10f;
    private const float DesafioTargetPickRadius = TileSize * 2.5f;
    private const float EspinhosRadius = TileSize * 4f;
    private const int EspinhosTickCount = 4;
    private const double EspinhosTickInterval = 0.5;
    private const double EspinhosSlowDuration = 3.0;
    private const int SegundoFolegoTickCount = 5;
    private const int SegundoFolegoFixedTotalHealth = 100;
    private const int SegundoFolegoFixedTotalMana = 100;
    private const double SegundoFolegoTickInterval = 0.5;
    private const float BastiaoRadius = TileSize * 3f;
    private const int BastiaoMaxHits = 8;
    private const float SaltoNasCostasRange = TileSize * 16f;
    private const float SaltoNasCostasBackOffset = TileSize;
    private const float InvisibilidadeProfundaOpeningDamageBonus = 1.25f;
    private const byte PlayerActionAttack = 1;
    private const byte PlayerActionInvisibility = 4;
    private const byte PlayerActionReveal = 5;
    private const byte PlayerActionArcaneTeleportEnter = 6;
    private const byte PlayerActionArcaneTeleportExit = 7;
    private const byte PlayerActionGolpesFreneticos = 8;
    private const byte PlayerActionCarnificinaJump = 9;
    private const double PetAttackCooldown = 0.8;
    private const int BossSlimeJumpSkillId = -201;
    private const double BossSlimeSpeedBuffCooldown = 60.0;
    private const double BossSlimeSpeedBuffDuration = 20.0;
    private const double BossSlimeJumpCooldown = 24.0;
    private const double BossSlimeSlowCooldown = 28.0;
    private const double BossSlimeTornadoCooldown = 42.0;
    private const double BossSlimeSlowDuration = 6.0;
    private const int BossSlimeSlowPercent = 40;
    private const float BossSlimeCastSeconds = 0.85f;
    private const float BossSlimeTornadoCastSeconds = 1.25f;
    private const int BossSlimeTornadoSkillId = -202;

    private static long XpForNextLevel(int level)
    {
        if (level < 1) level = 1;
        return 600L + level * 260L + level * level * 90L;
    }

    private double GetBasicAttackCooldown(PlayerEntity player)
    {
        PruneExpiredServerBuffs(player);
        string classe = player.CharacterClass?.Trim().ToLowerInvariant() ?? "";
        double baseCooldown = classe switch
        {
            "berseker" or "berserker" or "bÃ¡rbaro" or "barbaro" => BerserkerBasicAttackCooldown,
            "arqueiro" or "mago" => RangedBasicAttackCooldown,
            _ => DefaultBasicAttackCooldown,
        };
        return baseCooldown / player.CalculateAttackSpeedMultiplier();
    }

    private bool CanDamageEntity(Entity attacker, Entity target, out string reason)
    {
        reason = "";

        if (attacker.Id == target.Id)
        {
            reason = "VocÃª nÃ£o pode atacar a si mesmo.";
            return false;
        }

        if (attacker.Health <= 0 || target.Health <= 0)
            return false;

        if (attacker is PlayerEntity attackerPlayer && target is PlayerEntity targetPlayer)
            return CanPlayerDamagePlayer(attackerPlayer, targetPlayer, out reason);

        if (attacker is PlayerEntity playerAttacker && target is MonsterEntity mobTarget)
        {
            if (!string.IsNullOrWhiteSpace(mobTarget.FactionId)
                && string.Equals(mobTarget.FactionId, playerAttacker.FactionId, StringComparison.OrdinalIgnoreCase))
            {
                reason = "Alvo aliado.";
                return false;
            }
        }

        return true;
    }

    private bool CanPlayerDamagePlayer(PlayerEntity attacker, PlayerEntity target, out string reason)
    {
        reason = "";
        if (CanDuelistsDamage(attacker, target))
            return true;

        if (IsPlayerInActiveDuel(attacker.Id) || IsPlayerInActiveDuel(target.Id))
        {
            reason = "Duelo: PvP liberado somente entre duelistas dentro da area.";
            return false;
        }

        var area = ResolvePvpArea(attacker, target);

        if (area == PvpAreaKind.Safe)
        {
            reason = "Ãrea segura: PvP desativado.";
            return false;
        }

        if (ArePlayersInSameParty(attacker, target))
        {
            reason = "VocÃª nÃ£o pode atacar jogadores do seu grupo.";
            return false;
        }

        if (area == PvpAreaKind.Arena)
            return true;

        if (area == PvpAreaKind.Dungeon)
            return true;

        if (SameFaction(attacker, target))
        {
            reason = "VocÃª nÃ£o pode atacar jogadores da sua facÃ§Ã£o nesta Ã¡rea.";
            return false;
        }

        return true;
    }

    private PvpAreaKind ResolvePvpArea(PlayerEntity attacker, PlayerEntity target)
    {
        string attackerMap = GetPlayerCurrentMap(attacker.Id);
        string targetMap = GetPlayerCurrentMap(target.Id);
        var attackerArea = ResolvePvpArea(attackerMap, attacker.X, attacker.Y);
        var targetArea = ResolvePvpArea(targetMap, target.X, target.Y);

        if (attackerArea == PvpAreaKind.Safe || targetArea == PvpAreaKind.Safe)
            return PvpAreaKind.Safe;
        if (attackerArea == PvpAreaKind.Arena && targetArea == PvpAreaKind.Arena)
            return PvpAreaKind.Arena;
        if (attackerArea == PvpAreaKind.Dungeon && targetArea == PvpAreaKind.Dungeon)
            return PvpAreaKind.Dungeon;
        return PvpAreaKind.Normal;
    }

    private PvpAreaKind ResolvePvpArea(string mapName, float x, float y)
    {
        if (_config.PvpZones.Count > 0)
        {
            foreach (var priority in new[] { PvpZoneType.Safe, PvpZoneType.Arena, PvpZoneType.Dungeon, PvpZoneType.Normal })
            {
                foreach (var zone in _config.PvpZones)
                {
                    if (zone.Type == priority && zone.Contains(mapName, x, y))
                        return priority switch
                        {
                            PvpZoneType.Safe => PvpAreaKind.Safe,
                            PvpZoneType.Arena => PvpAreaKind.Arena,
                            PvpZoneType.Dungeon => PvpAreaKind.Dungeon,
                            _ => PvpAreaKind.Normal,
                        };
                }
            }
        }

        return ResolvePvpAreaFromMapName(mapName);
    }

    private static PvpAreaKind ResolvePvpAreaFromMapName(string mapName)
    {
        string map = (mapName ?? "").Trim().ToLowerInvariant();
        if (map.Contains("safe") || map.Contains("cidade") || map.Contains("town") || map.Contains("vila"))
            return PvpAreaKind.Safe;
        if (map.Contains("arena") || map.Contains("gvg") || map.Contains("pvp"))
            return PvpAreaKind.Arena;
        if (map.Contains("dungeon") || map.Contains("calabouco") || map.Contains("masmorra"))
            return PvpAreaKind.Dungeon;
        return PvpAreaKind.Normal;
    }

    private string GetPlayerCurrentMap(ulong entityId)
    {
        foreach (var session in _sessions.Values)
        {
            if (session.EntityId == entityId)
                return session.CurrentMap;
        }
        return MainSceneName;
    }

    private static bool SameFaction(PlayerEntity a, PlayerEntity b)
    {
        if (string.IsNullOrWhiteSpace(a.FactionId) || string.IsNullOrWhiteSpace(b.FactionId))
            return false;
        return string.Equals(a.FactionId, b.FactionId, StringComparison.OrdinalIgnoreCase);
    }

    private static bool SameFaction(Entity a, Entity b)
    {
        if (string.IsNullOrWhiteSpace(a.FactionId) || string.IsNullOrWhiteSpace(b.FactionId))
            return false;
        return string.Equals(a.FactionId, b.FactionId, StringComparison.OrdinalIgnoreCase);
    }

    private bool ArePlayersInSameParty(PlayerEntity a, PlayerEntity b)
    {
        if (a.PartyId < 0 || b.PartyId < 0)
            return false;
        return a.PartyId == b.PartyId;
    }

    private bool HandleMonsterAIAttack(Channel channel, MonsterEntity mob, Entity target, double gameTime)
    {
        return DealMonsterDamage(channel, mob, target, gameTime, 1f, 0);
    }

    private bool DealMonsterDamage(Channel channel, MonsterEntity mob, Entity target, double gameTime, float damageMultiplier, int skillId)
    {
        if (target is PlayerEntity targetPlayerForBuffs)
            RefreshTemporarySkillBonuses(targetPlayerForBuffs);

        int targetDefense = target switch
        {
            PlayerEntity p => p.CalculateDefense(),
            MonsterEntity m => m.CalculateDefense(),
            _ => 0,
        };
        if (target is PlayerEntity playerTarget
            && Random.Shared.NextDouble() * 100.0 < playerTarget.CalculateEvasion())
        {
            BroadcastCombatResult(channel, mob.Id, target.Id, 0, false, target.Health, target.MaxHealth, mob.X, mob.Y, skillId);
            return false;
        }
        float defReduction = MathF.Min(0.80f, targetDefense / (targetDefense + 400f));
        float critChance = MathF.Max(0f, mob.Destreza / 4f - (target is PlayerEntity tenacious ? tenacious.CalculateTenacity() * 0.5f : 0f));
        bool isCrit = Random.Shared.NextDouble() * 100.0 < critChance;
        int rawDamage = mob.CalculateAttackDamage();
        int damage = Math.Max(1, (int)(rawDamage * damageMultiplier * (1f - defReduction)));
        if (isCrit)
        {
            float critMultiplier = target is PlayerEntity critTarget ? MathF.Max(1.2f, 1.5f - critTarget.CalculateTenacity() / 200f) : 1.5f;
            damage = Math.Max(1, (int)(damage * critMultiplier));
        }

        int healthDamage = ApplyDamageToEntity(channel, target, damage);

        if (target is PlayerEntity reflectingPlayer && mob.Health > 0 && healthDamage > 0)
        {
            float reflectPercent = GetDamageReflectPercent(reflectingPlayer);
            if (reflectPercent > 0f)
            {
                int reflected = Math.Min(mob.Health, Math.Max(1, (int)MathF.Round(healthDamage * reflectPercent / 100f)));
                mob.Health = Math.Max(0, mob.Health - reflected);
            BroadcastCombatResult(channel, reflectingPlayer.Id, mob.Id, reflected, false, mob.Health, mob.MaxHealth, reflectingPlayer.X, reflectingPlayer.Y);
                if (mob.Health <= 0)
                {
                    var reflectingSession = _sessions.Values.FirstOrDefault(s => s.EntityId == reflectingPlayer.Id);
                    if (reflectingSession?.SelectedCharacter != null)
                        HandleMonsterDeath(channel, mob, reflectingPlayer, reflectingSession, mob.Id);
                    return true;
                }
            }
        }

        if (target is PlayerEntity)
        {
            target.LastCombatTime = gameTime;
            BroadcastPartyMemberUpdateForEntity(target.Id);
        }

        BroadcastCombatResult(channel, mob.Id, target.Id, damage, isCrit, target.Health, target.MaxHealth, mob.X, mob.Y, skillId);

        if (target.Health <= 0 && target is PlayerEntity player)
        {
            var session = _sessions.Values.FirstOrDefault(s => s.EntityId == target.Id);
            if (session != null)
                SendSystemMessage(session.Peer, "VocÃª morreu!");
        }

        return target.Health <= 0;
    }

    private int ApplyDamageToEntity(Channel channel, Entity target, int damage)
    {
        damage = Math.Max(0, damage);
        if (damage <= 0)
            return 0;

        if (target is PlayerEntity player)
        {
            damage = ApplyBastionReduction(channel, player, damage);
            if (damage <= 0)
                return 0;

            if (damage <= 0)
                return 0;

            PruneExpiredArcaneShield(channel, player);
            if (player.ArcaneShield > 0)
            {
                int absorbed = Math.Min(player.ArcaneShield, damage);
                player.ArcaneShield -= absorbed;
                damage -= absorbed;
                if (player.ArcaneShield <= 0)
                {
                    player.ArcaneShield = 0;
                    player.ArcaneShieldMax = 0;
                    player.ArcaneShieldExpiresAt = 0;
                }
                SendShieldUpdateToPlayer(channel, player);
            }
        }

        if (damage <= 0)
            return 0;

        int healthDamage = Math.Min(target.Health, damage);
        target.Health = Math.Max(0, target.Health - healthDamage);
        return healthDamage;
    }

    private float GetDamageReflectPercent(PlayerEntity player)
    {
        return MathF.Max(0f, player.DamageReflect);
    }

    private int ApplyBastionReduction(Channel channel, PlayerEntity target, int damage)
    {
        if (damage <= 0)
            return 0;

        PruneExpiredBastionAreas();

        ActiveBastionArea? bestArea = null;
        int bestReduction = 0;
        int? targetPartyId = _world.Parties.GetPlayerPartyId(target.Id);

        foreach (var area in _activeBastionAreas)
        {
            if (area.ChannelId != channel.Id || area.ExpiresAt <= _gameTime)
                continue;

            bool sameGroup = area.CasterId == target.Id
                || (area.PartyId > 0 && targetPartyId.HasValue && targetPartyId.Value == area.PartyId);
            if (!sameGroup)
                continue;

            float dx = target.X - area.X;
            float dy = target.Y - area.Y;
            if (MathF.Sqrt(dx * dx + dy * dy) > area.Radius)
                continue;

            if (area.ReductionPercent > bestReduction)
            {
                bestReduction = area.ReductionPercent;
                bestArea = area;
            }
        }

        if (bestArea == null || bestReduction <= 0)
            return damage;

        int reducedDamage = Math.Max(0, (int)MathF.Round(damage * (1f - bestReduction / 100f)));
        bestArea.HitsTaken++;

        if (bestArea.HitsTaken >= bestArea.MaxHits)
        {
            _activeBastionAreas.Remove(bestArea);
            BroadcastSkillAreaEffect(channel, null, BastiaoSkillId, bestArea.X, bestArea.Y, bestArea.Radius, 0f);
        }
        else
        {
            float progress = Math.Clamp(bestArea.HitsTaken / (float)bestArea.MaxHits, 0f, 1f);
            BroadcastSkillAreaEffect(channel, null, BastiaoSkillId, bestArea.X, bestArea.Y, bestArea.Radius, -progress);
        }

        return reducedDamage;
    }

    private bool HandleMonsterSpecial(Channel channel, MonsterEntity mob, Entity target, double gameTime)
    {
        if (!mob.IsBoss || !string.Equals(mob.PrefabId, "slimeBoss", StringComparison.OrdinalIgnoreCase))
            return false;

        if (!string.IsNullOrWhiteSpace(mob.BossPendingSkill))
            return ProcessBossSlimePendingSkill(channel, mob, target, gameTime);

        float dx = target.X - mob.X;
        float dy = target.Y - mob.Y;
        float dist = MathF.Sqrt(dx * dx + dy * dy);

        if (TryStartBossSlimeTornadoStar(channel, mob, target, gameTime))
            return true;

        if (TryStartBossSlimeSpeedBuff(channel, mob, gameTime))
            return true;

        if (target is PlayerEntity playerTarget)
        {
            if (TryStartBossSlimeSlow(channel, mob, playerTarget, gameTime, dist))
                return true;
        }

        if (dist > mob.AttackRange + 36f && dist <= 520f && gameTime - mob.LastBossJumpTime >= BossSlimeJumpCooldown)
        {
            mob.LastBossJumpTime = gameTime;
            StartBossSlimeCast(channel, mob, target.Id, "jump", "Salto Esmagador", BossSlimeCastSeconds, 0f, false);
            return true;
        }

        return false;
    }

    private bool ProcessBossSlimePendingSkill(Channel channel, MonsterEntity mob, Entity target, double gameTime)
    {
        mob.Moving = false;
        mob.AIState = MonsterAIState.Attack;
        if (gameTime < mob.BossPendingCompleteTime)
            return true;

        string pendingSkill = mob.BossPendingSkill;
        ulong pendingTargetId = mob.BossPendingTargetId;
        mob.BossPendingSkill = "";
        mob.BossPendingTargetId = 0;
        mob.BossPendingCompleteTime = 0;

        if (pendingSkill == "speed")
        {
            mob.ActiveServerBuffs["boss_speed"] = gameTime + BossSlimeSpeedBuffDuration;
            SendBossSlimeNotice(channel, mob, "Boss Slime ficou mais rÃ¡pido!");
            SendBossCast(channel, mob, "speed_buff", "AceleraÃ§Ã£o Viscosa", 0f, (float)BossSlimeSpeedBuffDuration, true);
            return true;
        }

        var castTarget = channel.GetEntity(pendingTargetId);
        if (castTarget == null || castTarget.Health <= 0)
            castTarget = target;

        if (pendingSkill == "tornado_star")
        {
            FireBossSlimeTornadoStar(channel, mob, castTarget);
            SendBossSlimeNotice(channel, mob, "Boss Slime lancou 5 tornados!");
            return true;
        }

        if (pendingSkill == "slow" && castTarget is PlayerEntity playerTarget)
        {
            playerTarget.ActiveServerBuffs["slow:boss_slime"] = gameTime + BossSlimeSlowDuration;
            SendStatusEffectToPlayer(channel, playerTarget.Id, "boss_slime_slow", "LentidÃ£o do Slime", true, (float)BossSlimeSlowDuration, BossSlimeSlowPercent, "res://skills/Incone Skills/Debuffs/1.png");
            SendBossSlimeNotice(channel, mob, "Boss Slime deixou o alvo lento!");
            DealMonsterDamage(channel, mob, playerTarget, gameTime, 0.35f, 0);
            return true;
        }

        if (pendingSkill == "jump")
        {
            float dx = castTarget.X - mob.X;
            float dy = castTarget.Y - mob.Y;
            float dist = MathF.Sqrt(dx * dx + dy * dy);
            float len = MathF.Max(1f, dist);
            float fromTargetX = -dx / len;
            float fromTargetY = -dy / len;
            float landingX = castTarget.X + fromTargetX * 52f;
            float landingY = castTarget.Y + fromTargetY * 52f;

            channel.MoveEntity(mob.Id, landingX, landingY);
            mob.X = landingX;
            mob.Y = landingY;
            mob.DirX = dx / len;
            mob.DirY = dy / len;
            mob.Moving = false;
            mob.AIState = MonsterAIState.Attack;
            mob.LastAttackTime = gameTime;
            BroadcastSingleEntityUpdate(channel, mob);

            bool targetDied = DealMonsterDamage(channel, mob, castTarget, gameTime, 1.35f, BossSlimeJumpSkillId);
            if (targetDied)
                mob.TargetEntityId = null;
            return true;
        }

        return false;
    }

    private bool TryStartBossSlimeSpeedBuff(Channel channel, MonsterEntity mob, double gameTime)
    {
        if (mob.ActiveServerBuffs.TryGetValue("boss_speed", out double activeUntil) && activeUntil > gameTime)
            return false;
        if (gameTime - mob.LastBossSpeedBuffTime < BossSlimeSpeedBuffCooldown)
            return false;

        mob.LastBossSpeedBuffTime = gameTime;
        StartBossSlimeCast(channel, mob, 0, "speed", "AceleraÃ§Ã£o Viscosa", BossSlimeCastSeconds, BossSlimeSpeedBuffDuration, true);
        return true;
    }

    private bool TryStartBossSlimeSlow(Channel channel, MonsterEntity mob, PlayerEntity target, double gameTime, float dist)
    {
        if (dist > 360f || gameTime - mob.LastBossSlowTime < BossSlimeSlowCooldown)
            return false;

        mob.LastBossSlowTime = gameTime;
        StartBossSlimeCast(channel, mob, target.Id, "slow", "Lodo Pegajoso", BossSlimeCastSeconds, 0f, false);
        return true;
    }

    private bool TryStartBossSlimeTornadoStar(Channel channel, MonsterEntity mob, Entity target, double gameTime)
    {
        if (gameTime - mob.LastBossTornadoTime < BossSlimeTornadoCooldown)
            return false;

        float dx = target.X - mob.X;
        float dy = target.Y - mob.Y;
        float distSq = dx * dx + dy * dy;
        if (distSq > 900f * 900f)
            return false;

        mob.LastBossTornadoTime = gameTime;
        StartBossSlimeCast(channel, mob, target.Id, "tornado_star", "Rajada de Tornados", BossSlimeTornadoCastSeconds, 0f, false);
        return true;
    }

    private void FireBossSlimeTornadoStar(Channel channel, MonsterEntity mob, Entity target)
    {
        float baseDirX = target.X - mob.X;
        float baseDirY = target.Y - mob.Y;
        float len = MathF.Sqrt(baseDirX * baseDirX + baseDirY * baseDirY);
        if (len <= 0.01f)
        {
            baseDirX = 1f;
            baseDirY = 0f;
        }
        else
        {
            baseDirX /= len;
            baseDirY /= len;
        }

        const float projectileRange = 680f;
        const float projectileRadius = 46f;
        const float projectileSpeed = 620f;
        const byte slimeTornadoProjectileType = 2;
        int[] angles = { 0, 72, -72, 144, -144 };

        foreach (int angleDegrees in angles)
        {
            var dir = Rotate(baseDirX, baseDirY, angleDegrees * MathF.PI / 180f);
            float originX = mob.X + dir.X * 28f;
            float originY = mob.Y + dir.Y * 28f;

            BroadcastProjectileSpawn(channel, mob.Id, originX, originY, dir.X, dir.Y, slimeTornadoProjectileType, includeCaster: true);
            var hit = FindFirstBossProjectileHit(channel, mob, originX, originY, dir.X, dir.Y, projectileRange, projectileRadius);
            if (hit == null)
                continue;

            float along = ProjectileHitDistance(originX, originY, dir.X, dir.Y, hit.X, hit.Y, projectileRange).Along;
            _pendingMonsterProjectileHits.Add(new PendingMonsterProjectileHit
            {
                ImpactAt = _gameTime + Math.Clamp(along / projectileSpeed, 0.08f, 1.8f),
                ChannelId = channel.Id,
                CasterId = mob.Id,
                TargetId = hit.Id,
                DamageMultiplier = 0.75f,
                SkillId = BossSlimeTornadoSkillId,
                AppliesSlow = true,
            });
        }
    }

    private static (float X, float Y) Rotate(float x, float y, float radians)
    {
        float cos = MathF.Cos(radians);
        float sin = MathF.Sin(radians);
        return (x * cos - y * sin, x * sin + y * cos);
    }

    private Entity? FindFirstBossProjectileHit(Channel channel, MonsterEntity mob, float originX, float originY, float dirX, float dirY, float range, float radius)
    {
        return channel.GetEntitiesInAoi(mob.X, mob.Y)
            .Select(id => channel.GetEntity(id))
            .Where(e => e is PlayerEntity { Health: > 0 })
            .Select(e => new { Entity = e!, Hit = ProjectileHitDistance(originX, originY, dirX, dirY, e!.X, e.Y, range) })
            .Where(x => x.Hit.Along >= 0f && x.Hit.Along <= range && x.Hit.Perpendicular <= radius)
            .OrderBy(x => x.Hit.Along)
            .Select(x => x.Entity)
            .FirstOrDefault();
    }

    private void ProcessPendingMonsterProjectileHits()
    {
        for (int i = _pendingMonsterProjectileHits.Count - 1; i >= 0; i--)
        {
            var pending = _pendingMonsterProjectileHits[i];
            if (pending.ImpactAt > _gameTime)
                continue;

            _pendingMonsterProjectileHits.RemoveAt(i);
            var channel = _world.GetChannel(pending.ChannelId);
            if (channel == null)
                continue;

            if (channel.GetEntity(pending.CasterId) is not MonsterEntity mob || mob.Health <= 0)
                continue;
            var target = channel.GetEntity(pending.TargetId);
            if (target is not PlayerEntity playerTarget || playerTarget.Health <= 0)
                continue;

            bool died = DealMonsterDamage(channel, mob, playerTarget, _gameTime, pending.DamageMultiplier, pending.SkillId);
            if (pending.AppliesSlow && !died)
            {
                playerTarget.ActiveServerBuffs["slow:boss_slime"] = _gameTime + BossSlimeSlowDuration;
                SendStatusEffectToPlayer(channel, playerTarget.Id, "boss_slime_slow", "LentidÃƒÂ£o do Slime", true, (float)BossSlimeSlowDuration, BossSlimeSlowPercent, "res://skills/Incone Skills/Debuffs/1.png");
            }
        }
    }

    private void StartBossSlimeCast(Channel channel, MonsterEntity mob, ulong targetId, string skillKey, string skillName, float castSeconds, double effectDuration, bool isBuff)
    {
        mob.BossPendingSkill = skillKey;
        mob.BossPendingTargetId = targetId;
        mob.BossPendingCompleteTime = _gameTime + Math.Max(0.1f, castSeconds);
        mob.Moving = false;
        mob.AIState = MonsterAIState.Attack;
        SendBossCast(channel, mob, skillKey, skillName, castSeconds, (float)effectDuration, isBuff);
    }

    private void SendBossCast(Channel channel, MonsterEntity mob, string effectId, string skillName, float castSeconds, float effectDuration, bool isBuff)
    {
        var writer = PacketSerializer.WritePacket(PacketId.S2C_BossCast);
        writer.Put(mob.Id);
        writer.Put(effectId);
        writer.Put(skillName);
        writer.Put(castSeconds);
        writer.Put(effectDuration);
        writer.Put(isBuff);

        var aoi = channel.GetEntitiesInAoi(mob.X, mob.Y);
        foreach (var eid in aoi)
        {
            var peer = channel.GetPlayerPeer(eid);
            if (peer == null) continue;
            peer.Send(writer, DeliveryMethod.ReliableOrdered);
            writer = PacketSerializer.WritePacket(PacketId.S2C_BossCast);
            writer.Put(mob.Id);
            writer.Put(effectId);
            writer.Put(skillName);
            writer.Put(castSeconds);
            writer.Put(effectDuration);
            writer.Put(isBuff);
        }
    }

    private void SendBossSlimeNotice(Channel channel, MonsterEntity mob, string message)
    {
        var aoi = channel.GetEntitiesInAoi(mob.X, mob.Y);
        foreach (var eid in aoi)
        {
            var peer = channel.GetPlayerPeer(eid);
            if (peer != null)
                SendSystemMessage(peer, message);
        }
    }

    private void SendStatusEffectToPlayer(Channel channel, ulong playerId, string effectId, string displayName, bool isDebuff, float duration, int power, string iconPath)
    {
        var peer = channel.GetPlayerPeer(playerId);
        if (peer == null)
            return;

        var writer = PacketSerializer.WritePacket(PacketId.S2C_StatusEffect);
        writer.Put(effectId);
        writer.Put(displayName);
        writer.Put(isDebuff);
        writer.Put(duration);
        writer.Put(power);
        writer.Put(iconPath);
        peer.Send(writer, DeliveryMethod.ReliableOrdered);
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

        if (HasMovementBlockingDebuff(caster))
        {
            SendSystemMessage(peer, "Voce esta sob efeito de controle.");
            return;
        }

        caster.LastCombatTime = _gameTime;

        int skillSlot = reader.GetInt();
        int skillId = reader.AvailableBytes >= 12 ? reader.GetInt() : 0;
        float targetX = reader.GetFloat();
        float targetY = reader.GetFloat();
        float chargePercent = reader.AvailableBytes >= 4 ? Math.Clamp(reader.GetFloat(), 0f, 1f) : 1f;

        if (skillId <= 0)
        {
            SendSystemMessage(peer, "Habilidade invÃ¡lida. Reatribua a skill na barra.");
            return;
        }

        if (skillSlot < 0 || skillSlot >= caster.SkillBarSlots.Length || caster.SkillBarSlots[skillSlot] != skillId)
        {
            SendSystemMessage(peer, "Esta habilidade nÃƒÂ£o estÃƒÂ¡ autorizada neste slot da barra.");
            return;
        }

        var skill = ServerSkillCatalog.Get(skillId);
        if (skill == null)
        {
            SendSystemMessage(peer, $"Skill {skillId} nÃ£o encontrada no catÃ¡logo do servidor.");
            return;
        }

        if (!ServerSkillCatalog.ClassMatches(caster.CharacterClass, skill.ClasseRestrita))
        {
            SendSystemMessage(peer, "Esta habilidade nÃ£o pertence Ã  sua classe.");
            return;
        }

        if (caster.Level < skill.NivelRequerido)
        {
            SendSystemMessage(peer, $"NÃ­vel {skill.NivelRequerido} necessÃ¡rio para usar {skill.Nome}.");
            return;
        }

        if (!ServerTalentCatalog.IsSkillUnlockedForPlayer(caster.CharacterClass, caster.UnlockedTalents, skill.SkillId))
        {
            SendSystemMessage(peer, "Esta habilidade ainda nÃƒÂ£o foi desbloqueada na ÃƒÂ¡rvore de talentos.");
            return;
        }

        if (TryGetBerserkerExclusiveBuffBlock(caster, skill.SkillId, out var blockedBy, out double blockedUntil))
        {
            SendSystemMessage(peer, $"{skill.Nome} nao pode ser usada enquanto {blockedBy} estiver ativa ({(blockedUntil - _gameTime):0.0}s).");
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

        bool applied = ApplyServerSkill(peer, channel, caster, session, skill, targetX, targetY, chargePercent);
        if (!applied)
        {
            SendSkillUseResult(peer, skillSlot, skill.SkillId, false, 0f);
            return;
        }

        double cooldownSeconds = skill.Cooldown > 0 ? GetEffectiveSkillCooldown(caster, skill.Cooldown) : 0.0;
        if (skill.Cooldown > 0)
        {
            caster.SkillCooldowns[skill.SkillId] = _gameTime + cooldownSeconds;
            ApplySharedBerserkerBuffCooldown(caster, skill.SkillId, cooldownSeconds);
        }
        if (skill.CustoMana > 0)
        {
            caster.Mana = Math.Max(0, caster.Mana - skill.CustoMana);
            BroadcastPartyMemberUpdateForEntity(caster.Id);
        }

        SendSkillUseResult(peer, skillSlot, skill.SkillId, true, (float)cooldownSeconds);
        SendSharedBerserkerBuffCooldownResult(peer, caster, skill.SkillId, skillSlot, (float)cooldownSeconds);
        SendSystemMessage(peer, $"{skill.Nome} usada.");
    }

    private bool TryGetBerserkerExclusiveBuffBlock(PlayerEntity caster, int skillId, out string blockedBy, out double blockedUntil)
    {
        blockedBy = "";
        blockedUntil = 0;

        int otherSkillId = skillId switch
        {
            FuriaBerserkerSkillId => FrenesiBerserkerSkillId,
            FrenesiBerserkerSkillId => FuriaBerserkerSkillId,
            _ => 0,
        };

        if (otherSkillId == 0)
            return false;

        string otherBuffId = $"skill:{otherSkillId}";
        if (!caster.ActiveServerBuffs.TryGetValue(otherBuffId, out blockedUntil) || blockedUntil <= _gameTime)
            return false;

        blockedBy = otherSkillId == FuriaBerserkerSkillId ? "Furia" : "Frenesi";
        return true;
    }

    private void ApplySharedBerserkerBuffCooldown(PlayerEntity caster, int skillId, double cooldownSeconds)
    {
        if (cooldownSeconds <= 0)
            return;

        if (skillId == FuriaBerserkerSkillId)
            caster.SkillCooldowns[FrenesiBerserkerSkillId] = _gameTime + cooldownSeconds;
        else if (skillId == FrenesiBerserkerSkillId)
            caster.SkillCooldowns[FuriaBerserkerSkillId] = _gameTime + cooldownSeconds;
    }

    private void SendSharedBerserkerBuffCooldownResult(NetPeer peer, PlayerEntity caster, int skillId, int usedSlot, float cooldownSeconds)
    {
        if (cooldownSeconds <= 0f)
            return;

        int otherSkillId = skillId switch
        {
            FuriaBerserkerSkillId => FrenesiBerserkerSkillId,
            FrenesiBerserkerSkillId => FuriaBerserkerSkillId,
            _ => 0,
        };

        if (otherSkillId == 0 || caster.SkillBarSlots == null)
            return;

        int otherSlot = Array.IndexOf(caster.SkillBarSlots, otherSkillId);
        if (otherSlot < 0 || otherSlot == usedSlot)
            return;

        SendSkillUseResult(peer, otherSlot, otherSkillId, true, cooldownSeconds);
    }

    private static void SendSkillUseResult(NetPeer peer, int slotIndex, int skillId, bool success, float cooldownSeconds)
    {
        var writer = PacketSerializer.WritePacket(PacketId.S2C_SkillUseResult);
        writer.Put(slotIndex);
        writer.Put(skillId);
        writer.Put(success);
        writer.Put(cooldownSeconds);
        peer.Send(writer, DeliveryMethod.ReliableOrdered);
    }

    private void SendShieldUpdateToPlayer(Channel channel, PlayerEntity player)
    {
        var peer = channel.GetPlayerPeer(player.Id);
        if (peer == null)
            return;

        var writer = PacketSerializer.WritePacket(PacketId.S2C_ShieldUpdate);
        writer.Put(player.ArcaneShield);
        writer.Put(player.ArcaneShieldMax);
        peer.Send(writer, DeliveryMethod.ReliableOrdered);
    }

    private void PruneExpiredArcaneShield(Channel channel, PlayerEntity player)
    {
        if (player.ArcaneShield <= 0)
            return;

        if (player.ArcaneShieldExpiresAt > 0 && player.ArcaneShieldExpiresAt <= _gameTime)
        {
            player.ArcaneShield = 0;
            player.ArcaneShieldMax = 0;
            player.ArcaneShieldExpiresAt = 0;
        }
    }

    private bool ApplyServerSkill(NetPeer peer, Channel channel, PlayerEntity caster, PlayerSession session, ServerSkillDefinition skill, float targetX, float targetY, float chargePercent)
    {
        int effect = skill.EffectType;
        if (IsExecucaoFinalSkill(skill))
            return ApplyExecucaoFinalSkill(peer, channel, caster, session, skill, targetX, targetY);
        if (skill.SkillId == RessurreicaoSkillId)
            return ApplyRessurreicaoSkill(peer, channel, caster, skill, targetX, targetY);
        if (skill.SkillId == ProvocacaoSkillId)
            return ApplyProvocacaoSkill(peer, channel, caster, skill);
        if (skill.SkillId == DesafioSkillId)
            return ApplyDesafioSkill(peer, channel, caster, skill, targetX, targetY);
        if (skill.SkillId == BastiaoSkillId)
            return ApplyBastiaoSkill(peer, channel, caster, skill);
        if (skill.SkillId == EspinhosSkillId)
            return ApplyEspinhosSkill(peer, channel, caster, skill);

        bool supportSkill = effect is 1 or 3 or 10;
        if (supportSkill)
            return ApplySupportSkill(peer, channel, caster, skill, targetX, targetY);
        if (skill.SkillId == PassoSombrioSkillId)
            return ApplyPassoSombrioSkill(peer, channel, caster, session, skill, targetX, targetY);
        if (skill.SkillId == SaltoNasCostasSkillId)
            return ApplySaltoNasCostasSkill(peer, channel, caster, session, skill, targetX, targetY);
        if (effect == 4)
            return ApplyInvisibilitySkill(peer, channel, caster, skill);
        if (skill.SkillId == InvestidaBrutalSkillId)
            return ApplyInvestidaBrutalSkill(peer, channel, caster, session, skill, targetX, targetY);
        if (skill.SkillId == EstocadaSkillId)
            return ApplyEstocadaSkill(peer, channel, caster, session, skill, targetX, targetY);
        if (skill.SkillId == GolpeDeEscudoSkillId)
            return ApplyGolpeDeEscudoSkill(peer, channel, caster, session, skill, targetX, targetY);
        if (effect == 2)
            return ApplyDashSkill(peer, channel, caster, session, skill, targetX, targetY);
        if (effect == 5)
            return ApplySummonSkill(peer, channel, caster, skill);

        if (skill.SkillId == LancaDeGeloSkillId)
            return ApplyLancaDeGeloSkill(peer, channel, caster, session, skill, targetX, targetY);
        if (skill.SkillId == TornadoSkillId)
            return ApplyTornadoSkill(peer, channel, caster, session, skill, targetX, targetY);
        if (skill.SkillId == SonoArcanoSkillId)
            return ApplySonoArcanoSkill(peer, channel, caster, skill, targetX, targetY);
        if (skill.SkillId == ChuvaMeteorosSkillId)
            return ApplyChuvaMeteorosSkill(peer, channel, caster, session, skill, targetX, targetY);
        if (skill.SkillId == GolpesFreneticosSkillId)
            return ApplyGolpesFreneticosSkill(peer, channel, caster, skill, targetX, targetY);
        if (skill.SkillId == MachadoGiratorioSkillId)
            return ApplyMachadoGiratorioSkill(peer, channel, caster, session, skill);
        if (skill.SkillId == CarnificinaSkillId)
            return ApplyCarnificinaSkill(peer, channel, caster, session, skill, targetX, targetY);
        if (skill.SkillId == MilagreDivinoSkillId)
            return ApplyMilagreDivinoSkill(peer, channel, caster, session, skill);
        if (skill.SkillId == SangramentoMortalSkillId)
            return ApplySangramentoMortalSkill(peer, channel, caster, session, skill, targetX, targetY);

        if (IsProjectileSkill(caster, skill))
            return ApplyProjectileSkill(peer, channel, caster, session, skill, targetX, targetY, chargePercent);

        bool isGolpeSombrio = IsGolpeSombrioSkill(skill);
        bool isPunhaladaNasCostas = IsPunhaladaNasCostasSkill(skill);
        bool isGolpeAtordoante = IsGolpeAtordoanteSkill(skill);
        bool isChuteNaVirilha = IsChuteNaVirilhaSkill(skill);
        bool isMeleeAssassinSkill = isGolpeSombrio || isPunhaladaNasCostas || isGolpeAtordoante || isChuteNaVirilha;
        float range = skill.TargetType == 0 || isMeleeAssassinSkill ? 0f : 720f;
        if (skill.SkillId == 15001 || skill.SkillId == MarteloDesolacaoSkillId)
            range = SacerdoteDanoRange;
        float dxTarget = targetX - caster.X;
        float dyTarget = targetY - caster.Y;
        if (range > 0 && MathF.Sqrt(dxTarget * dxTarget + dyTarget * dyTarget) > range)
        {
            SendSystemMessage(peer, "Alvo fora do alcance.");
            return false;
        }

        float radius = skill.IsArea ? 180f : (isMeleeAssassinSkill ? MeleeSkillRange : 80f);
        var targets = channel.GetEntitiesInAoi(caster.X, caster.Y)
            .Select(id => channel.GetEntity(id))
            .Where(e => e != null && e.Health > 0 && e.Id != caster.Id)
            .Where(e => CanDamageEntity(caster, e!, out _))
            .Select(e => e!)
            .Where(e =>
            {
                if (!isMeleeAssassinSkill)
                    return true;

                float dx = e.X - caster.X;
                float dy = e.Y - caster.Y;
                return MathF.Sqrt(dx * dx + dy * dy) <= MeleeSkillRange;
            })
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
            SendSystemMessage(peer, "Nenhum alvo vÃ¡lido para a habilidade.");
            return false;
        }

        foreach (var target in targets)
        {
            int damage = CalculateSkillDamage(caster, target, skill, out bool isCrit);
            int healthDamage = ApplyDamageToEntity(channel, target, damage);
            ApplyOffensiveSustain(channel, caster, healthDamage);
            if (target is PlayerEntity playerTarget)
            {
                playerTarget.LastCombatTime = _gameTime;
                BroadcastPartyMemberUpdateForEntity(playerTarget.Id);
                ApplyDamageReflect(channel, playerTarget, caster, healthDamage);
            }
            if (target is MonsterEntity hitMob)
                hitMob.TargetEntityId = caster.Id;

            BroadcastCombatResult(channel, caster.Id, target.Id, damage, isCrit, target.Health, target.MaxHealth, caster.X, caster.Y, skill.SkillId);
            ApplySkillDebuff(channel, target, skill);

            if (target is MonsterEntity killedMob && target.Health <= 0)
                HandleMonsterDeath(channel, killedMob, caster, session, target.Id);
        }

        return true;
    }

    private bool ApplyProvocacaoSkill(NetPeer peer, Channel channel, PlayerEntity caster, ServerSkillDefinition skill)
    {
        float duration = Math.Max(1f, skill.Duracao);
        var targets = channel.GetEntitiesInAoi(caster.X, caster.Y)
            .Select(id => channel.GetEntity(id) as MonsterEntity)
            .Where(m => m != null && m.Health > 0)
            .Select(m => m!)
            .Where(m =>
            {
                float dx = m.X - caster.X;
                float dy = m.Y - caster.Y;
                return MathF.Sqrt(dx * dx + dy * dy) <= ProvocacaoRadius;
            })
            .OrderBy(m =>
            {
                float dx = m.X - caster.X;
                float dy = m.Y - caster.Y;
                return dx * dx + dy * dy;
            })
            .Take(Math.Max(1, Math.Max(skill.MaxTargets, 12)))
            .ToList();

        BroadcastCombatResult(channel, caster.Id, caster.Id, 0, false, caster.Health, caster.MaxHealth, caster.X, caster.Y, skill.SkillId);

        foreach (var mob in targets)
        {
            mob.TargetEntityId = caster.Id;
            mob.ActiveServerBuffs["taunt"] = _gameTime + duration;
            mob.ActiveServerBuffs[$"taunt:{caster.Id}"] = _gameTime + duration;
            mob.AIState = MonsterAIState.Chase;
            BroadcastCombatResult(channel, caster.Id, mob.Id, 0, false, mob.Health, mob.MaxHealth, mob.X, mob.Y, skill.SkillId);
            BroadcastSingleEntityUpdate(channel, mob);
        }

        SendSystemMessage(peer, targets.Count > 0
            ? $"{skill.Nome}: {targets.Count} mob(s) provocado(s)."
            : $"{skill.Nome}: nenhum mob dentro da area.");
        return true;
    }

    private bool ApplyDesafioSkill(NetPeer peer, Channel channel, PlayerEntity caster, ServerSkillDefinition skill, float targetX, float targetY)
    {
        float duration = Math.Max(1f, skill.Duracao);
        var mob = channel.GetEntitiesInAoi(caster.X, caster.Y)
            .Select(id => channel.GetEntity(id) as MonsterEntity)
            .Where(m => m != null && m.Health > 0)
            .Select(m => m!)
            .Where(m =>
            {
                float dxCaster = m.X - caster.X;
                float dyCaster = m.Y - caster.Y;
                return MathF.Sqrt(dxCaster * dxCaster + dyCaster * dyCaster) <= DesafioRange;
            })
            .Where(m =>
            {
                float dxTarget = m.X - targetX;
                float dyTarget = m.Y - targetY;
                return MathF.Sqrt(dxTarget * dxTarget + dyTarget * dyTarget) <= DesafioTargetPickRadius;
            })
            .OrderBy(m =>
            {
                float dx = m.X - targetX;
                float dy = m.Y - targetY;
                return dx * dx + dy * dy;
            })
            .FirstOrDefault();

        if (mob == null)
        {
            SendSystemMessage(peer, $"{skill.Nome}: nenhum alvo valido em ate 10 tiles.");
            return false;
        }

        BroadcastCombatResult(channel, caster.Id, caster.Id, 0, false, caster.Health, caster.MaxHealth, caster.X, caster.Y, skill.SkillId);

        mob.TargetEntityId = caster.Id;
        mob.ActiveServerBuffs["taunt"] = _gameTime + duration;
        mob.ActiveServerBuffs[$"taunt:{caster.Id}"] = _gameTime + duration;
        mob.AIState = MonsterAIState.Chase;
        BroadcastCombatResult(channel, caster.Id, mob.Id, 0, false, mob.Health, mob.MaxHealth, mob.X, mob.Y, skill.SkillId);
        BroadcastSingleEntityUpdate(channel, mob);

        SendSystemMessage(peer, $"{skill.Nome}: {mob.Name} provocado.");
        return true;
    }

    private bool ApplyBastiaoSkill(NetPeer peer, Channel channel, PlayerEntity caster, ServerSkillDefinition skill)
    {
        int? partyId = _world.Parties.GetPlayerPartyId(caster.Id);
        int reductionPercent = skill.Valor > 0 ? skill.Valor : 10;
        reductionPercent = Math.Clamp(reductionPercent, 1, 80);
        float duration = Math.Max(1f, skill.Duracao);

        var oldAreas = _activeBastionAreas
            .Where(a => a.ChannelId == channel.Id && a.CasterId == caster.Id)
            .ToList();
        foreach (var oldArea in oldAreas)
            BroadcastSkillAreaEffect(channel, null, BastiaoSkillId, oldArea.X, oldArea.Y, oldArea.Radius, 0f);
        _activeBastionAreas.RemoveAll(a => a.ChannelId == channel.Id && a.CasterId == caster.Id);
        var area = new ActiveBastionArea
        {
            ChannelId = channel.Id,
            CasterId = caster.Id,
            PartyId = partyId ?? 0,
            X = caster.X,
            Y = caster.Y,
            Radius = BastiaoRadius,
            ReductionPercent = reductionPercent,
            ExpiresAt = _gameTime + duration,
            MaxHits = BastiaoMaxHits,
        };
        _activeBastionAreas.Add(area);

        BroadcastSkillAreaEffect(channel, peer, skill.SkillId, area.X, area.Y, area.Radius, duration);
        SendStatusEffectToPlayer(channel, caster.Id, "bastiao", skill.Nome, false, duration, reductionPercent, "res://skills/Incone Skills/Guerreiro/21.png");
        SendSystemMessage(peer, $"{skill.Nome}: aliados da party em 3 tiles recebem {reductionPercent}% de reducao de dano.");
        return true;
    }

    private bool ApplyEspinhosSkill(NetPeer peer, Channel channel, PlayerEntity caster, ServerSkillDefinition skill)
    {
        bool hasTargets = channel.GetEntitiesInAoi(caster.X, caster.Y)
            .Select(id => channel.GetEntity(id))
            .Where(e => e != null && e.Health > 0 && e.Id != caster.Id)
            .Where(e => CanDamageEntity(caster, e!, out _))
            .Any(e =>
            {
                float dx = e!.X - caster.X;
                float dy = e.Y - caster.Y;
                return MathF.Sqrt(dx * dx + dy * dy) <= EspinhosRadius;
            });

        if (!hasTargets)
        {
            SendSystemMessage(peer, "Nenhum inimigo dentro da area dos Espinhos.");
            return false;
        }

        for (int i = 0; i < EspinhosTickCount; i++)
        {
            _pendingAreaSkillTicks.Add(new PendingAreaSkillTick
            {
                TickAt = _gameTime + i * EspinhosTickInterval,
                ChannelId = channel.Id,
                CasterId = caster.Id,
                Skill = skill,
                Radius = EspinhosRadius,
                TickCount = EspinhosTickCount,
                AppliesSlow = true,
                SlowDuration = EspinhosSlowDuration,
            });
        }

        BroadcastSkillAreaEffect(channel, peer, skill.SkillId, caster.X, caster.Y, EspinhosRadius, (float)(EspinhosTickCount * EspinhosTickInterval));
        return true;
    }

    private bool ApplyGolpesFreneticosSkill(NetPeer peer, Channel channel, PlayerEntity caster, ServerSkillDefinition skill, float targetX, float targetY)
    {
        float dirX = targetX - caster.X;
        float dirY = targetY - caster.Y;
        float len = MathF.Sqrt(dirX * dirX + dirY * dirY);
        if (len < 0.001f)
        {
            SendSystemMessage(peer, "Vire para uma direcao antes de usar Golpes Freneticos.");
            return false;
        }

        dirX /= len;
        dirY /= len;
        caster.DirX = dirX;
        caster.DirY = dirY;

        const float range = TileSize * 5f;
        const float hitRadius = 44f;
        float originX = caster.X + dirX * 38f;
        float originY = caster.Y + dirY * 38f;
        if (FindFirstProjectileHit(channel, caster, originX, originY, dirX, dirY, range, hitRadius) == null)
        {
            SendSystemMessage(peer, $"{skill.Nome}: nenhum alvo valido na direcao.");
            return false;
        }

        const int strikeCount = 3;
        const double strikeInterval = 0.18;
        for (int i = 0; i < strikeCount; i++)
        {
            _pendingFreneticStrikes.Add(new PendingFreneticStrike
            {
                StrikeAt = _gameTime + i * strikeInterval,
                ChannelId = channel.Id,
                CasterId = caster.Id,
                DirX = dirX,
                DirY = dirY,
                Range = range,
                HitRadius = hitRadius,
                Skill = skill,
                StrikeCount = strikeCount,
            });
        }

        return true;
    }

    private bool ApplySangramentoMortalSkill(NetPeer peer, Channel channel, PlayerEntity caster, PlayerSession session, ServerSkillDefinition skill, float targetX, float targetY)
    {
        Entity? target = FindTargetNearPosition(channel, caster, targetX, targetY, SangramentoMortalRange, TileSize * 2.5f);

        float dirX;
        float dirY;
        if (target != null)
        {
            dirX = target.X - caster.X;
            dirY = target.Y - caster.Y;
        }
        else
        {
            dirX = targetX - caster.X;
            dirY = targetY - caster.Y;
        }

        float len = MathF.Sqrt(dirX * dirX + dirY * dirY);
        if (len < 0.001f)
        {
            dirX = caster.DirX;
            dirY = caster.DirY;
            len = MathF.Sqrt(dirX * dirX + dirY * dirY);
        }

        if (len < 0.001f)
        {
            SendSystemMessage(peer, "Vire para uma direcao antes de usar Sangramento Mortal.");
            return false;
        }

        dirX /= len;
        dirY /= len;

        const float originOffset = 42f;
        float originX = caster.X + dirX * originOffset;
        float originY = caster.Y + dirY * originOffset;

        target ??= FindFirstProjectileHit(channel, caster, originX, originY, dirX, dirY, SangramentoMortalRange, 38f);
        if (target == null)
        {
            SendSystemMessage(peer, "Sangramento Mortal: nenhum alvo valido em ate 10 tiles.");
            return false;
        }

        float distanceToTarget = MathF.Sqrt((target.X - caster.X) * (target.X - caster.X) + (target.Y - caster.Y) * (target.Y - caster.Y));
        if (distanceToTarget > SangramentoMortalRange + 16f)
        {
            SendSystemMessage(peer, "Sangramento Mortal: alvo fora do alcance.");
            return false;
        }

        BroadcastProjectileSpawn(channel, caster.Id, originX, originY, dirX, dirY, 3, includeCaster: true);
        BroadcastPlayerAction(channel, caster, PlayerActionAttack, dirX, dirY, includeSelf: true);

        const float projectileSpeed = 760f;
        float along = MathF.Max(0f, ProjectileHitDistance(originX, originY, dirX, dirY, target.X, target.Y, SangramentoMortalRange).Along);
        _pendingProjectileHits.Add(new PendingProjectileHit
        {
            ImpactAt = _gameTime + Math.Clamp(along / projectileSpeed, 0.06f, 0.8f),
            ChannelId = session.ChannelId,
            CasterId = caster.Id,
            TargetId = target.Id,
            Skill = skill,
        });

        return true;
    }

    private void ProcessPendingFreneticStrikes()
    {
        for (int i = _pendingFreneticStrikes.Count - 1; i >= 0; i--)
        {
            var pending = _pendingFreneticStrikes[i];
            if (pending.StrikeAt > _gameTime)
                continue;

            _pendingFreneticStrikes.RemoveAt(i);
            var channel = _world.GetChannel(pending.ChannelId);
            if (channel == null)
                continue;

            if (channel.GetEntity(pending.CasterId) is not PlayerEntity caster || caster.Health <= 0)
                continue;

            var session = _sessions.Values.FirstOrDefault(s => s.EntityId == pending.CasterId);
            if (session == null || session.SelectedCharacter == null)
                continue;

            BroadcastPlayerAction(channel, caster, PlayerActionGolpesFreneticos, pending.DirX, pending.DirY, includeSelf: true);

            float originX = caster.X + pending.DirX * 38f;
            float originY = caster.Y + pending.DirY * 38f;
            var target = FindFirstProjectileHit(channel, caster, originX, originY, pending.DirX, pending.DirY, pending.Range, pending.HitRadius);
            if (target == null || target.Health <= 0 || !CanDamageEntity(caster, target, out _))
                continue;

            int fullDamage = CalculateSkillDamage(caster, target, pending.Skill, out bool isCrit);
            int damage = Math.Max(1, (int)MathF.Round(fullDamage / MathF.Max(1, pending.StrikeCount)));
            int healthDamage = ApplyDamageToEntity(channel, target, damage);
            if (healthDamage > 0)
            {
                ApplyOffensiveSustain(channel, caster, healthDamage);
                if (target is PlayerEntity reflectedPlayer)
                {
                    BroadcastPartyMemberUpdateForEntity(reflectedPlayer.Id);
                    ApplyDamageReflect(channel, reflectedPlayer, caster, healthDamage);
                }
            }

            if (target is MonsterEntity hitMob)
                hitMob.TargetEntityId = caster.Id;

            BroadcastCombatResult(channel, caster.Id, target.Id, damage, isCrit, target.Health, target.MaxHealth, target.X, target.Y, pending.Skill.SkillId);
            if (target is MonsterEntity killedMob && target.Health <= 0)
                HandleMonsterDeath(channel, killedMob, caster, session, target.Id);
        }
    }

    private bool ApplyMachadoGiratorioSkill(NetPeer peer, Channel channel, PlayerEntity caster, PlayerSession session, ServerSkillDefinition skill)
    {
        BroadcastCombatResult(channel, caster.Id, caster.Id, 0, false, caster.Health, caster.MaxHealth, caster.X, caster.Y, skill.SkillId);

        for (int i = 0; i < MachadoGiratorioTickCount; i++)
        {
            _pendingAreaSkillTicks.Add(new PendingAreaSkillTick
            {
                TickAt = _gameTime + i * MachadoGiratorioTickInterval,
                ChannelId = channel.Id,
                CasterId = caster.Id,
                Skill = skill,
                Radius = MachadoGiratorioRadius,
                TickCount = MachadoGiratorioTickCount,
            });
        }

        return true;
    }

    private bool ApplyLancaDeGeloSkill(NetPeer peer, Channel channel, PlayerEntity caster, PlayerSession session, ServerSkillDefinition skill, float targetX, float targetY)
    {
        float dxTarget = targetX - caster.X;
        float dyTarget = targetY - caster.Y;
        if (MathF.Sqrt(dxTarget * dxTarget + dyTarget * dyTarget) > LancaDeGeloRange)
        {
            SendSystemMessage(peer, "Area fora do alcance.");
            return false;
        }

        var targets = channel.GetEntitiesInAoi(caster.X, caster.Y)
            .Select(id => channel.GetEntity(id))
            .Where(e => e != null && e.Health > 0 && e.Id != caster.Id)
            .Where(e => CanDamageEntity(caster, e!, out _))
            .Select(e => e!)
            .Where(e =>
            {
                float dx = e.X - targetX;
                float dy = e.Y - targetY;
                return MathF.Sqrt(dx * dx + dy * dy) <= LancaDeGeloRadius;
            })
            .OrderBy(e =>
            {
                float dx = e.X - targetX;
                float dy = e.Y - targetY;
                return dx * dx + dy * dy;
            })
            .Take(Math.Max(1, Math.Max(skill.MaxTargets, 6)))
            .ToList();

        if (targets.Count == 0)
        {
            SendSystemMessage(peer, "Nenhum alvo valido na area da Lanca de Gelo.");
            return false;
        }

        foreach (var target in targets)
        {
            int damage = CalculateSkillDamage(caster, target, skill, out bool isCrit);
            int healthDamage = ApplyDamageToEntity(channel, target, damage);
            ApplyOffensiveSustain(channel, caster, healthDamage);

            if (target is PlayerEntity playerTarget)
            {
                playerTarget.LastCombatTime = _gameTime;
                BroadcastPartyMemberUpdateForEntity(playerTarget.Id);
                ApplyDamageReflect(channel, playerTarget, caster, healthDamage);
            }
            if (target is MonsterEntity hitMob)
                hitMob.TargetEntityId = caster.Id;

            BroadcastCombatResult(channel, caster.Id, target.Id, damage, isCrit, target.Health, target.MaxHealth, targetX, targetY, skill.SkillId);
            ApplySkillDebuff(channel, target, skill);

            if (target is MonsterEntity killedMob && target.Health <= 0)
                HandleMonsterDeath(channel, killedMob, caster, session, target.Id);
        }

        return true;
    }

    private bool ApplyTornadoSkill(NetPeer peer, Channel channel, PlayerEntity caster, PlayerSession session, ServerSkillDefinition skill, float targetX, float targetY)
    {
        float dxTarget = targetX - caster.X;
        float dyTarget = targetY - caster.Y;
        if (MathF.Sqrt(dxTarget * dxTarget + dyTarget * dyTarget) > TornadoRange)
        {
            SendSystemMessage(peer, "Area fora do alcance.");
            return false;
        }

        var targets = channel.GetAllEntities().Values
            .Where(e => e.Health > 0 && e.Id != caster.Id)
            .Where(e => CanDamageEntity(caster, e, out _))
            .Where(e =>
            {
                float dx = e.X - targetX;
                float dy = e.Y - targetY;
                return MathF.Sqrt(dx * dx + dy * dy) <= TornadoRadius;
            })
            .OrderBy(e =>
            {
                float dx = e.X - targetX;
                float dy = e.Y - targetY;
                return dx * dx + dy * dy;
            })
            .Take(Math.Max(1, Math.Max(skill.MaxTargets, 12)))
            .ToList();

        if (targets.Count == 0)
        {
            SendSystemMessage(peer, "Nenhum alvo valido na area do Tornado.");
            return false;
        }

        BroadcastSkillAreaEffect(channel, peer, skill.SkillId, targetX, targetY, TornadoRadius, 1.65f);

        foreach (var target in targets)
        {
            PullEntityIntoTornado(channel, target, targetX, targetY);

            int damage = CalculateSkillDamage(caster, target, skill, out bool isCrit);
            int healthDamage = ApplyDamageToEntity(channel, target, damage);
            ApplyOffensiveSustain(channel, caster, healthDamage);

            if (target is PlayerEntity playerTarget)
            {
                playerTarget.LastCombatTime = _gameTime;
                BroadcastPartyMemberUpdateForEntity(playerTarget.Id);
                ApplyDamageReflect(channel, playerTarget, caster, healthDamage);
            }
            if (target is MonsterEntity hitMob)
            {
                hitMob.TargetEntityId = caster.Id;
                hitMob.AIState = MonsterAIState.Idle;
                hitMob.Moving = false;
                hitMob.ActiveServerBuffs["root"] = _gameTime + 1.15;
            }

            BroadcastCombatResult(channel, caster.Id, target.Id, damage, isCrit, target.Health, target.MaxHealth, targetX, targetY, skill.SkillId);
            ApplySkillDebuff(channel, target, skill);

            if (target is MonsterEntity killedMob && target.Health <= 0)
                HandleMonsterDeath(channel, killedMob, caster, session, target.Id);
        }

        return true;
    }

    private void BroadcastSkillAreaEffect(Channel channel, NetPeer? guaranteedPeer, int skillId, float x, float y, float radius, float duration)
    {
        static NetDataWriter CreateWriter(int skillId, float x, float y, float radius, float duration)
        {
            var writer = PacketSerializer.WritePacket(PacketId.S2C_SkillAreaEffect);
            writer.Put(skillId);
            writer.Put(x);
            writer.Put(y);
            writer.Put(radius);
            writer.Put(duration);
            return writer;
        }

        var sentPeers = new HashSet<NetPeer>();
        if (guaranteedPeer != null)
        {
            guaranteedPeer.Send(CreateWriter(skillId, x, y, radius, duration), DeliveryMethod.ReliableOrdered);
            sentPeers.Add(guaranteedPeer);
        }

        var aoi = channel.GetEntitiesInAoi(x, y);
        foreach (var eid in aoi)
        {
            var p = channel.GetPlayerPeer(eid);
            if (p == null || sentPeers.Contains(p))
                continue;

            p.Send(CreateWriter(skillId, x, y, radius, duration), DeliveryMethod.ReliableOrdered);
            sentPeers.Add(p);
        }
    }

    private void BroadcastSkillVisualEffect(Channel channel, ulong casterId, ulong targetId, int skillId, float x, float y, float duration)
    {
        static NetDataWriter CreateWriter(ulong casterId, ulong targetId, int skillId, float x, float y, float duration)
        {
            var writer = PacketSerializer.WritePacket(PacketId.S2C_SkillVisualEffect);
            writer.Put(casterId);
            writer.Put(targetId);
            writer.Put(skillId);
            writer.Put(x);
            writer.Put(y);
            writer.Put(duration);
            return writer;
        }

        var recipients = channel.GetEntitiesInAoi(x, y);
        if (channel.GetEntity(casterId) is { } caster)
        {
            recipients.Add(casterId);
            recipients.UnionWith(channel.GetEntitiesInAoi(caster.X, caster.Y));
        }
        if (targetId != 0 && channel.GetEntity(targetId) is { } target)
        {
            recipients.Add(targetId);
            recipients.UnionWith(channel.GetEntitiesInAoi(target.X, target.Y));
        }

        foreach (var eid in recipients)
        {
            var p = channel.GetPlayerPeer(eid);
            if (p == null)
                continue;

            p.Send(CreateWriter(casterId, targetId, skillId, x, y, duration), DeliveryMethod.ReliableOrdered);
        }
    }

    private void PullEntityIntoTornado(Channel channel, Entity target, float centerX, float centerY)
    {
        float dx = target.X - centerX;
        float dy = target.Y - centerY;
        float distance = MathF.Sqrt(dx * dx + dy * dy);
        float tangentSign = target.Id % 2UL == 0UL ? 1f : -1f;

        if (distance < 1f)
        {
            float closeX = centerX + tangentSign * TileSize * 0.35f;
            float closeY = centerY - TileSize * 0.65f;
            channel.MoveEntity(target.Id, closeX, closeY);
            BroadcastPulledEntity(channel, target, closeX, closeY);
            return;
        }

        float nx = dx / distance;
        float ny = dy / distance;
        float finalDistance = MathF.Min(TileSize * 0.55f, distance * 0.08f);
        float swirlOffset = MathF.Min(TileSize * 0.55f, MathF.Max(TileSize * 0.2f, distance * 0.10f));
        float liftOffset = MathF.Min(TileSize * 1.05f, MathF.Max(TileSize * 0.45f, distance * 0.18f));
        float pulledX = centerX + nx * finalDistance + (-ny * tangentSign * swirlOffset);
        float pulledY = centerY + ny * finalDistance + (nx * tangentSign * swirlOffset) - liftOffset;

        channel.MoveEntity(target.Id, pulledX, pulledY);
        BroadcastPulledEntity(channel, target, pulledX, pulledY);
    }

    private void BroadcastPulledEntity(Channel channel, Entity target, float pulledX, float pulledY)
    {
        if (target is PlayerEntity player)
        {
            player.Moving = false;
            player.Sprinting = false;
            BroadcastAuthoritativeMove(channel, player, pulledX, pulledY, player.DirX, player.DirY);
        }
        else
        {
            BroadcastSingleEntityUpdate(channel, target);
        }
    }

    private bool ApplySonoArcanoSkill(NetPeer peer, Channel channel, PlayerEntity caster, ServerSkillDefinition skill, float targetX, float targetY)
    {
        const float range = TileSize * 18f;
        var target = channel.GetEntitiesInAoi(caster.X, caster.Y)
            .Select(id => channel.GetEntity(id))
            .Where(e => e != null && e.Health > 0 && e.Id != caster.Id)
            .Where(e => CanDamageEntity(caster, e!, out _))
            .Select(e => e!)
            .Where(e =>
            {
                float dxCaster = e.X - caster.X;
                float dyCaster = e.Y - caster.Y;
                return MathF.Sqrt(dxCaster * dxCaster + dyCaster * dyCaster) <= range;
            })
            .OrderBy(e =>
            {
                float dx = e.X - targetX;
                float dy = e.Y - targetY;
                return dx * dx + dy * dy;
            })
            .FirstOrDefault();

        if (target == null)
        {
            SendSystemMessage(peer, "Nenhum alvo valido para Sono Arcano.");
            return false;
        }

        ApplySkillDebuff(channel, target, skill);

        if (target is PlayerEntity playerTarget)
        {
            playerTarget.Moving = false;
            playerTarget.Sprinting = false;
            BroadcastAuthoritativeMove(channel, playerTarget, playerTarget.X, playerTarget.Y, playerTarget.DirX, playerTarget.DirY);
        }
        else if (target is MonsterEntity mobTarget)
        {
            mobTarget.TargetEntityId = null;
            BroadcastSingleEntityUpdate(channel, mobTarget);
        }

        BroadcastCombatResult(channel, caster.Id, target.Id, 0, false, target.Health, target.MaxHealth, target.X, target.Y, skill.SkillId);
        return true;
    }

    private bool ApplyChuvaMeteorosSkill(NetPeer peer, Channel channel, PlayerEntity caster, PlayerSession session, ServerSkillDefinition skill, float targetX, float targetY)
    {
        float dxTarget = targetX - caster.X;
        float dyTarget = targetY - caster.Y;
        if (MathF.Sqrt(dxTarget * dxTarget + dyTarget * dyTarget) > ChuvaMeteorosRange)
        {
            SendSystemMessage(peer, "Area fora do alcance.");
            return false;
        }

        var nearbyIds = channel.GetEntitiesInAoi(caster.X, caster.Y);
        nearbyIds.UnionWith(channel.GetEntitiesInAoi(targetX, targetY));

        var targets = nearbyIds
            .Select(id => channel.GetEntity(id))
            .Where(e => e != null && e.Health > 0 && e.Id != caster.Id)
            .Where(e => CanDamageEntity(caster, e!, out _))
            .Select(e => e!)
            .Where(e =>
            {
                float dx = e.X - targetX;
                float dy = e.Y - targetY;
                return MathF.Sqrt(dx * dx + dy * dy) <= ChuvaMeteorosRadius;
            })
            .OrderBy(e =>
            {
                float dx = e.X - targetX;
                float dy = e.Y - targetY;
                return dx * dx + dy * dy;
            })
            .Take(Math.Max(1, Math.Max(skill.MaxTargets, 10)))
            .ToList();

        if (targets.Count == 0)
        {
            SendSystemMessage(peer, "Nenhum alvo valido na area da Chuva de Meteoros.");
            return false;
        }

        foreach (var target in targets)
        {
            int damage = CalculateSkillDamage(caster, target, skill, out bool isCrit);
            int healthDamage = ApplyDamageToEntity(channel, target, damage);
            ApplyOffensiveSustain(channel, caster, healthDamage);

            if (target is PlayerEntity playerTarget)
            {
                playerTarget.LastCombatTime = _gameTime;
                BroadcastPartyMemberUpdateForEntity(playerTarget.Id);
                ApplyDamageReflect(channel, playerTarget, caster, healthDamage);
            }
            if (target is MonsterEntity hitMob)
                hitMob.TargetEntityId = caster.Id;

            BroadcastCombatResult(channel, caster.Id, target.Id, damage, isCrit, target.Health, target.MaxHealth, targetX, targetY, skill.SkillId);

            if (target is MonsterEntity killedMob && target.Health <= 0)
                HandleMonsterDeath(channel, killedMob, caster, session, target.Id);
        }

        return true;
    }

    private static bool IsGolpeSombrioSkill(ServerSkillDefinition skill)
    {
        return skill.SkillId == GolpeSombrioSkillId
            || (!string.IsNullOrWhiteSpace(skill.Nome)
                && skill.Nome.Contains("Golpe", StringComparison.OrdinalIgnoreCase)
                && skill.Nome.Contains("Sombrio", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsPunhaladaNasCostasSkill(ServerSkillDefinition skill)
    {
        return skill.SkillId == PunhaladaNasCostasSkillId
            || (!string.IsNullOrWhiteSpace(skill.Nome)
                && skill.Nome.Contains("Punhalada", StringComparison.OrdinalIgnoreCase)
                && skill.Nome.Contains("Costas", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsGolpeAtordoanteSkill(ServerSkillDefinition skill)
    {
        return skill.SkillId == GolpeAtordoanteSkillId
            || (!string.IsNullOrWhiteSpace(skill.Nome)
                && skill.Nome.Contains("Golpe", StringComparison.OrdinalIgnoreCase)
                && skill.Nome.Contains("Atordoante", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsChuteNaVirilhaSkill(ServerSkillDefinition skill)
    {
        return skill.SkillId == ChuteNaVirilhaSkillId
            || (!string.IsNullOrWhiteSpace(skill.Nome)
                && skill.Nome.Contains("Chute", StringComparison.OrdinalIgnoreCase)
                && skill.Nome.Contains("Virilha", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsMarcaDaMorteSkill(ServerSkillDefinition skill)
    {
        return skill.SkillId == MarcaDaMorteAssassinoSkillId
            || skill.SkillId == MarcaDaMorteSlayerSkillId
            || (!string.IsNullOrWhiteSpace(skill.Nome)
                && skill.Nome.Contains("Marca", StringComparison.OrdinalIgnoreCase)
                && skill.Nome.Contains("Morte", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsExecucaoFinalSkill(ServerSkillDefinition skill)
    {
        return skill.SkillId == ExecucaoFinalSkillId
            || (!string.IsNullOrWhiteSpace(skill.Nome)
                && skill.Nome.Contains("Execu", StringComparison.OrdinalIgnoreCase)
                && skill.Nome.Contains("Final", StringComparison.OrdinalIgnoreCase));
    }

    private bool ApplyExecucaoFinalSkill(NetPeer peer, Channel channel, PlayerEntity caster, PlayerSession session, ServerSkillDefinition skill, float targetX, float targetY)
    {
        var target = channel.GetEntitiesInAoi(caster.X, caster.Y)
            .Select(id => channel.GetEntity(id))
            .Where(e => e != null && e.Health > 0 && e.Id != caster.Id)
            .Where(e => CanDamageEntity(caster, e!, out _))
            .Select(e => e!)
            .Where(e =>
            {
                float dxCaster = e.X - caster.X;
                float dyCaster = e.Y - caster.Y;
                return MathF.Sqrt(dxCaster * dxCaster + dyCaster * dyCaster) <= MeleeSkillRange;
            })
            .OrderBy(e =>
            {
                float dx = e.X - targetX;
                float dy = e.Y - targetY;
                return dx * dx + dy * dy;
            })
            .FirstOrDefault();

        if (target == null)
        {
            SendSystemMessage(peer, "Nenhum alvo em alcance para Execucao Final.");
            return false;
        }

        int damage = CalculateSkillDamage(caster, target, skill, out bool isCrit);
        int healthDamage = ApplyDamageToEntity(channel, target, damage);
        ApplyOffensiveSustain(channel, caster, healthDamage);
        if (target is PlayerEntity playerTarget)
        {
            playerTarget.LastCombatTime = _gameTime;
            BroadcastPartyMemberUpdateForEntity(playerTarget.Id);
            ApplyDamageReflect(channel, playerTarget, caster, healthDamage);
        }
        if (target is MonsterEntity hitMob)
            hitMob.TargetEntityId = caster.Id;

        caster.LastCombatTime = _gameTime;
        BroadcastCombatResult(channel, caster.Id, target.Id, damage, isCrit, target.Health, target.MaxHealth, caster.X, caster.Y, skill.SkillId);

        if (target is MonsterEntity killedMob && target.Health <= 0)
            HandleMonsterDeath(channel, killedMob, caster, session, target.Id);

        return true;
    }

    private bool ApplyInvisibilitySkill(NetPeer peer, Channel channel, PlayerEntity caster, ServerSkillDefinition skill)
    {
        float duration = Math.Max(1f, skill.Duracao);
        string buffId = $"skill:{skill.SkillId}";
        caster.ActiveServerBuffs[buffId] = _gameTime + duration;
        channel.ClearMonsterAggroOn(caster.Id);

        SendStatusEffectToPlayer(channel, caster.Id, "invisibility", skill.Nome, false, duration, 0, "res://skills/Incone Skills/Assasino/43.png");
        BroadcastPlayerAction(channel, caster, PlayerActionInvisibility, duration, 0f, includeSelf: false);
        SendSystemMessage(peer, $"{skill.Nome}: voce desapareceu nas sombras.");
        return true;
    }

    private bool ApplyPassoSombrioSkill(NetPeer peer, Channel channel, PlayerEntity caster, PlayerSession session, ServerSkillDefinition skill, float targetX, float targetY)
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

        float distance = TileSize * 5f;
        float newX = caster.X + dirX * distance;
        float newY = caster.Y + dirY * distance;
        ResolveMapCollision(session.CurrentMap, caster.X, caster.Y, ref newX, ref newY);

        caster.DirX = dirX;
        caster.DirY = dirY;
        caster.Moving = false;
        caster.Sprinting = false;
        channel.MoveEntity(caster.Id, newX, newY);
        BroadcastAuthoritativeMove(channel, caster, newX, newY, dirX, dirY);

        float duration = Math.Max(1f, skill.Duracao);
        caster.ActiveServerBuffs[$"skill:{skill.SkillId}"] = _gameTime + duration;
        channel.ClearMonsterAggroOn(caster.Id);
        SendStatusEffectToPlayer(channel, caster.Id, "invisibility", skill.Nome, false, duration, 0, "res://skills/Incone Skills/Assasino/41.png");
        BroadcastPlayerAction(channel, caster, PlayerActionInvisibility, duration, 0f, includeSelf: false);
        SendSystemMessage(peer, $"{skill.Nome}: voce avancou pelas sombras.");
        return true;
    }

    private bool ApplySaltoNasCostasSkill(NetPeer peer, Channel channel, PlayerEntity caster, PlayerSession session, ServerSkillDefinition skill, float targetX, float targetY)
    {
        var target = FindSaltoNasCostasTarget(channel, caster, targetX, targetY);
        if (target == null)
        {
            SendSystemMessage(peer, "Nenhum alvo valido para Salto nas Costas.");
            return false;
        }

        if (!CanDamageEntity(caster, target, out string denyReason))
        {
            if (!string.IsNullOrWhiteSpace(denyReason))
                SendSystemMessage(peer, denyReason);
            return false;
        }

        float targetFacingX = target.DirX;
        float targetFacingY = target.DirY;
        if (targetFacingX * targetFacingX + targetFacingY * targetFacingY < 0.001f)
        {
            targetFacingX = target.X - caster.X;
            targetFacingY = target.Y - caster.Y;
            if (targetFacingX * targetFacingX + targetFacingY * targetFacingY < 0.001f)
            {
                targetFacingX = 0f;
                targetFacingY = 1f;
            }
        }
        float facingLen = MathF.Sqrt(targetFacingX * targetFacingX + targetFacingY * targetFacingY);
        targetFacingX /= facingLen;
        targetFacingY /= facingLen;
        SnapDirectionToCardinal(ref targetFacingX, ref targetFacingY);

        float targetTileCenterX = MathF.Floor(target.X / TileSize) * TileSize + TileSize * 0.5f;
        float targetTileCenterY = MathF.Floor(target.Y / TileSize) * TileSize + TileSize * 0.5f;
        float newX = targetTileCenterX - targetFacingX * TileSize;
        float newY = targetTileCenterY - targetFacingY * TileSize;

        if (IsBlockedTile(session.CurrentMap, newX, newY))
        {
            SendSystemMessage(peer, "Nao ha espaco livre atras do alvo.");
            return false;
        }

        // O atacante precisa olhar exatamente para o alvo depois do salto.
        // Como ele esta no tile das costas, a direcao correta e a frente cardinal do alvo.
        float lookX = targetFacingX;
        float lookY = targetFacingY;

        caster.DirX = lookX;
        caster.DirY = lookY;
        caster.Moving = false;
        caster.Sprinting = false;
        channel.MoveEntity(caster.Id, newX, newY);
        BroadcastAuthoritativeMove(channel, caster, newX, newY, lookX, lookY);
        BroadcastPlayerAction(channel, caster, PlayerActionAttack, lookX, lookY, includeSelf: true);

        int damage = CalculateSkillDamage(caster, target, skill, out bool isCrit);
        int healthDamage = ApplyDamageToEntity(channel, target, damage);
        ApplyOffensiveSustain(channel, caster, healthDamage);
        if (target is PlayerEntity playerTarget)
        {
            playerTarget.LastCombatTime = _gameTime;
            BroadcastPartyMemberUpdateForEntity(playerTarget.Id);
            ApplyDamageReflect(channel, playerTarget, caster, healthDamage);
        }
        if (target is MonsterEntity hitMob)
            hitMob.TargetEntityId = caster.Id;

        caster.LastCombatTime = _gameTime;
        BroadcastCombatResult(channel, caster.Id, target.Id, damage, isCrit, target.Health, target.MaxHealth, caster.X, caster.Y, skill.SkillId);

        if (target is MonsterEntity killedMob && target.Health <= 0)
            HandleMonsterDeath(channel, killedMob, caster, session, target.Id);

        return true;
    }

    private static void SnapDirectionToCardinal(ref float x, ref float y)
    {
        if (MathF.Abs(x) >= MathF.Abs(y))
        {
            x = x >= 0f ? 1f : -1f;
            y = 0f;
        }
        else
        {
            x = 0f;
            y = y >= 0f ? 1f : -1f;
        }
    }

    private Entity? FindSaltoNasCostasTarget(Channel channel, PlayerEntity caster, float targetX, float targetY)
    {
        return channel.GetEntitiesInAoi(caster.X, caster.Y)
            .Select(id => channel.GetEntity(id))
            .Where(e => e != null && e.Health > 0 && e.Id != caster.Id)
            .Where(e => CanDamageEntity(caster, e!, out _))
            .Select(e => e!)
            .Where(e =>
            {
                float dxCaster = e.X - caster.X;
                float dyCaster = e.Y - caster.Y;
                return MathF.Sqrt(dxCaster * dxCaster + dyCaster * dyCaster) <= SaltoNasCostasRange;
            })
            .OrderBy(e =>
            {
                float dx = e.X - targetX;
                float dy = e.Y - targetY;
                return dx * dx + dy * dy;
            })
            .FirstOrDefault();
    }

    private bool ApplySummonSkill(NetPeer peer, Channel channel, PlayerEntity caster, ServerSkillDefinition skill)
    {
        if (skill.SkillId != 10101)
        {
            SendSystemMessage(peer, "Esta invocaÃ§Ã£o ainda nÃ£o estÃ¡ implementada no servidor.");
            return false;
        }

        const byte summonSheriganAction = 3;
        BroadcastPlayerAction(channel, caster, summonSheriganAction, caster.DirX, caster.DirY, includeSelf: true);
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
        float distance = skill.SkillId switch
        {
            10002 => tileSize * 6f,
            TeleporteArcanoSkillId => tileSize * 10f,
            PassoDivinoSkillId => tileSize * 10f,
            _ => MathF.Max(tileSize * 2f, skill.Valor),
        };
        float originX = caster.X + dirX * 42f;
        float originY = caster.Y + dirY * 42f;

        if (skill.SkillId == 10002)
            FireSkillProjectile(channel, caster, session, skill, originX, originY, dirX, dirY, notifyMiss: false, peer: peer);

        if (skill.SkillId == TeleporteArcanoSkillId)
            BroadcastPlayerAction(channel, caster, PlayerActionArcaneTeleportEnter, dirX, dirY, includeSelf: true);

        float directionSign = skill.SkillId == 10002 ? -1f : 1f;
        float newX = caster.X + dirX * distance * directionSign;
        float newY = caster.Y + dirY * distance * directionSign;
        caster.Moving = false;
        caster.Sprinting = false;
        channel.MoveEntity(caster.Id, newX, newY);
        BroadcastAuthoritativeMove(channel, caster, newX, newY, dirX, dirY);
        if (skill.SkillId == TeleporteArcanoSkillId)
        {
            const float exitPortalBackOffset = 30f;
            BroadcastPlayerAction(
                channel,
                caster,
                PlayerActionArcaneTeleportExit,
                newX - dirX * exitPortalBackOffset,
                newY - dirY * exitPortalBackOffset,
                includeSelf: true);
        }
        return true;
    }

    private bool ApplyInvestidaBrutalSkill(NetPeer peer, Channel channel, PlayerEntity caster, PlayerSession session, ServerSkillDefinition skill, float targetX, float targetY)
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
            SendSystemMessage(peer, "Vire para uma direcao antes de usar Investida Brutal.");
            return false;
        }

        dirX /= len;
        dirY /= len;

        const float distance = TileSize * 10f;
        const float hitRadius = 34f;
        float originX = caster.X;
        float originY = caster.Y;
        float finalX = originX + dirX * distance;
        float finalY = originY + dirY * distance;

        for (float step = TileSize; step <= distance; step += TileSize)
        {
            float checkX = originX + dirX * step;
            float checkY = originY + dirY * step;
            if (IsBlockedTile(session.CurrentMap, checkX, checkY))
            {
                finalX = originX + dirX * MathF.Max(0f, step - TileSize * 0.5f);
                finalY = originY + dirY * MathF.Max(0f, step - TileSize * 0.5f);
                break;
            }
        }

        var hit = channel.GetEntitiesInAoi(originX, originY)
            .Select(id => channel.GetEntity(id))
            .Where(e => e != null && e.Health > 0 && e.Id != caster.Id)
            .Where(e => CanDamageEntity(caster, e!, out _))
            .Select(e => new { Entity = e!, Hit = ProjectileHitDistance(originX, originY, dirX, dirY, e!.X, e.Y, distance) })
            .Where(x => x.Hit.Along >= 0f && x.Hit.Along <= distance && x.Hit.Perpendicular <= hitRadius)
            .OrderBy(x => x.Hit.Along)
            .FirstOrDefault();

        if (hit != null)
        {
            float stopDistance = MathF.Max(0f, hit.Hit.Along - TileSize * 0.65f);
            finalX = originX + dirX * stopDistance;
            finalY = originY + dirY * stopDistance;
        }

        caster.Moving = false;
        caster.Sprinting = false;
        caster.DirX = dirX;
        caster.DirY = dirY;
        channel.MoveEntity(caster.Id, finalX, finalY);
        BroadcastAuthoritativeMove(channel, caster, finalX, finalY, dirX, dirY);

        if (hit == null)
            return true;

        var target = hit.Entity;
        int damage = CalculateSkillDamage(caster, target, skill, out bool isCrit);
        int healthDamage = ApplyDamageToEntity(channel, target, damage);
        if (healthDamage > 0)
        {
            ApplyOffensiveSustain(channel, caster, healthDamage);
            if (target is PlayerEntity reflectedPlayer)
                ApplyDamageReflect(channel, reflectedPlayer, caster, healthDamage);
        }

        BroadcastCombatResult(channel, caster.Id, target.Id, damage, isCrit, target.Health, target.MaxHealth, target.X, target.Y, skill.SkillId);
        if (target is MonsterEntity killedMob && target.Health <= 0)
            HandleMonsterDeath(channel, killedMob, caster, session, target.Id);

        return true;
    }

    private bool ApplyEstocadaSkill(NetPeer peer, Channel channel, PlayerEntity caster, PlayerSession session, ServerSkillDefinition skill, float targetX, float targetY)
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
            SendSystemMessage(peer, "Vire para uma direcao antes de usar Estocada.");
            return false;
        }

        dirX /= len;
        dirY /= len;
        SnapDirectionToCardinal(ref dirX, ref dirY);

        const float distance = TileSize * 5f;
        const float hitRadius = 30f;
        float originX = caster.X;
        float originY = caster.Y;
        float maxDistance = distance;

        for (float step = TileSize; step <= distance; step += TileSize)
        {
            float checkX = originX + dirX * step;
            float checkY = originY + dirY * step;
            if (IsBlockedTile(session.CurrentMap, checkX, checkY))
            {
                maxDistance = MathF.Max(0f, step - TileSize * 0.5f);
                break;
            }
        }

        var hit = channel.GetEntitiesInAoi(originX, originY)
            .Select(id => channel.GetEntity(id))
            .Where(e => e != null && e.Health > 0 && e.Id != caster.Id)
            .Where(e => CanDamageEntity(caster, e!, out _))
            .Select(e => new { Entity = e!, Hit = ProjectileHitDistance(originX, originY, dirX, dirY, e!.X, e.Y, maxDistance) })
            .Where(x => x.Hit.Along >= 0f && x.Hit.Along <= maxDistance && x.Hit.Perpendicular <= hitRadius)
            .OrderBy(x => x.Hit.Along)
            .FirstOrDefault();

        if (hit == null)
        {
            SendSystemMessage(peer, "Nenhum alvo valido na linha da Estocada.");
            return false;
        }

        float stopDistance = MathF.Max(0f, hit.Hit.Along - TileSize * 0.85f);
        float finalX = originX + dirX * stopDistance;
        float finalY = originY + dirY * stopDistance;

        caster.Moving = false;
        caster.Sprinting = false;
        caster.DirX = dirX;
        caster.DirY = dirY;
        channel.MoveEntity(caster.Id, finalX, finalY);
        BroadcastAuthoritativeMove(channel, caster, finalX, finalY, dirX, dirY);
        BroadcastPlayerAction(channel, caster, PlayerActionAttack, dirX, dirY, includeSelf: true);

        var target = hit.Entity;
        int damage = CalculateSkillDamage(caster, target, skill, out bool isCrit);
        int healthDamage = ApplyDamageToEntity(channel, target, damage);
        if (healthDamage > 0)
        {
            ApplyOffensiveSustain(channel, caster, healthDamage);
            if (target is PlayerEntity reflectedPlayer)
                ApplyDamageReflect(channel, reflectedPlayer, caster, healthDamage);
        }

        if (target is PlayerEntity playerTarget)
        {
            playerTarget.LastCombatTime = _gameTime;
            BroadcastPartyMemberUpdateForEntity(playerTarget.Id);
        }
        if (target is MonsterEntity hitMob)
            hitMob.TargetEntityId = caster.Id;

        ApplySkillDebuff(channel, target, skill);
        BroadcastCombatResult(channel, caster.Id, target.Id, damage, isCrit, target.Health, target.MaxHealth, target.X, target.Y, skill.SkillId);

        if (target is MonsterEntity killedMob && target.Health <= 0)
            HandleMonsterDeath(channel, killedMob, caster, session, target.Id);

        return true;
    }

    private bool ApplyGolpeDeEscudoSkill(NetPeer peer, Channel channel, PlayerEntity caster, PlayerSession session, ServerSkillDefinition skill, float targetX, float targetY)
    {
        var target = FindGolpeDeEscudoTarget(channel, caster, targetX, targetY);
        if (target == null)
        {
            SendSystemMessage(peer, "Nenhum alvo valido para Golpe de Escudo.");
            return false;
        }

        float toTargetX = target.X - caster.X;
        float toTargetY = target.Y - caster.Y;
        float len = MathF.Sqrt(toTargetX * toTargetX + toTargetY * toTargetY);
        if (len < 0.001f)
        {
            SendSystemMessage(peer, "Alvo invalido para Golpe de Escudo.");
            return false;
        }

        float dirX = toTargetX / len;
        float dirY = toTargetY / len;
        SnapDirectionToCardinal(ref dirX, ref dirY);

        float sideX = -dirY;
        float sideY = dirX;
        float targetTileCenterX = MathF.Floor(target.X / TileSize) * TileSize + TileSize * 0.5f;
        float targetTileCenterY = MathF.Floor(target.Y / TileSize) * TileSize + TileSize * 0.5f;
        (float X, float Y)[] landingCandidates =
        {
            (targetTileCenterX + sideX * TileSize, targetTileCenterY + sideY * TileSize),
            (targetTileCenterX - sideX * TileSize, targetTileCenterY - sideY * TileSize),
            (targetTileCenterX - dirX * TileSize, targetTileCenterY - dirY * TileSize),
            (targetTileCenterX + dirX * TileSize, targetTileCenterY + dirY * TileSize),
        };

        (float X, float Y)? landing = null;
        foreach (var candidate in landingCandidates)
        {
            if (!IsBlockedTile(session.CurrentMap, candidate.X, candidate.Y))
            {
                landing = candidate;
                break;
            }
        }

        if (landing == null)
        {
            SendSystemMessage(peer, "Nao ha espaco livre ao lado do alvo.");
            return false;
        }

        float lookX = target.X - landing.Value.X;
        float lookY = target.Y - landing.Value.Y;
        float lookLen = MathF.Sqrt(lookX * lookX + lookY * lookY);
        if (lookLen > 0.001f)
        {
            lookX /= lookLen;
            lookY /= lookLen;
            SnapDirectionToCardinal(ref lookX, ref lookY);
        }
        else
        {
            lookX = dirX;
            lookY = dirY;
        }

        caster.Moving = false;
        caster.Sprinting = false;
        caster.DirX = lookX;
        caster.DirY = lookY;
        channel.MoveEntity(caster.Id, landing.Value.X, landing.Value.Y);
        BroadcastAuthoritativeMove(channel, caster, landing.Value.X, landing.Value.Y, lookX, lookY);
        BroadcastPlayerAction(channel, caster, PlayerActionAttack, lookX, lookY, includeSelf: true);

        int damage = CalculateSkillDamage(caster, target, skill, out bool isCrit);
        int healthDamage = ApplyDamageToEntity(channel, target, damage);
        if (healthDamage > 0)
        {
            ApplyOffensiveSustain(channel, caster, healthDamage);
            if (target is PlayerEntity reflectedPlayer)
                ApplyDamageReflect(channel, reflectedPlayer, caster, healthDamage);
        }

        if (target is PlayerEntity playerTarget)
        {
            playerTarget.LastCombatTime = _gameTime;
            BroadcastPartyMemberUpdateForEntity(playerTarget.Id);
        }
        if (target is MonsterEntity hitMob)
            hitMob.TargetEntityId = caster.Id;

        ApplySkillDebuff(channel, target, skill);
        BroadcastCombatResult(channel, caster.Id, target.Id, damage, isCrit, target.Health, target.MaxHealth, target.X, target.Y, skill.SkillId);

        if (target is MonsterEntity killedMob && target.Health <= 0)
            HandleMonsterDeath(channel, killedMob, caster, session, target.Id);

        return true;
    }

    private Entity? FindGolpeDeEscudoTarget(Channel channel, PlayerEntity caster, float targetX, float targetY)
    {
        return channel.GetEntitiesInAoi(caster.X, caster.Y)
            .Select(id => channel.GetEntity(id))
            .Where(e => e != null && e.Health > 0 && e.Id != caster.Id)
            .Where(e => CanDamageEntity(caster, e!, out _))
            .Select(e => e!)
            .Where(e =>
            {
                float dxCaster = e.X - caster.X;
                float dyCaster = e.Y - caster.Y;
                return MathF.Sqrt(dxCaster * dxCaster + dyCaster * dyCaster) <= GolpeDeEscudoRange;
            })
            .OrderBy(e =>
            {
                float dx = e.X - targetX;
                float dy = e.Y - targetY;
                return dx * dx + dy * dy;
            })
            .FirstOrDefault();
    }

    private bool ApplyCarnificinaSkill(NetPeer peer, Channel channel, PlayerEntity caster, PlayerSession session, ServerSkillDefinition skill, float targetX, float targetY)
    {
        const float range = TileSize * 15f;
        const float impactRadius = TileSize * 2.6f;
        float dxLanding = targetX - caster.X;
        float dyLanding = targetY - caster.Y;
        if (MathF.Sqrt(dxLanding * dxLanding + dyLanding * dyLanding) > range)
        {
            SendSystemMessage(peer, "Area fora do alcance da Carnificina.");
            return false;
        }

        if (IsBlockedTile(session.CurrentMap, targetX, targetY))
        {
            SendSystemMessage(peer, "Carnificina: local bloqueado.");
            return false;
        }

        var target = channel.GetEntitiesInAoi(caster.X, caster.Y)
            .Select(id => channel.GetEntity(id))
            .Where(e => e != null && e.Health > 0 && e.Id != caster.Id)
            .Where(e => CanDamageEntity(caster, e!, out _))
            .Select(e => e!)
            .Select(e =>
            {
                float dxTarget = e.X - targetX;
                float dyTarget = e.Y - targetY;
                return new
                {
                    Entity = e,
                    DistTargetSq = dxTarget * dxTarget + dyTarget * dyTarget,
                };
            })
            .Where(x => x.DistTargetSq <= impactRadius * impactRadius)
            .OrderBy(x => x.DistTargetSq)
            .Select(x => x.Entity!)
            .FirstOrDefault();

        if (target == null)
        {
            SendSystemMessage(peer, "Carnificina: nenhum alvo valido na area marcada.");
            return false;
        }

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
            dirX = 0f;
            dirY = 1f;
            len = 1f;
        }

        dirX /= len;
        dirY /= len;

        caster.Moving = false;
        caster.Sprinting = false;
        caster.DirX = dirX;
        caster.DirY = dirY;

        BroadcastPlayerAction(channel, caster, PlayerActionCarnificinaJump, dirX, dirY, includeSelf: true);
        channel.MoveEntity(caster.Id, targetX, targetY);
        BroadcastAuthoritativeMove(channel, caster, targetX, targetY, dirX, dirY);
        BroadcastSkillAreaEffect(channel, peer, skill.SkillId, targetX, targetY, impactRadius, 0.9f);

        int damage = CalculateSkillDamage(caster, target, skill, out bool isCrit);
        int healthDamage = ApplyDamageToEntity(channel, target, damage);
        if (healthDamage > 0)
        {
            ApplyOffensiveSustain(channel, caster, healthDamage);
            if (target is PlayerEntity reflectedPlayer)
            {
                reflectedPlayer.LastCombatTime = _gameTime;
                BroadcastPartyMemberUpdateForEntity(reflectedPlayer.Id);
                ApplyDamageReflect(channel, reflectedPlayer, caster, healthDamage);
            }
        }

        if (target is MonsterEntity hitMob)
        {
            hitMob.TargetEntityId = caster.Id;
            hitMob.ActiveServerBuffs["stun"] = _gameTime + Math.Max(0.25, skill.Duracao);
        }
        else if (target is PlayerEntity playerTarget)
        {
            double finalDuration = Math.Max(0.25, skill.Duracao * (1.0 - Math.Clamp(playerTarget.ControlResistance, 0f, 50f) / 100.0));
            playerTarget.ActiveServerBuffs["stun"] = _gameTime + finalDuration;
            SendStatusEffectToPlayer(channel, playerTarget.Id, "carnificina_stun", skill.Nome, true, (float)finalDuration, 0, "res://skills/Incone Skills/Debuffs/1.png");
        }

        BroadcastCombatResult(channel, caster.Id, target.Id, damage, isCrit, target.Health, target.MaxHealth, target.X, target.Y, skill.SkillId);

        if (target is MonsterEntity killedMob && target.Health <= 0)
            HandleMonsterDeath(channel, killedMob, caster, session, target.Id);

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

    private bool ApplyProjectileSkill(NetPeer peer, Channel channel, PlayerEntity caster, PlayerSession session, ServerSkillDefinition skill, float targetX, float targetY, float chargePercent)
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

        if (IsTripleShotSkill(skill))
        {
            if (FindFirstProjectileHit(channel, caster, originX, originY, dirX, dirY, 760f, 34f) == null)
            {
                SendSystemMessage(peer, $"{skill.Nome}: nenhum alvo valido no alcance.");
                return false;
            }

            ScheduleTripleShotProjectiles(peer, channel, caster, session, skill, originX, originY, dirX, dirY);
            return true;
        }

        return FireSkillProjectile(channel, caster, session, skill, originX, originY, dirX, dirY, notifyMiss: true, peer: peer, chargePercent: chargePercent);
    }

    private static bool IsTripleShotSkill(ServerSkillDefinition skill)
    {
        return skill.SkillId == 10210
            || string.Equals(skill.Nome, "Tiro Triplo", StringComparison.OrdinalIgnoreCase);
    }

    private void ScheduleTripleShotProjectiles(NetPeer peer, Channel channel, PlayerEntity caster, PlayerSession session, ServerSkillDefinition skill, float originX, float originY, float dirX, float dirY)
    {
        const double shotDelay = 0.18;
        for (int i = 0; i < 3; i++)
        {
            _pendingProjectileFires.Add(new PendingProjectileFire
            {
                FireAt = _gameTime + i * shotDelay,
                ChannelId = session.ChannelId,
                CasterId = caster.Id,
                OriginX = originX,
                OriginY = originY,
                DirX = dirX,
                DirY = dirY,
                Skill = skill,
                NotifyMiss = i == 0,
                Peer = peer,
            });
        }

        BroadcastPlayerAction(channel, caster, 1, dirX, dirY, includeSelf: true);
    }

    private void ProcessPendingProjectileFires()
    {
        for (int i = _pendingProjectileFires.Count - 1; i >= 0; i--)
        {
            var pending = _pendingProjectileFires[i];
            if (pending.FireAt > _gameTime)
                continue;

            _pendingProjectileFires.RemoveAt(i);
            var channel = _world.GetChannel(pending.ChannelId);
            if (channel == null)
                continue;

            if (channel.GetEntity(pending.CasterId) is not PlayerEntity caster || caster.Health <= 0)
                continue;

            var session = _sessions.Values.FirstOrDefault(s => s.EntityId == pending.CasterId);
            if (session == null)
                continue;

            FireSkillProjectile(
                channel,
                caster,
                session,
                pending.Skill,
                pending.OriginX,
                pending.OriginY,
                pending.DirX,
                pending.DirY,
                pending.NotifyMiss,
                pending.Peer);
        }
    }

    private bool FireSkillProjectile(Channel channel, PlayerEntity caster, PlayerSession session, ServerSkillDefinition skill, float originX, float originY, float dirX, float dirY, bool notifyMiss, NetPeer? peer, float chargePercent = 1f)
    {
        const float projectileRange = 760f;
        const float projectileRadius = 34f;
        const float projectileSpeed = 900f;

        var target = FindFirstProjectileHit(channel, caster, originX, originY, dirX, dirY, projectileRange, projectileRadius);
        if (target == null)
        {
            if (notifyMiss && peer != null)
                SendSystemMessage(peer, $"{skill.Nome}: nenhum alvo valido no alcance.");
            return false;
        }

        BroadcastProjectileSpawn(channel, caster.Id, originX, originY, dirX, dirY, ProjectileTypeForSkill(caster, skill), includeCaster: true);

        float along = ProjectileHitDistance(originX, originY, dirX, dirY, target.X, target.Y, projectileRange).Along;
        double impactAt = _gameTime + Math.Clamp(along / projectileSpeed, 0.05f, 1.8f);
        _pendingProjectileHits.Add(new PendingProjectileHit
        {
            ImpactAt = impactAt,
            ChannelId = session.ChannelId,
            CasterId = caster.Id,
            TargetId = target.Id,
            Skill = skill,
            ChargePercent = skill.SkillId is 10201 or 10203 or 10206 or 16 ? Math.Clamp(chargePercent, 0f, 1f) : 1f,
        });

        return true;
    }

    private void FireBasicProjectile(Channel channel, PlayerEntity attacker, PlayerSession session, float originX, float originY, float dirX, float dirY, byte projectileType)
    {
        const float projectileRange = 760f;
        const float projectileRadius = 34f;
        const float projectileSpeed = 700f;

        BroadcastProjectileSpawn(channel, attacker.Id, originX, originY, dirX, dirY, projectileType, includeCaster: false);

        var target = FindFirstProjectileHit(channel, attacker, originX, originY, dirX, dirY, projectileRange, projectileRadius);
        if (target == null)
            return;

        float along = ProjectileHitDistance(originX, originY, dirX, dirY, target.X, target.Y, projectileRange).Along;
        double impactAt = _gameTime + Math.Clamp(along / projectileSpeed, 0.05f, 1.8f);
        _pendingProjectileHits.Add(new PendingProjectileHit
        {
            ImpactAt = impactAt,
            ChannelId = session.ChannelId,
            CasterId = attacker.Id,
            TargetId = target.Id,
            Skill = null,
        });
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
            if (!CanDamageEntity(caster, target, out _))
                continue;

            var session = _sessions.Values.FirstOrDefault(s => s.EntityId == pending.CasterId);
            if (session == null || session.SelectedCharacter == null)
                continue;

            bool isCrit;
            int damage = pending.Skill != null
                ? CalculateSkillDamage(caster, target, pending.Skill, out isCrit, pending.ChargePercent)
                : CalculateBasicDamage(caster, target, out isCrit);
            if (pending.Skill != null && IsMarcaExecutorSkill(pending.Skill))
            {
                ApplySkillDebuff(channel, target, pending.Skill);
                ScheduleMarcaExecutorPoison(channel, caster, target, session, damage);
                continue;
            }
            if (pending.Skill != null && IsSangramentoMortalSkill(pending.Skill))
            {
                ApplySkillDebuff(channel, target, pending.Skill);
                ScheduleSangramentoMortal(channel, caster, target, session, damage);
                continue;
            }

            int healthDamage = ApplyDamageToEntity(channel, target, damage);
            ApplyOffensiveSustain(channel, caster, healthDamage);
            if (target is PlayerEntity playerTarget)
            {
                playerTarget.LastCombatTime = _gameTime;
                BroadcastPartyMemberUpdateForEntity(playerTarget.Id);
                ApplyDamageReflect(channel, playerTarget, caster, healthDamage);
            }
            if (target is MonsterEntity hitMob)
                hitMob.TargetEntityId = caster.Id;

            BroadcastCombatResult(channel, caster.Id, target.Id, damage, isCrit, target.Health, target.MaxHealth, caster.X, caster.Y, pending.Skill?.SkillId ?? 0);
            if (pending.Skill != null)
                ApplySkillDebuff(channel, target, pending.Skill);

            if (target is MonsterEntity killedMob && target.Health <= 0)
                HandleMonsterDeath(channel, killedMob, caster, session, target.Id);
        }
    }

    private static bool IsMarcaExecutorSkill(ServerSkillDefinition skill)
    {
        return skill.SkillId is 10206 or 16
            || string.Equals(skill.Nome, "Marca do Executor", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSangramentoMortalSkill(ServerSkillDefinition skill)
    {
        return skill.SkillId == SangramentoMortalSkillId
            || string.Equals(skill.Nome, "Sangramento Mortal", StringComparison.OrdinalIgnoreCase);
    }

    private void ScheduleMarcaExecutorPoison(Channel channel, PlayerEntity caster, Entity target, PlayerSession session, int totalDamage)
    {
        const int tickCount = 3;
        int safeTotal = Math.Max(1, totalDamage);
        int baseTick = Math.Max(1, safeTotal / tickCount);
        int remainder = Math.Max(0, safeTotal - baseTick * tickCount);

        for (int i = 0; i < tickCount; i++)
        {
            _pendingDotTicks.Add(new PendingDotTick
            {
                TickAt = _gameTime + i + 1,
                ChannelId = channel.Id,
                CasterId = caster.Id,
                TargetId = target.Id,
                SkillId = 10206,
                Damage = baseTick + (i == tickCount - 1 ? remainder : 0),
                IsCrit = false,
            });
        }

        SendSystemMessage(session.Peer, "Marca do Executor: veneno aplicado.");
    }

    private void ScheduleSangramentoMortal(Channel channel, PlayerEntity caster, Entity target, PlayerSession session, int totalDamage)
    {
        const int tickCount = 5;
        int safeTotal = Math.Max(1, totalDamage);
        int baseTick = Math.Max(1, safeTotal / tickCount);
        int remainder = Math.Max(0, safeTotal - baseTick * tickCount);

        for (int i = 0; i < tickCount; i++)
        {
            _pendingDotTicks.Add(new PendingDotTick
            {
                TickAt = _gameTime + i + 1,
                ChannelId = channel.Id,
                CasterId = caster.Id,
                TargetId = target.Id,
                SkillId = SangramentoMortalSkillId,
                Damage = baseTick + (i == tickCount - 1 ? remainder : 0),
                IsCrit = false,
            });
        }

        SendSystemMessage(session.Peer, "Sangramento Mortal aplicado.");
    }

    private void ProcessPendingDotTicks()
    {
        for (int i = _pendingDotTicks.Count - 1; i >= 0; i--)
        {
            var pending = _pendingDotTicks[i];
            if (pending.TickAt > _gameTime)
                continue;

            _pendingDotTicks.RemoveAt(i);
            var channel = _world.GetChannel(pending.ChannelId);
            if (channel == null)
                continue;

            if (channel.GetEntity(pending.CasterId) is not PlayerEntity caster || caster.Health <= 0)
                continue;
            var target = channel.GetEntity(pending.TargetId);
            if (target == null || target.Health <= 0)
                continue;
            if (!CanDamageEntity(caster, target, out _))
                continue;

            var session = _sessions.Values.FirstOrDefault(s => s.EntityId == pending.CasterId);
            if (session == null || session.SelectedCharacter == null)
                continue;

            int damage = Math.Max(1, pending.Damage);
            int healthDamage = ApplyDamageToEntity(channel, target, damage);
            ApplyOffensiveSustain(channel, caster, healthDamage);
            if (target is PlayerEntity playerTarget)
            {
                playerTarget.LastCombatTime = _gameTime;
                BroadcastPartyMemberUpdateForEntity(playerTarget.Id);
                ApplyDamageReflect(channel, playerTarget, caster, healthDamage);
            }
            if (target is MonsterEntity hitMob)
                hitMob.TargetEntityId = caster.Id;

            BroadcastCombatResult(channel, caster.Id, target.Id, damage, pending.IsCrit, target.Health, target.MaxHealth, caster.X, caster.Y, pending.SkillId);

            if (target is MonsterEntity killedMob && target.Health <= 0)
                HandleMonsterDeath(channel, killedMob, caster, session, target.Id);
        }
    }

    private Entity? FindFirstProjectileHit(Channel channel, PlayerEntity caster, float originX, float originY, float dirX, float dirY, float range, float radius)
    {
        return channel.GetEntitiesInAoi(caster.X, caster.Y)
            .Select(id => channel.GetEntity(id))
            .Where(e => e != null && e.Health > 0 && e.Id != caster.Id)
            .Where(e => CanDamageEntity(caster, e!, out _))
            .Select(e => new { Entity = e!, Hit = ProjectileHitDistance(originX, originY, dirX, dirY, e!.X, e.Y, range) })
            .Where(x => x.Hit.Along >= 0f && x.Hit.Along <= range && x.Hit.Perpendicular <= radius)
            .OrderBy(x => x.Hit.Along)
            .Select(x => x.Entity)
            .FirstOrDefault();
    }

    private Entity? FindTargetNearPosition(Channel channel, PlayerEntity caster, float targetX, float targetY, float maxCasterDistance, float pickRadius)
    {
        float maxCasterDistanceSq = maxCasterDistance * maxCasterDistance;
        float pickRadiusSq = pickRadius * pickRadius;
        return channel.GetEntitiesInAoi(caster.X, caster.Y)
            .Select(id => channel.GetEntity(id))
            .Where(e => e != null && e.Health > 0 && e.Id != caster.Id)
            .Where(e => CanDamageEntity(caster, e!, out _))
            .Select(e => new
            {
                Entity = e!,
                CasterDistanceSq = (e!.X - caster.X) * (e.X - caster.X) + (e.Y - caster.Y) * (e.Y - caster.Y),
                PickDistanceSq = (e.X - targetX) * (e.X - targetX) + (e.Y - targetY) * (e.Y - targetY),
            })
            .Where(x => x.CasterDistanceSq <= maxCasterDistanceSq && x.PickDistanceSq <= pickRadiusSq)
            .OrderBy(x => x.PickDistanceSq)
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
        if (IsMarcaDaMorteSkill(skill))
            return ApplyMarcaDaMorteSkill(peer, channel, caster, skill, targetX, targetY);
        if (skill.SkillId == CuraEmAreaSkillId)
            return ApplyCuraEmAreaSkill(peer, channel, caster, skill, targetX, targetY);

        PlayerEntity target = caster;
        if (skill.TargetType == 1)
        {
            target = channel.GetEntitiesInAoi(caster.X, caster.Y)
                .Select(id => channel.GetEntity(id) as PlayerEntity)
                .Where(p => p != null && p.Health > 0 && IsValidSupportTarget(caster, p))
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
            if (IsSegundoFolegoSkill(skill))
            {
                ScheduleSegundoFolegoTicks(channel, caster, target, skill);
                return true;
            }

            if (skill.SkillId == RenovacaoSkillId)
            {
                ScheduleRenovacaoTicks(channel, caster, caster, skill);
                if (target.Id != caster.Id)
                    ScheduleRenovacaoTicks(channel, caster, target, skill);
                return true;
            }

            ApplyInstantSupportRecovery(channel, caster, target, skill, amount, restoreMana: false);
            return true;
        }

        string buffId = $"skill:{skill.SkillId}";
        float duration = Math.Max(1f, skill.Duracao);
        target.ActiveServerBuffs[buffId] = _gameTime + duration;
        RefreshTemporarySkillBonuses(target);
        if (skill.SkillId == 12208)
        {
            SendStatusEffectToPlayer(channel, target.Id, "reflexos_assassinos", skill.Nome, false, duration, 0, "res://skills/Incone Skills/Assasino/37.png");
        }
        else if (skill.SkillId == FuriaElementalSkillId)
        {
            SendStatusEffectToPlayer(channel, target.Id, "furia_elemental", skill.Nome, false, duration, 20, "res://skills/Incone Skills/Mage/34.png");
        }
        else if (skill.SkillId == FuriaBerserkerSkillId)
        {
            SendStatusEffectToPlayer(channel, target.Id, "furia_berserker", skill.Nome, false, duration, 25, "res://skills/Incone Skills/Berseker/24.png");
        }
        else if (skill.SkillId == FrenesiBerserkerSkillId)
        {
            SendStatusEffectToPlayer(channel, target.Id, "frenesi_berserker", skill.Nome, false, duration, 35, "res://skills/Incone Skills/Berseker/36.png");
        }
        else if (skill.SkillId == SedeDeSangueSkillId)
        {
            SendStatusEffectToPlayer(channel, target.Id, "sede_de_sangue", skill.Nome, false, duration, 15, "res://skills/Incone Skills/Berseker/44.png");
        }
        else if (skill.SkillId == ForcaBrutalSkillId)
        {
            SendStatusEffectToPlayer(channel, target.Id, "forca_brutal", skill.Nome, false, duration, 25, "res://skills/Incone Skills/Berseker/18.png");
        }
        else if (skill.SkillId == DeusDaGuerraSkillId)
        {
            SendStatusEffectToPlayer(channel, target.Id, "deus_da_guerra", skill.Nome, false, duration, 50, "res://skills/Incone Skills/Berseker/26.png");
        }
        else if (skill.SkillId == BerserkSkillId)
        {
            SendStatusEffectToPlayer(channel, target.Id, "berserk", skill.Nome, false, duration, 30, "res://skills/Incone Skills/Berseker/43.png");
        }
        else if (skill.SkillId == SangueDeFerroSkillId)
        {
            SendStatusEffectToPlayer(channel, target.Id, "sangue_de_ferro", skill.Nome, false, duration, 0, "res://skills/Incone Skills/Berseker/40.png");
        }
        else if (skill.SkillId == FortificacaoSkillId)
        {
            int defensePercent = skill.Valor > 0 ? skill.Valor : 15;
            SendStatusEffectToPlayer(channel, target.Id, "fortificacao", skill.Nome, false, duration, defensePercent, "res://skills/Incone Skills/Guerreiro/15.png");
        }
        else if (skill.SkillId == MuralhaInabalavelSkillId)
        {
            int defensePercent = skill.Valor > 0 ? skill.Valor : 50;
            SendStatusEffectToPlayer(channel, target.Id, "muralha_inabalavel", skill.Nome, false, duration, defensePercent, "res://skills/Incone Skills/Guerreiro/16.png");
        }
        else if (skill.SkillId == BencaoDivinaSkillId)
        {
            SendStatusEffectToPlayer(channel, target.Id, "bencao_divina", skill.Nome, false, duration, 0, "res://skills/Incone Skills/Buffs/PNG/47.png");
        }
        else if (skill.SkillId == MarteloDesolacaoSkillId)
        {
            SendStatusEffectToPlayer(channel, target.Id, "bencao_sagrada", skill.Nome, false, duration, 0, "res://skills/Incone Skills/Buffs/PNG/47.png");
        }
        else if (skill.SkillId == PurificacaoSkillId)
        {
            SendStatusEffectToPlayer(channel, target.Id, "purificacao", skill.Nome, false, 1f, 0, "res://skills/Incone Skills/Buffs/PNG/48.png");
        }

        if (skill.SkillId == EscudoArcanoSkillId)
        {
            int shieldPercent = Math.Clamp(skill.Valor, 1, 100);
            int shieldAmount = Math.Max(1, (int)MathF.Round(target.MaxHealth * (shieldPercent / 100f)));
            target.ArcaneShield = shieldAmount;
            target.ArcaneShieldMax = shieldAmount;
            target.ArcaneShieldExpiresAt = _gameTime + duration;
            SendShieldUpdateToPlayer(channel, target);
            SendStatusEffectToPlayer(channel, target.Id, "arcane_shield", skill.Nome, false, duration, shieldAmount, "res://skills/Incone Skills/Buffs/PNG/6.png");
        }
        else if (skill.EffectType == 10)
        {
            int oldHealth = target.Health;
            target.Health = Math.Min(target.MaxHealth, target.Health + amount);
            BroadcastCombatResult(channel, caster.Id, target.Id, -(target.Health - oldHealth), false, target.Health, target.MaxHealth, caster.X, caster.Y);
        }
        if (skill.EffectType != 1 && skill.EffectType != 10)
            BroadcastSkillVisualEffect(channel, caster.Id, target.Id, skill.SkillId, target.X, target.Y, duration);
        SendSystemMessage(peer, $"{skill.Nome}: efeito aplicado.");
        return true;
    }

    private void ApplyInstantSupportRecovery(Channel channel, PlayerEntity caster, PlayerEntity target, ServerSkillDefinition skill, int amount, bool restoreMana)
    {
        if (target.Health <= 0)
            return;

        int oldHealth = target.Health;
        int oldMana = target.Mana;
        target.Health = Math.Min(target.MaxHealth, target.Health + amount);
        if (restoreMana)
            target.Mana = Math.Min(target.MaxMana, target.Mana + amount);

        int healedHealth = target.Health - oldHealth;
        BroadcastCombatResult(channel, caster.Id, target.Id, -healedHealth, false, target.Health, target.MaxHealth, target.X, target.Y, skill.SkillId);
        if (restoreMana && target.Mana != oldMana)
            BroadcastSingleEntityUpdate(channel, target);
        BroadcastPartyMemberUpdateForEntity(target.Id);
    }

    private static bool IsValidSupportTarget(PlayerEntity caster, PlayerEntity? target)
    {
        if (target == null || target.Health <= 0)
            return false;
        if (target.Id == caster.Id)
            return true;
        if (caster.PartyId >= 0 && target.PartyId == caster.PartyId)
            return true;
        return string.Equals(target.FactionId, caster.FactionId, StringComparison.OrdinalIgnoreCase);
    }

    private bool ApplyCuraEmAreaSkill(NetPeer peer, Channel channel, PlayerEntity caster, ServerSkillDefinition skill, float targetX, float targetY)
    {
        int amount = Math.Max(1, skill.Valor);
        var targets = channel.GetEntitiesInAoi(targetX, targetY)
            .Select(id => channel.GetEntity(id) as PlayerEntity)
            .Where(p => p != null && p.Health > 0)
            .Where(p => IsValidSupportTarget(caster, p))
            .Where(p =>
            {
                float dx = p!.X - targetX;
                float dy = p.Y - targetY;
                return MathF.Sqrt(dx * dx + dy * dy) <= CuraEmAreaRadius;
            })
            .ToList();

        if (targets.Count == 0)
        {
            SendSystemMessage(peer, "Nenhum aliado valido na area da cura.");
            return false;
        }

        foreach (var target in targets)
        {
            int oldHealth = target!.Health;
            target.Health = Math.Min(target.MaxHealth, target.Health + amount);
            BroadcastCombatResult(channel, caster.Id, target.Id, -(target.Health - oldHealth), false, target.Health, target.MaxHealth, target.X, target.Y, skill.SkillId);
            BroadcastPartyMemberUpdateForEntity(target.Id);
        }

        BroadcastSkillAreaEffect(channel, peer, skill.SkillId, targetX, targetY, CuraEmAreaRadius, 1.1f);
        SendSystemMessage(peer, $"{skill.Nome}: cura em area aplicada.");
        return true;
    }

    private static bool IsSegundoFolegoSkill(ServerSkillDefinition skill)
    {
        return skill.SkillId == SegundoFolegoUniversalSkillId
            || string.Equals(NormalizeSkillName(skill.Nome), "segundo folego", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeSkillName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";

        string formD = value.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(formD.Length);
        foreach (char c in formD)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(char.ToLowerInvariant(c));
        }

        return Regex.Replace(sb.ToString().Normalize(NormalizationForm.FormC), @"\s+", " ").Trim();
    }

    private void ScheduleSegundoFolegoTicks(Channel channel, PlayerEntity caster, PlayerEntity target, ServerSkillDefinition skill)
    {
        int totalHealth = SegundoFolegoFixedTotalHealth;
        int totalMana = SegundoFolegoFixedTotalMana;
        int baseHealthTick = Math.Max(1, totalHealth / SegundoFolegoTickCount);
        int baseManaTick = Math.Max(1, totalMana / SegundoFolegoTickCount);
        int healthRemainder = Math.Max(0, totalHealth - baseHealthTick * SegundoFolegoTickCount);
        int manaRemainder = Math.Max(0, totalMana - baseManaTick * SegundoFolegoTickCount);

        for (int i = 0; i < SegundoFolegoTickCount; i++)
        {
            _pendingHealTicks.Add(new PendingHealTick
            {
                TickAt = _gameTime + (i + 1) * SegundoFolegoTickInterval,
                ChannelId = channel.Id,
                CasterId = caster.Id,
                TargetId = target.Id,
                SkillId = skill.SkillId,
                HealthAmount = baseHealthTick + (i == SegundoFolegoTickCount - 1 ? healthRemainder : 0),
                ManaAmount = baseManaTick + (i == SegundoFolegoTickCount - 1 ? manaRemainder : 0),
            });
        }
    }

    private void ScheduleRenovacaoTicks(Channel channel, PlayerEntity caster, PlayerEntity target, ServerSkillDefinition skill)
    {
        float duration = Math.Max(1f, skill.Duracao);
        int tickCount = Math.Max(1, (int)MathF.Ceiling(duration / (float)RenovacaoTickInterval));
        int totalHealth = Math.Max(tickCount, skill.Valor);
        int totalMana = Math.Max(tickCount, skill.Valor);
        int baseHealthTick = Math.Max(1, totalHealth / tickCount);
        int baseManaTick = Math.Max(1, totalMana / tickCount);
        int healthRemainder = Math.Max(0, totalHealth - baseHealthTick * tickCount);
        int manaRemainder = Math.Max(0, totalMana - baseManaTick * tickCount);

        for (int i = 0; i < tickCount; i++)
        {
            _pendingHealTicks.Add(new PendingHealTick
            {
                TickAt = _gameTime + i * RenovacaoTickInterval,
                ChannelId = channel.Id,
                CasterId = caster.Id,
                TargetId = target.Id,
                SkillId = skill.SkillId,
                HealthAmount = baseHealthTick + (i == tickCount - 1 ? healthRemainder : 0),
                ManaAmount = baseManaTick + (i == tickCount - 1 ? manaRemainder : 0),
                StartVisual = i == 0,
            });
        }
    }

    private void ProcessPendingHealTicks()
    {
        for (int i = _pendingHealTicks.Count - 1; i >= 0; i--)
        {
            var pending = _pendingHealTicks[i];
            if (pending.TickAt > _gameTime)
                continue;

            _pendingHealTicks.RemoveAt(i);
            var channel = _world.GetChannel(pending.ChannelId);
            if (channel == null)
                continue;

            if (channel.GetEntity(pending.CasterId) is not PlayerEntity caster || caster.Health <= 0)
                continue;
            if (channel.GetEntity(pending.TargetId) is not PlayerEntity target || target.Health <= 0)
                continue;

            int oldHealth = target.Health;
            int oldMana = target.Mana;
            target.Health = Math.Min(target.MaxHealth, target.Health + Math.Max(1, pending.HealthAmount));
            target.Mana = Math.Min(target.MaxMana, target.Mana + Math.Max(1, pending.ManaAmount));

            int healedHealth = target.Health - oldHealth;
            int visualSkillId = pending.StartVisual ? pending.SkillId : 0;
            if (healedHealth > 0 || visualSkillId != 0)
                BroadcastCombatResult(channel, caster.Id, target.Id, -healedHealth, false, target.Health, target.MaxHealth, target.X, target.Y, visualSkillId);

            if (target.Mana != oldMana || healedHealth > 0)
            {
                BroadcastSingleEntityUpdate(channel, target);
                BroadcastPartyMemberUpdateForEntity(target.Id);
            }
        }
    }

    private bool ApplyMilagreDivinoSkill(NetPeer peer, Channel channel, PlayerEntity caster, PlayerSession session, ServerSkillDefinition skill)
    {
        var targets = channel.GetEntitiesInAoi(caster.X, caster.Y)
            .Select(id => channel.GetEntity(id))
            .Where(e => e != null && e.Health > 0 && e.Id != caster.Id)
            .Where(e => CanDamageEntity(caster, e!, out _))
            .Select(e => e!)
            .Where(e =>
            {
                float dx = e.X - caster.X;
                float dy = e.Y - caster.Y;
                return MathF.Sqrt(dx * dx + dy * dy) <= MilagreDivinoRadius;
            })
            .OrderBy(e =>
            {
                float dx = e.X - caster.X;
                float dy = e.Y - caster.Y;
                return dx * dx + dy * dy;
            })
            .ToList();

        if (targets.Count == 0)
        {
            SendSystemMessage(peer, "Nenhum inimigo valido na area do Milagre Divino.");
            return false;
        }

        foreach (var target in targets)
        {
            int damage = CalculateSkillDamage(caster, target, skill, out bool isCrit);
            int healthDamage = ApplyDamageToEntity(channel, target, damage);
            ApplyOffensiveSustain(channel, caster, healthDamage);
            if (target is PlayerEntity playerTarget)
            {
                playerTarget.LastCombatTime = _gameTime;
                BroadcastPartyMemberUpdateForEntity(playerTarget.Id);
                ApplyDamageReflect(channel, playerTarget, caster, healthDamage);
            }
            if (target is MonsterEntity hitMob)
                hitMob.TargetEntityId = caster.Id;

            BroadcastCombatResult(channel, caster.Id, target.Id, damage, isCrit, target.Health, target.MaxHealth, caster.X, caster.Y, skill.SkillId);
            if (target is MonsterEntity killedMob && target.Health <= 0)
                HandleMonsterDeath(channel, killedMob, caster, session, target.Id);
        }

        return true;
    }

    private bool ApplyRessurreicaoSkill(NetPeer peer, Channel channel, PlayerEntity caster, ServerSkillDefinition skill, float targetX, float targetY)
    {
        float dxCaster = targetX - caster.X;
        float dyCaster = targetY - caster.Y;
        if (MathF.Sqrt(dxCaster * dxCaster + dyCaster * dyCaster) > RessurreicaoRange)
        {
            SendSystemMessage(peer, "Area fora do alcance da ressurreicao.");
            return false;
        }

        PlayerEntity? target = channel.GetAllEntities().Values
            .OfType<PlayerEntity>()
            .Where(p => p.Id != caster.Id && p.Health <= 0)
            .Where(p => string.Equals(p.FactionId, caster.FactionId, StringComparison.OrdinalIgnoreCase)
                || (caster.PartyId >= 0 && p.PartyId == caster.PartyId))
            .Where(p =>
            {
                float dx = p.X - targetX;
                float dy = p.Y - targetY;
                return MathF.Sqrt(dx * dx + dy * dy) <= RessurreicaoTargetRadius;
            })
            .OrderBy(p =>
            {
                float dx = p.X - targetX;
                float dy = p.Y - targetY;
                return dx * dx + dy * dy;
            })
            .FirstOrDefault();

        if (target == null)
        {
            SendSystemMessage(peer, "Nenhum aliado morto dentro da area marcada.");
            return false;
        }

        int revivePercent = Math.Clamp(skill.Valor > 0 ? skill.Valor : 20, 1, 100);
        target.Health = Math.Max(1, (int)MathF.Round(target.MaxHealth * (revivePercent / 100f)));
        target.Mana = Math.Max(0, (int)MathF.Round(target.MaxMana * (revivePercent / 100f)));
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
            if (p == null)
                continue;

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

        BroadcastCombatResult(channel, caster.Id, target.Id, 0, false, target.Health, target.MaxHealth, target.X, target.Y, skill.SkillId);
        SendSystemMessage(peer, $"{target.Name} foi ressuscitado.");
        return true;
    }

    private bool ApplyMarcaDaMorteSkill(NetPeer peer, Channel channel, PlayerEntity caster, ServerSkillDefinition skill, float targetX, float targetY)
    {
        var target = channel.GetEntitiesInAoi(caster.X, caster.Y)
            .Select(id => channel.GetEntity(id))
            .Where(e => e != null && e.Health > 0 && e.Id != caster.Id)
            .Where(e => CanDamageEntity(caster, e!, out _))
            .Select(e => e!)
            .Where(e =>
            {
                float dxCaster = e.X - caster.X;
                float dyCaster = e.Y - caster.Y;
                return MathF.Sqrt(dxCaster * dxCaster + dyCaster * dyCaster) <= MarcaDaMorteRange;
            })
            .OrderBy(e =>
            {
                float dx = e.X - targetX;
                float dy = e.Y - targetY;
                return dx * dx + dy * dy;
            })
            .FirstOrDefault();

        if (target == null)
        {
            SendSystemMessage(peer, "Nenhum alvo valido para Marca da Morte.");
            return false;
        }

        float duration = Math.Max(1f, skill.Duracao);
        double expiresAt = _gameTime + duration;
        if (target is PlayerEntity playerTarget)
        {
            playerTarget.ActiveServerBuffs[$"mark_death:{caster.Id}"] = expiresAt;
            playerTarget.ActiveServerBuffs["mark_death"] = expiresAt;
        }
        else if (target is MonsterEntity mobTarget)
        {
            mobTarget.ActiveServerBuffs[$"mark_death:{caster.Id}"] = expiresAt;
            mobTarget.ActiveServerBuffs["mark_death"] = expiresAt;
            mobTarget.TargetEntityId = caster.Id;
        }

        SendSystemMessage(peer, $"{skill.Nome}: alvo marcado.");
        return true;
    }

    private int CalculateBasicDamage(PlayerEntity caster, Entity target, out bool isCrit)
    {
        RefreshTemporarySkillBonuses(caster);
        return CalculateDamageAgainstTarget(caster, target, caster.CalculateAttackDamage(), out isCrit, useMagicDamage: IsMagicClass(caster));
    }

    private int CalculateSkillDamage(PlayerEntity caster, Entity target, ServerSkillDefinition skill, out bool isCrit, float chargePercent = 1f)
    {
        RefreshTemporarySkillBonuses(caster);
        int baseDamage = caster.CalculateAttackDamage();
        float minMultiplier = skill.DamageMultiplierMin > 0 ? skill.DamageMultiplierMin : skill.DamageMultiplier;
        float maxMultiplier = skill.DamageMultiplierMax > 0 ? skill.DamageMultiplierMax : minMultiplier;
        if (maxMultiplier < minMultiplier)
            (minMultiplier, maxMultiplier) = (maxMultiplier, minMultiplier);
        float multiplier = maxMultiplier > minMultiplier
            ? minMultiplier + (float)Random.Shared.NextDouble() * (maxMultiplier - minMultiplier)
            : minMultiplier;
        if (skill.SkillId is 10201 or 10203 or 10206 or 16)
            multiplier = minMultiplier + (maxMultiplier - minMultiplier) * Math.Clamp(chargePercent, 0f, 1f);
        if (multiplier <= 0f)
            multiplier = 1f;
        int rawDamage = Math.Max(1, (int)(baseDamage * multiplier) + skill.FlatPower);
        if (skill.SkillId is 10209 or 19 or ExecucaoFinalSkillId && target.MaxHealth > 0 && target.Health <= target.MaxHealth * 0.30f)
            rawDamage = Math.Max(1, (int)MathF.Round(rawDamage * 1.5f));

        bool ignoreDefense = skill.SkillId == 10203
            || string.Equals(skill.Nome, "Tiro Penetrante", StringComparison.OrdinalIgnoreCase)
            || (!string.IsNullOrWhiteSpace(skill.EfeitoPrincipal)
                && skill.EfeitoPrincipal.Contains("Penetra", StringComparison.OrdinalIgnoreCase));
        return CalculateDamageAgainstTarget(caster, target, rawDamage, out isCrit, ignoreDefense, IsGuaranteedCriticalSkill(skill), IsMagicClass(caster));
    }

    private static bool IsMagicClass(PlayerEntity player)
    {
        string classe = player.CharacterClass?.Trim().ToLowerInvariant() ?? "";
        return classe is "mago" or "prist" or "clerigo" or "clérigo";
    }

    private int CalculateDamageAgainstTarget(PlayerEntity caster, Entity target, int rawDamage, out bool isCrit, bool ignoreDefense = false, bool forceCrit = false, bool useMagicDamage = false)
    {
        if (target is PlayerEntity playerTargetForRefresh)
            RefreshTemporarySkillBonuses(playerTargetForRefresh);

        if (caster.TemporaryDamageBonus > 0f)
            rawDamage = Math.Max(1, (int)MathF.Round(rawDamage * (1f + caster.TemporaryDamageBonus)));

        if (!ignoreDefense && target is PlayerEntity evasionTarget)
        {
            float hitChance = Math.Clamp(caster.CalculatePrecision() - evasionTarget.CalculateEvasion() * 0.65f, 15f, 98f);
            if (Random.Shared.NextDouble() * 100.0 > hitChance)
            {
                isCrit = false;
                return 0;
            }
        }

        if (target is PlayerEntity)
            rawDamage += caster.PvpDamageBonus;

        int targetDefense = ignoreDefense ? 0 : target switch
        {
            PlayerEntity p => (useMagicDamage ? p.CalculateMagicDefense() : p.CalculateDefense()) + p.PvpDefenseBonus,
            MonsterEntity m => useMagicDamage ? m.CalculateMagicDefense() : m.CalculateDefense(),
            _ => 0,
        };
        if (!ignoreDefense && !useMagicDamage && caster.ArmorPenetration > 0f)
            targetDefense = Math.Max(0, (int)MathF.Round(targetDefense * (1f - caster.ArmorPenetration / 100f)));
        float defReduction = MathF.Min(0.80f, targetDefense / (targetDefense + 400f));
        float critChance = GetCritChance(caster);
        if (target is PlayerEntity tenaciousTarget)
            critChance = MathF.Max(0f, critChance - tenaciousTarget.CalculateTenacity() * 0.5f);
        isCrit = forceCrit || Random.Shared.NextDouble() * 100.0 < critChance;
        if (HasActiveDebuff(target, "mark_executor"))
            rawDamage = Math.Max(1, (int)MathF.Round(rawDamage * 1.25f));
        if (HasActiveDebuff(target, $"mark_death:{caster.Id}"))
            rawDamage = Math.Max(1, (int)MathF.Round(rawDamage * 1.10f));

        int damage = Math.Max(1, (int)(rawDamage * (1f - defReduction)));
        if (isCrit)
        {
            float critMultiplier = caster.CalculateCritMultiplier();
            if (target is PlayerEntity pvpTarget)
                critMultiplier = MathF.Max(1.2f, critMultiplier - pvpTarget.CalculateTenacity() / 200f);
            damage = Math.Max(1, (int)(damage * critMultiplier));
        }
        if (HasInvisibilityOpeningBuff(caster))
            damage = Math.Max(1, (int)MathF.Round(damage * InvisibilidadeProfundaOpeningDamageBonus));
        return damage;
    }

    private static double GetEffectiveSkillCooldown(PlayerEntity player, float baseCooldown)
    {
        double reduction = Math.Clamp(player.CooldownReduction, 0f, 40f) / 100.0;
        return Math.Max(0.5, baseCooldown * (1.0 - reduction));
    }

    private void ApplyOffensiveSustain(Channel channel, PlayerEntity attacker, int damage)
    {
        if (damage <= 0)
            return;

        int healed = 0;
        int manaRestored = 0;
        float lifeSteal = attacker.LifeSteal + attacker.TemporaryLifeStealBonus;
        if (lifeSteal > 0f && attacker.Health < attacker.MaxHealth)
        {
            healed = Math.Min(attacker.MaxHealth - attacker.Health, Math.Max(1, (int)MathF.Round(damage * lifeSteal / 100f)));
            attacker.Health += healed;
        }
        if (attacker.ManaSteal > 0f && attacker.Mana < attacker.MaxMana)
        {
            manaRestored = Math.Min(attacker.MaxMana - attacker.Mana, Math.Max(1, (int)MathF.Round(damage * attacker.ManaSteal / 100f)));
            attacker.Mana += manaRestored;
        }

        if (healed > 0)
            BroadcastCombatResult(channel, attacker.Id, attacker.Id, -healed, false, attacker.Health, attacker.MaxHealth, attacker.X, attacker.Y);
        if (healed > 0 || manaRestored > 0)
            BroadcastPartyMemberUpdateForEntity(attacker.Id);
    }

    private void ApplyDamageReflect(Channel channel, PlayerEntity target, PlayerEntity attacker, int damage)
    {
        float reflectPercent = GetDamageReflectPercent(target);
        if (damage <= 0 || reflectPercent <= 0f || attacker.Health <= 0)
            return;

        int reflected = Math.Min(attacker.Health, Math.Max(1, (int)MathF.Round(damage * reflectPercent / 100f)));
        ApplyDamageToEntity(channel, attacker, reflected);
        BroadcastCombatResult(channel, target.Id, attacker.Id, reflected, false, attacker.Health, attacker.MaxHealth, target.X, target.Y);
        BroadcastPartyMemberUpdateForEntity(attacker.Id);
    }

    private bool HasInvisibilityOpeningBuff(PlayerEntity player)
    {
        return (player.ActiveServerBuffs.TryGetValue($"skill:{InvisibilidadeProfundaSkillId}", out double deepUntil)
                && deepUntil > _gameTime)
            || (player.ActiveServerBuffs.TryGetValue($"skill:{PassoSombrioSkillId}", out double stepUntil)
                && stepUntil > _gameTime);
    }

    private void ConsumeInvisibilityOpeningBuff(Channel channel, PlayerEntity player)
    {
        if (!HasInvisibilityOpeningBuff(player))
            return;

        player.ActiveServerBuffs.Remove($"skill:{InvisibilidadeProfundaSkillId}");
        player.ActiveServerBuffs.Remove($"skill:{PassoSombrioSkillId}");
        SendStatusEffectToPlayer(channel, player.Id, "invisibility", "Invisibilidade", false, 0f, 0, "res://skills/Incone Skills/Assasino/43.png");
        BroadcastPlayerAction(channel, player, PlayerActionReveal, 0f, 0f, includeSelf: true);
    }

    private static bool IsGuaranteedCriticalSkill(ServerSkillDefinition skill)
    {
        return skill.SkillId is 10205 or 15
            || string.Equals(skill.Nome, "Disparo CrÃ­tico", StringComparison.OrdinalIgnoreCase)
            || string.Equals(skill.Nome, "Disparo Critico", StringComparison.OrdinalIgnoreCase);
    }

    private bool HasActiveDebuff(Entity target, string debuff)
    {
        return target switch
        {
            PlayerEntity p => p.ActiveServerBuffs.TryGetValue(debuff, out double until) && until > _gameTime,
            MonsterEntity m => m.ActiveServerBuffs.TryGetValue(debuff, out double until) && until > _gameTime,
            _ => false,
        };
    }

    private void ApplySkillDebuff(Channel channel, Entity target, ServerSkillDefinition skill)
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
            24 => skill.SkillId is 10206 or 16 ? "mark_executor" : "poison",
            25 => "curse",
            _ => "",
        };
        if (debuff.Length == 0)
            return;

        if (target is PlayerEntity playerTarget)
        {
            double duration = skill.Duracao * (1.0 - Math.Clamp(playerTarget.ControlResistance, 0f, 50f) / 100.0);
            double finalDuration = Math.Max(0.25, duration);
            playerTarget.ActiveServerBuffs[debuff] = _gameTime + finalDuration;
            if (debuff == "stun")
                SendStatusEffectToPlayer(channel, playerTarget.Id, "stun", skill.Nome, true, (float)finalDuration, 0, "res://skills/Incone Skills/Debuffs/1.png");
            else if (debuff == "sleep")
                SendStatusEffectToPlayer(channel, playerTarget.Id, "sleep", skill.Nome, true, (float)finalDuration, 0, "res://skills/Incone Skills/Debuffs/31.png");
        }
        else if (target is MonsterEntity mobTarget)
            mobTarget.ActiveServerBuffs[debuff] = _gameTime + skill.Duracao;
    }

    private void BroadcastCombatResult(Channel channel, ulong attackerId, ulong targetId, int damage, bool isCrit, int targetHealth, int targetMaxHealth, float x, float y, int skillId = 0)
    {
        if (damage > 0)
        {
            if (channel.GetEntity(attackerId) is PlayerEntity attacker)
                ConsumeInvisibilityOpeningBuff(channel, attacker);
            BreakInvisibilityOnDamage(channel, targetId);
            BreakSleepOnDamage(channel, targetId);
        }

        var recipients = channel.GetEntitiesInAoi(x, y);
        if (channel.GetEntity(attackerId) is { } attackerEntity)
        {
            recipients.Add(attackerId);
            recipients.UnionWith(channel.GetEntitiesInAoi(attackerEntity.X, attackerEntity.Y));
        }
        if (channel.GetEntity(targetId) is { } targetEntity)
        {
            recipients.Add(targetId);
            recipients.UnionWith(channel.GetEntitiesInAoi(targetEntity.X, targetEntity.Y));
        }

        foreach (var eid in recipients)
        {
            var p = channel.GetPlayerPeer(eid);
            if (p == null) continue;
            var writer = PacketSerializer.WritePacket(PacketId.S2C_CombatResult);
            writer.Put(attackerId);
            writer.Put(targetId);
            writer.Put(damage);
            writer.Put(isCrit);
            writer.Put(targetHealth);
            writer.Put(targetMaxHealth);
            writer.Put(skillId);
            p.Send(writer, DeliveryMethod.ReliableOrdered);
        }
    }

    private void ProcessPendingAreaSkillTicks()
    {
        for (int i = _pendingAreaSkillTicks.Count - 1; i >= 0; i--)
        {
            var pending = _pendingAreaSkillTicks[i];
            if (pending.TickAt > _gameTime)
                continue;

            _pendingAreaSkillTicks.RemoveAt(i);
            var channel = _world.GetChannel(pending.ChannelId);
            if (channel == null)
                continue;

            if (channel.GetEntity(pending.CasterId) is not PlayerEntity caster || caster.Health <= 0)
                continue;

            var session = _sessions.Values.FirstOrDefault(s => s.EntityId == pending.CasterId);
            if (session == null || session.SelectedCharacter == null)
                continue;

            var targets = channel.GetEntitiesInAoi(caster.X, caster.Y)
                .Select(id => channel.GetEntity(id))
                .Where(e => e != null && e.Health > 0 && e.Id != caster.Id)
                .Where(e => CanDamageEntity(caster, e!, out _))
                .Select(e => e!)
                .Where(e =>
                {
                    float dx = e.X - caster.X;
                    float dy = e.Y - caster.Y;
                    return MathF.Sqrt(dx * dx + dy * dy) <= pending.Radius;
                })
                .OrderBy(e =>
                {
                    float dx = e.X - caster.X;
                    float dy = e.Y - caster.Y;
                    return dx * dx + dy * dy;
                })
                .Take(Math.Max(1, Math.Max(pending.Skill.MaxTargets, 8)))
                .ToList();

            foreach (var target in targets)
            {
                int fullDamage = CalculateSkillDamage(caster, target, pending.Skill, out bool isCrit);
                int damage = Math.Max(1, fullDamage / Math.Max(1, pending.TickCount));
                int healthDamage = ApplyDamageToEntity(channel, target, damage);
                ApplyOffensiveSustain(channel, caster, healthDamage);

                if (target is PlayerEntity playerTarget)
                {
                    playerTarget.LastCombatTime = _gameTime;
                    BroadcastPartyMemberUpdateForEntity(playerTarget.Id);
                    ApplyDamageReflect(channel, playerTarget, caster, healthDamage);
                }
                if (target is MonsterEntity hitMob)
                    hitMob.TargetEntityId = caster.Id;

                if (pending.AppliesSlow && healthDamage > 0)
                    ApplyAreaSlow(channel, target, pending.Skill, pending.SlowDuration);

                BroadcastCombatResult(channel, caster.Id, target.Id, damage, isCrit, target.Health, target.MaxHealth, caster.X, caster.Y, pending.Skill.SkillId);

                if (target is MonsterEntity killedMob && target.Health <= 0)
                    HandleMonsterDeath(channel, killedMob, caster, session, target.Id);
            }
        }
    }

    private void ApplyAreaSlow(Channel channel, Entity target, ServerSkillDefinition skill, double duration)
    {
        double finalDuration = Math.Max(0.25, duration);
        if (target is PlayerEntity playerTarget)
        {
            finalDuration *= 1.0 - Math.Clamp(playerTarget.ControlResistance, 0f, 50f) / 100.0;
            finalDuration = Math.Max(0.25, finalDuration);
            playerTarget.ActiveServerBuffs[$"slow:{skill.SkillId}"] = _gameTime + finalDuration;
            SendStatusEffectToPlayer(channel, playerTarget.Id, $"slow_{skill.SkillId}", skill.Nome, true, (float)finalDuration, 40, "res://skills/Incone Skills/Debuffs/1.png");
        }
        else if (target is MonsterEntity mobTarget)
        {
            mobTarget.ActiveServerBuffs["slow"] = _gameTime + finalDuration;
            mobTarget.ActiveServerBuffs[$"slow:{skill.SkillId}"] = _gameTime + finalDuration;
        }
    }

    private void ProcessActiveBastionAreas()
    {
        for (int i = _activeBastionAreas.Count - 1; i >= 0; i--)
        {
            var area = _activeBastionAreas[i];
            if (area.ExpiresAt > _gameTime)
                continue;

            _activeBastionAreas.RemoveAt(i);
            var channel = _world.GetChannel(area.ChannelId);
            if (channel != null)
                BroadcastSkillAreaEffect(channel, null, BastiaoSkillId, area.X, area.Y, area.Radius, 0f);
        }
    }

    private void PruneExpiredBastionAreas()
    {
        for (int i = _activeBastionAreas.Count - 1; i >= 0; i--)
        {
            if (_activeBastionAreas[i].ExpiresAt <= _gameTime)
                _activeBastionAreas.RemoveAt(i);
        }
    }

    private void BreakInvisibilityOnDamage(Channel channel, ulong targetId)
    {
        if (channel.GetEntity(targetId) is not PlayerEntity target)
            return;

        bool removed = false;
        foreach (var key in target.ActiveServerBuffs.Keys
            .Where(k => k.StartsWith("skill:12202", StringComparison.OrdinalIgnoreCase)
                || k.StartsWith("skill:12102", StringComparison.OrdinalIgnoreCase))
            .ToList())
        {
            target.ActiveServerBuffs.Remove(key);
            removed = true;
        }

        if (removed)
        {
            SendStatusEffectToPlayer(channel, target.Id, "invisibility", "Invisibilidade", false, 0f, 0, "res://skills/Incone Skills/Assasino/43.png");
            BroadcastPlayerAction(channel, target, PlayerActionReveal, 0f, 0f, includeSelf: true);
        }
    }

    private void BreakSleepOnDamage(Channel channel, ulong targetId)
    {
        var target = channel.GetEntity(targetId);
        if (target is PlayerEntity playerTarget)
        {
            if (playerTarget.ActiveServerBuffs.Remove("sleep"))
                SendStatusEffectToPlayer(channel, playerTarget.Id, "sleep", "Sono", false, 0f, 0, "res://skills/Incone Skills/Debuffs/31.png");
        }
        else if (target is MonsterEntity mobTarget)
        {
            mobTarget.ActiveServerBuffs.Remove("sleep");
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

        if (HasMovementBlockingDebuff(attacker))
        {
            SendSystemMessage(peer, "Voce esta sob efeito de controle.");
            return;
        }

        var target = channel.GetEntity(targetId);
        if (target == null || target.Health <= 0) return;

        float dx = target.X - attacker.X;
        float dy = target.Y - attacker.Y;
        float dist = MathF.Sqrt(dx * dx + dy * dy);
        bool isPetAttack = skillId == PetAttackSkillId;
        float attackRange = isPetAttack ? PetOwnerCommandRange : BasicAttackRange;

        if (dist > attackRange) return;

        if (isPetAttack && target is not MonsterEntity)
        {
            SendSystemMessage(peer, "Seu pet nao pode atacar jogadores aliados.");
            return;
        }

        if (!CanDamageEntity(attacker, target, out string denyReason))
        {
            if (!string.IsNullOrWhiteSpace(denyReason) && !isPetAttack)
                SendSystemMessage(peer, denyReason);
            return;
        }

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

        bool isCrit;
        int damage = isPetAttack
            ? CalculatePetDamage(channel, attacker, target, out isCrit)
            : CalculateBasicDamage(attacker, target, out isCrit);

        int healthDamage = ApplyDamageToEntity(channel, target, damage);
        ApplyOffensiveSustain(channel, attacker, healthDamage);
        if (damage > 0)
        {
            ConsumeInvisibilityOpeningBuff(channel, attacker);
            BreakInvisibilityOnDamage(channel, target.Id);
            BreakSleepOnDamage(channel, target.Id);
        }

        attacker.LastCombatTime = _gameTime;
        if (target is PlayerEntity reflectedTarget)
        {
            target.LastCombatTime = _gameTime;
            BroadcastPartyMemberUpdateForEntity(target.Id);
            ApplyDamageReflect(channel, reflectedTarget, attacker, healthDamage);
        }

        if (target is MonsterEntity hitMob)
        {
            hitMob.TargetEntityId = session.EntityId;
        }

        BroadcastCombatResult(channel, session.EntityId, targetId, damage, isCrit, target.Health, target.MaxHealth, attacker.X, attacker.Y);

        if (target is MonsterEntity killedMob && target.Health <= 0)
        {
            HandleMonsterDeath(channel, killedMob, attacker, session, targetId);
        }
    }

    private float GetCritChance(PlayerEntity player)
    {
        PruneExpiredServerBuffs(player);
        return player.CalculateCritChance();
    }

    private void PruneExpiredServerBuffs(PlayerEntity player)
    {
        RefreshTemporarySkillBonuses(player);
    }

    private void RefreshTemporarySkillBonuses(PlayerEntity player)
    {
        bool miraApuradaAtiva = player.ActiveServerBuffs.TryGetValue("skill:10202", out double miraApuradaUntil) && miraApuradaUntil > _gameTime;
        bool velocidadeAguiaAtiva =
            (player.ActiveServerBuffs.TryGetValue("skill:10208", out double velocidadeAguiaUntil) && velocidadeAguiaUntil > _gameTime)
            || (player.ActiveServerBuffs.TryGetValue("skill:18", out double velocidadeAguiaLegacyUntil) && velocidadeAguiaLegacyUntil > _gameTime);
        bool reflexosAssassinosAtivo = player.ActiveServerBuffs.TryGetValue("skill:12208", out double reflexosUntil) && reflexosUntil > _gameTime;
        bool sedeDeSangueAtiva = player.ActiveServerBuffs.TryGetValue($"skill:{SedeDeSangueSkillId}", out double sedeUntil) && sedeUntil > _gameTime;
        bool furiaBerserkerAtiva = player.ActiveServerBuffs.TryGetValue($"skill:{FuriaBerserkerSkillId}", out double furiaUntil) && furiaUntil > _gameTime;
        bool frenesiBerserkerAtivo = player.ActiveServerBuffs.TryGetValue($"skill:{FrenesiBerserkerSkillId}", out double frenesiUntil) && frenesiUntil > _gameTime;
        bool forcaBrutalAtiva = player.ActiveServerBuffs.TryGetValue($"skill:{ForcaBrutalSkillId}", out double forcaBrutalUntil) && forcaBrutalUntil > _gameTime;
        bool berserkAtivo = player.ActiveServerBuffs.TryGetValue($"skill:{BerserkSkillId}", out double berserkUntil) && berserkUntil > _gameTime;
        bool deusDaGuerraAtivo = player.ActiveServerBuffs.TryGetValue($"skill:{DeusDaGuerraSkillId}", out double deusUntil) && deusUntil > _gameTime;
        bool sangueDeFerroAtivo = player.ActiveServerBuffs.TryGetValue($"skill:{SangueDeFerroSkillId}", out double sangueUntil) && sangueUntil > _gameTime;
        bool fortificacaoAtiva = player.ActiveServerBuffs.TryGetValue($"skill:{FortificacaoSkillId}", out double fortUntil) && fortUntil > _gameTime;

        player.TemporaryPrecisionBonus = miraApuradaAtiva ? 15f : 0f;
        player.TemporaryCritChanceBonus = (miraApuradaAtiva ? 15f : 0f) + (reflexosAssassinosAtivo ? 10f : 0f);
        player.TemporaryAttackSpeedBonus = (velocidadeAguiaAtiva ? 0.25f : 0f)
            + (reflexosAssassinosAtivo ? 0.10f : 0f)
            + (frenesiBerserkerAtivo ? 0.35f : 0f)
            + (berserkAtivo ? 0.30f : 0f)
            + (deusDaGuerraAtivo ? 0.50f : 0f);
        player.TemporaryLifeStealBonus = (sedeDeSangueAtiva ? 15f : 0f)
            + (deusDaGuerraAtivo ? 15f : 0f);
        player.TemporaryDamageBonus = (furiaBerserkerAtiva ? 0.25f : 0f)
            + (forcaBrutalAtiva ? 0.35f : 0f)
            + (berserkAtivo ? 0.30f : 0f)
            + (deusDaGuerraAtivo ? 0.50f : 0f);
        player.TemporaryDefenseMultiplier = 1f
            * (forcaBrutalAtiva ? 0.80f : 1f)
            * (sangueDeFerroAtivo ? 1.25f : 1f)
            * (fortificacaoAtiva ? 1.15f : 1f);
    }

    private void HandleMonsterDeath(Channel channel, MonsterEntity mob, PlayerEntity killer, PlayerSession killerSession, ulong mobId)
    {
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

        AwardMonsterExperience(channel, mob, killer, killerSession, aoi);

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
                $"{killer.Name} derrotou o Boss Slime! Ele nascerÃ¡ novamente em 1 hora.");
        }

    }

    private void AwardMonsterExperience(Channel channel, MonsterEntity mob, PlayerEntity killer, PlayerSession killerSession, HashSet<ulong> aoi)
    {
        var eligibleMembers = GetEligiblePartyXpMembers(channel, killer);
        int splitCount = Math.Max(1, eligibleMembers.Count);
        int partyBonusPercent = Math.Clamp(eligibleMembers.Count * 5, 0, 25);
        int baseShare = Math.Max(1, (int)Math.Ceiling(mob.ExperienceReward / (double)splitCount));

        foreach (var member in eligibleMembers)
        {
            var memberSession = _sessions.Values.FirstOrDefault(s => s.EntityId == member.Id);
            if (memberSession?.SelectedCharacter == null)
                continue;

            int xpReward = baseShare;
            if (IsPlayerVip(member))
                xpReward *= 2;
            if (member.BonusExperience > 0f)
                xpReward = Math.Max(1, (int)Math.Round(xpReward * (1.0 + member.BonusExperience / 100.0)));
            if (partyBonusPercent > 0)
                xpReward = Math.Max(1, (int)Math.Round(xpReward * (1.0 + partyBonusPercent / 100.0)));

            ApplyExperienceReward(channel, member, memberSession, xpReward, aoi);
        }
    }

    private List<PlayerEntity> GetEligiblePartyXpMembers(Channel channel, PlayerEntity killer)
    {
        if (killer.PartyId < 0)
            return new List<PlayerEntity> { killer };

        var party = _world.Parties.GetParty(killer.PartyId);
        if (party == null)
            return new List<PlayerEntity> { killer };

        var eligible = new List<PlayerEntity>();
        foreach (var memberId in party.Members)
        {
            if (channel.GetEntity(memberId) is not PlayerEntity member || member.Health <= 0)
                continue;

            if (Math.Abs(member.Level - killer.Level) > 20)
                continue;

            eligible.Add(member);
        }

        if (!eligible.Contains(killer))
            eligible.Add(killer);

        return eligible;
    }

    private void ApplyExperienceReward(Channel channel, PlayerEntity player, PlayerSession session, int xpReward, HashSet<ulong> aoi)
    {
        player.Experience += xpReward;

        SendGainExpToAoiAndPlayer(channel, aoi, player.Id, xpReward, player.Experience);

        long xpForNextLevel = XpForNextLevel(player.Level);
        while (player.Experience >= xpForNextLevel)
        {
            player.Experience -= xpForNextLevel;
            player.Level++;
            xpForNextLevel = XpForNextLevel(player.Level);

            RecalculatePlayerStats(player);
            player.Health = player.MaxHealth;
            player.Mana = player.MaxMana;
            player.StatPoints += 5;

            SendLevelUpToAoiAndPlayer(channel, aoi, player.Id, player.Level, player.Experience);

            var peer = channel.GetPlayerPeer(player.Id);
            if (peer != null)
            {
                SendSystemMessage(peer, $"Parabens! Voce alcancou o nivel {player.Level}!");
                SendStatUpdate(peer, player);
                SendTalentData(peer, player);
            }
            BroadcastSingleEntityUpdate(channel, player);
            BroadcastPartyMemberUpdateForEntity(player.Id);
        }

        _db.SaveCharacterXp(session.SelectedCharacter!.Id, player.Experience);
        _db.SaveCharacterLevel(session.SelectedCharacter.Id, player.Level);
        _db.SaveCharacterStats(session.SelectedCharacter.Id, player.BaseForca, player.BaseAgilidade, player.BaseDestreza, player.BaseInteligencia, player.StatPoints);
        session.SelectedCharacter.Xp = player.Experience;
        session.SelectedCharacter.Level = player.Level;
        session.SelectedCharacter.StatPoints = player.StatPoints;

        ApplyPetExperienceReward(channel, player, session, xpReward);
    }

    private void ApplyPetExperienceReward(Channel channel, PlayerEntity owner, PlayerSession session, int xpReward)
    {
        if (session.SelectedCharacter == null || xpReward <= 0)
            return;

        var pet = channel.GetPetOwnedBy(owner.Id);
        if (pet == null || pet.PetId <= 0 || pet.Health <= 0)
            return;

        pet.Experience += xpReward;
        long xpForNextLevel = XpForNextLevel(pet.Level);
        bool leveled = false;
        while (pet.Experience >= xpForNextLevel)
        {
            pet.Experience -= xpForNextLevel;
            pet.Level++;
            xpForNextLevel = XpForNextLevel(pet.Level);
            leveled = true;
        }

        if (leveled)
        {
            float hpPercent = pet.MaxHealth > 0 ? Math.Clamp(pet.Health / (float)pet.MaxHealth, 0.01f, 1f) : 1f;
            pet.MaxHealth = CalculatePetMaxHealth(owner, pet.Level, pet.IsBossPet);
            pet.Health = Math.Max(1, (int)MathF.Ceiling(pet.MaxHealth * hpPercent));
            BroadcastSingleEntityUpdate(channel, pet);
        }

        _db.SavePetProgress(session.SelectedCharacter.Id, pet.PetId, pet.Level, pet.Experience);
        var peer = channel.GetPlayerPeer(owner.Id);
        if (peer != null)
            SendPetData(peer, session.SelectedCharacter.Id);
    }

    private void SendGainExpToAoiAndPlayer(Channel channel, HashSet<ulong> aoi, ulong playerId, int xpReward, long totalExperience)
    {
        var recipients = aoi.ToHashSet();
        recipients.Add(playerId);

        foreach (var eid in recipients)
        {
            var peer = channel.GetPlayerPeer(eid);
            if (peer == null)
                continue;

            var writer = PacketSerializer.WritePacket(PacketId.S2C_GainExp);
            writer.Put(playerId);
            writer.Put(xpReward);
            writer.Put(totalExperience);
            peer.Send(writer, DeliveryMethod.ReliableOrdered);
        }
    }

    private void SendLevelUpToAoiAndPlayer(Channel channel, HashSet<ulong> aoi, ulong playerId, int level, long experience)
    {
        var recipients = aoi.ToHashSet();
        recipients.Add(playerId);

        foreach (var eid in recipients)
        {
            var peer = channel.GetPlayerPeer(eid);
            if (peer == null)
                continue;

            var writer = PacketSerializer.WritePacket(PacketId.S2C_LevelUp);
            writer.Put(playerId);
            writer.Put(level);
            writer.Put((int)Math.Min(int.MaxValue, experience));
            peer.Send(writer, DeliveryMethod.ReliableOrdered);
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
        int equipmentLevel = mob.Level < 10 ? 1 : Math.Min(100, (mob.Level / 10) * 10);
        double equipmentDropChance = template.DropsEliteEquipment
            ? template.EliteDropChance
            : Math.Clamp(template.EliteDropChance, 0.0, 1.0);
        if (equipmentLevel is 1 or 10)
            equipmentDropChance = Math.Min(1.0, equipmentDropChance * 1.5);

        if (dropsEquipment && rng.NextDouble() < equipmentDropChance)
        {
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
            var recipients = aoi.ToHashSet();
            recipients.Add(killer.Id);

            var w = PacketSerializer.WritePacket(PacketId.S2C_LootSpawn);
            w.Put(loot.Id);
            w.Put(loot.X);
            w.Put(loot.Y);
            w.Put(loot.ItemId);
            w.Put(loot.Quantity);

            foreach (var eid in recipients)
            {
                var p = channel.GetPlayerPeer(eid);
                if (p == null)
                    continue;

                p.Send(w, DeliveryMethod.ReliableOrdered);
                if (_sessions.TryGetValue(p, out var session))
                    session.SpawnedLoot.Add(loot.Id);

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
        float pickupRange = 80f;
        if (session.SelectedCharacter != null)
        {
            var collarExpiry = _db.LoadPetCollarExpiry(session.SelectedCharacter.Id);
            if (collarExpiry > DateTime.UtcNow)
                pickupRange = PetLootPickupRange;
        }

        if (MathF.Sqrt(dx * dx + dy * dy) > pickupRange) return;

        if (loot.ItemId == 0)
        {
            player.Gold += loot.Quantity;
            if (session.SelectedCharacter != null)
                _db.SaveCharacterGold(session.SelectedCharacter.Id, player.Gold);
            SendGoldUpdate(peer, player.Gold);
        }
        else
        {
            bool itemAdded = loot.PreservedItem != null
                ? TryAddItemInstanceToInventory(player, character.Id, loot.PreservedItem)
                : TryAddItemToInventory(player, character.Id, loot.ItemId, loot.Quantity);

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
                SendSystemMessage(peer, "InventÃ¡rio cheio!");
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

    private int CalculatePetDamage(Channel channel, PlayerEntity owner, Entity target, out bool isCrit)
    {
        int ownerDamage = CalculateBasicDamage(owner, target, out isCrit);
        var pet = channel.GetPetOwnedBy(owner.Id);
        float multiplier = pet?.IsBossPet == true ? 0.30f : 0.20f;
        return Math.Max(1, (int)MathF.Ceiling(ownerDamage * multiplier));
    }

    private static int CalculatePetMaxHealth(PlayerEntity owner, int petLevel, bool isBossPet)
    {
        int level = Math.Max(1, petLevel);
        float ownerHpShare = isBossPet ? 0.45f : 0.35f;
        int levelBonus = (level - 1) * (isBossPet ? 10 : 7);
        return Math.Max(40, (int)MathF.Round(owner.MaxHealth * ownerHpShare) + levelBonus);
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

        SendSystemMessage(peer, "PoÃ§Ã£o de vida spawnada! Aproxime e aperte F para pegar.");
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
    public ServerSkillDefinition? Skill { get; init; }
    public float ChargePercent { get; init; } = 1f;
}

internal sealed class PendingProjectileFire
{
    public double FireAt { get; init; }
    public int ChannelId { get; init; }
    public ulong CasterId { get; init; }
    public float OriginX { get; init; }
    public float OriginY { get; init; }
    public float DirX { get; init; }
    public float DirY { get; init; }
    public ServerSkillDefinition Skill { get; init; } = null!;
    public bool NotifyMiss { get; init; }
    public NetPeer? Peer { get; init; }
}

internal sealed class PendingMonsterProjectileHit
{
    public double ImpactAt { get; init; }
    public int ChannelId { get; init; }
    public ulong CasterId { get; init; }
    public ulong TargetId { get; init; }
    public float DamageMultiplier { get; init; } = 1f;
    public int SkillId { get; init; }
    public bool AppliesSlow { get; init; }
}

internal sealed class PendingDotTick
{
    public double TickAt { get; init; }
    public int ChannelId { get; init; }
    public ulong CasterId { get; init; }
    public ulong TargetId { get; init; }
    public int SkillId { get; init; }
    public int Damage { get; init; }
    public bool IsCrit { get; init; }
}

internal sealed class PendingHealTick
{
    public double TickAt { get; init; }
    public int ChannelId { get; init; }
    public ulong CasterId { get; init; }
    public ulong TargetId { get; init; }
    public int SkillId { get; init; }
    public int HealthAmount { get; init; }
    public int ManaAmount { get; init; }
    public bool StartVisual { get; init; }
}

internal sealed class PendingAreaSkillTick
{
    public double TickAt { get; init; }
    public int ChannelId { get; init; }
    public ulong CasterId { get; init; }
    public ServerSkillDefinition Skill { get; init; } = null!;
    public float Radius { get; init; }
    public int TickCount { get; init; } = 1;
    public bool AppliesSlow { get; init; }
    public double SlowDuration { get; init; }
}

internal sealed class PendingFreneticStrike
{
    public double StrikeAt { get; init; }
    public int ChannelId { get; init; }
    public ulong CasterId { get; init; }
    public float DirX { get; init; }
    public float DirY { get; init; }
    public float Range { get; init; }
    public float HitRadius { get; init; }
    public ServerSkillDefinition Skill { get; init; } = null!;
    public int StrikeCount { get; init; } = 1;
}

internal sealed class ActiveBastionArea
{
    public int ChannelId { get; init; }
    public ulong CasterId { get; init; }
    public int PartyId { get; init; }
    public float X { get; init; }
    public float Y { get; init; }
    public float Radius { get; init; }
    public int ReductionPercent { get; init; }
    public double ExpiresAt { get; init; }
    public int MaxHits { get; init; } = 8;
    public int HitsTaken { get; set; }
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
            Logger.Info("SkillCatalog: raiz do projeto nÃ£o encontrada; catÃ¡logo vazio.");
            return result;
        }

        string skillsDir = Path.Combine(root, "skills", "habilidades");
        if (!Directory.Exists(skillsDir))
        {
            Logger.Info($"SkillCatalog: pasta nÃ£o encontrada: {skillsDir}");
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
        return text.Contains("ÃƒÂ¡rea") || text.Contains("area") || text.Contains("multi") || text.Contains("chuva") || text.Contains("explos");
    }

    private static bool IsAreaSkill(string tipo, string efeito)
    {
        string text = $"{tipo} {efeito}".ToLowerInvariant();
        return text.Contains("Ã¡rea") || text.Contains("area") || text.Contains("multi") || text.Contains("chuva") || text.Contains("explos");
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
