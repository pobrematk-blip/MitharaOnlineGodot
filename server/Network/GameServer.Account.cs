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
        string password = reader.GetString();
        string securityQuestion = reader.GetString();
        string securityAnswer = reader.GetString();

        var writer = PacketSerializer.WritePacket(PacketId.S2C_RegisterResult);

        if (!CheckRateLimit(_loginAttempts, _loginCooldowns, peer.Address.ToString(), _gameTime, out var limitMsg))
        {
            writer.Put(false);
            writer.Put(limitMsg);
            peer.Send(writer, DeliveryMethod.ReliableOrdered);
            return;
        }

        if (username.Length < 3 || password.Length < 3)
        {
            writer.Put(false);
            writer.Put("Usuário e senha devem ter pelo menos 3 caracteres.");
            peer.Send(writer, DeliveryMethod.ReliableOrdered);
            return;
        }

        int? accountId = _db.CreateAccount(username, password, securityQuestion, securityAnswer);
        if (accountId == null)
        {
            writer.Put(false);
            writer.Put("Usuário já existe.");
        }
        else
        {
            _loginAttempts.Remove(peer.Address.ToString());
            writer.Put(true);
            writer.Put("Conta criada com sucesso!");
            Logger.Info($"Conta registrada: {username} (id={accountId})");
        }

        peer.Send(writer, DeliveryMethod.ReliableOrdered);
    }

    private void HandleGetSecurityQuestion(NetPeer peer, NetDataReader reader)
    {
        string username = reader.GetString();

        var question = _db.GetSecurityQuestion(username);

        var writer = PacketSerializer.WritePacket(PacketId.S2C_SecurityQuestion);
        if (question == null)
        {
            writer.Put(false);
            writer.Put("Usuário não encontrado ou sem pergunta secreta.");
        }
        else
        {
            writer.Put(true);
            writer.Put(question);
        }
        peer.Send(writer, DeliveryMethod.ReliableOrdered);
    }

    private void HandleRecoverPassword(NetPeer peer, NetDataReader reader)
    {
        string username = reader.GetString();
        string answer = reader.GetString();
        string newPassword = reader.GetString();

        var writer = PacketSerializer.WritePacket(PacketId.S2C_RecoverResult);

        if (newPassword.Length < 3)
        {
            writer.Put(false);
            writer.Put("A nova senha deve ter pelo menos 3 caracteres.");
            peer.Send(writer, DeliveryMethod.ReliableOrdered);
            return;
        }

        bool ok = _db.RecoverPassword(username, answer, newPassword);
        if (ok)
        {
            writer.Put(true);
            writer.Put("Senha redefinida com sucesso!");
            Logger.Info($"Senha recuperada: {username}");
        }
        else
        {
            writer.Put(false);
            writer.Put("Resposta secreta inválida.");
        }
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
            writer.Put("Usuário ou senha inválidos.");
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
                Logger.Info($"Login recusado: conta {accountId.Value} já está conectada em outro cliente.");
                writer.Put(false);
                writer.Put("Esta conta já está logada.");
                peer.Send(writer, DeliveryMethod.ReliableOrdered);
                return;
            }

            // A conexão registrada já caiu; libera apenas a referência obsoleta.
            _activeAccounts.Remove(accountId.Value);
        }

        if (_sessions.TryGetValue(peer, out var session))
            session.AccountId = accountId.Value;
        _activeAccounts[accountId.Value] = peer;

        var chars = _db.GetCharacters(accountId.Value);

        writer.Put(true);
        writer.Put(accountId.Value);
        writer.Put(chars.Count);
        foreach (var ch in chars)
        {
            writer.Put(ch.SlotIndex);
            writer.Put(ch.Name);
            writer.Put(ch.Class);
            writer.Put(ch.Race);
            writer.Put(ch.Level);
        }
        peer.Send(writer, DeliveryMethod.ReliableOrdered);

        Logger.Info($"Login: {username} (id={accountId}) chars={chars.Count}");
    }
}
