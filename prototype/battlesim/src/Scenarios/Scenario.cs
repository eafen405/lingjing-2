using System.Text.Json;
using BattleSim.Core;
using BattleSim.Enemies;
using BattleSim.Nav;

namespace BattleSim.Scenarios;

public enum PolicyKind
{
    Scripted,
    Fairy
}

public enum PlayerScriptKind
{
    Flee,
    Orbit
}

public sealed class ScenarioSpawn
{
    public int Tick;
    public EnemyKind Kind;
    public Vec2 Pos;
    public int Order;
}

/// <summary>场景定义。JSON 是唯一的场景权威，代码里不写死任何关卡数据。</summary>
public sealed class Scenario
{
    public string Name = "";
    public string SourcePath = "";
    public ulong Seed;
    public PolicyKind Policy;
    public PlayerScriptKind PlayerScript;
    public double InitialCharge;
    public int DurationTicks;
    public Vec2 PlayerStart;
    public Grid Grid = null!;
    public readonly List<ScenarioSpawn> Spawns = new();
}

public sealed class ScenarioException : Exception
{
    public ScenarioException(string message) : base(message)
    {
    }
}

/// <summary>
/// 场景 JSON 解析。字段见 README；缺省值刻意很少——场景应显式写全，避免"跑起来才发现是默认值"。
/// </summary>
public static class ScenarioLoader
{
    public static Scenario Load(string path)
    {
        if (!File.Exists(path))
        {
            throw new ScenarioException($"场景文件不存在：{path}");
        }

        string text = File.ReadAllText(path);
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(text, new JsonDocumentOptions
            {
                CommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            });
        }
        catch (JsonException ex)
        {
            throw new ScenarioException($"场景 JSON 解析失败（{path}）：{ex.Message}");
        }

        using (doc)
        {
            JsonElement root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                throw new ScenarioException("场景根节点必须是对象");
            }

            var scenario = new Scenario
            {
                SourcePath = path,
                Name = RequireString(root, "name"),
                Seed = RequireSeed(root),
                Policy = ParsePolicy(OptionalString(root, "policy") ?? "scripted"),
                PlayerScript = ParsePlayerScript(OptionalString(root, "script") ?? "flee"),
                DurationTicks = ReadDuration(root)
            };

            JsonElement map = RequireObject(root, "map");
            int width = OptionalInt(map, "width") ?? 40;
            int height = OptionalInt(map, "height") ?? 24;
            var grid = new Grid(width, height);
            ApplyWalls(grid, map);
            scenario.Grid = grid;

            JsonElement player = RequireObject(root, "player");
            scenario.PlayerStart = RequireVec2(player, "pos");
            scenario.PlayerScript = ParsePlayerScript(OptionalString(player, "script") ?? scenario.PlayerScript.ToString());
            scenario.InitialCharge = OptionalDouble(player, "initialCharge") ?? 0.0;
            if (scenario.InitialCharge < 0.0 || scenario.InitialCharge > 100.0)
            {
                throw new ScenarioException("player.initialCharge 必须落在 [0,100]");
            }

