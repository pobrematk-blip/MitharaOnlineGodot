using Godot;

public partial class EditorOnly : Control
{
    private GameEditorUI _editor;

    public override void _Ready()
    {
        var scene = ResourceLoader.Load<PackedScene>("res://Editor/GameEditorUI.tscn");
        if (scene == null)
        {
            GD.PrintErr("GameEditorUI.tscn nao encontrado.");
            return;
        }

        _editor = scene.Instantiate<GameEditorUI>();
        if (_editor == null) return;

        AddChild(_editor);
        CallDeferred(nameof(MostrarEditor));
    }

    private void MostrarEditor()
    {
        if (_editor == null) return;

        _editor.AbrirEditor(0);

        var panel = _editor.GetNodeOrNull<Panel>("Panel");
        if (panel != null)
        {
            panel.Visible = true;
            panel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        }
    }
}
