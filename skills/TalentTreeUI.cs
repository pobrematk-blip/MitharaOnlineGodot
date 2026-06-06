using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class TalentTreeUI : Control
{
    private Panel _bgPanel;
    private Panel _titleBar;
    private Label _pointsLabel;
    private Button _closeButton;
    private Button _zoomInButton;
    private Button _zoomOutButton;
    private Control _board;
    private PanelContainer _slotTemplate;
    private TalentTreeComponent _talentTreeComponent;
    private int _playerNivel;
    private bool _arrastando;
    private Vector2 _pontoCliqueOriginal;
    private bool _draggingBoard;
    private Vector2 _boardDragOrigin;
    private Vector2 _boardOriginalPosition;

    private const float SlotSize = 38f;
    private const float RowHeight = 96f;
    private const float ColumnWidth = 108f;
    private const float BoardMargin = 50f;
    private const float DefaultZoom = 0.6f;
    private const float MinZoom = 0.3f;
    private const float MaxZoom = 1.15f;
    private const float ZoomStep = 0.08f;
    private float _zoomFactor = DefaultZoom;

    private static readonly Color CorDisponivel = new(0.35f, 0.2f, 0.45f);
    private static readonly Color CorNormal = new(0.08f, 0.08f, 0.12f);
    private static readonly Color CorDesbloqueado = new(0.35f, 0.5f, 0.9f);

    public override void _Ready()
    {
        _bgPanel = GetNodeOrNull<Panel>("BgPanel");
        if (_bgPanel == null) { GD.PrintErr("[TALENT] Scene inválida"); return; }

        _titleBar = _bgPanel.GetNodeOrNull<Panel>("TitleBar");
        _pointsLabel = _bgPanel.GetNodeOrNull<Label>("TitleBar/PointsLabel");
        _closeButton = _bgPanel.GetNodeOrNull<Button>("CloseButton");
        _zoomInButton = _bgPanel.GetNodeOrNull<Button>("TitleBar/ZoomInButton");
        _zoomOutButton = _bgPanel.GetNodeOrNull<Button>("TitleBar/ZoomOutButton");
        var scroll = _bgPanel.GetNodeOrNull<ScrollContainer>("VBox/Scroll");
        _board = scroll?.GetNodeOrNull<Control>("Board");
        _slotTemplate = _board?.GetNodeOrNull<PanelContainer>("SlotTemplate");

        if (_closeButton != null) _closeButton.Pressed += OnCloseButtonPressed;
        if (_titleBar != null) _titleBar.GuiInput += OnTitleBarGuiInput;
        if (_board == null) _board = new Control();
        if (_slotTemplate == null) { _slotTemplate = new PanelContainer(); _slotTemplate.Visible = false; if (_board != null) _board.AddChild(_slotTemplate); }
        if (_board != null) _board.GuiInput += OnBoardGuiInput;
        EnsureZoomButtons();

        if (_bgPanel != null)
        {
            _bgPanel.ZIndex = 200;
            _bgPanel.Visible = false;
            PreparePanelLayout();
        }

        EnsureInputAction();
        CriarBotaoToggle();
    }

    private void CriarBotaoToggle()
    {
        var btn = new TextureButton();
        btn.Name = "SkillsToggleButton";
        btn.TextureNormal = GD.Load<Texture2D>("res://ui/Incone de Menu/Skills.png");
        btn.TextureHover = GD.Load<Texture2D>("res://ui/Incone de Menu/Skills Selecionado.png");
        btn.CustomMinimumSize = new Vector2(36, 36);
        btn.StretchMode = TextureButton.StretchModeEnum.KeepCentered;
        btn.Pressed += () => TogglePanelVisibility();
        AddChild(btn);
        AtualizarPosicaoBotao(btn);
        GetTree().Root.SizeChanged += () => AtualizarPosicaoBotao(btn);
    }

    private void AtualizarPosicaoBotao(Control btn)
    {
        Vector2 tela = GetViewportRect().Size;
        btn.Position = new Vector2(tela.X - 44, tela.Y - 308);
    }

    private void EnsureZoomButtons()
    {
        if (_titleBar == null) return;

        if (_zoomInButton == null)
        {
            _zoomInButton = new Button();
            _zoomInButton.Name = "ZoomInButton";
            _zoomInButton.Text = "+";
            _zoomInButton.Size = new Vector2(32, 32);
            _zoomInButton.AnchorLeft = 0f;
            _zoomInButton.AnchorTop = 0f;
            _zoomInButton.AnchorRight = 0f;
            _zoomInButton.AnchorBottom = 0f;
            _zoomInButton.Position = new Vector2(0, 0);
            _titleBar.AddChild(_zoomInButton);
            _zoomInButton.Pressed += () => AdjustZoom(ZoomStep);
        }

        if (_zoomOutButton == null)
        {
            _zoomOutButton = new Button();
            _zoomOutButton.Name = "ZoomOutButton";
            _zoomOutButton.Text = "-";
            _zoomOutButton.Size = new Vector2(32, 32);
            _zoomOutButton.AnchorLeft = 0f;
            _zoomOutButton.AnchorTop = 0f;
            _zoomOutButton.AnchorRight = 0f;
            _zoomOutButton.AnchorBottom = 0f;
            _zoomOutButton.Position = new Vector2(0, 0);
            _titleBar.AddChild(_zoomOutButton);
            _zoomOutButton.Pressed += () => AdjustZoom(-ZoomStep);
        }
    }

    public override void _Input(InputEvent @event)
    {
        if (_bgPanel == null || !_bgPanel.Visible) return;
        if (@event is InputEventKey ek && ek.Pressed && !ek.Echo)
        {
            if (ek.Keycode == Key.Escape)
            {
                ClosePanel();
                GetViewport().SetInputAsHandled();
            }
            else if (_bgPanel != null && _bgPanel.Visible && (ek.Keycode == Key.Plus || ek.Keycode == Key.Equal))
            {
                AdjustZoom(ZoomStep);
                GetViewport().SetInputAsHandled();
            }
            else if (_bgPanel != null && _bgPanel.Visible && ek.Keycode == Key.Minus)
            {
                AdjustZoom(-ZoomStep);
                GetViewport().SetInputAsHandled();
            }
        }
        else if (_bgPanel != null && _bgPanel.Visible && @event is InputEventMouseButton wheelEvent)
        {
            if (wheelEvent.ButtonIndex == MouseButton.WheelUp)
            {
                AdjustZoom(ZoomStep);
                GetViewport().SetInputAsHandled();
            }
            else if (wheelEvent.ButtonIndex == MouseButton.WheelDown)
            {
                AdjustZoom(-ZoomStep);
                GetViewport().SetInputAsHandled();
            }
        }
    }

    public override void _Process(double delta)
    {
        if (GetViewport().GuiGetFocusOwner() is LineEdit) return;
        if (Input.IsActionJustPressed("talent_tree")) TogglePanelVisibility();
    }

    private static void EnsureInputAction()
    {
        if (InputMap.HasAction("talent_tree")) return;
        InputMap.AddAction("talent_tree");
        InputMap.ActionAddEvent("talent_tree", new InputEventKey { PhysicalKeycode = Key.K, Keycode = Key.K, Unicode = 107, Pressed = false });
    }

    private void TogglePanelVisibility()
    {
        if (_bgPanel == null) return;
        _bgPanel.Visible = !_bgPanel.Visible;
        if (_bgPanel.Visible)
        {
            PreparePanelLayout();
            UpdateTreeView();
        }
    }

    private void PreparePanelLayout()
    {
        if (_bgPanel == null) return;
        var viewportSize = GetViewport().GetVisibleRect().Size;
        var panelSize = viewportSize * new Vector2(0.55f, 0.55f);
        _bgPanel.Size = panelSize;
        _bgPanel.Position = (viewportSize - panelSize) * 0.5f;
        _bgPanel.ZIndex = 200;
        var parent = _bgPanel.GetParent();
        if (parent != null)
            parent.MoveChild(_bgPanel, parent.GetChildCount() - 1);

        // Apply board zoom (acts as a simple camera for the UI tree)
        if (_board != null)
        {
            _board.Scale = new Vector2(_zoomFactor, _zoomFactor);
            // Ensure board occupies available area so scaling centers correctly
            _board.Size = panelSize * 0.92f;
            _board.CustomMinimumSize = _board.Size;
            _board.Position = (panelSize - _board.Size * _zoomFactor) * 0.5f;
        }

        // Move close and zoom buttons to the top-right inside the panel
        float buttonSpacing = 6f;
        float buttonSize = 24f;
        float margin = 8f;
        
        if (_closeButton != null)
        {
            var cbSize = _closeButton.Size;
            _closeButton.Position = new Vector2(panelSize.X - cbSize.X - margin, margin);
            _bgPanel.MoveChild(_closeButton, _bgPanel.GetChildCount() - 1);
        }

        if (_zoomOutButton != null)
        {
            _zoomOutButton.Position = new Vector2(
                panelSize.X - (_closeButton != null ? _closeButton.Size.X : buttonSize) - buttonSize - buttonSpacing - margin, 
                margin + 2f); // Slightly lower to align better with close button
            _zoomOutButton.Size = new Vector2(buttonSize, buttonSize);
        }

        if (_zoomInButton != null)
        {
            _zoomInButton.Position = new Vector2(
                panelSize.X - (_closeButton != null ? _closeButton.Size.X : buttonSize) - buttonSize * 2 - buttonSpacing * 2 - margin, 
                margin + 2f); // Slightly lower to align better with close button
            _zoomInButton.Size = new Vector2(buttonSize, buttonSize);
        }
    }

    private void ClosePanel() { if (_bgPanel != null) _bgPanel.Visible = false; }
    private void OnCloseButtonPressed() => ClosePanel();

    private void AdjustZoom(float delta)
    {
        if (_board == null || _bgPanel == null) return;
        _zoomFactor = Mathf.Clamp(_zoomFactor + delta, MinZoom, MaxZoom);
        _board.Scale = new Vector2(_zoomFactor, _zoomFactor);
        _board.Position = (_bgPanel.Size - _board.Size * _zoomFactor) * 0.5f;
        UpdatePointsLabel();
    }

    private void UpdatePointsLabel()
    {
        if (_pointsLabel == null) return;
        var points = _talentTreeComponent?.PontosDisponiveis ?? 0;
        _pointsLabel.Text = $"Pontos: {points}  |  Nível: {_playerNivel}  |  Zoom: {(int)(_zoomFactor * 100)}%";
    }

    private void OnTitleBarGuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mouseEvent && mouseEvent.ButtonIndex == MouseButton.Left)
        {
            _arrastando = mouseEvent.Pressed;
            if (mouseEvent.Pressed)
                _pontoCliqueOriginal = mouseEvent.Position;
        }
        else if (@event is InputEventMouseMotion mouseMotion && _arrastando)
        {
            _bgPanel.Position += mouseMotion.Position - _pontoCliqueOriginal;
        }
    }

    private void OnBoardGuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mouseEvent && mouseEvent.ButtonIndex == MouseButton.Left)
        {
            _draggingBoard = mouseEvent.Pressed;
            if (mouseEvent.Pressed)
            {
                _boardDragOrigin = mouseEvent.Position;
                _boardOriginalPosition = _board.Position;
            }
        }
        else if (@event is InputEventMouseMotion mouseMotion && _draggingBoard)
        {
            Vector2 delta = mouseMotion.Position - _boardDragOrigin;
            _board.Position = _boardOriginalPosition + delta;
        }
    }

    private void UpdateTreeView()
    {
        var player = GetTree().CurrentScene.FindChild("Player", true, false) as Player;
        if (player == null) { _pointsLabel.Text = "Sem jogador"; return; }
        _playerNivel = player.Nivel;

        _talentTreeComponent = player.GetNodeOrNull<TalentTreeComponent>("TalentTreeComponent");
        if (_talentTreeComponent?.TalentTree == null) { _pointsLabel.Text = "Sem árvore"; return; }

        UpdatePointsLabel();

        foreach (var child in _board.GetChildren())
        {
            if (child != _slotTemplate)
                child.QueueFree();
        }

        var tree = _talentTreeComponent.TalentTree;
        var nodes = tree.Nodes?.Where(n => n != null).ToArray();
        if (nodes == null || nodes.Length == 0) return;

        var rootNodes = tree.ObterNosRaiz().Where(n => n != null).ToArray();
        string centerId = rootNodes.Length == 1 ? rootNodes[0].NodeId : null;

        var branchSeeds = new List<string>();
        if (centerId != null)
        {
            branchSeeds = tree.ObterFilhos(centerId).Where(n => n != null)
                                .Select(n => n.NodeId)
                                .Distinct()
                                .ToList();
            if (branchSeeds.Count == 0)
                branchSeeds.Add(centerId);
        }
        else
        {
            branchSeeds = rootNodes.Select(n => n.NodeId)
                                    .Distinct()
                                    .ToList();
        }

        if (branchSeeds.Count == 0 && nodes.Length > 0)
            branchSeeds.Add(nodes[0].NodeId);

        if (branchSeeds.Count > 3)
            branchSeeds = branchSeeds.Take(3).ToList();

        var branchAssignment = new Dictionary<string, int>();
        var branchQueue = new Queue<string>();

        if (centerId != null)
        {
            branchAssignment[centerId] = 0;
            for (int i = 0; i < branchSeeds.Count; i++)
            {
                branchAssignment[branchSeeds[i]] = i + 1;
                branchQueue.Enqueue(branchSeeds[i]);
            }
        }
        else
        {
            for (int i = 0; i < branchSeeds.Count; i++)
            {
                branchAssignment[branchSeeds[i]] = i;
                branchQueue.Enqueue(branchSeeds[i]);
            }
        }

        while (branchQueue.Count > 0)
        {
            var current = branchQueue.Dequeue();
            foreach (var child in tree.ObterFilhos(current))
            {
                if (child == null) continue;
                if (!branchAssignment.ContainsKey(child.NodeId))
                {
                    branchAssignment[child.NodeId] = branchAssignment[current];
                    branchQueue.Enqueue(child.NodeId);
                }
            }
        }

        foreach (var node in nodes)
            if (!branchAssignment.ContainsKey(node.NodeId))
                branchAssignment[node.NodeId] = 0;

        var positions = CalculateNodePositions(tree, nodes, out int maxDepth);
        DrawBackgroundGrid(_board.CustomMinimumSize, maxDepth);

        var slotMap = new Dictionary<string, PanelContainer>();
        foreach (var node in nodes)
        {
            var id = node.NodeId;
            if (!positions.ContainsKey(id)) continue;
            var slot = (PanelContainer)_slotTemplate.Duplicate(7);
            slot.Visible = true;
            slot.TooltipText = $"{node.Nome}\n{node.Descricao}";
            slot.AnchorLeft = 0;
            slot.AnchorTop = 0;
            slot.AnchorRight = 0;
            slot.AnchorBottom = 0;
            slot.Size = new Vector2(SlotSize, SlotSize);
            slot.CustomMinimumSize = new Vector2(SlotSize, SlotSize);
            _board.AddChild(slot);
            slotMap[id] = slot;

            var icon = slot.GetNodeOrNull<TextureRect>("Icon");
            if (icon != null && node.Icone != null)
                icon.Texture = node.Icone;

            bool unlocked = _talentTreeComponent.TemNoDesbloqueado(id);
            bool canUnlock = !unlocked && _talentTreeComponent.PodeDesbloquear(id, _playerNivel);

            if (tree.UsaLayoutPersonalizado && node.Posicao != new Vector2(-1, -1))
            {
                Estilo(slot, new Color(0.18f, 0.14f, 0.25f), new Color(0.9f, 0.75f, 0.4f));
            }
            else if (unlocked)
            {
                Estilo(slot, new Color(0.16f, 0.24f, 0.48f), CorDesbloqueado);
            }
            else if (canUnlock)
            {
                Estilo(slot, new Color(0.28f, 0.18f, 0.4f), CorDisponivel);
            }
            else
            {
                Estilo(slot, CorNormal, new Color(0.18f, 0.18f, 0.22f));
            }

            if (canUnlock)
            {
                Control.GuiInputEventHandler handler = null;
                handler = (InputEvent e) =>
                {
                    if (e is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
                        if (_talentTreeComponent.DesbloquearNo(id, _playerNivel))
                            UpdateTreeView();
                };
                slot.GuiInput += handler;
            }

            slot.Position = positions[id] - new Vector2(SlotSize * 0.5f, SlotSize * 0.5f);
        }

        foreach (var node in nodes)
        {
            if (node?.Requisitos == null || !slotMap.ContainsKey(node.NodeId)) continue;

            var childSlot = slotMap[node.NodeId];
            Vector2 cCenter = childSlot.Position + childSlot.Size / 2;

            foreach (var reqId in node.Requisitos)
            {
                if (!slotMap.ContainsKey(reqId)) continue;
                var parentSlot = slotMap[reqId];
                Vector2 pCenter = parentSlot.Position + parentSlot.Size / 2;
                bool parentUnlocked = _talentTreeComponent.TemNoDesbloqueado(reqId);
                bool childUnlocked = _talentTreeComponent.TemNoDesbloqueado(node.NodeId);

                var line = new Line2D();
                if (childUnlocked)
                {
                    line.DefaultColor = new Color(0.6f, 0.7f, 1f, 0.9f);
                    line.Width = 2.5f;
                }
                else if (parentUnlocked)
                {
                    line.DefaultColor = new Color(0.45f, 0.4f, 0.7f, 0.65f);
                    line.Width = 1.8f;
                }
                else
                {
                    line.DefaultColor = new Color(0.18f, 0.18f, 0.22f, 0.45f);
                    line.Width = 1.0f;
                }

                line.Points = new Vector2[] { pCenter, cCenter };
                _board.AddChild(line);
                _board.MoveChild(line, 0);
            }
        }
    }

    private Dictionary<string, Vector2> CalculateNodePositions(TalentTreeResource tree, TalentNodeResource[] nodes, out int maxDepth)
    {
        if (tree.UsaLayoutPersonalizado && nodes.Any(n => n.Posicao != new Vector2(-1, -1)))
            return CalculateManualNodePositions(tree, nodes, out maxDepth);

        return CalculateGridNodePositions(tree, nodes, out maxDepth);
    }

    private Dictionary<string, Vector2> CalculateManualNodePositions(TalentTreeResource tree, TalentNodeResource[] nodes, out int maxDepth)
    {
        maxDepth = 0;
        var positions = new Dictionary<string, Vector2>();
        if (tree.BoardSize.X <= 0 || tree.BoardSize.Y <= 0)
            tree.BoardSize = new Vector2(1200, 900);

        _board.CustomMinimumSize = tree.BoardSize;

        foreach (var node in nodes)
        {
            if (node.Posicao == new Vector2(-1, -1))
                continue;

            positions[node.NodeId] = node.Posicao;
            var depth = Mathf.RoundToInt((node.Posicao.Y - BoardMargin) / RowHeight);
            maxDepth = Mathf.Max(maxDepth, depth);
        }

        return positions;
    }

    private Dictionary<string, Vector2> CalculateGridNodePositions(TalentTreeResource tree, TalentNodeResource[] nodes, out int maxDepth)
    {
        var rootNodes = tree.ObterNosRaiz().Where(n => n != null).ToArray();
        string centerId = rootNodes.Length == 1 ? rootNodes[0].NodeId : null;

        var branchSeeds = new List<string>();
        if (centerId != null)
        {
            branchSeeds = tree.ObterFilhos(centerId).Where(n => n != null)
                                .Select(n => n.NodeId)
                                .Distinct()
                                .ToList();
            if (branchSeeds.Count == 0)
                branchSeeds.Add(centerId);
        }
        else
        {
            branchSeeds = rootNodes.Select(n => n.NodeId)
                                    .Distinct()
                                    .ToList();
        }

        if (branchSeeds.Count == 0 && nodes.Length > 0)
            branchSeeds.Add(nodes[0].NodeId);

        if (branchSeeds.Count > 5)
            branchSeeds = branchSeeds.Take(5).ToList();

        var branchAssignment = new Dictionary<string, int>();
        if (centerId != null)
        {
            branchAssignment[centerId] = 0;
            for (int i = 0; i < branchSeeds.Count; i++)
                branchAssignment[branchSeeds[i]] = i + 1;
        }
        else
        {
            for (int i = 0; i < branchSeeds.Count; i++)
                branchAssignment[branchSeeds[i]] = i;
        }

        var branchQueue = new Queue<string>(branchAssignment.Keys);
        while (branchQueue.Count > 0)
        {
            var current = branchQueue.Dequeue();
            foreach (var child in tree.ObterFilhos(current))
            {
                if (child == null) continue;
                if (!branchAssignment.ContainsKey(child.NodeId))
                {
                    branchAssignment[child.NodeId] = branchAssignment[current];
                    branchQueue.Enqueue(child.NodeId);
                }
            }
        }

        foreach (var node in nodes)
            if (!branchAssignment.ContainsKey(node.NodeId))
                branchAssignment[node.NodeId] = 0;

        var depths = new Dictionary<string, int>();
        var depthQueue = new Queue<(string id, int depth)>();

        if (centerId != null)
        {
            depths[centerId] = 0;
            depthQueue.Enqueue((centerId, 0));
        }
        else
        {
            foreach (var seed in branchSeeds)
            {
                depths[seed] = 1;
                depthQueue.Enqueue((seed, 1));
            }
        }

        while (depthQueue.Count > 0)
        {
            var (currentId, depth) = depthQueue.Dequeue();
            foreach (var child in tree.ObterFilhos(currentId))
            {
                if (child == null) continue;
                if (depths.ContainsKey(child.NodeId)) continue;
                depths[child.NodeId] = depth + 1;
                depthQueue.Enqueue((child.NodeId, depth + 1));
            }
        }

        maxDepth = depths.Count > 0 ? depths.Values.Max() : 0;

        var nodesByDepth = new Dictionary<int, List<string>>();
        foreach (var node in nodes)
        {
            int depth = depths.GetValueOrDefault(node.NodeId, centerId != null ? 1 : 0);
            if (!nodesByDepth.ContainsKey(depth))
                nodesByDepth[depth] = new List<string>();
            nodesByDepth[depth].Add(node.NodeId);
        }

        foreach (var kv in nodesByDepth)
        {
            kv.Value.Sort((a, b) =>
            {
                int branchA = branchAssignment.GetValueOrDefault(a, 0);
                int branchB = branchAssignment.GetValueOrDefault(b, 0);
                if (branchA != branchB)
                    return branchA.CompareTo(branchB);
                return string.Compare(a, b, StringComparison.Ordinal);
            });
        }

        int maxCount = nodesByDepth.Values.Any() ? nodesByDepth.Values.Max(list => list.Count) : 1;
        float boardWidth = Mathf.Max(_board.CustomMinimumSize.X, BoardMargin * 2 + (maxCount - 1) * ColumnWidth + SlotSize);
        float boardHeight = Mathf.Max(_board.CustomMinimumSize.Y, BoardMargin * 2 + (maxDepth + 2) * RowHeight + SlotSize);
        _board.CustomMinimumSize = new Vector2(boardWidth, boardHeight);

        var positions = new Dictionary<string, Vector2>();
        foreach (var kv in nodesByDepth)
        {
            int depth = kv.Key;
            var ids = kv.Value;
            int count = ids.Count;
            float rowWidth = (count - 1) * ColumnWidth;
            float startX = (_board.CustomMinimumSize.X * 0.5f) - (rowWidth * 0.5f);
            float y = BoardMargin + depth * RowHeight;

            for (int index = 0; index < ids.Count; index++)
            {
                var id = ids[index];
                positions[id] = new Vector2(startX + index * ColumnWidth, y);
            }
        }

        return positions;
    }

    private void DrawBackgroundGrid(Vector2 boardSize, int maxDepth)
    {
        int rowCount = Mathf.Max(4, maxDepth + 2);
        var gridColor = new Color(0.18f, 0.22f, 0.3f, 0.22f);
        float startX = BoardMargin;
        float endX = boardSize.X - BoardMargin;

        for (int row = 0; row < rowCount; row++)
        {
            float y = BoardMargin + row * RowHeight;
            var line = new Line2D();
            line.Width = 1.0f;
            line.DefaultColor = gridColor;
            line.Points = new[] { new Vector2(startX, y), new Vector2(endX, y) };
            line.ZIndex = -10;
            _board.AddChild(line);
            _board.MoveChild(line, 0);
        }

        int columnCount = 5;
        for (int column = 1; column < columnCount; column++)
        {
            float x = boardSize.X * column / columnCount;
            var line = new Line2D();
            line.Width = 1.0f;
            line.DefaultColor = new Color(0.18f, 0.22f, 0.3f, 0.12f);
            line.Points = new[] { new Vector2(x, BoardMargin - 40f), new Vector2(x, boardSize.Y - BoardMargin + 40f) };
            line.ZIndex = -10;
            _board.AddChild(line);
            _board.MoveChild(line, 0);
        }
    }

    private static void Estilo(PanelContainer slot, Color bg, Color border)
    {
        slot.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = bg,
            BorderWidthLeft = 3,
            BorderWidthTop = 3,
            BorderWidthRight = 3,
            BorderWidthBottom = 3,
            BorderColor = border,
            CornerRadiusTopLeft = (int)(SlotSize * 0.4f),
            CornerRadiusTopRight = (int)(SlotSize * 0.4f),
            CornerRadiusBottomRight = (int)(SlotSize * 0.4f),
            CornerRadiusBottomLeft = (int)(SlotSize * 0.4f),
            ContentMarginLeft = 0,
            ContentMarginTop = 0,
            ContentMarginRight = 0,
            ContentMarginBottom = 0,
        });
    }
}