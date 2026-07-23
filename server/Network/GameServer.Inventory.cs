using System.Linq;
using LiteNetLib;
using LiteNetLib.Utils;
using Mithara.Server.Entities;
using Mithara.Server.Packets;
using Mithara.Server.World;

namespace Mithara.Server.Network;

partial class GameServer
{
    private const int InventorySlotCount = 30;
    private const double PotionUseCooldownSeconds = 40.0;

    private void HandleAdminUpdateItemDefinition(NetPeer peer, NetDataReader reader)
    {
        if (!_sessions.TryGetValue(peer, out var session) || !session.IsAdminOrAdminMode(_config))
        {
            SendSystemMessage(peer, "Apenas administradores podem editar itens.");
            return;
        }

        int id = reader.GetInt();
        string name = reader.GetString().Trim();
        int clientType = reader.GetInt();
        int maxStack = Math.Clamp(reader.GetInt(), 1, 9999);
        bool stackable = reader.GetBool();
        bool isBag = reader.GetBool();
        int extraSlots = Math.Clamp(reader.GetInt(), 0, 100);
        var type = isBag ? ItemType.Bag : clientType switch
        {
            18 or 20 => ItemType.Consumable,
            19 => ItemType.Material,
            >= 0 and <= 17 => (ItemType)clientType,
            _ => ItemType.None,
        };

        var definition = new ItemDefinition
        {
            Id = id,
            Name = name,
            Type = type,
            MaxStack = maxStack,
            IsStackable = stackable,
            IsBag = isBag,
            ExtraSlots = extraSlots,
            Forca = reader.GetInt(),
            Agilidade = reader.GetInt(),
            Destreza = reader.GetInt(),
            Inteligencia = reader.GetInt(),
            BaseAttack = reader.GetInt(),
            Defense = reader.GetInt(),
            MagicDefense = reader.GetInt(),
            Hp = reader.GetInt(),
            Mana = reader.GetInt(),
            Evasion = reader.GetFloat(),
            BuyPrice = Math.Max(0, reader.GetInt()),
            AffixPool = reader.GetString()
                .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList(),
            RequiredLevel = Math.Max(1, reader.GetInt()),
            IsElite = reader.GetBool(),
            AllowedClasses = reader.GetString().Trim(),
            ForcaMin = reader.GetInt(),
            ForcaMax = reader.GetInt(),
            AgilidadeMin = reader.GetInt(),
            AgilidadeMax = reader.GetInt(),
            DestrezaMin = reader.GetInt(),
            DestrezaMax = reader.GetInt(),
            InteligenciaMin = reader.GetInt(),
            InteligenciaMax = reader.GetInt(),
            BaseAttackMin = reader.GetInt(),
            BaseAttackMax = reader.GetInt(),
            DefenseMin = reader.GetInt(),
            DefenseMax = reader.GetInt(),
            MagicDefenseMin = reader.GetInt(),
            MagicDefenseMax = reader.GetInt(),
            HpMin = reader.GetInt(),
            HpMax = reader.GetInt(),
            ManaMin = reader.GetInt(),
            ManaMax = reader.GetInt(),
            EvasionMin = reader.GetFloat(),
            EvasionMax = reader.GetFloat(),
        };

        if (definition.Id <= 0 || string.IsNullOrWhiteSpace(definition.Name)
            || !Enum.IsDefined(typeof(ItemType), definition.Type))
        {
            SendSystemMessage(peer, "Definicao de item invalida.");
            return;
        }

        _db.SaveItemDefinition(definition);
        ItemDefinitions.Register(definition);
        Logger.Info($"[ADMIN] Conta {session.AccountId} atualizou item {definition.Id} ({definition.Name}).");
        SendSystemMessage(peer, $"Item {definition.Name} sincronizado com o servidor.");
    }

    private void SendInventoryData(NetPeer peer, PlayerEntity player)
    {
        var writer = PacketSerializer.WritePacket(PacketId.S2C_InventoryData);
        writer.Put(player.Items.Count);
        foreach (var item in player.Items)
        {
            writer.Put(item.Slot);
            writer.Put(item.ItemId);
            writer.Put(item.Quantity);
            writer.Put(item.RefineLevel);
            writer.Put(System.Text.Json.JsonSerializer.Serialize(item.Roll));
        }
        writer.Put(player.Equipment.Count);
        foreach (var kv in player.Equipment)
        {
            writer.Put(kv.Key);
            writer.Put(kv.Value.ItemId);
            writer.Put(kv.Value.Quantity);
            writer.Put(kv.Value.RefineLevel);
            writer.Put(System.Text.Json.JsonSerializer.Serialize(kv.Value.Roll));
        }
        peer.Send(writer, DeliveryMethod.ReliableOrdered);
    }

