using LiteNetLib;
using LiteNetLib.Utils;
using Mithara.Server.Entities;
using Mithara.Server.Packets;
using System.Text.Json;

namespace Mithara.Server.Network;

partial class GameServer
{
    private void HandleTradeRequestPacket(NetPeer peer, NetDataReader reader)
    {
        string targetName = reader.GetString();
        if (!TryGetPlayer(peer, out var sender, out _) || sender == null) return;
        HandleTradeRequest(peer, sender, targetName);
    }

    private void HandleTradeRequest(NetPeer peer, PlayerEntity sender, string targetName)
    {
        var target = FindPlayerByName(targetName, out var targetPeer, out _);
        if (target == null)
        {
            SendSystemMessage(peer, $"Jogador '{targetName}' não encontrado.");
            return;
        }
        if (target.Id == sender.Id)
        {
            SendSystemMessage(peer, "Você não pode trocar consigo mesmo.");
            return;
        }
        if (_activeTrades.Values.Any(t => t.PlayerA == sender.Id || t.PlayerB == sender.Id))
        {
            SendSystemMessage(peer, "Você já está em uma troca.");
            return;
        }
        if (_activeTrades.Values.Any(t => t.PlayerA == target.Id || t.PlayerB == target.Id))
        {
            SendSystemMessage(peer, "Este jogador já está em uma troca.");
            return;
        }

        _tradeInvites[target.Id] = sender.Id;
        SendSystemMessage(peer, $"Solicitação de troca enviada para {target.Name}.");

        var notify = PacketSerializer.WritePacket(PacketId.S2C_TradeRequested);
        notify.Put(sender.Name);
        targetPeer!.Send(notify, DeliveryMethod.ReliableOrdered);
    }

    private void HandleTradeAcceptPacket(NetPeer peer, NetDataReader reader)
    {
        if (!TryGetPlayer(peer, out var player, out _) || player == null) return;
        if (!_tradeInvites.TryGetValue(player.Id, out var inviterId))
        {
            SendSystemMessage(peer, "Você não tem convite de troca pendente.");
            return;
        }
        _tradeInvites.Remove(player.Id);

        var inviter = FindEntityById(inviterId, out var inviterPeer, out _);
        if (inviter == null || inviterPeer == null)
        {
            SendSystemMessage(peer, "O convite expirou (jogador offline).");
            return;
        }

        var session = new TradeSession
        {
            Id = _nextTradeId++,
            PlayerA = inviterId,
            PlayerB = player.Id,
        };
        _activeTrades[inviterId] = session;
        _activeTrades[player.Id] = session;

        SendTradeStart(inviterPeer, player.Id, player.Name, inviter.Name);
        SendTradeStart(peer, inviter.Id, inviter.Name, player.Name);

        SendSystemMessage(inviterPeer, $"{player.Name} aceitou a troca!");
        SendSystemMessage(peer, $"Troca com {inviter.Name} iniciada!");
    }

    private void SendTradeStart(NetPeer peer, ulong partnerId, string partnerName, string receiverName)
    {
        var w = PacketSerializer.WritePacket(PacketId.S2C_TradeStart);
        w.Put(partnerId);
        w.Put(partnerName);
        peer.Send(w, DeliveryMethod.ReliableOrdered);
        Logger.Info($"TradeStart enviado para {receiverName}: parceiro={partnerName} ({partnerId}).");
    }

    private void HandleTradeDeclinePacket(NetPeer peer, NetDataReader reader)
    {
        if (!TryGetPlayer(peer, out var player, out _) || player == null) return;
        if (!_tradeInvites.TryGetValue(player.Id, out var inviterId)) return;
        _tradeInvites.Remove(player.Id);

        var inviterPeer = FindPeerByEntityId(inviterId);
        if (inviterPeer != null)
            SendSystemMessage(inviterPeer, $"{player.Name} recusou a troca.");
    }

