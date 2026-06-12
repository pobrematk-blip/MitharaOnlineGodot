using Godot;

public partial class OverheadUI : Control
{
    private bool _mostrarNome = true;
    private bool _mostrarBarraVida = true;
    private bool _mostrarBarraMana = true;
    private bool _mostrarTagGuild = true;
    private bool _mostrarEmblemaGuild = true;

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

    public bool MostrarTagGuild
    {
        get => _mostrarTagGuild;
        set
        {
            _mostrarTagGuild = value;
            AtualizarNomeCompleto();
        }
    }

    public bool MostrarEmblemaGuild
    {
        get => _mostrarEmblemaGuild;
        set
        {
            _mostrarEmblemaGuild = value;
            AtualizarNomeCompleto();
        }
    }

    private Label _nomeLabel;
    private Panel _hpBg;
    private Panel _hpFill;
    private Panel _manaBg;
    private Panel _manaFill;
    private Player _player;
    private Camera2D _camera;

    private string _nomePersonagem;
    private string _guildTag = "";
    private int _guildEmblemIdx = -1;

    private const float BarraLargura = 80f;
    private const float BarraAltura = 6f;
    private const int RaioCanto = 3;

    private static readonly Color[] EmblemCores = {
        Colors.Red, Colors.Blue, Colors.Green, Colors.Yellow,
        Colors.Purple, Colors.Orange, Colors.Cyan, Colors.Pink,
        Colors.Brown, Colors.White
    };

    private static StyleBoxFlat CriarEstilo(Color cor, bool bg)
    {
        var style = new StyleBoxFlat();
        style.CornerRadiusTopLeft = RaioCanto;
        style.CornerRadiusTopRight = RaioCanto;
        style.CornerRadiusBottomLeft = RaioCanto;
        style.CornerRadiusBottomRight = RaioCanto;
        style.BgColor = bg ? new Color(cor.R, cor.G, cor.B, 0.35f) : cor;
        return style;
    }

    private static Panel CriarPainelBarra(Vector2 pos)
    {
        var panel = new Panel();
        panel.Position = pos;
        panel.Size = new Vector2(BarraLargura, BarraAltura);
        panel.MouseFilter = MouseFilterEnum.Ignore;
        return panel;
    }

    private void CriarBarra(Color cor, Vector2 pos, out Panel bg, out Panel fill)
    {
        bg = CriarPainelBarra(pos);
        bg.AddThemeStyleboxOverride("panel", CriarEstilo(cor, true));

        fill = new Panel();
        fill.Position = Vector2.Zero;
        fill.Size = new Vector2(BarraLargura, BarraAltura);
        fill.MouseFilter = MouseFilterEnum.Ignore;
        fill.AddThemeStyleboxOverride("panel", CriarEstilo(cor, false));
        bg.AddChild(fill);
    }

    public override void _Ready()
    {
        _camera = GetViewport().GetCamera2D();

        _player = GetTree().CurrentScene.FindChild("Player", true, false) as Player;

        _nomeLabel = new Label();
        _nomeLabel.Size = new Vector2(120, 20);
        _nomeLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _nomeLabel.AddThemeFontSizeOverride("font_size", 13);
        _nomeLabel.AddThemeColorOverride("font_color", Colors.White);
        _nomeLabel.Visible = MostrarNome;
        AddChild(_nomeLabel);

        CriarBarra(new Color(0.85f, 0.15f, 0.15f), new Vector2(10, 20), out _hpBg, out _hpFill);
        _hpBg.Visible = MostrarBarraVida;
        AddChild(_hpBg);

        CriarBarra(new Color(0.1f, 0.3f, 0.9f), new Vector2(10, 27), out _manaBg, out _manaFill);
        _manaBg.Visible = MostrarBarraMana;
        AddChild(_manaBg);

        var escolhido = GetNodeOrNull<PersonagemEscolhido>("/root/PersonagemEscolhido");
        _nomePersonagem = escolhido?.NomePersonagem ?? "Aventureiro";

        CarregarDadosGuild();
        AtualizarNomeCompleto();

        if (_player != null)
        {
            _player.StatusAtualizado += Atualizar;
            Atualizar();
        }
    }

    public void RecarregarDadosGuild()
    {
        CarregarDadosGuild();
        AtualizarNomeCompleto();
    }

    private void CarregarDadosGuild()
    {
        var cfg = new ConfigFile();
        if (cfg.Load("user://guild_data.cfg") != Error.Ok) return;
        _guildTag = cfg.GetValue("Guild", "tag", "").AsString();
        _guildEmblemIdx = cfg.GetValue("Guild", "emblem_index", -1).AsInt32();
    }

    private void AtualizarNomeCompleto()
    {
        string nome = _nomePersonagem;

        if (_mostrarTagGuild && !string.IsNullOrEmpty(_guildTag))
            nome = $"[{_guildTag}] {nome}";

        if (_mostrarEmblemaGuild && _guildEmblemIdx >= 0 && _guildEmblemIdx < EmblemCores.Length)
        {
            string simbolo = _guildEmblemIdx switch
            {
                0 => "🔴", 1 => "🔵", 2 => "🟢", 3 => "🟡",
                4 => "🟣", 5 => "🟠", 6 => "🩵", 7 => "🩷",
                8 => "🟤", 9 => "⬜",
                _ => "⬛"
            };
            nome = $"{simbolo} {nome}";
        }

        _nomeLabel.Text = nome;
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
        _hpFill.Size = new Vector2(BarraLargura * hpPct, BarraAltura);
        _manaFill.Size = new Vector2(BarraLargura * manaPct, BarraAltura);
    }
}
