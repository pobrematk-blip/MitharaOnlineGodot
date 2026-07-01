using LiteNetLib;
using LiteNetLib.Utils;
using Mithara.Server.Entities;
using Mithara.Server.Packets;
using Mithara.Server.World;

namespace Mithara.Server.Network;

partial class GameServer
{
    private static ItemDefinition? GetItemDef(int id) => ItemDefinitions.Get(id);
    private const float NpcInteractionRange = 180f;

    private static bool IsNearNpc(Channel channel, Entity player, string prefabId)
    {
        float maxDistanceSq = NpcInteractionRange * NpcInteractionRange;
        return channel.GetAllEntities().Values
            .OfType<NPCEntity>()
            .Any(npc => npc.Health > 0
                && npc.PrefabId == prefabId
                && ((npc.X - player.X) * (npc.X - player.X)
                    + (npc.Y - player.Y) * (npc.Y - player.Y)) <= maxDistanceSq);
    }

    private void HandleNpcInteract(NetPeer peer, NetDataReader reader)
    {
        if (!TryGetPlayer(peer, out var player, out var channel))
        {
            Logger.Info("HandleNpcInteract: TryGetPlayer falhou");
            return;
        }

        ulong npcEntityId = reader.GetULong();
        Logger.Info($"HandleNpcInteract: npcEntityId={npcEntityId} player={player.Name} pos=({player.X},{player.Y})");

        var npcEntity = channel.GetEntity(npcEntityId) as NPCEntity;
        if (npcEntity == null)
        {
            Logger.Info($"Entidade {npcEntityId} n?o encontrada ou n?o ? NPCEntity");
            return;
        }
        if (npcEntity.Health <= 0)
        {
            Logger.Info($"NPC {npcEntity.Name} esta morto");
            return;
        }

        float dx = npcEntity.X - player.X;
        float dy = npcEntity.Y - player.Y;
        float dist = MathF.Sqrt(dx * dx + dy * dy);
        Logger.Info($"NPC pos=({npcEntity.X},{npcEntity.Y}) distancia={dist:F1} max={NpcInteractionRange}");
        if (dist > NpcInteractionRange)
        {
            Logger.Info($"Distancia {dist:F1} > {NpcInteractionRange}, ignorando");
            return;
        }

        var template = _world.Npcs.GetTemplate(npcEntity.PrefabId);
        string dialogId = npcEntity.DialogId;
        if (!string.IsNullOrEmpty(template?.DialogId))
            dialogId = template.DialogId;
        Logger.Info($"Template={npcEntity.PrefabId} dialogId={dialogId}");

        var dialog = _world.Npcs.GetDialog(dialogId);
        if (dialog == null)
        {
            Logger.Info($"Di?logo '{dialogId}' n?o encontrado, enviando mensagem padr?o");
            SendNpcDialog(peer, "O NPC não responde...", new List<(string text, string action, string data)>());
            return;
        }

        Logger.Info($"Di?logo encontrado: \"{dialog.Text}\" ({dialog.Options.Count} opcoes)");
        var options = dialog.Options.Select(o => (o.Text, o.Action, o.ActionData)).ToList();
        string dialogText = dialog.Text;

        if (dialog.Id == "guilda" && player.GuildId >= 0)
        {
            var guild = _world.Guilds.GetGuild(player.GuildId);
            if (guild != null)
            {
                dialogText = $"Bem-vindo de volta, {guild.Name}! Como posso ajudar?";
                options.Clear();
                if (guild.LeaderEntityId == player.Id || guild.GetRank(player.Id) == 0)
                    options.Add(("Desfazer Guilda", "guild_disband", ""));
                options.Add(("Entrar na base da guilda", "guild_enter_base", ""));
                options.Add(("Entrar na GvG", "guild_enter_gvg", ""));
                options.Add(("Sair", "close", ""));
            }
        }

        SendNpcDialog(peer, dialogText, options);
    }

