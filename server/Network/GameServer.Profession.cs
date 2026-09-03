using LiteNetLib;
using LiteNetLib.Utils;
using Mithara.Server.Entities;
using Mithara.Server.Packets;

namespace Mithara.Server.Network;

partial class GameServer
{
    private void HandleLearnRecipe(NetPeer peer, NetDataReader reader)
    {
        if (!_sessions.TryGetValue(peer, out var session) || session.SelectedCharacter == null)
            return;

        var channel = _world.GetChannel(session.ChannelId);
        if (channel?.GetEntity(session.EntityId) is not PlayerEntity player || player.Health <= 0)
            return;

        int slot = reader.GetInt();
        var item = player.Items.FirstOrDefault(c => c.Slot == slot);
        if (item == null)
            return;

        int recipeItemId = item.ItemId;
        var recipe = AlchemistRecipeRegistry.Get(recipeItemId);
        if (recipe == null)
        {
            SendLearnRecipeResult(peer, false, recipeItemId, "Item não é uma receita válida.");
            return;
        }

        if (player.KnownAlchemistRecipes.Contains(recipeItemId))
        {
            SendLearnRecipeResult(peer, false, recipeItemId, "Você já conhece esta receita.");
            return;
        }

        player.KnownAlchemistRecipes.Add(recipeItemId);
        _db.SaveAlchemistRecipe(session.SelectedCharacter.Id, recipeItemId);

        item.Quantity--;
        if (item.Quantity <= 0)
        {
            player.Items.Remove(item);
            _db.DeleteItem(session.SelectedCharacter.Id, item.DbId);
            ClearConsumableShortcutIfDepleted(peer, player, session.SelectedCharacter.Id, item.ItemId);
        }
        else
        {
            _db.SaveItem(session.SelectedCharacter.Id, item);
        }

        SendInventoryData(peer, player);
        SendLearnRecipeResult(peer, true, recipeItemId, $"Receita aprendida: {recipe.Name}");
        Logger.PlayerAction(player.Name, "APRENDEU_RECEITA", recipe.Name);
    }

