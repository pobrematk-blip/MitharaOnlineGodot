using Godot;
using System;

public partial class SkillBarUI : Control
{
    private const int SLOT_COUNT = 10;
    private const int SLOT_SIZE = 42;

    private SkillBarSlotUI[,] _slots = new SkillBarSlotUI[2, SLOT_COUNT];

    private PanelContainer _barContainer;
    private Panel _titleBar;
    private Button _lockButton;
    private bool _locked = true;
    private bool _arrastando;
    private Vector2 _pontoCliqueOriginal;

    private ProgressBar _xpBar;
    private Label _xpLabel;
    private LevelProgressionComponent _xpLevelComp;
    private PlayerSkillComponent _skillComp;
    private bool _connectRetryScheduled;

    private static readonly string[] NumKeys = { "1", "2", "3", "4", "5", "6", "7", "8", "9", "0" };
    private static readonly string[] FuncKeys = { "F1", "F2", "F3", "F4", "F5", "F6", "F7", "F8", "F9", "F10" };

    public override void _Ready()
    {
        BuildUI();
        RegisterInputActions();
        CallDeferred(nameof(ConnectXpBar));
    }

    private void BuildUI()
    {
        _barContainer = new PanelContainer();
        _barContainer.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0, 0, 0, 0.55f),
            BorderColor = new Color(0.35f, 0.35f, 0.45f),
            BorderWidthBottom = 1,
            BorderWidthLeft = 1,
            BorderWidthRight = 1,
            BorderWidthTop = 1,
            CornerRadiusBottomLeft = 6,
            CornerRadiusBottomRight = 6,
            CornerRadiusTopLeft = 6,
            CornerRadiusTopRight = 6,
            ContentMarginBottom = 0,
            ContentMarginLeft = 0,
            ContentMarginRight = 0,
            ContentMarginTop = 0,
        });
        AddChild(_barContainer);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 0);
        _barContainer.AddChild(vbox);

        BuildTitleBar(vbox);
        BuildXpSection(vbox);
        BuildSlotRows(vbox);

        CallDeferred(nameof(CenterBar));
    }

    private void BuildTitleBar(VBoxContainer parent)
    {
        _titleBar = new Panel();
        _titleBar.CustomMinimumSize = new Vector2(0, 18);
        _titleBar.MouseDefaultCursorShape = CursorShape.Arrow;
        _titleBar.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.2f, 0.2f, 0.28f, 0.8f),
            BorderColor = new Color(0.4f, 0.4f, 0.5f),
            BorderWidthBottom = 1,
            BorderWidthLeft = 0,
            BorderWidthRight = 0,
            BorderWidthTop = 0,
            CornerRadiusBottomLeft = 0,
            CornerRadiusBottomRight = 0,
            CornerRadiusTopLeft = 6,
            CornerRadiusTopRight = 6,
        });
        _titleBar.GuiInput += OnTitleBarGuiInput;
        parent.AddChild(_titleBar);

        var hbox = new HBoxContainer();
        hbox.SizeFlagsHorizontal = SizeFlags.Fill;
        hbox.SizeFlagsVertical = SizeFlags.Fill;
        hbox.AddThemeConstantOverride("separation", 0);
        _titleBar.AddChild(hbox);

        var label = new Label();
        label.Text = "≡  Habilidades";
        label.HorizontalAlignment = HorizontalAlignment.Center;
        label.VerticalAlignment = VerticalAlignment.Center;
        label.SizeFlagsHorizontal = SizeFlags.Expand;
        label.SizeFlagsVertical = SizeFlags.Fill;
        label.AddThemeFontSizeOverride("font_size", 10);
        label.AddThemeColorOverride("font_color", new Color(1, 1, 1, 0.7f));
        hbox.AddChild(label);

        _lockButton = new Button();
        _lockButton.CustomMinimumSize = new Vector2(20, 18);
        _lockButton.Flat = true;
        _lockButton.Text = "\U0001f512";
        _lockButton.AddThemeFontSizeOverride("font_size", 10);
        _lockButton.Pressed += ToggleLock;
        hbox.AddChild(_lockButton);
    }

    private void BuildXpSection(VBoxContainer parent)
    {
        var xpPanel = new PanelContainer();
        xpPanel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0, 0, 0, 0.3f),
            BorderColor = new Color(0.2f, 0.2f, 0.3f),
            BorderWidthBottom = 1,
            BorderWidthLeft = 0,
            BorderWidthRight = 0,
            BorderWidthTop = 0,
            ContentMarginBottom = 2,
            ContentMarginLeft = 4,
            ContentMarginRight = 4,
            ContentMarginTop = 2,
        });
        parent.AddChild(xpPanel);

        var innerVBox = new VBoxContainer();
        innerVBox.AddThemeConstantOverride("separation", 1);
        xpPanel.AddChild(innerVBox);

        _xpLabel = new Label();
        _xpLabel.Text = "N\u00edvel 1 | XP 0/100";
        _xpLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _xpLabel.SizeFlagsHorizontal = SizeFlags.Fill;
        _xpLabel.AddThemeFontSizeOverride("font_size", 8);
        _xpLabel.AddThemeColorOverride("font_color", new Color(1, 1, 1, 0.6f));
        innerVBox.AddChild(_xpLabel);

        _xpBar = new ProgressBar();
        _xpBar.CustomMinimumSize = new Vector2(0, 10);
        _xpBar.MaxValue = 1.0;
        _xpBar.Value = 0.0;
        _xpBar.ShowPercentage = false;
        _xpBar.AddThemeStyleboxOverride("background", new StyleBoxFlat
        {
            BgColor = new Color(0.08f, 0.08f, 0.12f, 0.9f),
            BorderColor = new Color(0.25f, 0.25f, 0.35f),
            BorderWidthBottom = 1,
            BorderWidthLeft = 1,
            BorderWidthRight = 1,
            BorderWidthTop = 1,
            CornerRadiusBottomLeft = 2,
            CornerRadiusBottomRight = 2,
            CornerRadiusTopLeft = 2,
            CornerRadiusTopRight = 2,
        });
        _xpBar.AddThemeStyleboxOverride("fill", new StyleBoxFlat
        {
            BgColor = new Color(0.85f, 0.65f, 0.15f, 1.0f),
            CornerRadiusBottomLeft = 1,
            CornerRadiusBottomRight = 1,
            CornerRadiusTopLeft = 1,
            CornerRadiusTopRight = 1,
        });
        innerVBox.AddChild(_xpBar);
    }

    private void ConnectXpBar()
    {
        var player = GetTree()?.CurrentScene?.FindChild("Player", true, false) as Player;
        if (player == null)
        {
            GD.Print("[SKILL BAR] Player n\u00e3o encontrado para conectar barra de XP.");
            ScheduleConnectRetry();
            return;
        }

        _xpLevelComp = player.FindChild("LevelProgressionComponent", true, false) as LevelProgressionComponent;
        if (_xpLevelComp == null)
        {
            GD.Print("[SKILL BAR] LevelProgressionComponent n\u00e3o encontrado no Player.");
            ScheduleConnectRetry();
            return;
        }

        _connectRetryScheduled = false;
        _xpLevelComp.ProgressaoAtualizada -= UpdateXpBar;
        _xpLevelComp.ProgressaoAtualizada += UpdateXpBar;
        _skillComp = player.FindChild("PlayerSkillComponent", true, false) as PlayerSkillComponent;
        if (_skillComp != null)
        {
            _skillComp.SkillSlotsAtualizados -= RefreshSkillSlotsFromComponent;
            _skillComp.SkillSlotsAtualizados += RefreshSkillSlotsFromComponent;
            RefreshSkillSlotsFromComponent();
        }
        else
        {
            ScheduleConnectRetry();
        }
        UpdateXpBar();
        GD.Print("[SKILL BAR] Barra de XP conectada ao Player.");
    }

    private void ScheduleConnectRetry()
    {
        if (_connectRetryScheduled || !IsInsideTree())
            return;

        _connectRetryScheduled = true;
        var timer = GetTree().CreateTimer(0.25);
        timer.Timeout += () =>
        {
            _connectRetryScheduled = false;
            if (IsInsideTree())
                ConnectXpBar();
        };
    }

    private void UpdateXpBar()
    {
        if (_xpLevelComp == null) return;

        int nivel = _xpLevelComp.Nivel;
        int xp = _xpLevelComp.ExperienciaAtual;

        // Fallback: tenta ler do GameNetwork se o componente ainda estiver com valores default
        if (nivel <= 1 && xp <= 0 && (GetNodeOrNull("/root/GameNetwork") is GameNetwork gn))
        {
            if (gn._pendingLevel > 1 || gn._pendingXp > 0)
            {
                nivel = gn._pendingLevel;
                xp = (int)gn._pendingXp;
            }
        }

        _xpBar.Value = _xpLevelComp.ProgressoXp;
        _xpLabel.Text = $"N\u00edvel {nivel} | XP {xp}/{_xpLevelComp.ExperienciaProximoLevel}";
    }

    private void BuildSlotRows(VBoxContainer parent)
    {
        var slotPanel = new PanelContainer();
        slotPanel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0, 0, 0, 0.35f),
            BorderColor = new Color(0, 0, 0, 0),
            BorderWidthBottom = 0,
            BorderWidthLeft = 0,
            BorderWidthRight = 0,
            BorderWidthTop = 0,
            ContentMarginBottom = 4,
            ContentMarginLeft = 4,
            ContentMarginRight = 4,
            ContentMarginTop = 3,
        });
        parent.AddChild(slotPanel);

        var innerVBox = new VBoxContainer();
        innerVBox.AddThemeConstantOverride("separation", 2);
        slotPanel.AddChild(innerVBox);

        var hbox1 = new HBoxContainer();
        hbox1.AddThemeConstantOverride("separation", 2);
        hbox1.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
        innerVBox.AddChild(hbox1);

        var hbox2 = new HBoxContainer();
        hbox2.AddThemeConstantOverride("separation", 2);
        hbox2.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
        innerVBox.AddChild(hbox2);

        for (int i = 0; i < SLOT_COUNT; i++)
        {
            hbox1.AddChild(CreateSlot(FuncKeys[i], 0, i));
            hbox2.AddChild(CreateSlot(NumKeys[i], 1, i));
        }
    }

    private SkillBarSlotUI CreateSlot(string keyName, int row, int col)
    {
        var slot = new SkillBarSlotUI();
        slot.Row = row;
        slot.Col = col;
        slot.Initialize(keyName, this);
        _slots[row, col] = slot;
        return slot;
    }

    private void CenterBar()
    {
        var viewportSize = GetViewportRect().Size;
        var barSize = _barContainer.Size;
        _barContainer.Position = new Vector2(
            (viewportSize.X - barSize.X) * 0.5f + 200f,
            viewportSize.Y - barSize.Y - 10f
        );
    }

    private void ToggleLock()
    {
        _locked = !_locked;
        _lockButton.Text = _locked ? "\U0001f512" : "\U0001f513";
        _titleBar.MouseDefaultCursorShape = _locked ? CursorShape.Arrow : CursorShape.Move;
        GD.Print($"[SKILL BAR] {( _locked ? "Trancada" : "Destrancada" )}");
    }

    private void OnTitleBarGuiInput(InputEvent @event)
    {
        if (_locked) return;

        if (@event is InputEventMouseButton mouseEvent && mouseEvent.ButtonIndex == MouseButton.Left)
        {
            if (mouseEvent.Pressed)
            {
                _arrastando = true;
                _pontoCliqueOriginal = mouseEvent.Position;
            }
            else
            {
                _arrastando = false;
            }
        }
        else if (@event is InputEventMouseMotion mouseMotion && _arrastando)
        {
            _barContainer.Position += mouseMotion.Position - _pontoCliqueOriginal;
        }
    }

    private void RegisterInputActions()
    {
        Key[] numKeys = { Key.Key1, Key.Key2, Key.Key3, Key.Key4, Key.Key5, Key.Key6, Key.Key7, Key.Key8, Key.Key9, Key.Key0 };
        Key[] funcKeys = { Key.F1, Key.F2, Key.F3, Key.F4, Key.F5, Key.F6, Key.F7, Key.F8, Key.F9, Key.F10 };

        for (int i = 0; i < SLOT_COUNT; i++)
        {
            string name = i < 9 ? $"skill_{i + 1}" : "skill_0";
            RegisterAction(name, numKeys[i]);
        }

        for (int i = 0; i < SLOT_COUNT; i++)
        {
            string name = $"skill_f{i + 1}";
            RegisterAction(name, funcKeys[i]);
        }
    }

    private void RegisterAction(string actionName, Key keycode)
    {
        if (!InputMap.HasAction(actionName))
        {
            InputMap.AddAction(actionName);
            var ev = new InputEventKey();
            ev.Keycode = keycode;
            InputMap.ActionAddEvent(actionName, ev);
            GD.Print($"[SKILL BAR] Input action '{actionName}' registered to key {keycode}");
        }
    }

    private void AtivarSkill(int row, int col)
    {
        string slotId = row == 0 ? $"F{col + 1}" : (col < 9 ? $"{col + 1}" : "0");

        GD.Print($"[SKILL BAR] Slot [{slotId}] activated!");

        _slots[row, col].Modulate = new Color(1.6f, 1.6f, 1.3f);
        var tween = CreateTween();
        tween.TweenProperty(_slots[row, col], "modulate", Colors.White, 0.12f);

        // Tenta ativar skill via PlayerSkillComponent
        var player = GetTree()?.CurrentScene?.FindChild("Player", true, false) as Node;
        if (player != null)
        {
            var comp = player.FindChild("PlayerSkillComponent", true, false) as PlayerSkillComponent;
            if (comp != null)
            {
                int slotIndex = row * SLOT_COUNT + col;
                comp.ActivateSlotIndex(slotIndex);
            }
        }
    }

    // Assign a skill to a slot (called by slot UI on drop)
    public void AssignSkill(int row, int col, SkillResource skill)
    {
        var player = GetTree()?.CurrentScene?.FindChild("Player", true, false) as Player;
        if (player == null)
        {
            GD.Print("[SKILL BAR] Player não encontrado para atribuir skill.");
            return;
        }

        var comp = player.FindChild("PlayerSkillComponent", true, false) as PlayerSkillComponent;
        if (comp == null)
        {
            GD.Print("[SKILL BAR] PlayerSkillComponent não encontrado no Player.");
            return;
        }

        int idx = row * SLOT_COUNT + col;
        if (idx >= 0 && idx < comp.SkillSlots.Length)
        {
            comp.ItemSlots[idx] = null;
            if (comp.ItemSlotIndexes != null && idx < comp.ItemSlotIndexes.Length)
                comp.ItemSlotIndexes[idx] = -1;
            var gameNet = GetNodeOrNull<GameNetwork>("/root/GameNetwork");
            if (gameNet == null || !gameNet.IsConnected)
            {
                GD.PrintErr("[SKILL BAR] Sem conexão. Atribuição de skill só pode ser feita pelo servidor.");
                return;
            }

            gameNet.SendSetSkillSlot(idx, skill?.SkillId ?? 0);
            GD.Print($"[SKILL BAR] Pedido ao servidor para atribuir skill '{skill?.Nome}' ao slot {row},{col} (idx {idx}).");
        }
    }

    // Assign an item to a slot (called by slot UI on drop from inventory)
    public void AssignItem(int row, int col, ItemResource item, int inventorySlot = -1)
    {
        var player = GetTree()?.CurrentScene?.FindChild("Player", true, false) as Player;
        if (player == null)
        {
            GD.Print("[SKILL BAR] Player não encontrado para atribuir item.");
            return;
        }

        var comp = player.FindChild("PlayerSkillComponent", true, false) as PlayerSkillComponent;
        if (comp == null)
        {
            GD.Print("[SKILL BAR] PlayerSkillComponent não encontrado no Player.");
            return;
        }

        int idx = row * SLOT_COUNT + col;
        if (idx >= 0 && idx < comp.ItemSlots.Length)
        {
            bool hadSkill = comp.SkillSlots[idx] != null;
            comp.SkillSlots[idx] = null;
            comp.ItemSlots[idx] = item;
            if (comp.ItemSlotIndexes != null && idx < comp.ItemSlotIndexes.Length)
                comp.ItemSlotIndexes[idx] = inventorySlot;
            _slots[row, col]?.SetItem(item, inventorySlot);
            if (hadSkill)
            {
                var gameNet = GetNodeOrNull<GameNetwork>("/root/GameNetwork");
                gameNet?.SendSetSkillSlot(idx, 0);
            }
            GD.Print($"[SKILL BAR] Item '{item?.Nome}' atribuido ao slot {row},{col} (idx {idx}) usando inventario slot {inventorySlot}.");
        }
    }

    public void ClearSlot(int row, int col)
    {
        var player = GetTree()?.CurrentScene?.FindChild("Player", true, false) as Player;
        if (player == null) return;

        var comp = player.FindChild("PlayerSkillComponent", true, false) as PlayerSkillComponent;
        if (comp == null) return;

        int idx = row * SLOT_COUNT + col;
        if (idx >= 0 && idx < comp.SkillSlots.Length)
        {
            bool hadSkill = comp.SkillSlots[idx] != null;
            bool hadItem = comp.ItemSlots[idx] != null;
            comp.SkillSlots[idx] = null;
            comp.ItemSlots[idx] = null;
            if (comp.ItemSlotIndexes != null && idx < comp.ItemSlotIndexes.Length)
                comp.ItemSlotIndexes[idx] = -1;
            _slots[row, col]?.SetSkill(null);
            if (hadSkill)
            {
                var gameNet = GetNodeOrNull<GameNetwork>("/root/GameNetwork");
                gameNet?.SendSetSkillSlot(idx, 0);
            }
            GD.Print($"[SKILL BAR] Slot {row},{col} (idx {idx}) limpo. tinhaSkill={hadSkill}, tinhaItem={hadItem}");
        }
    }

    private void RefreshSkillSlotsFromComponent()
    {
        if (_skillComp == null)
            return;

        for (int row = 0; row < 2; row++)
        {
            for (int col = 0; col < SLOT_COUNT; col++)
            {
                int idx = row * SLOT_COUNT + col;
                if (idx >= 0 && idx < _skillComp.SkillSlots.Length)
                {
                    if (_skillComp.SkillSlots[idx] != null)
                        _slots[row, col]?.SetSkill(_skillComp.SkillSlots[idx]);
                    else if (_skillComp.ItemSlots != null && idx < _skillComp.ItemSlots.Length && _skillComp.ItemSlots[idx] != null)
                    {
                        int invSlot = _skillComp.ItemSlotIndexes != null && idx < _skillComp.ItemSlotIndexes.Length
                            ? _skillComp.ItemSlotIndexes[idx]
                            : -1;
                        _slots[row, col]?.SetItem(_skillComp.ItemSlots[idx], invSlot);
                    }
                    else
                        _slots[row, col]?.SetSkill(null);
                }
            }
        }
    }

    public override void _Input(InputEvent @event)
    {
        if (GetViewport().GuiGetFocusOwner() is LineEdit) return;

        for (int i = 0; i < SLOT_COUNT; i++)
        {
            string actionName = i < 9 ? $"skill_{i + 1}" : "skill_0";
            if (@event.IsActionPressed(actionName, false))
            {
                AtivarSkill(1, i);
                GetViewport().SetInputAsHandled();
                return;
            }
        }

        for (int i = 0; i < SLOT_COUNT; i++)
        {
            string actionName = $"skill_f{i + 1}";
            if (@event.IsActionPressed(actionName, false))
            {
                AtivarSkill(0, i);
                GetViewport().SetInputAsHandled();
                return;
            }
        }
    }
}
