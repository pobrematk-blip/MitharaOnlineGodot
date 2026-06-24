using Godot;

public partial class TalentNodeSlotUI : PanelContainer
{
    public TalentNodeResource NodeData { get; set; }
    public bool IsUnlocked { get; set; }

    public override Variant _GetDragData(Vector2 position)
    {
        if (!IsUnlocked || NodeData == null || NodeData.HabilidadeAtiva == null)
            return default;

        var preview = new TextureRect();
        preview.Texture = NodeData.HabilidadeAtiva.Icone;
        preview.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
        preview.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
        preview.CustomMinimumSize = new Vector2(40, 40);
        preview.Size = new Vector2(40, 40);
        SetDragPreview(preview);
        return NodeData.HabilidadeAtiva;
    }
}
