using Godot;

[Tool]
public partial class NoMobZoneArea : Area2D
{
    private CollisionPolygon2D _collision;
    private Polygon2D _preview;
    private Vector2[] _lastPolygon = System.Array.Empty<Vector2>();

    public override void _Ready()
    {
        _collision = GetNodeOrNull<CollisionPolygon2D>("CollisionPolygon2D");
        _preview = GetNodeOrNull<Polygon2D>("Preview");

        CollisionLayer = 0;
        CollisionMask = 0;
        AddToGroup("no_mob_zone");

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
        _preview.Color = new Color(0.35f, 0.55f, 1.0f, 0.22f);
        EditorDescription = "[NoMobZone] Edite os pontos do CollisionPolygon2D";
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
