using System;
using System.Collections.Generic;

namespace Mithara.Server.World.Pathfinding;

public class PathfindingGrid
{
    private readonly bool[] _cells;
    public int Width { get; }
    public int Height { get; }
    public float CellSize { get; }
    public float OriginX { get; }
    public float OriginY { get; }

    public PathfindingGrid(int width, int height, float cellSize, float originX = 0f, float originY = 0f)
    {
        Width = width;
        Height = height;
        CellSize = cellSize;
        OriginX = originX;
        OriginY = originY;
        _cells = new bool[width * height];

        for (int i = 0; i < _cells.Length; i++)
            _cells[i] = true;
    }

    public void SetBlocked(int gx, int gy, bool blocked)
    {
        if (gx >= 0 && gx < Width && gy >= 0 && gy < Height)
            _cells[gy * Width + gx] = !blocked;
    }

    public bool IsWalkable(int gx, int gy)
    {
        if (gx < 0 || gx >= Width || gy < 0 || gy >= Height)
            return false;
        return _cells[gy * Width + gx];
    }

    public bool IsWalkableWorld(float wx, float wy)
    {
        var (gx, gy) = WorldToGrid(wx, wy);
        return IsWalkable(gx, gy);
    }

    public (int gx, int gy) WorldToGrid(float wx, float wy)
    {
        int gx = (int)MathF.Floor((wx - OriginX) / CellSize);
        int gy = (int)MathF.Floor((wy - OriginY) / CellSize);
        return (gx, gy);
    }

    public (float wx, float wy) GridToWorld(int gx, int gy)
    {
        float wx = OriginX + (gx + 0.5f) * CellSize;
        float wy = OriginY + (gy + 0.5f) * CellSize;
        return (wx, wy);
    }

    public (float wx, float wy) GridCornerToWorld(int gx, int gy)
    {
        float wx = OriginX + gx * CellSize;
        float wy = OriginY + gy * CellSize;
        return (wx, wy);
    }

    public void ApplyBlockedAreas(List<BlockedArea> areas)
    {
        foreach (var area in areas)
        {
            var (minGx, minGy) = WorldToGrid(area.X, area.Y);
            var (maxGx, maxGy) = WorldToGrid(area.X + area.Width, area.Y + area.Height);

            for (int gy = minGy; gy <= maxGy; gy++)
            {
                for (int gx = minGx; gx <= maxGx; gx++)
                {
                    SetBlocked(gx, gy, true);
                }
            }
        }
    }
}
