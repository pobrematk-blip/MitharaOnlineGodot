using Godot;
using System;
using System.Collections.Generic;

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
    private HBoxContainer _buffContainer;
    private readonly Dictionary<int, BuffIconUI> _activeBuffIcons = new();
    private VipIconUI _vipIcon;

    private static readonly string[] NumKeys = { "1", "2", "3", "4", "5", "6", "7", "8", "9", "0" };
    private static readonly string[] FuncKeys = { "F1", "F2", "F3", "F4", "F5", "F6", "F7", "F8", "F9", "F10" };

    public override void _Ready()
    {
        BuildUI();
        RegisterInputActions();
        CallDeferred(nameof(ConnectXpBar));
        CallDeferred(nameof(ConnectVipStatus));
    }

    public override void _ExitTree()
    {
        var net = GetNodeOrNull<GameNetwork>("/root/GameNetwork");
        if (net != null)
            net.OnVipStatus -= OnVipStatus;
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

    private void ConnectVipStatus()
    {
        var net = GetNodeOrNull<GameNetwork>("/root/GameNetwork");
        if (net == null)
            return;

        net.OnVipStatus -= OnVipStatus;
        net.OnVipStatus += OnVipStatus;
        if (net.VipExpiryBinary != 0)
            OnVipStatus(net.VipExpiryBinary);
    }

    private void OnVipStatus(long expiryBinary)
    {
        var expiry = DateTime.FromBinary(expiryBinary);
        if (expiry <= DateTime.UtcNow)
        {
            if (_vipIcon != null && IsInstanceValid(_vipIcon))
                _vipIcon.QueueFree();
            _vipIcon = null;
            return;
        }

        EnsureBuffContainer();
        if (_buffContainer == null)
            return;

        if (_vipIcon != null && IsInstanceValid(_vipIcon))
        {
            _vipIcon.Restart(expiry);
            return;
        }

        _vipIcon = new VipIconUI(expiry);
        _vipIcon.Expired += () =>
        {
            if (_vipIcon != null && IsInstanceValid(_vipIcon))
                _vipIcon.QueueFree();
            _vipIcon = null;
        };
        _buffContainer.AddChild(_vipIcon);
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
        var slot = _slots[row, col];
        if (slot == null)
            return;
        if (slot.IsCoolingDown)
        {
            GD.Print($"[SKILL BAR] Slot [{slotId}] ainda em cooldown.");
            return;
        }

        slot.Modulate = new Color(1.6f, 1.6f, 1.3f);
        var tween = CreateTween();
        tween.TweenProperty(slot, "modulate", Colors.White, 0.12f);

        // Tenta ativar skill via PlayerSkillComponent
        var player = GetTree()?.CurrentScene?.FindChild("Player", true, false) as Node;
        if (player != null)
        {
            var comp = player.FindChild("PlayerSkillComponent", true, false) as PlayerSkillComponent;
            if (comp != null)
            {
                int slotIndex = row * SLOT_COUNT + col;
                comp.ActivateSlotIndex(slotIndex);
                var skill = slot.AssignedSkill;
                if (skill != null)
                {
                    slot.StartCooldown(skill.Cooldown);
                    MostrarBuffSeNecessario(skill);
                    AplicarBonusVisualMiraApurada(player, skill);
                }
            }
        }
    }

    private void AplicarBonusVisualMiraApurada(Node player, SkillResource skill)
    {
        if (skill.SkillId != 10202)
            return;

        var equipamento = player.FindChild("EquipamentoComponent", true, false) as EquipamentoComponent;
        if (equipamento == null)
            return;

        equipamento.SetBonusTemporarioMiraApurada(15f, 15f);
        var timer = GetTree().CreateTimer(Mathf.Max(0.1f, skill.Duracao));
        timer.Timeout += () =>
        {
            if (IsInstanceValid(equipamento))
                equipamento.SetBonusTemporarioMiraApurada(0f, 0f);
        };
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

    private void MostrarBuffSeNecessario(SkillResource skill)
    {
        if (skill == null || skill.Duracao <= 0f || skill.Icone == null)
            return;

        bool looksLikeBuff = skill.EffectType is SkillEffectType.Buff
            or SkillEffectType.Heal
            or SkillEffectType.Shield
            or SkillEffectType.Invincibility
            or SkillEffectType.Reflect
            || !string.IsNullOrWhiteSpace(skill.BuffDebuff)
            || !string.IsNullOrWhiteSpace(skill.BuffType);
        if (!looksLikeBuff)
            return;

        EnsureBuffContainer();
        if (_buffContainer == null)
            return;

        if (_activeBuffIcons.TryGetValue(skill.SkillId, out var existing) && IsInstanceValid(existing))
        {
            existing.Restart(skill.Duracao);
            return;
        }

        var icon = new BuffIconUI(skill);
        icon.Expired += () =>
        {
            _activeBuffIcons.Remove(skill.SkillId);
            icon.QueueFree();
        };
        _activeBuffIcons[skill.SkillId] = icon;
        _buffContainer.AddChild(icon);
    }

    private void EnsureBuffContainer()
    {
        if (_buffContainer != null && IsInstanceValid(_buffContainer))
        {
            AtualizarPosicaoBuffContainer();
            return;
        }

        var parent = GetParent();
        if (parent == null)
            return;

        _buffContainer = new HBoxContainer
        {
            Name = "PlayerBuffBar",
            MouseFilter = MouseFilterEnum.Ignore,
            ZIndex = 120,
        };
        _buffContainer.AddThemeConstantOverride("separation", 5);
        parent.AddChild(_buffContainer);
        AtualizarPosicaoBuffContainer();
    }

    private void AtualizarPosicaoBuffContainer()
    {
        if (_buffContainer == null || !IsInstanceValid(_buffContainer))
            return;

        var playerHud = GetTree()?.CurrentScene?.FindChild("PlayerHud", true, false) as Control;
        if (playerHud != null)
            _buffContainer.Position = playerHud.GlobalPosition + new Vector2(64f, playerHud.Size.Y + 6f);
        else
            _buffContainer.Position = new Vector2(78f, 124f);
    }
}

public partial class BuffIconUI : Panel
{
    public event Action Expired;

    private readonly SkillResource _skill;
    private float _remaining;
    private Label _label;

    public BuffIconUI(SkillResource skill)
    {
        _skill = skill;
        _remaining = skill?.Duracao ?? 0f;
        CustomMinimumSize = new Vector2(34, 34);
        Size = new Vector2(34, 34);
        MouseFilter = MouseFilterEnum.Ignore;
    }

    public override void _Ready()
    {
        AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.03f, 0.035f, 0.05f, 0.86f),
            BorderColor = new Color(0.55f, 0.72f, 1f, 0.9f),
            BorderWidthBottom = 1,
            BorderWidthLeft = 1,
            BorderWidthRight = 1,
            BorderWidthTop = 1,
            CornerRadiusBottomLeft = 4,
            CornerRadiusBottomRight = 4,
            CornerRadiusTopLeft = 4,
            CornerRadiusTopRight = 4,
        });

        var icon = new TextureRect
        {
            Texture = _skill?.Icone,
            MouseFilter = MouseFilterEnum.Ignore,
            ExpandMode = TextureRect.ExpandModeEnum.FitWidthProportional,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
            Position = new Vector2(3, 3),
            Size = new Vector2(28, 28),
        };
        AddChild(icon);

        _label = new Label
        {
            MouseFilter = MouseFilterEnum.Ignore,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            ZIndex = 2,
        };
        _label.SetAnchorsPreset(LayoutPreset.FullRect);
        _label.AddThemeFontSizeOverride("font_size", 10);
        _label.AddThemeColorOverride("font_color", Colors.White);
        AddChild(_label);

        TooltipText = $"{_skill?.Nome}\n{_skill?.Descricao}";
        AtualizarLabel();
        SetProcess(true);
    }

    public void Restart(float duration)
    {
        _remaining = duration;
        AtualizarLabel();
        SetProcess(true);
    }

    public override void _Process(double delta)
    {
        _remaining = Mathf.Max(0f, _remaining - (float)delta);
        AtualizarLabel();
        if (_remaining <= 0f)
        {
            SetProcess(false);
            Expired?.Invoke();
        }
    }

    private void AtualizarLabel()
    {
        if (_label != null)
            _label.Text = Mathf.CeilToInt(_remaining).ToString();
    }
}

