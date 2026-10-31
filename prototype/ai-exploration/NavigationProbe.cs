using System.Diagnostics;
using BattleSim.Core;
using BattleSim.Nav;
using BattleSim.Scenarios;

namespace AiExploration;

/// <summary>Throwaway probe. Reads the existing map and navigation implementations; changes none of them.</summary>
public static class NavigationProbe
{
    private static readonly int[] Dx = { 1, 1, 0, -1, -1, -1, 0, 1 };
    private static readonly int[] Dy = { 0, 1, 1, 1, 0, -1, -1, -1 };
    private static readonly double Sqrt2 = Math.Sqrt(2);

    public static object Run()
    {
        string source = LocateMap();
        Scenario scenario = ScenarioLoader.Load(source);
        Grid grid = scenario.Grid;
        Vec2 origin = scenario.PlayerStart;
        int root = Index(grid, origin);
        Tree reference = Search(grid, root);
        var flow = new FlowField(grid);
        flow.Recompute(origin);
        var discrepancies = new List<object>();
        int reachable = 0, nonShortestSuccessors = 0;
        double worstLocalExcess = 0, worstRouteExcess = 0;
        int worstRouteStart = Enumerable.Range(0, grid.CellCount)
            .Where(i => double.IsFinite(reference.Distance[i]))
            .OrderByDescending(i => reference.Distance[i]).ThenBy(i => i).First();

        for (int node = 0; node < grid.CellCount; node++)
        {
            if (node == root || !double.IsFinite(reference.Distance[node])) continue;
            reachable++;
            Vec2 point = Center(grid, node);
            if (!flow.TryWaypoint(point, out Vec2 waypoint)) continue;
            int next = Index(grid, waypoint);
            double selected = StepCost(grid, node, next) + reference.Distance[next];
            double excess = selected - reference.Distance[node];
            if (excess > 1e-8)
            {
                nonShortestSuccessors++;
                worstLocalExcess = Math.Max(worstLocalExcess, excess);
                if (discrepancies.Count < 8)
                    discrepancies.Add(new { Cell = Pair(point), Next = Pair(waypoint), OptimalCost = reference.Distance[node], ChosenCost = selected, Excess = excess });
            }
            double route = FollowFlow(grid, flow, node, root, null);
            double routeExcess = route - reference.Distance[node];
            if (double.IsFinite(routeExcess) && routeExcess > worstRouteExcess + 1e-8)
            {
                worstRouteExcess = routeExcess;
                worstRouteStart = node;
            }
        }

        var actualRoute = new List<double[]>();
        double actualCost = FollowFlow(grid, flow, worstRouteStart, root, actualRoute);
        List<double[]> optimalRoute = TraceToRoot(grid, reference, worstRouteStart);
        var goals = new List<Vec2>();
        // Nearby but distinct candidate cells, not a claim about a final candidate generator.
        var available = Enumerable.Range(0, grid.CellCount)
            .Where(i => i != root && double.IsFinite(reference.Distance[i]) && reference.Distance[i] <= 8 && reference.Distance[i] >= 2)
            .OrderBy(i => (i * 137 + 29) % 997).ToArray();
        foreach (int node in available.Take(20)) goals.Add(Center(grid, node));
        var astar = new AStar(grid);
        var searchRows = new List<object>();
        foreach (int requested in new[] { 4, 12, 20 })
        {
            Vec2[] subset = goals.Take(requested).ToArray();
            if (subset.Length == 0) continue;
            int astarWaypoints = 0, unreachable = 0, invalidClearancePaths = 0;
            foreach (Vec2 goal in subset)
            {
                List<Vec2>? path = astar.FindPath(origin, goal);
                if (path == null) { unreachable++; continue; }
                astarWaypoints += path.Count;
                Vec2 previous = origin;
                bool valid = true;
                foreach (Vec2 point in path)
                {
                    if (!grid.LineOfSight(previous, point, 0.4)) valid = false;
                    previous = point;
                }
                if (!valid) invalidClearancePaths++;
            }
            Timing astarTiming = Measure(() =>
            {
                int count = 0;
                foreach (Vec2 goal in subset) count += astar.FindPath(origin, goal)?.Count ?? 0;
                return count;
            });
            Timing treeTiming = Measure(() =>
            {
                Tree tree = Search(grid, root);
                int count = 0;
                foreach (Vec2 goal in subset) count += TraceToRoot(grid, tree, Index(grid, goal)).Count;
                return count;
            });
            searchRows.Add(new
            {
                CandidateCount = subset.Length,
                RepeatedAStar = astarTiming,
                OneTreeAndBacktrace = treeTiming,
                SingleTreeExpandedCells = reference.Expanded,
                SingleTreeQueuePushes = reference.Pushes,
                AStarCalls = subset.Length,
                AStarUnreachable = unreachable,
                AStarWaypoints = astarWaypoints,
                PathsRejectedByRadius04SampleCheck = invalidClearancePaths
            });
        }
        var walls = new List<int[]>();
        for (int y = 0; y < grid.Height; y++)
            for (int x = 0; x < grid.Width; x++)
                if (!grid.IsWalkableCell(x, y)) walls.Add(new[] { x, y });
        return new
        {
            Question = "Existing flow successors versus weighted shortest paths; repeated A* versus one shared tree.",
            SourceMap = "prototype/battlesim/scenarios/mix_four.json",
            Scope = "Static undirected eight-neighbour graph; no dynamic avoidance. Timings are local .NET desktop measurements, not Android results.",
            TimingCaveat = "Existing A* includes string pulling and sampled clearance checks. The experimental tree includes allocation and backtraces but no smoothing; runtime numbers are not an equal-output speedup claim. Tree path costs are graph costs, not smoothed physical travel times.",
            ClearanceCaveat = "Radius-0.4 rejection uses the existing conservative sampled LineOfSight; it is a diagnostic count, not proof of physical collision. No original navigation code is changed.",
            Width = grid.Width, Height = grid.Height, Walls = walls, Origin = Pair(origin), CandidateGoals = goals.Select(Pair).ToArray(),
            Flow = new
            {
                ReachableNonRootCells = reachable,
                NonShortestSuccessorCells = nonShortestSuccessors,
                WorstLocalExcess = worstLocalExcess,
                WorstRouteExcess = worstRouteExcess,
                ActualRouteCost = actualCost,
                OptimalRouteCost = reference.Distance[worstRouteStart],
                Start = Pair(Center(grid, worstRouteStart)),
                ActualRoute = actualRoute,
                OptimalRoute = optimalRoute,
                Examples = discrepancies
            },
            Search = searchRows
        };
    }