    private bool TryAddItemToInventory(PlayerEntity player, int characterId, int itemId, int quantity)
    {
        if (quantity <= 0) return false;

        var def = ItemDefinitions.Get(itemId);
        if (def == null) return false;
        bool stackable = IsStackableInventoryItem(def);
        int maxStack = stackable ? Math.Max(1, def!.MaxStack) : 1;
        int remaining = quantity;

        var stackAdds = new List<(ItemInstance item, int amount)>();
        if (stackable)
        {
            foreach (var existing in player.Items.Where(i =>
                         i.Slot >= 0
                         && i.Slot < InventorySlotCount
                         && i.ItemId == itemId
                         && i.Quantity < maxStack).OrderBy(i => i.Slot))
            {
                int add = Math.Min(remaining, maxStack - existing.Quantity);
                if (add <= 0) continue;
                stackAdds.Add((existing, add));
                remaining -= add;
                if (remaining <= 0) break;
            }
        }

        var usedSlots = new HashSet<int>(player.Items.Select(i => i.Slot));
        var newSlots = new List<int>();
        int newStacksNeeded = stackable
            ? (int)Math.Ceiling(remaining / (double)maxStack)
            : remaining;

        for (int slot = 0; slot < InventorySlotCount && newSlots.Count < newStacksNeeded; slot++)
        {
            if (usedSlots.Contains(slot)) continue;
            usedSlots.Add(slot);
            newSlots.Add(slot);
        }

        if (newSlots.Count < newStacksNeeded)
            return false;

        foreach (var (item, amount) in stackAdds)
        {
            item.Quantity += amount;
            _db.SaveItem(characterId, item);
        }

        remaining = quantity - stackAdds.Sum(x => x.amount);
        foreach (int slot in newSlots)
        {
            if (remaining <= 0) break;

            int amount = stackable ? Math.Min(remaining, maxStack) : 1;
            var newItem = new ItemInstance { Slot = slot, ItemId = itemId, Quantity = amount };
            ItemRoller.EnsureRolled(newItem);
            player.Items.Add(newItem);
            _db.SaveItem(characterId, newItem);
            remaining -= amount;
        }

        return true;
    }

    private static bool IsStackableInventoryItem(ItemDefinition? def)
    {
        if (def == null || !def.IsStackable || def.MaxStack <= 1)
            return false;

        return def.Type is ItemType.Consumable or ItemType.Material;
    }

    private void NormalizeLoadedInventory(PlayerEntity player, int characterId)
    {
        var usedSlots = new HashSet<int>(player.Items.Select(i => i.Slot));

        int? TakeFreeSlot()
        {
            for (int slot = 0; slot < InventorySlotCount; slot++)
            {
                if (usedSlots.Contains(slot))
                    continue;

                usedSlots.Add(slot);
                return slot;
            }

            return null;
        }

        foreach (var item in player.Items.ToList())
        {
            if (IsStackableInventoryItem(item.Definition) || item.Quantity <= 1)
                continue;

            int extraQuantity = item.Quantity - 1;
            item.Quantity = 1;
            _db.SaveItem(characterId, item);

            for (int i = 0; i < extraQuantity; i++)
            {
                var freeSlot = TakeFreeSlot();
                if (freeSlot == null)
                {
                    Logger.Info($"Inventario sem espaco para separar item nao empilhavel {item.ItemId} do personagem {characterId}.");
                    break;
                }

                var splitItem = new ItemInstance
                {
                    Slot = freeSlot.Value,
                    ItemId = item.ItemId,
                    Quantity = 1,
                    RefineLevel = item.RefineLevel,
                };
                player.Items.Add(splitItem);
                _db.SaveItem(characterId, splitItem);
            }
        }

        foreach (var equipped in player.Equipment.Values.ToList())
        {
            if (IsStackableInventoryItem(equipped.Definition) || equipped.Quantity <= 1)
                continue;

            int extraQuantity = equipped.Quantity - 1;
            equipped.Quantity = 1;
            _db.SaveItem(characterId, equipped);

            for (int i = 0; i < extraQuantity; i++)
            {
                var freeSlot = TakeFreeSlot();
                if (freeSlot == null)
                {
                    Logger.Info($"Inventario sem espaco para separar item equipado nao empilhavel {equipped.ItemId} do personagem {characterId}.");
                    break;
                }

                var splitItem = new ItemInstance
                {
                    Slot = freeSlot.Value,
                    ItemId = equipped.ItemId,
                    Quantity = 1,
                    RefineLevel = equipped.RefineLevel,
                };
                player.Items.Add(splitItem);
                _db.SaveItem(characterId, splitItem);
            }
        }
    }

    private void HandleInventoryRequest(NetPeer peer)
    {
        if (!_sessions.TryGetValue(peer, out var session)) return;
        var channel = _world.GetChannel(session.ChannelId);
        if (channel == null) return;
        var entity = channel.GetEntity(session.EntityId);
        if (entity is not PlayerEntity player) return;

        SendInventoryData(peer, player);
    }

