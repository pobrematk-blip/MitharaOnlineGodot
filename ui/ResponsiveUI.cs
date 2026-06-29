using Godot;

public static class ResponsiveUI
{
    public static Vector2 FitToViewport(Control control, Vector2 preferredSize, Vector2 minimumSize, float margin = 12f)
    {
        if (control == null)
            return Vector2.Zero;

        Vector2 viewport = control.GetViewportRect().Size;
        Vector2 available = new(
            Mathf.Max(minimumSize.X, viewport.X - margin * 2f),
            Mathf.Max(minimumSize.Y, viewport.Y - margin * 2f));

        Vector2 size = new(
            Mathf.Clamp(preferredSize.X, minimumSize.X, available.X),
            Mathf.Clamp(preferredSize.Y, minimumSize.Y, available.Y));

        control.CustomMinimumSize = size;
        control.Size = size;
        return size;
    }

    public static void CenterInViewport(Control control, Vector2 preferredSize, Vector2 minimumSize, float margin = 12f)
    {
        if (control == null)
            return;

        Vector2 size = FitToViewport(control, preferredSize, minimumSize, margin);
        Vector2 viewport = control.GetViewportRect().Size;
        control.Position = new Vector2(
            Mathf.Max(margin, (viewport.X - size.X) * 0.5f),
            Mathf.Max(margin, (viewport.Y - size.Y) * 0.5f));
    }

    public static void ClampInsideViewport(Control control, float margin = 8f)
    {
        if (control == null)
            return;

        Vector2 viewport = control.GetViewportRect().Size;
        Vector2 size = control.Size;
        if (size.X <= 0f || size.Y <= 0f)
            size = control.CustomMinimumSize;

        control.Position = new Vector2(
            Mathf.Clamp(control.Position.X, margin, Mathf.Max(margin, viewport.X - size.X - margin)),
            Mathf.Clamp(control.Position.Y, margin, Mathf.Max(margin, viewport.Y - size.Y - margin)));
    }

    public static void AnchorFullRect(Control control)
    {
        if (control == null)
            return;

        control.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
    }

    public static Vector2 BottomRightPosition(Control control, Vector2 size, Vector2 margin)
    {
        Vector2 viewport = control.GetViewportRect().Size;
        return new Vector2(
            Mathf.Max(margin.X, viewport.X - size.X - margin.X),
            Mathf.Max(margin.Y, viewport.Y - size.Y - margin.Y));
    }
}
