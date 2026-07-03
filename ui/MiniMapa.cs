using Godot;
using System.Collections.Generic;

public partial class MiniMapa : Control
{
    private SubViewport _viewport;
    private Camera2D _minimapCamera;
    private Node2D _player;
    private Label _fpsLabel;
    private Label _pingLabel;
    private Panel _statsPanel;
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
    private const string SettingsPath = "user://settings.cfg";
    private const string SectionUI = "UI";
    private static readonly Color CorParty = new(0.35f, 0.85f, 1.0f, 0.95f);
    private static readonly Color CorGuild = new(0.35f, 1.0f, 0.35f, 0.95f);
    private static readonly Color CorFaccaoInimiga = new(0.78f, 0.28f, 1.0f, 0.95f);
    private static readonly Color CorPlayerNormal = new(1.0f, 1.0f, 1.0f, 0.95f);
    private static readonly Color CorBoss = new(1.0f, 0.48f, 0.08f, 0.95f);

    public override void _Ready()
    {
        _viewportContainer = GetNode<SubViewportContainer>("SubViewportContainer");
        _viewport = GetNode<SubViewport>("SubViewportContainer/MiniViewport");
        _minimapCamera = GetNode<Camera2D>("SubViewportContainer/MiniViewport/MiniCamera");
        _statsPanel = GetNodeOrNull<Panel>("StatsPanel");
        _fpsLabel = GetNode<Label>("StatsPanel/FpsLabel");
        _pingLabel = GetNode<Label>("StatsPanel/PingLabel");
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
        ConfigurarStatsPanel();
        CarregarVisibilidadeStats();

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

    private void ConfigurarStatsPanel()
    {
        if (_statsPanel != null)
        {
            var style = new StyleBoxFlat
            {
                BgColor = new Color(0.015f, 0.018f, 0.025f, 0.88f),
                BorderColor = new Color(1.0f, 0.72f, 0.16f, 0.95f),
                CornerRadiusTopLeft = 4,
                CornerRadiusTopRight = 4,
                CornerRadiusBottomLeft = 4,
                CornerRadiusBottomRight = 4,
                ShadowColor = new Color(0, 0, 0, 0.55f),
                ShadowSize = 5,
            };
            style.SetBorderWidthAll(1);
            style.ContentMarginLeft = 7;
            style.ContentMarginRight = 7;
            style.ContentMarginTop = 4;
            style.ContentMarginBottom = 4;
            _statsPanel.AddThemeStyleboxOverride("panel", style);
        }

        ConfigurarLabelStats(_fpsLabel, new Color(0.3f, 1.0f, 0.35f, 1));
        ConfigurarLabelStats(_pingLabel, new Color(0.35f, 0.82f, 1.0f, 1));
    }

    private static void ConfigurarLabelStats(Label label, Color color)
    {
        if (label == null) return;
        label.AddThemeColorOverride("font_color", color);
        label.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.95f));
        label.AddThemeConstantOverride("outline_size", 2);
        label.AddThemeFontSizeOverride("font_size", 13);
    }

    private void CarregarVisibilidadeStats()
    {
        var cfg = new ConfigFile();
        bool visible = true;
        if (cfg.Load(SettingsPath) == Error.Ok)
            visible = cfg.GetValue(SectionUI, "mostrar_fps_ping", true).AsBool();
        SetFpsPingVisible(visible);
    }

    public void SetFpsPingVisible(bool visible)
    {
        if (_statsPanel != null)
            _statsPanel.Visible = visible;
        if (_fpsLabel != null)
            _fpsLabel.Visible = visible;
        if (_pingLabel != null)
            _pingLabel.Visible = visible;
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

            if (_statsPanel != null)
            {
                Vector2 statsSize = new(118f, 42f);
                _statsPanel.Size = statsSize;
                _statsPanel.Position = new Vector2(
                    Mathf.Max(0f, _cashBtn.Position.X - statsSize.X - 8f),
                    _cashBtn.Position.Y + Mathf.Max(0f, (cashSize.Y - statsSize.Y) * 0.5f)
                );
            }
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
        _fpsLabel.Text = $"FPS  {Engine.GetFramesPerSecond()}";
        var net = GetNodeOrNull<GameNetwork>("/root/GameNetwork");
        _pingLabel.Text = net != null && net.IsConnected ? $"PING {net.ServerPing} ms" : "PING OFF";
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
        if (_playerFaction == null && _player is Player localPlayer)
            _playerFaction = localPlayer.FaccaoAtiva;

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
                _dotsOverlay.DrawCircle(pos, radius, inimigo.IsBoss ? CorBoss : new Color(1, 0, 0, 0.9f));
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

                _dotsOverlay.DrawCircle(pos, PlayerDotRadius, CalcularCorPlayer(node, otherPlayer.FaccaoAtiva?.IdFaccao ?? ""));
            }
        }

        var remotePlayers = GetTree().GetNodesInGroup("RemotePlayers");
        foreach (var node in remotePlayers)
        {
            if (node is not Node2D remote || !GodotObject.IsInstanceValid(remote)) continue;

            Vector2 pos = WorldToMinimap(remote.GlobalPosition);
            if (pos.DistanceTo(overlayCenter) > circleRadius) continue;

            string factionId = ObterMetaString(remote, "faction_id");
            _dotsOverlay.DrawCircle(pos, PlayerDotRadius, CalcularCorPlayer(remote, factionId));
        }
    }

    private Color CalcularCorPlayer(Node node, string factionId)
    {
        ulong entityId = ObterEntityId(node);
        var net = GetNodeOrNull<GameNetwork>("/root/GameNetwork");

        if (entityId != 0 && net != null && EstaNaParty(net, entityId))
            return CorParty;

        if (net != null && EstaNaGuild(net, entityId, ObterMetaString(node, "guild_name")))
            return CorGuild;

        string localFactionId = _playerFaction?.IdFaccao?.Trim() ?? "";
        if (!string.IsNullOrWhiteSpace(localFactionId)
            && !string.IsNullOrWhiteSpace(factionId)
            && !localFactionId.Equals(factionId.Trim(), System.StringComparison.OrdinalIgnoreCase))
            return CorFaccaoInimiga;

        return CorPlayerNormal;
    }

    private static bool EstaNaParty(GameNetwork net, ulong entityId)
    {
        if (!net.HasPendingPartyData)
            return false;

        foreach (var member in net.PendingPartyMembers)
        {
            if (member.ContainsKey("entity_id") && ConverterEntityId(member["entity_id"]) == entityId)
                return true;
        }
        return false;
    }

    private static bool EstaNaGuild(GameNetwork net, ulong entityId, string guildName)
    {
        if (net.GuildId < 0)
            return false;

        foreach (var member in net.CachedGuildMembers)
        {
            if (member.ContainsKey("entity_id") && ConverterEntityId(member["entity_id"]) == entityId)
                return true;
        }

        return !string.IsNullOrWhiteSpace(net.GuildName)
            && !string.IsNullOrWhiteSpace(guildName)
            && net.GuildName.Equals(guildName, System.StringComparison.OrdinalIgnoreCase);
    }

    private static ulong ObterEntityId(Node node)
    {
        if (!node.HasMeta("network_id"))
            return 0;
        return ConverterEntityId(node.GetMeta("network_id"));
    }

    private static string ObterMetaString(Node node, string key)
    {
        return node.HasMeta(key) ? node.GetMeta(key).AsString() : "";
    }

    private static ulong ConverterEntityId(Variant value)
    {
        return value.VariantType switch
        {
            Variant.Type.Int => (ulong)value.AsInt64(),
            Variant.Type.Float => (ulong)value.AsDouble(),
            Variant.Type.String => ulong.TryParse(value.AsString(), out var parsed) ? parsed : 0UL,
            _ => 0UL,
        };
    }

    public override void _ExitTree()
    {
        GetTree().Root.SizeChanged -= ManterNaBordaDireita;
    }
}
