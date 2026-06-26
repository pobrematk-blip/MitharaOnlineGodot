using Godot;

[Tool]
public partial class TileGridOverlay : Node2D
{
    [Export] public int RangeX { get; set; } = 200;
    [Export] public int RangeY { get; set; } = 200;

    private const float TILE_SIZE = 32f;
    private static readonly Color GRID_COLOR = new(1, 1, 1, 0.15f);
    private static readonly Color AXIS_COLOR = new(1, 1, 1, 0.4f);

    public override void _Ready()
    {
        if (!Engine.IsEditorHint())
        {
            Visible = false;
            SetProcess(false);
            QueueFree();
            return;
        }
        SetProcess(true);
    }

    public override void _Process(double delta)
    {
        if (Engine.IsEditorHint())
            QueueRedraw();
    }

    public override void _Draw()
    {
        for (int x = -RangeX; x <= RangeX; x++)
        {
            float wx = x * TILE_SIZE;
            float y1 = -RangeY * TILE_SIZE;
            float y2 = RangeY * TILE_SIZE;
            var color = (x == 0) ? AXIS_COLOR : GRID_COLOR;
            DrawLine(new Vector2(wx, y1), new Vector2(wx, y2), color);
        }
        for (int y = -RangeY; y <= RangeY; y++)
        {
            float wy = y * TILE_SIZE;
            float x1 = -RangeX * TILE_SIZE;
            float x2 = RangeX * TILE_SIZE;
            var color = (y == 0) ? AXIS_COLOR : GRID_COLOR;
            DrawLine(new Vector2(x1, wy), new Vector2(x2, wy), color);
        }
    }
}
