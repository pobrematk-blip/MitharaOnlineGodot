using Godot;

public partial class DuelArenaOverlay : CanvasLayer
{
    private const string OverlayName = "DuelArenaOverlay";
    private const string AreaName = "DuelArenaWorldArea";
    private const string FlagPath = "res://tiles/TileSets/Tileset101.png";

    private Label _timerLabel;
    private double _remaining;
    private Node2D _areaNode;
    private string _opponentName = "";

    public static void Show(SceneTree tree, Vector2 center, float halfSize, float durationSeconds, string opponentName)
    {
        if (tree == null)
            return;

        Clear(tree);

        var overlay = new DuelArenaOverlay
        {
            Name = OverlayName,
            _remaining = Mathf.Max(1f, durationSeconds),
            _opponentName = opponentName ?? "",
        };

        tree.Root.AddChild(overlay);
        overlay.CreateTimerLabel(opponentName);
        overlay.CreateWorldArea(tree, center, halfSize);
    }

    public static void Clear(SceneTree tree)
    {
        if (tree == null)
            return;

        var old = tree.Root.GetNodeOrNull<DuelArenaOverlay>(OverlayName);
        old?.QueueFree();

        var world = tree.CurrentScene?.FindChild("World", true, false) as Node;
        var oldArea = world?.GetNodeOrNull<Node2D>(AreaName);
        oldArea?.QueueFree();
    }

    public override void _Process(double delta)
    {
        _remaining = Mathf.Max(0.0, _remaining - delta);
        UpdateTimerText();
    }

    public override void _ExitTree()
    {
        if (_areaNode != null && GodotObject.IsInstanceValid(_areaNode))
            _areaNode.QueueFree();
    }

    private void CreateTimerLabel(string opponentName)
    {
        _timerLabel = new Label
        {
            Text = "",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            CustomMinimumSize = new Vector2(360, 40),
            Size = new Vector2(360, 40),
            Position = new Vector2(0, 28),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        _timerLabel.SetAnchorsPreset(Control.LayoutPreset.TopWide);
        _timerLabel.OffsetLeft = 0;
        _timerLabel.OffsetRight = 0;
        _timerLabel.OffsetTop = 24;
        _timerLabel.OffsetBottom = 64;
        _timerLabel.AddThemeFontSizeOverride("font_size", 24);
        _timerLabel.AddThemeColorOverride("font_color", new Color(1f, 0.92f, 0.82f, 1f));
        _timerLabel.AddThemeColorOverride("font_outline_color", new Color(0.08f, 0f, 0f, 1f));
        _timerLabel.AddThemeConstantOverride("outline_size", 6);
        _timerLabel.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.8f));
        _timerLabel.AddThemeConstantOverride("shadow_offset_x", 2);
        _timerLabel.AddThemeConstantOverride("shadow_offset_y", 2);
        AddChild(_timerLabel);
        UpdateTimerText();
    }

    private void CreateWorldArea(SceneTree tree, Vector2 center, float halfSize)
    {
        var world = tree.CurrentScene?.FindChild("World", true, false) as Node2D
            ?? tree.CurrentScene as Node2D;
        if (world == null)
            return;

        _areaNode = new Node2D
        {
            Name = AreaName,
            ZIndex = -1,
            ZAsRelative = false,
        };
        world.AddChild(_areaNode);

        var points = new Vector2[]
        {
            new(center.X - halfSize, center.Y - halfSize),
            new(center.X + halfSize, center.Y - halfSize),
            new(center.X + halfSize, center.Y + halfSize),
            new(center.X - halfSize, center.Y + halfSize),
        };

        var fill = new Polygon2D
        {
            Polygon = points,
            Color = new Color(1f, 0.05f, 0.02f, 0.16f),
            ZIndex = -1,
            ZAsRelative = false,
        };
        _areaNode.AddChild(fill);

        var border = new Line2D
        {
            Width = 6f,
            DefaultColor = new Color(1f, 0.1f, 0.05f, 0.75f),
            Closed = true,
            ZIndex = 0,
            ZAsRelative = false,
        };
        foreach (var p in points)
            border.AddPoint(p);
        _areaNode.AddChild(border);

        var flag = new Sprite2D
        {
            Texture = CreateFlagTexture(),
            GlobalPosition = center,
            Scale = new Vector2(1.25f, 1.25f),
            ZIndex = 1,
            ZAsRelative = false,
        };
        _areaNode.AddChild(flag);
    }

    private static Texture2D CreateFlagTexture()
    {
        var baseTexture = ResourceLoader.Load<Texture2D>(FlagPath);
        if (baseTexture == null)
            return null;

        return new AtlasTexture
        {
            Atlas = baseTexture,
            Region = new Rect2(201, 55, 38, 79),
        };
    }

    private void UpdateTimerText()
    {
        if (_timerLabel == null)
            return;

        int total = Mathf.CeilToInt((float)_remaining);
        int minutes = total / 60;
        int seconds = total % 60;
        string enemy = string.IsNullOrWhiteSpace(_opponentName) ? "" : $" vs {_opponentName}";
        _timerLabel.Text = $"DUELO{enemy}  {minutes:00}:{seconds:00}";
    }
}
