using System.Diagnostics;
using BattleSim.Combat;
using BattleSim.Core;
using BattleSim.Enemies;
using BattleSim.Fairy;

namespace AiExploration;

public sealed class WorkCounts
{
    public long MovementSteps { get; set; }
    public long HazardSteps { get; set; }
    public long ContactChecks { get; set; }
    public long RingVChecks { get; set; }
    public long FineCandidates { get; set; }
    public long CoarseCandidates { get; set; }
}
public sealed class CandidateRow
{
    public int Id { get; set; }
    public double[] Move { get; set; } = [];
    public int SpinSign { get; set; }
    public double Damage { get; set; }
    public double Ring { get; set; }
    public double V { get; set; }
    public double Continuity { get; set; }
    public bool FineEvaluated { get; set; }
    public bool Selected { get; set; }
    public string Stage { get; set; } = "eligible";
    public double GreedyScore { get; set; }
    public double CoarseRing { get; set; }
    public double CoarseV { get; set; }
}
public sealed record DecisionRow(int Tick, int SelectedId, double P0BestDamage, double P1BestRing,
    double P1Threshold, double[][] PredictedPath, CandidateRow[] Candidates);

public sealed class ExplorationPlanner(string strategy, bool capture) : IPlanner
{
    private static readonly double Acceleration = new Player().Acceleration;
    private readonly Dictionary<int,(Vec2 Pos,int Tick)> _history = new();
    private Vec2 _previousMove;
    public readonly List<double> Times = [];
    public readonly List<DecisionRow> Decisions = [];
    public readonly WorkCounts Work = new();
    private sealed record Prediction(SnapshotEnemy Enemy, Vec2 Velocity, Vec2[] Positions);
    private sealed record Path(Vec2[] Positions, Vec2[] Velocities, double Damage);