    private void HandleCraftAlchemist(NetPeer peer, NetDataReader reader)
    {
        if (!_sessions.TryGetValue(peer, out var session) || session.SelectedCharacter == null)
            return;

        var channel = _world.GetChannel(session.ChannelId);
        if (channel?.GetEntity(session.EntityId) is not PlayerEntity player || player.Health <= 0)
            return;

        int recipeId = reader.GetInt();
        var recipe = AlchemistRecipeRegistry.Get(recipeId);
        if (recipe == null)
        {
            SendCraftResult(peer, false, 0, 0, "Receita não encontrada.");
            return;
        }

        if (!player.KnownAlchemistRecipes.Contains(recipeId))
        {
            SendCraftResult(peer, false, recipeId, 0, "Você não conhece esta receita.");
            return;
        }

        var prof = player.Professions.GetValueOrDefault(ProfessionType.Alquimista);
        if (prof == null)
        {
            prof = new ProfessionData { Type = ProfessionType.Alquimista };
            player.Professions[ProfessionType.Alquimista] = prof;
        }

        if (prof.Level < recipe.RequiredLevel)
        {
            SendCraftResult(peer, false, recipeId, 0, $"Nível insuficiente. Necessário: Alquimista NV.{recipe.RequiredLevel}.");
            return;
        }

        if (player.Gold < recipe.GoldCost)
        {
            SendCraftResult(peer, false, recipeId, 0, $"Ouro insuficiente. Necessário: {recipe.GoldCost} gold.");
            return;
        }

        foreach (var ing in recipe.Ingredients)
        {
            int total = player.Items.Where(i => i.ItemId == ing.ItemId).Sum(i => i.Quantity);
            if (total < ing.Quantity)
            {
                var matDef = ItemDefinitions.Get(ing.ItemId);
                string matName = matDef?.Name ?? $"Item#{ing.ItemId}";
                SendCraftResult(peer, false, recipeId, 0, $"Material insuficiente: {matName} x{ing.Quantity}.");
                return;
            }
        }

        foreach (var ing in recipe.Ingredients)
        {
            int remaining = ing.Quantity;
            var slots = player.Items.Where(i => i.ItemId == ing.ItemId).OrderBy(i => i.Quantity).ToList();
            foreach (var slotItem in slots)
            {
                if (remaining <= 0) break;
                int take = Math.Min(remaining, slotItem.Quantity);
                slotItem.Quantity -= take;
                remaining -= take;
                if (slotItem.Quantity <= 0)
                {
                    player.Items.Remove(slotItem);
                    _db.DeleteItem(session.SelectedCharacter.Id, slotItem.DbId);
                }
                else
                {
                    _db.SaveItem(session.SelectedCharacter.Id, slotItem);
                }
            }
        }

        player.Gold -= recipe.GoldCost;

        int quantity = recipe.ProducesQuantity;
        bool isCrit = CalculateAlchemistCrit(prof.Level);
        if (isCrit)
            quantity += recipe.CritBonusQuantity;

        int producedId = recipe.ProducesItemId;
        for (int i = 0; i < quantity; i++)
        {
            var producedDef = ItemDefinitions.Get(producedId);
            if (producedDef == null) continue;

            var existingStack = player.Items.FirstOrDefault(x => x.ItemId == producedId && x.Quantity < producedDef.MaxStack);
            if (existingStack != null)
            {
                existingStack.Quantity++;
                _db.SaveItem(session.SelectedCharacter.Id, existingStack);
            }
            else
            {
                int newSlot = 0;
                while (player.Items.Any(x => x.Slot == newSlot) || player.Equipment.ContainsKey(newSlot))
                    newSlot++;

                var newItem = new ItemInstance
                {
                    ItemId = producedId,
                    Quantity = 1,
                    Slot = newSlot,
                };
                player.Items.Add(newItem);
                _db.SaveItem(session.SelectedCharacter.Id, newItem);
            }
        }

        bool leveledUp = prof.AddXp(recipe.XpReward);
        _db.SaveCharacterProfession(session.SelectedCharacter.Id, prof);
        _db.SaveCharacterGold(session.SelectedCharacter.Id, player.Gold);

        SendInventoryData(peer, player);
        SendGoldUpdate(peer, player.Gold);

        string critMsg = isCrit ? " (CRÍTICO!)" : "";
        string typeStr = recipe.Type == AlchemistRecipeType.Potion ? "Poção" : "Runa";
        SendCraftResult(peer, true, recipeId, producedId,
            $"{typeStr} {recipe.Name} criada x{quantity}{critMsg}. +{recipe.XpReward} XP Alquimista.");

        if (leveledUp)
        {
            SendProfessionLevelUp(peer, ProfessionType.Alquimista, prof.Level);
            SendSystemMessage(peer, $"Alquimista subiu para NV.{prof.Level}!");
        }

        Logger.PlayerAction(player.Name, "CRAFT_ALQUIMISTA",
            $"{recipe.Name} x{quantity}{critMsg} (xp={recipe.XpReward}, gold={recipe.GoldCost})");
    }

    private void HandleProfessionInfo(NetPeer peer, NetDataReader reader)
    {
        if (!_sessions.TryGetValue(peer, out var session) || session.SelectedCharacter == null)
            return;

        var channel = _world.GetChannel(session.ChannelId);
        if (channel?.GetEntity(session.EntityId) is not PlayerEntity player)
            return;

        SendProfessionInfo(peer, player);
    }

