using Lingjing.AI;
using Lingjing.Combat;
using Lingjing.Core;
using System;
using System.Collections.Generic;

namespace Lingjing.Fairy
{
    /// <summary>
    /// lex-full 规划器：把 prototype/ai-exploration/ExplorationPlanner.cs 的完整分层策略搬进正式模拟。
    ///
    /// 动作集合：16 个等角方向 + 零向量 + 当前 PlayerMoveDir + 最近 4 只以骸的站位两档
    /// + 最近 1 只以骸的闭环站位两档，共 28 个移动候选；每个候选两个旋向，共 56 个动作。
    /// 每个候选按固定 1 tick 步长前瞻 60 tick（1 秒），运动学与网格碰撞都复用正式规则。
    ///
    /// 决策链：
    ///   P0：保留预测伤害最小的候选（容差 1e-9）；
    ///   P1：在 P0 里保留环上覆盖不低于最优值 85% 的候选；
    ///   P1.5：在 P1 里保留「前瞻结束时落在环上」的候选（结束误差容差 0.05）；
    ///   P2：在 P1.5 里取相对角速度积分最大者（容差 1e-9）；
    ///   平局：连续性降序（dir·previousMove，同旋向 +0.05），再按候选 Id 升序。
    ///
    /// P0/P1/P2 与原型逐条一致；站位候选与 P1.5 是整局自走时补的——原型只跑 4 秒代行，
    /// 开环动作集在那段时间里够用，拉到整局就会绕着以骸满速转圈、永远不把对方压到环上。
    ///
    /// 只读 BattleSnapshot：不读墙钟、随机数或未来刷新。所有轨迹与评分缓冲都是成员字段，
    /// Decide 内不分配内存（敌人预测对象首次出现时分配，之后复用）。
    /// </summary>
    public sealed class LexPlanner : IPlanner
    {
        const int DirectionCount = 16;
        const int HoldEnemies = 4;
        const int HoldVariants = 2;
        const int HoldCount = HoldEnemies * HoldVariants;
        const int StationVariants = 2;
        const int MoveCount = DirectionCount + 2 + HoldCount + StationVariants;
        const int CandidateCount = MoveCount * 2;

        /// <summary>站位候选的慢速档：全速会冲过环，慢档才是"停在环上"。</summary>
        const double HoldSlowScale = 0.25;

        /// <summary>闭环站位的径向增益：半径误差 → 径向速度（每秒按误差的几倍修正）。</summary>
        const double StationGain = 3.0;

        /// <summary>环路切向速度占玩家速度的比例：越大 ω_rel 越高，但越容易甩出环带。</summary>
        const double StationTangentScale = 0.8;

        /// <summary>前瞻 60 tick = 1 秒。</summary>
        const int HorizonTicks = 60;

        /// <summary>前瞻步长 1 tick（lex-full 不做粗筛）。</summary>
        const int StrideTicks = 1;

        const double DamageTolerance = 1e-9;
        const double RingTolerance = 1e-12;
        const double RingRetention = 0.85;
        const double VTolerance = 1e-9;
        const double SameSpinBonus = 0.05;
        const double RingKernelWidth = 0.65;

        /// <summary>结束误差只在「还算够得着」的以骸上算：超过刀刃半径这么多就不算接触目标。</summary>
        const double EndReachMargin = 3.0;

        /// <summary>结束误差的容差：差这么多以内视为同样"停在环上"。</summary>
        const double EndErrorTolerance = 0.05;

        /// <summary>每个前瞻步的权重：stride × dt。</summary>
        const double StepWeight = StrideTicks * SimConstants.Dt;

        // 以骸状态编号与原型一致，这里用枚举常量而不是裸数字，避免两套状态机错位。
        const int StrikerWindup = (int)Striker.StrikerState.Windup;
        const int StrikerDash = (int)Striker.StrikerState.Dash;
        const int StrikerRecover = (int)Striker.StrikerState.Recover;
        const int NailGunAim = (int)NailGun.NailState.Aim;
        const int NailGunFire = (int)NailGun.NailState.Fire;
        const int NailGunRecover = (int)NailGun.NailState.Recover;
        const int DropperMark = (int)Dropper.DropperState.Mark;
        const int DropperDrop = (int)Dropper.DropperState.Drop;

        /// <summary>
        /// 玩家加速度。快照里没有这个字段，原型同样是取 Player 的默认值（<c>new Player().Acceleration</c>）：
        /// 它是固定调参，不是会逐帧变化的观测；玩家半径/速度仍以快照为准。
        /// </summary>
        static readonly double Acceleration = new Player().Acceleration;

