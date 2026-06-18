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

        if (_path != null && _path.Count > 0)
        {
            var (wgx, wgy) = _path[_waypointIndex];
            var (wx, wy) = _grid.GridToWorld(wgx, wgy);
            _targetX = wx;
            _targetY = wy;
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
        float moveDist = speed * dt;

        while (true)
        {
            float dx = _targetX - currentX;
            float dy = _targetY - currentY;
            float dist = MathF.Sqrt(dx * dx + dy * dy);

            if (dist >= waypointReachDist && dist > moveDist)
            {
                float ratio = MathF.Min(moveDist / dist, 1f);
                float newX = currentX + dx * ratio;
                float newY = currentY + dy * ratio;
                float dirX = dx / dist;
                float dirY = dy / dist;
                return (newX, newY, dirX, dirY, true);
            }

            _waypointIndex++;
            if (_waypointIndex >= _path!.Count)
            {
                _path = null;
                float lastDirX = dx / dist;
                float lastDirY = dy / dist;
                return (currentX, currentY, lastDirX, lastDirY, false);
            }

            var (wgx, wgy) = _path[_waypointIndex];
            var (wx, wy) = _grid.GridToWorld(wgx, wgy);
            _targetX = wx;
            _targetY = wy;

            if (dist <= moveDist)
            {
                currentX = _targetX;
                currentY = _targetY;
                moveDist -= dist;
                if (moveDist < 0.001f)
                {
                    float lastDirX = dx / dist;
                    float lastDirY = dy / dist;
                    return (currentX, currentY, lastDirX, lastDirY, false);
                }
                continue;
            }

            dx = _targetX - currentX;
            dy = _targetY - currentY;
            dist = MathF.Sqrt(dx * dx + dy * dy);

            if (dist < 0.001f)
            {
                return (currentX, currentY, 0, 0, false);
            }

            float ratio2 = MathF.Min(moveDist / dist, 1f);
            float newX2 = currentX + dx * ratio2;
            float newY2 = currentY + dy * ratio2;
            float dirX2 = dx / dist;
            float dirY2 = dy / dist;
            return (newX2, newY2, dirX2, dirY2, true);
        }
    }
}