    private void HandleBuyRecipe(NetPeer peer, NetDataReader reader)
    {
        if (!_sessions.TryGetValue(peer, out var session) || session.SelectedCharacter == null)
            return;

        var channel = _world.GetChannel(session.ChannelId);
        if (channel?.GetEntity(session.EntityId) is not PlayerEntity player || player.Health <= 0)
            return;

        int recipeId = reader.GetInt();
        string profId = reader.GetString();

        var prof = ProfessionCatalog.Get(profId);
        if (prof == null)
        {
            SendBuyRecipeResult(peer, false, recipeId, 0, "Profissão inválida.");
            return;
        }

        var pdata = player.Professions.GetValueOrDefault((ProfessionType)prof.TipoByte);
        int level = pdata?.Level ?? 1;

        var recipe = prof.Receitas.FirstOrDefault(r => r.RecipeItemId == recipeId);
        if (recipe == null)
        {
            SendBuyRecipeResult(peer, false, recipeId, 0, "Receita não encontrada.");
            return;
        }

        if (recipe.RequiredProfLevel > level)
        {
            SendBuyRecipeResult(peer, false, recipeId, 0, $"Nível insuficiente. Necessário NV.{recipe.RequiredProfLevel}.");
            return;
        }

        if (player.KnownAlchemistRecipes.Contains(recipeId))
        {
            SendBuyRecipeResult(peer, false, recipeId, 0, "Você já conhece esta receita.");
            return;
        }

        int recipePrice = recipe.GoldCost * 3;
        if (player.Gold < recipePrice)
        {
            SendBuyRecipeResult(peer, false, recipeId, 0, $"Gold insuficiente. Necessário {recipePrice} gold.");
            return;
        }

        player.Gold -= recipePrice;
        player.KnownAlchemistRecipes.Add(recipeId);
        _db.SaveAlchemistRecipe(session.SelectedCharacter.Id, recipeId);
        _db.SaveCharacterGold(session.SelectedCharacter.Id, player.Gold);

        SendGoldUpdate(peer, player.Gold);
        SendBuyRecipeResult(peer, true, recipeId, recipePrice, $"Receita aprendida: {recipe.Nome}");
        SendStationData(peer, player, prof);
        Logger.PlayerAction(player.Name, "COMPROU_RECEITA", $"{recipe.Nome} por {recipePrice}g");
    }

    private static bool CalculateAlchemistCrit(int level)
    {
        float critChance = 0.05f + level * 0.02f;
        return Random.Shared.NextDouble() < critChance;
    }

    private void SendLearnRecipeResult(NetPeer peer, bool success, int recipeItemId, string message)
    {
        var w = PacketSerializer.WritePacket(PacketId.S2C_LearnRecipeResult);
        w.Put(success);
        w.Put(recipeItemId);
        w.Put(message);
        peer.Send(w, DeliveryMethod.ReliableOrdered);
    }

    private void SendBuyRecipeResult(NetPeer peer, bool success, int recipeId, int goldCost, string message)
    {
        var w = PacketSerializer.WritePacket(PacketId.S2C_BuyRecipeResult);
        w.Put(success);
        w.Put(recipeId);
        w.Put(goldCost);
        w.Put(message);
        peer.Send(w, DeliveryMethod.ReliableOrdered);
    }

    private void SendCraftResult(NetPeer peer, bool success, int recipeId, int producedItemId, string message)
    {
        var w = PacketSerializer.WritePacket(PacketId.S2C_CraftResult);
        w.Put(success);
        w.Put(recipeId);
        w.Put(producedItemId);
        w.Put(message);
        peer.Send(w, DeliveryMethod.ReliableOrdered);
    }

    private void SendProfessionInfo(NetPeer peer, PlayerEntity player)
    {
        var w = PacketSerializer.WritePacket(PacketId.S2C_ProfessionInfo);

        var alq = player.Professions.GetValueOrDefault(ProfessionType.Alquimista);
        w.Put(alq != null);
        if (alq != null)
        {
            w.Put((byte)alq.Level);
            w.Put(alq.Xp);
            w.Put(alq.XpForNextLevel);
        }

        w.Put(player.KnownAlchemistRecipes.Count);
        foreach (int rid in player.KnownAlchemistRecipes)
            w.Put(rid);

        peer.Send(w, DeliveryMethod.ReliableOrdered);
    }

