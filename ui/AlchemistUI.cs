using Godot;

public partial class AlchemistUI : Control
{
    private Panel _panel;
    private Panel _titleBar;
    private Button _closeButton;
    private Label _titleLabel;
    private Label _levelLabel;
    private ProgressBar _xpBar;
    private TabContainer _tabContainer;
    private VBoxContainer _potionList;
    private VBoxContainer _runeList;
    private Label _statusLabel;
    private GameNetwork _gameNet;
    private bool _arrastando;
    private Vector2 _pontoCliqueOriginal;

    private int _alqLevel;
    private long _alqXp;
    private int _alqXpForNext;
    private Godot.Collections.Array _knownRecipes = new();

    public override void _Ready()
    {
        _panel = new Panel();
        _panel.CustomMinimumSize = new Vector2(520, 460);
        _panel.OffsetRight = 520;
        _panel.OffsetBottom = 460;
        AddChild(_panel);

        _titleBar = new Panel();
        _titleBar.OffsetRight = 520;
        _titleBar.OffsetBottom = 28;
        _panel.AddChild(_titleBar);

        _titleLabel = new Label();
        _titleLabel.OffsetRight = 520;
        _titleLabel.OffsetBottom = 28;
        _titleLabel.Text = "Alquimista";
        _titleLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _titleLabel.VerticalAlignment = VerticalAlignment.Center;
        _titleBar.AddChild(_titleLabel);

        _closeButton = new Button();
        _closeButton.OffsetLeft = 492;
        _closeButton.OffsetRight = 518;
        _closeButton.OffsetTop = 4;
        _closeButton.OffsetBottom = 24;
        _closeButton.Text = "X";

        var closeNormal = new StyleBoxFlat();
        closeNormal.BgColor = new Color(0.8f, 0.15f, 0.15f);
        closeNormal.SetCornerRadiusAll(3);
        closeNormal.SetContentMarginAll(0);
        _closeButton.AddThemeStyleboxOverride("normal", closeNormal);

        var closeHover = new StyleBoxFlat();
        closeHover.BgColor = new Color(1f, 0.25f, 0.25f);
        closeHover.SetCornerRadiusAll(3);
        closeHover.SetContentMarginAll(0);
        _closeButton.AddThemeStyleboxOverride("hover", closeHover);

        var closePressed = new StyleBoxFlat();
        closePressed.BgColor = new Color(0.6f, 0.1f, 0.1f);
        closePressed.SetCornerRadiusAll(3);
        closePressed.SetContentMarginAll(0);
        _closeButton.AddThemeStyleboxOverride("pressed", closePressed);

        _closeButton.AddThemeColorOverride("font_color", new Color(1, 1, 1));
        _panel.AddChild(_closeButton);

        _levelLabel = new Label();
        _levelLabel.OffsetLeft = 10;
        _levelLabel.OffsetTop = 35;
        _levelLabel.OffsetRight = 250;
        _levelLabel.OffsetBottom = 55;
        _levelLabel.Text = "Alquimista NV.1";
        _panel.AddChild(_levelLabel);

        _xpBar = new ProgressBar();
        _xpBar.OffsetLeft = 10;
        _xpBar.OffsetTop = 56;
        _xpBar.OffsetRight = 510;
        _xpBar.OffsetBottom = 72;
        _xpBar.MinValue = 0;
        _xpBar.MaxValue = 100;
        _xpBar.Value = 0;
        _panel.AddChild(_xpBar);

        _tabContainer = new TabContainer();
        _tabContainer.OffsetLeft = 10;
        _tabContainer.OffsetTop = 80;
        _tabContainer.OffsetRight = 510;
        _tabContainer.OffsetBottom = 420;
        _panel.AddChild(_tabContainer);

        var potionScroll = new ScrollContainer();
        potionScroll.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        potionScroll.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        _tabContainer.AddChild(potionScroll);

        _potionList = new VBoxContainer();
        _potionList.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        potionScroll.AddChild(_potionList);

        var runeScroll = new ScrollContainer();
        runeScroll.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        runeScroll.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        _tabContainer.AddChild(runeScroll);

        _runeList = new VBoxContainer();
        _runeList.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        runeScroll.AddChild(_runeList);

        _statusLabel = new Label();
        _statusLabel.OffsetLeft = 10;
        _statusLabel.OffsetTop = 425;
        _statusLabel.OffsetRight = 510;
        _statusLabel.OffsetBottom = 455;
        _statusLabel.Text = "";
        _statusLabel.AutowrapMode = TextServer.AutowrapMode.Word;
        _panel.AddChild(_statusLabel);

        _gameNet = GetNodeOrNull<GameNetwork>("/root/GameNetwork");
        if (_gameNet != null)
        {
            _gameNet.OnProfessionInfo += OnProfessionInfo;
            _gameNet.OnLearnRecipeResult += OnLearnRecipeResult;
            _gameNet.OnCraftResult += OnCraftResult;
            _gameNet.OnProfessionLevelUp += OnProfessionLevelUp;
        }

        _closeButton.Pressed += Fechar;
        _titleBar.GuiInput += OnTitleBarGuiInput;

        _panel.Visible = false;
    }

    public void Abrir()
    {
        _panel.Visible = true;
        Centralizar();
    }

    private void Fechar() { _panel.Visible = false; }

