using System.Diagnostics;
using BattleSim.Combat;
using BattleSim.Core;
using BattleSim.Enemies;
using BattleSim.Fairy;
using BattleSim.Metrics;
using BattleSim.Nav;
using BattleSim.Scenarios;

namespace BattleSim.Sim;

/// <summary>
/// 唯一的模拟接缝：固定步长推进世界，注入以骸与 Fairy，只依赖 BattleSnapshot 与 Command。
/// 所有测试/回归都从这里进。
/// </summary>
public sealed class BattleSimulation
{
    private readonly Scenario _scenario;
    private readonly ulong _seed;
    private readonly PolicyKind _policy;
    private readonly int _totalTicks;

    private readonly WorldState _world = new();
    private readonly ScriptedPlayerPolicy _scriptedPolicy = new();
    private readonly IPlanner _planner;
    private readonly TickHasher _hasher = new();
    private readonly MetricsCollector _metrics = new();
    private readonly List<TickLogEntry> _tickLog = new();
    private readonly Stopwatch _clock = new();

    private int _spawnCursor;
    private int _nextEnemyId = 1;
    private int _tick;
    private int _ticksExecuted;
    private int _deathTick = -1;
    private Command _playerCommand = Command.None;
    private Command _fairyCommand = Command.None;
    private Command _appliedCommand = Command.None;
    private bool _fairyActive;
    private int _fairyTicksLeft;
    private double _damageDealt;
    private int _enemiesSpawned;

    public BattleSimulation(Scenario scenario, ulong seed, PolicyKind policy, int? ticksOverride = null, IPlanner? planner = null)
    {
        _scenario = scenario;
        _seed = seed;
        _policy = policy;
        _totalTicks = Math.Max(1, ticksOverride ?? scenario.DurationTicks);
        _planner = planner ?? new BaselinePlanner();

        var grid = scenario.Grid;
        _world.Grid = grid;
        _world.AStar = new AStar(grid);
        _world.Flow = new FlowField(grid);
        _world.Rng = new Prng(seed);
        _world.Player = new Player
        {
            Pos = scenario.PlayerStart,
            Energy = Math.Min(DamageRules.EnergyMax, Math.Max(0.0, scenario.InitialCharge))
        };
        _world.Flow.Recompute(_world.Player.Pos);
    }

    /// <summary>本 tick 实际应用的指令（fairy 代行时为 planner 输出）。</summary>
    public Command AppliedCommand => _appliedCommand;

    public bool IsFairyActive => _fairyActive;

    public SimResult Run()
    {
        _clock.Restart();
        for (_tick = 0; _tick < _totalTicks; _tick++)
        {
            Step();
            _ticksExecuted++;
            if (_world.Player.Dead)
            {
                _deathTick = _tick;
                break;
            }
        }

        _clock.Stop();
        return BuildResult();
    }

    private void Step()
    {
        WorldState w = _world;
        w.Tick = _tick;
        w.AstarBudget = SimConstants.AstarBudgetPerTick;

        SpawnDue();

        if (!w.Player.Dead)
        {
            if (_tick % SimConstants.DecisionTicks == 0)
            {
                Decide();
            }

            ApplyPlayerCommand();
            IntegratePlayer();

            if (_tick % SimConstants.NavRecalcTicks == 0)
            {
                w.Flow.Recompute(w.Player.Pos);
            }

            UpdateBlades();
        }

        UpdateEnemies();
        ApplySeparation();
        UpdateProjectiles();
        UpdateZones();
        ApplyContactDamage();
        UpdateFairy();
        CollectMetrics();
        RecordTick();
        Compact();
        Trace?.Invoke(_tick, _world);
    }

    private void Decide()
    {
        _playerCommand = TimeDecision(() => _scriptedPolicy.Decide(_world, _scenario.PlayerScript));

        if (_fairyActive)
        {
            BattleSnapshot snapshot = SnapshotFactory.Create(_world, true);
            _fairyCommand = TimeDecision(() => _planner.Decide(snapshot));
        }
    }

