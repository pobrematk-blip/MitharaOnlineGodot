using Godot;

[Tool]
public partial class NoMobZoneMarker : Area2D
{
	private Vector2 _zoneSize = new(1050, 700);
	private Polygon2D _preview;
	private CollisionShape2D _collisionShape;

	[Export]
	public Vector2 ZoneSize
	{
		get => _zoneSize;
		set
		{
			_zoneSize = value;
			UpdateShape();
			UpdatePreview();
		}
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
		_collisionShape = GetNodeOrNull<CollisionShape2D>("CollisionShape2D");

		if (_collisionShape != null)
			_collisionShape.Shape = _collisionShape.Shape.Duplicate() as Shape2D;

		UpdateShape();
		UpdatePreview();
		AddToGroup("no_mob_zone");
	}

	private void UpdateShape()
	{
		if (_collisionShape == null) return;
		if (_collisionShape.Shape is RectangleShape2D rect)
			rect.Size = _zoneSize;
	}

	private void UpdatePreview()
	{
		if (_preview == null) return;
		var halfSize = _zoneSize / 2;
		_preview.Polygon = new Vector2[]
		{
			new(-halfSize.X, -halfSize.Y),
			new(halfSize.X, -halfSize.Y),
			new(halfSize.X, halfSize.Y),
			new(-halfSize.X, halfSize.Y),
		};
		EditorDescription = $"[NoMobZone] {_zoneSize.X:F0}x{_zoneSize.Y:F0}";
	}

	public Rect2 GetZoneRect()
	{
		return new Rect2(Position - _zoneSize / 2, _zoneSize);
	}
}
