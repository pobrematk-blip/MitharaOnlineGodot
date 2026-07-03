using Godot;

[Tool]
public partial class PvPZoneArea : Area2D
{
    public enum PvPZoneType
    {
        Normal = 0,
        Safe = 1,
        Arena = 2,
        Dungeon = 3,
    }

    private PvPZoneType _zoneType = PvPZoneType.Normal;
    private string _mapName = "main";
    private CollisionPolygon2D _collision;
    private Polygon2D _preview;
    private Label _label;
    private Vector2[] _lastPolygon = System.Array.Empty<Vector2>();

    [Export]
    public PvPZoneType ZoneType
    {
        get => _zoneType;
        set
        {
            _zoneType = value;
            UpdatePreview(force: true);
        }
    }

    [Export]
    public string MapName
    {
        get => _mapName;
        set => _mapName = string.IsNullOrWhiteSpace(value) ? "main" : value.Trim().ToLowerInvariant();
    }

    public override void _Ready()
    {
        _collision = GetNodeOrNull<CollisionPolygon2D>("CollisionPolygon2D");
        _preview = GetNodeOrNull<Polygon2D>("Preview");
        _label = GetNodeOrNull<Label>("Label");

        CollisionLayer = 0;
        CollisionMask = 0;
        AddToGroup("pvp_zone");

        if (!Engine.IsEditorHint())
        {
            Visible = false;
            SetProcess(false);
            return;
        }

        UpdatePreview(force: true);
    }

    public override void _Process(double delta)
    {
        if (Engine.IsEditorHint())
            UpdatePreview();
    }

    public Vector2[] GetWorldPolygon()
    {
        if (_collision == null || _collision.Polygon.Length < 3)
            return System.Array.Empty<Vector2>();

        var result = new Vector2[_collision.Polygon.Length];
        for (int i = 0; i < _collision.Polygon.Length; i++)
            result[i] = _collision.ToGlobal(_collision.Polygon[i]);
        return result;
    }

    private void UpdatePreview(bool force = false)
    {
        if (_collision == null || _preview == null)
            return;

        if (!force && SamePolygon(_lastPolygon, _collision.Polygon))
            return;

        _lastPolygon = (Vector2[])_collision.Polygon.Clone();
        _preview.Position = _collision.Position;
        _preview.Rotation = _collision.Rotation;
        _preview.Scale = _collision.Scale;
        _preview.Polygon = _collision.Polygon;
        _preview.Color = _zoneType switch
        {
            PvPZoneType.Safe => new Color(0.2f, 0.9f, 0.35f, 0.24f),
            PvPZoneType.Arena => new Color(1.0f, 0.15f, 0.15f, 0.26f),
            PvPZoneType.Dungeon => new Color(0.65f, 0.2f, 1.0f, 0.24f),
            _ => new Color(1.0f, 0.85f, 0.15f, 0.20f),
        };

        string label = _zoneType switch
        {
            PvPZoneType.Safe => "SAFE",
            PvPZoneType.Arena => "PVP",
            PvPZoneType.Dungeon => "DUN",
            _ => "NOR",
        };

        if (_label != null)
            _label.Text = label;

        EditorDescription = $"[PvPZone] {ZoneType} / mapa {MapName}";
    }

    private static bool SamePolygon(Vector2[] a, Vector2[] b)
    {
        if (a.Length != b.Length)
            return false;

        for (int i = 0; i < a.Length; i++)
        {
            if (a[i] != b[i])
                return false;
        }

        return true;
    }
}