        static readonly Vec2[] DirectionTable = BuildDirections();

        /// <summary>本帧的移动候选：16 方向 + 零 + 当前 MoveDir + 最近的几只以骸的站位候选。</summary>
        readonly Vec2[] _moveDirs = new Vec2[MoveCount];

        /// <summary>站位候选对应的以骸下标（-1 表示空位）。</summary>
        readonly int[] _holdTargets = new int[HoldEnemies];

        // 候选轨迹缓冲，索引 m*(HorizonTicks+1)+k。
        readonly Vec2[] _pathPositions = new Vec2[MoveCount * (HorizonTicks + 1)];
        readonly Vec2[] _pathVelocities = new Vec2[MoveCount * (HorizonTicks + 1)];
        readonly double[] _pathDamage = new double[MoveCount];

        // 36 个动作的评分。Damage 是每个移动候选共享的，按候选 Id 摊平以便与原型一致地逐 Id 比较。
        readonly double[] _candidateDamage = new double[CandidateCount];
        readonly double[] _candidateRing = new double[CandidateCount];
        readonly double[] _candidateEndError = new double[CandidateCount];
        readonly double[] _candidateV = new double[CandidateCount];
        readonly double[] _candidateContinuity = new double[CandidateCount];

        readonly List<EnemyPrediction> _predictions = new List<EnemyPrediction>(8);

        /// <summary>上一次决策时看到的敌人位置，供相邻快照差分估速。</summary>
        readonly Dictionary<int, HistoryPoint> _history = new Dictionary<int, HistoryPoint>();

        // 伤害推演用的可复用对象：只改字段，不参与世界写入。
        readonly Projectile _shot = new Projectile();
        readonly HazardZone _zone = new HazardZone();

        Vec2 _previousMove;

