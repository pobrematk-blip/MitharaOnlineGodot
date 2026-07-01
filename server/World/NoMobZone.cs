namespace Mithara.Server.World;

public class NoMobZonePoint
{
    public float X { get; set; }
    public float Y { get; set; }
}

public class NoMobZone
{
    public float X { get; set; }
    public float Y { get; set; }
    public float Width { get; set; }
    public float Height { get; set; }
    public List<NoMobZonePoint> Points { get; set; } = new();

    public bool Contains(float px, float py)
    {
        if (Points.Count >= 3)
            return ContainsPolygon(px, py);

        return px >= X && px <= X + Width && py >= Y && py <= Y + Height;
    }

    private bool ContainsPolygon(float px, float py)
    {
        bool inside = false;
        int j = Points.Count - 1;

        for (int i = 0; i < Points.Count; i++)
        {
            var pi = Points[i];
            var pj = Points[j];
            bool intersects = ((pi.Y > py) != (pj.Y > py))
                && (px < (pj.X - pi.X) * (py - pi.Y) / ((pj.Y - pi.Y) == 0f ? 0.0001f : (pj.Y - pi.Y)) + pi.X);

            if (intersects)
                inside = !inside;

            j = i;
        }

        return inside;
    }
}
