using Godot;

public partial class LojaCashUI : Control
{
    private Label _diamantesLabel;
    private Button _fecharBtn;
    private Control _feedback;
    private Label _feedbackLabel;
    private ScrollContainer _itensContainer;
    private GridContainer _gridContainer;
    private CashManager _cash;
    private LojaCashData _lojaData;
    private GameNetwork _net;
    private Label _tituloLabel;
    private bool _arrastando;
    private Vector2 _pontoCliqueOriginal;
    private Texture2D _coinIcon;

    private static readonly string LojaDataPath = "res://SistemaContas/LojaCashData.tres";
    private static readonly string CoinIconPath = "res://Itens/Incones/loja de cash.png";

    public override void _Ready()
    {
        _diamantesLabel = GetNode<Label>("%DiamantesLabel");
        _fecharBtn = GetNode<Button>("%FecharBtn");
        _feedback = GetNode<Control>("%Feedback");
        _feedbackLabel = GetNode<Label>("%FeedbackLabel");
        _itensContainer = GetNode<ScrollContainer>("%ItensContainer");
        _gridContainer = GetNode<GridContainer>("%Grid");
        _tituloLabel = GetNode<Label>("%TituloLabel");

        _cash = GetNode<CashManager>("/root/CashManager");
        _net = GetNodeOrNull<GameNetwork>("/root/GameNetwork");
        _coinIcon = ResourceLoader.Load<Texture2D>(CoinIconPath);

        _fecharBtn.Pressed += OnFechar;
        _tituloLabel.GuiInput += OnTituloGuiInput;

        if (_net != null)
            _net.Connect(GameNetwork.SignalName.OnCashShopResult, Callable.From((bool success, string message) => OnCashShopResult(success, message)));

        SubstituirDiamantePorIcone();
        Centralizar();
        CarregarDados();
        AtualizarUI();
    }

    private void SubstituirDiamantePorIcone()
    {
        if (_coinIcon == null) return;
        var topo = _diamantesLabel.GetParent() as HBoxContainer;
        if (topo == null) return;

        var icon = new TextureRect();
        icon.Texture = _coinIcon;
        icon.CustomMinimumSize = new Vector2(32, 32);
        icon.Size = new Vector2(32, 32);
        icon.ExpandMode = TextureRect.ExpandModeEnum.FitWidth;
        icon.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
        icon.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
        icon.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        topo.AddChild(icon);
        topo.MoveChild(icon, _diamantesLabel.GetIndex());

        _diamantesLabel.Text = $"{_cash?.Diamantes ?? 0}";
    }

    private void Centralizar()
    {
        Vector2 tela = GetViewportRect().Size;
        Vector2 desired = (tela / 2) - (Size / 2) + new Vector2(48f, 0f);
        Position = new Vector2(
            Mathf.Clamp(desired.X, 8f, Mathf.Max(8f, tela.X - Size.X - 8f)),
            Mathf.Clamp(desired.Y, 8f, Mathf.Max(8f, tela.Y - Size.Y - 8f))
        );
    }

