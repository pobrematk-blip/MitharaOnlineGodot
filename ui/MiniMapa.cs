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
        _viewport.RenderTargetUpdateMode = (SubViewport.UpdateMode)2;
        _viewport.World2D = GetWorld2D();

        float zoom = _viewport.Size.X / (WorldRadius * 2f);
        _minimapCamera.Zoom = new Vector2(zoom, zoom);

        var viewportContainer = GetNode<SubViewportContainer>("SubViewportContainer");
        viewportContainer.Visible = false;

        _borderOverlay.Draw += OnBorderDraw;
        _borderOverlay.Resized += () => _borderOverlay.QueueRedraw();
        _borderOverlay.QueueRedraw();

        _dotsOverlay.Draw += OnDotsDraw;

        FindPlayer();
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

        _updateTimer += (float)delta;
        if (_updateTimer >= 0.25f)
        {
            _updateTimer = 0f;
            UpdateLabels();
            _dotsOverlay.QueueRedraw();
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
