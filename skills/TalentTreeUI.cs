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
    private Control _canvasViewport;
    private Control _board;
    private PanelContainer _slotTemplate;
    private TalentTreeComponent _talentTreeComponent;
    private TalentTreeComponent _connectedTalentTreeComponent;
    private int _playerNivel;
    private bool _arrastando;
    private Vector2 _pontoCliqueOriginal;
    private bool _draggingBoard;
    private Vector2 _boardDragOrigin;
    private Vector2 _boardOriginalPosition;

    private const float SlotSize = 38f;
    private const float AttributeNodeSize = 28f;
    private const float ChoiceNodeWidth = 104f;
    private const float ChoiceNodeAreaSize = 112f;
    private const float SkillNodeSize = 72f;
    private const float SkillClusterWidth = 78f;
    private const float SkillClusterHeight = 78f;
    private const float SkillStatusNodeSize = 22f;
    private const float ManualLayoutSpacing = 1.38f;
    private const float UnlockNodeSize = 40f;
    private const float RootNodeSize = 68f;
    private const float RowHeight = 96f;
    private const float ColumnWidth = 108f;
    private const float BoardMargin = 50f;
    private const float DefaultZoom = 0.52f;
    private const float MinZoom = 0.25f;
    private const float MaxZoom = 1.25f;
    private const float ZoomStep = 0.08f;
    private float _zoomFactor = DefaultZoom;

    private static readonly Color CorDisponivel = new(0.35f, 0.2f, 0.45f);
    private static readonly Color CorNormal = new(0.08f, 0.08f, 0.12f);
    private static readonly Color CorDesbloqueado = new(0.35f, 0.5f, 0.9f);
    private TextureButton _toggleButton;

    public override void _ExitTree()
    {
        if (_toggleButton != null)
            GetTree().Root.SizeChanged -= OnSizeChanged;
    }

    public override void _Ready()
    {
        _bgPanel = GetNodeOrNull<Panel>("BgPanel");
        if (_bgPanel == null) { GD.PrintErr("[TALENT] Scene inválida"); return; }

        _titleBar = _bgPanel.GetNodeOrNull<Panel>("TitleBar");
        _pointsLabel = _bgPanel.GetNodeOrNull<Label>("TitleBar/PointsLabel");
        _closeButton = _bgPanel.GetNodeOrNull<Button>("CloseButton");
        _zoomInButton = _bgPanel.GetNodeOrNull<Button>("TitleBar/ZoomInButton");
        _zoomOutButton = _bgPanel.GetNodeOrNull<Button>("TitleBar/ZoomOutButton");
        _canvasViewport = _bgPanel.GetNodeOrNull<Control>("VBox/Scroll");
        _board = _canvasViewport?.GetNodeOrNull<Control>("Board");
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
        _toggleButton = new TextureButton();
        _toggleButton.Name = "SkillsToggleButton";
        _toggleButton.TextureNormal = GD.Load<Texture2D>("res://ui/Incone de Menu/Skills.png");
        _toggleButton.TextureHover = GD.Load<Texture2D>("res://ui/Incone de Menu/Skills Selecionado.png");
        _toggleButton.CustomMinimumSize = new Vector2(36, 36);
        _toggleButton.StretchMode = TextureButton.StretchModeEnum.KeepCentered;
        _toggleButton.Pressed += () => TogglePanelVisibility();
        AddChild(_toggleButton);
        AtualizarPosicaoBotao(_toggleButton);
        GetTree().Root.SizeChanged += OnSizeChanged;
    }

    private void OnSizeChanged()
    {
        if (_toggleButton != null)
            AtualizarPosicaoBotao(_toggleButton);
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
        var panelSize = viewportSize * new Vector2(0.82f, 0.78f);
        panelSize.X = Mathf.Min(panelSize.X, viewportSize.X - 24f);
        panelSize.Y = Mathf.Min(panelSize.Y, viewportSize.Y - 24f);
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
        Vector2 viewportCenter = _canvasViewport != null
            ? _canvasViewport.Size * 0.5f
            : _bgPanel.Size * 0.5f;
        Vector2 localCenter = (viewportCenter - _board.Position) / _zoomFactor;
        _zoomFactor = Mathf.Clamp(_zoomFactor + delta, MinZoom, MaxZoom);
        _board.Scale = new Vector2(_zoomFactor, _zoomFactor);
        _board.Position = viewportCenter - localCenter * _zoomFactor;
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
                _boardDragOrigin = mouseEvent.GlobalPosition;
                _boardOriginalPosition = _board.Position;
            }
        }
        else if (@event is InputEventMouseMotion mouseMotion && _draggingBoard)
        {
            Vector2 delta = mouseMotion.GlobalPosition - _boardDragOrigin;
            _board.Position = _boardOriginalPosition + delta;
        }
    }

    private void UpdateTreeView()
    {
        var player = GetTree().CurrentScene.FindChild("Player", true, false) as Player;
        if (player == null) { _pointsLabel.Text = "Sem jogador"; return; }
        _playerNivel = ObterNivelOnline(player);

        _talentTreeComponent = player.GetNodeOrNull<TalentTreeComponent>("TalentTreeComponent");
        if (_talentTreeComponent?.TalentTree == null) { _pointsLabel.Text = "Sem árvore"; return; }
        ConectarAtualizacaoDaArvore(_talentTreeComponent);

        UpdatePointsLabel();

        foreach (var child in _board.GetChildren())
        {
            if (child != _slotTemplate)
                child.QueueFree();
        }

        var tree = _talentTreeComponent.TalentTree;
        var titleLabel = _titleBar?.GetNodeOrNull<Label>("TitleLabel");
        if (titleLabel != null)
            titleLabel.Text = tree.NomeArvore.ToUpperInvariant();
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
        _board.Size = _board.CustomMinimumSize;
        CallDeferred(nameof(CentralizarCanvas));

        var slotMap = new Dictionary<string, PanelContainer>();
        foreach (var node in nodes)
        {
            // As escolhas de status são apresentadas como uma coroa sobre cada skill.
            // O nó lógico continua existindo na árvore para preservar os requisitos.
            if (node.TemEscolhaDeStatus)
                continue;

            var id = node.NodeId;
            if (!positions.ContainsKey(id)) continue;
            bool unlocked = _talentTreeComponent.TemNoDesbloqueado(id);
            bool canUnlock = !unlocked && _talentTreeComponent.PodeDesbloquear(id, _playerNivel);
            Vector2 nodeDimensions = ObterDimensaoNo(node, tree);
            var slot = new TalentNodeSlotUI
            {
                NodeData = node,
                IsUnlocked = unlocked,
                MouseFilter = MouseFilterEnum.Stop,
            };
            slot.GuiInput += inputEvent => OnTalentNodeGuiInput(inputEvent, id);
            slot.Visible = true;
            slot.TooltipText = CriarTooltip(node);
            slot.AnchorLeft = 0;
            slot.AnchorTop = 0;
            slot.AnchorRight = 0;
            slot.AnchorBottom = 0;
            slot.Size = nodeDimensions;
            slot.CustomMinimumSize = nodeDimensions;
            _board.AddChild(slot);
            slotMap[id] = slot;

            Texture2D nodeIcon = node.Icone ?? node.HabilidadeAtiva?.Icone;
            if (node.HabilidadeAtiva != null)
            {
                slot.AddThemeStyleboxOverride("panel", new StyleBoxEmpty());

                var skillFrame = new PanelContainer
                {
                    Position = new Vector2((SkillClusterWidth - SkillNodeSize) * 0.5f, (SkillClusterHeight - SkillNodeSize) * 0.5f),
                    Size = Vector2.One * SkillNodeSize,
                    CustomMinimumSize = Vector2.One * SkillNodeSize,
                    MouseFilter = MouseFilterEnum.Pass,
                };
                slot.AddChild(skillFrame);

                var icon = new TextureRect
                {
                    Texture = nodeIcon,
                    ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                    StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                    MouseFilter = MouseFilterEnum.Ignore,
                    CustomMinimumSize = Vector2.One * SkillNodeSize,
                    Size = Vector2.One * SkillNodeSize,
                };
                icon.SetAnchorsPreset(LayoutPreset.FullRect);
                skillFrame.AddChild(icon);
                CriarFallbackDeIcone(skillFrame, node, nodeIcon);

                Color border = TalentNodeResource.ObterCorTipo(node.NodeType).Darkened(0.35f);
                Color bg = CorNormal;
                if (unlocked) { bg = new Color(0.16f, 0.24f, 0.48f); border = CorDesbloqueado; }
                else if (canUnlock) { bg = new Color(0.28f, 0.18f, 0.4f); border = CorDisponivel; }
                Estilo(skillFrame, bg, border, SkillNodeSize);

                var choiceSource = ObterEscolhaParaSkill(node, nodes);
                if (choiceSource != null)
                    CriarCoroaDeStatus(slot, choiceSource, unlocked, canUnlock);
            }
            else
            {
                var icon = new TextureRect
                {
                    Texture = nodeIcon,
                    ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                    StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                    MouseFilter = MouseFilterEnum.Ignore,
                };
                icon.SetAnchorsPreset(LayoutPreset.FullRect);
                slot.AddChild(icon);
                CriarFallbackDeIcone(slot, node, nodeIcon);

                Color border = TalentNodeResource.ObterCorTipo(node.NodeType).Darkened(0.35f);
                Color bg = CorNormal;
                if (unlocked) { bg = new Color(0.16f, 0.24f, 0.48f); border = CorDesbloqueado; }
                else if (canUnlock) { bg = new Color(0.28f, 0.18f, 0.4f); border = CorDisponivel; }
                Estilo(slot, bg, border, nodeDimensions.Y);
            }

            slot.Position = positions[id] - nodeDimensions * 0.5f;

            if (node.NodeId.EndsWith("_rota", StringComparison.Ordinal))
                CriarRotuloCaminho(node, positions[id]);
        }

        foreach (var node in nodes)
        {
            if (node?.Requisitos == null || !slotMap.ContainsKey(node.NodeId)) continue;

            var childSlot = slotMap[node.NodeId];
            Vector2 cCenter = childSlot.Position + childSlot.Size / 2;

            var visualRequirements = node.Requisitos
                .SelectMany(reqId => ObterRequisitosVisuais(tree, reqId, slotMap, new HashSet<string>()))
                .Distinct();

            foreach (var reqId in visualRequirements)
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

    private static IEnumerable<string> ObterRequisitosVisuais(
        TalentTreeResource tree,
        string nodeId,
        IReadOnlyDictionary<string, PanelContainer> visibleSlots,
        HashSet<string> visited)
    {
        if (string.IsNullOrWhiteSpace(nodeId) || !visited.Add(nodeId))
            yield break;
        if (visibleSlots.ContainsKey(nodeId))
        {
            yield return nodeId;
            yield break;
        }

        var hiddenNode = tree.ObterNo(nodeId);
        if (hiddenNode?.Requisitos == null)
            yield break;
        foreach (string parentId in hiddenNode.Requisitos)
            foreach (string visibleId in ObterRequisitosVisuais(tree, parentId, visibleSlots, visited))
                yield return visibleId;
    }

    private void OnTalentNodeGuiInput(InputEvent inputEvent, string nodeId)
    {
        if (inputEvent is not InputEventMouseButton mouse || !mouse.Pressed || mouse.ButtonIndex != MouseButton.Left)
            return;

        if (_talentTreeComponent == null || !_talentTreeComponent.PodeDesbloquear(nodeId, _playerNivel))
            return;

        _talentTreeComponent.SolicitarDesbloqueioServidor(nodeId);
        GetViewport().SetInputAsHandled();
    }

    private void ConectarAtualizacaoDaArvore(TalentTreeComponent component)
    {
        if (_connectedTalentTreeComponent == component)
            return;

        if (_connectedTalentTreeComponent != null)
            _connectedTalentTreeComponent.EstadoAtualizado -= OnTalentTreeStateUpdated;

        _connectedTalentTreeComponent = component;
        if (_connectedTalentTreeComponent != null)
            _connectedTalentTreeComponent.EstadoAtualizado += OnTalentTreeStateUpdated;
    }

    private void OnTalentTreeStateUpdated()
    {
        if (_bgPanel == null || !_bgPanel.Visible)
            return;

        UpdateTreeView();
    }

    private int ObterNivelOnline(Player player)
    {
        var levelComp = player.FindChild("LevelProgressionComponent", true, false) as LevelProgressionComponent;
        if (levelComp != null && levelComp.Nivel > LevelProgressionUtil.NivelInicial)
            return levelComp.Nivel;

        var gameNet = GetNodeOrNull<GameNetwork>("/root/GameNetwork");
        if (gameNet != null && gameNet._pendingLevel > LevelProgressionUtil.NivelInicial)
            return gameNet._pendingLevel;

        return player.Nivel;
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

        Vector2 originalCenter = tree.BoardSize * 0.5f;
        _board.CustomMinimumSize = tree.BoardSize * ManualLayoutSpacing;
        Vector2 expandedCenter = _board.CustomMinimumSize * 0.5f;

        foreach (var node in nodes)
        {
            if (node.Posicao == new Vector2(-1, -1))
                continue;

            Vector2 expandedPosition = expandedCenter + (node.Posicao - originalCenter) * ManualLayoutSpacing;
            positions[node.NodeId] = expandedPosition;
            var depth = Mathf.RoundToInt((expandedPosition.Y - BoardMargin) / RowHeight);
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
        Vector2 center = boardSize * 0.5f;
        var guideColor = new Color(0.24f, 0.28f, 0.34f, 0.16f);
        foreach (float radius in new[] { 120f, 190f, 270f, 350f, 430f, 495f, 560f })
        {
            var line = new Line2D();
            line.Width = 1.0f;
            line.DefaultColor = guideColor;
            var points = new Vector2[65];
            for (int index = 0; index < points.Length; index++)
            {
                float angle = Mathf.Pi * 2f * index / (points.Length - 1);
                points[index] = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            }
            line.Points = points;
            line.ZIndex = -10;
            _board.AddChild(line);
            _board.MoveChild(line, 0);
        }

        foreach (float angle in new[] { -Mathf.Pi / 2f, Mathf.Pi * 5f / 6f, Mathf.Pi / 6f })
        {
            var line = new Line2D();
            line.Width = 1.0f;
            line.DefaultColor = new Color(0.24f, 0.28f, 0.34f, 0.1f);
            line.Points = new[] { center, center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 580f };
            line.ZIndex = -10;
            _board.AddChild(line);
            _board.MoveChild(line, 0);
        }
    }

    private void CentralizarCanvas()
    {
        if (_board == null || _canvasViewport == null) return;
        _board.Position = (_canvasViewport.Size - _board.Size * _zoomFactor) * 0.5f;
    }

    private static Vector2 ObterDimensaoNo(TalentNodeResource node, TalentTreeResource tree)
    {
        if (node.TemEscolhaDeStatus)
            return Vector2.One * ChoiceNodeAreaSize;
        if (tree.RootNodeIds?.Contains(node.NodeId) == true)
            return Vector2.One * RootNodeSize;
        if (node.HabilidadeAtiva != null)
            return new Vector2(SkillClusterWidth, SkillClusterHeight);
        float size = node.NodeType switch
        {
            TalentNodeType.Unlock => UnlockNodeSize,
            _ => AttributeNodeSize,
        };
        return Vector2.One * size;
    }

    private static TalentNodeResource ObterEscolhaParaSkill(TalentNodeResource skill, TalentNodeResource[] nodes)
    {
        const string marker = "_skill_";
        int markerIndex = skill.NodeId.LastIndexOf(marker, StringComparison.Ordinal);
        if (markerIndex < 0) return null;

        string branchPrefix = skill.NodeId[..markerIndex];
        var choices = nodes
            .Where(n => n != null && n.TemEscolhaDeStatus && n.NodeId.StartsWith(branchPrefix + "_", StringComparison.Ordinal))
            .OrderBy(n => n.NodeId, StringComparer.Ordinal)
            .ToArray();
        if (choices.Length == 0) return null;

        string numberText = skill.NodeId[(markerIndex + marker.Length)..];
        int skillNumber = int.TryParse(numberText, out int parsed) ? parsed : 1;
        return choices[(Math.Max(1, skillNumber) - 1) % choices.Length];
    }

    private static void CriarCoroaDeStatus(
        PanelContainer cluster,
        TalentNodeResource source,
        bool unlocked,
        bool canUnlock)
    {
        int optionCount = Math.Min(3, source.StatOptionIds.Length);
        float totalWidth = optionCount * SkillStatusNodeSize + Math.Max(0, optionCount - 1) * 7f;
        float startX = (SkillClusterWidth - totalWidth) * 0.5f;

        for (int index = 0; index < optionCount; index++)
        {
            string statId = source.StatOptionIds[index];
            float value = source.StatOptionValues != null && index < source.StatOptionValues.Length
                ? source.StatOptionValues[index]
                : 0.1f;
            var option = new PanelContainer
            {
                Position = new Vector2(startX + index * (SkillStatusNodeSize + 7f), 5f + Math.Abs(index - 1) * 5f),
                Size = Vector2.One * SkillStatusNodeSize,
                CustomMinimumSize = Vector2.One * SkillStatusNodeSize,
                TooltipText = $"{statId}: {value:+0.0;-0.0}\nOpção de status desta habilidade",
                MouseFilter = MouseFilterEnum.Pass,
            };

            Color border = TalentNodeResource.ObterCorTipo(TalentNodeType.Attribute).Darkened(0.35f);
            Color bg = CorNormal;
            if (unlocked) { bg = new Color(0.16f, 0.24f, 0.48f); border = CorDesbloqueado; }
            else if (canUnlock) { bg = new Color(0.16f, 0.28f, 0.32f); border = CorDisponivel; }
            Estilo(option, bg, border, SkillStatusNodeSize);

            var label = new Label
            {
                Text = AbreviarStatus(statId),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                MouseFilter = MouseFilterEnum.Ignore,
            };
            label.AddThemeFontSizeOverride("font_size", 7);
            option.AddChild(label);
            cluster.AddChild(option);
        }
    }

    private static void CriarFallbackDeIcone(Control parent, TalentNodeResource node, Texture2D icon)
    {
        if (icon != null) return;

        var fallback = new Label
        {
            Text = node.NodeType switch
            {
                TalentNodeType.Skill => "?",
                TalentNodeType.Unlock => "D",
                _ => "+",
            },
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        fallback.AddThemeFontSizeOverride("font_size", node.NodeType == TalentNodeType.Skill ? 22 : 13);
        fallback.AddThemeColorOverride("font_color", new Color(0.88f, 0.9f, 0.94f));
        parent.AddChild(fallback);
    }

    private static void CriarOpcoesDeStatus(
        PanelContainer slot,
        TalentNodeResource node,
        bool unlocked,
        bool canUnlock,
        Vector2 groupCenter,
        Vector2 boardCenter)
    {
        slot.AddThemeStyleboxOverride("panel", new StyleBoxEmpty());
        var orbitLayer = new Control
        {
            MouseFilter = MouseFilterEnum.Pass,
            CustomMinimumSize = Vector2.One * ChoiceNodeAreaSize,
        };
        slot.AddChild(orbitLayer);

        int optionCount = Math.Min(3, node.StatOptionIds.Length);
        Vector2 radial = groupCenter - boardCenter;
        float radius = Mathf.Max(1f, radial.Length());
        float baseAngle = radius > 1f ? radial.Angle() : -Mathf.Pi * 0.5f;
        float angleStep = Mathf.Clamp(38f / radius, 0.08f, 0.32f);

        for (int index = 0; index < optionCount; index++)
        {
            string statId = node.StatOptionIds[index];
            float value = node.StatOptionValues != null && index < node.StatOptionValues.Length
                ? node.StatOptionValues[index]
                : 0.1f;
            var option = new PanelContainer
            {
                CustomMinimumSize = Vector2.One * AttributeNodeSize,
                Size = Vector2.One * AttributeNodeSize,
                TooltipText = $"{node.Nome}\n{statId}: {value:+0.0;-0.0}\nEscolha exclusiva",
                MouseFilter = MouseFilterEnum.Stop,
            };

            float centeredIndex = index - (optionCount - 1) * 0.5f;
            float optionAngle = baseAngle + centeredIndex * angleStep;
            Vector2 pointOnCircle = boardCenter
                + new Vector2(Mathf.Cos(optionAngle), Mathf.Sin(optionAngle)) * radius;
            Vector2 localCenter = ChoiceNodeAreaSize * Vector2.One * 0.5f
                + (pointOnCircle - groupCenter);
            option.Position = localCenter - Vector2.One * AttributeNodeSize * 0.5f;
            Color border = TalentNodeResource.ObterCorTipo(TalentNodeType.Attribute).Darkened(0.35f);
            Color bg = CorNormal;
            if (unlocked) { bg = new Color(0.16f, 0.24f, 0.48f); border = CorDesbloqueado; }
            else if (canUnlock) { bg = new Color(0.16f, 0.28f, 0.32f); border = CorDisponivel; }
            Estilo(option, bg, border, AttributeNodeSize);

            var label = new Label
            {
                Text = AbreviarStatus(statId),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                MouseFilter = MouseFilterEnum.Ignore,
            };
            label.AddThemeFontSizeOverride("font_size", 8);
            option.AddChild(label);
            orbitLayer.AddChild(option);
        }
    }

    private static string AbreviarStatus(string statId)
    {
        if (string.IsNullOrWhiteSpace(statId)) return "+";
        var letras = statId.Where(char.IsUpper).Take(3).ToArray();
        return letras.Length >= 2
            ? new string(letras).ToUpperInvariant()
            : statId[..Math.Min(3, statId.Length)].ToUpperInvariant();
    }

    private static string CriarTooltip(TalentNodeResource node)
    {
        var linhas = new List<string>
        {
            node.Nome,
            TalentNodeResource.ObterRotuloTipo(node.NodeType),
            node.Descricao,
            $"Custo: {node.CustoPontos} ponto(s) | Nível: {node.NivelMinimo}",
        };
        if (!string.IsNullOrWhiteSpace(node.StatId) && !Mathf.IsZeroApprox(node.BonusValor))
            linhas.Add($"{node.StatId}: {node.BonusValor:+0.0;-0.0}");
        if (node.TemEscolhaDeStatus)
            linhas.Add("Escolha uma das três opções deste grupo.");
        return string.Join("\n", linhas.Where(l => !string.IsNullOrWhiteSpace(l)));
    }

    private void CriarRotuloCaminho(TalentNodeResource node, Vector2 center)
    {
        Vector2 offset = node.NodeId.Contains("sniper", StringComparison.Ordinal)
            ? new Vector2(-190f, -12f)
            : node.NodeId.Contains("ranger", StringComparison.Ordinal)
                ? new Vector2(10f, -12f)
                : new Vector2(-90f, 28f);
        var label = new Label
        {
            Text = node.Nome.ToUpperInvariant(),
            Position = center + offset,
            Size = new Vector2(180f, 24f),
            HorizontalAlignment = HorizontalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        label.AddThemeFontSizeOverride("font_size", 14);
        label.AddThemeColorOverride("font_color", TalentNodeResource.ObterCorTipo(node.NodeType));
        _board.AddChild(label);
    }

    private static void Estilo(PanelContainer slot, Color bg, Color border, float nodeSize)
    {
        int corner = Mathf.IsEqualApprox(nodeSize, SkillNodeSize) ? 7 : (int)(nodeSize * 0.5f);
        slot.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = bg,
            BorderWidthLeft = 3,
            BorderWidthTop = 3,
            BorderWidthRight = 3,
            BorderWidthBottom = 3,
            BorderColor = border,
            CornerRadiusTopLeft = corner,
            CornerRadiusTopRight = corner,
            CornerRadiusBottomRight = corner,
            CornerRadiusBottomLeft = corner,
            ContentMarginLeft = 0,
            ContentMarginTop = 0,
            ContentMarginRight = 0,
            ContentMarginBottom = 0,
        });
    }
}