    private void HandleEquipItem(NetPeer peer, NetDataReader reader)
    {
        if (!_sessions.TryGetValue(peer, out var session)) return;
        var channel = _world.GetChannel(session.ChannelId);
        if (channel == null) return;
        var entity = channel.GetEntity(session.EntityId);
        if (entity is not PlayerEntity player) return;

        int invSlot = reader.GetInt();
        int equipSlot = reader.GetInt();

        if (invSlot < 0 || invSlot >= InventorySlotCount)
        {
            SendSystemMessage(peer, "Slot de inventario invalido.");
            SendInventoryData(peer, player);
            return;
        }

        var sourceItem = player.Items.FirstOrDefault(i => i.Slot == invSlot);
        if (sourceItem == null) return;
        var def = sourceItem.Definition;
        if (def == null) return;
        if (player.Level < def.RequiredLevel)
        {
            SendSystemMessage(peer, $"Nivel {def.RequiredLevel} necessario para equipar este item.");
            return;
        }
        if (!string.IsNullOrWhiteSpace(def.AllowedClasses)
            && !def.AllowedClasses.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Contains(player.CharacterClass, StringComparer.OrdinalIgnoreCase))
        {
            SendSystemMessage(peer, "Sua classe nao pode equipar este item.");
            return;
        }

        int eqType = def.Type switch
        {
            ItemType.Helmet => 1,
            ItemType.Chestplate => 2,
            ItemType.Belt => 3,
            ItemType.Gloves => 4,
            ItemType.Pants => 5,
            ItemType.Boots => 6,
            ItemType.Weapon => 7,
            ItemType.Shield => 8,
            ItemType.Necklace => 9,
            ItemType.Ring => 10,
            ItemType.Earring => 11,
            ItemType.Rune => 12,
            ItemType.Wing => 13,
            ItemType.Mount => 14,
            ItemType.Pet => 15,
            ItemType.Skin => 16,
            _ => -1,
        };
        if (eqType != equipSlot) return;

        player.Equipment.TryGetValue(equipSlot, out var currentEquipped);

        player.Items.Remove(sourceItem);
        sourceItem.Slot = 100 + equipSlot;
        player.Equipment[equipSlot] = sourceItem;

        if (currentEquipped != null)
        {
            currentEquipped.Slot = invSlot;
            player.Items.Add(currentEquipped);
            _db.DeleteItemBySlot(session.SelectedCharacter!.Id, 100 + equipSlot);
            _db.SaveItem(session.SelectedCharacter.Id, currentEquipped);
        }
        else
        {
            _db.DeleteItemBySlot(session.SelectedCharacter!.Id, invSlot);
        }
        _db.SaveItem(session.SelectedCharacter.Id, sourceItem);

        RecalculatePlayerStats(player);
        player.Health = Math.Min(player.Health, player.MaxHealth);
        player.Mana = Math.Min(player.Mana, player.MaxMana);
        SendStatUpdate(peer, player);

        var writer = PacketSerializer.WritePacket(PacketId.S2C_EquipUpdate);
        writer.Put(equipSlot);
        writer.Put(sourceItem.ItemId);
        writer.Put(sourceItem.Quantity);
        writer.Put(sourceItem.RefineLevel);
        writer.Put(System.Text.Json.JsonSerializer.Serialize(sourceItem.Roll));
        if (currentEquipped != null)
        {
            writer.Put(true);
            writer.Put(currentEquipped.Slot);
            writer.Put(currentEquipped.ItemId);
            writer.Put(currentEquipped.Quantity);
            writer.Put(currentEquipped.RefineLevel);
            writer.Put(System.Text.Json.JsonSerializer.Serialize(currentEquipped.Roll));
        }
        else
        {
            writer.Put(false);
            writer.Put(invSlot);
            writer.Put(0);
            writer.Put(0);
            writer.Put(0);
            writer.Put("");
        }
        peer.Send(writer, DeliveryMethod.ReliableOrdered);

        var aoi = channel.GetEntitiesInAoi(player.X, player.Y);
        foreach (var eid in aoi)
        {
            var p = channel.GetPlayerPeer(eid);
            if (p != null && eid != session.EntityId)
            {
                var visWriter = PacketSerializer.WritePacket(PacketId.S2C_EntityUpdate);
                visWriter.Put(1);
                visWriter.Put(session.EntityId);
                visWriter.Put(player.X);
                visWriter.Put(player.Y);
                visWriter.Put(player.DirX);
                visWriter.Put(player.DirY);
                visWriter.Put(player.Moving);
                visWriter.Put(player.Sprinting);
                visWriter.Put(player.Health);
                visWriter.Put(player.MaxHealth);
                visWriter.Put(player.Mana);
                visWriter.Put(player.MaxMana);
                visWriter.Put(player.Level);
                visWriter.Put(player.Name);
                visWriter.Put(player.FactionId);
                visWriter.Put((byte)0);
                visWriter.Put(player.Experience);
                visWriter.Put(XpForNextLevel(player.Level));
                var guild = player.GuildId >= 0 ? _world.Guilds.GetGuild(player.GuildId) : null;
                visWriter.Put(guild?.Name ?? "");
                visWriter.Put(guild?.Tag ?? "");
                visWriter.Put(guild?.Emblem ?? -1);
                p.Send(visWriter, DeliveryMethod.Unreliable);
            }
        }
    }

    private void HandleUnequipItem(NetPeer peer, NetDataReader reader)
    {
        if (!_sessions.TryGetValue(peer, out var session)) return;
        var channel = _world.GetChannel(session.ChannelId);
        if (channel == null) return;
        var entity = channel.GetEntity(session.EntityId);
        if (entity is not PlayerEntity player) return;

        int equipSlot = reader.GetInt();
        int targetInvSlot = reader.GetInt();

        if (!player.Equipment.TryGetValue(equipSlot, out var equipped)) return;

        if (targetInvSlot < 0)
            targetInvSlot = FindEmptyInventorySlot(player);

        if (targetInvSlot < 0 || targetInvSlot >= InventorySlotCount)
        {
            SendSystemMessage(peer, "Inventario cheio!");
            SendInventoryData(peer, player);
            return;
        }

        var existing = player.Items.FirstOrDefault(i => i.Slot == targetInvSlot);
        if (existing != null && existing.ItemId != 0)
        {
            SendSystemMessage(peer, "Slot de inventario ocupado.");
            SendInventoryData(peer, player);
            return;
        }

        player.Equipment.Remove(equipSlot);
        equipped.Slot = targetInvSlot;
        player.Items.Add(equipped);

        _db.DeleteItemBySlot(session.SelectedCharacter!.Id, 100 + equipSlot);
        _db.SaveItem(session.SelectedCharacter.Id, equipped);

        RecalculatePlayerStats(player);
        player.Health = Math.Min(player.Health, player.MaxHealth);
        player.Mana = Math.Min(player.Mana, player.MaxMana);
        SendStatUpdate(peer, player);

        var writer = PacketSerializer.WritePacket(PacketId.S2C_EquipUpdate);
        writer.Put(equipSlot);
        writer.Put(0);
        writer.Put(0);
        writer.Put(0);
        writer.Put("");
        writer.Put(true);
        writer.Put(targetInvSlot);
        writer.Put(equipped.ItemId);
        writer.Put(equipped.Quantity);
        writer.Put(equipped.RefineLevel);
        writer.Put(System.Text.Json.JsonSerializer.Serialize(equipped.Roll));
        peer.Send(writer, DeliveryMethod.ReliableOrdered);
    }

