using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using LiteNetLib;
using LiteNetLib.Utils;
using Mithara.Server.Entities;
using Mithara.Server.Packets;
using Mithara.Server.Quests;
using Mithara.Server.World;

namespace Mithara.Server.Network;

partial class GameServer
{
    private const int TalentPointsPerLevel = 3;

    private static readonly Dictionary<string, int[]> _classStartingItems = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Arqueiro"] = new[] { 1000 },
        ["Mago"] = new[] { 1066 },
        ["Ladino"] = new[] { 1011, 1022 },
        ["Berseker"] = new[] { 1033 },
        ["Berserker"] = new[] { 1033 },
        ["Guardiao"] = new[] { 1044, 1055 },
        ["Guardião"] = new[] { 1044, 1055 },
        ["Prist"] = new[] { 1077, 1088 },
        ["Clerigo"] = new[] { 1077, 1088 },
        ["Clérigo"] = new[] { 1077, 1088 },
    };

    private static int[] GetStartingItemsForClass(string className)
    {
        if (string.IsNullOrWhiteSpace(className)) return Array.Empty<int>();
        var catalogItems = ItemDefinitions.GetAll()
            .Where(def => !def.IsElite
                && def.RequiredLevel <= 1
                && def.Type is ItemType.Weapon or ItemType.Shield
                && IsItemAllowedForStartingClass(def, className))
            .OrderBy(def => def.Type == ItemType.Weapon ? 0 : 1)
            .ThenBy(def => def.Id)
            .Select(def => def.Id)
            .Take(2)
            .ToArray();

        if (catalogItems.Length > 0)
            return catalogItems;

        return _classStartingItems.TryGetValue(className, out var items) ? items : Array.Empty<int>();
    }

    private static bool IsItemAllowedForStartingClass(ItemDefinition def, string className)
    {
        if (string.IsNullOrWhiteSpace(def.AllowedClasses))
            return true;

        string wanted = NormalizeClassAlias(className);
        return def.AllowedClasses
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(allowed => NormalizeClassAlias(allowed) == wanted);
    }

    private static string NormalizeClassAlias(string className)
    {
        string normalized = className.Trim().ToLowerInvariant();
        return normalized switch
        {
            "berserker" => "berseker",
            "guardião" or "guardiÃ£o" => "guardiao",
            "clerigo" or "clérigo" or "clÃ©rigo" => "prist",
            _ => normalized,
        };
    }

    private void GiveStartingItems(int characterId, string className)
    {
        var itemIds = GetStartingItemsForClass(className);
        int invSlot = 0;
        int count = 0;
        foreach (var itemId in itemIds)
        {
            var def = ItemDefinitions.Get(itemId);
            if (def == null) continue;

            int eqSlot = def.Type switch
            {
                ItemType.Weapon => 107,
                ItemType.Shield => 108,
                ItemType.Helmet => 101,
                ItemType.Chestplate => 102,
                ItemType.Belt => 103,
                ItemType.Gloves => 104,
                ItemType.Pants => 105,
                ItemType.Boots => 106,
                ItemType.Necklace => 109,
                ItemType.Ring => 110,
                ItemType.Earring => 111,
                ItemType.Bag => 0, // bags go to inventory for manual equipping
                _ => -1,
            };

            int slot = eqSlot > 0 ? eqSlot : invSlot++;
            var item = new ItemInstance
            {
                ItemId = itemId,
                Slot = slot,
                Quantity = 1,
            };
            ItemRoller.EnsureRolled(item, ItemRarity.Common);
            _db.DeleteItemBySlot(characterId, slot);
            _db.SaveItem(characterId, item);
            count++;
        }
        if (count > 0)
            Logger.Info($"Itens iniciais dados ao personagem {characterId}: {count} item(ns)");
    }

    private void HandleCreateCharacter(NetPeer peer, NetDataReader reader)
    {
        string name = reader.GetString();
        string className = reader.GetString();
        string race = reader.GetString();

        if (!_sessions.TryGetValue(peer, out var session)) return;

        int charId = _db.CreateCharacter(session.AccountId, name, className, race);

        GiveStartingItems(charId, className);

        var chars = _db.GetCharacters(session.AccountId);

        var created = chars.FirstOrDefault(c => c.Id == charId);
        if (created != null)
            session.SelectedCharacter = created;

        var writer = PacketSerializer.WritePacket(PacketId.S2C_LoginResult);
        writer.Put(true);
        writer.Put(session.AccountId);
        writer.Put(chars.Count);
        foreach (var ch in chars)
        {
            writer.Put(ch.SlotIndex);
            writer.Put(ch.Name);
            writer.Put(ch.Class);
            writer.Put(ch.Race);
            writer.Put(ch.Level);
        }
        peer.Send(writer, DeliveryMethod.ReliableOrdered);

        Logger.Info($"Personagem criado: {name} (id={charId})");
    }

    private void HandleSelectCharacter(NetPeer peer, NetDataReader reader)
    {
        int slotIndex = reader.GetInt();

        if (!_sessions.TryGetValue(peer, out var session)) return;

        var chars = _db.GetCharacters(session.AccountId);
        var selected = chars.FirstOrDefault(c => c.SlotIndex == slotIndex);
        if (selected == null) return;

        session.SelectedCharacter = selected;
    }

    private void HandleLeaveWorld(NetPeer peer)
    {
        if (!_sessions.TryGetValue(peer, out var session)) return;

        if (session.EntityId > 0 && session.ChannelId >= 0)
        {
            var channel = _world.GetChannel(session.ChannelId);
            var player = channel?.GetEntity(session.EntityId) as PlayerEntity;

            if (player != null && session.SelectedCharacter != null)
                _db.SaveCharacterFull(session.SelectedCharacter.Id, player, session.SelectedCharacter.BankGold);

            if (player?.PartyId >= 0)
                HandlePartyLeave(player);

            if (channel != null)
            {
                channel.RemoveEntity(session.EntityId);
                BroadcastDespawn(channel, session.EntityId);
            }
        }

        Logger.Info($"{session.SelectedCharacter?.Name ?? "Jogador"} saiu do mundo e voltou à seleção.");
        session.EntityId = 0;
        session.ChannelId = -1;
        session.SelectedCharacter = null;
        session.SpawnedEntities.Clear();
        session.SpawnedLoot.Clear();

        var characters = _db.GetCharacters(session.AccountId);
        var writer = PacketSerializer.WritePacket(PacketId.S2C_LeaveWorld);
        writer.Put(characters.Count);
        foreach (var character in characters)
        {
            writer.Put(character.SlotIndex);
            writer.Put(character.Name);
            writer.Put(character.Class);
            writer.Put(character.Race);
            writer.Put(character.Level);
        }
        peer.Send(writer, DeliveryMethod.ReliableOrdered);
    }

    private void HandleDeleteCharacter(NetPeer peer, NetDataReader reader)
    {
        int slotIndex = reader.GetInt();
        if (!_sessions.TryGetValue(peer, out var session)) return;

        var chars = _db.GetCharacters(session.AccountId);
        var toDelete = chars.FirstOrDefault(c => c.SlotIndex == slotIndex);
        if (toDelete == null)
        {
            Logger.Info($"[DELETE] Personagem slot {slotIndex} não encontrado para account {session.AccountId}");
            return;
        }

        _db.DeleteCharacter(toDelete.Id, toDelete.Name);

        var updated = _db.GetCharacters(session.AccountId);
        var writer = PacketSerializer.WritePacket(PacketId.S2C_CharacterDeleted);
        writer.Put(updated.Count);
        foreach (var ch in updated)
        {
            writer.Put(ch.SlotIndex);
            writer.Put(ch.Name);
            writer.Put(ch.Class);
            writer.Put(ch.Race);
            writer.Put(ch.Level);
        }
        peer.Send(writer, DeliveryMethod.ReliableOrdered);

        if (session.SelectedCharacter?.SlotIndex == slotIndex)
            session.SelectedCharacter = null;

        Logger.Info($"Personagem deletado: {toDelete.Name} (slot {slotIndex})");
    }

    private void HandleEnterWorld(NetPeer peer, NetDataReader reader)
    {
        int channelId = reader.GetInt();
        string inlineName = reader.GetString();
        string inlineClass = reader.GetString();
        string inlineRace = reader.GetString();
        float inlineX = reader.GetFloat();
        float inlineY = reader.GetFloat();

        if (!_sessions.TryGetValue(peer, out var session)) return;

        // Remove old player if re-entering world (e.g. after returning to character selection)
        if (session.EntityId > 0 && session.ChannelId >= 0)
            _world.RemoveFromChannel(session.ChannelId, session.EntityId);

        // Use character from DB if available, otherwise use inline data (quick test mode)
        bool useInline = session.SelectedCharacter == null;
        var ch = session.SelectedCharacter;

        if (ch != null)
            session.CurrentMap = ch.CurrentMap;

        string name = useInline ? inlineName : ch!.Name;
        string charClass = useInline ? inlineClass : ch!.Class;
        string race = useInline ? inlineRace : ch!.Race;
        float posX = useInline ? inlineX : ch!.PosX;
        float posY = useInline ? inlineY : ch!.PosY;
        int level = useInline ? 1 : ch!.Level;
        long xp = useInline ? 0 : ch!.Xp;
        int forca = Math.Max(5, useInline ? 5 : ch!.Forca);
        int agilidade = Math.Max(5, useInline ? 5 : ch!.Agilidade);
        int destreza = Math.Max(5, useInline ? 5 : ch!.Destreza);
        int inteligencia = Math.Max(5, useInline ? 5 : ch!.Inteligencia);
        int gold = useInline ? 100 : ch!.Gold;
        int statPoints = useInline ? 10 : ch!.StatPoints;

        int baseAttack = charClass.ToLowerInvariant() switch
        {
            "guerreiro" => 10,
            "arqueiro" => 7,
            "mago" => 5,
            _ => 6,
        };
        int baseDefense = charClass.ToLowerInvariant() switch
        {
            "guerreiro" => 8,
            "arqueiro" => 4,
            "mago" => 2,
            _ => 4,
        };
        int maxHp = 80 + forca * 2 + level * 10;
        int maxMana = 30 + inteligencia * 3 + level * 5;

        var player = new PlayerEntity
        {
            AccountId = session.AccountId,
            SlotIndex = ch?.SlotIndex ?? 0,
            Name = name,
            CharacterClass = charClass,
            Race = race,
            Level = level,
            X = posX,
            Y = posY,
            Speed = 185f,
            Health = maxHp,
            MaxHealth = maxHp,
            Mana = maxMana,
            MaxMana = maxMana,
            Forca = forca,
            Agilidade = agilidade,
            Destreza = destreza,
            Inteligencia = inteligencia,
            BaseForca = forca,
            BaseAgilidade = agilidade,
            BaseDestreza = destreza,
            BaseInteligencia = inteligencia,
            Experience = xp,
            BaseAttack = baseAttack,
            Defense = baseDefense,
            FactionId = GetFactionForRace(race),
            Gold = gold,
            StatPoints = statPoints,
        };

        if (!useInline && ch != null)
        {
            player.UnlockedTalents = _db.GetCharacterTalents(ch.Id);
            player.SkillBarSlots = _db.GetCharacterSkillSlots(ch.Id);
            SanitizeSkillBar(player);
        }

        ulong entityId = _world.SpawnPlayerInChannel(channelId, player, peer);
        session.EntityId = entityId;
        session.ChannelId = channelId;
        session.IsTransitioning = false;

        // Restore guild membership
        var (guildId, oldEntityId, rank) = _db.GetCharacterGuildData(name);
        if (guildId >= 0)
        {
            var guild = _world.Guilds.GetGuild(guildId);
            if (guild != null)
            {
                // Replace stale entity ID with current runtime ID
                if (oldEntityId > 0 && oldEntityId != entityId)
                    _world.Guilds.ReplaceMemberEntityId(guildId, oldEntityId, entityId);

                _world.Guilds.SetMemberRank(guildId, entityId, rank);

                // Persist current entity ID in database
                if (oldEntityId != entityId)
                {
                    _db.DeleteGuildMemberByName(guildId, name);
                    _db.SaveGuildMember(guildId, entityId, name, rank);
                }

                player.GuildId = guildId;
                player.GuildName = guild.Name;
            }
            else
            {
                _db.DeleteGuildMemberByName(guildId, name);
            }
        }

        var channel = _world.GetChannel(channelId);

        var writer = PacketSerializer.WritePacket(PacketId.S2C_EnterWorld);
        writer.Put(entityId);
        writer.Put(channelId);
        writer.Put(player.X);
        writer.Put(player.Y);
        writer.Put(player.Level);
        writer.Put(player.Experience);
        writer.Put(player.Forca);
        writer.Put(player.Agilidade);
        writer.Put(player.Destreza);
        writer.Put(player.Inteligencia);
        writer.Put(player.Health);
        writer.Put(player.MaxHealth);
        writer.Put(player.Mana);
        writer.Put(player.MaxMana);
        writer.Put(player.BaseAttack);
        writer.Put(player.Defense);
        writer.Put(player.StatPoints);
        peer.Send(writer, DeliveryMethod.ReliableOrdered);

        if (!useInline && ch != null)
        {
            var dbQuests = _db.GetPlayerQuests(ch.Id);
            foreach (var (questId, progressJson, completed, claimed) in dbQuests)
            {
                try
                {
                    var progress = System.Text.Json.JsonSerializer.Deserialize<List<int>>(progressJson) ?? new List<int>();
                    player.Quests[questId] = new PlayerQuest
                    {
                        QuestId = questId,
                        Progress = progress,
                        Completed = completed,
                        Claimed = claimed,
                    };
                }
                catch { }
            }

            var items = _db.LoadItems(ch.Id);
            bool repairedStartingEquipment = false;
            bool hasWeapon = items.Any(item =>
                item.Slot == 107 && ItemDefinitions.Get(item.ItemId)?.Type == ItemType.Weapon);
            if (player.Level == 1 && !hasWeapon)
            {
                GiveStartingItems(ch.Id, player.CharacterClass);
                items = _db.LoadItems(ch.Id);
                repairedStartingEquipment = items.Any(item =>
                    item.Slot == 107 && ItemDefinitions.Get(item.ItemId)?.Type == ItemType.Weapon);
                Logger.Info($"Reparo de arma inicial para {player.Name}: {(repairedStartingEquipment ? "sucesso" : "falhou")}");
            }

            var officialStartingItems = GetStartingItemsForClass(player.CharacterClass).ToHashSet();
            foreach (var item in items)
            {
                bool repairedStarterRarity = player.Level == 1
                    && item.Slot >= 100
                    && officialStartingItems.Contains(item.ItemId)
                    && item.Roll.IsRolled
                    && item.Roll.Rarity != ItemRarity.Common;
                if (repairedStarterRarity)
                {
                    item.Roll = new ItemRoll();
                    ItemRoller.EnsureRolled(item, ItemRarity.Common);
                    _db.SaveItem(ch.Id, item);
                    Logger.Info($"Raridade do item inicial {item.ItemId} corrigida para Comum em {player.Name}.");
                }

                bool neededRoll = !item.Roll.IsRolled && item.Definition is { IsStackable: false };
                ItemRoller.EnsureRolled(item);
                if (neededRoll) _db.SaveItem(ch.Id, item);
                if (item.Slot >= 100 && item.Slot <= 116)
                    player.Equipment[item.Slot - 100] = item;
                else
                    player.Items.Add(item);
            }
            NormalizeLoadedInventory(player, ch.Id);

            RecalculatePlayerStats(player);

            if (player.Level == 1 && player.Equipment.ContainsKey((int)ItemType.Weapon))
                SendSystemMessage(peer, repairedStartingEquipment
                    ? "Sua arma inicial estava ausente e foi equipada automaticamente."
                    : "Sua arma inicial está equipada.");
        }

        SendInventoryData(peer, player);
        SendStatUpdate(peer, player);
        SendTalentData(peer, player);
        SendSkillBarData(peer, player);
        SendGoldUpdate(peer, player.Gold);
        if (!useInline && ch != null)
            SendPetData(peer, ch.Id);

        var vipExpiry = _db.LoadVipExpiry(session.AccountId);
        player.VipExpiry = vipExpiry;
        SendVipStatus(peer, vipExpiry);

            if (channel != null)
            {
                var aoi = channel.GetEntitiesInAoi(player.X, player.Y);
                Logger.Info($"HandleEnterWorld({player.Name}): AOI contem {aoi.Count} entidades (incluindo self)");

                session.SpawnedEntities.Clear();
                session.SpawnedEntities.Add(entityId);
                session.SpawnedLoot.Clear();

                int sentNearby = 0;
                foreach (var eid in aoi)
                {
                    if (eid == entityId) continue;
                    var existing = channel.GetEntity(eid);
                    if (existing == null) continue;
                    var existingWriter = PacketSerializer.WritePacket(PacketId.S2C_SpawnEntity);
                    WriteEntityPacket(existingWriter, existing);
                    peer.Send(existingWriter, DeliveryMethod.ReliableOrdered);
                    session.SpawnedEntities.Add(eid);
                    sentNearby++;
                }

                Logger.Info($"HandleEnterWorld({player.Name}): enviou {sentNearby} spawn(s) da AOI");

                BroadcastSpawnToNearby(channel, player, player.X, player.Y);
            }

        Logger.Info($"{player.Name} entrou no mundo (canal {channelId})");
    }

    private void HandleTalentUnlock(NetPeer peer, NetDataReader reader)
    {
        if (!_sessions.TryGetValue(peer, out var session) || session.SelectedCharacter == null)
            return;

        var channel = _world.GetChannel(session.ChannelId);
        var player = channel?.GetEntity(session.EntityId) as PlayerEntity;
        if (player == null || player.Health <= 0)
            return;

        string nodeId = reader.GetString();
        if (string.IsNullOrWhiteSpace(nodeId))
            return;

        var node = ServerTalentCatalog.GetNodeForClass(player.CharacterClass, nodeId);
        if (node == null)
        {
            SendSystemMessage(peer, "Talento inválido para sua classe.");
            SendTalentData(peer, player);
            return;
        }

        if (player.UnlockedTalents.Contains(nodeId))
        {
            SendTalentData(peer, player);
            return;
        }

        if (player.Level < node.NivelMinimo)
        {
            SendSystemMessage(peer, $"Nível {node.NivelMinimo} necessário para desbloquear {node.Nome}.");
            SendTalentData(peer, player);
            return;
        }

        int available = GetAvailableTalentPoints(player);
        if (available < node.CustoPontos)
        {
            SendSystemMessage(peer, "Pontos de talento insuficientes.");
            SendTalentData(peer, player);
            return;
        }

        if (!ServerTalentCatalog.RequirementsSatisfied(player.CharacterClass, node, player.UnlockedTalents))
        {
            SendSystemMessage(peer, "Requisitos do talento ainda não foram cumpridos.");
            SendTalentData(peer, player);
            return;
        }


        if (!ServerTalentCatalog.SpecializationAllowed(player.CharacterClass, node, player.UnlockedTalents, out string lockedSpec))
        {
            SendSystemMessage(peer, $"Voce ja escolheu a especializacao {lockedSpec}. Use um Pergaminho de Reset de Talentos para trocar.");
            SendTalentData(peer, player);
            return;
        }
        player.UnlockedTalents.Add(nodeId);
        _db.SaveCharacterTalent(session.SelectedCharacter.Id, nodeId);
        SendTalentData(peer, player);
        SendSkillBarData(peer, player);
        SendSystemMessage(peer, $"Talento desbloqueado: {node.Nome}");
    }

    private void HandleSetSkillSlot(NetPeer peer, NetDataReader reader)
    {
        if (!_sessions.TryGetValue(peer, out var session) || session.SelectedCharacter == null)
            return;

        var channel = _world.GetChannel(session.ChannelId);
        var player = channel?.GetEntity(session.EntityId) as PlayerEntity;
        if (player == null)
            return;

        int slotIndex = reader.GetInt();
        int skillId = reader.GetInt();
        if (slotIndex < 0 || slotIndex >= player.SkillBarSlots.Length)
        {
            SendSkillBarData(peer, player);
            return;
        }

        if (skillId > 0)
        {
            var skill = ServerSkillCatalog.Get(skillId);
            if (skill == null)
            {
                SendSystemMessage(peer, "Skill inexistente.");
                SendSkillBarData(peer, player);
                return;
            }

            if (!ServerSkillCatalog.ClassMatches(player.CharacterClass, skill.ClasseRestrita))
            {
                SendSystemMessage(peer, "Esta skill não pertence à sua classe.");
                SendSkillBarData(peer, player);
                return;
            }

            if (!ServerTalentCatalog.IsSkillUnlocked(player.CharacterClass, player.UnlockedTalents, skillId))
            {
                SendSystemMessage(peer, "Desbloqueie esta skill na árvore de talentos antes de colocar na barra.");
                SendSkillBarData(peer, player);
                return;
            }
        }

        player.SkillBarSlots[slotIndex] = Math.Max(0, skillId);
        _db.SaveCharacterSkillSlot(session.SelectedCharacter.Id, slotIndex, player.SkillBarSlots[slotIndex]);
        SendSkillBarData(peer, player);
    }

    private void SendTalentData(NetPeer peer, PlayerEntity player)
    {
        var writer = PacketSerializer.WritePacket(PacketId.S2C_TalentData);
        writer.Put(GetAvailableTalentPoints(player));
        writer.Put(player.UnlockedTalents.Count);
        foreach (string nodeId in player.UnlockedTalents.OrderBy(id => id, StringComparer.Ordinal))
            writer.Put(nodeId);
        peer.Send(writer, DeliveryMethod.ReliableOrdered);
    }

    private void SendSkillBarData(NetPeer peer, PlayerEntity player)
    {
        SanitizeSkillBar(player);
        var writer = PacketSerializer.WritePacket(PacketId.S2C_SkillBarData);
        writer.Put(player.SkillBarSlots.Length);
        foreach (int skillId in player.SkillBarSlots)
            writer.Put(skillId);
        peer.Send(writer, DeliveryMethod.ReliableOrdered);
    }

    private static void SanitizeSkillBar(PlayerEntity player)
    {
        if (player.SkillBarSlots == null || player.SkillBarSlots.Length != 20)
            player.SkillBarSlots = new int[20];

        for (int i = 0; i < player.SkillBarSlots.Length; i++)
        {
            int skillId = player.SkillBarSlots[i];
            if (skillId <= 0)
                continue;

            var skill = ServerSkillCatalog.Get(skillId);
            if (skill == null
                || !ServerSkillCatalog.ClassMatches(player.CharacterClass, skill.ClasseRestrita)
                || !ServerTalentCatalog.IsSkillUnlocked(player.CharacterClass, player.UnlockedTalents, skillId))
            {
                player.SkillBarSlots[i] = 0;
            }
        }
    }

    private static int GetAvailableTalentPoints(PlayerEntity player)
    {
        int earned = Math.Max(1, player.Level) * TalentPointsPerLevel;
        int spent = ServerTalentCatalog.GetSpentPoints(player.CharacterClass, player.UnlockedTalents);
        return Math.Max(0, earned - spent);
    }
}

