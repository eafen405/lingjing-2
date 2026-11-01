using System;
using Lingjing.Core;
using Lingjing.Nav;

namespace Lingjing.Level
{
    /// <summary>把关卡数据变成可走的场地：边界墙、障碍矩形、玩家出生点。</summary>
    public static class LevelField
    {
        /// <summary>缺省沿用设计稿 §6 的 96×54；数据里没写时才用。</summary>
        public const int DefaultWidth = 96;
        public const int DefaultHeight = 54;
        public const int DefaultWallThickness = 1;

        public const double PlayerRadius = 0.4;

        public static int Width(LevelDef level) => level != null && level.mapWidth > 0 ? level.mapWidth : DefaultWidth;

        public static int Height(LevelDef level) => level != null && level.mapHeight > 0 ? level.mapHeight : DefaultHeight;

        public static Grid Build(LevelDef level)
        {
            var grid = new Grid(Width(level), Height(level));

            int thickness = level != null && level.wallThickness > 0 ? level.wallThickness : DefaultWallThickness;
            thickness = Math.Min(thickness, Math.Min(grid.Width, grid.Height) / 2);

            grid.FillRect(0, 0, grid.Width, thickness);
            grid.FillRect(0, grid.Height - thickness, grid.Width, thickness);
            grid.FillRect(0, 0, thickness, grid.Height);
            grid.FillRect(grid.Width - thickness, 0, thickness, grid.Height);

            RectDef[] obstacles = level?.obstacles;
            if (obstacles != null)
            {
                for (int i = 0; i < obstacles.Length; i++)
                {
                    FillObstacle(grid, obstacles[i]);
                }
            }

            return grid;
        }

        /// <summary>障碍越界就裁到场内，不抛异常：手写数据错一格不该让整局起不来。</summary>
        static void FillObstacle(Grid grid, RectDef rect)
        {
            if (rect == null || rect.w <= 0 || rect.h <= 0)
            {
                return;
            }

            int x0 = Math.Max(0, Math.Min(rect.x, grid.Width));
            int y0 = Math.Max(0, Math.Min(rect.y, grid.Height));
            int x1 = Math.Min(grid.Width, rect.x + rect.w);
            int y1 = Math.Min(grid.Height, rect.y + rect.h);
            if (x1 <= x0 || y1 <= y0)
            {
                return;
            }

            grid.FillRect(x0, y0, x1 - x0, y1 - y0);
        }

        /// <summary>
        /// 出生点：场地中心最近的可走格中心，再推出墙外。
        /// 不能直接 ResolveCircle(中心)：万一障碍正好压在中心上，圆心落在墙格内部时
        /// 挤出方向是退化的，玩家会被卡在墙里动不了。
        /// </summary>
        public static Vec2 PlayerStart(Grid grid)
        {
            int cx = grid.Width / 2;
            int cy = grid.Height / 2;
            if (grid.TryFindNearestWalkable(cx, cy, 32, out int x, out int y))
            {
                return grid.ResolveCircle(Grid.CellCenter(x, y), PlayerRadius);
            }

            return grid.ResolveCircle(new Vec2(grid.Width * 0.5, grid.Height * 0.5), PlayerRadius);
        }
    }
}
