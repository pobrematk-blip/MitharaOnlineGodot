using LiteNetLib;
using LiteNetLib.Utils;
using Mithara.Server.Entities;
using Mithara.Server.Packets;
using Mithara.Server.World;

namespace Mithara.Server.Network;

partial class GameServer
{
    private const float ResourceGatherMaxRange = 160f;

    private void HandleResourceGather(NetPeer peer, NetDataReader reader)
    {
        if (!TryGetPlayer(peer, out var player, out var channel))
            return;
        if (!_sessions.TryGetValue(peer, out var session) || session.SelectedCharacter == null)
            return;

        string resourceName = reader.GetString();
        float resourceX = reader.GetFloat();
        float resourceY = reader.GetFloat();

        if (string.IsNullOrEmpty(resourceName))
        {
            SendResourceGatherResult(peer, false, resourceName, 0, 0,
                "Nome do recurso invalido.");
            return;
        }

        var def = ResourceDefinitions.Get(resourceName);
        if (def == null)
        {
            SendResourceGatherResult(peer, false, resourceName, 0, 0,
                $"Recurso '{resourceName}' nao reconhecido pelo servidor.");
            return;
        }

        if (player.Health <= 0)
        {
            SendResourceGatherResult(peer, false, resourceName, 0, 0,
                "Voce esta morto e nao pode coletar recursos.");
            return;
        }

        float dx = resourceX - player.X;
        float dy = resourceY - player.Y;
        float distSq = dx * dx + dy * dy;
        float maxDist = Math.Max(def.InteractionRange, ResourceGatherMaxRange);
        if (distSq > maxDist * maxDist)
        {
            SendResourceGatherResult(peer, false, resourceName, 0, 0,
                "Voce esta longe demais do recurso.");
            return;
        }

        if (def.RequiredToolItemId > 0)
        {
            bool hasTool = player.Items.Any(i => i.ItemId == def.RequiredToolItemId)
                || player.Equipment.Values.Any(e => e.ItemId == def.RequiredToolItemId);

            if (!hasTool)
            {
                var toolDef = ItemDefinitions.Get(def.RequiredToolItemId);
                string toolName = toolDef?.Name ?? $"Item#{def.RequiredToolItemId}";
                SendResourceGatherResult(peer, false, resourceName, 0, 0,
                    $"Voce precisa de [{toolName}] equipado para coletar {def.Name}!");
                return;
            }
        }

        var drops = new List<(int itemId, int quantity)>();
        foreach (var drop in def.Drops)
        {
            int qty = drop.QtyMin;
            if (drop.QtyMax > drop.QtyMin)
                qty += Random.Shared.Next(drop.QtyMax - drop.QtyMin + 1);

            if (qty <= 0) continue;

            var itemDef = ItemDefinitions.Get(drop.ItemId);
            if (itemDef == null)
            {
                Logger.Warn($"[RESOURCE] Item#{drop.ItemId} nao encontrado no ItemDefinitions");
                continue;
            }

            if (!TryAddItemToInventory(player, session.SelectedCharacter.Id, drop.ItemId, qty))
            {
                SendResourceGatherResult(peer, false, resourceName, 0, 0,
                    "Inventario cheio! Nao foi possivel coletar o recurso.");
                return;
            }

            drops.Add((drop.ItemId, qty));
        }

        SendInventoryData(peer, player);

        if (drops.Count > 0)
        {
            int firstItemId = drops[0].itemId;
            int firstQty = drops[0].quantity;
            string msg = drops.Count == 1
                ? $"Voce coletou {ItemDefinitions.Get(firstItemId)?.Name ?? $"Item#{firstItemId}"} x{firstQty}!"
                : $"Voce coletou {drops.Count} tipos de materiais!";
            SendResourceGatherResult(peer, true, resourceName, firstItemId, firstQty, msg);
        }
        else
        {
            SendResourceGatherResult(peer, false, resourceName, 0, 0,
                "Nada foi coletado deste recurso.");
        }

        BroadcastResourceStateChange(channel, resourceX, resourceY, resourceName, 1);

        Logger.PlayerAction(player.Name, "RESOURCE_GATHER",
            $"{resourceName} @ ({resourceX:F0},{resourceY:F0}) => {string.Join(", ", drops.Select(d => $"{d.itemId}x{d.quantity}"))}");
    }

    private void SendResourceGatherResult(NetPeer peer, bool success, string resourceName,
        int itemId, int quantity, string message)
    {
        var w = PacketSerializer.WritePacket(PacketId.S2C_ResourceGatherResult);
        w.Put(success);
        w.Put(resourceName);
        w.Put(itemId);
        w.Put(quantity);
        w.Put(message);
        peer.Send(w, DeliveryMethod.ReliableOrdered);
    }

    private void BroadcastResourceStateChange(Channel channel, float x, float y,
        string resourceName, int newPhase)
    {
        var w = PacketSerializer.WritePacket(PacketId.S2C_ResourceStateChange);
        w.Put(x);
        w.Put(y);
        w.Put(resourceName);
        w.Put((byte)newPhase);

        foreach (var kv in channel.GetAllEntities())
        {
            if (kv.Value.Type != EntityType.Player) continue;
            var peer = channel.GetPlayerPeer(kv.Key);
            if (peer != null)
                peer.Send(w, DeliveryMethod.ReliableOrdered);
        }
    }
}