    private static int FindEmptyInventorySlot(PlayerEntity player)
    {
        for (int slot = 0; slot < InventorySlotCount; slot++)
        {
            if (!player.Items.Any(item => item.Slot == slot))
                return slot;
        }

        return -1;
    }

    private void HandleMoveItem(NetPeer peer, NetDataReader reader)
    {
        if (!_sessions.TryGetValue(peer, out var session)) return;
        var channel = _world.GetChannel(session.ChannelId);
        if (channel == null) return;
        var entity = channel.GetEntity(session.EntityId);
        if (entity is not PlayerEntity player) return;

        int fromSlot = reader.GetInt();
        int toSlot = reader.GetInt();

        if (fromSlot < 0 || fromSlot >= InventorySlotCount || toSlot < 0 || toSlot >= InventorySlotCount)
        {
            SendSystemMessage(peer, "Slot de inventario invalido.");
            SendInventoryData(peer, player);
            return;
        }

        var fromItem = player.Items.FirstOrDefault(i => i.Slot == fromSlot);
        var toItem = player.Items.FirstOrDefault(i => i.Slot == toSlot);

        if (fromItem == null) return;

        if (toItem != null && fromItem.ItemId == toItem.ItemId && IsStackableInventoryItem(fromItem.Definition))
        {
            int totalQty = fromItem.Quantity + toItem.Quantity;
            int maxStack = Math.Max(1, fromItem.Definition!.MaxStack);
            if (totalQty <= maxStack)
            {
                toItem.Quantity = totalQty;
                player.Items.Remove(fromItem);
                _db.DeleteItem(session.SelectedCharacter!.Id, fromItem.DbId);
                _db.SaveItem(session.SelectedCharacter!.Id, toItem);
            }
            else
            {
                toItem.Quantity = maxStack;
                fromItem.Quantity = totalQty - maxStack;
                _db.SaveItem(session.SelectedCharacter!.Id, toItem);
                _db.SaveItem(session.SelectedCharacter!.Id, fromItem);
            }
            SendInventoryData(peer, player);
            return;
        }

        fromItem.Slot = toSlot;
        if (toItem != null)
            toItem.Slot = fromSlot;
        _db.SaveItem(session.SelectedCharacter!.Id, fromItem);
        if (toItem != null)
            _db.SaveItem(session.SelectedCharacter!.Id, toItem);

        SendInventoryData(peer, player);
    }

