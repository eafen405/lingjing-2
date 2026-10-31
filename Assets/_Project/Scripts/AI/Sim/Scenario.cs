using Lingjing.Core;
using Lingjing.AI;
using Lingjing.Nav;
using System;
using System.Collections.Generic;

namespace Lingjing.Data
{

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
}
