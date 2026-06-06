using Godot;

public partial class LojaCashUI : Control
{
    private Label _tituloLabel;
    private Label _diamantesLabel;
    private Label _slotsLabel;
    private Label _precoLabel;
    private Button _comprarSlotBtn;
    private Button _fecharBtn;
    private Control _feedback;
    private Label _feedbackLabel;

    private CashManager _cash;

    public override void _Ready()
    {
        _tituloLabel = GetNode<Label>("%TituloLabel");
        _diamantesLabel = GetNode<Label>("%DiamantesLabel");
        _slotsLabel = GetNode<Label>("%SlotsLabel");
        _precoLabel = GetNode<Label>("%PrecoLabel");
        _comprarSlotBtn = GetNode<Button>("%ComprarSlotBtn");
        _fecharBtn = GetNode<Button>("%FecharBtn");
        _feedback = GetNode<Control>("%Feedback");
        _feedbackLabel = GetNode<Label>("%FeedbackLabel");

        _cash = GetNode<CashManager>("/root/CashManager");

        _fecharBtn.Pressed += OnFechar;
        _comprarSlotBtn.Pressed += OnComprarSlot;

        AtualizarUI();
    }

    private void AtualizarUI()
    {
        if (_cash == null) return;
        _diamantesLabel.Text = $"ðŸ’Ž {_cash.Diamantes}";
        _slotsLabel.Text = $"Slots: {_cash.SlotsDisponiveis}";
        _precoLabel.Text = $"ðŸ’Ž {_cash.PrecoSlot}";
        _comprarSlotBtn.Disabled = _cash.Diamantes < _cash.PrecoSlot;
    }

    private void OnComprarSlot()
    {
        if (_cash == null) return;

        if (_cash.ComprarSlotPersonagem())
        {
            MostrarFeedback("✓ Slot comprado!", new Color(0.3f, 0.8f, 0.4f));
            AtualizarUI();
        }
        else
        {
            MostrarFeedback("âŒ Diamantes insuficientes!", new Color(0.9f, 0.3f, 0.3f));
        }
    }

    private void MostrarFeedback(string texto, Color cor)
    {
        if (_feedback == null || _feedbackLabel == null) return;
        _feedbackLabel.Text = texto;
        _feedbackLabel.AddThemeColorOverride("font_color", cor);
        _feedback.Visible = true;

        var timer = GetTree().CreateTimer(2.0);
        timer.Timeout += () =>
        {
            if (_feedback != null) _feedback.Visible = false;
        };
    }

    private void OnFechar()
    {
        QueueFree();
    }

    public void Abrir()
    {
        var parent = GetTree().CurrentScene;
        if (parent != null)
        {
            parent.AddChild(this);
            AtualizarUI();
        }
    }
}