    public Command Decide(BattleSnapshot s)
    {
        long started = Stopwatch.GetTimestamp();
        int horizon = strategy == "greedy" ? 15 : 60;
        var enemies = PredictEnemies(s,horizon);
        var dirs = Enumerable.Range(0,16).Select(i=>Vec2.FromAngle(i*Math.PI*2/16))
            .Concat(new[]{Vec2.Zero,s.PlayerMoveDir}).ToArray();
        var paths = dirs.Select(d=>BuildPath(s,d,horizon,enemies)).ToArray();
        var candidates = new CandidateRow[36];
        for(int m=0;m<18;m++) for(int signIndex=0;signIndex<2;signIndex++)
        {
            int id=m*2+signIndex, sign=signIndex==0?1:-1;
            candidates[id] = new CandidateRow { Id=id,Move=[dirs[m].X,dirs[m].Y],SpinSign=sign,Damage=paths[m].Damage,
                Continuity=dirs[m].Dot(_previousMove)+(sign==s.SpinSign?0.05:0) };
        }
        CandidateRow chosen;
        double bestDamage, bestRing;
        if(strategy=="greedy")
        {
            foreach(var c in candidates)
            {
                Score(s,c,paths[c.Id/2],enemies,horizon,horizon,true);
                c.FineEvaluated=true;
                double danger=0;
                var pos=paths[c.Id/2].Positions[horizon];
                foreach(var e in enemies)
                {
                    double d=pos.DistanceTo(e.Positions[horizon]);
                    danger += d<1 ? (1-d)*40 : 0.4/(d-1+0.3);
                }
                c.GreedyScore=-40*c.Damage-danger+3*c.Ring+0.6*c.V+0.35*c.Continuity;
            }
            chosen=candidates.OrderByDescending(c=>c.GreedyScore).ThenBy(c=>c.Id).First();
            bestDamage=candidates.Min(c=>c.Damage); bestRing=candidates.Max(c=>c.Ring);
        }
        else
        {
            var pool=candidates.AsEnumerable();
            if(strategy=="lex-coarse4")
            {
                foreach(var c in candidates)
                {
                    Score(s,c,paths[c.Id/2],enemies,horizon,6,false);
                    c.CoarseRing=c.Ring; c.CoarseV=c.V;
                }
                Work.CoarseCandidates+=36;
                var coarseOrder = LayerOrder(candidates);
                var fine=coarseOrder.Take(4).ToArray();
                foreach(var c in candidates) c.Stage="coarse-pruned";
                foreach(var c in fine) { c.Stage="eligible"; c.FineEvaluated=true; Score(s,c,paths[c.Id/2],enemies,horizon,1,false); }
                Work.FineCandidates+=fine.Length;
                pool=fine;
            }
            else
            {
                foreach(var c in candidates) { c.FineEvaluated=true; Score(s,c,paths[c.Id/2],enemies,horizon,1,false); }
                Work.FineCandidates+=36;
            }
            var available=pool.ToArray();
            bestDamage=available.Min(c=>c.Damage);
            var p0=available.Where(c=>c.Damage<=bestDamage+1e-9).ToArray();
            bestRing=p0.Max(c=>c.Ring);
            var p1=p0.Where(c=>c.Ring+1e-12>=bestRing*0.85).ToArray();
            double bestV=p1.Max(c=>c.V);
            var p2=p1.Where(c=>c.V>=bestV-1e-9).ToArray();
            chosen=p2.OrderByDescending(c=>c.Continuity).ThenBy(c=>c.Id).First();
            foreach(var c in available)
                c.Stage=c.Damage>bestDamage+1e-9?"p0-filtered":c.Ring+1e-12<bestRing*0.85?"p1-filtered":c.V<bestV-1e-9?"p2-filtered":"eligible";
        }
        chosen.Selected=true; chosen.Stage="selected";
        _previousMove=new Vec2(chosen.Move[0],chosen.Move[1]);
        Times.Add(Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        if(capture)
        {
            var path=paths[chosen.Id/2].Positions;
            var points=Enumerable.Range(0,horizon+1).Where(i=>i%6==0||i==horizon)
                .Select(i=>new[]{path[i].X,path[i].Y}).ToArray();
            Decisions.Add(new DecisionRow(s.Tick,chosen.Id,bestDamage,bestRing,bestRing*0.85,points,candidates));
        }
        return new Command(_previousMove,chosen.SpinSign,false);
    }

    private static CandidateRow[] LayerOrder(CandidateRow[] candidates)
    {
        // Repeatedly take the lexicographic winner, recomputing the 85% band on the remaining pool.
        var remaining=candidates.ToList(); var ordered=new List<CandidateRow>();
        while(remaining.Count>0)
        {
            double d=remaining.Min(c=>c.Damage);
            var p0=remaining.Where(c=>c.Damage<=d+1e-9).ToArray();
            double r=p0.Max(c=>c.Ring);
            var c=p0.Where(c=>c.Ring+1e-12>=r*0.85).OrderByDescending(c=>c.V).ThenByDescending(c=>c.Continuity).ThenBy(c=>c.Id).First();
            ordered.Add(c); remaining.Remove(c);
        }
        return ordered.ToArray();
    }

    private Prediction[] PredictEnemies(BattleSnapshot s,int horizon)
    {
        var result=new List<Prediction>();
        foreach(var e in s.Enemies.Where(e=>e.Alive))
        {
            Vec2 velocity=Vec2.Zero;
            if(_history.TryGetValue(e.Id,out var old)&&s.Tick>old.Tick)
                velocity=(e.Pos-old.Pos)/((s.Tick-old.Tick)*SimConstants.Dt);
            bool hold=(e.Kind==EnemyKind.Striker&&(e.State==1||e.State==3)) ||
                      (e.Kind==EnemyKind.NailGun&&(e.State==1||e.State==2||e.State==3)) ||
                      (e.Kind==EnemyKind.Dropper&&(e.State==1||e.State==2));
            if(hold) velocity=Vec2.Zero;
            double maxSpeed=e.Kind==EnemyKind.Striker&&e.State==2?Striker.DashSpeedConst:e.MoveSpeed;
            if(velocity.Length>maxSpeed&&maxSpeed>0) velocity=velocity.Normalized()*maxSpeed;
            var positions=new Vec2[horizon+1]; positions[0]=e.Pos;
            for(int k=1;k<=horizon;k++)
            {
                var next=positions[k-1]+velocity*SimConstants.Dt;
                positions[k]=e.Flying?new Vec2(SimMath.Clamp(next.X,0.5,s.Grid.Width-0.5),SimMath.Clamp(next.Y,0.5,s.Grid.Height-0.5)):
                    s.Grid.ResolveCircle(next,e.Radius);
            }
            _history[e.Id]=(e.Pos,s.Tick);
            result.Add(new Prediction(e,velocity,positions));
        }
        return result.ToArray();
    }

    private Path BuildPath(BattleSnapshot s,Vec2 move,int horizon,Prediction[] enemies)
    {
        var pos=new Vec2[horizon+1];var vel=new Vec2[horizon+1]; pos[0]=s.PlayerPos;vel[0]=s.PlayerVel;
        for(int k=1;k<=horizon;k++)
        {
            vel[k]=Motion.MoveTowards(vel[k-1],move*s.PlayerSpeed,Acceleration*SimConstants.Dt);
            pos[k]=s.Grid.ResolveCircle(pos[k-1]+vel[k]*SimConstants.Dt,s.PlayerRadius);
            Work.MovementSteps++;
        }
        double damage=0;
        foreach(var shot in s.Projectiles)
        {
            var p=new Projectile{Pos=shot.Pos,Vel=shot.Vel};
            for(int k=1;k<=horizon&&p.Alive;k++)
            {
                p.Step(s.Grid); Work.HazardSteps++;
                if(p.Alive&&p.Pos.DistanceSquaredTo(pos[k])<=Math.Pow(p.Radius+s.PlayerRadius,2))
                { damage+=p.Damage;p.Alive=false; }
            }
        }
        foreach(var z in s.Zones)
        {
            int k=Math.Max(1,z.WarnTicksLeft);
            if(k>horizon)continue;
            var zone=new HazardZone{X=z.X,Y=z.Y,Width=z.Width,Height=z.Height};
            Work.HazardSteps++;
            if(zone.ContainsCircle(pos[k],s.PlayerRadius))damage+=zone.Damage;
        }
        foreach(var e in enemies)
        {
            bool dashHit=false;
            for(int k=1;k<=horizon;k++)
            {
                Work.ContactChecks++;
                if(pos[k].DistanceSquaredTo(e.Positions[k])>Math.Pow(s.PlayerRadius+e.Enemy.Radius,2))continue;
                if(e.Enemy.Kind==EnemyKind.TailLight)damage+=DamageRules.TailLightContactDps*SimConstants.Dt;
                if(e.Enemy.Kind==EnemyKind.Striker&&e.Enemy.State==2&&!dashHit)
                { damage+=DamageRules.StrikerDashImpact;dashHit=true; }
            }
        }
        return new Path(pos,vel,damage);
    }

    private void Score(BattleSnapshot s,CandidateRow c,Path path,Prediction[] enemies,int horizon,int stride,bool endpoint)
    {
        double ring=0,v=0;
        for(int k=endpoint?horizon:stride;k<=horizon;k+=stride)
        {
            double weight=endpoint?1:stride*SimConstants.Dt;
            foreach(var e in enemies)
            {
                Work.RingVChecks++;
                var delta=e.Positions[k]-path.Positions[k]; double d=delta.Length;
                ring+=Math.Max(0,1-Math.Abs(d-s.BladeRadius)/0.65)*weight;
                if(d<1e-9||Math.Abs(d-s.BladeRadius)>e.Enemy.Radius+BladeRing.TipRadius)continue;
                var tangent=delta.Perpendicular()/d;
                double relative=c.SpinSign*s.Omega+(path.Velocities[k]-e.Velocity).Dot(tangent)/s.BladeRadius;
                v+=DamageRules.HitsPerSecond(s.BladeCount,relative)*weight;
            }
        }
        c.Ring=ring;c.V=v;
    }
}