            ReadSpawns(root, scenario);
            Validate(scenario);
            return scenario;
        }
    }

    private static void ApplyWalls(Grid grid, JsonElement map)
    {
        if (map.TryGetProperty("walls", out JsonElement walls))
        {
            if (walls.ValueKind != JsonValueKind.Array)
            {
                throw new ScenarioException("map.walls 必须是 [x,y] 数组的数组");
            }

            int index = 0;
            foreach (JsonElement cell in walls.EnumerateArray())
            {
                if (cell.ValueKind != JsonValueKind.Array || cell.GetArrayLength() != 2)
                {
                    throw new ScenarioException($"map.walls[{index}] 必须是 [x,y]");
                }

                grid.SetWall(cell[0].GetInt32(), cell[1].GetInt32());
                index++;
            }
        }

        if (map.TryGetProperty("wallRects", out JsonElement rects))
        {
            if (rects.ValueKind != JsonValueKind.Array)
            {
                throw new ScenarioException("map.wallRects 必须是 [x,y,w,h] 数组的数组");
            }

            int index = 0;
            foreach (JsonElement rect in rects.EnumerateArray())
            {
                if (rect.ValueKind != JsonValueKind.Array || rect.GetArrayLength() != 4)
                {
                    throw new ScenarioException($"map.wallRects[{index}] 必须是 [x,y,w,h]");
                }

                grid.FillRect(rect[0].GetInt32(), rect[1].GetInt32(), rect[2].GetInt32(), rect[3].GetInt32());
                index++;
            }
        }
    }

    private static void ReadSpawns(JsonElement root, Scenario scenario)
    {
        JsonElement spawns = RequireArray(root, "spawns");
        int order = 0;
        foreach (JsonElement item in spawns.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                throw new ScenarioException($"spawns[{order}] 必须是对象");
            }

            var spawn = new ScenarioSpawn
            {
                Tick = OptionalInt(item, "tick") ?? 0,
                Kind = ParseEnemyKind(RequireString(item, "type")),
                Pos = RequireVec2(item, "pos"),
                Order = order
            };

            if (spawn.Tick < 0)
            {
                throw new ScenarioException($"spawns[{order}].tick 不能为负");
            }

            scenario.Spawns.Add(spawn);
            order++;
        }

        // 稳定排序：同一 tick 的刷新按文件里的顺序。
        scenario.Spawns.Sort((a, b) => a.Tick != b.Tick ? a.Tick.CompareTo(b.Tick) : a.Order.CompareTo(b.Order));
    }

    private static void Validate(Scenario s)
    {
        if (!s.Grid.InBounds(Grid.CellOf(s.PlayerStart.X), Grid.CellOf(s.PlayerStart.Y)) ||
            !s.Grid.CircleFits(s.PlayerStart, Combat.Player.RadiusConst))
        {
            throw new ScenarioException($"player.pos {s.PlayerStart} 不可站（墙内或离墙太近）");
        }

        for (int i = 0; i < s.Spawns.Count; i++)
        {
            ScenarioSpawn spawn = s.Spawns[i];
            bool inBounds = spawn.Pos.X >= 0 && spawn.Pos.Y >= 0 && spawn.Pos.X <= s.Grid.Width && spawn.Pos.Y <= s.Grid.Height;
            if (!inBounds)
            {
                throw new ScenarioException($"spawns[{i}].pos {spawn.Pos} 超出地图 {s.Grid.Width}x{s.Grid.Height}");
            }

            if (spawn.Kind != EnemyKind.Dropper && !s.Grid.CircleFits(spawn.Pos, Combat.DamageRules.EnemyRadius))
            {
                throw new ScenarioException($"spawns[{i}].pos {spawn.Pos} 不可站（墙内或离墙太近），类型 {spawn.Kind}");
            }
        }

        if (s.DurationTicks <= 0)
        {
            throw new ScenarioException("durationTicks 必须为正");
        }
    }

    private static int ReadDuration(JsonElement root)
    {
        int? ticks = OptionalInt(root, "durationTicks");
        if (ticks.HasValue)
        {
            return ticks.Value;
        }

        double? seconds = OptionalDouble(root, "durationSeconds");
        if (seconds.HasValue)
        {
            return SimConstants.FromSeconds(seconds.Value);
        }

        throw new ScenarioException("必须提供 durationTicks 或 durationSeconds");
    }

    private static ulong RequireSeed(JsonElement root)
    {
        if (!root.TryGetProperty("seed", out JsonElement seed) || seed.ValueKind != JsonValueKind.Number)
        {
            throw new ScenarioException("必须提供数字字段 seed");
        }

        return seed.TryGetInt64(out long asLong) ? unchecked((ulong)asLong) : (ulong)seed.GetDouble();
    }

    private static PolicyKind ParsePolicy(string value) => value.ToLowerInvariant() switch
    {
        "scripted" => PolicyKind.Scripted,
        "fairy" => PolicyKind.Fairy,
        _ => throw new ScenarioException($"未知 policy：{value}（可选 scripted / fairy）")
    };

    private static PlayerScriptKind ParsePlayerScript(string value) => value.ToLowerInvariant() switch
    {
        "flee" => PlayerScriptKind.Flee,
        "orbit" => PlayerScriptKind.Orbit,
        _ => throw new ScenarioException($"未知 player.script：{value}（可选 flee / orbit）")
    };

    private static EnemyKind ParseEnemyKind(string value) => value.ToLowerInvariant() switch
    {
        "tail_light" => EnemyKind.TailLight,
        "striker" => EnemyKind.Striker,
        "nail_gun" => EnemyKind.NailGun,
        "dropper" => EnemyKind.Dropper,
        "chain_boss" => EnemyKind.ChainBoss,
        _ => throw new ScenarioException($"未知敌人类型：{value}")
    };

    private static JsonElement RequireObject(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out JsonElement e) || e.ValueKind != JsonValueKind.Object)
        {
            throw new ScenarioException($"缺少对象字段 {name}");
        }

        return e;
    }

    private static JsonElement RequireArray(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out JsonElement e) || e.ValueKind != JsonValueKind.Array)
        {
            throw new ScenarioException($"缺少数组字段 {name}");
        }

        return e;
    }

    private static string RequireString(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out JsonElement e) || e.ValueKind != JsonValueKind.String)
        {
            throw new ScenarioException($"缺少字符串字段 {name}");
        }

        return e.GetString() ?? "";
    }

    private static string? OptionalString(JsonElement root, string name) =>
        root.TryGetProperty(name, out JsonElement e) && e.ValueKind == JsonValueKind.String ? e.GetString() : null;

    private static int? OptionalInt(JsonElement root, string name) =>
        root.TryGetProperty(name, out JsonElement e) && e.ValueKind == JsonValueKind.Number ? e.GetInt32() : null;

    private static double? OptionalDouble(JsonElement root, string name) =>
        root.TryGetProperty(name, out JsonElement e) && e.ValueKind == JsonValueKind.Number ? e.GetDouble() : null;

    private static Vec2 RequireVec2(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out JsonElement e) || e.ValueKind != JsonValueKind.Array || e.GetArrayLength() != 2)
        {
            throw new ScenarioException($"缺少 [x,y] 字段 {name}");
        }

        return new Vec2(e[0].GetDouble(), e[1].GetDouble());
    }
}
