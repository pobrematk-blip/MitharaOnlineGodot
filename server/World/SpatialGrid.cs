namespace Mithara.Server.World;

public class SpatialGrid
{
    public const float CellSize = 200f;

    private readonly Dictionary<long, HashSet<ulong>> _cells = new();
    private readonly Dictionary<ulong, (int cx, int cy)> _entityCells = new();

    private static long PackCell(int cx, int cy) => ((long)cx << 32) | (uint)cy;

    public void AddEntity(ulong entityId, float x, float y)
    {
        int cx = (int)Math.Floor(x / CellSize);
        int cy = (int)Math.Floor(y / CellSize);
        long key = PackCell(cx, cy);

        if (!_cells.ContainsKey(key))
            _cells[key] = new HashSet<ulong>();
        _cells[key].Add(entityId);
        _entityCells[entityId] = (cx, cy);
    }

    public void MoveEntity(ulong entityId, float oldX, float oldY, float newX, float newY)
    {
        int ocx = (int)Math.Floor(oldX / CellSize);
        int ocy = (int)Math.Floor(oldY / CellSize);
        int ncx = (int)Math.Floor(newX / CellSize);
        int ncy = (int)Math.Floor(newY / CellSize);

        if (ocx == ncx && ocy == ncy) return;

        long oldKey = PackCell(ocx, ocy);
        if (_cells.TryGetValue(oldKey, out var oldSet))
        {
            oldSet.Remove(entityId);
            if (oldSet.Count == 0) _cells.Remove(oldKey);
        }

        long newKey = PackCell(ncx, ncy);
        if (!_cells.ContainsKey(newKey))
            _cells[newKey] = new HashSet<ulong>();
        _cells[newKey].Add(entityId);
        _entityCells[entityId] = (ncx, ncy);
    }

    public void RemoveEntity(ulong entityId)
    {
        if (!_entityCells.TryGetValue(entityId, out var cell)) return;
        long key = PackCell(cell.cx, cell.cy);
        if (_cells.TryGetValue(key, out var set))
        {
            set.Remove(entityId);
            if (set.Count == 0) _cells.Remove(key);
        }
        _entityCells.Remove(entityId);
    }

    public HashSet<ulong> GetEntitiesInRadius(float x, float y, float radius)
    {
        var result = new HashSet<ulong>();
        int minCx = (int)Math.Floor((x - radius) / CellSize);
        int maxCx = (int)Math.Floor((x + radius) / CellSize);
        int minCy = (int)Math.Floor((y - radius) / CellSize);
        int maxCy = (int)Math.Floor((y + radius) / CellSize);

        float radiusSq = radius * radius;

        for (int cx = minCx; cx <= maxCx; cx++)
        {
            for (int cy = minCy; cy <= maxCy; cy++)
            {
                long key = PackCell(cx, cy);
                if (_cells.TryGetValue(key, out var set))
                {
                    foreach (var eid in set)
                        result.Add(eid);
                }
            }
        }
        return result;
    }

    public void Clear()
    {
        _cells.Clear();
        _entityCells.Clear();
    }
}
