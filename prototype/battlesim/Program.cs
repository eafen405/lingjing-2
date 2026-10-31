using System.Globalization;
using System.Text;
using BattleSim.Fairy;
using BattleSim.Metrics;
using BattleSim.Scenarios;
using BattleSim.Sim;

namespace BattleSim;

internal static class Program
{
    private static int Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;

        CliOptions options;
        try
        {
            options = CliOptions.Parse(args);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"参数错误：{ex.Message}");
            PrintUsage();
            return 2;
        }

        if (options.Help)
        {
            PrintUsage();
            return 0;
        }

        string projectDir = ProjectPaths.FindProjectDir();
        string resultsDir = options.OutDir ?? Path.Combine(projectDir, "results");

        List<string> scenarioPaths;
        try
        {
            scenarioPaths = ResolveScenarioPaths(options, projectDir);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 2;
        }

        try
        {
            return options.SelfTest
                ? RunSelfTest(scenarioPaths, options, resultsDir)
                : RunBatch(scenarioPaths, options, resultsDir);
        }
        catch (ScenarioException ex)
        {
            Console.Error.WriteLine($"场景错误：{ex.Message}");
            return 2;
        }
    }

    private static int RunBatch(List<string> scenarioPaths, CliOptions options, string resultsDir)
    {
        foreach (string path in scenarioPaths)
        {
            Scenario scenario = ScenarioLoader.Load(path);
            ulong seed = options.Seed ?? scenario.Seed;
            PolicyKind policy = options.Policy ?? scenario.Policy;

            SimResult result = Execute(scenario, seed, policy, options.Ticks, options.TraceEvery, out BattleSimulation sim);
            result.TickLogPath = Save(result, sim, resultsDir, writeFiles: !options.NoWrite);
            PrintSummary(result);
        }

        return 0;
    }

    private static int RunSelfTest(List<string> scenarioPaths, CliOptions options, string resultsDir)
    {
        bool allPassed = true;
        Console.WriteLine("SELFTEST: 每个场景同种子跑两遍，逐 tick 哈希必须完全一致。");

        foreach (string path in scenarioPaths)
        {
            Scenario scenario = ScenarioLoader.Load(path);
            ulong seed = options.Seed ?? scenario.Seed;
            PolicyKind policy = options.Policy ?? scenario.Policy;

            Console.WriteLine();
            Console.WriteLine($"[selftest] {scenario.Name} seed={seed} policy={policy.ToString().ToLowerInvariant()}");

            SimResult first = Execute(scenario, seed, policy, options.Ticks, options.TraceEvery, out BattleSimulation simFirst);
            SimResult second = Execute(scenario, seed, policy, options.Ticks, 0, out _);

            int compareLength = Math.Min(first.TickHashes.Length, second.TickHashes.Length);
            int firstDiff = -1;
            for (int i = 0; i < compareLength; i++)
            {
                if (first.TickHashes[i] != second.TickHashes[i])
                {
                    firstDiff = i;
                    break;
                }
            }

            bool ticksEqual = first.TickHashes.Length == second.TickHashes.Length && firstDiff < 0;
            bool metricsEqual =
                first.Survived == second.Survived &&
                first.Hp.Equals(second.Hp) &&
                first.Hits == second.Hits &&
                first.Kills == second.Kills &&
                Math.Abs(first.DamageTaken - second.DamageTaken) < 1e-9 &&
                Math.Abs(first.AvgEnemiesOnRing - second.AvgEnemiesOnRing) < 1e-9 &&
                first.FairyTakeovers == second.FairyTakeovers;
            bool passed = ticksEqual && metricsEqual;

            Console.WriteLine($"  run1  hash={ResultWriter.Hex(first.HashChainTail)}  hits={first.Hits} kills={first.Kills} hp={first.Hp:0.###} ring={first.AvgEnemiesOnRing:0.####}");
            Console.WriteLine($"  run2  hash={ResultWriter.Hex(second.HashChainTail)}  hits={second.Hits} kills={second.Kills} hp={second.Hp:0.###} ring={second.AvgEnemiesOnRing:0.####}");
            Console.WriteLine($"  ticks compared = {compareLength}/{first.TickHashes.Length}");
            if (!passed)
            {
                Console.WriteLine(firstDiff >= 0
                    ? $"  RESULT: FAIL（首个不一致 tick = {firstDiff}）"
                    : "  RESULT: FAIL（指标不一致）");
            }
            else
            {
                Console.WriteLine("  RESULT: PASS");
            }

            allPassed &= passed;
            first.TickLogPath = Save(first, simFirst, resultsDir, writeFiles: !options.NoWrite);
        }

        Console.WriteLine();
        Console.WriteLine(allPassed ? "SELFTEST: PASS" : "SELFTEST: FAIL");
        return allPassed ? 0 : 1;
    }

    private static SimResult Execute(Scenario scenario, ulong seed, PolicyKind policy, int? ticks, int traceEvery, out BattleSimulation sim)
    {
        IPlanner planner = new BaselinePlanner();
        BattleSimulation simulation = new BattleSimulation(scenario, seed, policy, ticks, planner);
        if (traceEvery > 0)
        {
            simulation.Trace = (tick, world) =>
            {
                if (tick % traceEvery == 0)
                {
                    PrintTrace(tick, world, simulation.IsFairyActive);
                }
            };
        }

        sim = simulation;
        return simulation.Run();
    }

    /// <summary>调试叠层的雏形：每个采样 tick 打印世界状态。默认关闭。</summary>
    private static void PrintTrace(int tick, WorldState w, bool fairyActive)
    {
        var sb = new StringBuilder();
        sb.Append("t=").Append(tick.ToString("D5", CultureInfo.InvariantCulture));
        sb.Append(" p=(").Append(w.Player.Pos.X.ToString("0.00", CultureInfo.InvariantCulture)).Append(',').Append(w.Player.Pos.Y.ToString("0.00", CultureInfo.InvariantCulture)).Append(')');
        sb.Append(" hp=").Append(w.Player.Hp.ToString("0.0", CultureInfo.InvariantCulture));
        sb.Append(" en=").Append(w.Player.Energy.ToString("0.0", CultureInfo.InvariantCulture));
        sb.Append(" w=").Append(w.Player.SpinSign > 0 ? "+" : "-");
        sb.Append(" f=").Append(fairyActive ? "1" : "0");
        sb.Append(" |");
        for (int i = 0; i < w.Enemies.Count; i++)
        {
            Enemies.Enemy e = w.Enemies[i];
            sb.Append(' ').Append(e.Id).Append(':').Append(e.Kind.ToString()[0]).Append('/').Append(e.StateName);
            sb.Append('@').Append(e.Pos.X.ToString("0.0", CultureInfo.InvariantCulture)).Append(',').Append(e.Pos.Y.ToString("0.0", CultureInfo.InvariantCulture));
            sb.Append('#').Append(e.Hp.ToString("0", CultureInfo.InvariantCulture));
        }

        if (w.Projectiles.Count > 0)
        {
            sb.Append(" | shots=").Append(w.Projectiles.Count);
        }

        if (w.Zones.Count > 0)
        {
            sb.Append(" | zones=").Append(w.Zones.Count);
        }

        Console.WriteLine(sb.ToString());
    }

    /// <summary>写 results/*.json 与 results/*.ticks.csv，返回 csv 的相对路径。</summary>
    private static string Save(SimResult result, BattleSimulation sim, string resultsDir, bool writeFiles)
    {
        string baseName = $"{result.Scenario}-{result.Seed.ToString(CultureInfo.InvariantCulture)}";
        string jsonPath = Path.Combine(resultsDir, baseName + ".json");
        string csvPath = Path.Combine(resultsDir, baseName + ".ticks.csv");

        if (!writeFiles)
        {
            return "";
        }

        ResultWriter.WriteTickLog(csvPath, sim.TickLog, result.Seed);
        result.TickLogPath = Path.GetFileName(csvPath);
        ResultWriter.WriteJson(jsonPath, result);
        return result.TickLogPath;
    }

    private static void PrintSummary(SimResult r)
    {
        Console.WriteLine($"== {r.Scenario}  seed={r.Seed}  policy={r.Policy}  script={r.PlayerScript} ==");
        Console.WriteLine($"   ticks={r.Ticks}  survived={r.Survived}  hp={r.Hp:0.###}  hpLost={r.HpLost:0.###}");
        Console.WriteLine($"   hits={r.Hits}  kills={r.Kills}  damageDealt={r.DamageDealt:0.###}  damageTaken={r.DamageTaken:0.###}");
        Console.WriteLine($"   avgEnemiesOnRing={r.AvgEnemiesOnRing:0.####}  avgDecisionMs={r.AvgDecisionMs:0.#####}  maxDecisionMs={r.MaxDecisionMs:0.#####}  decisions={r.DecisionCount}");
        Console.WriteLine($"   fairyTakeovers={r.FairyTakeovers}  fairyTicks={r.FairyTicks}  enemiesAlive={r.EnemiesAlive}/{r.EnemiesSpawned}");
        Console.WriteLine($"   simElapsedMs={r.SimElapsedMs:0.###}  (per1000ticks≈{r.SimElapsedMs / Math.Max(1, r.Ticks) * 1000.0:0.###}ms)");
        Console.WriteLine($"   hashChainTail={ResultWriter.Hex(r.HashChainTail)}  tickLog={r.TickLogPath}");
    }

    private static List<string> ResolveScenarioPaths(CliOptions options, string projectDir)
    {
        if (options.ScenarioPaths.Count == 0)
        {
            string dir = Path.Combine(projectDir, "scenarios");
            if (!Directory.Exists(dir))
            {
                throw new FileNotFoundException($"找不到场景目录：{dir}");
            }

            string[] files = Directory.GetFiles(dir, "*.json");
            Array.Sort(files, StringComparer.Ordinal);
            if (files.Length == 0)
            {
                throw new FileNotFoundException($"场景目录为空：{dir}");
            }

            return new List<string>(files);
        }

        var resolved = new List<string>(options.ScenarioPaths.Count);
        foreach (string raw in options.ScenarioPaths)
        {
            string path = raw;
            if (!Path.IsPathRooted(path) && !File.Exists(path))
            {
                string candidate = Path.Combine(projectDir, raw);
                if (File.Exists(candidate))
                {
                    path = candidate;
                }
            }

            if (!File.Exists(path))
            {
                throw new FileNotFoundException($"场景文件不存在：{raw}");
            }

            resolved.Add(Path.GetFullPath(path));
        }

        return resolved;
    }

    private static void PrintUsage()
    {
        Console.WriteLine("""
battlesim —— 纯 C# 无头战斗模拟器（可复刻原型）

用法：
  battlesim [选项]

选项：
  --scenario <file>   场景 JSON，可重复；缺省时跑 scenarios/ 下全部
  --seed <n>          覆盖场景文件里的 seed
  --policy <p>        scripted | fairy，覆盖场景文件里的 policy
  --ticks <n>         覆盖场景时长（只用于性能/冒烟）
  --out <dir>         结果目录，默认 <project>/results
  --selftest          同场景同种子跑两遍，逐 tick 哈希比对
  --trace <n>         每 n tick 打印一行世界状态（调试用）
  --no-write          只打印指标，不落盘
  -h, --help          显示本帮助
""");
    }
}