        public Command Decide(BattleSnapshot s)
        {
            int enemyCount = PredictEnemies(s);

            Array.Copy(DirectionTable, _moveDirs, DirectionCount);
            _moveDirs[DirectionCount] = Vec2.Zero;
            _moveDirs[DirectionCount + 1] = s.PlayerMoveDir;
            FillHoldMoves(s);
            FillStationMoves(s);

            for (int m = 0; m < MoveCount; m++)
            {
                BuildPath(s, _moveDirs[m], m, enemyCount);
            }

            // 构建 36 个动作并评分（lex-full 对全部动作做完整精评）。
            for (int m = 0; m < MoveCount; m++)
            {
                double damage = _pathDamage[m];
                double dotPrevious = _moveDirs[m].Dot(_previousMove);

                for (int spin = 0; spin < 2; spin++)
                {
                    int id = m * 2 + spin;
                    int sign = spin == 0 ? 1 : -1;

                    _candidateDamage[id] = damage;
                    _candidateContinuity[id] = dotPrevious + (sign == s.SpinSign ? SameSpinBonus : 0.0);
                    Evaluate(s, sign, m, id, enemyCount);
                }
            }

            // P0：最小预测伤害。
            double bestDamage = double.PositiveInfinity;
            for (int id = 0; id < CandidateCount; id++)
            {
                if (_candidateDamage[id] < bestDamage)
                {
                    bestDamage = _candidateDamage[id];
                }
            }

            // P1：环上覆盖不低于最优值的 85%。
            double bestRing = double.NegativeInfinity;
            for (int id = 0; id < CandidateCount; id++)
            {
                if (_candidateDamage[id] <= bestDamage + DamageTolerance && _candidateRing[id] > bestRing)
                {
                    bestRing = _candidateRing[id];
                }
            }

            // P1.5：在 P1 里保留「前瞻结束时落在环上」的候选。
            // 只用 ring 的平均覆盖会奖励「站得够近」而不是「踩在环上」——实测绕圈覆盖 0.34 就饱和，
            // 结果她绕着以骸满速转圈，一只钉枪能空耗两千秒。
            double bestEndError = double.PositiveInfinity;
            for (int id = 0; id < CandidateCount; id++)
            {
                if (_candidateDamage[id] <= bestDamage + DamageTolerance &&
                    _candidateRing[id] + RingTolerance >= bestRing * RingRetention &&
                    _candidateEndError[id] < bestEndError)
                {
                    bestEndError = _candidateEndError[id];
                }
            }

            // P2：相对角速度积分最大。
            double bestV = double.NegativeInfinity;
            for (int id = 0; id < CandidateCount; id++)
            {
                if (_candidateDamage[id] <= bestDamage + DamageTolerance &&
                    _candidateRing[id] + RingTolerance >= bestRing * RingRetention &&
                    _candidateEndError[id] <= bestEndError + EndErrorTolerance &&
                    _candidateV[id] > bestV)
                {
                    bestV = _candidateV[id];
                }
            }

            // 平局：连续性降序，再按 Id 升序（升序扫描 + 严格大于即等价于先按连续性再按 Id）。
            int chosenId = -1;
            double bestContinuity = double.NegativeInfinity;
            for (int id = 0; id < CandidateCount; id++)
            {
                if (_candidateDamage[id] > bestDamage + DamageTolerance)
                {
                    continue;
                }

                if (!(_candidateRing[id] + RingTolerance >= bestRing * RingRetention))
                {
                    continue;
                }

                if (!(_candidateEndError[id] <= bestEndError + EndErrorTolerance))
                {
                    continue;
                }

                if (!(_candidateV[id] >= bestV - VTolerance))
                {
                    continue;
                }

                if (_candidateContinuity[id] > bestContinuity)
                {
                    bestContinuity = _candidateContinuity[id];
                    chosenId = id;
                }
            }

            if (chosenId < 0)
            {
                // 只有出现 NaN 评分才会走到这里；退回静止，避免把无效指令丢给执行层。
                return Command.None;
            }

            // 调试量：只读，供探针观察"她以为哪个候选更好"。不参与决策。
            DebugBestDamage = bestDamage;
            DebugBestRing = bestRing;
            DebugBestV = bestV;
            DebugChosenDamage = _candidateDamage[chosenId];
            DebugChosenRing = _candidateRing[chosenId];
            DebugChosenV = _candidateV[chosenId];

            // 站位候选自己的分：用来分清"环覆盖上不去"还是"被 P0 门槛挡掉"。
            DebugHoldRing = 0.0;
            DebugHoldDamage = double.PositiveInfinity;
            for (int m = DirectionCount + 2; m < MoveCount; m++)
            {
                for (int spin = 0; spin < 2; spin++)
                {
                    int id = m * 2 + spin;
                    if (_candidateRing[id] > DebugHoldRing)
                    {
                        DebugHoldRing = _candidateRing[id];
                    }

                    if (_candidateDamage[id] < DebugHoldDamage)
                    {
                        DebugHoldDamage = _candidateDamage[id];
                    }
                }
            }

            int moveIndex = chosenId / 2;
            _previousMove = _moveDirs[moveIndex];
            return new Command(_previousMove, chosenId % 2 == 0 ? 1 : -1, false);
        }

        /// <summary>上一次决策的三级评分（最优值 / 实际选中值）。探针用，不参与决策。</summary>
        public double DebugBestDamage { get; private set; }

        public double DebugBestRing { get; private set; }

        public double DebugBestV { get; private set; }

        public double DebugChosenDamage { get; private set; }

        public double DebugChosenRing { get; private set; }

        public double DebugChosenV { get; private set; }

        /// <summary>站位候选里最好的环覆盖与最小预测伤害。</summary>
        public double DebugHoldRing { get; private set; }

        public double DebugHoldDamage { get; private set; }

        /// <summary>
        /// 敌人预测：相邻快照位置差分成速度；预警/硬直阶段按静止外推；撞针冲刺速度用常量截断；
        /// 地面单位按 Grid.ResolveCircle 推进，飞行单位只夹到地图边界。与原型同一套简化模型。
        /// </summary>
        int PredictEnemies(BattleSnapshot s)
        {
            int count = 0;
            for (int i = 0; i < s.Enemies.Length; i++)
            {
                SnapshotEnemy enemy = s.Enemies[i];
                if (!enemy.Alive)
                {
                    continue;
                }

                EnemyPrediction prediction = AcquirePrediction(count);

                Vec2 velocity = Vec2.Zero;
                if (_history.TryGetValue(enemy.Id, out HistoryPoint previous) && s.Tick > previous.Tick)
                {
                    velocity = (enemy.Pos - previous.Pos) / ((s.Tick - previous.Tick) * SimConstants.Dt);
                }

                if (HoldsStill(enemy))
                {
                    velocity = Vec2.Zero;
                }

                double maxSpeed = enemy.Kind == EnemyKind.Striker && enemy.State == StrikerDash
                    ? Striker.DashSpeedConst
                    : enemy.MoveSpeed;
                if (velocity.Length > maxSpeed && maxSpeed > 0.0)
                {
                    velocity = velocity.Normalized() * maxSpeed;
                }

                prediction.Enemy = enemy;
                prediction.Velocity = velocity;
                Vec2[] positions = prediction.Positions;
                positions[0] = enemy.Pos;

                for (int k = 1; k <= HorizonTicks; k++)
                {
                    Vec2 next = positions[k - 1] + velocity * SimConstants.Dt;
                    positions[k] = enemy.Flying
                        ? new Vec2(
                            SimMath.Clamp(next.X, 0.5, s.Grid.Width - 0.5),
                            SimMath.Clamp(next.Y, 0.5, s.Grid.Height - 0.5))
                        : s.Grid.ResolveCircle(next, enemy.Radius);
                }

                _history[enemy.Id] = new HistoryPoint(enemy.Pos, s.Tick);
                count++;
            }

            return count;
        }

