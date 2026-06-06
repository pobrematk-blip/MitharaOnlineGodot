using Godot;

public partial class BankUI : Control
{
    private Panel _panel;
    private Panel _titleBar;
    private Button _closeButton;
    private Label _goldLabel;
    private Label _bankGoldLabel;
    private SpinBox _amountInput;
    private Button _depositButton;
    private Button _withdrawButton;
    private GameNetwork _gameNet;
    private bool _arrastando;
    private Vector2 _pontoCliqueOriginal;

    public override void _Ready()
    {
        _panel = GetNode<Panel>("Panel");
        _titleBar = _panel.GetNode<Panel>("TitleBar");
        _closeButton = _panel.GetNode<Button>("CloseButton");
        _goldLabel = _panel.GetNode<Label>("GoldLabel");
        _bankGoldLabel = _panel.GetNode<Label>("BankGoldLabel");
        _amountInput = _panel.GetNode<SpinBox>("AmountInput");
        _depositButton = _panel.GetNode<Button>("DepositButton");
        _withdrawButton = _panel.GetNode<Button>("WithdrawButton");

        _gameNet = GetNodeOrNull<GameNetwork>("/root/GameNetwork");
        if (_gameNet != null)
        {
            _gameNet.OnBankData += OnBankData;
            _gameNet.OnBankResult += OnBankResult;
        }

        _closeButton.Pressed += Fechar;
        _titleBar.GuiInput += OnTitleBarGuiInput;
        _depositButton.Pressed += OnDeposit;
        _withdrawButton.Pressed += OnWithdraw;

        _panel.Visible = false;
    }

    private void OnBankData(int onHandGold, int bankGold)
    {
        _goldLabel.Text = $"Ouro em mãos: {onHandGold}";
        _bankGoldLabel.Text = $"Ouro no Banco: {bankGold}";
        _amountInput.Value = 1;
        _panel.Visible = true;
        Centralizar();
    }

    private void OnBankResult(bool success, string message)
    {
        if (!success)
        {
            GD.PrintErr($"[BANK] {message}");
        }
    }

    private void OnDeposit()
    {
        if (_gameNet == null) return;
        int amount = (int)_amountInput.Value;
        if (amount > 0)
        {
            _gameNet.SendBankDeposit(amount);
        }
    }

    private void OnWithdraw()
    {
        if (_gameNet == null) return;
        int amount = (int)_amountInput.Value;
        if (amount > 0)
        {
            _gameNet.SendBankWithdraw(amount);
        }
    }

    private void Centralizar()
    {
        Vector2 tela = GetViewportRect().Size;
        _panel.Position = (tela / 2) - (_panel.Size / 2);
    }

    private void Fechar()
    {
        _panel.Visible = false;
    }

    private void OnTitleBarGuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mouseEvent && mouseEvent.ButtonIndex == MouseButton.Left)
        {
            _arrastando = mouseEvent.Pressed;
            if (mouseEvent.Pressed) _pontoCliqueOriginal = mouseEvent.Position;
        }
        else if (@event is InputEventMouseMotion mouseMotion && _arrastando)
            _panel.Position += mouseMotion.Position - _pontoCliqueOriginal;
    }

    public override void _ExitTree()
    {
        if (_gameNet != null)
        {
            _gameNet.OnBankData -= OnBankData;
            _gameNet.OnBankResult -= OnBankResult;
        }
    }
}