    private static string LocateMap()
    {
        foreach (string start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
            for (DirectoryInfo? directory = new(start); directory != null; directory = directory.Parent)
            {
                string path = Path.Combine(directory.FullName, "prototype", "battlesim", "scenarios", "mix_four.json");
                if (File.Exists(path)) return path;
            }
        throw new FileNotFoundException("Run from the repository containing prototype/battlesim/scenarios/mix_four.json.");
    }

    private static Tree Search(Grid grid, int root)
    {
        var tree = new Tree(grid.CellCount);
        var queue = new PriorityQueue<int, (double Cost, int Node)>();
        tree.Distance[root] = 0;
        queue.Enqueue(root, (0, root));
        tree.Pushes++;
        while (queue.TryDequeue(out int node, out var key))
        {
            if (tree.Settled[node] || key.Cost > tree.Distance[node] + 1e-10) continue;
            tree.Settled[node] = true;
            tree.Expanded++;
            int x = node % grid.Width, y = node / grid.Width;
            for (int k = 0; k < 8; k++)
            {
                int nx = x + Dx[k], ny = y + Dy[k];
                if (!grid.IsWalkableCell(nx, ny)) continue;
                bool diagonal = Dx[k] != 0 && Dy[k] != 0;
                if (diagonal && (!grid.IsWalkableCell(x + Dx[k], y) || !grid.IsWalkableCell(x, y + Dy[k]))) continue;
                int neighbour = ny * grid.Width + nx;
                double cost = tree.Distance[node] + (diagonal ? Sqrt2 : 1);
                if (tree.Settled[neighbour] || cost >= tree.Distance[neighbour] - 1e-10) continue;
                tree.Distance[neighbour] = cost;
                tree.Parent[neighbour] = node;
                queue.Enqueue(neighbour, (cost, neighbour));
                tree.Pushes++;
            }
        }
        return tree;
    }

    private static double FollowFlow(Grid grid, FlowField flow, int start, int root, List<double[]>? points)
    {
        int cursor = start;
        double cost = 0;
        points?.Add(Pair(Center(grid, cursor)));
        for (int step = 0; step < grid.CellCount; step++)
        {
            if (cursor == root) return cost;
            if (!flow.TryWaypoint(Center(grid, cursor), out Vec2 waypoint)) return double.PositiveInfinity;
            int next = Index(grid, waypoint);
            if (next == cursor) return double.PositiveInfinity;
            cost += StepCost(grid, cursor, next);
            cursor = next;
            points?.Add(Pair(waypoint));
        }
        return double.PositiveInfinity;
    }

    private static List<double[]> TraceToRoot(Grid grid, Tree tree, int start)
    {
        var points = new List<double[]>();
        if (!double.IsFinite(tree.Distance[start])) return points;
        int cursor = start;
        for (int n = 0; n < grid.CellCount && cursor >= 0; n++)
        {
            points.Add(Pair(Center(grid, cursor)));
            cursor = tree.Parent[cursor];
        }
        return points;
    }

    private static Timing Measure(Func<int> action)
    {
        int sink = 0;
        for (int i = 0; i < 5; i++) sink ^= action();
        var milliseconds = new double[31];
        long allocationBefore = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < milliseconds.Length; i++)
        {
            long start = Stopwatch.GetTimestamp();
            sink ^= action();
            milliseconds[i] = (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency;
        }
        long allocated = GC.GetAllocatedBytesForCurrentThread() - allocationBefore;
        Array.Sort(milliseconds);
        GC.KeepAlive(sink);
        return new Timing(milliseconds[15], milliseconds[29], milliseconds[30], allocated / milliseconds.Length, milliseconds.Length);
    }

    private static int Index(Grid grid, Vec2 p) => Grid.CellOf(p.Y) * grid.Width + Grid.CellOf(p.X);
    private static Vec2 Center(Grid grid, int node) => Grid.CellCenter(node % grid.Width, node / grid.Width);
    private static double[] Pair(Vec2 p) => new[] { p.X, p.Y };
    private static double StepCost(Grid grid, int a, int b) => a % grid.Width != b % grid.Width && a / grid.Width != b / grid.Width ? Sqrt2 : 1;

    private sealed class Tree
    {
        public readonly double[] Distance;
        public readonly int[] Parent;
        public readonly bool[] Settled;
        public int Expanded, Pushes;
        public Tree(int size)
        {
            Distance = Enumerable.Repeat(double.PositiveInfinity, size).ToArray();
            Parent = Enumerable.Repeat(-1, size).ToArray();
            Settled = new bool[size];
        }
    }
    private sealed record Timing(double P50Ms, double P95Ms, double P99Ms, long AllocatedBytesPerRun, int Samples);
}
