using Godot;

public partial class GuildCreateUI : Control
{
    private Panel _panel;
    private Panel _titleBar;
    private Button _closeButton;
    private bool _arrastando;
    private Vector2 _pontoCliqueOriginal;

    private Label _npcDialog;
    private LineEdit _nomeEdit;
    private LineEdit _tagEdit;
    private GridContainer _emblemasGrid;
    private Label _custoLabel;
    private Label _statusLabel;
    private Button _criarBtn;
    private Button _cancelarBtn;

    private int _emblemaSelecionado = -1;
    private TextureRect[] _emblemaRects;

    private static readonly Color[] EmblemCores = {
        Colors.Red, Colors.Blue, Colors.Green, Colors.Yellow,
        Colors.Purple, Colors.Orange, Colors.Cyan, Colors.Pink,
        Colors.Brown, Colors.White
    };

    private const int CUSTO_GOLD = 10000;
    private const int SCROLL_ITEM_ID = 200;

    private static readonly string GuildDataPath = "user://guild_data.cfg";

    public override void _Ready()
    {
        _panel = GetNode<Panel>("Panel");
        _titleBar = _panel.GetNode<Panel>("TitleBar");
        _closeButton = _panel.GetNode<Button>("CloseButton");
        _npcDialog = _panel.GetNode<Label>("NpcDialog");
        _nomeEdit = _panel.GetNode<LineEdit>("EditGrid/NomeEdit");
        _tagEdit = _panel.GetNode<LineEdit>("EditGrid/TagEdit");
        _emblemasGrid = _panel.GetNode<GridContainer>("EmblemasGrid");
        _custoLabel = _panel.GetNode<Label>("CustoLabel");
        _statusLabel = _panel.GetNode<Label>("StatusLabel");
        _criarBtn = _panel.GetNode<Button>("CriarBtn");
        _cancelarBtn = _panel.GetNode<Button>("CancelarBtn");

        _closeButton.Pressed += OnFechar;
        _cancelarBtn.Pressed += OnFechar;
        _criarBtn.Pressed += OnCriar;

        _titleBar.GuiInput += OnTitleBarGuiInput;

        _tagEdit.TextChanged += OnTagTextChanged;

        PopularEmblemas();
        AtualizarCusto();

        CallDeferred(MethodName.Centralizar);
    }

    private void OnTagTextChanged(string text)
    {
        string filtrado = "";
        foreach (char c in text.ToUpper())
        {
            if (c >= 'A' && c <= 'Z')
            {
                if (filtrado.Length < 3)
                    filtrado += c;
            }
        }
        if (_tagEdit.Text != filtrado)
            _tagEdit.Text = filtrado;
        _tagEdit.CaretColumn = _tagEdit.Text.Length;
    }