    private void HandleTradeUpdateOfferPacket(NetPeer peer, NetDataReader reader)
    {
        if (!_sessions.TryGetValue(peer, out var session)) return;
        var channel = _world.GetChannel(session.ChannelId);
        if (channel?.GetEntity(session.EntityId) is not PlayerEntity player) return;

        if (!_activeTrades.TryGetValue(player.Id, out var trade)) return;

        int inventorySlot = reader.GetInt();
        int quantity = reader.GetInt();

        var item = player.Items.FirstOrDefault(i => i.Slot == inventorySlot);
        if (item == null || item.Quantity < quantity || quantity <= 0)
        {
            SendSystemMessage(peer, "Item inválido ou quantidade insuficiente.");
            return;
        }

        var offers = player.Id == trade.PlayerA ? trade.PlayerAOffers : trade.PlayerBOffers;
        if (offers.Count >= 9)
        {
            SendSystemMessage(peer, "Máximo de 9 itens na troca.");
            return;
        }
        if (offers.Any(o => o.InventorySlot == inventorySlot))
        {
            SendSystemMessage(peer, "Este item já está na oferta de troca.");
            return;
        }

        int tradeSlot = 0;
        while (offers.Any(o => o.TradeSlot == tradeSlot)) tradeSlot++;

        offers.Add(new TradeOfferItem
        {
            TradeSlot = tradeSlot,
            InventorySlot = inventorySlot,
            ItemId = item.ItemId,
            Quantity = quantity,
            DbId = item.DbId,
        });

        if (player.Id == trade.PlayerA)
            trade.PlayerAConfirmed = false;
        else
            trade.PlayerBConfirmed = false;

        BroadcastTradeOfferUpdate(trade, player.Id);
        NotifyTradeConfirmationReset(trade, player.Id);
    }

    private void HandleTradeRemoveOfferPacket(NetPeer peer, NetDataReader reader)
    {
        if (!_sessions.TryGetValue(peer, out var session)) return;
        if (!_activeTrades.TryGetValue(session.EntityId, out var trade)) return;

        int tradeSlot = reader.GetInt();
        var offers = session.EntityId == trade.PlayerA ? trade.PlayerAOffers : trade.PlayerBOffers;
        offers.RemoveAll(o => o.TradeSlot == tradeSlot);

        if (session.EntityId == trade.PlayerA)
            trade.PlayerAConfirmed = false;
        else
            trade.PlayerBConfirmed = false;

        BroadcastTradeOfferUpdate(trade, session.EntityId);
        NotifyTradeConfirmationReset(trade, session.EntityId);
    }

    private void HandleTradeUpdateGoldPacket(NetPeer peer, NetDataReader reader)
    {
        if (!_sessions.TryGetValue(peer, out var session)) return;
        var channel = _world.GetChannel(session.ChannelId);
        if (channel?.GetEntity(session.EntityId) is not PlayerEntity player) return;
        if (!_activeTrades.TryGetValue(player.Id, out var trade)) return;

        int gold = Math.Max(0, reader.GetInt());
        if (gold > player.Gold)
        {
            SendSystemMessage(peer, "Ouro insuficiente para oferecer na troca.");
            gold = player.Gold;
        }

        if (player.Id == trade.PlayerA)
        {
            trade.PlayerAGoldOffer = gold;
            trade.PlayerAConfirmed = false;
        }
        else
        {
            trade.PlayerBGoldOffer = gold;
            trade.PlayerBConfirmed = false;
        }

        BroadcastTradeOfferUpdate(trade, player.Id);
        NotifyTradeConfirmationReset(trade, player.Id);
    }