public partial class VipIconUI : Panel
{
    public event Action Expired;

    private DateTime _expiryUtc;
    private Label _label;

    public VipIconUI(DateTime expiryUtc)
    {
        _expiryUtc = expiryUtc.ToUniversalTime();
        CustomMinimumSize = new Vector2(34, 34);
        Size = new Vector2(34, 34);
        MouseFilter = MouseFilterEnum.Ignore;
    }

    public override void _Ready()
    {
        AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.07f, 0.045f, 0.015f, 0.88f),
            BorderColor = new Color(1f, 0.78f, 0.22f, 0.95f),
            BorderWidthBottom = 1,
            BorderWidthLeft = 1,
            BorderWidthRight = 1,
            BorderWidthTop = 1,
            CornerRadiusBottomLeft = 4,
            CornerRadiusBottomRight = 4,
            CornerRadiusTopLeft = 4,
            CornerRadiusTopRight = 4,
        });

        var icon = new TextureRect
        {
            Texture = ResourceLoader.Load<Texture2D>("res://Itens/Incones/Vip 1.png"),
            MouseFilter = MouseFilterEnum.Ignore,
            ExpandMode = TextureRect.ExpandModeEnum.FitWidthProportional,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
            Position = new Vector2(3, 3),
            Size = new Vector2(28, 28),
        };
        AddChild(icon);

        _label = new Label
        {
            MouseFilter = MouseFilterEnum.Ignore,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            ZIndex = 2,
        };
        _label.SetAnchorsPreset(LayoutPreset.FullRect);
        _label.AddThemeFontSizeOverride("font_size", 9);
        _label.AddThemeColorOverride("font_color", Colors.White);
        _label.AddThemeColorOverride("font_shadow_color", new Color(0f, 0f, 0f, 0.85f));
        _label.AddThemeConstantOverride("shadow_offset_x", 1);
        _label.AddThemeConstantOverride("shadow_offset_y", 1);
        AddChild(_label);

        AtualizarLabel();
        SetProcess(true);
    }

    public void Restart(DateTime expiryUtc)
    {
        _expiryUtc = expiryUtc.ToUniversalTime();
        AtualizarLabel();
        SetProcess(true);
    }

    public override void _Process(double delta)
    {
        AtualizarLabel();
        if (_expiryUtc <= DateTime.UtcNow)
        {
            SetProcess(false);
            Expired?.Invoke();
        }
    }

    private void AtualizarLabel()
    {
        TimeSpan remaining = _expiryUtc - DateTime.UtcNow;
        if (remaining < TimeSpan.Zero)
            remaining = TimeSpan.Zero;

        if (_label != null)
            _label.Text = FormatarTempo(remaining);

        TooltipText = $"VIP ativo\n2x XP e 2x chance de drop\nExpira em: {FormatarTooltip(remaining)}";
    }

    private static string FormatarTempo(TimeSpan remaining)
    {
        if (remaining.TotalDays >= 1)
            return $"{Math.Max(1, (int)Math.Ceiling(remaining.TotalDays))}d";
        if (remaining.TotalHours >= 1)
            return $"{Math.Max(1, (int)Math.Ceiling(remaining.TotalHours))}h";
        return $"{Math.Max(0, (int)Math.Ceiling(remaining.TotalMinutes))}m";
    }

    private static string FormatarTooltip(TimeSpan remaining)
    {
        if (remaining.TotalDays >= 1)
            return $"{(int)remaining.TotalDays}d {remaining.Hours}h";
        if (remaining.TotalHours >= 1)
            return $"{(int)remaining.TotalHours}h {remaining.Minutes}m";
        return $"{remaining.Minutes}m {remaining.Seconds}s";
    }
}
