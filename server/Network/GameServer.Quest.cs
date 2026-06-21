using System.Linq;
using System.Text.Json;
using LiteNetLib;
using LiteNetLib.Utils;
using Mithara.Server.Entities;
using Mithara.Server.Packets;
using Mithara.Server.Quests;

namespace Mithara.Server.Network;

partial class GameServer
{
    private void HandleQuestList(NetPeer peer, NetDataReader reader)
    {
        if (!_sessions.TryGetValue(peer, out var session)) return;
        var player = _world.GetChannel(session.ChannelId)?.GetEntity(session.EntityId) as PlayerEntity;
        if (player == null) return;

        SendQuestList(peer, player);
    }

    private void HandleQuestClaimReward(NetPeer peer, NetDataReader reader)
    {
        if (!_sessions.TryGetValue(peer, out var session)) return;
        var player = _world.GetChannel(session.ChannelId)?.GetEntity(session.EntityId) as PlayerEntity;
        if (player == null) return;

        int questId = reader.GetInt();
        if (!player.Quests.TryGetValue(questId, out var pq)) return;
        if (!pq.Completed || pq.Claimed) return;

        var def = _questManager.GetDefinition(questId);
        if (def == null) return;

        pq.Claimed = true;

        // Award XP
        long questXp = def.Reward.Experience;
        if (IsPlayerVip(player))
            questXp *= 2;
        player.Experience += questXp;

        // Award gold
        player.Gold += def.Reward.Gold;

        // Award items
        int characterId = session.SelectedCharacter!.Id;
        foreach (var (itemId, quantity) in def.Reward.Items)
        {
            if (!TryAddItemToInventory(player, characterId, itemId, quantity))
            {
                SendSystemMessage(peer, "Inventario cheio para receber recompensa.");
                return;
            }
        }

        _db.UpsertPlayerQuest(characterId, questId,
            JsonSerializer.Serialize(pq.Progress), pq.Completed, pq.Claimed);

        _db.SaveCharacterXp(characterId, player.Experience);
        _db.SaveCharacterGold(characterId, player.Gold);
        session.SelectedCharacter.Xp = player.Experience;
        session.SelectedCharacter.Gold = player.Gold;

        SendInventoryData(peer, player);
        SendGoldUpdate(peer, player.Gold);

        SendQuestRewardClaimed(peer, questId, true);
    }

    public void SendQuestList(NetPeer peer, PlayerEntity player)
    {
        var writer = new NetDataWriter();
        writer.Put((ushort)PacketId.S2C_QuestList);

        var available = _questManager.GetQuestsForLevel(player.Level);
        writer.Put(available.Count);
        foreach (var q in available)
        {
            writer.Put(q.Id);
            writer.Put(q.Name);
            writer.Put(q.Description);
            writer.Put(q.RequiredLevel);
            writer.Put(q.Objectives.Count);
            foreach (var obj in q.Objectives)
            {
                writer.Put((byte)obj.Type);
                writer.Put(obj.TargetId);
                writer.Put(obj.RequiredCount);
            }

            bool hasProgress = player.Quests.TryGetValue(q.Id, out var pq);
            writer.Put(hasProgress);
            if (hasProgress)
            {
                writer.Put(pq!.Progress.Count);
                foreach (var p in pq!.Progress)
                    writer.Put(p);
                writer.Put(pq.Completed);
                writer.Put(pq.Claimed);
            }
        }

        writer.Put((byte)0); // Count of quest objectives to highlight (reserved)
        peer.Send(writer, DeliveryMethod.ReliableOrdered);
    }

    public void SendQuestProgressUpdate(NetPeer peer, int questId, List<int> progress, bool completed)
    {
        var writer = new NetDataWriter();
        writer.Put((ushort)PacketId.S2C_QuestProgress);
        writer.Put(questId);
        writer.Put(progress.Count);
        foreach (var p in progress)
            writer.Put(p);
        writer.Put(completed);
        peer.Send(writer, DeliveryMethod.ReliableOrdered);
    }

    public void SendQuestCompleted(NetPeer peer, int questId)
    {
        var writer = new NetDataWriter();
        writer.Put((ushort)PacketId.S2C_QuestCompleted);
        writer.Put(questId);
        peer.Send(writer, DeliveryMethod.ReliableOrdered);
    }

    public void SendQuestRewardClaimed(NetPeer peer, int questId, bool success)
    {
        var writer = new NetDataWriter();
        writer.Put((ushort)PacketId.S2C_QuestRewardClaimed);
        writer.Put(questId);
        writer.Put(success);
        peer.Send(writer, DeliveryMethod.ReliableOrdered);
    }

    public void UpdateQuestKillProgress(PlayerEntity player, string monsterPrefabId)
    {
        var session = _sessions.Values.FirstOrDefault(s => s.EntityId == player.Id);
        if (session?.SelectedCharacter == null) return;
        int characterId = session.SelectedCharacter.Id;

        foreach (var kv in player.Quests.ToList())
        {
            var pq = kv.Value;
            if (pq.Completed || pq.Claimed) continue;

            var def = _questManager.GetDefinition(kv.Key);
            if (def == null) continue;

            bool changed = false;
            for (int i = 0; i < def.Objectives.Count; i++)
            {
                var obj = def.Objectives[i];
                if (obj.Type == QuestObjectiveType.Kill && obj.TargetId == monsterPrefabId)
                {
                    if (i >= pq.Progress.Count)
                        pq.Progress.Add(0);
                    if (pq.Progress[i] < obj.RequiredCount)
                    {
                        pq.Progress[i]++;
                        changed = true;
                    }
                }
            }

            if (!changed) continue;

            bool completed = _questManager.CheckCompletion(def, pq);
            if (completed && !pq.Completed)
            {
                pq.Completed = true;
                _db.UpsertPlayerQuest(characterId, kv.Key,
                    System.Text.Json.JsonSerializer.Serialize(pq.Progress), true, false);

                if (_sessions.TryGetValue(session.Peer, out _))
                {
                    SendQuestCompleted(session.Peer, kv.Key);
                    SendQuestProgressUpdate(session.Peer, kv.Key, pq.Progress, true);
                }
            }
            else
            {
                _db.UpsertPlayerQuest(characterId, kv.Key,
                    System.Text.Json.JsonSerializer.Serialize(pq.Progress), false, false);

                if (_sessions.TryGetValue(session.Peer, out _))
                {
                    SendQuestProgressUpdate(session.Peer, kv.Key, pq.Progress, false);
                }
            }
        }
    }
}