    private void HandleTradeConfirmPacket(NetPeer peer, NetDataReader reader)
    {
        if (!_sessions.TryGetValue(peer, out var session)) return;
        if (!_activeTrades.TryGetValue(session.EntityId, out var trade)) return;

        if (session.EntityId == trade.PlayerA)
            trade.PlayerAConfirmed = true;
        else
            trade.PlayerBConfirmed = true;

        var otherSide = session.EntityId == trade.PlayerA ? trade.PlayerB : trade.PlayerA;
        var otherPeer = FindPeerByEntityId(otherSide);
        if (otherPeer != null)
        {
            var w = PacketSerializer.WritePacket(PacketId.S2C_TradePartnerConfirm);
            w.Put(session.EntityId);
            w.Put(true);
            otherPeer.Send(w, DeliveryMethod.ReliableOrdered);
        }

        if (trade.PlayerAConfirmed && trade.PlayerBConfirmed)
            ExecuteTrade(trade);
    }

    private void ExecuteTrade(TradeSession trade)
    {
        var playerA = FindEntityById(trade.PlayerA, out var peerA, out _) as PlayerEntity;
        var playerB = FindEntityById(trade.PlayerB, out var peerB, out _) as PlayerEntity;

        if (playerA == null || playerB == null || peerA == null || peerB == null)
        {
            EndTradeSession(trade, false);
            return;
        }

        if (!_sessions.TryGetValue(peerA, out var sessionA) || !_sessions.TryGetValue(peerB, out var sessionB))
        {
            EndTradeSession(trade, false);
            return;
        }

        int charIdA = sessionA.SelectedCharacter?.Id ?? 0;
        int charIdB = sessionB.SelectedCharacter?.Id ?? 0;
        if (charIdA == 0 || charIdB == 0)
        {
            EndTradeSession(trade, false);
            return;
        }

        foreach (var offer in trade.PlayerAOffers)
        {
            var src = playerA.Items.FirstOrDefault(i => i.Slot == offer.InventorySlot && i.ItemId == offer.ItemId);
            if (src == null || src.Quantity < offer.Quantity)
            { EndTradeSession(trade, false); return; }
        }
        foreach (var offer in trade.PlayerBOffers)
        {
            var src = playerB.Items.FirstOrDefault(i => i.Slot == offer.InventorySlot && i.ItemId == offer.ItemId);
            if (src == null || src.Quantity < offer.Quantity)
            { EndTradeSession(trade, false); return; }
        }

        if (trade.PlayerAGoldOffer < 0 || trade.PlayerBGoldOffer < 0 ||
            playerA.Gold < trade.PlayerAGoldOffer || playerB.Gold < trade.PlayerBGoldOffer)
        {
            SendSystemMessage(peerA, "Ouro insuficiente para concluir a troca.");
            SendSystemMessage(peerB, "Ouro insuficiente para concluir a troca.");
            EndTradeSession(trade, false);
            return;
        }

        if (!TemEspacoParaReceber(playerA, trade.PlayerBOffers, trade.PlayerAOffers))
        {
            SendSystemMessage(peerA, "Inventario sem espaco para receber os itens da troca.");
            SendSystemMessage(peerB, $"{playerA.Name} nao tem espaco no inventario.");
            EndTradeSession(trade, false);
            return;
        }

        if (!TemEspacoParaReceber(playerB, trade.PlayerAOffers, trade.PlayerBOffers))
        {
            SendSystemMessage(peerB, "Inventario sem espaco para receber os itens da troca.");
            SendSystemMessage(peerA, $"{playerB.Name} nao tem espaco no inventario.");
            EndTradeSession(trade, false);
            return;
        }

        foreach (var offer in trade.PlayerAOffers)
        {
            var src = playerA.Items.FirstOrDefault(i => i.Slot == offer.InventorySlot && i.ItemId == offer.ItemId);
            if (src == null) continue;
            var recebido = ClonarItemParaTroca(src, offer.Quantity);
            src.Quantity -= offer.Quantity;
            if (src.Quantity <= 0) { playerA.Items.Remove(src); _db.DeleteItem(charIdA, src.DbId); }
            else _db.SaveItem(charIdA, src);

            AdicionarItemRecebido(playerB, charIdB, recebido);
        }

        foreach (var offer in trade.PlayerBOffers)
        {
            var src = playerB.Items.FirstOrDefault(i => i.Slot == offer.InventorySlot && i.ItemId == offer.ItemId);
            if (src == null) continue;
            var recebido = ClonarItemParaTroca(src, offer.Quantity);
            src.Quantity -= offer.Quantity;
            if (src.Quantity <= 0) { playerB.Items.Remove(src); _db.DeleteItem(charIdB, src.DbId); }
            else _db.SaveItem(charIdB, src);

            AdicionarItemRecebido(playerA, charIdA, recebido);
        }

        if (trade.PlayerAGoldOffer > 0 || trade.PlayerBGoldOffer > 0)
        {
            playerA.Gold = playerA.Gold - trade.PlayerAGoldOffer + trade.PlayerBGoldOffer;
            playerB.Gold = playerB.Gold - trade.PlayerBGoldOffer + trade.PlayerAGoldOffer;
            _db.SaveCharacterGold(charIdA, playerA.Gold);
            _db.SaveCharacterGold(charIdB, playerB.Gold);
        }

        SendInventoryData(peerA, playerA);
        SendInventoryData(peerB, playerB);
        SendGoldUpdate(peerA, playerA.Gold);
        SendGoldUpdate(peerB, playerB.Gold);
        SendSystemMessage(peerA, "Troca realizada com sucesso!");
        SendSystemMessage(peerB, "Troca realizada com sucesso!");
        EndTradeSession(trade, true);
    }

