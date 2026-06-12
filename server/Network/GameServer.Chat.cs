using LiteNetLib;
using LiteNetLib.Utils;
using Mithara.Server.Entities;
using Mithara.Server.Packets;
using Mithara.Server.World;
using System.Linq;

namespace Mithara.Server.Network;

partial class GameServer
{
    private bool VerificarAdmin(PlayerSession session, NetPeer peer, out string? error)
    {
        error = null;
        if (session.IsAdminOrAdminMode(_config)) return true;
        error = "Você não é admin.";
        return false;
    }
    private void HandleChat(NetPeer peer, NetDataReader reader)
    {
        if (!_sessions.TryGetValue(peer, out var session)) return;
        var channel = _world.GetChannel(session.ChannelId);
        if (channel == null) return;

        var sender = channel.GetEntity(session.EntityId);
        if (sender == null) return;

        byte channelType = reader.GetByte();
        string targetName = reader.GetString();
        string message = reader.GetString();
        string language = reader.GetString();

        if (string.IsNullOrWhiteSpace(message)) return;
        if (message.Length > 500) message = message[..500];

        if (message.StartsWith('/'))
        {
            HandleChatCommand(peer, session, sender, channel, message);
            return;
        }

        var chatChannel = (ChatChannel)channelType;

        switch (chatChannel)
        {
            case ChatChannel.Global:
                SendGlobalChat(channel, sender.Name, message, language);
                break;

            case ChatChannel.Whisper:
                SendWhisperChat(sender.Name, targetName, message, language);
                break;

            case ChatChannel.Group:
                SendGroupChat(sender, message, language);
                break;

            case ChatChannel.Guild:
                SendGuildChat(sender, message, language);
                break;
        }
    }

