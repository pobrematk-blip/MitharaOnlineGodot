#nullable enable
using Godot;
using LiteNetLib.Utils;
using Mithara.Network;

partial class GameNetwork
{
    [Signal] public delegate void OnBankDataEventHandler(int onHandGold, int bankGold);
    [Signal] public delegate void OnBankResultEventHandler(bool success, string message);

    public void SendBankDeposit(int amount)
    {
        _client?.SendPacket(PacketId.C2S_BankDeposit, w =>
        {
            w.Put(amount);
        });
    }

    public void SendBankWithdraw(int amount)
    {
        _client?.SendPacket(PacketId.C2S_BankWithdraw, w =>
        {
            w.Put(amount);
        });
    }

    public void SendBankRequest()
    {
        _client?.SendPacket(PacketId.C2S_BankRequest, w => { });
    }

    private void HandleBankData(NetDataReader r)
    {
        int onHandGold = r.GetInt();
        int bankGold = r.GetInt();
        Gold = onHandGold;
        GD.Print($"[GAME] Banco - Mãos: {onHandGold} | Banco: {bankGold}");

        if (GetTree()?.CurrentScene != null)
        {
            var banco = GetTree().CurrentScene.FindChild("BancoUi", true, false) as BancoUI;
            if (banco == null)
            {
                var bancoScene = ResourceLoader.Load<PackedScene>("res://Banco/BancoUI.tscn");
                if (bancoScene != null)
                {
                    banco = bancoScene.Instantiate<BancoUI>();
                    GetTree().CurrentScene.AddChild(banco);
                }
            }
        }

        EmitSignal(SignalName.OnBankData, onHandGold, bankGold);
    }

    private void HandleBankResult(NetDataReader r)
    {
        bool success = r.GetBool();
        string message = r.GetString();
        GD.Print($"[GAME] Banco: {(success ? "OK" : "FALHOU")} - {message}");
        EmitSignal(SignalName.OnBankResult, success, message);
    }

    private void HandleGoldUpdate(NetDataReader r)
    {
        int gold = r.GetInt();
        Gold = gold;
        GD.Print($"[GAME] Ouro atualizado: {gold}");
        EmitSignal(SignalName.OnGoldUpdate, gold);
    }
}
