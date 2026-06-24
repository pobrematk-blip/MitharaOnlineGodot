#nullable enable
using Godot;
using LiteNetLib.Utils;
using Mithara.Network;
using System.Collections.Generic;
using System.Linq;

public partial class GameNetwork
{
    public void SendLojinhaOpen(ulong lojinhaId)
    {
        _client.SendPacket(PacketId.C2S_LojinhaOpen, w =>
        {
            w.Put(lojinhaId);
        });
    }

    public void SendLojinhaAddItem(ulong lojinhaId, int invSlot, int quantity, int pricePerUnit)
    {
        _client.SendPacket(PacketId.C2S_LojinhaAddItem, w =>
        {
            w.Put(lojinhaId);
            w.Put(invSlot);
            w.Put(quantity);
            w.Put(pricePerUnit);
        });
    }

    public void SendLojinhaRemoveItem(ulong lojinhaId, int slot)
    {
        _client.SendPacket(PacketId.C2S_LojinhaRemoveItem, w =>
        {
            w.Put(lojinhaId);
            w.Put(slot);
        });
    }

    public void SendLojinhaBuyItem(ulong lojinhaId, int slot, int quantity)
    {
        _client.SendPacket(PacketId.C2S_LojinhaBuyItem, w =>
        {
            w.Put(lojinhaId);
            w.Put(slot);
            w.Put(quantity);
        });
    }

    public void SendLojinhaCollect(ulong lojinhaId)
    {
        _client.SendPacket(PacketId.C2S_LojinhaCollect, w =>
        {
            w.Put(lojinhaId);
        });
    }

    public void SendLojinhaClose(ulong lojinhaId)
    {
        _client.SendPacket(PacketId.C2S_LojinhaClose, w =>
        {
            w.Put(lojinhaId);
        });
    }

    public void SendLojinhaListRequest()
    {
        _client.SendPacket(PacketId.C2S_LojinhaListRequest, w => { });
    }

    private void HandleOpenLojinha(NetDataReader r)
    {
        ulong lojinhaId = r.GetULong();
        bool isOwner = r.GetBool();
        string ownerName = r.GetString();
        int itemCount = r.GetInt();
        var items = new Godot.Collections.Array<Godot.Collections.Dictionary>();
        for (int i = 0; i < itemCount; i++)
        {
            var dict = new Godot.Collections.Dictionary
            {
                ["slot"] = r.GetInt(),
                ["item_id"] = r.GetInt(),
                ["quantity"] = r.GetInt(),
                ["price"] = r.GetInt(),
                ["roll_data"] = r.GetString(),
            };
            items.Add(dict);
        }
        EmitSignal(SignalName.OnOpenLojinha, lojinhaId, isOwner, ownerName, items);
    }

    private void HandleLojinhaData(NetDataReader r)
    {
        ulong lojinhaId = r.GetULong();
        bool isOwner = r.GetBool();
        string ownerName = r.GetString();
        int goldEarned = r.GetInt();
        int itemCount = r.GetInt();
        var items = new Godot.Collections.Array<Godot.Collections.Dictionary>();
        for (int i = 0; i < itemCount; i++)
        {
            var dict = new Godot.Collections.Dictionary
            {
                ["slot"] = r.GetInt(),
                ["item_id"] = r.GetInt(),
                ["quantity"] = r.GetInt(),
                ["price"] = r.GetInt(),
                ["roll_data"] = r.GetString(),
            };
            items.Add(dict);
        }
        EmitSignal(SignalName.OnLojinhaData, lojinhaId, isOwner, ownerName, goldEarned, items);
    }

    private void HandleLojinhaBuyResult(NetDataReader r)
    {
        bool success = r.GetBool();
        string message = r.GetString();
        EmitSignal(SignalName.OnLojinhaBuyResult, success, message);
    }

    private void HandleLojinhaListResult(NetDataReader r)
    {
        int count = r.GetInt();
        var lojinhas = new Godot.Collections.Array<Godot.Collections.Dictionary>();
        for (int i = 0; i < count; i++)
        {
            var dict = new Godot.Collections.Dictionary
            {
                ["lojinha_id"] = r.GetULong(),
                ["owner_name"] = r.GetString(),
                ["x"] = r.GetFloat(),
                ["y"] = r.GetFloat(),
                ["channel_id"] = r.GetInt(),
                ["item_count"] = r.GetInt(),
            };
            int itemCount = r.GetInt();
            var items = new Godot.Collections.Array<Godot.Collections.Dictionary>();
            for (int j = 0; j < itemCount; j++)
            {
                var itemDict = new Godot.Collections.Dictionary
                {
                    ["slot"] = r.GetInt(),
                    ["item_id"] = r.GetInt(),
                    ["name"] = r.GetString(),
                    ["quantity"] = r.GetInt(),
                    ["price"] = r.GetInt(),
                    ["roll_data"] = r.GetString(),
                };
                items.Add(itemDict);
            }
            dict["items"] = items;
            lojinhas.Add(dict);
        }
        EmitSignal(SignalName.OnLojinhaListResult, lojinhas);
    }

    private void HandleLojinhaSpawn(NetDataReader r)
    {
        ulong lojinhaId = r.GetULong();
        string ownerName = r.GetString();
        float x = r.GetFloat();
        float y = r.GetFloat();
        EmitSignal(SignalName.OnLojinhaSpawn, lojinhaId, ownerName, x, y);
    }

    private void HandleLojinhaDespawn(NetDataReader r)
    {
        ulong lojinhaId = r.GetULong();
        EmitSignal(SignalName.OnLojinhaDespawn, lojinhaId);
    }
}
