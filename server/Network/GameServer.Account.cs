using LiteNetLib;
using LiteNetLib.Utils;
using Mithara.Server.Packets;

namespace Mithara.Server.Network;

partial class GameServer
{
    private void HandleRegister(NetPeer peer, NetDataReader reader)
    {
        string username = reader.GetString();
        string password = reader.GetString();
        string securityQuestion = reader.GetString();
        string securityAnswer = reader.GetString();

        var writer = PacketSerializer.WritePacket(PacketId.S2C_RegisterResult);

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
            writer.Put(true);
            writer.Put("Conta criada com sucesso!");
            Console.WriteLine($"[SERVER] Conta registrada: {username} (id={accountId})");
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
            Console.WriteLine($"[SERVER] Senha recuperada: {username}");
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

        int? accountId = _db.LoginAccount(username, password);

        var writer = PacketSerializer.WritePacket(PacketId.S2C_LoginResult);

        if (accountId == null)
        {
            writer.Put(false);
            writer.Put("Usuário ou senha inválidos.");
            peer.Send(writer, DeliveryMethod.ReliableOrdered);
            return;
        }

        if (_sessions.TryGetValue(peer, out var session))
            session.AccountId = accountId.Value;

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

        Console.WriteLine($"[SERVER] Login: {username} (id={accountId}) chars={chars.Count}");
    }
}
