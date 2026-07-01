using Godot;

public partial class CashManager : Node
{
    private SaveManager _saveManager;
    private GameNetwork _net;

    public int Diamantes
    {
        get => _net?.CashBalance ?? _saveManager?.Conta?.Diamantes ?? 0;
        set => GD.PrintErr("[CASH] Alteracao local de diamantes bloqueada. O saldo vem do servidor.");
    }

    public int SlotsDisponiveis => _saveManager?.SlotsDisponiveis ?? 3;

    public override void _Ready()
    {
        _saveManager = GetNode<SaveManager>("/root/SaveManager");
        _net = GetNodeOrNull<GameNetwork>("/root/GameNetwork");
        if (_net != null && !_net.IsConnected(nameof(GameNetwork.OnCashBalance), Callable.From<int>(OnCashBalance)))
            _net.Connect(GameNetwork.SignalName.OnCashBalance, Callable.From<int>(OnCashBalance));
    }

    private void OnCashBalance(int balance)
    {
        if (_saveManager?.Conta != null)
            _saveManager.Conta.Diamantes = balance;
    }

    public bool GastarDiamantes(int quantidade)
    {
        GD.PrintErr("[CASH] Gasto local bloqueado. Compra deve passar pelo servidor.");
        return false;
    }

    public void AdicionarDiamantes(int quantidade)
    {
        GD.PrintErr("[CASH] Adicao local bloqueada. Credito de cash deve passar pelo servidor.");
    }

    public bool ComprarSlotPersonagem()
    {
        if (_saveManager == null) return false;
        return _saveManager.ComprarSlot();
    }

    public int PrecoSlot => _saveManager?.Conta?.PrecoSlot ?? 500;
}
