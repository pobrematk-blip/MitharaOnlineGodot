using Godot;
using System.Collections.Generic;

public partial class WorldMapUI : Control
{
    private Panel _bgPanel;
    private Panel _titleBar;
    private Button _closeButton;
    private SubViewportContainer _mapContainer;
    private SubViewport _mapViewport;
    private Camera2D _mapCamera;
    private Control _markersOverlay;
    private Label _statusLabel;
    private Node2D _player;

    private bool _arrastando;
    private Vector2 _pontoCliqueOriginal;
    private bool _draggingMap;
    private Vector2 _mapDragOrigin;
    private Vector2 _cameraOriginalPosition;

    private float _zoom = 0.18f;
    private const float MinZoom = 0.08f;
    private const float MaxZoom = 4.0f;
    private const float ZoomStep = 0.15f;

    private struct MapMarker
    {
        public int MarkerId;
        public Vector2 WorldPos;
        public string PlayerName;
        public Color Color;
    }
    private readonly List<MapMarker> _markers = new();

    private static WorldMapUI _instance;

    private static readonly Color[] MarkerColors = new Color[]
    {
        new Color(1, 0.3f, 0.3f),
        new Color(0.3f, 1, 0.3f),
        new Color(0.3f, 0.6f, 1),
        new Color(1, 1, 0.3f),
        new Color(1, 0.6f, 0.3f),
        new Color(0.8f, 0.3f, 1),
    };

    public override void _Ready()
    {
        _instance = this;
        _bgPanel = GetNode<Panel>("BgPanel");
        _titleBar = _bgPanel.GetNode<Panel>("TitleBar");
        _closeButton = _bgPanel.GetNode<Button>("CloseButton");
        _mapContainer = _bgPanel.GetNode<SubViewportContainer>("MapContainer");
        _mapViewport = _mapContainer.GetNode<SubViewport>("WorldViewport");
        _mapCamera = _mapViewport.GetNode<Camera2D>("WorldCamera");
        _markersOverlay = _bgPanel.GetNode<Control>("MarkersOverlay");
        _statusLabel = _bgPanel.GetNode<Label>("StatusLabel");

        _mapViewport.TransparentBg = true;
        _mapViewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Always;
        _mapViewport.World2D = GetWorld2D();

        _closeButton.Pressed += Close;
        _titleBar.GuiInput += OnTitleBarGuiInput;
        _markersOverlay.GuiInput += OnMarkersOverlayGuiInput;
        _markersOverlay.Draw += OnMarkersDraw;
        _markersOverlay.Resized += () => _markersOverlay.QueueRedraw();

        PrepareLayout();
        FindPlayer();
        UpdateCamera();

        var net = GetNodeOrNull<GameNetwork>("/root/GameNetwork");
        net?.SendMapMarkerRequest();
    }

    public override void _ExitTree()
    {
        _instance = null;
    }

    public static void Open()
    {
        if (_instance != null && IsInstanceValid(_instance))
        {
            _instance.Close();
            return;
        }

        var scene = ResourceLoader.Load<PackedScene>("res://ui/WorldMapUI.tscn");
        if (scene == null) return;

        var inst = scene.Instantiate();
        if (inst is not WorldMapUI wm)
        {
            inst?.QueueFree();
            return;
        }

        var main = wm.GetTree().CurrentScene;
        var hud = main?.GetNodeOrNull<CanvasLayer>("UI");
        if (hud != null)
        {
            hud.AddChild(wm);
            wm.MoveToFront();
        }
        else
        {
            main?.AddChild(wm);
        }
    }

    private void PrepareLayout()
    {
        var viewportSize = GetViewport().GetVisibleRect().Size;
        var panelSize = viewportSize * new Vector2(0.82f, 0.78f);
        panelSize.X = Mathf.Min(panelSize.X, viewportSize.X - 24f);
        panelSize.Y = Mathf.Min(panelSize.Y, viewportSize.Y - 24f);
        _bgPanel.Size = panelSize;
        _bgPanel.Position = (viewportSize - panelSize) * 0.5f;
        _bgPanel.ZIndex = 200;

        float margin = 8f;
        var cbSize = _closeButton.Size;
        _closeButton.Position = new Vector2(panelSize.X - cbSize.X - margin, margin);

        var parent = _bgPanel.GetParent();
        if (parent != null)
            parent.MoveChild(_bgPanel, parent.GetChildCount() - 1);
    }

    private void FindPlayer()
    {
        _player = GetTree().GetFirstNodeInGroup("player") as Node2D;
    }

    private void UpdateCamera()
    {
        if (_player != null)
        {
            _mapCamera.GlobalPosition = _player.GlobalPosition;
            _mapCamera.Zoom = new Vector2(_zoom, _zoom);
        }
        UpdateZoom();
    }

    public override void _Process(double delta)
    {
        if (_player == null)
        {
            FindPlayer();
            return;
        }
        if (!_draggingMap)
            _mapCamera.GlobalPosition = _player.GlobalPosition;
    }

    public override void _Input(InputEvent @event)
    {
        if (@event.IsActionPressed("ui_cancel"))
        {
            Close();
            GetViewport().SetInputAsHandled();
        }
    }

    private void Close()
    {
        _instance = null;
        QueueFree();
    }