    private void HandleUseItem(NetPeer peer, NetDataReader reader)
    {
        if (!_sessions.TryGetValue(peer, out var session) || session.SelectedCharacter == null)
            return;

        var channel = _world.GetChannel(session.ChannelId);
        if (channel?.GetEntity(session.EntityId) is not PlayerEntity player || player.Health <= 0)
            return;

        int slot = reader.GetInt();
        var item = player.Items.FirstOrDefault(candidate => candidate.Slot == slot);
        var def = item?.Definition;
        if (item == null || def == null || def.Type != ItemType.Consumable)
            return;

        int restoredHealth = 0;
        int restoredMana = 0;
        string? customUseMessage = null;
        if (item.ItemId == ItemDefinitions.PocaoVida && def.Hp > 0)
        {
            restoredHealth = Math.Min(def.Hp, player.MaxHealth - player.Health);
            if (restoredHealth <= 0)
            {
                SendSystemMessage(peer, "Sua vida já está cheia.");
                return;
            }
            if (!TryConsumePotionCooldown(peer, player, "potion:health", "pocao de vida"))
                return;
            player.Health += restoredHealth;
            BroadcastPartyMemberUpdateForEntity(player.Id);
        }
        else if (item.ItemId == ItemDefinitions.PocaoMana && def.Mana > 0)
        {
            restoredMana = Math.Min(def.Mana, player.MaxMana - player.Mana);
            if (restoredMana <= 0)
            {
                SendSystemMessage(peer, "Sua mana já está cheia.");
                return;
            }
            if (!TryConsumePotionCooldown(peer, player, "potion:mana", "pocao de mana"))
                return;
            player.Mana += restoredMana;
            BroadcastPartyMemberUpdateForEntity(player.Id);
        }
        else if (item.ItemId == ItemDefinitions.PergaminhoResetTalentos)
        {
            player.UnlockedTalents.Clear();
            if (player.SkillBarSlots == null || player.SkillBarSlots.Length != 20)
                player.SkillBarSlots = new int[20];
            Array.Fill(player.SkillBarSlots, 0);

            _db.DeleteCharacterTalents(session.SelectedCharacter.Id);
            _db.DeleteCharacterSkillSlots(session.SelectedCharacter.Id);

            item.Quantity--;
            if (item.Quantity <= 0)
            {
                player.Items.Remove(item);
                _db.DeleteItem(session.SelectedCharacter.Id, item.DbId);
            }
            else
            {
                _db.SaveItem(session.SelectedCharacter.Id, item);
            }

            SendInventoryData(peer, player);
            SendTalentData(peer, player);
            SendSkillBarData(peer, player);
            SendSystemMessage(peer, "Talentos resetados. Seus pontos foram devolvidos.");
            return;
        }
        else if (item.ItemId is ItemDefinitions.PergaminhoVip7Dias or ItemDefinitions.PergaminhoVip15Dias or ItemDefinitions.PergaminhoVip30Dias or ItemDefinitions.PergaminhoVip7DiasTrial)
        {
            int days = item.ItemId switch
            {
                ItemDefinitions.PergaminhoVip7Dias => 7,
                ItemDefinitions.PergaminhoVip15Dias => 15,
                ItemDefinitions.PergaminhoVip30Dias => 30,
                ItemDefinitions.PergaminhoVip7DiasTrial => 7,
                _ => 0,
            };
            if (days <= 0) return;
            var currentExpiry = _db.LoadVipExpiry(session.AccountId);
            var now = DateTime.UtcNow;
            var baseTime = currentExpiry > now ? currentExpiry : now;
            var newExpiry = baseTime.AddDays(days);
            _db.SaveVipExpiry(session.AccountId, newExpiry);
            player.VipExpiry = newExpiry;
            SendVipStatus(peer, newExpiry);
            customUseMessage = $"VIP ativado ate {newExpiry:dd/MM/yyyy HH:mm} UTC. Bonus: 2x XP e 2x chance de drop.";
            Logger.Info($"[VIP] Conta {session.AccountId} usou pergaminho VIP de {days} dias. Expira em: {newExpiry:yyyy-MM-dd HH:mm:ss}");

            item.Quantity--;
            if (item.Quantity <= 0)
            {
                player.Items.Remove(item);
                _db.DeleteItem(session.SelectedCharacter.Id, item.DbId);
            }
            else
            {
                _db.SaveItem(session.SelectedCharacter.Id, item);
            }

            SendInventoryData(peer, player);
            SendSystemMessage(peer, customUseMessage);

            var vipResult = PacketSerializer.WritePacket(PacketId.S2C_ItemUseResult);
            vipResult.Put(player.Health);
            vipResult.Put(player.MaxHealth);
            vipResult.Put(player.Mana);
            vipResult.Put(player.MaxMana);
            peer.Send(vipResult, DeliveryMethod.ReliableOrdered);
            return;
        }
        else if (ItemDefinitions.GetLojinhaMaxSlots(item.ItemId) > 0)
        {
            int maxSlots = ItemDefinitions.GetLojinhaMaxSlots(item.ItemId);
            var lojinha = new LojinhaEntity(player.X, player.Y)
            {
                MaxSlots = maxSlots,
                  OwnerCharacterId = session.SelectedCharacter.Id,
                  OwnerEntityId = player.Id,
                  OwnerName = player.Name,
                  ShopName = $"Loja de {player.Name}",
                  OwnerClass = player.CharacterClass,
                  OwnerRace = player.Race,
                  IsOpen = false,
                  ChannelId = session.ChannelId,
              };

            ulong dbId = _db.SaveLojinha(lojinha);
            lojinha.DbId = dbId;
            channel.AddLojinha(lojinha);

            SendLojinhaSpawnToPeer(peer, lojinha);

            item.Quantity--;
            if (item.Quantity <= 0)
            {
                player.Items.Remove(item);
                _db.DeleteItem(session.SelectedCharacter.Id, item.DbId);
            }
            else
            {
                _db.SaveItem(session.SelectedCharacter.Id, item);
            }

            SendInventoryData(peer, player);
              SendSystemMessage(peer, "Lojinha colocada! Clique no seu clone para configurar e abrir a loja.");
              return;
          }
        else
        {
            return;
        }

        item.Quantity--;
        if (item.Quantity <= 0)
        {
            player.Items.Remove(item);
            _db.DeleteItem(session.SelectedCharacter.Id, item.DbId);
        }
        else
        {
            _db.SaveItem(session.SelectedCharacter.Id, item);
        }

        SendInventoryData(peer, player);
        SendSystemMessage(peer, restoredHealth > 0
            ? $"Poção de Vida usada: +{restoredHealth} HP."
            : $"Poção de Mana usada: +{restoredMana} mana.");

        var result = PacketSerializer.WritePacket(PacketId.S2C_ItemUseResult);
        result.Put(player.Health);
        result.Put(player.MaxHealth);
        result.Put(player.Mana);
        result.Put(player.MaxMana);
        result.Put(item.ItemId);
        result.Put((float)PotionUseCooldownSeconds);
        peer.Send(result, DeliveryMethod.ReliableOrdered);
    }

    private bool TryConsumePotionCooldown(NetPeer peer, PlayerEntity player, string cooldownKey, string potionName)
    {
        if (player.ItemCooldowns.TryGetValue(cooldownKey, out double readyAt) && readyAt > _gameTime)
        {
            int remainingSeconds = Math.Max(1, (int)Math.Ceiling(readyAt - _gameTime));
            SendSystemMessage(peer, $"Aguarde {remainingSeconds}s para usar outra {potionName}.");
            return false;
        }

        player.ItemCooldowns[cooldownKey] = _gameTime + PotionUseCooldownSeconds;
        return true;
    }

    private void HandleDropItem(NetPeer peer, NetDataReader reader)
    {
        if (!_sessions.TryGetValue(peer, out var session)) return;
        var channel = _world.GetChannel(session.ChannelId);
        if (channel == null) return;
        var entity = channel.GetEntity(session.EntityId);
        if (entity is not PlayerEntity player) return;

        int slot = reader.GetInt();
        int quantity = reader.GetInt();

        if (quantity <= 0) return;

        var item = player.Items.FirstOrDefault(i => i.Slot == slot);
        if (item == null) return;

        int dropQty;
        if (quantity >= item.Quantity)
        {
            dropQty = item.Quantity;
            player.Items.Remove(item);
            _db.DeleteItem(session.SelectedCharacter!.Id, item.DbId);
        }
        else
        {
            dropQty = quantity;
            item.Quantity -= quantity;
            _db.SaveItem(session.SelectedCharacter!.Id, item);
        }

        SendInventoryData(peer, player);

        var offsetX = (float)(Random.Shared.NextDouble() - 0.5) * 20f;
        var offsetY = (float)(Random.Shared.NextDouble() - 0.5) * 20f;
        var loot = new LootEntity(entity.X + offsetX, entity.Y + offsetY, item.ItemId, dropQty, player.Id, _gameTime);
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
    }