    private void OnTituloGuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mouseEvent && mouseEvent.ButtonIndex == MouseButton.Left)
        {
            _arrastando = mouseEvent.Pressed;
            if (mouseEvent.Pressed) _pontoCliqueOriginal = mouseEvent.Position;
        }
        else if (@event is InputEventMouseMotion mouseMotion && _arrastando)
        {
            Position += mouseMotion.Position - _pontoCliqueOriginal;
        }
    }

    private void CarregarDados()
    {
        if (ResourceLoader.Exists(LojaDataPath))
            _lojaData = ResourceLoader.Load<LojaCashData>(LojaDataPath);

        if (_lojaData == null)
            _lojaData = new LojaCashData();

        PopularItens();
    }

    private void PopularItens()
    {
        foreach (var child in _gridContainer.GetChildren())
            child.QueueFree();

        var itemDB = GetNodeOrNull<ItemDatabase>("/root/GameNetwork/ItemDatabase");

        foreach (var entry in _lojaData.Itens)
        {
            var card = new PanelContainer();
            var style = new StyleBoxFlat();
            style.BgColor = new Color(0.1f, 0.11f, 0.15f, 0.94f);
            style.BorderWidthLeft = 1;
            style.BorderWidthTop = 1;
            style.BorderWidthRight = 1;
            style.BorderWidthBottom = 1;
            style.BorderColor = new Color(0.3f, 0.35f, 0.5f, 1);
            style.CornerRadiusTopLeft = 8;
            style.CornerRadiusTopRight = 8;
            style.CornerRadiusBottomLeft = 8;
            style.CornerRadiusBottomRight = 8;
            card.AddThemeStyleboxOverride("panel", style);
            card.SizeFlagsHorizontal = SizeFlags.ExpandFill;

            var vbox = new VBoxContainer();
            vbox.CustomMinimumSize = new Vector2(0, 160);
            vbox.AddThemeConstantOverride("separation", 4);
            vbox.ThemeTypeVariation = "VBoxContainer";
            card.AddChild(vbox);

            string nome = !string.IsNullOrEmpty(entry.NomeExibicao) ? entry.NomeExibicao : $"Item #{entry.ItemID}";
            Texture2D itemIcon = null;
            if (itemDB != null)
            {
                var item = itemDB.GetItem(entry.ItemID);
                if (item != null)
                {
                    if (string.IsNullOrEmpty(entry.NomeExibicao))
                        nome = item.Nome;
                    itemIcon = item.Icone;
                }
            }
            itemIcon ??= CarregarIconeFallback(entry.ItemID);

            var nomeLabel = new Label();
            nomeLabel.Text = nome;
            nomeLabel.AddThemeFontSizeOverride("font_size", 12);
            nomeLabel.AddThemeColorOverride("font_color", new Color(0.85f, 0.85f, 0.95f));
            nomeLabel.HorizontalAlignment = HorizontalAlignment.Center;
            nomeLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            nomeLabel.MaxLinesVisible = 2;
            vbox.AddChild(nomeLabel);

            var iconContainer = new CenterContainer();
            iconContainer.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            iconContainer.SizeFlagsVertical = SizeFlags.ExpandFill;
            iconContainer.CustomMinimumSize = new Vector2(72, 72);
            vbox.AddChild(iconContainer);

            var iconRect = new TextureRect();
            iconRect.Texture = itemIcon;
            iconRect.CustomMinimumSize = new Vector2(62, 62);
            iconRect.ExpandMode = TextureRect.ExpandModeEnum.FitWidth;
            iconRect.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
            iconContainer.AddChild(iconRect);

            var precoContainer = new HBoxContainer();
            precoContainer.Alignment = BoxContainer.AlignmentMode.Center;
            precoContainer.AddThemeConstantOverride("separation", 4);
            vbox.AddChild(precoContainer);

            var coinIconRect = new TextureRect();
            coinIconRect.Texture = _coinIcon;
            coinIconRect.CustomMinimumSize = new Vector2(16, 16);
            coinIconRect.Size = new Vector2(16, 16);
            coinIconRect.ExpandMode = TextureRect.ExpandModeEnum.FitWidth;
            coinIconRect.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
            coinIconRect.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
            coinIconRect.SizeFlagsVertical = SizeFlags.ShrinkCenter;
            precoContainer.AddChild(coinIconRect);

            var precoLabel = new Label();
            precoLabel.Text = $"{entry.PrecoDiamantes}";
            precoLabel.AddThemeFontSizeOverride("font_size", 12);
            precoLabel.AddThemeColorOverride("font_color", new Color(0.91f, 0.77f, 0.28f));
            precoLabel.VerticalAlignment = VerticalAlignment.Center;
            precoContainer.AddChild(precoLabel);

            var comprarBtn = new Button();
            comprarBtn.Text = "Comprar";
            comprarBtn.CustomMinimumSize = new Vector2(0, 24);
            comprarBtn.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            comprarBtn.AddThemeFontSizeOverride("font_size", 11);
            int capturedItemId = entry.ItemID;
            int capturedPreco = entry.PrecoDiamantes;
            comprarBtn.Pressed += () => OnComprarItem(capturedItemId, capturedPreco);
            vbox.AddChild(comprarBtn);

            _gridContainer.AddChild(card);
        }

        if (_lojaData.Itens.Count == 0)
        {
            var vazio = new Label();
            vazio.Text = "Nenhum item disponivel na loja.";
            vazio.HorizontalAlignment = HorizontalAlignment.Center;
            vazio.AddThemeColorOverride("font_color", new Color(0.5f, 0.5f, 0.5f));
            _gridContainer.AddChild(vazio);
        }
    }

    private void AtualizarUI()
    {
        if (_cash == null) return;
        _diamantesLabel.Text = $"{_cash.Diamantes}";
    }

    private static Texture2D CarregarIconeFallback(int itemId)
    {
        string path = itemId switch
        {
            -1 => "res://ui/Incone de Menu/Character.png",
            100 => "res://Itens/Incones/Pergaminho de Captura de Pet.png",
            102 => "res://Itens/Incones/Pergaminho de Criação de Guild.png",
            103 => "res://Itens/Incones/Vip 1.png",
            104 => "res://Itens/Incones/Vip 2.png",
            105 => "res://Itens/Incones/vip 3.png",
            108 or 109 or 112 => "res://Itens/Incones/Bau surpresa 1.png",
            _ => "res://Itens/Incones/bagitem.png",
        };

        return ResourceLoader.Exists(path)
            ? ResourceLoader.Load<Texture2D>(path)
            : ResourceLoader.Load<Texture2D>("res://Itens/Incones/bagitem.png");
    }

    private void OnComprarItem(int itemId, int preco)
    {
        if (_cash == null) return;

        if (_cash.Diamantes < preco)
        {
            MostrarFeedback("❌ Diamantes insuficientes!", new Color(0.9f, 0.3f, 0.3f));
            return;
        }

        if (itemId == -1)
        {
            if (!_cash.ComprarSlotPersonagem())
                MostrarFeedback("❌ Erro ao comprar slot!", new Color(0.9f, 0.3f, 0.3f));
            else
            {
                _cash.GastarDiamantes(preco);
                MostrarFeedback("✓ Slot comprado!", new Color(0.3f, 0.8f, 0.4f));
                AtualizarUI();
            }
            return;
        }

        MostrarFeedback("⏳ Solicitando compra...", new Color(0.9f, 0.9f, 0.3f));

        var net = GetNodeOrNull<GameNetwork>("/root/GameNetwork");
        if (net == null || !net.IsConnected)
        {
            MostrarFeedback("❌ Sem conexão com o servidor!", new Color(0.9f, 0.3f, 0.3f));
            return;
        }

        net.SendCashShopBuy(itemId, preco);
        _ultimoItemComprado = itemId;
        _ultimoPrecoCompra = preco;
    }

    private int _ultimoItemComprado;
    private int _ultimoPrecoCompra;

    private void OnCashShopResult(bool success, string message)
    {
        if (success)
        {
            if (_cash != null && _cash.GastarDiamantes(_ultimoPrecoCompra))
            {
                MostrarFeedback("✓ Item comprado!", new Color(0.3f, 0.8f, 0.4f));
                AtualizarUI();
            }
        }
        else
        {
            MostrarFeedback($"❌ {message}", new Color(0.9f, 0.3f, 0.3f));
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
        if (parent != null && GetParent() == null)
        {
            parent.AddChild(this);
        }
        CarregarDados();
        AtualizarUI();
        Visible = true;
        CallDeferred(MethodName.Centralizar);
        MoveToFront();
    }
}
