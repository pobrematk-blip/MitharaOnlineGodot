#nullable enable
using Godot;
using LiteNetLib.Utils;
using Mithara.Network;

partial class GameNetwork
{
    public void SendInventoryRequest()
    {
        _client?.SendPacket(PacketId.C2S_InventoryRequest, _ => { });
    }

    public void SendEquipItem(int inventorySlot, int equipSlot)
    {
        _client?.SendPacket(PacketId.C2S_EquipItem, w =>
        {
            w.Put(inventorySlot);
            w.Put(equipSlot);
        });
    }

    public void SendUnequipItem(int equipSlot, int targetInvSlot)
    {
        _client?.SendPacket(PacketId.C2S_UnequipItem, w =>
        {
            w.Put(equipSlot);
            w.Put(targetInvSlot);
        });
    }

    public void SendMoveItem(int fromSlot, int toSlot)
    {
        _client?.SendPacket(PacketId.C2S_MoveItem, w =>
        {
            w.Put(fromSlot);
            w.Put(toSlot);
        });
    }

    public void SendDropItem(int slot, int quantity)
    {
        _client?.SendPacket(PacketId.C2S_DropItem, w =>
        {
            w.Put(slot);
            w.Put(quantity);
        });
    }

    private void HandleInventoryData(NetDataReader r)
    {
        int invCount = r.GetInt();
        var items = new Godot.Collections.Array<Godot.Collections.Dictionary>();
        for (int i = 0; i < invCount; i++)
        {
            var entry = new Godot.Collections.Dictionary
            {
                ["slot"] = r.GetInt(),
                ["item_id"] = r.GetInt(),
                ["quantity"] = r.GetInt(),
                ["refine_level"] = r.GetInt(),
            };
            items.Add(entry);
        }

        int equipCount = r.GetInt();
        var equipment = new Godot.Collections.Array<Godot.Collections.Dictionary>();
        for (int i = 0; i < equipCount; i++)
        {
            var entry = new Godot.Collections.Dictionary
            {
                ["slot"] = r.GetInt(),
                ["item_id"] = r.GetInt(),
                ["quantity"] = r.GetInt(),
                ["refine_level"] = r.GetInt(),
            };
            equipment.Add(entry);
        }

        PendingInventoryData = items;
        PendingEquipmentData = equipment;

        GD.Print($"[GAME] Inventário recebido: {invCount} itens, {equipCount} equipados");
        EmitSignal(SignalName.OnInventoryData, items, equipment);

        var player = GetTree().CurrentScene?.FindChild("Player", true, false);
        if (player == null) return;

        var inv = player.FindChild("InventarioComponent", true, false) as InventarioComponent;
        if (inv != null && ItemDB != null)
        {
            inv.AplicarDadosServidor(items, ItemDB);
        }

        var equip = player.FindChild("EquipamentoComponent", true, false) as EquipamentoComponent;
        if (equip != null && ItemDB != null)
        {
            foreach (var entry in equipment)
            {
                int slot = (int)entry["slot"];
                int itemId = (int)entry["item_id"];
                int qty = (int)entry["quantity"];
                int refineLevel = (int)entry["refine_level"];
                var resource = ItemDB.GetItem(itemId);
                if (resource != null)
                {
                    var tipo = (TipoEquipamento)slot;
                    equip.ItensEquipados[tipo] = new SlotInventario(resource, qty, refineLevel);
                }
            }
            equip.EmitSignal(EquipamentoComponent.SignalName.EquipamentoAtualizado);
        }
    }

    private void HandleEquipUpdate(NetDataReader r)
    {
        int equipSlot = r.GetInt();
        int itemId = r.GetInt();
        int quantity = r.GetInt();
        int refineLevel = r.GetInt();
        bool hasUnequip = r.GetBool();
        int invSlot = r.GetInt();
        int unequipItemId = r.GetInt();
        int unequipQuantity = r.GetInt();
        int unequipRefineLevel = r.GetInt();

        EmitSignal(SignalName.OnEquipUpdate, equipSlot, itemId, quantity, hasUnequip, invSlot, unequipItemId, unequipQuantity);

        var player = GetTree().CurrentScene?.FindChild("Player", true, false);
        if (player == null) return;

        var equip = player.FindChild("EquipamentoComponent", true, false) as EquipamentoComponent;
        var inv = player.FindChild("InventarioComponent", true, false) as InventarioComponent;
        if (equip == null) return;

        var tipo = (TipoEquipamento)equipSlot;
        var newItem = ItemDB?.GetItem(itemId);

        if (hasUnequip && inv != null)
        {
            var oldItem = ItemDB?.GetItem(unequipItemId);
            if (oldItem != null && invSlot >= 0 && invSlot < inv.Slots.Count)
            {
                inv.Slots[invSlot] = new SlotInventario(oldItem, unequipQuantity, unequipRefineLevel);
            }
        }

        if (newItem != null)
        {
            equip.ItensEquipados[tipo] = new SlotInventario(newItem, quantity, refineLevel);
        }
        else if (itemId == 0 && equip.ItensEquipados.ContainsKey(tipo))
        {
            equip.ItensEquipados.Remove(tipo);
        }

        equip.RecalcularBonusEquipamentos();
        equip.EmitSignal(EquipamentoComponent.SignalName.EquipamentoAtualizado);
    }

    private void HandleItemUpdate(NetDataReader r)
    {
        int slot = r.GetInt();
        int itemId = r.GetInt();
        int quantity = r.GetInt();
        int refineLevel = r.GetInt();

        GD.Print($"[GAME] Item update: slot={slot} itemId={itemId} qty={quantity} refine={refineLevel}");
        EmitSignal(SignalName.OnItemUpdate, slot, itemId, quantity);

        var player = GetTree().CurrentScene?.FindChild("Player", true, false);
        if (player == null) return;

        var inv = player.FindChild("InventarioComponent", true, false) as InventarioComponent;
        if (inv == null || ItemDB == null) return;

        if (slot >= 0 && slot < inv.Slots.Count)
        {
            if (itemId == 0 || quantity <= 0)
            {
                inv.Slots[slot] = new SlotInventario(null, 0);
            }
            else
            {
                var resource = ItemDB.GetItem(itemId);
                if (resource != null)
                    inv.Slots[slot] = new SlotInventario(resource, quantity, refineLevel);
            }
            inv.EmitSignal(InventarioComponent.SignalName.InventarioAtualizado);
        }
    }
}
