using Godot;

public partial class LojaCashUI : Control
{
    private Label _diamantesLabel;
    private Button _fecharBtn;
    private Control _feedback;
    private Label _feedbackLabel;
    private VBoxContainer _itensContainer;
    private CashManager _cash;
    private LojaCashData _lojaData;
    private GameNetwork _net;
    private Label _tituloLabel;
    private bool _arrastando;
    private Vector2 _pontoCliqueOriginal;
    private Texture2D _coinIcon;

    private static readonly string LojaDataPath = "res://SistemaContas/LojaCashData.tres";
    private static readonly string CoinIconPath = "res://Itens/Incones/Loja de Cash.png";

    public override void _Ready()
    {
        _diamantesLabel = GetNode<Label>("%DiamantesLabel");
        _fecharBtn = GetNode<Button>("%FecharBtn");
        _feedback = GetNode<Control>("%Feedback");
        _feedbackLabel = GetNode<Label>("%FeedbackLabel");
        _itensContainer = GetNode<VBoxContainer>("%ItensContainer");
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
        icon.CustomMinimumSize = new Vector2(16, 16);
        icon.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
        topo.AddChild(icon);
        topo.MoveChild(icon, _diamantesLabel.GetIndex());

        _diamantesLabel.Text = $"{_cash?.Diamantes ?? 0}";
    }

    private void Centralizar()
    {
        Vector2 tela = GetViewportRect().Size;
        Position = (tela / 2) - (Size / 2);
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
        foreach (var child in _itensContainer.GetChildren())
            child.QueueFree();

        var itemDB = GetNodeOrNull<ItemDatabase>("/root/ItemDatabase");

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

            var hbox = new HBoxContainer();
            hbox.ThemeTypeVariation = "HBoxContainer";
            hbox.AddThemeConstantOverride("separation", 8);
            card.AddChild(hbox);

            string nome = !string.IsNullOrEmpty(entry.NomeExibicao) ? entry.NomeExibicao : $"Item #{entry.ItemID}";
            if (itemDB != null)
            {
                var item = itemDB.GetItem(entry.ItemID);
                if (item != null && string.IsNullOrEmpty(entry.NomeExibicao))
                    nome = item.Nome;
            }

            var infoVbox = new VBoxContainer();
            infoVbox.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            hbox.AddChild(infoVbox);

            var nomeLabel = new Label();
            nomeLabel.Text = nome;
            nomeLabel.AddThemeFontSizeOverride("font_size", 14);
            infoVbox.AddChild(nomeLabel);

            if (!string.IsNullOrEmpty(entry.Descricao))
            {
                var descLabel = new Label();
                descLabel.Text = entry.Descricao;
                descLabel.AddThemeFontSizeOverride("font_size", 11);
                descLabel.AddThemeColorOverride("font_color", new Color(0.6f, 0.65f, 0.75f));
                infoVbox.AddChild(descLabel);
            }

            var precoHbox = new HBoxContainer();
            precoHbox.AddThemeConstantOverride("separation", 4);
            if (_coinIcon != null)
            {
                var coinIcon = new TextureRect();
                coinIcon.Texture = _coinIcon;
                coinIcon.CustomMinimumSize = new Vector2(16, 16);
                coinIcon.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
                precoHbox.AddChild(coinIcon);
            }
            var precoLabel = new Label();
            precoLabel.Text = $"{entry.PrecoDiamantes}";
            precoLabel.AddThemeFontSizeOverride("font_size", 16);
            precoLabel.AddThemeColorOverride("font_color", new Color(0.91f, 0.77f, 0.28f));
            precoLabel.VerticalAlignment = VerticalAlignment.Center;
            precoHbox.AddChild(precoLabel);
            hbox.AddChild(precoHbox);

            var comprarBtn = new Button();
            comprarBtn.Text = "Comprar";
            comprarBtn.CustomMinimumSize = new Vector2(70, 32);
            int capturedItemId = entry.ItemID;
            int capturedPreco = entry.PrecoDiamantes;
            comprarBtn.Pressed += () => OnComprarItem(capturedItemId, capturedPreco);
            hbox.AddChild(comprarBtn);

            _itensContainer.AddChild(card);
        }

        if (_lojaData.Itens.Count == 0)
        {
            var vazio = new Label();
            vazio.Text = "Nenhum item disponivel na loja.";
            vazio.HorizontalAlignment = HorizontalAlignment.Center;
            vazio.AddThemeColorOverride("font_color", new Color(0.5f, 0.5f, 0.5f));
            _itensContainer.AddChild(vazio);
        }
    }

    private void AtualizarUI()
    {
        if (_cash == null) return;
        _diamantesLabel.Text = $"{_cash.Diamantes}";
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
