using System;
using System.Collections.Generic;

namespace Mithara.Server.World.Pathfinding;

public static class AStar
{
    private struct Node : IComparable<Node>
    {
        public int Gx;
        public int Gy;
        public float GCost;
        public float HCost;
        public float FCost => GCost + HCost;
        public int ParentIndex;

        public int CompareTo(Node other)
        {
            int cmp = FCost.CompareTo(other.FCost);
            if (cmp == 0)
                cmp = HCost.CompareTo(other.HCost);
            return cmp;
        }
    }

    private static readonly (int dx, int dy)[] Dirs =
    {
        (0, -1), (1, 0), (0, 1), (-1, 0),
        (1, -1), (1, 1), (-1, 1), (-1, -1),
    };

    public static List<(int gx, int gy)>? FindPath(PathfindingGrid grid, int startGx, int startGy, int endGx, int endGy, int maxSteps = 500)
    {
        if (!grid.IsWalkable(startGx, startGy) || !grid.IsWalkable(endGx, endGy))
            return null;

        if (startGx == endGx && startGy == endGy)
            return new List<(int, int)> { (startGx, startGy) };

        int capacity = grid.Width * grid.Height;
        var nodes = new Node[capacity];
        var closed = new bool[capacity];
        var open = new MinHeap<Node>(capacity);

        int startIdx = startGy * grid.Width + startGx;
        nodes[startIdx] = new Node
        {
            Gx = startGx, Gy = startGy,
            GCost = 0,
            HCost = Heuristic(startGx, startGy, endGx, endGy),
            ParentIndex = -1,
        };
        open.Push(nodes[startIdx]);

        int steps = 0;
        while (open.Count > 0 && steps < maxSteps)
        {
            var current = open.Pop();
            int curIdx = current.Gy * grid.Width + current.Gx;

            if (closed[curIdx]) continue;
            closed[curIdx] = true;

            if (current.Gx == endGx && current.Gy == endGy)
                return ReconstructPath(nodes, grid.Width, curIdx);

            steps++;

            for (int i = 0; i < Dirs.Length; i++)
            {
                int nx = current.Gx + Dirs[i].dx;
                int ny = current.Gy + Dirs[i].dy;
                int nIdx = ny * grid.Width + nx;

                if (!grid.IsWalkable(nx, ny)) continue;
                if (closed[nIdx]) continue;

                bool diagonal = Dirs[i].dx != 0 && Dirs[i].dy != 0;

                if (diagonal)
                {
                    if (!grid.IsWalkable(current.Gx + Dirs[i].dx, current.Gy) ||
                        !grid.IsWalkable(current.Gx, current.Gy + Dirs[i].dy))
                        continue;
                }

                float moveCost = diagonal ? 1.4142f : 1f;
                float gCost = current.GCost + moveCost;
                float hCost = Heuristic(nx, ny, endGx, endGy);

                if (nodes[nIdx].GCost == 0 || gCost < nodes[nIdx].GCost)
                {
                    nodes[nIdx] = new Node
                    {
                        Gx = nx, Gy = ny,
                        GCost = gCost,
                        HCost = hCost,
                        ParentIndex = curIdx,
                    };
                    open.Push(nodes[nIdx]);
                }
            }
        }

        return null;
    }

    private static float Heuristic(int ax, int ay, int bx, int by)
    {
        float dx = Math.Abs(ax - bx);
        float dy = Math.Abs(ay - by);
        return (dx + dy) + (1.4142f - 2f) * MathF.Min(dx, dy);
    }

    private static List<(int gx, int gy)> ReconstructPath(Node[] nodes, int width, int endIdx)
    {
        var path = new List<(int, int)>();
        int idx = endIdx;
        while (idx >= 0)
        {
            var n = nodes[idx];
            path.Add((n.Gx, n.Gy));
            idx = n.ParentIndex;
        }
        path.Reverse();
        return path;
    }

    private class MinHeap<T> where T : IComparable<T>
    {
        private readonly List<T> _items;
        public int Count => _items.Count;

        public MinHeap(int capacity)
        {
            _items = new List<T>(capacity);
        }

        public void Push(T item)
        {
            _items.Add(item);
            int i = _items.Count - 1;
            while (i > 0)
            {
                int p = (i - 1) / 2;
                if (_items[i].CompareTo(_items[p]) >= 0) break;
                (_items[i], _items[p]) = (_items[p], _items[i]);
                i = p;
            }
        }

        public T Pop()
        {
            int last = _items.Count - 1;
            var result = _items[0];
            _items[0] = _items[last];
            _items.RemoveAt(last);

            int i = 0;
            while (true)
            {
                int smallest = i;
                int left = 2 * i + 1;
                int right = 2 * i + 2;

                if (left < _items.Count && _items[left].CompareTo(_items[smallest]) < 0)
                    smallest = left;
                if (right < _items.Count && _items[right].CompareTo(_items[smallest]) < 0)
                    smallest = right;

                if (smallest == i) break;
                (_items[i], _items[smallest]) = (_items[smallest], _items[i]);
                i = smallest;
            }

            return result;
        }
    }
}
