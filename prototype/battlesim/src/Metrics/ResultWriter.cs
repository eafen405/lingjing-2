using System.Globalization;
using System.Text;
using System.Text.Json;
using BattleSim.Core;

namespace BattleSim.Metrics;

/// <summary>
/// 结果输出：results/&lt;scenario&gt;-&lt;seed&gt;.json（汇总指标 + 哈希链尾）
/// 与 results/&lt;scenario&gt;-&lt;seed&gt;.ticks.csv（逐 tick 的 {tick, seed, playerCmd, fairyCmd, hash}）。
/// </summary>
public static class ResultWriter
{
    public static void WriteJson(string path, SimResult result)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);

        var options = new JsonWriterOptions { Indented = true };
        using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
        using var writer = new Utf8JsonWriter(stream, options);

        writer.WriteStartObject();
        writer.WriteString("scenario", result.Scenario);
        writer.WriteNumber("seed", result.Seed);
        writer.WriteString("policy", result.Policy);
        writer.WriteString("playerScript", result.PlayerScript);
        writer.WriteNumber("ticks", result.Ticks);
        writer.WriteNumber("durationSeconds", Math.Round(SimConstants.ToSeconds(result.Ticks), 3));

        writer.WriteBoolean("survived", result.Survived);
        if (result.DeathTick >= 0)
        {
            writer.WriteNumber("deathTick", result.DeathTick);
        }
        else
        {
            writer.WriteNull("deathTick");
        }

        writer.WriteNumber("hp", Round(result.Hp, 3));
        writer.WriteNumber("hpLost", Round(result.HpLost, 3));
        writer.WriteNumber("hits", result.Hits);
        writer.WriteNumber("kills", result.Kills);
        writer.WriteNumber("damageDealt", Round(result.DamageDealt, 3));
        writer.WriteNumber("damageTaken", Round(result.DamageTaken, 3));
        writer.WriteNumber("avgEnemiesOnRing", Round(result.AvgEnemiesOnRing, 4));
        writer.WriteNumber("avgDecisionMs", Round(result.AvgDecisionMs, 5));
        writer.WriteNumber("maxDecisionMs", Round(result.MaxDecisionMs, 5));
        writer.WriteNumber("decisionCount", result.DecisionCount);
        writer.WriteNumber("fairyTakeovers", result.FairyTakeovers);
        writer.WriteNumber("fairyTicks", result.FairyTicks);
        writer.WriteNumber("enemiesSpawned", result.EnemiesSpawned);
        writer.WriteNumber("enemiesAlive", result.EnemiesAlive);
        writer.WriteNumber("simElapsedMs", Round(result.SimElapsedMs, 3));
        writer.WriteString("hashChainTail", Hex(result.HashChainTail));
        writer.WriteString("tickLog", result.TickLogPath);
        writer.WriteEndObject();
    }

    /// <summary>逐 tick 记录。playerCmd/fairyCmd 用 3 位小数的方向向量表示，便于人眼比对。</summary>
    public static void WriteTickLog(string path, IReadOnlyList<TickLogEntry> entries, ulong seed)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var sb = new StringBuilder(entries.Count * 96 + 128);
        sb.Append("tick,seed,playerCmdX,playerCmdY,playerSpin,playerReverse,fairyCmdX,fairyCmdY,fairySpin,fairyActive,hash\n");
        for (int i = 0; i < entries.Count; i++)
        {
            TickLogEntry e = entries[i];
            sb.Append(e.Tick).Append(',')
              .Append(seed).Append(',')
              .Append(Fmt(e.PlayerMoveX)).Append(',')
              .Append(Fmt(e.PlayerMoveY)).Append(',')
              .Append(e.PlayerSpin).Append(',')
              .Append(e.PlayerReverse ? 1 : 0).Append(',')
              .Append(Fmt(e.FairyMoveX)).Append(',')
              .Append(Fmt(e.FairyMoveY)).Append(',')
              .Append(e.FairySpin).Append(',')
              .Append(e.FairyActive ? 1 : 0).Append(',')
              .Append(Hex(e.Hash)).Append('\n');
        }

        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
    }

    private static string Fmt(double v) => v.ToString("0.000", CultureInfo.InvariantCulture);

    private static double Round(double v, int digits) => Math.Round(v, digits, MidpointRounding.AwayFromZero);

    public static string Hex(ulong value) => value.ToString("x16", CultureInfo.InvariantCulture);
}

/// <summary>一行逐 tick 日志。字段是定值拷贝，不持有世界引用。</summary>
public readonly struct TickLogEntry
{
    public readonly int Tick;
    public readonly double PlayerMoveX;
    public readonly double PlayerMoveY;
    public readonly int PlayerSpin;
    public readonly bool PlayerReverse;
    public readonly double FairyMoveX;
    public readonly double FairyMoveY;
    public readonly int FairySpin;
    public readonly bool FairyActive;
    public readonly ulong Hash;

    public TickLogEntry(int tick, double playerMoveX, double playerMoveY, int playerSpin, bool playerReverse,
        double fairyMoveX, double fairyMoveY, int fairySpin, bool fairyActive, ulong hash)
    {
        Tick = tick;
        PlayerMoveX = playerMoveX;
        PlayerMoveY = playerMoveY;
        PlayerSpin = playerSpin;
        PlayerReverse = playerReverse;
        FairyMoveX = fairyMoveX;
        FairyMoveY = fairyMoveY;
        FairySpin = fairySpin;
        FairyActive = fairyActive;
        Hash = hash;
    }
}
