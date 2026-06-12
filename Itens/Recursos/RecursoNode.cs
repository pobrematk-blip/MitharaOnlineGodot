using Godot;

public partial class RecursoNode : StaticBody2D
{
    [Export] public RecursoResource RecursoData { get; set; }

    private int _faseAtual = 3;
    private Sprite2D _sprite;
    private Timer _timerCrescimento;
    private Label _labelNome;
    private Label _prompt;
    private Area2D _area;
    private bool _playerPerto;

    public int FaseAtual
    {
        get => _faseAtual;
        set
        {
            _faseAtual = Mathf.Clamp(value, 1, 3);
            AtualizarAparencia();
        }
    }

    public override void _Ready()
    {
        _sprite = new Sprite2D();
        _sprite.Name = "Sprite";
        AddChild(_sprite);

        var col = new CollisionShape2D();
        col.Shape = new CircleShape2D { Radius = 16f };
        AddChild(col);

        _area = new Area2D();
        _area.Name = "AreaInteracao";
        var areaShape = new CollisionShape2D();
        areaShape.Shape = new CircleShape2D { Radius = 50f };
        _area.AddChild(areaShape);
        AddChild(_area);

        _area.BodyEntered += OnBodyEntered;
        _area.BodyExited += OnBodyExited;

        _timerCrescimento = new Timer();
        _timerCrescimento.Name = "TimerCrescimento";
        _timerCrescimento.OneShot = true;
        _timerCrescimento.Timeout += AvancarFase;
        AddChild(_timerCrescimento);

        _labelNome = new Label();
        _labelNome.Name = "LabelNome";
        _labelNome.Position = new Vector2(-50, -40);
        _labelNome.AddThemeFontSizeOverride("font_size", 12);
        _labelNome.AddThemeColorOverride("font_color", Colors.White);
        _labelNome.AddThemeConstantOverride("shadow_offset_x", 1);
        _labelNome.AddThemeConstantOverride("shadow_offset_y", 1);
        _labelNome.AddThemeColorOverride("shadow_color", new Color(0, 0, 0, 0.8f));
        _labelNome.Text = RecursoData?.Nome ?? "Recurso";
        AddChild(_labelNome);

        _prompt = new Label();
        _prompt.Name = "Prompt";
        _prompt.Text = "[F] Coletar";
        _prompt.Position = new Vector2(-30, -60);
        _prompt.AddThemeFontSizeOverride("font_size", 16);
        _prompt.AddThemeColorOverride("font_color", new Color(1, 1, 0.3f));
        _prompt.AddThemeConstantOverride("shadow_offset_x", 1);
        _prompt.AddThemeConstantOverride("shadow_offset_y", 1);
        _prompt.AddThemeColorOverride("shadow_color", new Color(0, 0, 0, 0.8f));
        _prompt.Visible = false;
        AddChild(_prompt);

        FaseAtual = 3;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!_playerPerto) return;
        if (@event is InputEventKey key && key.Pressed && !key.Echo && key.Keycode == Key.F)
        {
            if (FaseAtual >= 3)
                Interagir();
            GetViewport().SetInputAsHandled();
        }
    }

    private void OnBodyEntered(Node body)
    {
        if (body is Player || body.IsInGroup("player"))
        {
            _playerPerto = true;
            _prompt.Visible = FaseAtual >= 3;
        }
    }

    private void OnBodyExited(Node body)
    {
        if (body is Player || body.IsInGroup("player"))
        {
            _playerPerto = false;
            _prompt.Visible = false;
        }
    }

    private void Interagir()
    {
        if (RecursoData == null || FaseAtual < 3) return;

        SoltarItem();

        FaseAtual = 1;
        _prompt.Visible = false;

        if (_timerCrescimento.IsStopped())
            _timerCrescimento.Start(RecursoData.TempoFase1Para2);
    }

    private void SoltarItem()
    {
        if (RecursoData.ItemDropID <= 0) return;

        var itemDB = GetNodeOrNull<ItemDatabase>("/root/ItemDatabase");
        if (itemDB == null) return;

        var item = itemDB.GetItem(RecursoData.ItemDropID);
        if (item == null) return;

        int qtd = RecursoData.QuantidadeMinima;
        if (RecursoData.QuantidadeMaxima > RecursoData.QuantidadeMinima)
            qtd += (int)(GD.Randi() % (RecursoData.QuantidadeMaxima - RecursoData.QuantidadeMinima + 1));

        var lootScene = ResourceLoader.Load<PackedScene>("res://resources/Inventario/ItemColetavel.tscn");
        if (lootScene == null) return;

        var loot = lootScene.Instantiate<ItemColetavel>();
        loot.ItemContido = item;
        loot.Position = GlobalPosition + new Vector2((float)(GD.Randi() % 40 - 20), (float)(GD.Randi() % 40 - 20));
        GetParent().AddChild(loot);
    }

    private void AvancarFase()
    {
        if (FaseAtual < 3)
        {
            FaseAtual++;
            if (FaseAtual < 3)
            {
                float tempo = FaseAtual == 1 ? RecursoData.TempoFase1Para2 : RecursoData.TempoFase2Para3;
                _timerCrescimento.Start(tempo);
            }
        }
    }

    private void AtualizarAparencia()
    {
        if (RecursoData == null || _sprite == null) return;

        Texture2D tex = _faseAtual switch
        {
            1 => RecursoData.TexturaFase1,
            2 => RecursoData.TexturaFase2,
            3 => RecursoData.TexturaFase3,
            _ => null
        };

        _sprite.Texture = tex;

        if (_playerPerto)
            _prompt.Visible = _faseAtual >= 3;
    }
}
