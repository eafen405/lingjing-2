using BattleSim.Core;

namespace BattleSim.Metrics;

/// <summary>一次模拟的指标与结果。字段名直接对应 results/*.json。</summary>
public sealed class SimResult
{
    public string Scenario = "";
    public string Policy = "";
    public string PlayerScript = "";
    public ulong Seed;

    public int Ticks;
    public bool Survived;
    public int DeathTick = -1;
    public double Hp;
    public double HpLost;

    public int Hits;
    public int Kills;
    public double DamageDealt;
    public double DamageTaken;

    public double AvgEnemiesOnRing;
    public double AvgDecisionMs;
    public double MaxDecisionMs;
    public int DecisionCount;

    public int FairyTakeovers;
    public int FairyTicks;

    public int EnemiesSpawned;
    public int EnemiesAlive;

    public double SimElapsedMs;
    public ulong HashChainTail = 14695981039346656037UL;
    public ulong[] TickHashes = Array.Empty<ulong>();
    public string TickLogPath = "";
}

/// <summary>逐 tick 采集，最后一次性结算。所有累加都用 double，避免整数除法丢精度。</summary>
public sealed class MetricsCollector
{
    private double _ringSum;
    private int _ringSamples;
    private double _decisionMsSum;
    private int _decisionSamples;
    private double _decisionMsMax;

    public int Hits;
    public int Kills;
    public double DamageDealt;
    public int FairyTakeovers;
    public int FairyTicks;

    public void AddRingSample(int enemiesOnRing)
    {
        _ringSum += enemiesOnRing;
        _ringSamples++;
    }

    public void AddDecisionTime(double milliseconds)
    {
        _decisionMsSum += milliseconds;
        if (milliseconds > _decisionMsMax)
        {
            _decisionMsMax = milliseconds;
        }

        _decisionSamples++;
    }

    public double AverageRing => _ringSamples == 0 ? 0.0 : _ringSum / _ringSamples;

    public double AverageDecisionMs => _decisionSamples == 0 ? 0.0 : _decisionMsSum / _decisionSamples;

    public double MaxDecisionMs => _decisionMsMax;

    public int DecisionCount => _decisionSamples;
}
