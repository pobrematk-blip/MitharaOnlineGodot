#nullable enable
using Godot;

public static class VisualStateReset
{
    private static readonly string[] RootOverlayNames =
    {
        "LoadingScreen",
        "DuelArenaOverlay",
        "DuelResultFlagOverlay",
        "TalentTooltipLayer",
    };

    public static void PrepararTelaSelecao(SceneTree? tree)
    {
        if (tree == null)
            return;

        LimparOverlaysGlobais(tree);
        Input.MouseMode = Input.MouseModeEnum.Visible;

        if (tree.Root != null)
            tree.Root.Theme = null;
    }

    public static void LimparOverlaysGlobais(SceneTree? tree)
    {
        var root = tree?.Root;
        if (root == null)
            return;

        foreach (var name in RootOverlayNames)
            RemoverFilhoRoot(root, name);
    }

    private static void RemoverFilhoRoot(Window root, string name)
    {
        var node = root.GetNodeOrNull(name);
        if (node == null)
            return;

        if (GodotObject.IsInstanceValid(node))
            node.QueueFree();
    }
}
