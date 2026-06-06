using Godot;

public partial class OverheadUI : Control
{
    private bool _mostrarNome = true;
    private bool _mostrarBarraVida = true;
    private bool _mostrarBarraMana = true;

    [Export]
    public bool MostrarNome
    {
        get => _mostrarNome;
        set
        {
            _mostrarNome = value;
            if (_nomeLabel != null) _nomeLabel.Visible = value;
        }
    }

    [Export]
    public bool MostrarBarraVida
    {
        get => _mostrarBarraVida;
        set
        {
            _mostrarBarraVida = value;
            if (_hpBg != null) _hpBg.Visible = value;
        }
    }

    [Export]
    public bool MostrarBarraMana
    {
        get => _mostrarBarraMana;
        set
        {
            _mostrarBarraMana = value;
            if (_manaBg != null) _manaBg.Visible = value;
        }
    }

    private Label _nomeLabel;
    private ColorRect _hpBg;
    private ColorRect _hpFill;
    private ColorRect _manaBg;
    private ColorRect _manaFill;
    private Player _player;
    private Camera2D _camera;

    private static ColorRect CriarBarra(Color cor, Vector2 pos)
    {
        var bg = new ColorRect();
        bg.Position = pos;
        bg.Size = new Vector2(100, 10);
        bg.Color = new Color(0, 0, 0, 0.35f);

        var fill = new ColorRect();
        fill.Position = Vector2.Zero;
        fill.Size = new Vector2(100, 10);
        fill.Color = cor;
        bg.AddChild(fill);

        return bg;
    }

    public override void _Ready()
    {
        _camera = GetViewport().GetCamera2D();

        _player = GetTree().CurrentScene.FindChild("Player", true, false) as Player;

        _nomeLabel = new Label();
        _nomeLabel.Size = new Vector2(100, 20);
        _nomeLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _nomeLabel.AddThemeFontSizeOverride("font_size", 14);
        _nomeLabel.AddThemeColorOverride("font_color", Colors.White);
        _nomeLabel.Visible = MostrarNome;
        AddChild(_nomeLabel);

        _hpBg = CriarBarra(new Color(0.8f, 0.1f, 0.1f), new Vector2(0, 20));
        _hpFill = _hpBg.GetChild<ColorRect>(0);
        _hpBg.Visible = MostrarBarraVida;
        AddChild(_hpBg);

        _manaBg = CriarBarra(new Color(0.1f, 0.3f, 0.9f), new Vector2(0, 31));
        _manaFill = _manaBg.GetChild<ColorRect>(0);
        _manaBg.Visible = MostrarBarraMana;
        AddChild(_manaBg);

        var escolhido = GetNodeOrNull<PersonagemEscolhido>("/root/PersonagemEscolhido");
        _nomeLabel.Text = escolhido?.NomePersonagem ?? "Aventureiro";

        if (_player != null)
        {
            _player.StatusAtualizado += Atualizar;
            Atualizar();
        }
    }

    public override void _Process(double delta)
    {
        if (_player == null) return;

        if (_camera == null)
            _camera = GetViewport().GetCamera2D();

        if (_camera == null) return;

        Vector2 screenPos = (_player.GlobalPosition - _camera.GlobalPosition) * _camera.Zoom + GetViewportRect().Size / 2;
        Position = screenPos + new Vector2(-50, -75);
    }

    private void Atualizar()
    {
        if (_player == null) return;

        float hpPct = Mathf.Clamp(_player.CurrentHealth / (float)_player.MaxHealth, 0, 1);
        float manaPct = Mathf.Clamp(_player.CurrentMana / (float)_player.MaxMana, 0, 1);
        _hpFill.Size = new Vector2(100 * hpPct, 10);
        _manaFill.Size = new Vector2(100 * manaPct, 10);
    }
}
