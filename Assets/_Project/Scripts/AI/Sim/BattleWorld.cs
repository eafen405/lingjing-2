using System;
using System.Collections.Generic;
using Lingjing.AI;
using Lingjing.Combat;
using Lingjing.Core;
using Lingjing.Fairy;
using Lingjing.Nav;

namespace Lingjing.Sim
{
    /// <summary>
    /// Unity 侧的模拟接缝：固定 1/60 步长推进世界，玩家指令由外部注入。
    /// 与无头原型（prototype/battlesim）共用同一套运动、伤害与命中规则；
    /// 表现层只读世界、只订阅事件，不得回写。
    /// </summary>
    public sealed class BattleWorld
    {
        public readonly WorldState World = new WorldState();

        public int Tick { get; private set; }

        public bool PlayerDead => World.Player.Dead;

        /// <summary>飞刀命中：参数为被命中的以骸与本刀伤害。</summary>
        public event Action<Enemy, double> BladeHit;

        /// <summary>以骸死亡。事件在移除前发出，调用方仍可读到位置。</summary>
        public event Action<Enemy> EnemyKilled;

        /// <summary>玩家受伤。</summary>
        public event Action<double> PlayerHurt;

        /// <summary>玩家死亡，每局只发一次。</summary>
        public event Action PlayerDied;

        int _nextEnemyId = 1;
        bool _deathAnnounced;

        IPlanner _planner;
        int _fairyTicksLeft;
        Command _fairyCommand = Command.None;

        /// <summary>代行进行中：玩家输入被屏蔽，指令来自规划器，且不获得充能。</summary>
        public bool FairyActive => _fairyTicksLeft > 0;

        public int FairyTicksLeft => _fairyTicksLeft;

        public event Action FairyStarted;
        public event Action FairyEnded;

        /// <summary>注入代行规划器；不注入则技能键无效。</summary>
        public void SetFairyPlanner(IPlanner planner)
        {
            _planner = planner;
        }

        /// <summary>满槽且不在代行中时接管 4 秒。返回是否真的接管。</summary>
        public bool TryActivateFairy()
        {
            if (_planner == null || FairyActive || World.Player.Dead)
            {
                return false;
            }

            if (World.Player.Energy < DamageRules.EnergyMax - 1e-9)
            {
                return false;
            }

            World.Player.Energy = 0.0;
            _fairyTicksLeft = SimConstants.FairyDurationTicks;
            _fairyCommand = new Command(World.Player.MoveDir, World.Player.SpinSign, false);
            FairyStarted?.Invoke();
            return true;
        }

        public BattleWorld(Grid grid, Vec2 playerStart, ulong seed, double initialCharge = 0.0)
        {
            World.Grid = grid;
            World.AStar = new AStar(grid);
            World.Flow = new FlowField(grid);
            World.Rng = new Prng(seed);
            World.Player = new Player
            {
                Pos = playerStart,
                Energy = SimMath.Clamp(initialCharge, 0.0, DamageRules.EnergyMax)
            };
            World.Flow.Recompute(World.Player.Pos);
        }

        /// <summary>生成一个以骸，返回其稳定 Id。Id 只增不减，供表现层做绑定键。</summary>
        public Enemy Spawn(EnemyKind kind, Vec2 position)
        {
            Enemy enemy = Create(kind);
            enemy.Id = _nextEnemyId++;
            enemy.SpawnTick = Tick;
            enemy.Pos = position;
            World.Enemies.Add(enemy);
            return enemy;
        }