    private void SendProfessionLevelUp(NetPeer peer, ProfessionType type, int newLevel)
    {
        var w = PacketSerializer.WritePacket(PacketId.S2C_ProfessionLevelUp);
        w.Put((byte)type);
        w.Put((byte)newLevel);
        peer.Send(w, DeliveryMethod.ReliableOrdered);
    }

    private void HandleOpenStation(NetPeer peer, NetDataReader reader)
    {
        if (!_sessions.TryGetValue(peer, out var session) || session.SelectedCharacter == null)
            return;

        var channel = _world.GetChannel(session.ChannelId);
        if (channel?.GetEntity(session.EntityId) is not PlayerEntity player || player.Health <= 0)
            return;

        string profId = reader.GetString();
        var prof = ProfessionCatalog.Get(profId);
        if (prof == null)
        {
            SendSystemMessage(peer, "Mesa de profissão não encontrada.");
            return;
        }

        if (!IsNearNpc(channel, player, prof.NpcPrefab, session.CurrentMap))
        {
            SendSystemMessage(peer, $"Aproxime-se do NPC da mesa de {prof.Nome}.");
            return;
        }

        SendStationData(peer, player, prof);
    }

    private void SendStationData(NetPeer peer, PlayerEntity player, CatalogProfession prof)
    {
        var pdata = player.Professions.GetValueOrDefault((ProfessionType)prof.TipoByte);
        int level = pdata?.Level ?? 1;
        long xp = pdata?.Xp ?? 0;
        int xpForNext = pdata?.XpForNextLevel ?? ProfessionData.GetXpForLevel(2);

        // Todas as receitas disponiveis para o nivel do jogador.
        var available = prof.Receitas
            .Where(r => r.RequiredProfLevel <= level)
            .ToList();

        var w = PacketSerializer.WritePacket(PacketId.S2C_StationData);
        w.Put(prof.Id);
        w.Put(prof.Nome);
        w.Put((byte)level);
        w.Put(xp);
        w.Put(xpForNext);
        w.Put(5f + level * 2f);

        w.Put(available.Count);
        foreach (var r in available)
        {
            bool isKnown = player.KnownAlchemistRecipes.Contains(r.RecipeItemId);
            w.Put(r.RecipeItemId);
            w.Put(string.IsNullOrWhiteSpace(r.Nome) ? $"Receita #{r.RecipeItemId}" : r.Nome);
            w.Put(r.ProducedItemId);
            w.Put(ItemDefinitions.Get(r.ProducedItemId)?.Name ?? $"Item#{r.ProducedItemId}");
            w.Put((short)Math.Max(1, r.ProducedQuantity));
            w.Put(r.RequiredProfLevel);
            w.Put(r.XpReward);
            w.Put(r.GoldCost);
            w.Put(Math.Clamp(r.SuccessRate, 1, 100));
            w.Put((short)Math.Max(0, r.CritBonusQuantity));
            w.Put(isKnown);

            var ings = r.Ingredientes.Where(i => i.Quantity > 0).ToList();
            w.Put((short)ings.Count);
            foreach (var ing in ings)
            {
                w.Put(ing.ItemId);
                w.Put((short)Math.Max(1, ing.Quantity));
                w.Put(ItemDefinitions.Get(ing.ItemId)?.Name ?? $"Item#{ing.ItemId}");
            }
        }

        var ingIds = available.SelectMany(r => r.Ingredientes)
            .Where(i => i.Quantity > 0)
            .Select(i => i.ItemId)
            .Distinct()
            .ToList();
        w.Put(ingIds.Count);
        foreach (int itemId in ingIds)
        {
            long total = player.Items.Where(i => i.ItemId == itemId).Sum(i => (long)i.Quantity);
            w.Put(itemId);
            w.Put(total);
            w.Put(ItemDefinitions.Get(itemId)?.Name ?? $"Item#{itemId}");
        }

        peer.Send(w, DeliveryMethod.ReliableOrdered);
    }