internal sealed class CliOptions
{
    public readonly List<string> ScenarioPaths = new();
    public ulong? Seed;
    public PolicyKind? Policy;
    public int? Ticks;
    public string? OutDir;
    public bool SelfTest;
    public bool NoWrite;
    public bool Help;
    public int TraceEvery;

    public static CliOptions Parse(string[] args)
    {
        var options = new CliOptions();
        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];
            switch (arg)
            {
                case "--scenario":
                case "-s":
                    options.ScenarioPaths.Add(RequireValue(args, ref i));
                    break;
                case "--seed":
                    options.Seed = ParseSeed(RequireValue(args, ref i));
                    break;
                case "--policy":
                    options.Policy = RequireValue(args, ref i).ToLowerInvariant() switch
                    {
                        "scripted" => PolicyKind.Scripted,
                        "fairy" => PolicyKind.Fairy,
                        var v => throw new ArgumentException($"--policy 只接受 scripted / fairy，收到 {v}")
                    };
                    break;
                case "--ticks":
                    options.Ticks = int.Parse(RequireValue(args, ref i), CultureInfo.InvariantCulture);
                    break;
                case "--out":
                    options.OutDir = RequireValue(args, ref i);
                    break;
                case "--selftest":
                    options.SelfTest = true;
                    break;
                case "--no-write":
                    options.NoWrite = true;
                    break;
                case "--trace":
                    options.TraceEvery = int.Parse(RequireValue(args, ref i), CultureInfo.InvariantCulture);
                    break;
                case "-h":
                case "--help":
                    options.Help = true;
                    break;
                default:
                    throw new ArgumentException($"未知参数 {arg}");
            }
        }

        return options;
    }

    private static string RequireValue(string[] args, ref int index)
    {
        if (index + 1 >= args.Length)
        {
            throw new ArgumentException($"参数 {args[index]} 缺少值");
        }

        index++;
        return args[index];
    }

    private static ulong ParseSeed(string value) =>
        ulong.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out ulong seed)
            ? seed
            : throw new ArgumentException($"seed 必须是无符号整数，收到 {value}");
}

internal static class ProjectPaths
{
    /// <summary>从 exe 目录向上找 BattleSim.csproj，使结果目录不依赖当前工作目录（也不会写穿到 prototype/ 之外）。</summary>
    public static string FindProjectDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "BattleSim.csproj")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        return Directory.GetCurrentDirectory();
    }
}