        /// <summary>推进一个固定步长。command 为玩家本步指令（Fairy 代行时由规划器给出）。</summary>
        public void Step(Command command)
        {
            WorldState w = World;
            w.Tick = Tick;
            w.AstarBudget = SimConstants.AstarBudgetPerTick;

            if (!w.Player.Dead)
            {
                Command applied = command;
                if (FairyActive)
                {
                    if (Tick % SimConstants.DecisionTicks == 0 && _planner != null)
                    {
                        _fairyCommand = _planner.Decide(SnapshotFactory.Create(w, true));
                    }

                    applied = _fairyCommand;
                }

                ApplyPlayerCommand(applied);
                IntegratePlayer();

                if (Tick % SimConstants.NavRecalcTicks == 0)
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

            Compact();

            if (w.Player.Dead && !_deathAnnounced)
            {
                _deathAnnounced = true;
                PlayerDied?.Invoke();
            }

            if (_fairyTicksLeft > 0)
            {
                _fairyTicksLeft--;
                if (_fairyTicksLeft == 0)
                {
                    FairyEnded?.Invoke();
                }
            }

            Tick++;
        }

        static Enemy Create(EnemyKind kind)
        {
            switch (kind)
            {
                case EnemyKind.TailLight: return new TailLight();
                case EnemyKind.Striker: return new Striker();
                case EnemyKind.NailGun: return new NailGun();
                case EnemyKind.Dropper: return new Dropper();
                case EnemyKind.ChainBoss: return new ChainBoss();
                default: throw new ArgumentOutOfRangeException(nameof(kind), kind, "未知以骸类型");
            }
        }

        void ApplyPlayerCommand(Command command)
        {
            Player p = World.Player;

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

        void IntegratePlayer()
        {
            Player p = World.Player;
            Vec2 wanted = p.MoveDir * p.Speed;
            p.Vel = Motion.MoveTowards(p.Vel, wanted, p.Acceleration * SimConstants.Dt);
            p.Pos = World.Grid.ResolveCircle(p.Pos + p.Vel * SimConstants.Dt, p.Radius);
        }

        void UpdateBlades()
        {
            Player p = World.Player;
            p.Ring.Advance(p.SpinSign, SimConstants.Dt);

            List<Enemy> enemies = World.Enemies;
            for (int i = 0; i < enemies.Count; i++)
            {
                if (enemies[i].Alive)
                {
                    enemies[i].TickBladeCooldowns();
                }
            }

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

        void ApplyBladeHit(Enemy enemy, int blade)
        {
            enemy.TakeDamage(DamageRules.BladeHitDamage);
            enemy.BladeCooldown[blade] = DamageRules.BladePerTargetCooldownTicks;

            Player p = World.Player;
            if (!FairyActive)
            {
                p.Energy = Math.Min(DamageRules.EnergyMax, p.Energy + DamageRules.EnergyPerHit);
            }

            BladeHit?.Invoke(enemy, DamageRules.BladeHitDamage);

            if (!enemy.Alive)
            {
                if (!FairyActive)
                {
                    p.Energy = Math.Min(DamageRules.EnergyMax, p.Energy + DamageRules.EnergyPerKill);
                }

                EnemyKilled?.Invoke(enemy);
            }
        }

        void UpdateEnemies()
        {
            List<Enemy> enemies = World.Enemies;
            for (int i = 0; i < enemies.Count; i++)
            {
                enemies[i].Update(World);
            }
        }

        /// <summary>同类之间最小间距 0.6：只在小于间距时施加分离位移，不做物理推挤。</summary>
        void ApplySeparation()
        {
            const double minSpacing = 0.6;
            List<Enemy> enemies = World.Enemies;

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
                    ? new Vec2(
                        SimMath.Clamp(e.Pos.X, 0.5, World.Grid.Width - 0.5),
                        SimMath.Clamp(e.Pos.Y, 0.5, World.Grid.Height - 0.5))
                    : World.Grid.ResolveCircle(e.Pos, e.Radius);
            }
        }

        void UpdateProjectiles()
        {
            Player p = World.Player;
            List<Projectile> projectiles = World.Projectiles;

            for (int i = 0; i < projectiles.Count; i++)
            {
                Projectile projectile = projectiles[i];
                if (!projectile.Alive)
                {
                    continue;
                }

                projectile.Step(World.Grid);
                if (!projectile.Alive)
                {
                    continue;
                }

                double reach = projectile.Radius + p.Radius;
                if ((projectile.Pos - p.Pos).LengthSquared <= reach * reach)
                {
                    projectile.Alive = false;
                    Hurt(projectile.Damage);
                }
            }
        }

        void UpdateZones()
        {
            Player p = World.Player;
            List<HazardZone> zones = World.Zones;

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
                    Hurt(zone.Damage);
                }
            }
        }

        void ApplyContactDamage()
        {
            Player p = World.Player;
            if (p.Dead)
            {
                return;
            }

            List<Enemy> enemies = World.Enemies;
            for (int i = 0; i < enemies.Count; i++)
            {
                Enemy enemy = enemies[i];
                if (!enemy.Alive)
                {
                    continue;
                }

                double reach = enemy.Radius + p.Radius;
                if ((enemy.Pos - p.Pos).LengthSquared > reach * reach)
                {
                    continue;
                }

                switch (enemy)
                {
                    case TailLight:
                        Hurt(DamageRules.TailLightContactDps * SimConstants.Dt);
                        break;
                    case Striker striker when striker.CanHitDuringDash:
                        Hurt(DamageRules.StrikerDashImpact);
                        striker.ConsumeDashHit();
                        break;
                }
            }
        }

        void Hurt(double amount)
        {
            if (amount <= 0.0)
            {
                return;
            }

            World.Player.TakeDamage(amount);
            PlayerHurt?.Invoke(amount);
        }

        void Compact()
        {
            World.Enemies.RemoveAll(static e => !e.Alive);
            World.Projectiles.RemoveAll(static p => !p.Alive);
            World.Zones.RemoveAll(static z => z.Resolved);
        }
    }
}
