using Godot;

public static class DirectionUtil
{
    public static string VectorToDirectionString(Vector2 vec)
    {
        if (vec.LengthSquared() < 0.001f) return "down";
        vec = vec.Normalized();
        float angle = Mathf.RadToDeg(Mathf.Atan2(vec.Y, vec.X));

        if (angle < -157.5f || angle >= 157.5f) return "left";
        if (angle < -112.5f) return "up_left";
        if (angle < -67.5f) return "up";
        if (angle < -22.5f) return "up_right";
        if (angle < 22.5f) return "right";
        if (angle < 67.5f) return "down_right";
        if (angle < 112.5f) return "down";
        return "down_left";
    }

    public static string VectorToCardinal(Vector2 vec)
    {
        if (vec.LengthSquared() < 0.001f) return "down";
        vec = vec.Normalized();
        if (Mathf.Abs(vec.X) > Mathf.Abs(vec.Y))
            return vec.X < 0 ? "left" : "right";
        return vec.Y < 0 ? "up" : "down";
    }

    public static string DirectionToCardinal(string dir)
    {
        return dir switch
        {
            "up_right" or "up_left" => "up",
            "down_right" or "down_left" => "down",
            _ => dir
        };
    }

    public static Vector2 DirectionToVector(string dir)
    {
        return dir switch
        {
            "up" => Vector2.Up,
            "down" => Vector2.Down,
            "left" => Vector2.Left,
            "right" => Vector2.Right,
            "up_right" => new Vector2(0.7071f, -0.7071f),
            "up_left" => new Vector2(-0.7071f, -0.7071f),
            "down_right" => new Vector2(0.7071f, 0.7071f),
            "down_left" => new Vector2(-0.7071f, 0.7071f),
            _ => Vector2.Down
        };
    }
}
