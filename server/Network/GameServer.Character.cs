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

        Console.WriteLine($"[SERVER] Personagem criado: {name} (id={charId})");
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

    private void HandleEnterWorld(NetPeer peer, NetDataReader reader)
    {
        int channelId = reader.GetInt();

        if (!_sessions.TryGetValue(peer, out var session)) return;
        if (session.SelectedCharacter == null) return;

        var ch = session.SelectedCharacter;
        int baseAttack = ch.Class.ToLowerInvariant() switch
        {
            "guerreiro" => 10,
            "arqueiro" => 7,
            "mago" => 5,
            _ => 6,
        };
        int baseDefense = ch.Class.ToLowerInvariant() switch
        {
            "guerreiro" => 8,
            "arqueiro" => 4,
            "mago" => 2,
            _ => 4,
        };
        int maxHp = 80 + ch.Forca * 5 + ch.Level * 10;
        int maxMana = 30 + ch.Inteligencia * 5 + ch.Level * 5;

        var player = new PlayerEntity
        {
            AccountId = session.AccountId,
            SlotIndex = ch.SlotIndex,
            Name = ch.Name,
            CharacterClass = ch.Class,
            Race = ch.Race,
            Level = ch.Level,
            X = ch.PosX,
            Y = ch.PosY,
            Speed = 185f,
            Health = maxHp,
            MaxHealth = maxHp,
            Mana = maxMana,
            MaxMana = maxMana,
            Forca = ch.Forca,
            Agilidade = ch.Agilidade,
            Destreza = ch.Destreza,
            Inteligencia = ch.Inteligencia,
            Experience = ch.Xp,
            BaseAttack = baseAttack,
            Defense = baseDefense,
            FactionId = GetFactionForRace(ch.Race),
            Gold = ch.Gold,
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

        SendInventoryData(peer, player);
        SendGoldUpdate(peer, player.Gold);

        if (channel != null)
        {
            var writerSpawn = PacketSerializer.WritePacket(PacketId.S2C_SpawnEntity);
            WriteEntityPacket(writerSpawn, player);
            peer.Send(writerSpawn, DeliveryMethod.ReliableOrdered);

            var aoi = channel.GetEntitiesInAoi(player.X, player.Y);
            foreach (var eid in aoi)
            {
                if (eid == entityId) continue;
                var existing = channel.GetEntity(eid);
                if (existing == null) continue;
                var existingWriter = PacketSerializer.WritePacket(PacketId.S2C_SpawnEntity);
                WriteEntityPacket(existingWriter, existing);
                peer.Send(existingWriter, DeliveryMethod.ReliableOrdered);
            }

            BroadcastSpawnToNearby(channel, player, player.X, player.Y);
        }

        Console.WriteLine($"[SERVER] {ch.Name} entrou no mundo (canal {channelId})");
    }
}
