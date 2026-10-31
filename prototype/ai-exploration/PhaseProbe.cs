using BattleSim.Combat;
using BattleSim.Core;
using BattleSim.Enemies;

namespace AiExploration;

public static class PhaseProbe
{
    public static object Run()
    {
        var cases=new List<Case>();
        double[] distances=[1.4,1.7,2.0,2.3,2.6,3.0];
        double[] speeds=[-2.6,0,2.6];
        Vec2[] moves=[Vec2.Zero,Vec2.UnitY,-Vec2.UnitY,Vec2.UnitX,-Vec2.UnitX];
        foreach(double distance in distances) foreach(double speed in speeds) for(int phaseIndex=0;phaseIndex<12;phaseIndex++)
        {
            double phase=phaseIndex*(Math.PI/2)/12;
            var options=new List<Option>();
            for(int m=0;m<moves.Length;m++) foreach(int sign in new[]{1,-1})
            {
                var ring=new BladeRing();ring.Advance(1,phase/BladeRing.Omega);
                var enemy=new TailLight{Pos=new Vec2(distance,0),Hp=10000};
                Vec2 enemyVel=new(0,speed),player=Vec2.Zero,velocity=Vec2.Zero;
                int hits=0;double v=0;
                for(int tick=0;tick<60;tick++)
                {
                    velocity=Motion.MoveTowards(velocity,moves[m]*Player.SpeedConst,new Player().Acceleration*SimConstants.Dt);
                    player+=velocity*SimConstants.Dt;
                    ring.Advance(sign,SimConstants.Dt);enemy.TickBladeCooldowns();
                    var delta=enemy.Pos-player;double d=delta.Length;
                    if(d>1e-9&&Math.Abs(d-BladeRing.OuterRadius)<=enemy.Radius+BladeRing.TipRadius)
                    {
                        double rel=sign*BladeRing.Omega+(velocity-enemyVel).Dot(delta.Perpendicular()/d)/BladeRing.OuterRadius;
                        v+=DamageRules.HitsPerSecond(BladeRing.BladeCount,rel)*SimConstants.Dt;
                    }
                    for(int blade=0;blade<BladeRing.BladeCount;blade++)
                    {
                        if(enemy.BladeCooldown[blade]>0)continue;
                        double reach=enemy.Radius+BladeRing.TipRadius;
                        if(ring.TipAt(player,blade).DistanceSquaredTo(enemy.Pos)>reach*reach)continue;
                        hits++;enemy.BladeCooldown[blade]=DamageRules.BladePerTargetCooldownTicks;
                    }
                    // Same ordering as sim: blade intersection before enemy movement.
                    enemy.Pos+=enemyVel*SimConstants.Dt;
                }
                options.Add(new Option(options.Count,m,sign,v,hits));
            }
            var selected=options.OrderByDescending(o=>o.V).ThenBy(o=>o.Id).First();
            int best=options.Max(o=>o.Hits);
            cases.Add(new Case(distance,speed,phase,options.ToArray(),selected.Id,best,selected.Hits,best-selected.Hits));
        }
        return new { SchemaVersion=1,
            Configuration=new { HorizonTicks=60,Distances=distances,EnemySpeeds=speeds,PhaseCount=12,MoveVectors=moves.Select(m=>new[]{m.X,m.Y}),
                BladeRing.BladeCount,BladeRing.OuterRadius,BladeRing.Omega,DamageRules.BladePerTargetCooldownTicks,
                Notes="Independent open-space geometry probe; shared BladeRing and enemy cooldown helpers; no HP death or enemy AI; V integrated only in reachable annulus; stable option ID ties." },
            Summary=new { CaseCount=cases.Count, RegretCases=cases.Count(c=>c.Regret>0), MaxRegret=cases.Max(c=>c.Regret),
                MeanRegret=cases.Average(c=>c.Regret), ExactBestFraction=cases.Count(c=>c.Regret==0)/(double)cases.Count,
                ZeroVWithHitsCases=cases.Count(c=>c.Options.Any(o=>o.V==0&&o.Hits>0)),
                Interpretation="V is phase-blind and counts angular opportunity, not phase-specific finite-window hits. Dwell time and per-blade cooldown can reverse its ranking; zero regret here is not proof of equivalence." },
            Cases=cases };
    }
    private sealed record Option(int Id,int MoveId,int SpinSign,double V,int Hits);
    private sealed record Case(double Distance,double EnemySpeed,double InitialPhase,Option[] Options,int VSelectedId,int BestHits,int SelectedHits,int Regret);
}
