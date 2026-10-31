using Lingjing.Core;
using System;

namespace Lingjing.Nav
{

/// <summary>
/// 流场：从玩家所在格做 Dijkstra，记录每格到目标的距离与"下一个格心"。
/// 追兵只读梯度，不各自跑 A*。每 0.25s（15 tick）重算一次。
/// </summary>
public sealed class FlowField
{
    private static readonly double Sqrt2 = Math.Sqrt(2.0);
    private static readonly int[] NeighborX = { 1, 1, 0, -1, -1, -1, 0, 1 };
    private static readonly int[] NeighborY = { 0, 1, 1, 1, 0, -1, -1, -1 };

    private readonly Grid _grid;
    private readonly double[] _dist;
    private readonly Vec2[] _waypoint;
    private readonly int[] _settledStamp;
    private readonly int[] _seenStamp;
    private int[] _heapNode;
    private double[] _heapKey;
    private int _heapCount;
    private int _stamp;

    public FlowField(Grid grid)
    {
        _grid = grid ?? throw new ArgumentNullException(nameof(grid));
        int n = grid.CellCount;
        _dist = new double[n];
        _waypoint = new Vec2[n];
        _settledStamp = new int[n];
        _seenStamp = new int[n];
        _heapNode = new int[n * 8 + 64];
        _heapKey = new double[n * 8 + 64];
        Array.Fill(_dist, double.PositiveInfinity);
    }

    public int TargetCellX { get; private set; } = -1;
    public int TargetCellY { get; private set; } = -1;
    public int RecomputeCount { get; private set; }

    public void Recompute(Vec2 targetWorld)
    {
        _stamp++;
        RecomputeCount++;

        int tx = Grid.CellOf(targetWorld.X);
        int ty = Grid.CellOf(targetWorld.Y);
        if (!_grid.IsWalkableCell(tx, ty) && !_grid.TryFindNearestWalkable(tx, ty, 6, out tx, out ty))
        {
            return;
        }

        TargetCellX = tx;
        TargetCellY = ty;
        int start = ty * _grid.Width + tx;

        _heapCount = 0;
        _dist[start] = 0.0;
        _seenStamp[start] = _stamp;
        HeapPush(start, 0.0);

        while (_heapCount > 0)
        {
            int node = HeapPop();
            if (_settledStamp[node] == _stamp)
            {
                continue;
            }

            _settledStamp[node] = _stamp;
            int cx = node % _grid.Width;
            int cy = node / _grid.Width;
            double d = _dist[node];

            for (int k = 0; k < 8; k++)
            {
                int nx = cx + NeighborX[k];
                int ny = cy + NeighborY[k];
                if (!_grid.IsWalkableCell(nx, ny))
                {
                    continue;
                }

                bool diagonal = NeighborX[k] != 0 && NeighborY[k] != 0;
                if (diagonal && (!_grid.IsWalkableCell(cx + NeighborX[k], cy) || !_grid.IsWalkableCell(cx, cy + NeighborY[k])))
                {
                    continue;
                }

                int ni = ny * _grid.Width + nx;
                if (_settledStamp[ni] == _stamp)
                {
                    continue;
                }

                double tentative = d + (diagonal ? Sqrt2 : 1.0);
                if (_seenStamp[ni] == _stamp && tentative >= _dist[ni])
                {
                    continue;
                }

                _seenStamp[ni] = _stamp;
                _dist[ni] = tentative;
                HeapPush(ni, tentative);
            }
        }

        // 梯度：每格指向距离最小的邻格格心；无更近邻格时指向自己（死路）。
        for (int y = 0; y < _grid.Height; y++)
        {
            for (int x = 0; x < _grid.Width; x++)
            {
                int idx = y * _grid.Width + x;
                if (!_grid.IsWalkableCell(x, y) || _settledStamp[idx] != _stamp)
                {
                    _waypoint[idx] = Grid.CellCenter(x, y);
                    continue;
                }

                double best = _dist[idx];
                Vec2 bestPoint = Grid.CellCenter(x, y);
                for (int k = 0; k < 8; k++)
                {
                    int nx = x + NeighborX[k];
                    int ny = y + NeighborY[k];
                    if (!_grid.IsWalkableCell(nx, ny))
                    {
                        continue;
                    }

                    bool diagonal = NeighborX[k] != 0 && NeighborY[k] != 0;
                    if (diagonal && (!_grid.IsWalkableCell(x + NeighborX[k], y) || !_grid.IsWalkableCell(x, y + NeighborY[k])))
                    {
                        continue;
                    }

                    int ni = ny * _grid.Width + nx;
                    if (_settledStamp[ni] == _stamp && _dist[ni] < best)
                    {
                        best = _dist[ni];
                        bestPoint = Grid.CellCenter(nx, ny);
                    }
                }

                _waypoint[idx] = bestPoint;
            }
        }
    }

    public bool TryWaypoint(Vec2 worldPos, out Vec2 waypoint)
    {
        int x = Grid.CellOf(worldPos.X);
        int y = Grid.CellOf(worldPos.Y);
        if (!_grid.InBounds(x, y))
        {
            waypoint = worldPos;
            return false;
        }

        int idx = y * _grid.Width + x;
        if (_settledStamp[idx] != _stamp)
        {
            waypoint = worldPos;
            return false;
        }

        waypoint = _waypoint[idx];
        return true;
    }

    private void HeapPush(int node, double key)
    {
        if (_heapCount >= _heapNode.Length)
        {
            Array.Resize(ref _heapNode, _heapNode.Length * 2);
            Array.Resize(ref _heapKey, _heapKey.Length * 2);
        }

        int i = _heapCount++;
        _heapNode[i] = node;
        _heapKey[i] = key;
        while (i > 0)
        {
            int parent = (i - 1) >> 1;
            if (!Less(i, parent))
            {
                break;
            }

            Swap(i, parent);
            i = parent;
        }
    }

    private int HeapPop()
    {
        int result = _heapNode[0];
        _heapCount--;
        if (_heapCount > 0)
        {
            _heapNode[0] = _heapNode[_heapCount];
            _heapKey[0] = _heapKey[_heapCount];
            int i = 0;
            while (true)
            {
                int l = (i << 1) + 1;
                int r = l + 1;
                int smallest = i;
                if (l < _heapCount && Less(l, smallest))
                {
                    smallest = l;
                }

                if (r < _heapCount && Less(r, smallest))
                {
                    smallest = r;
                }

                if (smallest == i)
                {
                    break;
                }

                Swap(i, smallest);
                i = smallest;
            }
        }

        return result;
    }

    private bool Less(int a, int b) =>
        _heapKey[a] < _heapKey[b] || (_heapKey[a] == _heapKey[b] && _heapNode[a] < _heapNode[b]);

    private void Swap(int a, int b)
    {
        (_heapNode[a], _heapNode[b]) = (_heapNode[b], _heapNode[a]);
        (_heapKey[a], _heapKey[b]) = (_heapKey[b], _heapKey[a]);
    }
}
}
