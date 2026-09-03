using Godot;

[Tool]
public partial class DungeonFogConfig : CanvasLayer
{
    public enum FogDirection { None, Up, Down, Left, Right }

    [Export] public int FogIndex { get; set; } = 1;
    [Export(PropertyHint.Range, "0,100,1")] public int Density { get; set; } = 50;
    [Export] public FogDirection MoveDirection { get; set; } = FogDirection.None;
    [Export(PropertyHint.Range, "0,500,1")] public float MoveSpeed { get; set; } = 30f;

    private TextureRect _fogOverlay;
    private Vector2 _scrollOffset;
    private int _lastFogIndex = -1;
    private int _lastDensity = -1;

    private bool _debugged = false;

    public override void _Ready()
    {
        _fogOverlay = GetNode<TextureRect>("FogOverlay");
        _fogOverlay.TextureFilter = CanvasItem.TextureFilterEnum.Nearest;
        _fogOverlay.TextureRepeat = CanvasItem.TextureRepeatEnum.Enabled;
        _fogOverlay.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
        _fogOverlay.StretchMode = TextureRect.StretchModeEnum.Tile;
        _fogOverlay.MouseFilter = Control.MouseFilterEnum.Ignore;
        _lastFogIndex = -1;
        _lastDensity = -1;
        GD.Print($"[FOG] _Ready: FogIndex={FogIndex}, Density={Density}, MoveDirection={MoveDirection}, overlay={_fogOverlay != null}");
        ApplyFog();
    }

    public override void _Process(double delta)
    {
        if (_fogOverlay == null) return;

        var vpSize = GetViewport().GetVisibleRect().Size;

        if (!_debugged)
        {
            GD.Print($"[FOG] _Process frame1: vpSize={vpSize}, Texture={_fogOverlay.Texture != null}, MoveDir={MoveDirection}, Position={_fogOverlay.Position}, Size={_fogOverlay.Size}, Visible={_fogOverlay.Visible}, Modulate={_fogOverlay.Modulate}");
            _debugged = true;
        }

        if (MoveDirection != FogDirection.None && _fogOverlay.Texture != null)
        {
            float speed = MoveSpeed * (float)delta;
            Vector2 dir = MoveDirection switch
            {
                FogDirection.Up => Vector2.Up,
                FogDirection.Down => Vector2.Down,
                FogDirection.Left => Vector2.Left,
                FogDirection.Right => Vector2.Right,
                _ => Vector2.Zero
            };
            _scrollOffset += dir * speed;
            _fogOverlay.Position = _scrollOffset;
            _fogOverlay.Size = vpSize + new Vector2(Mathf.Abs(_scrollOffset.X), Mathf.Abs(_scrollOffset.Y)) * 2f;
        }
        else
        {
            _fogOverlay.Position = Vector2.Zero;
            _fogOverlay.Size = vpSize;
        }

        ApplyFog();
    }

    private void ApplyFog()
    {
        if (_fogOverlay == null) return;
        if (_lastFogIndex == FogIndex && _lastDensity == Density) return;

        _lastFogIndex = FogIndex;
        _lastDensity = Density;

        var path = $"res://Fog/{FogIndex}.png";
        if (!ResourceLoader.Exists(path))
        {
            GD.PrintErr($"[FOG] Texture not found: {path}");
            _fogOverlay.Texture = null;
            return;
        }

        _fogOverlay.Texture = GD.Load<Texture2D>(path);
        float alpha = Density / 100f;
        _fogOverlay.Modulate = new Color(1f, 1f, 1f, alpha);
        GD.Print($"[FOG] ApplyFog: path={path}, texture={_fogOverlay.Texture != null}, alpha={alpha}, Modulate={_fogOverlay.Modulate}");
    }
}
