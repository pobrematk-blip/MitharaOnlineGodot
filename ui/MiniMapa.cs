using Godot;
using System.Collections.Generic;

public partial class MiniMapa : Control
{
    private SubViewport _viewport;
    private Camera2D _minimapCamera;
    private Node2D _player;
    private Label _fpsLabel;
    private Label _pingLabel;
    private Control _borderOverlay;
    private Control _dotsOverlay;

    private Control _fullMapOverlay;
    private SubViewportContainer _fullMapContainer;
    private SubViewport _fullMapViewport;
    private Camera2D _fullMapCamera;
    private bool _fullMapOpen;

    private float _updateTimer;
    private FaccaoResource _playerFaction;

    private const float WorldRadius = 600f;
    private const float OverlaySize = 180f;
    private const float WorldToMinimapScale = OverlaySize / (WorldRadius * 2f);

    private const float EnemyDotRadius = 4f;
    private const float BossDotRadius = 7f;
    private const float NpcDotRadius = 4f;
    private const float PlayerDotRadius = 4f;

    public override void _Ready()
    {
        _viewport = GetNode<SubViewport>("SubViewportContainer/MiniViewport");
        _minimapCamera = GetNode<Camera2D>("SubViewportContainer/MiniViewport/MiniCamera");
        _fpsLabel = GetNode<Label>("FpsLabel");
        _pingLabel = GetNode<Label>("PingLabel");
        _borderOverlay = GetNode<Control>("BorderOverlay");
        _dotsOverlay = GetNode<Control>("DotsOverlay");

        _viewport.TransparentBg = true;
        _viewport.Size = new Vector2I(128, 128);
        _viewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Always;
        _viewport.World2D = GetWorld2D();

        float zoom = _viewport.Size.X / (WorldRadius * 2f);
        _minimapCamera.Zoom = new Vector2(zoom, zoom);

        _borderOverlay.Draw += OnBorderDraw;
        _borderOverlay.Resized += () => _borderOverlay.QueueRedraw();
        _borderOverlay.QueueRedraw();

        _dotsOverlay.Draw += OnDotsDraw;
        _dotsOverlay.GuiInput += OnMinimapGuiInput;

        FindPlayer();
    }