    public override void _ExitTree()
    {
        if (_gameNet != null)
        {
            _gameNet.OnProfessionInfo -= OnProfessionInfo;
            _gameNet.OnLearnRecipeResult -= OnLearnRecipeResult;
            _gameNet.OnCraftResult -= OnCraftResult;
            _gameNet.OnProfessionLevelUp -= OnProfessionLevelUp;
        }
    }

    private void OnProfessionInfo(int alqLevel, long alqXp, int alqXpForNext, Godot.Collections.Array knownRecipes)
    {
        _alqLevel = alqLevel;
        _alqXp = alqXp;
        _alqXpForNext = alqXpForNext;
        _knownRecipes = knownRecipes;

        _levelLabel.Text = $"Alquimista NV.{_alqLevel}";
        if (_alqXpForNext > 0)
        {
            float pct = (float)_alqXp / _alqXpForNext * 100f;
            _xpBar.MaxValue = _alqXpForNext;
            _xpBar.Value = _alqXp;
            _levelLabel.Text += $" ({_alqXp}/{_alqXpForNext} XP)";
        }
        else
        {
            _xpBar.MaxValue = 1;
            _xpBar.Value = 1;
            _levelLabel.Text += " (MAX)";
        }

        RebuildRecipeLists();
    }

    private void RebuildRecipeLists()
    {
        foreach (var c in _potionList.GetChildren()) c.QueueFree();
        foreach (var c in _runeList.GetChildren()) c.QueueFree();

        bool hasRecipes = false;

        for (int recipeId = 2001; recipeId <= 2160; recipeId++)
        {
            if (!_knownRecipes.Contains(recipeId))
                continue;

            hasRecipes = true;
            bool isPotion = recipeId <= 2080;
            var container = isPotion ? _potionList : _runeList;

            var hbox = new HBoxContainer();
            hbox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

            var nameLabel = new Label();
            nameLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            nameLabel.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
            nameLabel.Text = GetRecipeDisplayName(recipeId);
            hbox.AddChild(nameLabel);

            var craftBtn = new Button();
            craftBtn.CustomMinimumSize = new Vector2(80, 0);
            craftBtn.Text = "Craft";
            int capturedId = recipeId;
            craftBtn.Pressed += () => OnCraftPressed(capturedId);
            hbox.AddChild(craftBtn);

            container.AddChild(hbox);
        }

        if (!hasRecipes)
        {
            var noRecipes = new Label();
            noRecipes.Text = "Nenhuma receita aprendida. Use receitas do inventário para aprender.";
            _potionList.AddChild(noRecipes);

            var noRecipes2 = new Label();
            noRecipes2.Text = "Nenhuma receita aprendida. Use receitas do inventário para aprender.";
            _runeList.AddChild(noRecipes2);
        }
    }

    private void OnCraftPressed(int recipeId)
    {
        _gameNet?.SendCraftAlchemist(recipeId);
        _statusLabel.Text = "Criando...";
    }

    private void OnLearnRecipeResult(bool success, int recipeItemId, string message)
    {
        _statusLabel.Text = message;
        if (success)
            _gameNet?.SendProfessionInfo();
    }

    private void OnCraftResult(bool success, int recipeId, int producedItemId, string message)
    {
        _statusLabel.Text = message;
        if (success)
            _gameNet?.SendProfessionInfo();
    }

    private void OnProfessionLevelUp(byte professionType, byte newLevel)
    {
        if (professionType == 6)
        {
            _statusLabel.Text = $"Alquimista subiu para NV.{newLevel}!";
            _gameNet?.SendProfessionInfo();
        }
    }

    private string GetRecipeDisplayName(int recipeId)
    {
        string[] cats = { "Vida", "Mana", "Estamina", "Forca", "Destreza", "Inteligencia", "Defesa Fisica", "Defesa Magica", "Agilidade", "Sorte" };
        if (recipeId <= 2080)
        {
            int idx = recipeId - 2001;
            int catIdx = idx / 10;
            int lvl = (idx % 10) + 1;
            if (catIdx < cats.Length)
                return $"Poção de {cats[catIdx]} NV.{lvl}";
            return $"Poção #{recipeId}";
        }
        else
        {
            int idx = recipeId - 2081;
            int catIdx = idx / 10;
            int lvl = (idx % 10) + 1;
            if (catIdx < cats.Length)
                return $"Runa de {cats[catIdx]} NV.{lvl}";
            return $"Runa #{recipeId}";
        }
    }

    private void Centralizar()
    {
        var vpSize = GetViewport().GetVisibleRect().Size;
        _panel.OffsetLeft = (int)((vpSize.X - _panel.CustomMinimumSize.X) / 2);
        _panel.OffsetTop = (int)((vpSize.Y - _panel.CustomMinimumSize.Y) / 2);
        _panel.OffsetRight = _panel.OffsetLeft + (int)_panel.CustomMinimumSize.X;
        _panel.OffsetBottom = _panel.OffsetTop + (int)_panel.CustomMinimumSize.Y;
    }

    private void OnTitleBarGuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mouseEvent)
        {
            if (mouseEvent.Pressed && mouseEvent.ButtonIndex == MouseButton.Left)
            {
                _arrastando = true;
                _pontoCliqueOriginal = mouseEvent.Position;
            }
            else
                _arrastando = false;
        }
        else if (@event is InputEventMouseMotion motion && _arrastando)
        {
            _panel.OffsetLeft += (int)motion.Relative.X;
            _panel.OffsetTop += (int)motion.Relative.Y;
            _panel.OffsetRight += (int)motion.Relative.X;
            _panel.OffsetBottom += (int)motion.Relative.Y;
        }
    }
}