    private Command TimeDecision(Func<Command> decide)
    {
        long start = Stopwatch.GetTimestamp();
        Command command = decide();
        long elapsed = Stopwatch.GetTimestamp() - start;
        _metrics.AddDecisionTime(elapsed * 1000.0 / Stopwatch.Frequency);
        return command;
    }

    private void ApplyPlayerCommand()
    {
        Player p = _world.Player;
        Command command = _fairyActive ? _fairyCommand : _playerCommand;
        _appliedCommand = command;

        if (p.ReverseCooldown > 0)
        {
            p.ReverseCooldown--;
        }

        bool wantsFlip = command.RequestReverse || (command.SpinSign != 0 && command.SpinSign != p.SpinSign);
        if (wantsFlip && p.ReverseCooldown == 0)
        {
            p.SpinSign = -p.SpinSign;
            p.ReverseCooldown = SimConstants.ReverseCooldownTicks;
        }

        Vec2 move = command.Move;
        double length = move.Length;
        if (length > 1.0)
        {
            move /= length;
        }

        p.MoveDir = move;
    }

    private void IntegratePlayer()
    {
        Player p = _world.Player;

        // 速度插值（球球大作战式手感）：目标速度 = 指令方向 × 移速，本 tick 只逼近固定增量。
        // 因此松手会滑行、反向会走弧线，但速度上限恒为 4，不会瞬移。
        Vec2 wanted = p.MoveDir * p.Speed;
        p.Vel = Motion.MoveTowards(p.Vel, wanted, p.Acceleration * SimConstants.Dt);
        p.Pos = _world.Grid.ResolveCircle(p.Pos + p.Vel * SimConstants.Dt, p.Radius);
    }

    private void UpdateBlades()
    {
        Player p = _world.Player;
        p.Ring.Advance(p.SpinSign, SimConstants.Dt);

        List<Enemy> enemies = _world.Enemies;
        for (int i = 0; i < enemies.Count; i++)
        {
            if (enemies[i].Alive)
            {
                enemies[i].TickBladeCooldowns();
            }
        }

        double reach = DamageRules.EnemyRadius + BladeRing.TipRadius;
        double reachSq = reach * reach;

        for (int blade = 0; blade < BladeRing.BladeCount; blade++)
        {
            Vec2 tip = p.Ring.TipAt(p.Pos, blade);
            for (int e = 0; e < enemies.Count; e++)
            {
                Enemy enemy = enemies[e];
                if (!enemy.Alive || enemy.BladeCooldown[blade] > 0)
                {
                    continue;
                }

                double radius = enemy.Radius + BladeRing.TipRadius;
                if ((tip - enemy.Pos).LengthSquared > radius * radius)
                {
                    continue;
                }

                ApplyBladeHit(enemy, blade);
            }
        }
    }

    private void ApplyBladeHit(Enemy enemy, int blade)
    {
        enemy.TakeDamage(DamageRules.BladeHitDamage);
        enemy.BladeCooldown[blade] = DamageRules.BladePerTargetCooldownTicks;
        _metrics.Hits++;
        _damageDealt += DamageRules.BladeHitDamage;

        if (!_fairyActive)
        {
            _world.Player.Energy = Math.Min(DamageRules.EnergyMax, _world.Player.Energy + DamageRules.EnergyPerHit);
        }

        if (!enemy.Alive)
        {
            _metrics.Kills++;
            if (!_fairyActive)
            {
                _world.Player.Energy = Math.Min(DamageRules.EnergyMax, _world.Player.Energy + DamageRules.EnergyPerKill);
            }
        }
    }

    private void UpdateEnemies()
    {
        List<Enemy> enemies = _world.Enemies;
        int count = enemies.Count;
        for (int i = 0; i < count; i++)
        {
            Enemy enemy = enemies[i];
            enemy.Update(_world);
        }
    }

