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
    private GameNetwork _gameNet;

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

    private string[] _emblemFiles;

    private string[] CarregarEmblemas()
    {
        try
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
        catch
        {
            return FALLBACK_EMBLEMS;
        }
    }

    private const int CUSTO_GOLD = 10000;

    public override void _Ready()
    {
        GD.Print("[GUILD-CREATE] _Ready() iniciado!");
        _panel = GetNode<Panel>("Panel");
        _titleBar = _panel.GetNode<Panel>("TitleBar");
        _closeButton = _panel.GetNode<Button>("CloseButton");
        _npcDialog = _panel.GetNode<Label>("NpcDialogScroll/NpcDialog");
        _nomeEdit = _panel.GetNode<LineEdit>("EditGrid/NomeEdit");
        _tagEdit = _panel.GetNode<LineEdit>("EditGrid/TagEdit");
        _emblemasGrid = _panel.GetNode<GridContainer>("EmblemasScroll/EmblemasGrid");
        _custoLabel = _panel.GetNode<Label>("CustoLabel");
        _statusLabel = _panel.GetNode<Label>("StatusLabel");
        _criarBtn = _panel.GetNode<Button>("CriarBtn");
        _cancelarBtn = _panel.GetNode<Button>("CancelarBtn");
        GD.Print("[GUILD-CREATE] Todos os nodes obtidos!");

        _gameNet = GetNodeOrNull<GameNetwork>("/root/GameNetwork");
        GD.Print($"[GUILD-CREATE] _gameNet = {_gameNet}");

        _emblemFiles = CarregarEmblemas();

        _closeButton.Pressed += OnFechar;
        _cancelarBtn.Pressed += OnFechar;
        _criarBtn.Pressed += OnCriar;

        _titleBar.GuiInput += OnTitleBarGuiInput;

        _tagEdit.TextChanged += OnTagTextChanged;

        PopularEmblemas();
        AtualizarCusto();

        if (_gameNet != null)
        {
            _gameNet.OnGuildCreateResult += OnGuildCreateResult;
            GD.Print("[GUILD-CREATE] Conectado ao OnGuildCreateResult!");
        }

        GD.Print("[GUILD-CREATE] _Ready() finalizado!");
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
        _emblemaRects = new TextureRect[_emblemFiles.Length];

        for (int i = 0; i < _emblemFiles.Length; i++)
        {
            int idx = i;
            var container = new PanelContainer();
            var style = MitharaUiTheme.Slot();
            style.SetBorderWidthAll(2);
            container.AddThemeStyleboxOverride("panel", style);
            container.CustomMinimumSize = new Vector2(48, 48);

            var rect = new TextureRect();
            rect.CustomMinimumSize = new Vector2(36, 36);
            rect.ExpandMode = TextureRect.ExpandModeEnum.FitWidth;
            rect.StretchMode = TextureRect.StretchModeEnum.KeepAspect;

            string path = "res://Itens/Emblema de Guild/" + _emblemFiles[idx];
            var tex = ResourceLoader.Load<Texture2D>(path);
            if (tex != null)
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
            style.BorderColor = i == idx ? MitharaUiTheme.Accent : MitharaUiTheme.BorderMuted;
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

        _criarBtn.Disabled = true;
        _statusLabel.Text = "Criando guilda...";

        if (_gameNet != null && _gameNet.IsConnected)
        {
            _gameNet.SendGuildCreateRequest(nome, tag, _emblemaSelecionado);
        }
        else
        {
            _statusLabel.Text = "Você precisa estar online para criar uma guilda.";
            _criarBtn.Disabled = false;
        }
    }

    private void OnGuildCreateResult(int guildId, bool success, string message)
    {
        if (success)
        {
            if (_gameNet != null)
            {
                _gameNet.GuildId = guildId;
                _gameNet.IsGuildLeader = true;
            }

            var guildUI = GetTree().Root.FindChild("GuildUI", true, false) as GuildUI;
            guildUI?.AbrirFechar(true);

            var overhead = GetTree().Root.FindChild("OverheadUI", true, false) as OverheadUI;
            overhead?.RecarregarDadosGuild();

            _statusLabel.Text = message;
            var timer = GetTree().CreateTimer(2.0);
            timer.Timeout += OnFechar;
        }
        else
        {
            _statusLabel.Text = message;
            _criarBtn.Disabled = false;
        }
    }

    private void Centralizar()
    {
        Vector2 tela = GetViewportRect().Size;
        _panel.Position = (tela / 2) - (_panel.Size / 2);
    }

    private void OnFechar()
    {
        if (_gameNet != null)
            _gameNet.OnGuildCreateResult -= OnGuildCreateResult;
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
