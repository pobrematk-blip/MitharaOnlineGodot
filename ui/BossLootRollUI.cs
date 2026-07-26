#nullable enable
using Godot;
using System;
using System.Collections.Generic;

public partial class BossLootRollUI : Control
{
    private readonly Dictionary<int, RollWidgets> _rolls = new();
    private Panel _panel = null!;
    private VBoxContainer _list = null!;
    private ItemDatabase? _itemDb;
    private GameNetwork? _net;

    public override void _Ready()
    {
        AnchorLeft = 0;
        AnchorTop = 0;
        AnchorRight = 1;
        AnchorBottom = 1;
        MouseFilter = MouseFilterEnum.Ignore;
        _itemDb = GetNodeOrNull<ItemDatabase>("/root/GameNetwork/ItemDatabase") ?? GetNodeOrNull<ItemDatabase>("/root/ItemDatabase");
        BuildUi();
    }

    public override void _Process(double delta)
    {
        foreach (var entry in _rolls.Values)
        {
            entry.Remaining = Mathf.Max(0f, entry.Remaining - (float)delta);
            if (!entry.Chosen)
                entry.Timer.Text = $"{entry.Remaining:0}s";
        }
    }

    public void AddRoll(int rollId, int itemId, int quantity, string itemName, string rarity, float seconds, GameNetwork net)
    {
        _net = net;
        if (_panel == null)
        {
            BuildUi();
        }

        if (_rolls.ContainsKey(rollId))
            return;

        var item = _itemDb?.GetItem(itemId);
        string displayName = item?.Nome ?? itemName;
        Texture2D? icon = item?.Icone;

        var row = new Panel { CustomMinimumSize = new Vector2(380, 92) };
        row.AddThemeStyleboxOverride("panel", MitharaUiTheme.Inner(0.82f, 5));
        _list.AddChild(row);

        var margin = new MarginContainer();
        margin.SetAnchorsPreset(LayoutPreset.FullRect);
        margin.AddThemeConstantOverride("margin_left", 8);
        margin.AddThemeConstantOverride("margin_top", 8);
        margin.AddThemeConstantOverride("margin_right", 8);
        margin.AddThemeConstantOverride("margin_bottom", 8);
        row.AddChild(margin);

        var h = new HBoxContainer();
        h.AddThemeConstantOverride("separation", 8);
        margin.AddChild(h);

        var iconPanel = new Panel { CustomMinimumSize = new Vector2(56, 56) };
        iconPanel.AddThemeStyleboxOverride("panel", MitharaUiTheme.Slot(true));
        h.AddChild(iconPanel);

        var texture = new TextureRect
        {
            Texture = icon,
            ExpandMode = TextureRect.ExpandModeEnum.FitWidthProportional,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
        };
        texture.SetAnchorsPreset(LayoutPreset.FullRect);
        texture.OffsetLeft = 5;
        texture.OffsetTop = 5;
        texture.OffsetRight = -5;
        texture.OffsetBottom = -5;
        iconPanel.AddChild(texture);

        var textBox = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        h.AddChild(textBox);

        var name = new Label { Text = quantity > 1 ? $"{displayName} x{quantity}" : displayName };
        name.AddThemeFontSizeOverride("font_size", 14);
        name.AddThemeColorOverride("font_color", MitharaUiTheme.Accent);
        textBox.AddChild(name);

        var meta = new Label { Text = string.IsNullOrWhiteSpace(rarity) ? "Drop de boss" : $"Drop de boss | {rarity}" };
        meta.AddThemeFontSizeOverride("font_size", 11);
        meta.AddThemeColorOverride("font_color", MitharaUiTheme.TextMuted);
        textBox.AddChild(meta);

        var result = new Label { Text = "Escolha: Passar ou tentar o drop." };
        result.AddThemeFontSizeOverride("font_size", 11);
        result.AddThemeColorOverride("font_color", MitharaUiTheme.Text);
        textBox.AddChild(result);

        var buttons = new VBoxContainer { CustomMinimumSize = new Vector2(82, 0) };
        buttons.AddThemeConstantOverride("separation", 4);
        h.AddChild(buttons);

        var timer = new Label { Text = $"{seconds:0}s", HorizontalAlignment = HorizontalAlignment.Center };
        timer.AddThemeFontSizeOverride("font_size", 12);
        timer.AddThemeColorOverride("font_color", MitharaUiTheme.Text);
        buttons.AddChild(timer);

        var drop = CreateButton("Drop");
        var pass = CreateButton("Pass");
        buttons.AddChild(drop);
        buttons.AddChild(pass);

        var widgets = new RollWidgets(row, timer, result, drop, pass, seconds);
        _rolls[rollId] = widgets;

        drop.Pressed += () => Choose(rollId, true);
        pass.Pressed += () => Choose(rollId, false);
        Visible = true;
        Reposition();
    }