    /// <summary>同类之间最小间距 0.6：只在小于间距时施加分离位移，不做物理推挤。</summary>
    private void ApplySeparation()
    {
        const double minSpacing = 0.6;
        List<Enemy> enemies = _world.Enemies;
        for (int i = 0; i < enemies.Count; i++)
        {
            Enemy a = enemies[i];
            if (!a.Alive)
            {
                continue;
            }

            for (int j = i + 1; j < enemies.Count; j++)
            {
                Enemy b = enemies[j];
                if (!b.Alive || a.Kind != b.Kind)
                {
                    continue;
                }

                Vec2 delta = a.Pos - b.Pos;
                double distance = delta.Length;
                if (distance >= minSpacing || distance < 1e-9)
                {
                    continue;
                }

                Vec2 normal = delta / distance;
                double push = (minSpacing - distance) * 0.5;
                a.Pos = a.Pos + normal * push;
                b.Pos = b.Pos - normal * push;
            }
        }

        for (int i = 0; i < enemies.Count; i++)
        {
            Enemy e = enemies[i];
            if (!e.Alive)
            {
                continue;
            }

            e.Pos = e.Flying
                ? new Vec2(SimMath.Clamp(e.Pos.X, 0.5, _world.Grid.Width - 0.5), SimMath.Clamp(e.Pos.Y, 0.5, _world.Grid.Height - 0.5))
                : _world.Grid.ResolveCircle(e.Pos, e.Radius);
        }
    }

    private void UpdateProjectiles()
    {
        Player p = _world.Player;
        List<Projectile> projectiles = _world.Projectiles;
        for (int i = 0; i < projectiles.Count; i++)
        {
            Projectile projectile = projectiles[i];
            if (!projectile.Alive)
            {
                continue;
            }

            projectile.Step(_world.Grid);
            if (!projectile.Alive)
            {
                continue;
            }

            double reach = projectile.Radius + p.Radius;
            if ((projectile.Pos - p.Pos).LengthSquared <= reach * reach)
            {
                p.TakeDamage(projectile.Damage);
                projectile.Alive = false;
            }
        }
    }

    private void UpdateZones()
    {
        Player p = _world.Player;
        List<HazardZone> zones = _world.Zones;
        for (int i = 0; i < zones.Count; i++)
        {
            HazardZone zone = zones[i];
            if (zone.Resolved)
            {
                continue;
            }

            zone.WarnTicksLeft--;
            if (zone.WarnTicksLeft > 0)
            {
                continue;
            }

            zone.Resolved = true;
            if (zone.ContainsCircle(p.Pos, p.Radius))
            {
                p.TakeDamage(zone.Damage);
            }
        }
    }

    private void ApplyContactDamage()
    {
        Player p = _world.Player;
        if (p.Dead)
        {
            return;
        }

        List<Enemy> enemies = _world.Enemies;
        for (int i = 0; i < enemies.Count; i++)
        {
            Enemy enemy = enemies[i];
            if (!enemy.Alive)
            {
                continue;
            }

            double reach = enemy.Radius + p.Radius;
            double distanceSq = (enemy.Pos - p.Pos).LengthSquared;
            if (distanceSq > reach * reach)
            {
                continue;
            }

            switch (enemy)
            {
                case TailLight:
                    p.TakeDamage(DamageRules.TailLightContactDps * SimConstants.Dt);
                    break;
                case Striker striker when striker.CanHitDuringDash:
                    p.TakeDamage(DamageRules.StrikerDashImpact);
                    striker.ConsumeDashHit();
                    break;
            }
        }
    }

    private void UpdateFairy()
    {
        Player p = _world.Player;

        if (_fairyActive)
        {
            _fairyTicksLeft--;
            _metrics.FairyTicks++;
            if (_fairyTicksLeft <= 0)
            {
                _fairyActive = false;
            }

            return;
        }

        if (_policy == PolicyKind.Fairy && !p.Dead && p.Energy >= DamageRules.EnergyMax - 1e-9)
        {
            // 满槽释放：清空充能，4 秒内玩家输入被屏蔽，由 planner 下指令。
            p.Energy = 0.0;
            _fairyActive = true;
            _fairyTicksLeft = SimConstants.FairyDurationTicks;
            _fairyCommand = new Command(p.MoveDir, p.SpinSign, false);
            _metrics.FairyTakeovers++;
        }
    }