internal sealed class ServerTalentNode
{
    public string NodeId { get; init; } = "";
    public string Nome { get; init; } = "";
    public int CustoPontos { get; init; } = 1;
    public int NivelMinimo { get; init; } = 1;
    public int SkillId { get; init; }
    public bool TemEscolhaStatus { get; init; }
    public string[] Requisitos { get; init; } = Array.Empty<string>();
    public string SpecializationKey { get; init; } = "";
}

internal static class ServerTalentCatalog
{
    private static readonly Lazy<Dictionary<string, Dictionary<string, ServerTalentNode>>> Trees = new(LoadAll);

    public static ServerTalentNode? GetNodeForClass(string className, string nodeId)
    {
        var tree = GetTreeForClass(className);
        if (tree == null) return null;
        tree.TryGetValue(nodeId, out var node);
        return node;
    }

    public static int GetSpentPoints(string className, HashSet<string> unlocked)
    {
        var tree = GetTreeForClass(className);
        if (tree == null) return 0;
        int total = 0;
        foreach (string nodeId in unlocked)
            if (tree.TryGetValue(nodeId, out var node))
                total += Math.Max(0, node.CustoPontos);
        return total;
    }

    public static bool RequirementsSatisfied(string className, ServerTalentNode node, HashSet<string> unlocked)
    {
        var tree = GetTreeForClass(className);
        if (tree == null) return false;
        foreach (string req in node.Requisitos)
            if (!RequirementSatisfied(tree, req, unlocked))
                return false;
        return true;
    }