    private void HandleNpcSelectOption(NetPeer peer, NetDataReader reader)
    {
        if (!TryGetPlayer(peer, out var player, out var channel)) return;

        string action = reader.GetString();
        string actionData = reader.GetString();

        switch (action)
        {
            case "goto":
                var dialog = _world.Npcs.GetDialog(actionData);
                if (dialog == null)
                {
                    SendNpcDialog(peer, "...", new List<(string, string, string)>());
                    return;
                }
                var options = dialog.Options.Select(o => (o.Text, o.Action, o.ActionData)).ToList();
                SendNpcDialog(peer, dialog.Text, options);
                break;

            case "shop":
                if (!IsNearNpc(channel, player, "general_merchant"))
                {
                    SendSystemMessage(peer, "Aproxime-se do General Merchante para abrir a loja.");
                    break;
                }

                // Find the NPC first to get shopId
                string shopId = actionData;
                // Try to find shop by the shop data passed as action data
                var shop = _world.Npcs.GetShop(shopId);
                if (shop == null)
                {
                    SendNpcDialog(peer, "A loja está fechada.", new List<(string, string, string)>());
                    return;
                }
                SendNpcShopItems(peer, shopId, shop);
                break;

            case "bank":
                if (!_sessions.TryGetValue(peer, out var session)) return;
                var ch = session.SelectedCharacter;
                if (ch == null) return;
                int bankGold = ch.BankGold;
                SendBankData(peer, player.Gold, bankGold, ch.Id);
                SendNpcDialog(peer, "", new List<(string, string, string)>());
                break;

            case "guild_open_form":
                Logger.Info("[GUILD] guild_open_form: enviando dialog vazio + OpenGuildForm");
                SendNpcDialog(peer, "", new List<(string, string, string)>());
                SendOpenGuildForm(peer);
                Logger.Info("[GUILD] guild_open_form: pacotes enviados!");
                break;

            case "guild_manage":
                Logger.Info("[GUILD] guild_manage: enviando OpenGuildForm");
                SendNpcDialog(peer, "", new List<(string, string, string)>());
                SendOpenGuildForm(peer);
                break;

            case "guild_disband":
                HandleGuildDisband(peer, player);
                break;

            case "guild_enter_base":
                HandleGuildEnterBase(peer, player, channel);
                break;

            case "guild_enter_gvg":
                HandleGuildEnterGvg(peer, player, channel);
                break;

            case "merchant_sell":
                if (!IsNearNpc(channel, player, "general_merchant"))
                {
                    SendSystemMessage(peer, "Aproxime-se do General Merchante para vender itens.");
                    break;
                }

                SendNpcDialog(peer, "", new List<(string, string, string)>());
                SendNpcShopItems(peer, "merchant_sell", new List<ShopEntry>());
                break;

            case "open_refine":
                bool nearRefiner = IsNearNpc(channel, player, "refiner");

                if (!nearRefiner)
                {
                    SendSystemMessage(peer, "Aproxime-se do Refinador para abrir a forja.");
                    break;
                }

                SendNpcDialog(peer, "", new List<(string, string, string)>());
                SendOpenRefine(peer);
                break;

            case "leilao_list":
                if (!IsNearNpc(channel, player, "merchant_auctioneer"))
                {
                    SendSystemMessage(peer, "Aproxime-se do Mercador Leiloeiro para ver os itens.");
                    break;
                }
                SendNpcDialog(peer, "", new List<(string, string, string)>());
                HandleLojinhaListRequest(peer);
                break;

            case "close":
                SendNpcDialog(peer, "", new List<(string, string, string)>());
                break;
        }
    }

    private void HandleNpcBuyItem(NetPeer peer, NetDataReader reader)
    {
        if (!TryGetPlayer(peer, out var player, out var channel)) return;

        string shopId = reader.GetString();
        int itemId = reader.GetInt();
        int quantity = reader.GetInt();

        if (!IsNearNpc(channel, player, "general_merchant"))
        {
            SendNpcBuyResult(peer, false, "Você está longe do General Merchante.");
            return;
        }

        if (quantity <= 0 || quantity > 99)
        {
            SendNpcBuyResult(peer, false, "Quantidade inválida.");
            return;
        }

        var shop = _world.Npcs.GetShop(shopId);
        if (shop == null)
        {
            SendNpcBuyResult(peer, false, "Loja não encontrada.");
            return;
        }

        var entry = shop.FirstOrDefault(e => e.ItemId == itemId);
        if (entry == null)
        {
            SendNpcBuyResult(peer, false, "Item não encontrado na loja.");
            return;
        }

        if (entry.Stock > 0 && entry.Stock < quantity)
        {
            SendNpcBuyResult(peer, false, "Estoque insuficiente.");
            return;
        }

        long totalCostLong = (long)entry.Price * quantity;
        if (totalCostLong <= 0 || totalCostLong > int.MaxValue)
        {
            SendNpcBuyResult(peer, false, "Valor da compra invalido.");
            return;
        }

        int totalCost = (int)totalCostLong;
        if (player.Gold < totalCost)
        {
            SendNpcBuyResult(peer, false, "Gold insuficiente.");
            return;
        }

        if (!_sessions.TryGetValue(peer, out var buySession) || buySession.SelectedCharacter == null)
            return;
        if (!TryAddItemToInventory(player, buySession.SelectedCharacter.Id, itemId, quantity))
        {
            SendNpcBuyResult(peer, false, "Inventário cheio.");
            return;
        }

        player.Gold -= totalCost;
        _db.SaveCharacterGold(buySession.SelectedCharacter.Id, player.Gold);

        if (entry.Stock > 0)
            entry.Stock -= quantity;

        SendNpcBuyResult(peer, true, "Compra realizada!");
        SendInventoryData(peer, player);
        SendGoldUpdate(peer, player.Gold);
    }