    private static double GetRefineMultiplier(int refineLevel)
    {
        return refineLevel switch
        {
            1 => 1.02,
            2 => 1.04,
            3 => 1.06,
            4 => 1.08,
            5 => 1.10,
            6 => 1.13,
            7 => 1.16,
            8 => 1.20,
            9 => 1.25,
            10 => 1.30,
            _ => 1.0,
        };
    }

    private static void RecalculatePlayerStats(PlayerEntity player)
    {
        int bonusAtk = 0, bonusDef = 0, bonusMagicDef = 0, bonusHp = 0, bonusMana = 0;
        int bonusForca = 0, bonusAgi = 0, bonusDes = 0, bonusInt = 0;
        float bonusEvasion = 0;
        float bonusCritChance = 0f;
        float bonusCritDamage = 0f;
        float bonusPrecision = 0f;
        float bonusTenacity = 0f;
        float bonusAttackSpeed = 0f;
        float bonusMovementSpeed = 0f;
        float bonusArmorPen = 0f;
        float bonusHealthRegen = 0f;
        float bonusManaRegen = 0f;
        float bonusLifeSteal = 0f;
        float bonusManaSteal = 0f;
        float bonusCooldownReduction = 0f;
        int bonusPvpDamage = 0;
        int bonusPvpDefense = 0;
        float bonusExperience = 0f;
        float bonusDamageReflect = 0f;
        float bonusControlResistance = 0f;
        foreach (var kv in player.Equipment)
        {
            var item = kv.Value;
            var def = item.Definition;
            if (def == null) continue;
            ItemRoller.EnsureRolled(item);
            var roll = item.Roll;
            double refineMult = GetRefineMultiplier(item.RefineLevel);
            bonusAtk += Refined(roll.BaseAttack + Affix(item, "BaseAttack"), item.RefineLevel, refineMult);
            bonusDef += Refined(roll.Defense + Affix(item, "DefesaFisica"), item.RefineLevel, refineMult);
            bonusMagicDef += Refined(roll.MagicDefense + Affix(item, "DefesaMagica"), item.RefineLevel, refineMult);
            bonusHp += Refined(roll.Hp + Affix(item, "Hp"), item.RefineLevel, refineMult);
            bonusMana += Refined(roll.Mana + Affix(item, "Mana"), item.RefineLevel, refineMult);
            bonusEvasion += Math.Min(40f, (float)((roll.Evasion + Affix(item, "Evasao")) * refineMult));
            bonusForca += Refined(roll.Forca + Affix(item, "Forca"), item.RefineLevel, refineMult);
            bonusAgi += Refined(roll.Agilidade + Affix(item, "Agilidade"), item.RefineLevel, refineMult);
            bonusDes += Refined(roll.Destreza + Affix(item, "Destreza"), item.RefineLevel, refineMult);
            bonusInt += Refined(roll.Inteligencia + Affix(item, "Inteligencia"), item.RefineLevel, refineMult);
            bonusCritChance += (float)(Affix(item, "ChanceCritica") * refineMult);
            bonusCritDamage += (float)(Affix(item, "DanoCriticoBonus") * refineMult);
            bonusPrecision += (float)(Affix(item, "Precisao") * refineMult);
            bonusTenacity += (float)(Affix(item, "Tenacidade") * refineMult);
            bonusAttackSpeed += (float)(Affix(item, "VelocidadeAtaque") * refineMult);
            bonusMovementSpeed += (float)(Affix(item, "VelocidadeMovimento") * refineMult);
            bonusArmorPen += (float)(Affix(item, "PenetracaoArmadura") * refineMult);
            bonusHealthRegen += (float)(Affix(item, "RegeneracaoVida") * refineMult);
            bonusManaRegen += (float)(Affix(item, "RegeneracaoMana") * refineMult);
            bonusLifeSteal += (float)(Affix(item, "RouboVida") * refineMult);
            bonusManaSteal += (float)(Affix(item, "RouboMana") * refineMult);
            bonusCooldownReduction += (float)(Affix(item, "ReducaoCooldown") * refineMult);
            bonusPvpDamage += Refined(Affix(item, "DanoPvp"), item.RefineLevel, refineMult);
            bonusPvpDefense += Refined(Affix(item, "DefesaPvp"), item.RefineLevel, refineMult);
            bonusExperience += (float)(Affix(item, "BonusExperiencia") * refineMult);
            bonusDamageReflect += (float)((Affix(item, "ReflexaoDano") + Affix(item, "Reflexao")) * refineMult);
            bonusControlResistance += (float)(Affix(item, "ResistenciaControle") * refineMult);
        }
        int baseAttack = player.CharacterClass.ToLowerInvariant() switch
        {
            "guerreiro" => 10,
            "arqueiro" => 7,
            "mago" => 5,
            _ => 6,
        };
        int baseDefense = player.CharacterClass.ToLowerInvariant() switch
        {
            "guerreiro" => 8,
            "arqueiro" => 4,
            "mago" => 2,
            _ => 4,
        };

        player.BaseAttack = baseAttack + bonusAtk;
        player.Defense = baseDefense + bonusDef;
        player.MagicDefense = bonusMagicDef;
        player.EquipmentEvasion = Math.Clamp(bonusEvasion, 0f, 40f);
        player.CritChanceBonus = Math.Clamp(bonusCritChance, 0f, 45f);
        player.CritDamageBonus = Math.Clamp(bonusCritDamage, 0f, 100f);
        player.PrecisionBonus = Math.Clamp(bonusPrecision, 0f, 60f);
        player.TenacityBonus = Math.Clamp(bonusTenacity, 0f, 75f);
        player.AttackSpeedBonus = Math.Clamp(bonusAttackSpeed, 0f, 80f);
        player.MovementSpeedBonus = Math.Clamp(bonusMovementSpeed, 0f, 30f);
        player.ArmorPenetration = Math.Clamp(bonusArmorPen, 0f, 60f);
        player.HealthRegenBonus = Math.Clamp(bonusHealthRegen, 0f, 200f);
        player.ManaRegenBonus = Math.Clamp(bonusManaRegen, 0f, 200f);
        player.LifeSteal = Math.Clamp(bonusLifeSteal, 0f, 15f);
        player.ManaSteal = Math.Clamp(bonusManaSteal, 0f, 15f);
        player.CooldownReduction = Math.Clamp(bonusCooldownReduction, 0f, 40f);
        player.PvpDamageBonus = Math.Max(0, bonusPvpDamage);
        player.PvpDefenseBonus = Math.Max(0, bonusPvpDefense);
        player.BonusExperience = Math.Clamp(bonusExperience, 0f, 100f);
        player.DamageReflect = Math.Clamp(bonusDamageReflect, 0f, 25f);
        player.ControlResistance = Math.Clamp(bonusControlResistance, 0f, 50f);
        player.Forca = player.BaseForca + bonusForca;
        player.Agilidade = player.BaseAgilidade + bonusAgi;
        player.Destreza = player.BaseDestreza + bonusDes;
        player.Inteligencia = player.BaseInteligencia + bonusInt;
        player.MaxHealth = 80 + player.Forca * 2 + player.Level * 10 + bonusHp;
        player.MaxMana = 30 + player.Inteligencia * 3 + player.Level * 5 + bonusMana;
    }