    private void HandleChatCommand(NetPeer peer, PlayerSession session, Entity sender, Channel channel, string message)
    {
        var parts = message.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return;
        var cmd = parts[0].ToLowerInvariant();

        switch (cmd)
        {
            case "/invite":
                if (parts.Length < 2) { SendSystemMessage(peer, "Use: /invite <nome>"); return; }
                HandlePartyInvite(peer, session, sender, parts[1]);
                break;

            case "/aceitar":
            case "/accept":
                HandlePartyAccept(peer, session, sender);
                break;

            case "/sair":
            case "/leave":
                HandlePartyLeave(sender);
                break;

            case "/gcreate":
                if (parts.Length < 2) { SendSystemMessage(peer, "Use: /gcreate <nome>"); return; }
                HandleGuildCreate(peer, sender, parts[1]);
                break;

            case "/ginvite":
                if (parts.Length < 2) { SendSystemMessage(peer, "Use: /ginvite <nome>"); return; }
                HandleGuildInvite(peer, session, sender, parts[1]);
                break;

            case "/gaceitar":
            case "/gaccept":
                HandleGuildAccept(peer, session, sender);
                break;

            case "/gsair":
            case "/gleave":
                HandleGuildLeave(sender);
                break;

            case "/pocao":
                SpawnTestPotion(peer, channel, sender);
                break;

            case "/admin":
                if (parts.Length >= 3 && parts[1] == "add")
                {
                    if (!VerificarAdmin(session, peer, out var addErr)) { SendSystemMessage(peer, addErr!); return; }
                    string accountToAdd = parts[2];
                    var adminList = _config.AdminAccounts.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(a => a.Trim()).ToList();
                    if (!adminList.Contains(accountToAdd))
                    {
                        adminList.Add(accountToAdd);
                        _config.AdminAccounts = string.Join(",", adminList);
                        SendSystemMessage(peer, $"Conta '{accountToAdd}' adicionada como admin.");
                    }
                    else
                        SendSystemMessage(peer, $"Conta '{accountToAdd}' ja e admin.");
                    break;
                }
                if (parts.Length >= 3 && parts[1] == "remove")
                {
                    if (!VerificarAdmin(session, peer, out var remErr)) { SendSystemMessage(peer, remErr!); return; }
                    string accountToRemove = parts[2];
                    var adminList = _config.AdminAccounts.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(a => a.Trim()).ToList();
                    if (adminList.Remove(accountToRemove))
                    {
                        _config.AdminAccounts = string.Join(",", adminList);
                        SendSystemMessage(peer, $"Conta '{accountToRemove}' removida dos admins.");
                    }
                    else
                        SendSystemMessage(peer, $"Conta '{accountToRemove}' nao e admin.");
                    break;
                }
                if (_config.AdminMode)
                {
                    session.IsAdmin = true;
                    SendSystemMessage(peer, "Modo admin ativado!");
                }
                else
                {
                    var adminList = _config.AdminAccounts.Split(',', StringSplitOptions.RemoveEmptyEntries);
                    if (adminList.Length == 0 || System.Array.Exists(adminList, a => a.Trim() == session.AccountId.ToString()))
                    {
                        session.IsAdmin = true;
                        SendSystemMessage(peer, "Modo admin ativado!");
                    }
                    else
                        SendSystemMessage(peer, "Sua conta nao tem permissao de admin.");
                }
                break;

            case "/heal":
                if (!VerificarAdmin(session, peer, out var healErr)) { SendSystemMessage(peer, healErr!); return; }
                sender.Health = sender.MaxHealth;
                sender.Mana = sender.MaxMana;
                SendSystemMessage(peer, $"Vida e mana restaurados: {sender.Health}/{sender.MaxHealth} HP, {sender.Mana}/{sender.MaxMana} MP.");
                break;

            case "/god":
                if (!VerificarAdmin(session, peer, out var godErr)) { SendSystemMessage(peer, godErr!); return; }
                session.IsAdmin = true;
                sender.Health = sender.MaxHealth;
                sender.Mana = sender.MaxMana;
                SendSystemMessage(peer, "Modo Deus ativado! (invulnerabilidade + heal full)");
                break;

            case "/level":
                if (!VerificarAdmin(session, peer, out var lvlErr)) { SendSystemMessage(peer, lvlErr!); return; }
                if (parts.Length < 2 || !int.TryParse(parts[1], out int newLevel) || newLevel < 1 || newLevel > 999)
                { SendSystemMessage(peer, "Use: /level <1-999>"); return; }
                sender.Level = newLevel;
                sender.Experience = 0;
                SendSystemMessage(peer, $"Nível alterado para {newLevel}.");
                var lvlWriter = PacketSerializer.WritePacket(PacketId.S2C_LevelUp);
                lvlWriter.Put(sender.Id);
                lvlWriter.Put(newLevel);
                peer.Send(lvlWriter, DeliveryMethod.ReliableOrdered);
                break;

            case "/speed":
                if (!VerificarAdmin(session, peer, out var spdErr)) { SendSystemMessage(peer, spdErr!); return; }
                if (parts.Length < 2 || !float.TryParse(parts[1], out float newSpeed) || newSpeed < 10 || newSpeed > 2000)
                { SendSystemMessage(peer, "Use: /speed <10-2000>"); return; }
                sender.Speed = newSpeed;
                SendSystemMessage(peer, $"Velocidade alterada para {newSpeed}.");
                break;

            case "/summon":
                if (!VerificarAdmin(session, peer, out var sumErr)) { SendSystemMessage(peer, sumErr!); return; }
                if (parts.Length < 2)
                { SendSystemMessage(peer, "Use: /summon <prefabId> (ex: slime, goblin, wolf, skeleton, boss_demon)"); return; }
                var prefab = parts[1].ToLowerInvariant();
                if (channel.Spawner == null)
                { SendSystemMessage(peer, "Spawner não disponível neste canal."); return; }
                var template = channel.Spawner.GetTemplate(prefab);
                if (template == null)
                { SendSystemMessage(peer, $"Prefab '{prefab}' não encontrado."); return; }
                var offsetX = (float)(Random.Shared.NextDouble() - 0.5) * 80f;
                var offsetY = (float)(Random.Shared.NextDouble() - 0.5) * 80f;
                var mob = channel.Spawner.CriarMonstroEm(template, sender.X + offsetX, sender.Y + offsetY);
                if (mob != null)
                {
                    channel.AddEntity(mob);
                    SendSystemMessage(peer, $"{template.Name} invocado em ({mob.X:F0}, {mob.Y:F0}).");
                }
                break;

            case "/item":
                if (!VerificarAdmin(session, peer, out var itemErr)) { SendSystemMessage(peer, itemErr!); return; }
                if (parts.Length < 2 || !int.TryParse(parts[1], out int itemId))
                { SendSystemMessage(peer, "Use: /item <itemId> [quantidade]"); return; }
                int qty = parts.Length >= 3 && int.TryParse(parts[2], out int q) ? Math.Max(1, q) : 1;
                if (sender is not PlayerEntity playerEntity)
                { SendSystemMessage(peer, "Apenas players podem receber itens."); return; }
                int slot = playerEntity.FindEmptyInventorySlot();
                if (slot < 0)
                { SendSystemMessage(peer, "Inventário cheio."); return; }
                playerEntity.Items.Add(new ItemInstance { ItemId = itemId, Quantity = qty, Slot = slot });
                SendSystemMessage(peer, $"Item {itemId} x{qty} adicionado ao inventário (slot {slot}).");
                var invWriter = PacketSerializer.WritePacket(PacketId.S2C_InventoryData);
                invWriter.Put((byte)1);
                invWriter.Put(itemId);
                invWriter.Put(qty);
                invWriter.Put(slot);
                peer.Send(invWriter, DeliveryMethod.ReliableOrdered);
                break;

            case "/kick":
                if (!VerificarAdmin(session, peer, out var kickErr)) { SendSystemMessage(peer, kickErr!); return; }
                if (parts.Length < 2)
                { SendSystemMessage(peer, "Use: /kick <nome_do_jogador>"); return; }
                var targetName = parts[1];
                NetPeer? targetPeer = null;
                foreach (var ch in _world.GetAllChannels())
                {
                    foreach (var kv in ch.GetAllEntities())
                    {
                        if (kv.Value.Type != EntityType.Player) continue;
                        if (kv.Value.Name.Equals(targetName, StringComparison.OrdinalIgnoreCase))
                        {
                            var p = ch.GetPlayerPeer(kv.Key);
                            if (p != null) targetPeer = p;
                            break;
                        }
                    }
                }
                if (targetPeer == null)
                { SendSystemMessage(peer, $"Jogador '{targetName}' não encontrado."); return; }
                targetPeer.Disconnect();
                SendSystemMessage(peer, $"Jogador '{targetName}' desconectado.");
                break;

            case "/broadcast":
                if (!VerificarAdmin(session, peer, out var bcErr)) { SendSystemMessage(peer, bcErr!); return; }
                if (parts.Length < 2)
                { SendSystemMessage(peer, "Use: /broadcast <mensagem>"); return; }
                string bcMsg = string.Join(" ", parts, 1, parts.Length - 1);
                var bcWriter = PacketSerializer.WritePacket(PacketId.S2C_Chat);
                bcWriter.Put((byte)ChatChannel.Global);
                bcWriter.Put("!Admin");
                bcWriter.Put($"[BROADCAST] {bcMsg}");
                bcWriter.Put("pt");
                foreach (var ch in _world.GetAllChannels())
                {
                    foreach (var kv in ch.GetAllEntities())
                    {
                        if (kv.Value.Type != EntityType.Player) continue;
                        var p = ch.GetPlayerPeer(kv.Key);
                        p?.Send(bcWriter, DeliveryMethod.ReliableOrdered);
                        bcWriter = PacketSerializer.WritePacket(PacketId.S2C_Chat);
                        bcWriter.Put((byte)ChatChannel.Global);
                        bcWriter.Put("!Admin");
                        bcWriter.Put($"[BROADCAST] {bcMsg}");
                        bcWriter.Put("pt");
                    }
                }
                SendSystemMessage(peer, $"Broadcast enviado: {bcMsg}");
                break;

            case "/players":
                if (!VerificarAdmin(session, peer, out var plrErr)) { SendSystemMessage(peer, plrErr!); return; }
                int total = 0;
                var playerList = new System.Text.StringBuilder();
                playerList.AppendLine("Jogadores online:");
                foreach (var ch in _world.GetAllChannels())
                {
                    foreach (var kv in ch.GetAllEntities())
                    {
                        if (kv.Value.Type != EntityType.Player) continue;
                        var e = kv.Value;
                        total++;
                        playerList.AppendLine($"  {e.Name} | Lv.{e.Level} | HP:{e.Health}/{e.MaxHealth} | ({e.X:F0},{e.Y:F0})");
                    }
                }
                if (total == 0) playerList.AppendLine("  (nenhum)");
                playerList.Append($"Total: {total}");
                SendSystemMessage(peer, playerList.ToString());
                break;

            default:
                SendSystemMessage(peer, $"Comando desconhecido: {cmd}. Use /ajuda para ver os comandos.");
                break;
        }
    }

