using Godot;

public partial class Main : Node2D
{
    public override void _Ready()
    {
        AplicarTemaGlobal();

        var world = GetNodeOrNull<Node2D>("World");
        if (world != null)
            world.YSortEnabled = true;
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
