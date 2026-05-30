using Godot;
using System;

public partial class PlayerHud : Control
{
    private ProgressBar _healthBar;
    private ProgressBar _manaBar;
    private Label _healthLabel;
    private Label _manaLabel;
    private Player _player;

    public override void _Ready()
    {
        _healthBar = GetNode<ProgressBar>("Background/VBox/HealthBar");
        _manaBar = GetNode<ProgressBar>("Background/VBox/ManaBar");
        _healthLabel = GetNode<Label>("Background/VBox/HealthBarLabel");
        _manaLabel = GetNode<Label>("Background/VBox/ManaBarLabel");

        CallDeferred(nameof(ConnectPlayer));
    }

    private void ConnectPlayer()
    {
        _player = GetTree()?.CurrentScene?.FindChild("Player", true, false) as Player;
        if (_player == null)
        {
            GD.PrintErr("[PLAYER HUD] Player não encontrado para conectar a barra de vida!");
            return;
        }

        _player.StatusAtualizado += UpdateHud;
        UpdateHud();
        GD.Print("[PLAYER HUD] ✅ Barra de vida/mana conectada ao Player.");
    }

    private void UpdateHud()
    {
        if (_player == null) return;

        _healthBar.MaxValue = _player.MaxHealth;
        _healthBar.Value = _player.CurrentHealth;
        _manaBar.MaxValue = _player.MaxMana;
        _manaBar.Value = _player.CurrentMana;

        _healthLabel.Text = $"Vida: {_player.CurrentHealth}/{_player.MaxHealth}";
        _manaLabel.Text = $"Mana: {_player.CurrentMana}/{_player.MaxMana}";
    }
}