        /// <summary>预警/瞄准/硬直/冲刺预备阶段敌人原地不动，速度按零外推（原型同一判据）。</summary>
        static bool HoldsStill(SnapshotEnemy e)
        {
            switch (e.Kind)
            {
                case EnemyKind.Striker:
                    return e.State == StrikerWindup || e.State == StrikerRecover;
                case EnemyKind.NailGun:
                    return e.State == NailGunAim || e.State == NailGunFire || e.State == NailGunRecover;
                case EnemyKind.Dropper:
                    return e.State == DropperMark || e.State == DropperDrop;
                default:
                    return false;
            }
        }

        /// <summary>一个移动候选的 60 tick 轨迹与预测伤害（弹丸、落区、敌人接触）。</summary>
        /// <summary>
        /// 站位候选：朝「该以骸周围、刀刃半径上的那个点」走，全速一档、慢速一档。
        /// 没有这两档时，动作集只有「朝某方向走满一秒」——永远停不在环上。
        /// 实测（整局自走）表现为绕着以骸满速转圈、环上覆盖 0.34 一直上不去，一只钉枪能耗掉两千秒。
        /// </summary>
        void FillHoldMoves(BattleSnapshot s)
        {
            for (int k = 0; k < HoldEnemies; k++)
            {
                _holdTargets[k] = -1;
            }

            // 就近插入：站位只需要覆盖当前最相关的几只，不必全排序。
            for (int i = 0; i < s.Enemies.Length; i++)
            {
                if (!s.Enemies[i].Alive)
                {
                    continue;
                }

                double distance = (s.Enemies[i].Pos - s.PlayerPos).LengthSquared;
                for (int k = 0; k < HoldEnemies; k++)
                {
                    if (_holdTargets[k] < 0)
                    {
                        _holdTargets[k] = i;
                        break;
                    }

                    double current = (s.Enemies[_holdTargets[k]].Pos - s.PlayerPos).LengthSquared;
                    if (distance >= current)
                    {
                        continue;
                    }

                    for (int j = HoldEnemies - 1; j > k; j--)
                    {
                        _holdTargets[j] = _holdTargets[j - 1];
                    }

                    _holdTargets[k] = i;
                    break;
                }
            }

            for (int k = 0; k < HoldEnemies; k++)
            {
                int slot = DirectionCount + 2 + k * HoldVariants;
                int index = _holdTargets[k];
                if (index < 0)
                {
                    _moveDirs[slot] = Vec2.Zero;
                    _moveDirs[slot + 1] = Vec2.Zero;
                    continue;
                }

                Vec2 enemyPos = s.Enemies[index].Pos;
                Vec2 away = s.PlayerPos - enemyPos;
                double length = away.Length;
                Vec2 bearing = length > 1e-9 ? away / length : Vec2.UnitX;

                // 环上那个点：从以骸出发、沿当前方位走一个刀刃半径。
                Vec2 toRing = (enemyPos + bearing * s.BladeRadius) - s.PlayerPos;
                Vec2 dir = toRing.Normalized();

                _moveDirs[slot] = dir;
                _moveDirs[slot + 1] = dir * HoldSlowScale;
            }
        }