    private void HandleNpcSellItem(NetPeer peer, NetDataReader reader)
    {
        if (!TryGetPlayer(peer, out var player, out var channel)) return;

        int slot = reader.GetInt();
        int quantity = reader.GetInt();

        if (!IsNearNpc(channel, player, "general_merchant"))
        {
            SendNpcSellResult(peer, false, "Você está longe do General Merchante.");
            return;
        }

        var item = player.Items.FirstOrDefault(i => i.Slot == slot);
        if (item == null)
        {
            SendNpcSellResult(peer, false, "Item não encontrado.");
            return;
        }

        if (player.Equipment.Values.Any(equipped =>
                ReferenceEquals(equipped, item)
                || (item.DbId > 0 && equipped.DbId == item.DbId)))
        {
            SendNpcSellResult(peer, false, "Desequipe o item antes de vendê-lo.");
            return;
        }

        if (quantity <= 0 || quantity > item.Quantity)
        {
            SendNpcSellResult(peer, false, "Quantidade inválida.");
            return;
        }

        var itemDef = GetItemDef(item.ItemId);
        if (itemDef == null)
        {
            SendNpcSellResult(peer, false, "Item desconhecido.");
            return;
        }

        if (itemDef.BuyPrice <= 0)
        {
            SendNpcSellResult(peer, false, "Este item não pode ser vendido.");
            return;
        }

        int sellPrice = itemDef.BuyPrice / 4;
        if (sellPrice < 1) sellPrice = 1;

        long totalGoldLong = (long)sellPrice * quantity;
        if (totalGoldLong <= 0 || totalGoldLong > int.MaxValue)
        {
            SendNpcSellResult(peer, false, "Valor da venda invalido.");
            return;
        }

        int totalGold = (int)totalGoldLong;
        player.Gold += totalGold;
        if (_sessions.TryGetValue(peer, out var sellSession) && sellSession.SelectedCharacter != null)
        {
            _db.SaveCharacterGold(sellSession.SelectedCharacter.Id, player.Gold);
            if (quantity >= item.Quantity)
            {
                player.Items.Remove(item);
                _db.DeleteItem(sellSession.SelectedCharacter.Id, item.DbId);
            }
            else
            {
                item.Quantity -= quantity;
                _db.SaveItem(sellSession.SelectedCharacter.Id, item);
            }
        }
        else
        {
            if (quantity >= item.Quantity)
                player.Items.Remove(item);
            else
                item.Quantity -= quantity;
        }

        SendNpcSellResult(peer, true, $"Vendido por {totalGold} gold!");
        SendInventoryData(peer, player);
        SendGoldUpdate(peer, player.Gold);
    }

    private void SendNpcDialog(NetPeer peer, string text, List<(string text, string action, string data)> options)
    {
        var writer = PacketSerializer.WritePacket(PacketId.S2C_NpcDialog);
        writer.Put(text);
        writer.Put(options.Count);
        foreach (var opt in options)
        {
            writer.Put(opt.text);
            writer.Put(opt.action);
            writer.Put(opt.data);
        }
        peer.Send(writer, DeliveryMethod.ReliableOrdered);
    }

    private void SendNpcShopItems(NetPeer peer, string shopId, List<ShopEntry> items)
    {
        var writer = PacketSerializer.WritePacket(PacketId.S2C_NpcShopItems);
        writer.Put(shopId);
        writer.Put(items.Count);
        foreach (var entry in items)
        {
            writer.Put(entry.ItemId);
            writer.Put(entry.Price);
            writer.Put(entry.Stock);
        }
        peer.Send(writer, DeliveryMethod.ReliableOrdered);
    }

    private void SendNpcBuyResult(NetPeer peer, bool success, string message)
    {
        var writer = PacketSerializer.WritePacket(PacketId.S2C_NpcBuyResult);
        writer.Put(success);
        writer.Put(message);
        peer.Send(writer, DeliveryMethod.ReliableOrdered);
    }

    private void SendNpcSellResult(NetPeer peer, bool success, string message)
    {
        var writer = PacketSerializer.WritePacket(PacketId.S2C_NpcSellResult);
        writer.Put(success);
        writer.Put(message);
        peer.Send(writer, DeliveryMethod.ReliableOrdered);
    }

    private void SendOpenGuildForm(NetPeer peer)
    {
        var writer = PacketSerializer.WritePacket(PacketId.S2C_OpenGuildForm);
        peer.Send(writer, DeliveryMethod.ReliableOrdered);
    }

    private void SendOpenRefine(NetPeer peer)
    {
        var writer = PacketSerializer.WritePacket(PacketId.S2C_OpenRefine);
        peer.Send(writer, DeliveryMethod.ReliableOrdered);
    }
}
