#nullable enable
using Godot;
using LiteNetLib.Utils;
using Mithara.Network;

partial class GameNetwork
{
    public void SendLogin(string username, string password)
    {
        _client?.SendPacket(PacketId.C2S_Login, w =>
        {
            w.Put(username);
            w.Put(password);
        });
    }

    public void SendRegister(string username, string password, string securityQuestion = "", string securityAnswer = "")
    {
        _client?.SendPacket(PacketId.C2S_Register, w =>
        {
            w.Put(username);
            w.Put(password);
            w.Put(securityQuestion);
            w.Put(securityAnswer);
        });
    }

    public void SendGetSecurityQuestion(string username)
    {
        _client?.SendPacket(PacketId.C2S_GetSecurityQuestion, w =>
        {
            w.Put(username);
        });
    }

    public void SendRecoverPassword(string username, string answer, string newPassword)
    {
        _client?.SendPacket(PacketId.C2S_RecoverPassword, w =>
        {
            w.Put(username);
            w.Put(answer);
            w.Put(newPassword);
        });
    }

    private void HandleLoginResult(NetDataReader r)
    {
        bool success = r.GetBool();
        if (success)
        {
            AccountId = r.GetInt();
            int charCount = r.GetInt();
            Characters.Clear();
            for (int i = 0; i < charCount; i++)
            {
                Characters.Add(new CharacterEntry
                {
                    SlotIndex = r.GetInt(),
                    Name = r.GetString(),
                    Class = r.GetString(),
                    Race = r.GetString(),
                    Level = r.GetInt(),
                });
            }
            LoggedIn = true;
            GD.Print($"[GAME] Login OK! AccountId={AccountId}, chars={charCount}");
            EmitSignal(SignalName.OnLoginResult, true, $"Bem-vindo! ({Characters.Count} personagens)");
        }
        else
        {
            string reason = r.GetString();
            GD.Print($"[GAME] Login falhou: {reason}");
            EmitSignal(SignalName.OnLoginResult, false, reason);
        }
    }

    private void HandleRegisterResult(NetDataReader r)
    {
        bool success = r.GetBool();
        string message = r.GetString();
        GD.Print($"[GAME] Registro: {(success ? "OK" : "falhou")} - {message}");
        EmitSignal(SignalName.OnRegisterResult, success, message);
    }

    private void HandleSecurityQuestion(NetDataReader r)
    {
        bool found = r.GetBool();
        string questionOrError = found ? r.GetString() : r.GetString();
        GD.Print($"[GAME] Pergunta secreta: {(found ? questionOrError : "ERRO: " + questionOrError)}");
        EmitSignal(SignalName.OnSecurityQuestion, found, questionOrError);
    }

    private void HandleRecoverResult(NetDataReader r)
    {
        bool success = r.GetBool();
        string message = r.GetString();
        GD.Print($"[GAME] Recuperação: {(success ? "OK" : "falhou")} - {message}");
        EmitSignal(SignalName.OnRecoverResult, success, message);
    }
}
