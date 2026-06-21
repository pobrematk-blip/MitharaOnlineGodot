using System;
using System.Collections.Generic;

namespace Mithara.Server.World.Pathfinding;

public class PathFollower
{
    private readonly PathfindingGrid _grid;
    private List<(int gx, int gy)>? _path;
    private int _waypointIndex;
    private float _targetX;
    private float _targetY;
    public bool HasPath => _path != null && _waypointIndex < _path.Count;
    public bool IsFinished => _path == null || _waypointIndex >= _path.Count;

    public float TargetX => _targetX;
    public float TargetY => _targetY;

    public PathFollower(PathfindingGrid grid)
    {
        _grid = grid;
    }

    public void SetDestination(float startX, float startY, float destX, float destY, int maxSteps = 500)
    {
        var (sgx, sgy) = _grid.WorldToGrid(startX, startY);
        var (dgx, dgy) = _grid.WorldToGrid(destX, destY);

        _path = AStar.FindPath(_grid, sgx, sgy, dgx, dgy, maxSteps);
        _waypointIndex = 0;

        if (_path != null && _path.Count > 1 && _path[0].gx == sgx && _path[0].gy == sgy)
        {
            _waypointIndex = 1;
        }

        if (_path != null && _waypointIndex < _path.Count)
        {
            var (wgx, wgy) = _path[_waypointIndex];
            var (wx, wy) = _grid.GridToWorld(wgx, wgy);
            _targetX = wx;
            _targetY = wy;
        }
        else
        {
            Stop();
        }
    }

    public void Stop()
    {
        _path = null;
        _waypointIndex = 0;
    }

    public (float x, float y, float dirX, float dirY, bool moving) MoveToward(float currentX, float currentY, float speed, float dt)
    {
        if (IsFinished)
            return (currentX, currentY, 0, 0, false);

        const float waypointReachDist = 8f;
        float remainingMove = speed * dt;
        float startX = currentX;
        float startY = currentY;

        while (HasPath && remainingMove > 0.001f)
        {
            float dx = _targetX - currentX;
            float dy = _targetY - currentY;
            float dist = MathF.Sqrt(dx * dx + dy * dy);

            if (dist <= waypointReachDist)
            {
                _waypointIndex++;
                if (_waypointIndex >= _path!.Count)
                {
                    _path = null;
                    break;
                }

                var (nextGx, nextGy) = _path[_waypointIndex];
                (_targetX, _targetY) = _grid.GridToWorld(nextGx, nextGy);
                continue;
            }

            float step = MathF.Min(remainingMove, dist);
            currentX += dx / dist * step;
            currentY += dy / dist * step;
            remainingMove -= step;
        }

        float movedX = currentX - startX;
        float movedY = currentY - startY;
        float moved = MathF.Sqrt(movedX * movedX + movedY * movedY);
        return moved > 0.001f
            ? (currentX, currentY, movedX / moved, movedY / moved, true)
            : (currentX, currentY, 0f, 0f, false);
    }
}
