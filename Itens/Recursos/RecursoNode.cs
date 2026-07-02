using Godot;
using System.Linq;

public partial class RecursoNode : StaticBody2D
{
    [Export] public RecursoResource RecursoData { get; set; }

    [ExportGroup("Colisao")]
    [Export] public float CapsulaRaio { get; set; } = 21f;
    [Export] public float DistanciaInteracao { get; set; } = 120f;
    [Export] public Vector2 ColisaoBaseTamanho { get; set; } = new(34f, 22f);
    [Export] public float ColisaoBaseOffsetY { get; set; } = 50f;
    [Export] public float ColisaoExtraParaCima { get; set; } = 10f;

    private int _faseAtual = 5;
    private Sprite2D _sprite;
    private CollisionShape2D _colisao;
    private Godot.Timer _timerCrescimento;
    private Label _prompt;
    private bool _playerPerto;
    public int FaseAtual
    {
        get => _faseAtual;
        set
        {
            _faseAtual = Mathf.Clamp(value, 1, 5);
            AtualizarAparencia();
        }
    }

    public override void _Ready()
    {
        _sprite = new Sprite2D();
        _sprite.Name = "Sprite";
        AddChild(_sprite);

        _colisao = new CollisionShape2D();
        _colisao.Shape = new RectangleShape2D { Size = ColisaoBaseTamanho };
        _colisao.Position = new Vector2(0, ColisaoBaseOffsetY);
        AddChild(_colisao);

        _timerCrescimento = new Godot.Timer();
        _timerCrescimento.Name = "TimerCrescimento";
        _timerCrescimento.OneShot = true;
        _timerCrescimento.Timeout += AvancarFase;
        AddChild(_timerCrescimento);

        _prompt = new Label();
        _prompt.Name = "Prompt";
        _prompt.Text = "[F] Coletar";
        _prompt.HorizontalAlignment = HorizontalAlignment.Center;
        _prompt.Size = new Vector2(200, 26);
        _prompt.Position = new Vector2(-100, -260);
        _prompt.AddThemeFontSizeOverride("font_size", 18);
        _prompt.AddThemeColorOverride("font_color", new Color(1, 1, 0.3f));
        _prompt.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 1));
        _prompt.AddThemeConstantOverride("outline_size", 2);
        _prompt.AddThemeConstantOverride("shadow_offset_x", 1);
        _prompt.AddThemeConstantOverride("shadow_offset_y", 1);
        _prompt.AddThemeColorOverride("shadow_color", new Color(0, 0, 0, 0.8f));
        _prompt.Visible = false;
        AddChild(_prompt);

        FaseAtual = 5;
    }

    public override void _Process(double delta)
    {
        var player = GetTree().GetFirstNodeInGroup("player");
        if (player is Node2D p)
        {
            bool estavaPerto = _playerPerto;
            _playerPerto = GlobalPosition.DistanceTo(p.GlobalPosition) <= DistanciaInteracao;
            if (_playerPerto != estavaPerto)
            {
                _prompt.Visible = _playerPerto && FaseAtual >= 5;
            }

            bool playerAcima = p.GlobalPosition.Y < GlobalPosition.Y;
            ZIndex = playerAcima ? 1 : 0;
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!_playerPerto) return;
        if (@event is InputEventKey key && key.Pressed && !key.Echo && key.Keycode == Key.F)
        {
            if (FaseAtual >= 5)
                Interagir();
            GetViewport().SetInputAsHandled();
        }
    }

    private void Interagir()
    {
        if (RecursoData == null || FaseAtual < 5) return;

        MensagemSistema("Coleta de recursos local bloqueada. Recursos devem ser validados pelo servidor.");
        bool localResourceGatherEnabled = false;
        if (!localResourceGatherEnabled)
            return;

        if (!TemFerramentaEquipada())
            return;

        int qtd = SoltarItem();
        if (qtd > 0)
        {
            var itemDB = GetNodeOrNull<ItemDatabase>("/root/ItemDatabase");
            string nomeItem = itemDB?.GetItemName(RecursoData.ItemDropID) ?? "Item";
            MensagemSistema($"Você coletou {nomeItem} x{qtd}!");
        }

        FaseAtual = 1;
        _prompt.Visible = false;

        if (_timerCrescimento.IsStopped())
            _timerCrescimento.Start(RecursoData.TempoFase1Para2);
    }

    private bool TemFerramentaEquipada()
    {
        var ferramenta = RecursoData.FerramentaNecessaria;
        if (ferramenta == null || ferramenta.ItemID <= 0)
            return true;

        var player = GetTree().GetFirstNodeInGroup("player");
        var equip = player?.FindChild("EquipamentoComponent", true, false) as EquipamentoComponent;
        if (equip == null)
            return true;

        bool temFerramenta = equip.ItensEquipados.Values
            .Any(s => s?.Item?.ItemID == ferramenta.ItemID);

        if (!temFerramenta)
            MensagemSistema($"Você precisa de [{ferramenta.Nome}] equipado para coletar {RecursoData.Nome}!");

        return temFerramenta;
    }

    private void MensagemSistema(string texto)
    {
        var chat = GetNodeOrNull<ChatUI>("/root/main/HUD/ChatUI");
        chat?.AddSystemMessage(texto);
    }

    private int SoltarItem()
    {
        GD.PrintErr("[RECURSO] SoltarItem local bloqueado. Drops devem vir do servidor.");
        bool localResourceDropsEnabled = false;
        if (!localResourceDropsEnabled)
            return 0;

        if (RecursoData.ItemDropID <= 0) return 0;

        var itemDB = GetNodeOrNull<ItemDatabase>("/root/ItemDatabase");
        if (itemDB == null) return 0;

        var item = itemDB.GetItem(RecursoData.ItemDropID);
        if (item == null) return 0;

        int qtd = RecursoData.QuantidadeMinima;
        if (RecursoData.QuantidadeMaxima > RecursoData.QuantidadeMinima)
            qtd += (int)(GD.Randi() % (RecursoData.QuantidadeMaxima - RecursoData.QuantidadeMinima + 1));

        var lootScene = ResourceLoader.Load<PackedScene>("res://Itens/Outros/ItemColetavel.tscn");
        if (lootScene == null) return 0;

        var loot = lootScene.Instantiate<ItemColetavel>();
        loot.ItemContido = item;
        loot.Position = GlobalPosition + new Vector2((float)(GD.Randi() % 40 - 20), (float)(GD.Randi() % 40 - 20));
        GetParent().AddChild(loot);

        return qtd;
    }

    private void AvancarFase()
    {
        if (FaseAtual < 5)
        {
            FaseAtual++;
            if (FaseAtual < 5)
            {
                float tempo = FaseAtual switch
                {
                    1 => RecursoData.TempoFase1Para2,
                    2 => RecursoData.TempoFase2Para3,
                    3 => RecursoData.TempoFase3Para4,
                    4 => RecursoData.TempoFase4Para5,
                    _ => 0f
                };
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
            4 => RecursoData.TexturaFase4,
            5 => RecursoData.TexturaFase5,
            _ => null
        };

        _sprite.Texture = tex;
        _sprite.Scale = RecursoData?.Scale ?? Vector2.One;

        AtualizarColisaoBase(tex);

        if (_playerPerto)
            _prompt.Visible = _faseAtual >= 5;
    }

    private void AtualizarColisaoBase(Texture2D textura)
    {
        if (_colisao == null)
            return;

        if (_faseAtual == 1)
        {
            _colisao.Shape = new RectangleShape2D { Size = new Vector2(34f, 18f) };
            _colisao.Position = new Vector2(0, 10f);
            return;
        }

        Vector2 escala = RecursoData?.Scale ?? Vector2.One;
        float alturaRenderizada = textura != null ? textura.GetHeight() * escala.Y : 96f;
        float baseY = Mathf.Max(18f, (alturaRenderizada * 0.5f) - 18f);

        float extraTopo = Mathf.Max(0f, ColisaoExtraParaCima);
        var tamanho = new Vector2(ColisaoBaseTamanho.X, ColisaoBaseTamanho.Y + extraTopo);
        float centroY = Mathf.Max(ColisaoBaseOffsetY, baseY) - (extraTopo * 0.5f);

        _colisao.Shape = new RectangleShape2D { Size = tamanho };
        _colisao.Position = new Vector2(0, centroY);
    }
}
