using Godot;

public partial class SkillBarSlotUI : Panel
{
    private TextureRect _icon;
    private Label _keyLabel;
    private string _keyName;
    private SkillResource _assignedSkill;
    private ItemResource _assignedItem;
    private int _assignedItemInventorySlot = -1;
    private SkillBarUI _owner;

    public int Row { get; set; }
    public int Col { get; set; }

    public SkillResource AssignedSkill => _assignedSkill;
    public ItemResource AssignedItem => _assignedItem;
    public int AssignedItemInventorySlot => _assignedItemInventorySlot;
    public bool IsItemSlot => _assignedItem != null;

    public void Initialize(string keyName, SkillBarUI owner)
    {
        _keyName = keyName;
        _owner = owner;
        CustomMinimumSize = new Vector2(42, 42);
        AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.12f, 0.12f, 0.18f, 0.9f),
            BorderColor = new Color(0.3f, 0.3f, 0.4f),
            BorderWidthBottom = 1,
            BorderWidthLeft = 1,
            BorderWidthRight = 1,
            BorderWidthTop = 1,
            CornerRadiusBottomLeft = 3,
            CornerRadiusBottomRight = 3,
            CornerRadiusTopLeft = 3,
            CornerRadiusTopRight = 3,
        });

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
        AtualizarVisual();
        _owner?.ClearSlot(Row, Col);
    }

    private void AtualizarVisual()
    {
        if (_assignedSkill != null)
        {
            _icon.Texture = _assignedSkill.Icone;
            _keyLabel.Text = _assignedSkill.Nome;
            TooltipText = $"{_assignedSkill.Nome}\n{_assignedSkill.Descricao}";
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
        var obj = data.AsGodotObject();
        if (obj is SkillResource) return true;
        if (obj is SkillBarSlotUI) return true;
        if (obj is SlotUI slot)
            return slot.SlotInterno?.Item?.Tipo == TipoEquipamento.Consumivel;
        return false;
    }

    public override void _DropData(Vector2 position, Variant data)
    {
        if (data.AsGodotObject() is SkillResource skill && _owner != null)
        {
            _owner.AssignSkill(Row, Col, skill);
        }
        else if (data.AsGodotObject() is SkillBarSlotUI sourceSlot && _owner != null)
        {
            if (sourceSlot._assignedSkill != null)
                _owner.AssignSkill(Row, Col, sourceSlot._assignedSkill);
            else if (sourceSlot._assignedItem != null)
                _owner.AssignItem(Row, Col, sourceSlot._assignedItem, sourceSlot._assignedItemInventorySlot);
            sourceSlot.Clear();
        }
        else if (data.AsGodotObject() is SlotUI slot && _owner != null)
        {
            var item = slot.SlotInterno?.Item;
            if (item != null && item.Tipo == TipoEquipamento.Consumivel)
            {
                _owner.AssignItem(Row, Col, item, slot.SlotIndex);
            }
        }
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

        return this;
    }
}
