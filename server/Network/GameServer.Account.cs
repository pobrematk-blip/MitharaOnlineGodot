using LiteNetLib;
using LiteNetLib.Utils;
using Mithara.Server.Packets;

namespace Mithara.Server.Network;

partial class GameServer
{
    private static bool CheckRateLimit(Dictionary<string, int> attempts, Dictionary<string, double> cooldowns, string key, double currentTime, out string message)
    {
        if (cooldowns.TryGetValue(key, out var until) && currentTime < until)
        {
            int secs = (int)(until - currentTime) + 1;
            message = $"Aguarde {secs}s antes de tentar novamente.";
            return false;
        }

        attempts.TryGetValue(key, out var count);
        count++;
        attempts[key] = count;

        if (count > 5)
        {
            cooldowns[key] = currentTime + 30.0;
            attempts[key] = 0;
            message = "Muitas tentativas. Aguarde 30s.";
            return false;
        }

        message = "";
        return true;
    }

    private void HandleRegister(NetPeer peer, NetDataReader reader)
    {
        string username = reader.GetString();

        var writer = PacketSerializer.WritePacket(PacketId.S2C_RegisterResult);
        writer.Put(false);
        writer.Put("A criacao de conta agora e feita somente pelo site oficial: https://mithara.online");
        peer.Send(writer, DeliveryMethod.ReliableOrdered);
        Logger.Info($"Registro recusado pelo jogo para '{username}'. Cadastro permitido somente pelo site.");
    }

    private void HandleGetSecurityQuestion(NetPeer peer, NetDataReader reader)
    {
        string email = reader.GetString();

        var writer = PacketSerializer.WritePacket(PacketId.S2C_SecurityQuestion);
        writer.Put(false);
        writer.Put("Recupere sua senha pelo site oficial: https://mithara.online/Account/ForgotPassword");
        Logger.Info($"Recuperacao recusada pelo jogo para '{email}'. Recuperacao permitida somente pelo site.");
        peer.Send(writer, DeliveryMethod.ReliableOrdered);
    }

    private void HandleRecoverPassword(NetPeer peer, NetDataReader reader)
    {
        _ = reader.GetString();
        _ = reader.GetString();
        _ = reader.GetString();

        var writer = PacketSerializer.WritePacket(PacketId.S2C_RecoverResult);
        writer.Put(false);
        writer.Put("Recupere sua senha pelo site oficial: https://mithara.online/Account/ForgotPassword");
        peer.Send(writer, DeliveryMethod.ReliableOrdered);
    }

    private void HandleLogin(NetPeer peer, NetDataReader reader)
    {
        string username = reader.GetString();
        string password = reader.GetString();

        var writer = PacketSerializer.WritePacket(PacketId.S2C_LoginResult);

        if (!CheckRateLimit(_loginAttempts, _loginCooldowns, peer.Address.ToString(), _gameTime, out var limitMsg))
        {
            writer.Put(false);
            writer.Put(limitMsg);
            peer.Send(writer, DeliveryMethod.ReliableOrdered);
            return;
        }

        int? accountId = _db.LoginAccount(username, password);

        if (accountId == null)
        {
            writer.Put(false);
            writer.Put("Usuario ou senha invalidos.");
            peer.Send(writer, DeliveryMethod.ReliableOrdered);
            return;
        }

        _loginAttempts.Remove(peer.Address.ToString());

        if (_activeAccounts.TryGetValue(accountId.Value, out var existingPeer) && existingPeer != peer)
        {
            if (existingPeer.ConnectionState == ConnectionState.Connected &&
                _sessions.TryGetValue(existingPeer, out var activeSession) &&
                activeSession.AccountId == accountId.Value)
            {
                Logger.Info($"Login recusado: conta {accountId.Value} ja esta conectada em outro cliente.");
                writer.Put(false);
                writer.Put("Esta conta ja esta logada.");
                peer.Send(writer, DeliveryMethod.ReliableOrdered);
                return;
            }

            _activeAccounts.Remove(accountId.Value);
        }

        if (_sessions.TryGetValue(peer, out var session))
            session.AccountId = accountId.Value;
        _activeAccounts[accountId.Value] = peer;
        _db.UpdateAccountLastSeen(accountId.Value);

        var chars = _db.GetCharacters(accountId.Value);

        writer.Put(true);
        writer.Put(accountId.Value);
        writer.Put(chars.Count);
        writer.Put(Math.Clamp(_db.GetCharacterSlotLimit(accountId.Value), BaseCharacterSlots, MaxCharacterSlots));
        foreach (var ch in chars)
        {
            writer.Put(ch.SlotIndex);
            writer.Put(ch.Name);
            writer.Put(ch.Class);
            writer.Put(ch.Race);
            writer.Put(ch.Level);
            WriteCharacterAppearance(writer, ch);
        }
        peer.Send(writer, DeliveryMethod.ReliableOrdered);
        SendCashBalance(peer, accountId.Value);

        Logger.Info($"Login: {username} (id={accountId}) chars={chars.Count}");
    }
}
