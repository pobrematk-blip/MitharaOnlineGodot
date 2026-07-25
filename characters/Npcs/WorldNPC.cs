using Godot;
using System;

public partial class WorldNPC : CharacterBody2D
{
    [Export] public string NpcName { get; set; } = "";
    [Export] public string NpcRace { get; set; } = "Humano";
    [Export] public string AnimPrefix { get; set; } = "padrao";
    [Export] public string DialogId { get; set; } = "";
    [Export] public string PrefabId { get; set; } = "";

    private static Font _boldFont;

    public override void _Ready()
    {
        AddToGroup("NPC");
        ZIndex = 0;
        ZAsRelative = true;
        YSortEnabled = false;
        CharacterBody2DDefaultSetup();

        string sheetPath = ConstruirPathSprite();
        if (!string.IsNullOrEmpty(sheetPath) && ResourceLoader.Exists(sheetPath))
        {
            CriarSprite(sheetPath);
        }
        else
        {
            GD.PrintErr($"[WorldNPC] Sprite sheet não encontrada: {sheetPath}");
        }

        CriarLabels();
    }

    private void CharacterBody2DDefaultSetup()
    {
        var col = new CollisionShape2D();
        col.Shape = new CircleShape2D { Radius = 40f };
        AddChild(col);

        CollisionLayer = 2u;
        CollisionMask = 1u;
    }

    private string ConstruirPathSprite()
    {
        string race = string.IsNullOrWhiteSpace(NpcRace) ? "Humano" : NpcRace.Trim();
        string npcPath = LpcSpriteFramesBuilder.PastaSpritesNpc + race + ".png";
        if (ResourceLoader.Exists(npcPath))
            return npcPath;
        string raceFile = race.Replace(" ", "");
        string fallbackPath = LpcSpriteFramesBuilder.PastaSpritesRaca + raceFile + ".png";
        if (ResourceLoader.Exists(fallbackPath))
            return fallbackPath;
        return null;
    }

    private void CriarSprite(string sheetPath)
    {
        var sheet = ResourceLoader.Load<Texture2D>(sheetPath);
        if (sheet == null) return;

        string prefix = string.IsNullOrWhiteSpace(AnimPrefix) ? "padrao" : AnimPrefix;
        var frames = LpcSpriteFramesBuilder.Construir(sheet, prefix);
        if (frames == null || frames.GetAnimationNames().Length == 0) return;

        var sprite = new AnimatedSprite2D();
        sprite.Name = "AnimatedSprite";
        sprite.Position = new Vector2(0, -5);
        sprite.Scale = Vector2.One * 2f;
        sprite.SpriteFrames = frames;

        if (frames.HasAnimation("idle_down"))
            sprite.Play("idle_down");
        else
            sprite.Play(frames.GetAnimationNames()[0]);

        AddChild(sprite);
    }

    private void CriarLabels()
    {
        var labelName = new Label
        {
            Name = "NameLabel",
            Text = NpcName,
            Position = new Vector2(-70, -68),
            ZIndex = 2,
            Size = new Vector2(140, 24),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        AplicarEstiloNomeNpc(labelName);
        AddChild(labelName);

        var prompt = new Label();
        prompt.Text = "[F] Falar";
        prompt.Name = "InteractPrompt";
        prompt.Position = new Vector2(-40, -45);
        prompt.ZIndex = 2;
        prompt.Size = new Vector2(80, 0);
        prompt.HorizontalAlignment = HorizontalAlignment.Center;
        prompt.AddThemeFontSizeOverride("font_size", 18);
        prompt.AddThemeColorOverride("font_color", new Color(1.0f, 1.0f, 0.3f));
        prompt.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.8f));
        prompt.AddThemeConstantOverride("outline_size", 2);
        prompt.Visible = false;
        AddChild(prompt);
    }

    private static void AplicarEstiloNomeNpc(Label label)
    {
        label.AddThemeFontOverride("font", GetBoldFont());
        label.AddThemeFontSizeOverride("font_size", 16);
        label.AddThemeColorOverride("font_color", new Color(1.0f, 0.86f, 0.16f, 1f));
        label.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 1f));
        label.AddThemeConstantOverride("outline_size", 5);
        label.AddThemeConstantOverride("shadow_offset_x", 1);
        label.AddThemeConstantOverride("shadow_offset_y", 1);
        label.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.9f));
    }

    private static Font GetBoldFont()
    {
        if (_boldFont != null)
            return _boldFont;

        var fnt = ResourceLoader.Load<Font>("res://fonts/Montserrat-Variable.ttf");
        if (fnt != null)
        {
            var variation = new FontVariation();
            variation.SetBaseFont(fnt);
            variation.SetVariationEmbolden(1.0f);
            _boldFont = variation;
        }
        else
        {
            _boldFont = ThemeDB.GetProjectTheme().DefaultFont;
        }

        return _boldFont;
    }
}
