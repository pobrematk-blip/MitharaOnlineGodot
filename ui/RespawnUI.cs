using Godot;

public partial class RespawnUI : Control
{
    private Panel _panel;
    private Label _titleLabel;
    private Label _messageLabel;
    private Label _timerLabel;
    private Button _respawnButton;
    private Button _waitButton;
    private GameNetwork _gameNet;

    private const float WaitDuration = 300f;
    private float _waitTimer;
    private bool _isWaiting;
    private bool _isDead;

    public override void _Ready()
    {
        _panel = GetNode<Panel>("Panel");
        _titleLabel = _panel.GetNode<Label>("TitleLabel");
        _messageLabel = _panel.GetNode<Label>("MessageLabel");
        _timerLabel = _panel.GetNode<Label>("TimerLabel");
        _respawnButton = _panel.GetNode<Button>("RespawnButton");
        _waitButton = _panel.GetNode<Button>("WaitButton");

        _gameNet = GetNodeOrNull<GameNetwork>("/root/GameNetwork");

        _respawnButton.Pressed += OnRespawnNow;
        _waitButton.Pressed += OnWait;

        _panel.Visible = false;
        _timerLabel.Visible = false;
    }

    public void ShowDeathScreen()
    {
        _isDead = true;
        _isWaiting = false;
        _waitTimer = 0f;
        _titleLabel.Text = "Voce Morreu!";
        _messageLabel.Text = "Deseja reviver agora ou aguardar ajuda?";
        _timerLabel.Visible = false;
        _respawnButton.Text = "Reviver Agora";
        _waitButton.Text = "Aguardar (5:00)";
        _respawnButton.Disabled = false;
        _waitButton.Disabled = false;
        _panel.Visible = true;
        Centralizar();
    }

    public void HideDeathScreen()
    {
        _isDead = false;
        _isWaiting = false;
        _panel.Visible = false;
    }

    private void OnRespawnNow()
    {
        if (_gameNet == null || !_isDead) return;
        _isWaiting = false;
        _respawnButton.Disabled = true;
        _waitButton.Disabled = true;
        _gameNet.SendRespawn();
    }

    private void OnWait()
    {
        if (!_isDead) return;
        _isWaiting = true;
        _waitTimer = WaitDuration;
        _timerLabel.Visible = true;
        _messageLabel.Text = "Aguardando revive...";
        _respawnButton.Disabled = true;
        _waitButton.Visible = false;
    }

    public override void _Process(double delta)
    {
        if (!_isWaiting || !_isDead) return;

        _waitTimer -= (float)delta;
        if (_waitTimer <= 0f)
        {
            _waitTimer = 0f;
            _isWaiting = false;
            if (_gameNet != null)
                _gameNet.SendRespawn();
            return;
        }

        int minutes = Mathf.FloorToInt(_waitTimer / 60f);
        int seconds = Mathf.FloorToInt(_waitTimer % 60f);
        _timerLabel.Text = $"Revive automatico em {minutes}:{seconds:D2}";
    }

    private void Centralizar()
    {
        Vector2 tela = GetViewportRect().Size;
        _panel.Position = (tela / 2) - (_panel.Size / 2);
    }
}
