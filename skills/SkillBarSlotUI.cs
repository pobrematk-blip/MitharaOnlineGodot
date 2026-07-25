using Godot;

public partial class SkillBarSlotUI : Panel
{
    private TextureRect _icon;
    private Label _keyLabel;
    private ColorRect _cooldownOverlay;
    private Label _cooldownLabel;
    private string _keyName;
    private SkillResource _assignedSkill;
    private ItemResource _assignedItem;
    private int _assignedItemInventorySlot = -1;
    private SkillBarUI _owner;
    private float _cooldownRemaining;
    private float _cooldownDuration;
    private bool _draggingFromThisSlot;

    public int Row { get; set; }
    public int Col { get; set; }

    public SkillResource AssignedSkill => _assignedSkill;
    public ItemResource AssignedItem => _assignedItem;
    public int AssignedItemInventorySlot => _assignedItemInventorySlot;
    public bool IsItemSlot => _assignedItem != null;
    public bool IsCoolingDown => _cooldownRemaining > 0f;

    public void Initialize(string keyName, SkillBarUI owner)
    {
        _keyName = keyName;
        _owner = owner;
        CustomMinimumSize = new Vector2(42, 42);
        AddThemeStyleboxOverride("panel", MitharaUiTheme.Slot());

        _icon = new TextureRect();
        _icon.Name = "Icon";
        _icon.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
        _icon.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
        _icon.CustomMinimumSize = new Vector2(40, 40);
        _icon.Size = new Vector2(40, 40);
        _icon.Position = new Vector2(1, 1);
        _icon.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(_icon);

        _keyLabel = new Label();
        _keyLabel.Text = keyName;
        _keyLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _keyLabel.VerticalAlignment = VerticalAlignment.Center;
        _keyLabel.AddThemeFontSizeOverride("font_size", 9);
        _keyLabel.AddThemeColorOverride("font_color", new Color(1, 1, 1, 0.9f));
        _keyLabel.MouseFilter = MouseFilterEnum.Ignore;
        _keyLabel.ZIndex = 1;
        AddChild(_keyLabel);

        _cooldownOverlay = new ColorRect
        {
            Color = new Color(0f, 0f, 0f, 0.68f),
            MouseFilter = MouseFilterEnum.Ignore,
            Visible = false,
            ZIndex = 2,
        };
        _cooldownOverlay.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(_cooldownOverlay);

        _cooldownLabel = new Label
        {
            MouseFilter = MouseFilterEnum.Ignore,
            Visible = false,
            ZIndex = 3,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        _cooldownLabel.SetAnchorsPreset(LayoutPreset.FullRect);
        _cooldownLabel.AddThemeFontSizeOverride("font_size", 13);
        _cooldownLabel.AddThemeColorOverride("font_color", Colors.White);
        AddChild(_cooldownLabel);
    }

    public void SetSkill(SkillResource skill)
    {
        _assignedItem = null;
        _assignedItemInventorySlot = -1;
        _assignedSkill = skill;
        AtualizarVisual();
    }

    public void SetItem(ItemResource item, int inventorySlot = -1)
    {
        _assignedSkill = null;
        _assignedItem = item;
        _assignedItemInventorySlot = item != null ? inventorySlot : -1;
        AtualizarVisual();
    }

    public void Clear()
    {
        _assignedSkill = null;
        _assignedItem = null;
        _assignedItemInventorySlot = -1;
        _cooldownRemaining = 0f;
        AtualizarCooldownVisual();
        AtualizarVisual();
        _owner?.ClearSlot(Row, Col);
    }

    public void StartCooldown(float duration)
    {
        if (duration <= 0f)
            return;

        _cooldownDuration = duration;
        _cooldownRemaining = duration;
        AtualizarCooldownVisual();
        SetProcess(true);
    }

    private void AtualizarVisual()
    {
        if (_assignedSkill != null)
        {
            _icon.Texture = _assignedSkill.Icone;
            _keyLabel.Text = _assignedSkill.Nome;
            TooltipText = _assignedSkill.ObterDescricaoCompleta();
        }
        else if (_assignedItem != null)
        {
            _icon.Texture = _assignedItem.Icone;
            _keyLabel.Text = _assignedItem.Nome;
            TooltipText = $"{_assignedItem.Nome}\n{_assignedItem.Descricao}";
        }
        else
        {
            _icon.Texture = null;
            _keyLabel.Text = _keyName ?? string.Empty;
            TooltipText = string.Empty;
        }
    }

    public override bool _CanDropData(Vector2 position, Variant data)
    {
        if (TryGetSkillFromDragData(data, out _)) return true;

        if (TryGetSkillBarSlotFromDragData(data, out _)) return true;
        if (TryGetInventoryDrop(data, out var item, out _))
            return IsConsumableShortcut(item);
        return false;
    }

    public override void _DropData(Vector2 position, Variant data)
    {
        if (TryGetSkillFromDragData(data, out var skill) && _owner != null)
        {
            _owner.AssignSkill(Row, Col, skill);
        }
        else if (TryGetSkillBarSlotFromDragData(data, out var sourceSlot) && _owner != null)
        {
            if (sourceSlot == this)
                return;

            if (sourceSlot._assignedSkill != null)
                _owner.AssignSkill(Row, Col, sourceSlot._assignedSkill);
            else if (sourceSlot._assignedItem != null)
                _owner.AssignItem(Row, Col, sourceSlot._assignedItem, sourceSlot._assignedItemInventorySlot);
            sourceSlot.Clear();
        }
        else if (TryGetInventoryDrop(data, out var item, out int inventorySlot) && _owner != null)
        {
            if (IsConsumableShortcut(item))
            {
                _owner.AssignItem(Row, Col, item, inventorySlot);
            }
        }
    }

    private static bool IsConsumableShortcut(ItemResource item)
    {
        if (item == null)
            return false;

        return item.Tipo == TipoEquipamento.Consumivel
            || (item.ItemID >= 100 && item.ItemID < 200);
    }

    private static bool TryGetInventoryDrop(Variant data, out ItemResource item, out int inventorySlot)
    {
        item = null;
        inventorySlot = -1;

        if (TryGetInventorySlotFromDragData(data, out var slot))
        {
            item = slot.SlotInterno?.Item;
            inventorySlot = slot.SlotIndex;
            return item != null && inventorySlot >= 0;
        }

        if (data.VariantType != Variant.Type.Dictionary)
            return false;

        var dict = data.AsGodotDictionary();
        if (dict.ContainsKey("item") && dict["item"].VariantType == Variant.Type.Object)
            item = dict["item"].Obj as ItemResource ?? dict["item"].AsGodotObject() as ItemResource;

        inventorySlot = GetOptionalInt(dict, "slot");
        if (inventorySlot < 0)
            inventorySlot = GetOptionalInt(dict, "inventory_slot");
        if (inventorySlot < 0)
            inventorySlot = GetOptionalInt(dict, "slot_index");

        return item != null && inventorySlot >= 0;
    }

    private static bool TryGetInventorySlotFromDragData(Variant data, out SlotUI slot)
    {
        slot = null;

        if (data.VariantType == Variant.Type.Object)
        {
            slot = data.Obj as SlotUI ?? data.AsGodotObject() as SlotUI;
            return slot != null;
        }

        if (data.VariantType != Variant.Type.Dictionary)
            return false;

        var dict = data.AsGodotDictionary();
        if (dict.ContainsKey("slot") && dict["slot"].VariantType == Variant.Type.Object)
        {
            slot = dict["slot"].Obj as SlotUI ?? dict["slot"].AsGodotObject() as SlotUI;
            return slot != null;
        }

        if (dict.ContainsKey("source") && dict["source"].VariantType == Variant.Type.Object)
        {
            slot = dict["source"].Obj as SlotUI ?? dict["source"].AsGodotObject() as SlotUI;
            return slot != null;
        }

        return false;
    }

    private static bool TryGetSkillBarSlotFromDragData(Variant data, out SkillBarSlotUI slot)
    {
        slot = null;

        if (data.VariantType == Variant.Type.Object)
        {
            slot = data.Obj as SkillBarSlotUI ?? data.AsGodotObject() as SkillBarSlotUI;
            return slot != null;
        }

        return false;
    }

    private bool TryGetSkillFromDragData(Variant data, out SkillResource skill)
    {
        skill = null;

        if (data.AsGodotObject() is SkillResource directSkill)
        {
            skill = directSkill;
            return true;
        }

        if (data.VariantType != Variant.Type.Dictionary)
            return false;

        var dict = data.AsGodotDictionary();
        string kind = GetString(dict, "kind");
        if (!string.Equals(kind, "skill", System.StringComparison.OrdinalIgnoreCase))
            return false;

        string skillPath = GetString(dict, "skill_path");
        int skillId = GetInt(dict, "skill_id");

        if (!string.IsNullOrWhiteSpace(skillPath))
            skill = ResourceLoader.Load<SkillResource>(skillPath);

        if (skill == null && skillId > 0)
            skill = ResolveSkillById(skillId);

        if (skill == null)
            GD.PrintErr($"[SKILL BAR] Drop de skill recebido, mas nao consegui resolver a skill. id={skillId}, path='{skillPath}'");

        return skill != null;
    }

    private static string GetString(Godot.Collections.Dictionary dict, string key)
    {
        return dict.ContainsKey(key) ? dict[key].AsString() : string.Empty;
    }

    private static int GetInt(Godot.Collections.Dictionary dict, string key)
    {
        return dict.ContainsKey(key) ? dict[key].AsInt32() : 0;
    }

    private static int GetOptionalInt(Godot.Collections.Dictionary dict, string key)
    {
        return dict.ContainsKey(key) ? dict[key].AsInt32() : -1;
    }

    private SkillResource ResolveSkillById(int skillId)
    {
        var player = GetTree()?.CurrentScene?.FindChild("Player", true, false) as Player;
        var comp = player?.FindChild("PlayerSkillComponent", true, false) as PlayerSkillComponent;
        return comp?.ObterSkillPorId(skillId);
    }

    public override Variant _GetDragData(Vector2 position)
    {
        if (_assignedSkill == null && _assignedItem == null)
            return default;

        var preview = new TextureRect();
        preview.Texture = _icon.Texture;
        preview.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
        preview.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
        preview.CustomMinimumSize = new Vector2(40, 40);
        preview.Size = new Vector2(40, 40);
        SetDragPreview(preview);

        _draggingFromThisSlot = true;
        return this;
    }

    public override void _Notification(int what)
    {
        if (what != NotificationDragEnd || !_draggingFromThisSlot)
            return;

        _draggingFromThisSlot = false;
        if (GetViewport()?.GuiIsDragSuccessful() == true)
            return;

        Clear();
    }

    public override void _Process(double delta)
    {
        if (_cooldownRemaining <= 0f)
        {
            SetProcess(false);
            return;
        }

        _cooldownRemaining = Mathf.Max(0f, _cooldownRemaining - (float)delta);
        AtualizarCooldownVisual();
        if (_cooldownRemaining <= 0f)
            SetProcess(false);
    }

    private void AtualizarCooldownVisual()
    {
        bool active = _cooldownRemaining > 0f;
        if (_cooldownOverlay != null)
            _cooldownOverlay.Visible = active;
        if (_cooldownLabel != null)
        {
            _cooldownLabel.Visible = active;
            _cooldownLabel.Text = active ? Mathf.CeilToInt(_cooldownRemaining).ToString() : "";
        }
    }
}