    public static bool SpecializationAllowed(string className, ServerTalentNode node, HashSet<string> unlocked, out string lockedSpec)
    {
        lockedSpec = "";
        if (string.IsNullOrWhiteSpace(node.SpecializationKey))
            return true;

        var tree = GetTreeForClass(className);
        if (tree == null) return false;

        foreach (string unlockedNodeId in unlocked)
        {
            if (!tree.TryGetValue(unlockedNodeId, out var unlockedNode))
                continue;
            if (string.IsNullOrWhiteSpace(unlockedNode.SpecializationKey))
                continue;
            if (string.Equals(unlockedNode.SpecializationKey, node.SpecializationKey, StringComparison.OrdinalIgnoreCase))
                return true;

            lockedSpec = unlockedNode.SpecializationKey;
            return false;
        }

        return true;
    }

    public static bool IsSkillUnlocked(string className, HashSet<string> unlocked, int skillId)
    {
        if (skillId <= 0) return false;
        var tree = GetTreeForClass(className);
        if (tree == null) return false;

        foreach (var node in tree.Values)
            if (node.SkillId == skillId)
                return unlocked.Contains(node.NodeId);

        return false;
    }

    private static bool RequirementSatisfied(Dictionary<string, ServerTalentNode> tree, string nodeId, HashSet<string> unlocked)
    {
        if (unlocked.Contains(nodeId)) return true;
        if (!tree.TryGetValue(nodeId, out var reqNode) || !reqNode.TemEscolhaStatus)
            return false;
        foreach (string parent in reqNode.Requisitos)
            if (!RequirementSatisfied(tree, parent, unlocked))
                return false;
        return true;
    }

