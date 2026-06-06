using Godot;

/// <summary>
/// Visual suave para sprites — filtro linear, leve suavização e cores naturais.
/// </summary>
public static class SpriteVisualUtil
{
    // Shader desativado — não compatível com Godot 4 canvas_item

    public static void AplicarPixelArt(CanvasItem sprite)
    {
        AplicarVisualSuave(sprite);
    }

    public static void AplicarVisualSuave(CanvasItem sprite)
    {
        if (sprite == null) return;

        sprite.TextureFilter = CanvasItem.TextureFilterEnum.Linear;
        sprite.TextureRepeat = CanvasItem.TextureRepeatEnum.Disabled;
        sprite.Material = ObterMaterialSuave();
    }

    public static void AplicarVisualSuave(AnimatedSprite2D sprite, float escala)
    {
        if (sprite == null) return;

        AplicarVisualSuave(sprite as CanvasItem);
        sprite.Scale = new Vector2(escala, escala);
    }

    public static void AplicarPixelArt(AnimatedSprite2D sprite, float escala = 3f)
    {
        AplicarVisualSuave(sprite, escala);
    }

    private static ShaderMaterial ObterMaterialSuave()
    {
        // Shader desativado temporariamente (não compativel com Godot 4 canvas_item)
        return null;
    }
}
