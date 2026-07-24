using Godot;

[Tool]
public partial class TeleportPairMarker : Area2D
{
    private int _pairId = 1;
    private Vector2 _destinationOffset = new(0, 32);
    private Polygon2D _preview;
    private Label _label;
    private bool _localPlayerInside;

    [Export]
    public int PairId
    {
        get => _pairId;
        set
        {
            _pairId = Mathf.Max(1, value);
            UpdatePreview();
        }
    }

    [Export]
    public Vector2 DestinationOffset
    {
        get => _destinationOffset;
        set => _destinationOffset = value;
    }

    public override void _Ready()
    {
        _preview = GetNodeOrNull<Polygon2D>("Preview");
        _label = GetNodeOrNull<Label>("Label");

        CollisionLayer = 0;
        CollisionMask = 2;
        Monitoring = true;
        AddToGroup("teleport_pair_marker");

        if (!Engine.IsEditorHint())
        {
            Visible = false;
            BodyEntered += OnBodyEntered;
            BodyExited += OnBodyExited;
            SetPhysicsProcess(true);
            return;
        }

        UpdatePreview();
    }

    private void UpdatePreview()
    {
        if (_preview != null)
        {
            const float half = 16f;
            _preview.Polygon = new Vector2[]
            {
                new(-half, -half),
                new(half, -half),
                new(half, half),
                new(-half, half),
            };
            _preview.Color = new Color(0.15f, 1.0f, 0.35f, 0.45f);
        }

        if (_label != null)
            _label.Text = PairId.ToString();

        EditorDescription = $"[Teleporte Casal] PairId={PairId}";
    }

    public int GetTileX() => Mathf.FloorToInt(GlobalPosition.X / 32f);
    public int GetTileY() => Mathf.FloorToInt(GlobalPosition.Y / 32f);

    public Vector2 GetDestinationPosition()
    {
        var tileCenter = new Vector2(GetTileX() * 32f + 16f, GetTileY() * 32f + 16f);
        return tileCenter + DestinationOffset;
    }

    private void OnBodyEntered(Node2D body)
    {
        if (_localPlayerInside || body is not Player)
            return;

        var network = GetNodeOrNull<GameNetwork>("/root/GameNetwork");
        if (network == null || !network.IsConnected)
            return;

        _localPlayerInside = true;
        GD.Print($"[TELEPORT PAIR] Player tocou o colisor PairId={PairId}.");
        network.SendSceneTeleport(PairId);
    }

    private void OnBodyExited(Node2D body)
    {
        if (body is Player)
            _localPlayerInside = false;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_localPlayerInside)
            return;

        foreach (Node2D body in GetOverlappingBodies())
        {
            if (body is Player)
            {
                OnBodyEntered(body);
                return;
            }
        }
    }
}
