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
    private Panel _xpBg;
    private Panel _xpFill;
    private Label _guildLabel;
    private Player _player;
    private Camera2D _camera;

    private string _nomePersonagem;
    private string _guildTag = "";
    private string _guildName = "";
    private int _guildEmblemIdx = -1;
    private TextureRect _emblemaIcon;
    private bool _remoteMode;
    private long _xpAtual;
    private long _xpMaximo = 1;

    private const float BarraLargura = 80f;
    private const float BarraAltura = 6f;
    private const int RaioCanto = 3;

    private static readonly string[] FALLBACK_EMBLEMS = {
        "12.png","13.png","14.png","17.png","18.png","19.png","2.png","20.png","21.png","22.png",
        "2250.png","2256.png","2266.png","2279.png","2280.png","23.png","2307.png","2311.png",
        "2314.png","2315.png","2330.png","2338.png","2340.png","2341.png","2347.png","2353.png",
        "2354.png","2355.png","2366.png","2367.png","25.png","2544.png","2545.png","2583.png",
        "2592.png","2594.png","26.png","2608.png","2635.png","2646.png","2649.png","2656.png",
        "27.png","28.png","29.png","3.png","30.png","33.png","35.png","36.png","37.png","39.png",
        "4.png","40.png","41.png","42.png","43.png","44.png","45.png","46.png","47.png","49.png",
        "51.png","7.png","8.png",
    };

    private static readonly string[] EmblemFiles = CarregarEmblemas();

    private static string[] CarregarEmblemas()
    {
        var dir = DirAccess.Open("res://Itens/Emblema de Guild/");
        if (dir == null) return FALLBACK_EMBLEMS;
        var files = new System.Collections.Generic.List<string>();
        dir.ListDirBegin();
        string file = dir.GetNext();
        while (!string.IsNullOrEmpty(file))
        {
            if (file.EndsWith(".png"))
                files.Add(file);
            file = dir.GetNext();
        }
        dir.ListDirEnd();
        if (files.Count == 0) return FALLBACK_EMBLEMS;
        files.Sort();
        return files.ToArray();
    }

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
        if (!_remoteMode)
            _camera = GetViewport().GetCamera2D();

        if (!_remoteMode)
            _player = GetTree().CurrentScene.FindChild("Player", true, false) as Player;

        _nomeLabel = new Label();
        _nomeLabel.Size = new Vector2(120, 20);
        _nomeLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _nomeLabel.AddThemeFontSizeOverride("font_size", 13);
        _nomeLabel.AddThemeColorOverride("font_color", Colors.White);
        _nomeLabel.Visible = MostrarNome;
        AddChild(_nomeLabel);

        _guildLabel = new Label
        {
            Position = new Vector2(0, 17),
            Size = new Vector2(120, 18),
            HorizontalAlignment = HorizontalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        _guildLabel.AddThemeFontSizeOverride("font_size", 11);
        _guildLabel.AddThemeColorOverride("font_color", new Color(0.35f, 0.8f, 1f));
        AddChild(_guildLabel);

        _emblemaIcon = new TextureRect();
        _emblemaIcon.Position = new Vector2(2, 1);
        _emblemaIcon.Size = new Vector2(20, 20);
        _emblemaIcon.ExpandMode = TextureRect.ExpandModeEnum.FitWidth;
        _emblemaIcon.StretchMode = TextureRect.StretchModeEnum.KeepAspect;
        _emblemaIcon.Visible = false;
        AddChild(_emblemaIcon);

        CriarBarra(new Color(0.85f, 0.15f, 0.15f), new Vector2(20, 35), out _hpBg, out _hpFill);
        _hpBg.Visible = MostrarBarraVida;
        AddChild(_hpBg);

        CriarBarra(new Color(0.1f, 0.3f, 0.9f), new Vector2(20, 42), out _manaBg, out _manaFill);
        _manaBg.Visible = MostrarBarraMana;
        AddChild(_manaBg);

        if (!_remoteMode)
        {
            var escolhido = GetNodeOrNull<PersonagemEscolhido>("/root/PersonagemEscolhido");
            _nomePersonagem = escolhido?.NomePersonagem ?? "Aventureiro";
        }

        CarregarDadosGuild();
        AtualizarNomeCompleto();

        if (_player != null)
        {
            _player.StatusAtualizado += Atualizar;
            Atualizar();
        }
        else
        {
            AtualizarXp();
        }
    }

    public void ConfigurarRemoto(string nome, string guildName, string guildTag, int guildEmblem, long xp, long xpMax)
    {
        _remoteMode = true;
        _nomePersonagem = nome;
        AtualizarDadosRemotos(nome, guildName, guildTag, guildEmblem, xp, xpMax);
    }

    public void AtualizarDadosRemotos(string nome, string guildName, string guildTag, int guildEmblem, long xp, long xpMax)
    {
        _nomePersonagem = nome;
        _guildName = guildName;
        _guildTag = guildTag;
        _guildEmblemIdx = guildEmblem;
        _xpAtual = System.Math.Max(0, xp);
        _xpMaximo = System.Math.Max(1, xpMax);
        if (_nomeLabel != null)
            AtualizarNomeCompleto();
        AtualizarXp();
    }

    public void RecarregarDadosGuild()
    {
        CarregarDadosGuild();
        AtualizarNomeCompleto();
    }

    private void CarregarDadosGuild()
    {
        var gameNet = GetNodeOrNull<GameNetwork>("/root/GameNetwork");
        if (gameNet == null) return;
        _guildName = gameNet.GuildName;
        _guildTag = gameNet.GuildTag;
        _guildEmblemIdx = gameNet.GuildEmblem;
    }

    private void AtualizarNomeCompleto()
    {
        string nome = _nomePersonagem;

        if (_mostrarTagGuild && !string.IsNullOrEmpty(_guildTag))
            nome = $"[{_guildTag}] {nome}";

        if (_mostrarEmblemaGuild && _guildEmblemIdx >= 0 && _guildEmblemIdx < EmblemFiles.Length)
        {
            string path = "res://Itens/Emblema de Guild/" + EmblemFiles[_guildEmblemIdx];
            var tex = ResourceLoader.Load<Texture2D>(path);
            if (tex != null)
            {
                _emblemaIcon.Texture = tex;
                _emblemaIcon.Visible = true;
            }
        }
        else
        {
            _emblemaIcon.Visible = false;
        }

        _nomeLabel.Text = nome;
        if (_guildLabel != null)
        {
            _guildLabel.Text = _mostrarTagGuild ? _guildName : "";
            _guildLabel.Visible = !string.IsNullOrWhiteSpace(_guildLabel.Text);
        }
    }

    public override void _Process(double delta)
    {
        if (_remoteMode) return;
        if (_player == null) return;

        if (_camera == null)
            _camera = GetViewport().GetCamera2D();

        if (_camera == null) return;

        Vector2 screenPos = (_player.GlobalPosition - _camera.GlobalPosition) * _camera.Zoom + GetViewportRect().Size / 2;
        Position = screenPos + new Vector2(-60, -75);
    }

    private void Atualizar()
    {
        if (_player == null) return;

        float hpPct = Mathf.Clamp(_player.CurrentHealth / (float)_player.MaxHealth, 0, 1);
        float manaPct = Mathf.Clamp(_player.CurrentMana / (float)_player.MaxMana, 0, 1);
        _hpFill.Size = new Vector2(BarraLargura * hpPct, BarraAltura);
        _manaFill.Size = new Vector2(BarraLargura * manaPct, BarraAltura);
    }

    private void AtualizarXp()
    {
        if (_xpFill == null) return;
        float xpPct = Mathf.Clamp(_xpAtual / (float)System.Math.Max(1, _xpMaximo), 0, 1);
        _xpFill.Size = new Vector2(BarraLargura * xpPct, BarraAltura);
    }
}