    private void PopularEmblemas()
    {
        _emblemaRects = new TextureRect[EmblemCores.Length];

        for (int i = 0; i < EmblemCores.Length; i++)
        {
            int idx = i;
            var container = new PanelContainer();
            var style = new StyleBoxFlat();
            style.BgColor = new Color(0.12f, 0.13f, 0.18f, 0.95f);
            style.BorderWidthLeft = 2;
            style.BorderWidthTop = 2;
            style.BorderWidthRight = 2;
            style.BorderWidthBottom = 2;
            style.BorderColor = new Color(0.3f, 0.35f, 0.5f, 1);
            style.CornerRadiusTopLeft = 6;
            style.CornerRadiusTopRight = 6;
            style.CornerRadiusBottomLeft = 6;
            style.CornerRadiusBottomRight = 6;
            container.AddThemeStyleboxOverride("panel", style);
            container.CustomMinimumSize = new Vector2(48, 48);

            var rect = new TextureRect();
            rect.CustomMinimumSize = new Vector2(36, 36);
            rect.ExpandMode = TextureRect.ExpandModeEnum.FitWidth;
            rect.StretchMode = TextureRect.StretchModeEnum.KeepAspect;

            var img = Image.CreateEmpty(36, 36, false, Image.Format.Rgba8);
            img.Fill(EmblemCores[idx]);
            var tex = ImageTexture.CreateFromImage(img);
            rect.Texture = tex;

            container.AddChild(rect);
            _emblemaRects[idx] = rect;

            var click = new Button();
            click.MouseFilter = MouseFilterEnum.Ignore;
            click.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            click.SizeFlagsVertical = SizeFlags.ExpandFill;
            container.AddChild(click);

            container.GuiInput += (InputEvent @event) =>
            {
                if (@event is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
                    SelecionarEmblema(idx);
            };

            _emblemasGrid.AddChild(container);
        }
    }

    private void SelecionarEmblema(int idx)
    {
        _emblemaSelecionado = idx;
        for (int i = 0; i < _emblemaRects.Length; i++)
        {
            var parent = _emblemaRects[i].GetParent<PanelContainer>();
            if (parent == null) continue;
            var style = parent.GetThemeStylebox("panel").Duplicate() as StyleBoxFlat;
            if (style == null) continue;
            style.BorderColor = i == idx ? new Color(0.8f, 0.9f, 1f, 1) : new Color(0.3f, 0.35f, 0.5f, 1);
            style.BorderWidthLeft = i == idx ? 3 : 2;
            style.BorderWidthTop = i == idx ? 3 : 2;
            style.BorderWidthRight = i == idx ? 3 : 2;
            style.BorderWidthBottom = i == idx ? 3 : 2;
            parent.AddThemeStyleboxOverride("panel", style);
        }
    }

    private void AtualizarCusto()
    {
        _custoLabel.Text = $"Custo: {CUSTO_GOLD:N0} moedas de ouro OU Pergaminho de Criação de Clã";
    }

    private void OnCriar()
    {
        string nome = _nomeEdit.Text.Trim();
        string tag = _tagEdit.Text.Trim();

        if (string.IsNullOrEmpty(nome))
        {
            _statusLabel.Text = "Digite um nome para a guilda.";
            return;
        }
        if (nome.Length < 3)
        {
            _statusLabel.Text = "O nome da guilda deve ter pelo menos 3 caracteres.";
            return;
        }
        if (tag.Length != 3)
        {
            _statusLabel.Text = "A sigla deve ter exatamente 3 letras.";
            return;
        }
        if (_emblemaSelecionado < 0)
        {
            _statusLabel.Text = "Selecione um emblema para a guilda.";
            return;
        }

        if (!VerificarPagamento())
            return;

        SalvarGuilda(nome, tag, _emblemaSelecionado);
    }

    private bool VerificarPagamento()
    {
        var net = GetNodeOrNull<GameNetwork>("/root/GameNetwork");
        int gold = net?.Gold ?? 0;

        if (gold >= CUSTO_GOLD)
        {
            _statusLabel.Text = "Guilda criada! (modo offline - gold nao sera deduzido)";
            return true;
        }

        var inventario = GetNodeOrNull<InventarioComponent>("/root/main/Player/InventarioComponent");
        if (inventario != null)
        {
            foreach (var slot in inventario.Slots)
            {
                if (slot.Item != null && slot.Item.ItemID == SCROLL_ITEM_ID && slot.Quantidade > 0)
                {
                    _statusLabel.Text = "Guilda criada! Pergaminho consumido.";
                    slot.Quantidade--;
                    if (slot.Quantidade <= 0)
                        slot.Item = null;
                    return true;
                }
            }
        }

        _statusLabel.Text = $"Gold insuficiente! Voce tem {gold:N0}, precisa de {CUSTO_GOLD:N0} OU um Pergaminho de Criação de Clã.";
        return false;
    }

    private void SalvarGuilda(string nome, string tag, int emblemIdx)
    {
        var cfg = new ConfigFile();
        cfg.SetValue("Guild", "nome", nome);
        cfg.SetValue("Guild", "tag", tag);
        cfg.SetValue("Guild", "emblem_index", emblemIdx);
        var err = cfg.Save(GuildDataPath);

        if (err == Error.Ok)
        {
            _statusLabel.Text = $"Guilda '{nome}' ({tag}) fundada com sucesso!";
            _criarBtn.Disabled = true;

            var overhead = GetTree().CurrentScene?.FindChild("OverheadUI", true, false) as OverheadUI;
            overhead?.RecarregarDadosGuild();

            var timer = GetTree().CreateTimer(2.0);
            timer.Timeout += OnFechar;
        }
        else
        {
            _statusLabel.Text = "Erro ao salvar dados da guilda.";
        }
    }

    private void Centralizar()
    {
        Vector2 tela = GetViewportRect().Size;
        _panel.Position = (tela / 2) - (_panel.Size / 2);
    }

    private void OnFechar()
    {
        QueueFree();
    }

    private void OnTitleBarGuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mouseEvent && mouseEvent.ButtonIndex == MouseButton.Left)
        {
            _arrastando = mouseEvent.Pressed;
            if (mouseEvent.Pressed) _pontoCliqueOriginal = mouseEvent.Position;
        }
        else if (@event is InputEventMouseMotion mouseMotion && _arrastando)
            _panel.Position += mouseMotion.Position - _pontoCliqueOriginal;
    }
}
