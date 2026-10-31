using Lingjing.Core;
using System;
using System.Collections.Generic;

namespace Lingjing.Nav
{

/// <summary>
/// 定点寻路：8 邻接、对角代价 √2、禁止贴角穿越、视线拉直。
/// 每次更新全局检查（stamp 数组）而非清空数组，保持确定性且便宜。
/// </summary>
public sealed class AStar
{
    private static readonly double Sqrt2 = Math.Sqrt(2.0);
    private static readonly int[] NeighborX = { 1, 1, 0, -1, -1, -1, 0, 1 };
    private static readonly int[] NeighborY = { 0, 1, 1, 1, 0, -1, -1, -1 };

    private readonly Grid _grid;
    private readonly double[] _gScore;
    private readonly int[] _parent;
    private readonly int[] _seenStamp;
    private readonly int[] _closedStamp;
    private int[] _heapNode;
    private double[] _heapKey;
    private int _heapCount;
    private int _stamp;

    public AStar(Grid grid)
    {
        _grid = grid ?? throw new ArgumentNullException(nameof(grid));
        int n = grid.CellCount;
        _gScore = new double[n];
        _parent = new int[n];
        _seenStamp = new int[n];
        _closedStamp = new int[n];
        _heapNode = new int[n * 4 + 64];
        _heapKey = new double[n * 4 + 64];
    }

    /// <summary>返回从 start 到 goal 的路径点（不含起点；终点为真实 goal，不是格心）。不可达返回 null。</summary>
    public List<Vec2>? FindPath(Vec2 start, Vec2 goal, int maxExpansions = 6000)
    {
        _stamp++;
        int sx = Grid.CellOf(start.X);
        int sy = Grid.CellOf(start.Y);
        int gx = Grid.CellOf(goal.X);
        int gy = Grid.CellOf(goal.Y);

        if (!_grid.IsWalkableCell(gx, gy) && !_grid.TryFindNearestWalkable(gx, gy, 6, out gx, out gy))
        {
            return null;
        }

        if (!_grid.IsWalkableCell(sx, sy) && !_grid.TryFindNearestWalkable(sx, sy, 6, out sx, out sy))
        {
            return null;
        }

        int startIdx = sy * _grid.Width + sx;
        int goalIdx = gy * _grid.Width + gx;
        if (startIdx == goalIdx)
        {
            return new List<Vec2> { goal };
        }

        _heapCount = 0;
        _gScore[startIdx] = 0.0;
        _parent[startIdx] = -1;
        _seenStamp[startIdx] = _stamp;
        HeapPush(startIdx, Octile(sx, sy, gx, gy));

        int expansions = 0;
        bool found = false;

        while (_heapCount > 0)
        {
            int node = HeapPop();
            if (_closedStamp[node] == _stamp)
            {
                continue;
            }

            _closedStamp[node] = _stamp;

            if (node == goalIdx)
            {
                found = true;
                break;
            }

            if (++expansions > maxExpansions)
            {
                break;
            }

            int cx = node % _grid.Width;
            int cy = node / _grid.Width;
            double baseG = _gScore[node];

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
                    // 禁止贴角穿越：两个正交邻格都必须可走。
                    continue;
                }

                int ni = ny * _grid.Width + nx;
                if (_closedStamp[ni] == _stamp)
                {
                    continue;
                }

                double tentative = baseG + (diagonal ? Sqrt2 : 1.0);
                if (_seenStamp[ni] == _stamp && tentative >= _gScore[ni])
                {
                    continue;
                }

                _seenStamp[ni] = _stamp;
                _gScore[ni] = tentative;
                _parent[ni] = node;
                HeapPush(ni, tentative + Octile(nx, ny, gx, gy));
            }
        }

        if (!found)
        {
            return null;
        }

        var cells = new List<Vec2>(64);
        int cursor = goalIdx;
        while (cursor != -1)
        {
            cells.Add(Grid.CellCenter(cursor % _grid.Width, cursor / _grid.Width));
            cursor = _parent[cursor];
        }

        cells.Reverse();
        return Smooth(start, goal, cells);
    }

    private List<Vec2> Smooth(Vec2 start, Vec2 goal, List<Vec2> cells)
    {
        var result = new List<Vec2>(cells.Count);
        Vec2 anchor = start;
        int index = 0;

        while (index < cells.Count)
        {
            int farthest = index;
            int limit = Math.Min(cells.Count - 1, index + 24);
            for (int j = limit; j > index; j--)
            {
                if (_grid.LineOfSight(anchor, cells[j], 0.3))
                {
                    farthest = j;
                    break;
                }
            }

            anchor = cells[farthest];
            result.Add(anchor);
            index = farthest + 1;
        }

        if (result.Count == 0 && _grid.LineOfSight(start, goal, 0.3))
        {
            result.Add(goal);
        }
        else if (_grid.LineOfSight(anchor, goal, 0.3))
        {
            result[result.Count - 1] = goal;
        }

        return result;
    }

    private static double Octile(int ax, int ay, int bx, int by)
    {
        int dx = Math.Abs(ax - bx);
        int dy = Math.Abs(ay - by);
        int min = Math.Min(dx, dy);
        int max = Math.Max(dx, dy);
        return (max - min) + Sqrt2 * min;
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

    // 平局按节点下标决胜，保证同一输入得到同一条路径。
    private bool Less(int a, int b) =>
        _heapKey[a] < _heapKey[b] || (_heapKey[a] == _heapKey[b] && _heapNode[a] < _heapNode[b]);

    private void Swap(int a, int b)
    {
        (_heapNode[a], _heapNode[b]) = (_heapNode[b], _heapNode[a]);
        (_heapKey[a], _heapKey[b]) = (_heapKey[b], _heapKey[a]);
    }
}
}