    private static Dictionary<string, ServerTalentNode>? GetTreeForClass(string className)
    {
        foreach (var (key, tree) in Trees.Value)
            if (ServerSkillCatalog.ClassMatches(className, key))
                return tree;
        return null;
    }

    private static Dictionary<string, Dictionary<string, ServerTalentNode>> LoadAll()
    {
        var result = new Dictionary<string, Dictionary<string, ServerTalentNode>>(StringComparer.OrdinalIgnoreCase);
        string? root = FindProjectRoot();
        if (root == null) return result;

        string treeDir = Path.Combine(root, "skills", "ArvoresClasses");
        if (!Directory.Exists(treeDir)) return result;

        foreach (string path in Directory.EnumerateFiles(treeDir, "Arvore*.tres"))
        {
            string className = Path.GetFileNameWithoutExtension(path).Replace("Arvore", "", StringComparison.OrdinalIgnoreCase);
            if (string.Equals(className, "Berserk", StringComparison.OrdinalIgnoreCase))
                className = "Berseker";
            if (string.Equals(className, "Sacerdote", StringComparison.OrdinalIgnoreCase))
                className = "Clerigo";

            string text = File.ReadAllText(path);
            var skillResourceIds = GetSkillResourceIds(root, text);
            var tree = new Dictionary<string, ServerTalentNode>(StringComparer.Ordinal);
            foreach (Match block in Regex.Matches(text, @"(?s)\[sub_resource[^\]]+\]\s*(?<body>.*?)(?=\n\[sub_resource|\n\[resource\]|\z)"))
            {
                string body = block.Groups["body"].Value;
                string nodeId = GetString(body, "NodeId");
                if (string.IsNullOrWhiteSpace(nodeId)) continue;

                tree[nodeId] = new ServerTalentNode
                {
                    NodeId = nodeId,
                    Nome = GetString(body, "Nome"),
                    CustoPontos = Math.Max(0, GetInt(body, "CustoPontos", 1)),
                    NivelMinimo = Math.Max(1, GetInt(body, "NivelMinimo", 1)),
                    SkillId = GetNodeSkillId(body, skillResourceIds),
                    TemEscolhaStatus = body.Contains("StatOptionIds", StringComparison.Ordinal),
                    Requisitos = GetRequirements(body),
                    SpecializationKey = GetSpecializationKey(nodeId),
                };
            }

            if (tree.Count > 0)
                result[className] = tree;
        }

        Logger.Info($"TalentCatalog: {result.Count} árvore(s) carregada(s).");
        return result;
    }

