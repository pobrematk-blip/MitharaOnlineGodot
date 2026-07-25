using Godot;

using System.Globalization;
using System.Text;

public partial class PlayerHud : Control
{
    private ProgressBar _healthBar;
    private ProgressBar _shieldBar;
    private ProgressBar _manaBar;
    private ProgressBar _staminaBar;
    private Label _healthLabel;
    private Label _manaLabel;
    private Label _staminaLabel;
    private Label _nameLevelLabel;
    private TextureRect _portraitIcon;
    private Player _player;
    private LevelProgressionComponent _levelComp;

    public override void _Ready()
    {
        _healthBar = GetNode<ProgressBar>("Background/MainHBox/VBox/HealthBar");
        _manaBar = GetNode<ProgressBar>("Background/MainHBox/VBox/ManaBar");
        _staminaBar = GetNode<ProgressBar>("Background/MainHBox/VBox/StaminaBar");
        _healthLabel = GetNode<Label>("Background/MainHBox/VBox/HealthBar/HealthBarLabel");
        _manaLabel = GetNode<Label>("Background/MainHBox/VBox/ManaBar/ManaBarLabel");
        _staminaLabel = GetNode<Label>("Background/MainHBox/VBox/StaminaBar/StaminaBarLabel");
        _nameLevelLabel = GetNode<Label>("Background/MainHBox/VBox/NameLevelLabel");
        _portraitIcon = GetNode<TextureRect>("Background/MainHBox/PortraitPanel/PortraitIcon");
        _portraitIcon.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
        _portraitIcon.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
        _portraitIcon.CustomMinimumSize = Vector2.Zero;
        CriarBarraEscudo();

        CallDeferred(nameof(ConnectPlayer));
    }

    private void CriarBarraEscudo()
    {
        _shieldBar = new ProgressBar
        {
            Name = "ShieldBar",
            ShowPercentage = false,
            MouseFilter = MouseFilterEnum.Ignore,
            MaxValue = 100,
            Value = 0,
        };
        _shieldBar.SetAnchorsPreset(LayoutPreset.FullRect);
        _shieldBar.GrowHorizontal = GrowDirection.Both;
        _shieldBar.GrowVertical = GrowDirection.Both;

        var empty = new StyleBoxEmpty();
        var fill = new StyleBoxFlat
        {
            BgColor = new Color(0.92f, 0.98f, 1f, 0.82f),
            CornerRadiusTopLeft = 4,
            CornerRadiusTopRight = 4,
            CornerRadiusBottomLeft = 4,
            CornerRadiusBottomRight = 4,
        };

        _shieldBar.AddThemeStyleboxOverride("background", empty);
        _shieldBar.AddThemeStyleboxOverride("fill", fill);
        _healthBar.AddChild(_shieldBar);
        _healthLabel.MoveToFront();
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
        CarregarIconeClasse();
        UpdateHud();
        GD.Print("[PLAYER HUD] Conectado ao Player.");
    }

    private void CarregarIconeClasse()
    {
        var escolhido = GetNodeOrNull<PersonagemEscolhido>("/root/PersonagemEscolhido");
        string classe = escolhido?.ClasseBase?.NomeClasse ?? "";
        int itemId = ObterItemClasseNivel100(classe);

        if (itemId > 0 && GetNodeOrNull<ItemDatabase>("/root/ItemDatabase") is ItemDatabase itemDb)
        {
            var item = itemDb.GetItem(itemId);
            if (item?.Icone != null)
            {
                _portraitIcon.Texture = item.Icone;
                return;
            }
        }

        string iconPath = ObterIconeClasseFallback(classe);
        if (!string.IsNullOrEmpty(iconPath) && ResourceLoader.Exists(iconPath))
            _portraitIcon.Texture = ResourceLoader.Load<Texture2D>(iconPath);
    }

    private static int ObterItemClasseNivel100(string classe)
    {
        string normalizada = NormalizarClasse(classe);
        return normalizada switch
        {
            "arqueiro" or "cacador" or "sniper" or "ranger" => 1010,
            "assassino" or "assasino" or "ladino" or "sombra" => 1021,
            "berserker" or "berseker" or "barbaro" => 1043,
            "guardiao" or "protetor" or "tank" or "tankudo" => 1065,
            "mago" or "elementalista" => 1076,
            "clerigo" or "sacerdote" or "healer" or "curandeiro" => 1087,
            _ => 0,
        };
    }

    private static string ObterIconeClasseFallback(string classe)
    {
        string normalizada = NormalizarClasse(classe);
        return normalizada switch
        {
            "arqueiro" or "cacador" or "sniper" or "ranger" => "res://Itens/Incones/Arco 1.png",
            "assassino" or "assasino" or "ladino" or "sombra" => "res://Itens/Incones/Adaga 1.png",
            "berserker" or "berseker" or "barbaro" => "res://Itens/Incones/Machados Perdisos 2.png",
            "mago" => "res://Itens/Incones/Cajado 6.png",
            "clerigo" => "res://Itens/Incones/Martelo quebrada.png",
            "guardiao" or "protetor" or "tank" or "tankudo" => "res://Itens/Incones/Escudo de Goglin.png",
            _ => "res://Itens/Incones/Bag 3.png",
        };
    }

    private static string NormalizarClasse(string classe)
    {
        if (string.IsNullOrWhiteSpace(classe))
            return "";

        string decomposed = classe.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (char c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                builder.Append(c);
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
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
            GD.PrintErr("[PLAYER HUD] LevelProgressionComponent n?o encontrado!");
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
        _shieldBar.MaxValue = _player.MaxHealth;
        _shieldBar.Value = Mathf.Min(_player.CurrentArcaneShield, _player.MaxHealth);
        _shieldBar.Visible = _player.CurrentArcaneShield > 0;
        _manaBar.MaxValue = _player.MaxMana;
        _manaBar.Value = _player.CurrentMana;
        _staminaBar.MaxValue = _player.MaxStamina;
        _staminaBar.Value = _player.CurrentStamina;

        _healthLabel.Text = $"Vida: {_player.CurrentHealth}/{_player.MaxHealth}";
        _manaLabel.Text = $"Mana: {_player.CurrentMana}/{_player.MaxMana}";
        _staminaLabel.Text = $"Stamina: {_player.CurrentStamina}/{_player.MaxStamina}";
    }
}
