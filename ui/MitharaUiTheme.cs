using Godot;

public static class MitharaUiTheme
{
    public static readonly Color PanelBg = new(0.015f, 0.02f, 0.035f, 0.88f);
    public static readonly Color PanelBgStrong = new(0.015f, 0.02f, 0.035f, 0.95f);
    public static readonly Color TitleBg = new(0.025f, 0.035f, 0.055f, 0.94f);
    public static readonly Color InnerBg = new(0.02f, 0.025f, 0.04f, 0.72f);
    public static readonly Color SlotBg = new(0.025f, 0.03f, 0.05f, 0.9f);
    public static readonly Color Border = new(0.32f, 0.36f, 0.46f, 0.9f);
    public static readonly Color BorderMuted = new(0.22f, 0.25f, 0.34f, 0.75f);
    public static readonly Color Text = new(0.9f, 0.92f, 1f, 0.92f);
    public static readonly Color TextMuted = new(0.66f, 0.7f, 0.82f, 0.78f);
    public static readonly Color Accent = new(0.91f, 0.77f, 0.28f, 1f);

    public static StyleBoxFlat Panel(float alpha = 0.88f, int radius = 5)
        => Box(new Color(PanelBg.R, PanelBg.G, PanelBg.B, alpha), Border, radius, 1);

    public static StyleBoxFlat Inner(float alpha = 0.72f, int radius = 4)
        => Box(new Color(InnerBg.R, InnerBg.G, InnerBg.B, alpha), BorderMuted, radius, 1);

    public static StyleBoxFlat Slot(bool active = false)
        => Box(active ? new Color(0.04f, 0.07f, 0.11f, 0.9f) : SlotBg,
            active ? new Color(0.42f, 0.55f, 0.76f, 0.9f) : BorderMuted, 4, 1);

    public static StyleBoxFlat TitleBar()
    {
        var style = Box(TitleBg, Border, 5, 1);
        style.BorderWidthLeft = 0;
        style.BorderWidthRight = 0;
        style.BorderWidthTop = 0;
        style.CornerRadiusBottomLeft = 0;
        style.CornerRadiusBottomRight = 0;
        return style;
    }

    public static StyleBoxFlat BarBackground(Color tint)
        => Box(tint, BorderMuted, 3, 1);

    public static StyleBoxFlat Fill(Color color, int radius = 2)
    {
        var style = new StyleBoxFlat { BgColor = color };
        style.SetCornerRadiusAll(radius);
        return style;
    }

    public static StyleBoxFlat Box(Color bg, Color border, int radius, int borderWidth)
    {
        var style = new StyleBoxFlat
        {
            BgColor = bg,
            BorderColor = border,
        };
        style.SetCornerRadiusAll(radius);
        style.SetBorderWidthAll(borderWidth);
        return style;
    }
}