    private void HandleTradeCancelPacket(NetPeer peer, NetDataReader reader)
    {
        if (!_sessions.TryGetValue(peer, out var session)) return;
        if (!_activeTrades.TryGetValue(session.EntityId, out var trade)) return;
        var otherSide = session.EntityId == trade.PlayerA ? trade.PlayerB : trade.PlayerA;
        var otherPeer = FindPeerByEntityId(otherSide);
        if (otherPeer != null)
            SendSystemMessage(otherPeer, "O outro jogador cancelou a troca.");
        EndTradeSession(trade, false);
    }

    private void BroadcastTradeOfferUpdate(TradeSession trade, ulong changedPlayerId)
    {
        var offers = changedPlayerId == trade.PlayerA ? trade.PlayerAOffers : trade.PlayerBOffers;
        int goldOffer = changedPlayerId == trade.PlayerA ? trade.PlayerAGoldOffer : trade.PlayerBGoldOffer;

        foreach (var eid in new[] { trade.PlayerA, trade.PlayerB })
        {
            var peer = FindPeerByEntityId(eid);
            if (peer == null) continue;
            var w = PacketSerializer.WritePacket(PacketId.S2C_TradeOfferUpdate);
            w.Put(changedPlayerId);
            w.Put((byte)offers.Count);
            foreach (var o in offers)
            {
                w.Put((byte)o.TradeSlot);
                w.Put(o.ItemId);
                w.Put(o.Quantity);
            }
            w.Put(goldOffer);
            peer.Send(w, DeliveryMethod.ReliableOrdered);
        }
    }

    private void NotifyTradeConfirmationReset(TradeSession trade, ulong changedPlayerId)
    {
        var otherSide = changedPlayerId == trade.PlayerA ? trade.PlayerB : trade.PlayerA;
        var otherPeer = FindPeerByEntityId(otherSide);
        if (otherPeer == null)
            return;

        var wc = PacketSerializer.WritePacket(PacketId.S2C_TradePartnerConfirm);
        wc.Put(changedPlayerId);
        wc.Put(false);
        otherPeer.Send(wc, DeliveryMethod.ReliableOrdered);
    }

