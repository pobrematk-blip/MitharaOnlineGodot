using Godot;
using System.Collections.Generic;
using Mithara.Network;

public partial class MapEditor : Control
{
    private GameNetwork _network;
    private bool _active;
    private Vector2I _selectedTile = new(-1, -1);
    private bool _showingButtons;

    private ColorRect _btnBlock;
    private ColorRect _btnTeleport;
    private ColorRect _btnNpc;
    private Label _statusLabel;

    private readonly Dictionary<Vector2I, byte> _tiles = new();

    private const float TILE_SIZE = 32f;
    private static readonly Color GRID_COLOR = new(1, 1, 1, 0.12f);
    private static readonly Color BLOCK_COLOR = new(1, 0, 0, 0.4f);
    private static readonly Color TELEPORT_COLOR = new(0, 1, 0, 0.4f);
    private static readonly Color NPC_COLOR = new(0.4f, 0.7f, 1f, 0.4f);
    private static readonly Color SELECT_COLOR = new(1, 1, 0, 0.35f);
    private static readonly Color GRID_AXIS_COLOR = new(1, 1, 1, 0.35f);

    private string CurrentScene { get; set; } = "main";

    public override void _EnterTree()
    {
        GD.Print("[MAPEDITOR] _EnterTree executado!");
    }

    public override void _Ready()
    {
        GD.Print("[MAPEDITOR] _Ready executado!");

        _network = GetNodeOrNull<GameNetwork>("/root/GameNetwork");
        GD.Print($"[MAPEDITOR] GameNetwork encontrado: {_network != null}");

        _btnBlock = GetNode<ColorRect>("BtnBlock");
        _btnTeleport = GetNode<ColorRect>("BtnTeleport");
        _btnNpc = GetNode<ColorRect>("BtnNpc");

        var labelNode = new Label();
        labelNode.Name = "StatusLabel";
        labelNode.Text = "MAP EDITOR [F12]";
        labelNode.Position = new Vector2(10, 10);
        labelNode.AddThemeColorOverride("font_color", Colors.Yellow);
        labelNode.AddThemeFontSizeOverride("font_size", 16);
        AddChild(labelNode);
        _statusLabel = labelNode;
        _statusLabel.Visible = false;

        _btnBlock.GuiInput += (ev) => OnButtonInput(ev, 0);
        _btnTeleport.GuiInput += (ev) => OnButtonInput(ev, 1);
        _btnNpc.GuiInput += (ev) => OnButtonInput(ev, 2);

        HideButtons();

        _active = false;
        Visible = false;
        MouseFilter = MouseFilterEnum.Pass;

        if (!InputMap.HasAction("toggle_map_editor"))
        {
            InputMap.AddAction("toggle_map_editor");
            var inputEv = new InputEventKey();
            inputEv.Keycode = Key.F12;
            InputMap.ActionAddEvent("toggle_map_editor", inputEv);
            GD.Print("[MAPEDITOR] Ação 'toggle_map_editor' registrada para F12");
        }
        else
        {
            GD.Print("[MAPEDITOR] Ação 'toggle_map_editor' já existia");
        }

        SetProcess(true);
        GD.Print("[MAPEDITOR] _Ready concluído!");
    }

    public override void _Process(double delta)
    {
        if (Input.IsActionJustPressed("toggle_map_editor"))
        {
            GD.Print("[MAPEDITOR] toggle_map_editor acionado!");
            ToggleEditor();
        }
    }

    public override void _ExitTree()
    {
        if (_network != null)
        {
            _network.OnMapEditorTileData -= OnTileData;
            _network.OnMapEditorTileUpdate -= OnTileUpdate;
            _network.OnSceneChange -= OnGameSceneChange;
        }
    }

    private void OnGameSceneChange(string sceneName, float x, float y)
    {
        CurrentScene = sceneName;
        if (_active)
        {
            _tiles.Clear();
            RequestTiles();
        }
    }

    public override void _Input(InputEvent ev)
    {
        if (!_active) return;

        if (ev is InputEventMouseButton mb && mb.Pressed)
        {
            if (mb.ButtonIndex == MouseButton.Left)
            {
                if (!_showingButtons)
                {
                    SelectTile(mb.Position);
                    AcceptEvent();
                }
            }
            else if (mb.ButtonIndex == MouseButton.Right)
            {
                DeselectTile();
                AcceptEvent();
            }
        }
    }

    private void ToggleEditor()
    {
        _active = !_active;
        Visible = _active;

        if (_active)
        {
            MouseFilter = MouseFilterEnum.Stop;
            _statusLabel.Visible = true;
            DeselectTile();
            RequestTiles();
            GD.Print("[MAPEDITOR] Ativado");
        }
        else
        {
            MouseFilter = MouseFilterEnum.Pass;
            _statusLabel.Visible = false;
            HideButtons();
            GD.Print("[MAPEDITOR] Desativado");
        }

        QueueRedraw();
    }

    private void RequestTiles()
    {
        _network?.SendMapEditorRequestTiles(CurrentScene);
    }

    private void SelectTile(Vector2 mousePos)
    {
        var worldPos = ScreenToWorld(mousePos);
        _selectedTile = WorldToTile(worldPos);
        _showingButtons = true;
        ShowButtons(mousePos);
        QueueRedraw();
    }

    private void DeselectTile()
    {
        _selectedTile = new Vector2I(-1, -1);
        _showingButtons = false;
        HideButtons();
        QueueRedraw();
    }

