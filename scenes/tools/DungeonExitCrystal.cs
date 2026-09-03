using Godot;

[Tool]
public partial class DungeonExitCrystal : Node2D
{
    public override void _Ready()
    {
        AddToGroup("dungeon_exit_crystal");
        if (Engine.IsEditorHint())
            return;

        SetUnlocked(false);
    }

    public void SetUnlocked(bool unlocked)
    {
        Visible = unlocked;
        var area = GetNodeOrNull<Area2D>("TeleportPairMarker");
        if (area != null)
        {
            area.Monitoring = unlocked;
            area.Monitorable = unlocked;
        }

        var sprite = GetNodeOrNull<AnimatedSprite2D>("CrystalAnimation");
        if (sprite == null)
            return;

        if (unlocked)
            sprite.Play("active");
        else
            sprite.Stop();
    }
}