    private void EndTradeSession(TradeSession trade, bool success)
    {
        _activeTrades.Remove(trade.PlayerA);
        _activeTrades.Remove(trade.PlayerB);

        foreach (var eid in new[] { trade.PlayerA, trade.PlayerB })
        {
            var peer = FindPeerByEntityId(eid);
            if (peer == null) continue;
            var w = PacketSerializer.WritePacket(PacketId.S2C_TradeEnd);
            w.Put(success);
            peer.Send(w, DeliveryMethod.ReliableOrdered);
        }
    }

    private static bool IsStackable(int itemId)
    {
        var def = ItemDefinitions.Get(itemId);
        return def != null && def.Type == ItemType.Consumable;
    }

    private bool TemEspacoParaReceber(PlayerEntity destino, List<TradeOfferItem> incoming, List<TradeOfferItem> outgoing)
    {
        var slotsOcupados = destino.Items
            .Where(item => item.Slot >= 0 && item.Slot < GetInventorySlotLimit(destino))
            .Where(item => !outgoing.Any(offer =>
                offer.InventorySlot == item.Slot &&
                offer.ItemId == item.ItemId &&
                item.Quantity <= offer.Quantity))
            .Select(item => item.Slot)
            .ToHashSet();

        int slotsLivres = GetInventorySlotLimit(destino) - slotsOcupados.Count;
        int slotsNecessarios = 0;

        foreach (var offer in incoming)
        {
            var def = ItemDefinitions.Get(offer.ItemId);
            if (def == null)
                return false;

            bool stackable = IsStackable(offer.ItemId);
            if (!stackable)
            {
                slotsNecessarios += Math.Max(1, offer.Quantity);
                continue;
            }

            int maxStack = Math.Max(1, def.MaxStack);
            int capacidadeExistente = destino.Items
                .Where(item => item.ItemId == offer.ItemId && slotsOcupados.Contains(item.Slot))
                .Sum(item => Math.Max(0, maxStack - item.Quantity));
            int restante = Math.Max(0, offer.Quantity - capacidadeExistente);
            slotsNecessarios += (int)Math.Ceiling(restante / (double)maxStack);
        }

        return slotsLivres >= slotsNecessarios;
    }

    private void AdicionarItemRecebido(PlayerEntity destino, int characterId, ItemInstance item)
    {
        bool stackable = IsStackable(item.ItemId);
        if (stackable)
        {
            var def = ItemDefinitions.Get(item.ItemId);
            int maxStack = Math.Max(1, def?.MaxStack ?? item.Quantity);
            var dest = destino.Items.FirstOrDefault(i => i.ItemId == item.ItemId && i.Quantity < maxStack);
            if (dest != null)
            {
                int add = Math.Min(item.Quantity, maxStack - dest.Quantity);
                dest.Quantity += add;
                item.Quantity -= add;
                _db.SaveItem(characterId, dest);
            }
        }

        while (item.Quantity > 0)
        {
            int qtd = stackable ? Math.Min(item.Quantity, Math.Max(1, ItemDefinitions.Get(item.ItemId)?.MaxStack ?? item.Quantity)) : item.Quantity;
            int slot = destino.FindEmptyInventorySlot();
            if (slot < 0)
                throw new InvalidOperationException("Inventario sem espaco apos validacao de trade.");

            var novo = ClonarItemParaTroca(item, qtd);
            novo.Slot = slot;
            destino.Items.Add(novo);
            _db.SaveItem(characterId, novo);
            item.Quantity -= qtd;

            if (!stackable)
                break;
        }
    }

    private static ItemInstance ClonarItemParaTroca(ItemInstance origem, int quantidade)
    {
        return new ItemInstance
        {
            ItemId = origem.ItemId,
            Quantity = quantidade,
            RefineLevel = origem.RefineLevel,
            Roll = JsonSerializer.Deserialize<ItemRoll>(JsonSerializer.Serialize(origem.Roll)) ?? new ItemRoll(),
        };
    }
}
