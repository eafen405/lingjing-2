using Lingjing.Core;
using System;

namespace Lingjing.Nav
{

/// <summary>
/// 40×24 的可走图。格 (x,y) 覆盖世界坐标 [x,x+1)×[y,y+1)。
/// 圆 vs 网格墙的碰撞求解也在这里——不用任何物理引擎。
/// </summary>
public sealed class Grid
{
    private const double Epsilon = 1e-9;

    private readonly bool[] _walkable;

    public Grid(int width, int height)
    {
        if (width <= 0 || height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "地图尺寸必须为正");
        }

        Width = width;
        Height = height;
        _walkable = new bool[width * height];
        Array.Fill(_walkable, true);
    }

    public int Width { get; }
    public int Height { get; }
    public int CellCount => Width * Height;

    public void SetWall(int x, int y)
    {
        if (!InBounds(x, y))
        {
            throw new ArgumentOutOfRangeException(nameof(x), $"墙格 ({x},{y}) 超出地图 {Width}x{Height}");
        }

        _walkable[y * Width + x] = false;
    }

    public void FillRect(int x, int y, int w, int h)
    {
        for (int j = y; j < y + h; j++)
        {
            for (int i = x; i < x + w; i++)
            {
                SetWall(i, j);
            }
        }
    }

    public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;

    public bool IsWalkableCell(int x, int y) => InBounds(x, y) && _walkable[y * Width + x];

    public static int CellOf(double v) => (int)Math.Floor(v);

    public bool IsWalkableWorld(Vec2 p) => IsWalkableCell(CellOf(p.X), CellOf(p.Y));

    public static Vec2 CellCenter(int x, int y) => new(x + 0.5, y + 0.5);

    /// <summary>圆是否完全落在可走格内（用于候选点过滤与视线拉直）。</summary>
    public bool CircleFits(Vec2 p, double radius)
    {
        int x0 = CellOf(p.X - radius + Epsilon);
        int x1 = CellOf(p.X + radius - Epsilon);
        int y0 = CellOf(p.Y - radius + Epsilon);
        int y1 = CellOf(p.Y + radius - Epsilon);
        for (int y = y0; y <= y1; y++)
        {
            for (int x = x0; x <= x1; x++)
            {
                if (!IsWalkableCell(x, y))
                {
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>
    /// 把半径为 radius 的圆推出所有重叠的墙格。分多次迭代，因此沿墙移动自然得到"贴墙滑行"。
    /// </summary>
    public Vec2 ResolveCircle(Vec2 p, double radius)
    {
        double x = p.X;
        double y = p.Y;

        for (int iter = 0; iter < 4; iter++)
        {
            bool touched = false;
            int x0 = CellOf(x - radius);
            int x1 = CellOf(x + radius);
            int y0 = CellOf(y - radius);
            int y1 = CellOf(y + radius);

            for (int cy = y0; cy <= y1; cy++)
            {
                for (int cx = x0; cx <= x1; cx++)
                {
                    if (IsWalkableCell(cx, cy))
                    {
                        continue;
                    }

                    double qx = SimMath.Clamp(x, cx, cx + 1);
                    double qy = SimMath.Clamp(y, cy, cy + 1);
                    double dx = x - qx;
                    double dy = y - qy;
                    double d2 = dx * dx + dy * dy;
                    if (d2 >= radius * radius)
                    {
                        continue;
                    }

                    if (d2 > 1e-12)
                    {
                        double d = Math.Sqrt(d2);
                        double push = radius - d;
                        x += dx / d * push;
                        y += dy / d * push;
                    }
                    else
                    {
                        // 圆心已落在墙格内部：沿最浅的一条边挤出。
                        double toLeft = x - cx;
                        double toRight = cx + 1 - x;
                        double toBottom = y - cy;
                        double toTop = cy + 1 - y;
                        double m = Math.Min(Math.Min(toLeft, toRight), Math.Min(toBottom, toTop));
                        if (m == toLeft)
                        {
                            x = cx - radius;
                        }
                        else if (m == toRight)
                        {
                            x = cx + 1 + radius;
                        }
                        else if (m == toBottom)
                        {
                            y = cy - radius;
                        }
                        else
                        {
                            y = cy + 1 + radius;
                        }
                    }

                    touched = true;
                }
            }

            if (!touched)
            {
                break;
            }
        }

        x = SimMath.Clamp(x, radius, Width - radius);
        y = SimMath.Clamp(y, radius, Height - radius);
        return new Vec2(x, y);
    }

    /// <summary>沿线段采样，判断半径 clearance 的圆能否整段通过。采样步长 0.1 格。</summary>
    public bool LineOfSight(Vec2 a, Vec2 b, double clearance = 0.15)
    {
        double dx = b.X - a.X;
        double dy = b.Y - a.Y;
        double len = Math.Sqrt(dx * dx + dy * dy);
        if (len < 1e-9)
        {
            return CircleFits(a, clearance);
        }

        int steps = (int)Math.Ceiling(len / 0.1);
        if (steps < 1)
        {
            steps = 1;
        }

        for (int i = 0; i <= steps; i++)
        {
            double t = i / (double)steps;
            var q = new Vec2(a.X + dx * t, a.Y + dy * t);
            if (!CircleFits(q, clearance))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>以 (x,y) 为中心向外做方环搜索，找最近的可走格。确定性：同半径按扫描顺序。</summary>
    public bool TryFindNearestWalkable(int x, int y, int maxRadius, out int foundX, out int foundY)
    {
        if (IsWalkableCell(x, y))
        {
            foundX = x;
            foundY = y;
            return true;
        }

        for (int r = 1; r <= maxRadius; r++)
        {
            for (int dy = -r; dy <= r; dy++)
            {
                for (int dx = -r; dx <= r; dx++)
                {
                    if (Math.Abs(dx) != r && Math.Abs(dy) != r)
                    {
                        continue;
                    }

                    int nx = x + dx;
                    int ny = y + dy;
                    if (IsWalkableCell(nx, ny))
                    {
                        foundX = nx;
                        foundY = ny;
                        return true;
                    }
                }
            }
        }

        foundX = x;
        foundY = y;
        return false;
    }
}
}
