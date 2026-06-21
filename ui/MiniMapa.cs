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
    private Control _resizeHandle;
    private TextureButton _cashBtn;
    private Label _vipLabel;
    private SubViewportContainer _viewportContainer;

    private Control _fullMapOverlay;
    private SubViewportContainer _fullMapContainer;
    private SubViewport _fullMapViewport;
    private Camera2D _fullMapCamera;
    private bool _fullMapOpen;

    private float _updateTimer;
    private FaccaoResource _playerFaction;

    private float _worldRadius = 1200f;
    private const float MinWorldRadius = 300f;
    private const float MaxWorldRadius = 5000f;

    private bool _resizing;
    private Vector2 _resizeStartMouse;
    private Vector2 _resizeStartSize;

    private const float EnemyDotRadius = 4f;
    private const float BossDotRadius = 7f;
    private const float NpcDotRadius = 4f;
    private const float PlayerDotRadius = 4f;

    private const float Padding = 10f;
    private const float MinSize = 150f;

    public override void _Ready()
    {
        _viewportContainer = GetNode<SubViewportContainer>("SubViewportContainer");
        _viewport = GetNode<SubViewport>("SubViewportContainer/MiniViewport");
        _minimapCamera = GetNode<Camera2D>("SubViewportContainer/MiniViewport/MiniCamera");
        _fpsLabel = GetNode<Label>("FpsLabel");
        _pingLabel = GetNode<Label>("PingLabel");
        _borderOverlay = GetNode<Control>("BorderOverlay");
        _dotsOverlay = GetNode<Control>("DotsOverlay");
        _resizeHandle = GetNode<Control>("ResizeHandle");
        _cashBtn = GetNode<TextureButton>("CashBtn");
        _vipLabel = GetNode<Label>("VipLabel");

        _viewport.TransparentBg = true;
        _viewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Always;
        _viewport.World2D = GetWorld2D();

        _borderOverlay.Draw += OnBorderDraw;
        _borderOverlay.Resized += () => _borderOverlay.QueueRedraw();
        _borderOverlay.QueueRedraw();

        _dotsOverlay.Draw += OnDotsDraw;
        _dotsOverlay.GuiInput += OnDotsGuiInput;

        _resizeHandle.GuiInput += OnResizeHandleInput;
        _resizeHandle.Draw += OnResizeHandleDraw;
        _resizeHandle.Resized += () => _resizeHandle.QueueRedraw();
        _cashBtn.Pressed += OnCashBtnPressed;

        Resized += OnRootResized;

        AtualizarLayout();
        FindPlayer();
    }

    private void OnRootResized()
    {
        AtualizarLayout();
    }

    private void AtualizarLayout()
    {
        float mapSize = Mathf.Max(MinSize, Size.X - 20f);
        float mapHeight = Mathf.Max(MinSize, Size.Y - 80f);
        float size = Mathf.Min(mapSize, mapHeight);

        _viewportContainer.Size = new Vector2(size, size);
        _viewportContainer.Position = new Vector2(Padding, Padding);

        _borderOverlay.Size = new Vector2(size, size);
        _borderOverlay.Position = new Vector2(Padding, Padding);

        _dotsOverlay.Size = new Vector2(size, size);
        _dotsOverlay.Position = new Vector2(Padding, Padding);

        var playerDot = GetNodeOrNull<ColorRect>("PlayerDot");
        if (playerDot != null)
        {
            float dotSize = 6f;
            float center = size / 2f + Padding - dotSize / 2f;
            playerDot.Position = new Vector2(center, center);
            playerDot.Size = new Vector2(dotSize, dotSize);
        }

        _resizeHandle.Position = new Vector2(
            Mathf.Max(MinSize, Size.X) - 16f,
            Mathf.Max(MinSize, Size.Y) - 16f
        );

        AtualizarZoomCamera();
    }

    private void OnResizeHandleInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton btn && btn.ButtonIndex == MouseButton.Left)
        {
            _resizing = btn.Pressed;
            if (btn.Pressed)
            {
                _resizeStartMouse = GetGlobalMousePosition();
                _resizeStartSize = Size;
            }
            GetViewport().SetInputAsHandled();
        }
        else if (@event is InputEventMouseMotion motion && _resizing)
        {
            Vector2 delta = GetGlobalMousePosition() - _resizeStartMouse;
            Size = new Vector2(
                Mathf.Max(MinSize + 20f, _resizeStartSize.X + delta.X),
                Mathf.Max(MinSize + 80f, _resizeStartSize.Y + delta.Y)
            );
            GetViewport().SetInputAsHandled();
        }
    }

    private void OnDotsGuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton btn && btn.Pressed)
        {
            if (btn.ButtonIndex == MouseButton.Left)
            {
                ToggleFullMap();
                GetViewport().SetInputAsHandled();
            }
            else if (btn.ButtonIndex == MouseButton.WheelUp)
            {
                _worldRadius = Mathf.Max(MinWorldRadius, _worldRadius * 0.8f);
                AtualizarZoomCamera();
                _dotsOverlay.QueueRedraw();
                GetViewport().SetInputAsHandled();
            }
            else if (btn.ButtonIndex == MouseButton.WheelDown)
            {
                _worldRadius = Mathf.Min(MaxWorldRadius, _worldRadius * 1.25f);
                AtualizarZoomCamera();
                _dotsOverlay.QueueRedraw();
                GetViewport().SetInputAsHandled();
            }
        }
    }

    private void AtualizarZoomCamera()
    {
        float size = _viewport.Size.X;
        if (size <= 0) size = 180f;

        float zoom = size / (_worldRadius * 2f);
        _minimapCamera.Zoom = new Vector2(zoom, zoom);
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

        float zoom = mapSize / (_worldRadius * 4f);
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
            AtualizarVipIndicator();
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

    private void AtualizarVipIndicator()
    {
        if (_vipLabel == null) return;
        var net = GetNodeOrNull<GameNetwork>("/root/GameNetwork");
        bool ativo = net != null && net.IsVipActive;
        _vipLabel.Visible = ativo;
        if (ativo)
        {
            var expiry = System.DateTime.FromBinary(net.VipExpiryBinary);
            var remaining = expiry - System.DateTime.UtcNow;
            if (remaining.TotalDays >= 1)
                _vipLabel.Text = $"⭐ VIP ({remaining.Days}d {remaining.Hours}h)";
            else if (remaining.TotalHours >= 1)
                _vipLabel.Text = $"⭐ VIP ({remaining.Hours}h {remaining.Minutes}m)";
            else
                _vipLabel.Text = $"⭐ VIP ({remaining.Minutes}m {remaining.Seconds}s)";
        }
    }

    private void OnCashBtnPressed()
    {
        var lojaCena = ResourceLoader.Load<PackedScene>(SceneConstants.LOJA_CASH_UI);
        if (lojaCena == null) return;
        var loja = lojaCena.Instantiate<LojaCashUI>();
        GetTree().CurrentScene.AddChild(loja);
        loja.Abrir();
    }

    private Vector2 WorldToMinimap(Vector2 worldPos)
    {
        float overlaySize = _dotsOverlay.Size.X;
        if (overlaySize <= 0) overlaySize = 180f;

        float scale = overlaySize / (_worldRadius * 2f);
        Vector2 center = _dotsOverlay.Size / 2f;
        Vector2 relative = worldPos - _player.GlobalPosition;
        return center + relative * scale;
    }

    private void OnResizeHandleDraw()
    {
        var size = _resizeHandle.Size;
        float right = size.X;
        float bottom = size.Y;
        float line = 4f;
        float gap = 3f;

        for (int i = 0; i < 3; i++)
        {
            float x = right - (i + 1) * (line + gap) + gap;
            float y = bottom - (i + 1) * (line + gap) + gap;
            _resizeHandle.DrawLine(new Vector2(x, bottom), new Vector2(right, y), new Color(1, 1, 1, 0.5f), 2f);
        }
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