    private void CollectMetrics()
    {
        Player p = _world.Player;
        double inner = BladeRing.OuterRadius * 0.85;
        double outer = BladeRing.OuterRadius * 1.15;

        int onRing = 0;
        List<Enemy> enemies = _world.Enemies;
        for (int i = 0; i < enemies.Count; i++)
        {
            Enemy enemy = enemies[i];
            if (!enemy.Alive)
            {
                continue;
            }

            double d = enemy.Pos.DistanceTo(p.Pos);
            if (d >= inner && d <= outer)
            {
                onRing++;
            }
        }

        _metrics.AddRingSample(onRing);
    }

    private void RecordTick()
    {
        ulong hash = _hasher.HashTick(_tick, _seed, _playerCommand, _fairyCommand, _fairyActive, _world);
        _tickLog.Add(new TickLogEntry(
            _tick,
            _playerCommand.Move.X,
            _playerCommand.Move.Y,
            _playerCommand.SpinSign,
            _playerCommand.RequestReverse,
            _fairyCommand.Move.X,
            _fairyCommand.Move.Y,
            _fairyCommand.SpinSign,
            _fairyActive,
            hash));
    }

    private void Compact()
    {
        _world.Enemies.RemoveAll(static e => !e.Alive);
        _world.Projectiles.RemoveAll(static p => !p.Alive);
        _world.Zones.RemoveAll(static z => z.Resolved);
    }

    private void SpawnDue()
    {
        List<ScenarioSpawn> spawns = _scenario.Spawns;
        while (_spawnCursor < spawns.Count && spawns[_spawnCursor].Tick <= _tick)
        {
            ScenarioSpawn spawn = spawns[_spawnCursor];
            Enemy enemy = CreateEnemy(spawn.Kind);
            enemy.Id = _nextEnemyId++;
            enemy.SpawnTick = spawn.Tick;
            enemy.Pos = spawn.Pos;
            _world.Enemies.Add(enemy);
            _enemiesSpawned++;
            _spawnCursor++;
        }
    }

    private static Enemy CreateEnemy(EnemyKind kind) => kind switch
    {
        EnemyKind.TailLight => new TailLight(),
        EnemyKind.Striker => new Striker(),
        EnemyKind.NailGun => new NailGun(),
        EnemyKind.Dropper => new Dropper(),
        EnemyKind.ChainBoss => new ChainBoss(),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "未知敌人类型")
    };

    private SimResult BuildResult()
    {
        Player p = _world.Player;
        ulong[] hashes = new ulong[_tickLog.Count];
        for (int i = 0; i < _tickLog.Count; i++)
        {
            hashes[i] = _tickLog[i].Hash;
        }

        return new SimResult
        {
            Scenario = _scenario.Name,
            Policy = _policy == PolicyKind.Fairy ? "fairy" : "scripted",
            PlayerScript = _scenario.PlayerScript == PlayerScriptKind.Orbit ? "orbit" : "flee",
            Seed = _seed,
            Ticks = _ticksExecuted,
            Survived = !p.Dead,
            DeathTick = _deathTick,
            Hp = p.Hp,
            HpLost = p.MaxHp - p.Hp,
            Hits = _metrics.Hits,
            Kills = _metrics.Kills,
            DamageDealt = _damageDealt,
            DamageTaken = p.DamageTakenTotal,
            AvgEnemiesOnRing = _metrics.AverageRing,
            AvgDecisionMs = _metrics.AverageDecisionMs,
            MaxDecisionMs = _metrics.MaxDecisionMs,
            DecisionCount = _metrics.DecisionCount,
            FairyTakeovers = _metrics.FairyTakeovers,
            FairyTicks = _metrics.FairyTicks,
            EnemiesSpawned = _enemiesSpawned,
            EnemiesAlive = _world.Enemies.Count,
            SimElapsedMs = _clock.Elapsed.TotalMilliseconds,
            HashChainTail = _tickLog.Count == 0 ? 14695981039346656037UL : _tickLog[_tickLog.Count - 1].Hash,
            TickHashes = hashes,
            TickLogPath = ""
        };
    }

    /// <summary>逐 tick 记录，供写 CSV 使用（不参与模拟）。</summary>
    public IReadOnlyList<TickLogEntry> TickLog => _tickLog;

    /// <summary>调试用逐 tick 观察回调（默认关闭）。只读世界，不得修改，否则破坏复刻。</summary>
    public Action<int, WorldState>? Trace { get; set; }
}
