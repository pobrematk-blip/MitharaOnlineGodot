using Godot;

public partial class PlayerHud : Control
{
    private ProgressBar _healthBar;
    private ProgressBar _manaBar;
    private ProgressBar _staminaBar;
    private Label _healthLabel;
    private Label _manaLabel;
    private Label _staminaLabel;
    private Label _nameLevelLabel;
    private Player _player;
    private LevelProgressionComponent _levelComp;

    public override void _Ready()
    {
        _healthBar = GetNode<ProgressBar>("Background/VBox/HealthBar");
        _manaBar = GetNode<ProgressBar>("Background/VBox/ManaBar");
        _staminaBar = GetNode<ProgressBar>("Background/VBox/StaminaBar");
        _healthLabel = GetNode<Label>("Background/VBox/HealthBar/HealthBarLabel");
        _manaLabel = GetNode<Label>("Background/VBox/ManaBar/ManaBarLabel");
        _staminaLabel = GetNode<Label>("Background/VBox/StaminaBar/StaminaBarLabel");
        _nameLevelLabel = GetNode<Label>("Background/VBox/NameLevelLabel");

        CallDeferred(nameof(ConnectPlayer));
    }

    private void ConnectPlayer()
    {
        _player = GetTree()?.CurrentScene?.FindChild("Player", true, false) as Player;
        if (_player == null)
        {
            GD.PrintErr("[PLAYER HUD] Player n\u00e3o encontrado!");
            return;
        }

        _player.StatusAtualizado += UpdateHud;

        ConectarProgressao();
        AtualizarInfoNivel();
        UpdateHud();
        GD.Print("[PLAYER HUD] Conectado ao Player.");
    }

    private void ConectarProgressao()
    {
        _levelComp = _player.FindChild("LevelProgressionComponent", true, false) as LevelProgressionComponent;
        if (_levelComp != null)
        {
            _levelComp.ProgressaoAtualizada += AtualizarInfoNivel;
        }
        else
        {
            GD.PrintErr("[PLAYER HUD] LevelProgressionComponent nao encontrado!");
        }
    }

    private void AtualizarInfoNivel()
    {
        if (_player == null) return;

        int nivel = _levelComp?.Nivel ?? 1;

        // Fallback: try to get level from GameNetwork _pendingLevel if component is not yet set
        if (nivel <= 1 && (GetNodeOrNull("/root/GameNetwork") is GameNetwork gn))
        {
            if (gn._pendingLevel > 1)
                nivel = gn._pendingLevel;
        }

        var escolhido = GetNodeOrNull<PersonagemEscolhido>("/root/PersonagemEscolhido");
        string nome = escolhido?.NomePersonagem ?? "Aventureiro";
        _nameLevelLabel.Text = $"{nome} | Nv. {nivel}";
    }

    private void UpdateHud()
    {
        if (_player == null) return;

        _healthBar.MaxValue = _player.MaxHealth;
        _healthBar.Value = _player.CurrentHealth;
        _manaBar.MaxValue = _player.MaxMana;
        _manaBar.Value = _player.CurrentMana;
        _staminaBar.MaxValue = _player.MaxStamina;
        _staminaBar.Value = _player.CurrentStamina;

        _healthLabel.Text = $"Vida: {_player.CurrentHealth}/{_player.MaxHealth}";
        _manaLabel.Text = $"Mana: {_player.CurrentMana}/{_player.MaxMana}";
        _staminaLabel.Text = $"Stamina: {_player.CurrentStamina}/{_player.MaxStamina}";
    }
}