        /// <summary>
        /// 闭环站位候选：按半径误差实时给径向速度，再叠一个切向分量榨 ω_rel。
        /// 开环动作集解决不了"追着跑的以骸"——它每秒都在变距离，任何固定方向走一秒都会过冲或者够不着。
        /// 两条候选对应两个切向（正/反），正好与两个旋向配对。
        /// </summary>
        void FillStationMoves(BattleSnapshot s)
        {
            int slot = DirectionCount + 2 + HoldCount;
            int nearest = -1;
            double best = double.PositiveInfinity;
            for (int i = 0; i < s.Enemies.Length; i++)
            {
                if (!s.Enemies[i].Alive)
                {
                    continue;
                }

                double distance = (s.Enemies[i].Pos - s.PlayerPos).LengthSquared;
                if (distance < best)
                {
                    best = distance;
                    nearest = i;
                }
            }

            if (nearest < 0)
            {
                _moveDirs[slot] = Vec2.Zero;
                _moveDirs[slot + 1] = Vec2.Zero;
                return;
            }

            Vec2 away = s.PlayerPos - s.Enemies[nearest].Pos;
            double length = away.Length;
            Vec2 bearing = length > 1e-9 ? away / length : Vec2.UnitX;
            Vec2 tangent = bearing.Perpendicular();

            // 半径误差换成速度（世界单位/秒），再折算成「玩家速度的比例」。
            double speed = s.PlayerSpeed > 1e-9 ? s.PlayerSpeed : 1.0;
            double radial = (length - s.BladeRadius) * StationGain;
            Vec2 radialPart = bearing * (radial / speed);
            Vec2 tangentPart = tangent * StationTangentScale;

            Vec2 plus = radialPart + tangentPart;
            Vec2 minus = radialPart - tangentPart;
            _moveDirs[slot] = plus.Length > 1.0 ? plus.Normalized() : plus;
            _moveDirs[slot + 1] = minus.Length > 1.0 ? minus.Normalized() : minus;
        }

        void BuildPath(BattleSnapshot s, Vec2 move, int moveIndex, int enemyCount)
        {
            int offset = moveIndex * (HorizonTicks + 1);

            Vec2 position = s.PlayerPos;
            Vec2 velocity = s.PlayerVel;
            _pathPositions[offset] = position;
            _pathVelocities[offset] = velocity;

            double accelerationStep = Acceleration * SimConstants.Dt;
            for (int k = 1; k <= HorizonTicks; k++)
            {
                velocity = Motion.MoveTowards(velocity, move * s.PlayerSpeed, accelerationStep);
                position = s.Grid.ResolveCircle(position + velocity * SimConstants.Dt, s.PlayerRadius);
                _pathVelocities[offset + k] = velocity;
                _pathPositions[offset + k] = position;
            }

            double damage = 0.0;

            for (int i = 0; i < s.Projectiles.Length; i++)
            {
                SnapshotProjectile shot = s.Projectiles[i];
                _shot.Pos = shot.Pos;
                _shot.Vel = shot.Vel;
                _shot.Radius = Projectile.DefaultRadius;
                _shot.Damage = EnemyTuning.NailDamage;
                _shot.TtlTicks = Projectile.DefaultTtlTicks;
                _shot.Alive = true;

                double reachSquared = Math.Pow(_shot.Radius + s.PlayerRadius, 2);
                for (int k = 1; k <= HorizonTicks && _shot.Alive; k++)
                {
                    _shot.Step(s.Grid);
                    if (_shot.Alive && _shot.Pos.DistanceSquaredTo(_pathPositions[offset + k]) <= reachSquared)
                    {
                        damage += _shot.Damage;
                        _shot.Alive = false;
                    }
                }
            }

            for (int i = 0; i < s.Zones.Length; i++)
            {
                SnapshotZone zone = s.Zones[i];
                int k = Math.Max(1, zone.WarnTicksLeft);
                if (k > HorizonTicks)
                {
                    continue;
                }

                _zone.X = zone.X;
                _zone.Y = zone.Y;
                _zone.Width = zone.Width;
                _zone.Height = zone.Height;
                _zone.Damage = EnemyTuning.FallingZoneDamage;
                if (_zone.ContainsCircle(_pathPositions[offset + k], s.PlayerRadius))
                {
                    damage += _zone.Damage;
                }
            }

            for (int e = 0; e < enemyCount; e++)
            {
                EnemyPrediction prediction = _predictions[e];
                SnapshotEnemy enemy = prediction.Enemy;
                Vec2[] enemyPositions = prediction.Positions;

                bool tailLight = enemy.Kind == EnemyKind.TailLight;
                bool strikerDashing = enemy.Kind == EnemyKind.Striker && enemy.State == StrikerDash;
                bool dashHit = false;
                double reachSquared = Math.Pow(s.PlayerRadius + enemy.Radius, 2);

                for (int k = 1; k <= HorizonTicks; k++)
                {
                    if (_pathPositions[offset + k].DistanceSquaredTo(enemyPositions[k]) > reachSquared)
                    {
                        continue;
                    }

                    if (tailLight)
                    {
                        damage += EnemyTuning.TailLightContactDps * SimConstants.Dt;
                    }

                    if (strikerDashing && !dashHit)
                    {
                        damage += EnemyTuning.StrikerDashImpact;
                        dashHit = true;
                    }
                }
            }

            _pathDamage[moveIndex] = damage;
        }