    private static int Refined(float value, int refineLevel, double multiplier)
    {
        if (value <= 0f)
            return 0;

        int baseValue = (int)Math.Round(value);
        int refinedValue = (int)Math.Round(value * multiplier);
        if (refineLevel <= 0)
            return refinedValue;

        return Math.Max(refinedValue, baseValue + refineLevel);
    }

    private static float Affix(ItemInstance item, string name)
    {
        var def = item.Definition;
        if (def == null || !ItemRoller.IsAffixAllowedForItem(def, name))
            return 0f;

        return item.Roll.Affixes.TryGetValue(name, out float value) ? value : 0f;
    }

    private void HandleCollectLocalItem(NetPeer peer, NetDataReader reader)
    {
        if (!_sessions.TryGetValue(peer, out var onlineSession) || onlineSession.ChannelId < 0) return;
        var onlineCharacter = onlineSession.SelectedCharacter;
        if (onlineCharacter == null) return;

        int onlineItemId = reader.GetInt();
        int onlineQuantity = Math.Clamp(reader.GetInt(), 1, 99);
        float itemX = reader.AvailableBytes >= 8 ? reader.GetFloat() : 0f;
        float itemY = reader.AvailableBytes >= 4 ? reader.GetFloat() : 0f;

        if (ItemDefinitions.Get(onlineItemId) == null)
        {
            SendSystemMessage(peer, "Item invalido.");
            return;
        }

        var onlineChannel = _world.GetChannel(onlineSession.ChannelId);
        if (onlineChannel == null) return;

        var onlinePlayer = onlineChannel.GetEntity(onlineSession.EntityId) as PlayerEntity;
        if (onlinePlayer == null) return;

        if (itemX != 0f || itemY != 0f)
        {
            float dx = onlinePlayer.X - itemX;
            float dy = onlinePlayer.Y - itemY;
            if (MathF.Sqrt(dx * dx + dy * dy) > 100f)
            {
                SendSystemMessage(peer, "Item muito longe.");
                return;
            }
        }

        if (!TryAddItemToInventory(onlinePlayer, onlineCharacter.Id, onlineItemId, onlineQuantity))
        {
            SendSystemMessage(peer, "Inventario cheio!");
            SendInventoryData(peer, onlinePlayer);
            return;
        }

        UpdateQuestCollectProgress(onlinePlayer, onlineItemId.ToString(), onlineQuantity);
        SendInventoryData(peer, onlinePlayer);
        return;
/*
    SendSystemMessage(peer, "Inventário cheio!");
}
*/
    }

    private void HandleCashShopBuy(NetPeer peer, NetDataReader reader)
    {
        if (!TryGetPlayer(peer, out var player, out var channel)) return;

        int itemId = reader.GetInt();
        _ = reader.GetInt();

        if (!_sessions.TryGetValue(peer, out var cashSession) || cashSession.SelectedCharacter == null)
            return;

        int preco = GetCashShopPrice(itemId);
        if (preco <= 0)
        {
            SendCashShopResult(peer, false, "Item nao disponivel na loja cash.");
            SendCashBalance(peer, cashSession.AccountId);
            return;
        }

        if (itemId == -1)
        {
            if (!_db.TryBuyCharacterSlot(cashSession.AccountId, preco, MaxCharacterSlots, out var slotBalance, out var newSlotLimit, out var reason))
            {
                SendCashShopResult(peer, false, string.IsNullOrWhiteSpace(reason) ? "Nao foi possivel comprar o slot." : reason);
                SendCashBalanceValue(peer, slotBalance);
                return;
            }

            SendCashShopResult(peer, true, $"Slot extra comprado! Limite atual: {newSlotLimit}.");
            SendCashBalanceValue(peer, slotBalance);
            SendCharacterList(peer, _db.GetCharacters(cashSession.AccountId));
            return;
        }

        var def = ItemDefinitions.Get(itemId);
        if (def == null)
        {
            SendCashShopResult(peer, false, "Item inválido.");
            return;
        }

        if (!_db.TrySpendCash(cashSession.AccountId, preco, out var balanceAfterSpend))
        {
            SendCashShopResult(peer, false, "Diamantes insuficientes.");
            SendCashBalance(peer, cashSession.AccountId);
            return;
        }

        if (!TryAddItemToInventory(player, cashSession.SelectedCharacter.Id, itemId, 1))
        {
            int refundedBalance = _db.AddCash(cashSession.AccountId, preco);
            SendCashShopResult(peer, false, "Inventário cheio.");
            SendCashBalanceValue(peer, refundedBalance);
            return;
        }

        SendCashShopResult(peer, true, "Compra realizada!");
        SendCashBalanceValue(peer, balanceAfterSpend);
        SendInventoryData(peer, player);
    }