    private static string? FindProjectRoot()
    {
        foreach (string start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            var dir = new DirectoryInfo(start);
            while (dir != null)
            {
                if (Directory.Exists(Path.Combine(dir.FullName, "skills", "ArvoresClasses")))
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

    private static Dictionary<string, int> GetSkillResourceIds(string root, string treeText)
    {
        var result = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (Match match in Regex.Matches(treeText, @"\[ext_resource[^\]]*script_class=""SkillResource""[^\]]*path=""(?<path>[^""]+)""[^\]]*id=""(?<id>[^""]+)""[^\]]*\]"))
        {
            string id = match.Groups["id"].Value;
            string resourcePath = match.Groups["path"].Value;
            if (!resourcePath.StartsWith("res://", StringComparison.Ordinal))
                continue;

            string filePath = Path.Combine(root, resourcePath["res://".Length..].Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(filePath))
                continue;

            int skillId = GetInt(File.ReadAllText(filePath), "SkillId", 0);
            if (skillId > 0)
                result[id] = skillId;
        }
        return result;
    }

    private static int GetNodeSkillId(string body, Dictionary<string, int> skillResourceIds)
    {
        var match = Regex.Match(body, @"(?m)^HabilidadeAtiva\s*=\s*ExtResource\(""(?<id>[^""]+)""\)");
        return match.Success && skillResourceIds.TryGetValue(match.Groups["id"].Value, out int skillId)
            ? skillId
            : 0;
    }

    private static int GetInt(string text, string key, int fallback)
    {
        var match = Regex.Match(text, $@"(?m)^{Regex.Escape(key)}\s*=\s*(?<v>-?\d+)");
        return match.Success && int.TryParse(match.Groups["v"].Value, out int value) ? value : fallback;
    }

    private static string[] GetRequirements(string text)
    {
        var match = Regex.Match(text, @"(?m)^Requisitos\s*=\s*PackedStringArray\((?<v>[^\)]*)\)");
        if (!match.Success) return Array.Empty<string>();
        return Regex.Matches(match.Groups["v"].Value, @"""(?<id>[^""]+)""")
            .Select(m => m.Groups["id"].Value)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToArray();
    }

    private static string GetSpecializationKey(string nodeId)
    {
        if (string.IsNullOrWhiteSpace(nodeId))
            return "";

        string[] parts = nodeId.Split('_', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 3)
            return "";

        string key = parts[1];
        return key.Equals("base", StringComparison.OrdinalIgnoreCase)
            ? ""
            : key;
    }
}
