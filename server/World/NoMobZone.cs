namespace Mithara.Server.World;

public class NoMobZone
{
    public float X { get; set; }
    public float Y { get; set; }
    public float Width { get; set; }
    public float Height { get; set; }

    public bool Contains(float px, float py)
    {
        return px >= X && px <= X + Width && py >= Y && py <= Y + Height;
    }
}