    private void OnMinimapGuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mouseEvent && mouseEvent.Pressed && mouseEvent.ButtonIndex == MouseButton.Left)
        {
            ToggleFullMap();
        }
    }

    private void ToggleFullMap()
    {
        if (_fullMapOpen)
            CloseFullMap();
        else
            OpenFullMap();
    }

    private void OpenFullMap()
    {
        if (_fullMapOpen) return;
        _fullMapOpen = true;

        _fullMapOverlay = new Control();
        _fullMapOverlay.Name = "FullMapOverlay";
        _fullMapOverlay.SetAnchorsPreset(LayoutPreset.FullRect);
        _fullMapOverlay.MouseFilter = MouseFilterEnum.Stop;

        var bg = new ColorRect();
        bg.Color = new Color(0, 0, 0, 0.85f);
        bg.SetAnchorsPreset(LayoutPreset.FullRect);
        _fullMapOverlay.AddChild(bg);

        var title = new Label();
        title.Text = "MAPA DO MUNDO";
        title.HorizontalAlignment = HorizontalAlignment.Center;
        title.SetAnchorsPreset(LayoutPreset.TopWide);
        title.Position = new Vector2(0, 12);
        title.AddThemeColorOverride("font_color", new Color(1, 1, 1, 0.9f));
        title.AddThemeFontSizeOverride("font_size", 18);
        _fullMapOverlay.AddChild(title);

        var closeBtn = new Button();
        closeBtn.Text = "Fechar";
        closeBtn.Position = new Vector2(12, 10);
        closeBtn.Pressed += CloseFullMap;
        _fullMapOverlay.AddChild(closeBtn);

        _fullMapContainer = new SubViewportContainer();
        _fullMapContainer.Stretch = true;
        _fullMapContainer.MouseFilter = MouseFilterEnum.Ignore;
        _fullMapOverlay.AddChild(_fullMapContainer);

        _fullMapViewport = new SubViewport();
        _fullMapViewport.TransparentBg = true;
        _fullMapViewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Always;
        _fullMapViewport.World2D = GetWorld2D();
        _fullMapContainer.AddChild(_fullMapViewport);

        _fullMapCamera = new Camera2D();
        _fullMapViewport.AddChild(_fullMapCamera);

        AddChild(_fullMapOverlay);

        GetViewport().SizeChanged += OnFullMapViewportResized;
        Callable.From(UpdateFullMapLayout).CallDeferred();
    }

    private void UpdateFullMapLayout()
    {
        if (_fullMapOverlay == null || _fullMapContainer == null || _fullMapViewport == null) return;

        Vector2 viewportSize = GetViewportRect().Size;
        float mapSize = Mathf.Min(viewportSize.X, viewportSize.Y) * 0.85f;
        mapSize = Mathf.Min(mapSize, 700f);

        _fullMapContainer.Size = new Vector2(mapSize, mapSize);
        _fullMapContainer.Position = new Vector2(
            (viewportSize.X - mapSize) / 2f,
            (viewportSize.Y - mapSize) / 2f
        );

        Vector2I vpSize = new Vector2I((int)mapSize, (int)mapSize);
        _fullMapViewport.Size = vpSize;

        float zoom = mapSize / (WorldRadius * 4f);
        _fullMapCamera.Zoom = new Vector2(zoom, zoom);

        if (_player != null)
            _fullMapCamera.GlobalPosition = _player.GlobalPosition;
    }

    private void OnFullMapViewportResized()
    {
        if (_fullMapOpen)
            Callable.From(UpdateFullMapLayout).CallDeferred();
    }

    private void CloseFullMap()
    {
        _fullMapOpen = false;
        GetViewport().SizeChanged -= OnFullMapViewportResized;
        if (_fullMapOverlay != null)
        {
            _fullMapOverlay.QueueFree();
            _fullMapOverlay = null;
            _fullMapContainer = null;
            _fullMapViewport = null;
            _fullMapCamera = null;
        }
    }

    private void FindPlayer()
    {
        if (_player != null) return;
        _player = GetTree().GetFirstNodeInGroup("player") as Node2D;
        if (_player is Player p)
        {
            _playerFaction = p.FaccaoAtiva;
        }
    }

    public override void _Process(double delta)
    {
        if (_player == null)
        {
            FindPlayer();
            if (_player == null) return;
        }

        _minimapCamera.GlobalPosition = _player.GlobalPosition;

        if (_fullMapCamera != null)
            _fullMapCamera.GlobalPosition = _player.GlobalPosition;

        _updateTimer += (float)delta;
        if (_updateTimer >= 0.25f)
        {
            _updateTimer = 0f;
            UpdateLabels();
            _dotsOverlay.QueueRedraw();
        }
    }

    public override void _Input(InputEvent @event)
    {
        if (_fullMapOpen && @event.IsActionPressed("ui_cancel"))
        {
            CloseFullMap();
            GetViewport().SetInputAsHandled();
        }
    }

    private void UpdateLabels()
    {
        _fpsLabel.Text = $"FPS: {Engine.GetFramesPerSecond()}";
        var net = GetNodeOrNull<GameNetwork>("/root/GameNetwork");
        _pingLabel.Text = net != null && net.IsConnected ? $"Ping: {net.ServerPing}ms" : "Ping: Offline";
    }

    private Vector2 WorldToMinimap(Vector2 worldPos)
    {
        Vector2 relative = worldPos - _player.GlobalPosition;
        return new Vector2(90f, 90f) + relative * WorldToMinimapScale;
    }

    private void OnBorderDraw()
    {
        var size = _borderOverlay.Size;
        float radius = Mathf.Min(size.X, size.Y) / 2f - 2f;
        Vector2 center = size / 2f;

        _borderOverlay.DrawCircle(center, radius + 2, new Color(0, 0, 0, 0.4f));
        _borderOverlay.DrawArc(center, radius, 0, Mathf.Tau, 64, new Color(1, 1, 1, 0.8f), 2f);
    }

    private void OnDotsDraw()
    {
        if (_player == null) return;

        float circleRadius = Mathf.Min(_dotsOverlay.Size.X, _dotsOverlay.Size.Y) / 2f - 2f;
        Vector2 overlayCenter = _dotsOverlay.Size / 2f;

        var inimigos = GetTree().GetNodesInGroup("Inimigos");
        foreach (var node in inimigos)
        {
            if (node is Inimigo inimigo && GodotObject.IsInstanceValid(inimigo))
            {
                Vector2 pos = WorldToMinimap(inimigo.GlobalPosition);
                if (pos.DistanceTo(overlayCenter) > circleRadius) continue;

                float radius = inimigo.IsBoss ? BossDotRadius : EnemyDotRadius;
                _dotsOverlay.DrawCircle(pos, radius, new Color(1, 0, 0, 0.9f));
            }
        }

        var npcs = GetTree().GetNodesInGroup("NPC");
        foreach (var node in npcs)
        {
            if (node is Node2D npc && GodotObject.IsInstanceValid(npc) && npc != _player)
            {
                Vector2 pos = WorldToMinimap(npc.GlobalPosition);
                if (pos.DistanceTo(overlayCenter) > circleRadius) continue;

                _dotsOverlay.DrawCircle(pos, NpcDotRadius, new Color(1, 1, 0, 0.9f));
            }
        }

        var players = GetTree().GetNodesInGroup("player");
        foreach (var node in players)
        {
            if (node == _player || !GodotObject.IsInstanceValid(node)) continue;

            if (node is Player otherPlayer)
            {
                Vector2 pos = WorldToMinimap(otherPlayer.GlobalPosition);
                if (pos.DistanceTo(overlayCenter) > circleRadius) continue;

                bool sameFaction = _playerFaction != null &&
                    otherPlayer.FaccaoAtiva != null &&
                    _playerFaction.MesmaFaccao(otherPlayer.FaccaoAtiva);

                Color dotColor = sameFaction
                    ? new Color(0, 1, 0, 0.9f)
                    : new Color(0.5f, 0, 0.5f, 0.9f);

                _dotsOverlay.DrawCircle(pos, PlayerDotRadius, dotColor);
            }
        }
    }
}
