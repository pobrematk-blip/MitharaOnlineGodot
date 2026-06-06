using Godot;

public partial class CashManager : Node
{
    private SaveManager _saveManager;

    public int Diamantes
    {
        get => _saveManager?.Conta?.Diamantes ?? 0;
        set
        {
            if (_saveManager?.Conta != null)
            {
                _saveManager.Conta.Diamantes = value;
                _saveManager.SalvarConta();
            }
        }
    }

    public int SlotsDisponiveis => _saveManager?.SlotsDisponiveis ?? 3;

    public override void _Ready()
    {
        _saveManager = GetNode<SaveManager>("/root/SaveManager");
    }

    public bool GastarDiamantes(int quantidade)
    {
        if (quantidade <= 0 || Diamantes < quantidade) return false;
        Diamantes -= quantidade;
        return true;
    }

    public void AdicionarDiamantes(int quantidade)
    {
        if (quantidade > 0) Diamantes += quantidade;
    }

    public bool ComprarSlotPersonagem()
    {
        if (_saveManager == null) return false;
        return _saveManager.ComprarSlot();
    }

    public int PrecoSlot => _saveManager?.Conta?.PrecoSlot ?? 500;
}