    private void SendGlobalChat(Channel channel, string senderName, string message, string language)
    {
        var writer = PacketSerializer.WritePacket(PacketId.S2C_Chat);
        writer.Put((byte)ChatChannel.Global);
        writer.Put(senderName);
        writer.Put(message);
        writer.Put(language);

        foreach (var kv in channel.GetAllEntities())
        {
            if (kv.Value.Type != EntityType.Player) continue;
            var peer = channel.GetPlayerPeer(kv.Key);
            peer?.Send(writer, DeliveryMethod.ReliableOrdered);
        }
    }

    private void SendWhisperChat(string senderName, string targetName, string message, string language)
    {
        if (string.IsNullOrWhiteSpace(targetName)) return;

        var writerToTarget = PacketSerializer.WritePacket(PacketId.S2C_Chat);
        writerToTarget.Put((byte)ChatChannel.Whisper);
        writerToTarget.Put(senderName);
        writerToTarget.Put(message);
        writerToTarget.Put(language);

        var writerToSender = PacketSerializer.WritePacket(PacketId.S2C_Chat);
        writerToSender.Put((byte)ChatChannel.Whisper);
        writerToSender.Put($"-> {targetName}");
        writerToSender.Put(message);
        writerToSender.Put(language);

        NetPeer? targetPeer = null;
        NetPeer? senderPeer = null;

        foreach (var ch in _world.GetAllChannels())
        {
            foreach (var kv in ch.GetAllEntities())
            {
                if (kv.Value.Type != EntityType.Player) continue;
                var peer = ch.GetPlayerPeer(kv.Key);
                if (kv.Value.Name.Equals(targetName, StringComparison.OrdinalIgnoreCase))
                    targetPeer = peer;
                if (kv.Value.Name.Equals(senderName, StringComparison.OrdinalIgnoreCase))
                    senderPeer = peer;
            }
        }

        targetPeer?.Send(writerToTarget, DeliveryMethod.ReliableOrdered);
        senderPeer?.Send(writerToSender, DeliveryMethod.ReliableOrdered);
    }