    private static int GetCashShopPrice(int itemId)
    {
        return itemId switch
        {
            -1 => 30,
            100 => 30,
            102 => 50,
            103 => 100,
            104 => 180,
            105 => 300,
            108 => 50,
            109 => 100,
            112 => 200,
            _ => 0,
        };
    }

    private void SendCashShopResult(NetPeer peer, bool success, string message)
    {
        var writer = PacketSerializer.WritePacket(PacketId.S2C_CashShopResult);
        writer.Put(success);
        writer.Put(message);
        peer.Send(writer, DeliveryMethod.ReliableOrdered);
    }

    private void SendCashBalance(NetPeer peer, int accountId)
    {
        SendCashBalanceValue(peer, _db.GetCashBalance(accountId));
    }

    private void SendCashBalanceValue(NetPeer peer, int balance)
    {
        var writer = PacketSerializer.WritePacket(PacketId.S2C_CashBalance);
        writer.Put(balance);
        peer.Send(writer, DeliveryMethod.ReliableOrdered);
    }

    private static readonly int[] RefineSuccessRates = { 100, 80, 70, 60, 50, 40, 30, 20, 10, 5 };

    private void HandleRefineItem(NetPeer peer, NetDataReader reader)
    {
        if (!TryGetPlayer(peer, out var player, out var channel)) return;

        if (!_sessions.TryGetValue(peer, out var session) || session.SelectedCharacter == null)
            return;

        int slot = reader.GetInt();
        int itemId = reader.GetInt();

        var item = player.Items.FirstOrDefault(i => i.Slot == slot && i.ItemId == itemId);
        if (item == null)
        {
            SendRefineResult(peer, false, 0, "Item não encontrado no inventário.");
            return;
        }

        var def = ItemDefinitions.Get(itemId);
        if (def == null)
        {
            SendRefineResult(peer, false, 0, "Item inválido.");
            return;
        }

        int type = (int)def.Type;
        if (type < 1 || type > 13)
        {
            SendRefineResult(peer, false, 0, "Este item não pode ser refinado.");
            return;
        }

        if (item.RefineLevel >= 10)
        {
            SendRefineResult(peer, false, item.RefineLevel, "Item já está no nível máximo (+10).");
            return;
        }

        int goldCost = (item.RefineLevel + 1) * 1000;
        int stardustCost = (item.RefineLevel + 1) * 5;

        if (player.Gold < goldCost)
        {
            SendRefineResult(peer, false, item.RefineLevel, $"Gold insuficiente. Necessário: {goldCost}");
            return;
        }

        int stardustTotal = player.Items
            .Where(i => i.ItemId == ItemDefinitions.PoeiraEstelar)
            .Sum(i => i.Quantity);

        if (stardustTotal < stardustCost)
        {
            SendRefineResult(peer, false, item.RefineLevel, $"Poeira Estelar insuficiente. Necessário: {stardustCost}");
            return;
        }

        int chance = RefineSuccessRates[Math.Min(item.RefineLevel, 9)];
        bool success = Random.Shared.Next(100) < chance;

        player.Gold -= goldCost;
        _db.SaveCharacterGold(session.SelectedCharacter.Id, player.Gold);

        int remaining = stardustCost;
        foreach (var sd in player.Items.Where(i => i.ItemId == ItemDefinitions.PoeiraEstelar).OrderBy(i => i.Slot))
        {
            if (remaining <= 0) break;
            int take = Math.Min(remaining, sd.Quantity);
            sd.Quantity -= take;
            remaining -= take;
            _db.SaveItem(session.SelectedCharacter.Id, sd);
        }
        player.Items.RemoveAll(i => i.ItemId == ItemDefinitions.PoeiraEstelar && i.Quantity <= 0);

        if (success)
        {
            item.RefineLevel++;
        }
        else
        {
            if (item.RefineLevel > 0)
                item.RefineLevel--;
        }

        _db.SaveItem(session.SelectedCharacter.Id, item);
        RecalculatePlayerStats(player);
        player.Health = Math.Min(player.Health, player.MaxHealth);
        player.Mana = Math.Min(player.Mana, player.MaxMana);
        SendStatUpdate(peer, player);

        SendRefineResult(peer, success, item.RefineLevel, success
            ? "Refino bem-sucedido!"
            : "Refino falhou. O item perdeu um nível.");
        SendInventoryData(peer, player);
        SendGoldUpdate(peer, player.Gold);
    }

    private void SendRefineResult(NetPeer peer, bool success, int newLevel, string message)
    {
        var writer = PacketSerializer.WritePacket(PacketId.S2C_RefineResult);
        writer.Put(success);
        writer.Put(newLevel);
        writer.Put(message);
        peer.Send(writer, DeliveryMethod.ReliableOrdered);
    }
}