    private void OnTitleBarGuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton btn && btn.ButtonIndex == MouseButton.Left)
        {
            _arrastando = btn.Pressed;
            if (btn.Pressed)
                _pontoCliqueOriginal = btn.Position;
        }
        else if (@event is InputEventMouseMotion motion && _arrastando)
        {
            _bgPanel.Position += motion.Position - _pontoCliqueOriginal;
        }
    }

    private void OnMarkersOverlayGuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton btn)
        {
            if (btn.ButtonIndex == MouseButton.Left)
            {
                _draggingMap = btn.Pressed;
                if (btn.Pressed)
                {
                    _mapDragOrigin = GetGlobalMousePosition();
                    _cameraOriginalPosition = _mapCamera.GlobalPosition;
                }
                GetViewport().SetInputAsHandled();
            }
            else if (btn.ButtonIndex == MouseButton.Right && btn.Pressed)
            {
                Vector2 localPos = btn.Position;
                Vector2 worldPos = ScreenToWorld(localPos);
                PlaceMarker(worldPos);
                GetViewport().SetInputAsHandled();
            }
            else if (btn.ButtonIndex == MouseButton.WheelUp && btn.Pressed)
            {
                _zoom = Mathf.Min(MaxZoom, _zoom + ZoomStep);
                UpdateZoom();
                GetViewport().SetInputAsHandled();
            }
            else if (btn.ButtonIndex == MouseButton.WheelDown && btn.Pressed)
            {
                _zoom = Mathf.Max(MinZoom, _zoom - ZoomStep);
                UpdateZoom();
                GetViewport().SetInputAsHandled();
            }
        }
        else if (@event is InputEventMouseMotion motion && _draggingMap)
        {
            Vector2 delta = (GetGlobalMousePosition() - _mapDragOrigin) / _zoom;
            _mapCamera.GlobalPosition = _cameraOriginalPosition - delta;
            _markersOverlay.QueueRedraw();
        }
    }

    private Vector2 ScreenToWorld(Vector2 screenPos)
    {
        Vector2 overlayCenter = _markersOverlay.Size / 2f;
        return _mapCamera.GlobalPosition + (screenPos - overlayCenter) / _zoom;
    }

    private Vector2 WorldToScreen(Vector2 worldPos)
    {
        Vector2 overlayCenter = _markersOverlay.Size / 2f;
        return overlayCenter + (worldPos - _mapCamera.GlobalPosition) * _zoom;
    }

    private void UpdateZoom()
    {
        _mapCamera.Zoom = new Vector2(_zoom, _zoom);
        _statusLabel.Text = $"Zoom: {(int)(_zoom * 100)}% | Clique direito para marcar | Scroll para zoom";
        _markersOverlay.QueueRedraw();
    }

    private void PlaceMarker(Vector2 worldPos)
    {
        var net = GetNodeOrNull<GameNetwork>("/root/GameNetwork");
        string playerName = "Aventureiro";
        var pc = GetNodeOrNull<PersonagemEscolhido>("/root/PersonagemEscolhido");
        if (pc != null) playerName = pc.NomePersonagem;

        net?.SendMapMarkerPlace(worldPos.X, worldPos.Y, playerName);
    }

    public static void AddOrUpdateMarker(int markerId, float worldX, float worldY, string playerName)
    {
        if (_instance == null || !IsInstanceValid(_instance)) return;

        int existing = _instance._markers.FindIndex(m => m.MarkerId == markerId);
        Color color = MarkerColors[markerId % MarkerColors.Length];

        var marker = new MapMarker
        {
            MarkerId = markerId,
            WorldPos = new Vector2(worldX, worldY),
            PlayerName = playerName,
            Color = color,
        };

        if (existing >= 0)
            _instance._markers[existing] = marker;
        else
            _instance._markers.Add(marker);

        _instance._markersOverlay.QueueRedraw();
    }

    public static void RemoveMarker(int markerId)
    {
        if (_instance == null || !IsInstanceValid(_instance)) return;
        _instance._markers.RemoveAll(m => m.MarkerId == markerId);
        _instance._markersOverlay.QueueRedraw();
    }

    public static void ClearMarkers()
    {
        if (_instance == null || !IsInstanceValid(_instance)) return;
        _instance._markers.Clear();
        _instance._markersOverlay.QueueRedraw();
    }

    private void OnMarkersDraw()
    {
        var font = ThemeDB.FallbackFont;
        int fontSize = ThemeDB.FallbackFontSize;

        foreach (var marker in _markers)
        {
            Vector2 screenPos = WorldToScreen(marker.WorldPos);
            if (screenPos.X < -50 || screenPos.X > _markersOverlay.Size.X + 50 ||
                screenPos.Y < -50 || screenPos.Y > _markersOverlay.Size.Y + 50)
                continue;

            _markersOverlay.DrawCircle(screenPos, 7f, marker.Color);
            _markersOverlay.DrawCircle(screenPos, 4f, new Color(1, 1, 1, 0.9f));

            string text = marker.PlayerName;
            var textSize = font.GetStringSize(text, HorizontalAlignment.Left, -1, fontSize);
            var textPos = new Vector2(screenPos.X - textSize.X / 2f, screenPos.Y - 14f);
            _markersOverlay.DrawString(font, textPos, text, HorizontalAlignment.Left, -1, fontSize, new Color(1, 1, 1, 0.9f));
        }
    }
}