    private void SendGroupChat(Entity sender, string message, string language)
    {
        if (sender is not PlayerEntity player || player.PartyId < 0) return;

        var writer = PacketSerializer.WritePacket(PacketId.S2C_Chat);
        writer.Put((byte)ChatChannel.Group);
        writer.Put(sender.Name);
        writer.Put(message);
        writer.Put(language);

        var members = _world.Parties.GetMemberEntityIds(player.PartyId);
        foreach (var eid in members)
        {
            foreach (var ch in _world.GetAllChannels())
            {
                var peer = ch.GetPlayerPeer(eid);
                if (peer != null)
                {
                    peer.Send(writer, DeliveryMethod.ReliableOrdered);
                    break;
                }
            }
        }
    }

    private void SendGuildChat(Entity sender, string message, string language)
    {
        if (sender is not PlayerEntity player || player.GuildId < 0) return;

        var writer = PacketSerializer.WritePacket(PacketId.S2C_Chat);
        writer.Put((byte)ChatChannel.Guild);
        writer.Put(sender.Name);
        writer.Put(message);
        writer.Put(language);

        var members = _world.Guilds.GetMemberEntityIds(player.GuildId);
        foreach (var eid in members)
        {
            foreach (var ch in _world.GetAllChannels())
            {
                var peer = ch.GetPlayerPeer(eid);
                if (peer != null)
                {
                    peer.Send(writer, DeliveryMethod.ReliableOrdered);
                    break;
                }
            }
        }
    }

    private void SendPong(NetPeer peer)
    {
        var writer = PacketSerializer.WritePacket(PacketId.S2C_Pong);
        peer.Send(writer, DeliveryMethod.Unreliable);
    }

    private void SendSystemMessage(NetPeer peer, string text)
    {
        if (peer == null) return;
        var writer = PacketSerializer.WritePacket(PacketId.S2C_Chat);
        writer.Put((byte)ChatChannel.Whisper);
        writer.Put("!Sistema");
        writer.Put(text);
        writer.Put("pt");
        peer.Send(writer, DeliveryMethod.ReliableOrdered);
    }
}
