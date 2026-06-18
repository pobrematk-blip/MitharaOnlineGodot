using Godot;

public partial class Main : Node2D
{
    public override void _Ready()
    {
        AplicarTemaGlobal();

        var world = GetNodeOrNull<Node2D>("World");
        if (world != null)
        {
            world.YSortEnabled = true;
            GarantirPlayerNoWorld(world);
        }
    }

    private void GarantirPlayerNoWorld(Node2D world)
    {
        var player = GetNodeOrNull<Node2D>("Player");
        if (player == null)
            return;

        Vector2 globalPosition = player.GlobalPosition;
        player.Reparent(world);
        player.GlobalPosition = globalPosition;
        player.ZIndex = 0;
        player.ZAsRelative = true;
        GD.Print("[YSORT] Player movido para World para participar da ordenacao por Y.");
    }

    private void AplicarTemaGlobal()
    {
        var regularFile = ResourceLoader.Load<FontFile>("res://fonts/Montserrat-Variable.ttf");
        if (regularFile == null) return;

        var tema = new Theme();
        tema.DefaultFont = regularFile;
        tema.SetFontSize("font_size", "Label", 14);
        tema.SetFontSize("font_size", "Button", 14);
        tema.SetFontSize("font_size", "RichTextLabel", 13);
        tema.SetFontSize("font_size", "LineEdit", 14);
        tema.SetFontSize("font_size", "TextEdit", 14);
        tema.SetFontSize("font_size", "Window", 13);

        GetTree().Root.Theme = tema;
    }
}
