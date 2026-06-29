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
    private Control _cashBtn;
    private Label _vipLabel;
    private SubViewportContainer _viewportContainer;

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

    private const float Padding = 3f;
    private const float EdgeMargin = 3f;
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
        _cashBtn = GetNode<Control>("CashBtn");
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
        _cashBtn.GuiInput += OnCashBtnGuiInput;

        Resized += OnRootResized;
        GetTree().Root.SizeChanged += ManterNaBordaDireita;

        AtualizarLayout();
        ManterNaBordaDireita();
        FindPlayer();
    }

    private void OnRootResized()
    {
        AtualizarLayout();
    }

    private void AtualizarLayout()
    {
        float mapSize = Mathf.Max(MinSize, Size.X - Padding);
        float mapHeight = Mathf.Max(MinSize, Size.Y - 80f);
        float size = Mathf.Min(mapSize, mapHeight);
        Vector2 mapPosition = new(
            Mathf.Max(0f, Size.X - size - EdgeMargin),
            Padding
        );

        _viewportContainer.Size = new Vector2(size, size);
        _viewportContainer.Position = mapPosition;

        _borderOverlay.Size = new Vector2(size, size);
        _borderOverlay.Position = mapPosition;

        _dotsOverlay.Size = new Vector2(size, size);
        _dotsOverlay.Position = mapPosition;

        var playerDot = GetNodeOrNull<ColorRect>("PlayerDot");
        if (playerDot != null)
        {
            float dotSize = 6f;
            playerDot.Position = mapPosition + new Vector2(size / 2f - dotSize / 2f, size / 2f - dotSize / 2f);
            playerDot.Size = new Vector2(dotSize, dotSize);
        }

        _resizeHandle.Position = new Vector2(
            Mathf.Max(MinSize, Size.X) - 16f,
            Mathf.Max(MinSize, Size.Y) - 16f
        );

        if (_cashBtn != null)
        {
            Vector2 cashSize = _cashBtn.Size;
            if (cashSize.X <= 0 || cashSize.Y <= 0)
                cashSize = _cashBtn.CustomMinimumSize;

            _cashBtn.Size = cashSize;
            _cashBtn.Position = new Vector2(
                Mathf.Max(0f, Size.X - cashSize.X - EdgeMargin),
                Mathf.Max(Padding + size + 8f, Size.Y - cashSize.Y - EdgeMargin)
            );
        }

        AtualizarZoomCamera();
    }

    private void ManterNaBordaDireita()
    {
        SetAnchorsPreset(LayoutPreset.TopRight, false);
        OffsetRight = -EdgeMargin;
        OffsetLeft = OffsetRight - Mathf.Max(Size.X, MinSize + 20f);
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
            ManterNaBordaDireita();
            GetViewport().SetInputAsHandled();
        }
    }

    private void OnDotsGuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton btn && btn.Pressed)
        {
            if (btn.ButtonIndex == MouseButton.Left)
            {
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
        WorldMapUI.Open();
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
        if (Input.IsActionJustPressed("mapa"))
        {
            ToggleFullMap();
            GetViewport()?.SetInputAsHandled();
        }

        if (_player == null)
        {
            FindPlayer();
            if (_player == null) return;
        }

        _minimapCamera.GlobalPosition = _player.GlobalPosition;

        _updateTimer += (float)delta;
        if (_updateTimer >= 0.25f)
        {
            _updateTimer = 0f;
            UpdateLabels();
            _dotsOverlay.QueueRedraw();
            AtualizarVipIndicator();
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

    private CanvasLayer _hudLayer;

    private CanvasLayer ObterHudLayer()
    {
        if (_hudLayer != null && IsInstanceValid(_hudLayer))
            return _hudLayer;

        var scene = GetTree().CurrentScene;
        _hudLayer = scene != null ? scene.GetNodeOrNull<CanvasLayer>("HUD") : null;
        return _hudLayer;
    }

    private void OnCashBtnGuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mouse && mouse.Pressed && mouse.ButtonIndex == MouseButton.Left)
        {
            var hud = ObterHudLayer();
            if (hud == null) return;

            var aberta = hud.FindChild("LojaCashUI", true, false) as LojaCashUI;
            if (aberta != null && IsInstanceValid(aberta))
            {
                aberta.Abrir();
                aberta.Visible = true;
                aberta.MoveToFront();
                return;
            }

            var lojaCena = ResourceLoader.Load<PackedScene>(SceneConstants.LOJA_CASH_UI);
            if (lojaCena == null) return;
            var instancia = lojaCena.Instantiate();
            if (instancia is not LojaCashUI loja)
            {
                GD.PushError("[CASH] A raiz de LojaCashUI.tscn precisa usar o script LojaCashUI.cs.");
                instancia?.QueueFree();
                return;
            }
            hud.AddChild(loja);
            loja.Abrir();
        }
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

        _borderOverlay.DrawCircle(center, radius + 3f, MitharaUiTheme.PanelBg);
        _borderOverlay.DrawArc(center, radius + 1f, 0, Mathf.Tau, 96, MitharaUiTheme.Border, 3f);
        _borderOverlay.DrawArc(center, radius - 3f, 0, Mathf.Tau, 96, new Color(MitharaUiTheme.Accent.R, MitharaUiTheme.Accent.G, MitharaUiTheme.Accent.B, 0.45f), 1f);
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

    public override void _ExitTree()
    {
        GetTree().Root.SizeChanged -= ManterNaBordaDireita;
    }
}