    private void ShowButtons(Vector2 screenPos)
    {
        float btnSize = TILE_SIZE * 1.5f;
        float gap = 4f;
        float offsetX = screenPos.X + 40f;
        float startY = screenPos.Y - btnSize * 1.5f - gap;

        _btnBlock.Position = new Vector2(offsetX, startY);
        _btnBlock.Size = new Vector2(btnSize, btnSize);
        _btnBlock.Visible = true;

        _btnTeleport.Position = new Vector2(offsetX, startY + btnSize + gap);
        _btnTeleport.Size = new Vector2(btnSize, btnSize);
        _btnTeleport.Visible = true;

        _btnNpc.Position = new Vector2(offsetX, startY + (btnSize + gap) * 2);
        _btnNpc.Size = new Vector2(btnSize, btnSize);
        _btnNpc.Visible = true;
    }

    private void HideButtons()
    {
        _btnBlock.Visible = false;
        _btnTeleport.Visible = false;
        _btnNpc.Visible = false;
    }

    private void OnButtonInput(InputEvent ev, byte type)
    {
        if (ev is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
        {
            var key = _selectedTile;
            bool remove = _tiles.TryGetValue(key, out var existing) && existing == type;

            if (remove)
                _tiles.Remove(key);
            else
                _tiles[key] = type;

            _network?.SendMapEditorPlaceTile(CurrentScene, key.X, key.Y, type, remove);
            DeselectTile();
            QueueRedraw();
        }
    }

    public override void _Draw()
    {
        if (!_active) return;

        var camera = GetViewport().GetCamera2D();
        if (camera == null) return;

        var viewportSize = GetViewport().GetVisibleRect().Size;
        var screenCenter = viewportSize * 0.5f;
        float zoom = camera.Zoom.X;

        Vector2 topLeft = camera.GlobalPosition - screenCenter / zoom;
        Vector2 bottomRight = camera.GlobalPosition + screenCenter / zoom;

        int startX = Mathf.FloorToInt(topLeft.X / TILE_SIZE) - 1;
        int startY = Mathf.FloorToInt(topLeft.Y / TILE_SIZE) - 1;
        int endX = Mathf.FloorToInt(bottomRight.X / TILE_SIZE) + 1;
        int endY = Mathf.FloorToInt(bottomRight.Y / TILE_SIZE) + 1;

        foreach (var kvp in _tiles)
        {
            var tile = kvp.Key;
            if (tile.X < startX || tile.X > endX || tile.Y < startY || tile.Y > endY)
                continue;

            var worldPos = new Vector2(tile.X * TILE_SIZE, tile.Y * TILE_SIZE);
            var screenPos = screenCenter + (worldPos - camera.GlobalPosition) * zoom;
            var size = Vector2.One * TILE_SIZE * zoom;

            var color = kvp.Value switch
            {
                0 => BLOCK_COLOR,
                1 => TELEPORT_COLOR,
                2 => NPC_COLOR,
                _ => Colors.Transparent
            };

            DrawRect(new Rect2(screenPos, size), color);
        }

        if (_selectedTile.X >= 0 && _selectedTile.Y >= 0)
        {
            var worldPos = new Vector2(_selectedTile.X * TILE_SIZE, _selectedTile.Y * TILE_SIZE);
            var screenPos = screenCenter + (worldPos - camera.GlobalPosition) * zoom;
            DrawRect(new Rect2(screenPos, Vector2.One * TILE_SIZE * zoom), SELECT_COLOR, false, 2f);
        }

        for (int x = startX; x <= endX; x++)
        {
            float worldX = x * TILE_SIZE;
            float screenX = screenCenter.X + (worldX - camera.GlobalPosition.X) * zoom;
            var color = (x == 0) ? GRID_AXIS_COLOR : GRID_COLOR;
            DrawLine(new Vector2(screenX, 0), new Vector2(screenX, viewportSize.Y), color);
        }

        for (int y = startY; y <= endY; y++)
        {
            float worldY = y * TILE_SIZE;
            float screenY = screenCenter.Y + (worldY - camera.GlobalPosition.Y) * zoom;
            var color = (y == 0) ? GRID_AXIS_COLOR : GRID_COLOR;
            DrawLine(new Vector2(0, screenY), new Vector2(viewportSize.X, screenY), color);
        }
    }

    private Vector2 ScreenToWorld(Vector2 screenPos)
    {
        var camera = GetViewport().GetCamera2D();
        var viewportSize = GetViewport().GetVisibleRect().Size;
        return camera.GlobalPosition + (screenPos - viewportSize * 0.5f) / camera.Zoom;
    }

    private static Vector2I WorldToTile(Vector2 worldPos)
    {
        return new Vector2I(
            Mathf.FloorToInt(worldPos.X / TILE_SIZE),
            Mathf.FloorToInt(worldPos.Y / TILE_SIZE)
        );
    }

    private void OnTileData(string sceneName, Godot.Collections.Array<Godot.Collections.Dictionary> tiles)
    {
        if (sceneName != CurrentScene) return;
        _tiles.Clear();
        foreach (var t in tiles)
        {
            var key = new Vector2I((int)t["tileX"], (int)t["tileY"]);
            _tiles[key] = (byte)(int)t["type"];
        }
        QueueRedraw();
    }

    private void OnTileUpdate(string sceneName, int tileX, int tileY, byte type, bool removed)
    {
        if (sceneName != CurrentScene) return;
        var key = new Vector2I(tileX, tileY);
        if (removed)
            _tiles.Remove(key);
        else
            _tiles[key] = type;
        QueueRedraw();
    }
}
