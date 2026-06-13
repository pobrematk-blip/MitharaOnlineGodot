using LiteNetLib;
using LiteNetLib.Utils;
using Mithara.Server.Entities;
using Mithara.Server.Packets;
using Mithara.Server.Quests;
using Mithara.Server.World;

namespace Mithara.Server.Network;

partial class GameServer
{
    private void HandleCreateCharacter(NetPeer peer, NetDataReader reader)
    {
        string name = reader.GetString();
        string className = reader.GetString();
        string race = reader.GetString();

        if (!_sessions.TryGetValue(peer, out var session)) return;

        int charId = _db.CreateCharacter(session.AccountId, name, className, race);
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

        _db.DeleteCharacter(toDelete.Id);

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

        // Use character from DB if available, otherwise use inline data (quick test mode)
        bool useInline = session.SelectedCharacter == null;
        var ch = session.SelectedCharacter;

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
        int maxHp = 80 + forca * 5 + level * 10;
        int maxMana = 30 + inteligencia * 5 + level * 5;

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
        };

        ulong entityId = _world.SpawnPlayerInChannel(channelId, player, peer);
        session.EntityId = entityId;
        session.ChannelId = channelId;

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
            foreach (var item in items)
            {
                if (item.Slot >= 100 && item.Slot <= 116)
                    player.Equipment[item.Slot - 100] = item;
                else
                    player.Items.Add(item);
            }

            RecalculatePlayerStats(player);
        }

        SendInventoryData(peer, player);
        SendGoldUpdate(peer, player.Gold);
        if (!useInline && ch != null)
            SendPetData(peer, ch.Id);

            if (channel != null)
            {
                var writerSpawn = PacketSerializer.WritePacket(PacketId.S2C_SpawnEntity);
                WriteEntityPacket(writerSpawn, player);
                peer.Send(writerSpawn, DeliveryMethod.ReliableOrdered);

                var aoi = channel.GetEntitiesInAoi(player.X, player.Y);
                Logger.Info($"HandleEnterWorld({player.Name}): AOI contem {aoi.Count} entidades (incluindo self)");

                int sentNearby = 0;
                foreach (var eid in aoi)
                {
                    if (eid == entityId) continue;
                    var existing = channel.GetEntity(eid);
                    if (existing == null) continue;
                    var existingWriter = PacketSerializer.WritePacket(PacketId.S2C_SpawnEntity);
                    WriteEntityPacket(existingWriter, existing);
                    peer.Send(existingWriter, DeliveryMethod.ReliableOrdered);
                    sentNearby++;
                }

                // Also send all player entities in the channel (regardless of AOI distance)
                int sentPlayers = 0;
                foreach (var kv in channel.GetAllEntities())
                {
                    if (kv.Value.Type != EntityType.Player) continue;
                    if (kv.Key == entityId) continue;
                    var playerWriter = PacketSerializer.WritePacket(PacketId.S2C_SpawnEntity);
                    WriteEntityPacket(playerWriter, kv.Value);
                    peer.Send(playerWriter, DeliveryMethod.ReliableOrdered);
                    sentPlayers++;
                }
                Logger.Info($"HandleEnterWorld({player.Name}): enviou {sentNearby} spawn(s) de entidades (AOI) + {sentPlayers} player(s) no canal");

                BroadcastSpawnToNearby(channel, player, player.X, player.Y);
            }

        Logger.Info($"{player.Name} entrou no mundo (canal {channelId})");
    }
}