    public void ShowResult(int rollId, bool won, string message, string winnerName, int winningRoll, int myRoll)
    {
        if (!_rolls.TryGetValue(rollId, out var widgets))
            return;

        widgets.Chosen = true;
        string rollText = myRoll >= 0 ? $" Seu dado: {myRoll}." : "";
        string winnerText = winningRoll >= 0 && !string.IsNullOrWhiteSpace(winnerName)
            ? $" Vencedor: {winnerName} ({winningRoll})."
            : "";
        widgets.Result.Text = $"{message}{rollText}{winnerText}";
        widgets.Result.AddThemeColorOverride("font_color", won ? new Color(0.45f, 1f, 0.55f) : MitharaUiTheme.Text);
        widgets.Timer.Text = won ? "Seu" : "Fim";
        widgets.Drop.Disabled = true;
        widgets.Pass.Disabled = true;

        var timer = GetTree()?.CreateTimer(4.0);
        if (timer != null)
            timer.Timeout += () => RemoveRoll(rollId);
    }

    private void Choose(int rollId, bool wantDrop)
    {
        if (!_rolls.TryGetValue(rollId, out var widgets) || widgets.Chosen)
            return;

        widgets.Chosen = true;
        widgets.Drop.Disabled = true;
        widgets.Pass.Disabled = true;
        widgets.Result.Text = wantDrop ? "Voce entrou no sorteio." : "Voce passou este item.";
        widgets.Timer.Text = "...";
        _net?.SendBossLootRollChoice(rollId, wantDrop);
    }

    private void RemoveRoll(int rollId)
    {
        if (!_rolls.Remove(rollId, out var widgets))
            return;

        widgets.Row.QueueFree();
        if (_rolls.Count == 0)
            QueueFree();
    }

    private void BuildUi()
    {
        if (_panel != null)
            return;

        _panel = new Panel { CustomMinimumSize = new Vector2(420, 0), Size = new Vector2(420, 120) };
        _panel.AddThemeStyleboxOverride("panel", MitharaUiTheme.Panel(0.94f, 5));
        AddChild(_panel);

        var margin = new MarginContainer();
        margin.SetAnchorsPreset(LayoutPreset.FullRect);
        margin.AddThemeConstantOverride("margin_left", 10);
        margin.AddThemeConstantOverride("margin_top", 8);
        margin.AddThemeConstantOverride("margin_right", 10);
        margin.AddThemeConstantOverride("margin_bottom", 10);
        _panel.AddChild(margin);

        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 8);
        margin.AddChild(root);

        var title = new Label { Text = "Drops de Boss", HorizontalAlignment = HorizontalAlignment.Center };
        title.AddThemeFontSizeOverride("font_size", 16);
        title.AddThemeColorOverride("font_color", MitharaUiTheme.Accent);
        root.AddChild(title);

        _list = new VBoxContainer();
        _list.AddThemeConstantOverride("separation", 6);
        root.AddChild(_list);
        Reposition();
    }

    private void Reposition()
    {
        if (_panel == null) return;

        _panel.Size = new Vector2(420, 48 + Math.Max(1, _rolls.Count) * 100);
        Vector2 viewport = GetViewportRect().Size;
        _panel.Position = new Vector2((viewport.X - _panel.Size.X) / 2f, 70f);
    }

    private static Button CreateButton(string text)
    {
        var button = new Button { Text = text, CustomMinimumSize = new Vector2(74, 28) };
        button.AddThemeFontSizeOverride("font_size", 12);
        return button;
    }

    private sealed class RollWidgets
    {
        public Panel Row { get; }
        public Label Timer { get; }
        public Label Result { get; }
        public Button Drop { get; }
        public Button Pass { get; }
        public float Remaining { get; set; }
        public bool Chosen { get; set; }

        public RollWidgets(Panel row, Label timer, Label result, Button drop, Button pass, float remaining)
        {
            Row = row;
            Timer = timer;
            Result = result;
            Drop = drop;
            Pass = pass;
            Remaining = remaining;
        }
    }
}
