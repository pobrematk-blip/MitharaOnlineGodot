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

    private static readonly string LojaDataPath = "res://SistemaContas/LojaCashData.tres";

    public override void _Ready()
    {
        _diamantesLabel = GetNode<Label>("%DiamantesLabel");
        _fecharBtn = GetNode<Button>("%FecharBtn");
        _feedback = GetNode<Control>("%Feedback");
        _feedbackLabel = GetNode<Label>("%FeedbackLabel");
        _itensContainer = GetNode<VBoxContainer>("%ItensContainer");

        _cash = GetNode<CashManager>("/root/CashManager");

        _fecharBtn.Pressed += OnFechar;

        CarregarDados();
        AtualizarUI();
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

            var precoLabel = new Label();
            precoLabel.Text = $"💎 {entry.PrecoDiamantes}";
            precoLabel.AddThemeFontSizeOverride("font_size", 16);
            precoLabel.AddThemeColorOverride("font_color", new Color(0.91f, 0.77f, 0.28f));
            precoLabel.VerticalAlignment = VerticalAlignment.Center;
            hbox.AddChild(precoLabel);

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
        _diamantesLabel.Text = $"💎 {_cash.Diamantes}";
    }

    private void OnComprarItem(int itemId, int preco)
    {
        if (_cash == null) return;

        if (_cash.Diamantes < preco)
        {
            MostrarFeedback("❌ Diamantes insuficientes!", new Color(0.9f, 0.3f, 0.3f));
            return;
        }

        if (!_cash.GastarDiamantes(preco))
        {
            MostrarFeedback("❌ Erro ao processar compra!", new Color(0.9f, 0.3f, 0.3f));
            return;
        }

        ProcessarItemComprado(itemId);
        MostrarFeedback("✓ Item comprado!", new Color(0.3f, 0.8f, 0.4f));
        AtualizarUI();
    }

    private void ProcessarItemComprado(int itemId)
    {
        var saveManager = GetNodeOrNull<SaveManager>("/root/SaveManager");
        var itemDB = GetNodeOrNull<ItemDatabase>("/root/ItemDatabase");

        switch (itemId)
        {
            case -1:
                if (saveManager != null)
                    saveManager.ComprarSlot();
                break;

            default:
                if (itemDB != null && saveManager != null)
                {
                    var item = itemDB.GetItem(itemId);
                    if (item != null)
                    {
                        var inventario = GetNodeOrNull<InventarioComponent>("/root/main/Player/InventarioComponent");
                        if (inventario != null)
                            inventario.AdicionarItem(item, 1);
                    }
                }
                break;
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
        if (parent != null)
        {
            parent.AddChild(this);
            CarregarDados();
            AtualizarUI();
        }
    }
}