        /// <summary>
        /// 覆盖代理 Ring：以刀环半径为峰、宽度 0.65 的三角核，逐 tick 积分。
        /// 输出代理 V：可命中环带内的相对角速度按 DamageRules.HitsPerSecond 积分。
        /// </summary>
        void Evaluate(BattleSnapshot s, int spinSign, int moveIndex, int candidateId, int enemyCount)
        {
            int offset = moveIndex * (HorizonTicks + 1);
            double bladeRadius = s.BladeRadius;
            double omega = s.Omega;
            int bladeCount = s.BladeCount;

            double ring = 0.0;
            double v = 0.0;
            double endError = double.PositiveInfinity;

            for (int k = StrideTicks; k <= HorizonTicks; k += StrideTicks)
            {
                Vec2 playerPosition = _pathPositions[offset + k];
                Vec2 playerVelocity = _pathVelocities[offset + k];

                for (int e = 0; e < enemyCount; e++)
                {
                    EnemyPrediction prediction = _predictions[e];
                    Vec2 delta = prediction.Positions[k] - playerPosition;
                    double distance = delta.Length;
                    double ringOffset = Math.Abs(distance - bladeRadius);

                    ring += Math.Max(0.0, 1.0 - ringOffset / RingKernelWidth) * StepWeight;

                    if (distance < 1e-9 || distance > bladeRadius + prediction.Enemy.Radius + BladeRing.BladeHalfWidth)
                    {
                        continue;
                    }

                    Vec2 tangent = delta.Perpendicular() / distance;
                    double relative = spinSign * omega + (playerVelocity - prediction.Velocity).Dot(tangent) / bladeRadius;

                    // 命中机会 = 刀数 × 转速，与站位无关；相对速度只改伤害倍率（与模拟层同一套规则）。
                    v += DamageRules.HitsPerSecond(bladeCount, omega)
                         * DamageRules.RelativeDamageFactor(relative, omega) * StepWeight;

                    // 前瞻结束那一刻离环有多远：只算最近的一次接触，让她真的"停在环上"。
                    if (k == HorizonTicks)
                    {
                        Vec2 endDelta = prediction.Positions[HorizonTicks] - _pathPositions[offset + HorizonTicks];
                        double endDistance = endDelta.Length;
                        if (endDistance < bladeRadius + EndReachMargin)
                        {
                            double error = Math.Abs(endDistance - bladeRadius);
                            if (error < endError)
                            {
                                endError = error;
                            }
                        }
                    }
                }
            }

            _candidateRing[candidateId] = ring;
            _candidateV[candidateId] = v;
            _candidateEndError[candidateId] = double.IsPositiveInfinity(endError) ? RingKernelWidth : endError;
        }

        EnemyPrediction AcquirePrediction(int index)
        {
            while (_predictions.Count <= index)
            {
                _predictions.Add(new EnemyPrediction());
            }

            return _predictions[index];
        }

        static Vec2[] BuildDirections()
        {
            var dirs = new Vec2[DirectionCount];
            for (int i = 0; i < DirectionCount; i++)
            {
                // 与原型相同的表达式顺序，保证方向表逐位一致。
                dirs[i] = Vec2.FromAngle(i * Math.PI * 2.0 / DirectionCount);
            }

            return dirs;
        }

        /// <summary>一只敌人的预测轨迹。对象按索引复用，Position 缓冲只分配一次。</summary>
        sealed class EnemyPrediction
        {
            public SnapshotEnemy Enemy;
            public Vec2 Velocity;
            public readonly Vec2[] Positions = new Vec2[HorizonTicks + 1];
        }

        readonly struct HistoryPoint
        {
            public readonly Vec2 Pos;
            public readonly int Tick;

            public HistoryPoint(Vec2 pos, int tick)
            {
                Pos = pos;
                Tick = tick;
            }
        }
    }
}
