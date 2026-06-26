#nullable enable
using Godot;
using LiteNetLib.Utils;
using Mithara.Network;

partial class GameNetwork
{
    [Signal] public delegate void OnBankDataEventHandler(int onHandGold, int bankGold, Godot.Collections.Array<Godot.Collections.Dictionary> items);
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

    public void SendBankDepositItem(int inventorySlot, int bankSlot)
    {
        _client?.SendPacket(PacketId.C2S_BankDepositItem, w =>
        {
            w.Put(inventorySlot);
            w.Put(bankSlot);
        });
    }

    public void SendBankWithdrawItem(int bankSlot, int inventorySlot)
    {
        _client?.SendPacket(PacketId.C2S_BankWithdrawItem, w =>
        {
            w.Put(bankSlot);
            w.Put(inventorySlot);
        });
    }

    public void SendBankMoveItem(int fromBankSlot, int toBankSlot)
    {
        _client?.SendPacket(PacketId.C2S_BankMoveItem, w =>
        {
            w.Put(fromBankSlot);
            w.Put(toBankSlot);
        });
    }

    private void HandleBankData(NetDataReader r)
    {
        int onHandGold = r.GetInt();
        int bankGold = r.GetInt();
        var items = new Godot.Collections.Array<Godot.Collections.Dictionary>();
        if (r.AvailableBytes >= 4)
        {
            int itemCount = r.GetInt();
            for (int i = 0; i < itemCount; i++)
            {
                items.Add(new Godot.Collections.Dictionary
                {
                    ["slot"] = r.GetInt(),
                    ["item_id"] = r.GetInt(),
                    ["quantity"] = r.GetInt(),
                    ["refine_level"] = r.GetInt(),
                    ["instance_data"] = r.GetString(),
                });
            }
        }

        Gold = onHandGold;
        GD.Print($"[GAME] Banco - Maos: {onHandGold} | Banco: {bankGold} | Itens: {items.Count}");

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

        EmitSignal(SignalName.OnBankData, onHandGold, bankGold, items);
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
