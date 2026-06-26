using Godot;

[Tool]
public partial class TileMarker : Area2D
{
    public enum TileType
    {
        Block = 0,
        Teleport = 1,
        Npc = 2
    }

    private TileType _type = TileType.Block;
    private string _targetScene = "";
    private float _targetX;
    private float _targetY;
    private Polygon2D _preview;
    private Label _label;

    [Export]
    public TileType Type
    {
        get => _type;
        set
        {
            _type = value;
            UpdatePreview();
        }
    }

    [Export]
    public string TargetScene
    {
        get => _targetScene;
        set => _targetScene = value;
    }

    [Export]
    public float TargetX
    {
        get => _targetX;
        set => _targetX = value;
    }

    [Export]
    public float TargetY
    {
        get => _targetY;
        set => _targetY = value;
    }

    public override void _Ready()
    {
        if (!Engine.IsEditorHint())
        {
            Visible = false;
            SetProcess(false);
            return;
        }

        _preview = GetNodeOrNull<Polygon2D>("Preview");
        _label = GetNodeOrNull<Label>("Label");

        CollisionLayer = 0;
        CollisionMask = 0;

        UpdatePreview();
        AddToGroup("tile_marker");
    }

    private void UpdatePreview()
    {
        if (_preview == null) return;

        float half = 16f;
        _preview.Polygon = new Vector2[]
        {
            new(-half, -half),
            new(half, -half),
            new(half, half),
            new(-half, half),
        };

        _preview.Color = _type switch
        {
            TileType.Block => new Color(1, 0, 0, 0.4f),
            TileType.Teleport => new Color(0, 1, 0, 0.4f),
            TileType.Npc => new Color(0.4f, 0.7f, 1, 0.4f),
            _ => new Color(1, 1, 1, 0.3f),
        };

        string typeName = _type switch
        {
            TileType.Block => "Bloqueio",
            TileType.Teleport => "Teleporte",
            TileType.Npc => "NPC Void",
            _ => "?"
        };

        string extra = "";
        if (_type == TileType.Teleport && !string.IsNullOrEmpty(_targetScene))
            extra = $" -> {_targetScene} ({_targetX:F0},{_targetY:F0})";

        EditorDescription = $"[{typeName}]{extra}";

        if (_label != null)
        {
            string letter = _type switch
            {
                TileType.Block => "B",
                TileType.Teleport => "T",
                TileType.Npc => "N",
                _ => "?"
            };
            _label.Text = letter;
        }
    }

    public int GetTileX()
    {
        return Mathf.FloorToInt(Position.X / 32f);
    }

    public int GetTileY()
    {
        return Mathf.FloorToInt(Position.Y / 32f);
    }
}