    private void HandleStationCraft(NetPeer peer, NetDataReader reader)
    {
        if (!_sessions.TryGetValue(peer, out var session) || session.SelectedCharacter == null)
            return;

        var channel = _world.GetChannel(session.ChannelId);
        if (channel?.GetEntity(session.EntityId) is not PlayerEntity player || player.Health <= 0)
            return;

        string profId = reader.GetString();
        int count = reader.GetInt();
        if (count < 0 || count > 32)
        {
            SendCraftResult(peer, false, 0, 0, "Materiais inválidos.");
            return;
        }

        var materiais = new Dictionary<int, int>();
        for (int i = 0; i < count; i++)
        {
            int itemId = reader.GetInt();
            int qty = reader.GetInt();
            if (itemId <= 0 || qty <= 0) continue;
            materiais[itemId] = materiais.TryGetValue(itemId, out int atual) ? atual + qty : qty;
        }

        if (materiais.Count == 0)
        {
            SendCraftResult(peer, false, 0, 0, "Coloque os materiais na mesa antes de criar.");
            return;
        }

        var prof = ProfessionCatalog.Get(profId);
        if (prof == null)
        {
            SendCraftResult(peer, false, 0, 0, "Mesa de profissão não encontrada.");
            return;
        }

        if (!IsNearNpc(channel, player, prof.NpcPrefab, session.CurrentMap))
        {
            SendCraftResult(peer, false, 0, 0, "Aproxime-se da mesa para criar itens.");
            return;
        }

        // Encontra receita conhecida cujos ingredientes correspondem exatamente
        // aos materiais colocados na mesa.
        CatalogRecipe? recipe = null;
        foreach (var r in prof.Receitas)
        {
            if (!player.KnownAlchemistRecipes.Contains(r.RecipeItemId)) continue;

            var ings = r.Ingredientes
                .Where(i => i.Quantity > 0)
                .ToDictionary(i => i.ItemId, i => Math.Max(1, i.Quantity));
            if (ings.Count != materiais.Count) continue;
            bool igual = ings.All(kv => materiais.TryGetValue(kv.Key, out int q) && q == kv.Value);
            if (!igual) continue;

            recipe = r;
            break;
        }

        if (recipe == null)
        {
            SendCraftResult(peer, false, 0, 0, "Os materiais não formam uma receita conhecida.");
            return;
        }

        if (ItemDefinitions.Get(recipe.ProducedItemId) == null)
        {
            SendCraftResult(peer, false, recipe.RecipeItemId, 0,
                $"Item produzido #{recipe.ProducedItemId} não está registrado no catálogo do servidor.");
            return;
        }

        var pdata = player.Professions.GetValueOrDefault((ProfessionType)prof.TipoByte);
        if (pdata == null)
        {
            pdata = new ProfessionData { Type = (ProfessionType)prof.TipoByte };
            player.Professions[(ProfessionType)prof.TipoByte] = pdata;
        }

        if (pdata.Level < Math.Max(1, recipe.RequiredProfLevel))
        {
            SendCraftResult(peer, false, recipe.RecipeItemId, 0,
                $"Nível insuficiente. Necessário: {prof.Nome} NV.{Math.Max(1, recipe.RequiredProfLevel)}.");
            return;
        }

        if (player.Gold < recipe.GoldCost)
        {
            SendCraftResult(peer, false, recipe.RecipeItemId, 0,
                $"Ouro insuficiente. Necessário: {recipe.GoldCost} gold.");
            return;
        }

        foreach (var ing in recipe.Ingredientes)
        {
            if (ing.Quantity <= 0) continue;
            int total = player.Items.Where(i => i.ItemId == ing.ItemId).Sum(i => i.Quantity);
            if (total >= ing.Quantity) continue;

            string matName = ItemDefinitions.Get(ing.ItemId)?.Name ?? $"Item#{ing.ItemId}";
            SendCraftResult(peer, false, recipe.RecipeItemId, 0,
                $"Material insuficiente: {matName} x{ing.Quantity}.");
            return;
        }

        float sucesso = Math.Clamp(recipe.SuccessRate, 1, 100) / 100f;
        if (Random.Shared.NextDouble() > sucesso)
        {
            SendCraftResult(peer, false, recipe.RecipeItemId, 0,
                $"A criação falhou! (Chance de sucesso: {Math.Clamp(recipe.SuccessRate, 1, 100)}%)");
            Logger.PlayerAction(player.Name, "STATION_CRAFT_FALHA", $"{prof.Nome}:{recipe.Nome}");
            return;
        }

        // Consome os materiais.
        foreach (var ing in recipe.Ingredientes)
        {
            int remaining = Math.Max(1, ing.Quantity);
            var slots = player.Items.Where(i => i.ItemId == ing.ItemId).OrderBy(i => i.Quantity).ToList();
            foreach (var slotItem in slots)
            {
                if (remaining <= 0) break;
                int take = Math.Min(remaining, slotItem.Quantity);
                slotItem.Quantity -= take;
                remaining -= take;
                if (slotItem.Quantity <= 0)
                {
                    player.Items.Remove(slotItem);
                    _db.DeleteItem(session.SelectedCharacter.Id, slotItem.DbId);
                    ClearConsumableShortcutIfDepleted(peer, player, session.SelectedCharacter.Id, ing.ItemId);
                }
                else
                {
                    _db.SaveItem(session.SelectedCharacter.Id, slotItem);
                }
            }
        }

        player.Gold -= recipe.GoldCost;

        int producedDef = ItemDefinitions.Get(recipe.ProducedItemId)?.MaxStack ?? 99;
        int quantity = Math.Max(1, recipe.ProducedQuantity);
        bool isCrit = CalculateAlchemistCrit(pdata.Level);
        if (isCrit)
            quantity += Math.Max(0, recipe.CritBonusQuantity);

        int producedId = recipe.ProducedItemId;
        for (int i = 0; i < quantity; i++)
        {
            var existingStack = player.Items.FirstOrDefault(x =>
                x.ItemId == producedId && x.Quantity < producedDef);
            if (existingStack != null)
            {
                existingStack.Quantity++;
                _db.SaveItem(session.SelectedCharacter.Id, existingStack);
            }
            else
            {
                int newSlot = 0;
                while (player.Items.Any(x => x.Slot == newSlot) || player.Equipment.ContainsKey(newSlot))
                    newSlot++;

                var newItem = new ItemInstance
                {
                    ItemId = producedId,
                    Quantity = 1,
                    Slot = newSlot,
                };
                player.Items.Add(newItem);
                _db.SaveItem(session.SelectedCharacter.Id, newItem);
            }
        }

        bool leveledUp = pdata.AddXp(recipe.XpReward);
        _db.SaveCharacterProfession(session.SelectedCharacter.Id, pdata);
        _db.SaveCharacterGold(session.SelectedCharacter.Id, player.Gold);

        SendInventoryData(peer, player);
        SendGoldUpdate(peer, player.Gold);

        string critMsg = isCrit ? " (CRÍTICO!)" : "";
        SendCraftResult(peer, true, recipe.RecipeItemId, producedId,
            $"{recipe.Nome}: criado x{quantity}{critMsg}. +{recipe.XpReward} XP de {prof.Nome}.");
        SendStationData(peer, player, prof);

        if (leveledUp)
        {
            SendProfessionLevelUp(peer, (ProfessionType)prof.TipoByte, pdata.Level);
            SendSystemMessage(peer, $"{prof.Nome} subiu para NV.{pdata.Level}!");
        }

        Logger.PlayerAction(player.Name, "STATION_CRAFT",
            $"{prof.Nome}:{recipe.Nome} x{quantity}{critMsg} (xp={recipe.XpReward}, gold={recipe.GoldCost})");
    }
}
