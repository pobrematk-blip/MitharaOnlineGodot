#nullable enable
using Godot;
using LiteNetLib.Utils;
using Mithara.Network;

partial class GameNetwork
{
    [Signal] public delegate void OnNpcDialogEventHandler(string text, Godot.Collections.Array options);
    [Signal] public delegate void OnNpcShopEventHandler(string shopId, Godot.Collections.Array items);
    [Signal] public delegate void OnNpcBuyResultEventHandler(bool success, string message);
    [Signal] public delegate void OnNpcSellResultEventHandler(bool success, string message);

    public void SendNpcInteract(ulong npcEntityId)
    {
        _client?.SendPacket(PacketId.C2S_NpcInteract, w =>
        {
            w.Put(npcEntityId);
        });
    }

    public void SendNpcSelectOption(string action, string actionData)
    {
        _client?.SendPacket(PacketId.C2S_NpcSelectOption, w =>
        {
            w.Put(action);
            w.Put(actionData);
        });
    }

    public void SendNpcBuyItem(string shopId, int itemId, int quantity)
    {
        _client?.SendPacket(PacketId.C2S_NpcBuyItem, w =>
        {
            w.Put(shopId);
            w.Put(itemId);
            w.Put(quantity);
        });
    }

    public void SendNpcSellItem(int slot, int quantity)
    {
        _client?.SendPacket(PacketId.C2S_NpcSellItem, w =>
        {
            w.Put(slot);
            w.Put(quantity);
        });
    }

    private void HandleNpcDialog(NetDataReader r)
    {
        string text = r.GetString();
        int optionCount = r.GetInt();

        var options = new Godot.Collections.Array();
        for (int i = 0; i < optionCount; i++)
        {
            var opt = new Godot.Collections.Dictionary
            {
                ["text"] = r.GetString(),
                ["action"] = r.GetString(),
                ["data"] = r.GetString(),
            };
            options.Add(opt);
        }

        GD.Print($"[GAME] NPC Dialog: \"{text}\" ({optionCount} opções)");
        EmitSignal(SignalName.OnNpcDialog, text, options);
    }

    private void HandleNpcShopItems(NetDataReader r)
    {
        string shopId = r.GetString();
        int itemCount = r.GetInt();

        var items = new Godot.Collections.Array();
        for (int i = 0; i < itemCount; i++)
        {
            var entry = new Godot.Collections.Dictionary
            {
                ["item_id"] = r.GetInt(),
                ["price"] = r.GetInt(),
                ["stock"] = r.GetInt(),
            };
            items.Add(entry);
        }

        GD.Print($"[GAME] NPC Shop: {shopId} ({itemCount} itens)");
        EmitSignal(SignalName.OnNpcShop, shopId, items);
    }

    private void HandleNpcBuyResult(NetDataReader r)
    {
        bool success = r.GetBool();
        string message = r.GetString();
        GD.Print($"[GAME] Compra: {(success ? "OK" : "FALHOU")} - {message}");
        EmitSignal(SignalName.OnNpcBuyResult, success, message);
    }

    private void HandleNpcSellResult(NetDataReader r)
    {
        bool success = r.GetBool();
        string message = r.GetString();
        GD.Print($"[GAME] Venda: {(success ? "OK" : "FALHOU")} - {message}");
        EmitSignal(SignalName.OnNpcSellResult, success, message);
    }
}
