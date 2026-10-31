using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using BattleSim.Metrics;
using BattleSim.Scenarios;
using BattleSim.Sim;
using AiExploration;

const int warmups=3, repeats=7;
string output="prototype/ai-exploration/results";
bool noWrite=args.Contains("--no-write");
for(int i=0;i<args.Length;i++) if(args[i]=="--out"&&i+1<args.Length)output=args[++i];
var json=new JsonSerializerOptions { PropertyNamingPolicy=JsonNamingPolicy.CamelCase,WriteIndented=true };
string projectRoot=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../.."));
string outputPath=Path.GetFullPath(output);
if(!noWrite&&!outputPath.StartsWith(projectRoot+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))
    throw new ArgumentException("Output must remain under prototype/ai-exploration/.");
if(!noWrite)Directory.CreateDirectory(outputPath);
void Save(string name,object value) { if(!noWrite) File.WriteAllText(Path.Combine(outputPath,name),JsonSerializer.Serialize(value,json)); }
var scenes=ScenarioInputs.All();
string[] strategies=["greedy","lex-full","lex-coarse4"];
Save("scenario-manifest.json",new{SchemaVersion=1,Scenes=scenes});
var summaries=new List<RunSummary>();
foreach(var scene in scenes)
{
    foreach(string strategy in strategies)
    {
        Console.WriteLine($"{scene.Id} / {strategy}: warming {warmups}, measuring {repeats}");
        for(int i=0;i<warmups;i++)Execute(scene,strategy,false);
        var measurements=new List<Execution>();
        for(int i=0;i<repeats;i++)measurements.Add(Execute(scene,strategy,false));
        var reference=measurements[0];
        var export=Execute(scene,strategy,true);
        var all=measurements.Append(export).ToArray();
        int? mismatch=null;
        foreach(var run in all)
        {
            int max=Math.Max(reference.Result.TickHashes.Length,run.Result.TickHashes.Length);
            for(int tick=0;tick<max;tick++)
                if(tick>=reference.Result.TickHashes.Length||tick>=run.Result.TickHashes.Length||reference.Result.TickHashes[tick]!=run.Result.TickHashes[tick])
                { mismatch=mismatch is null?tick:Math.Min(mismatch.Value,tick);break; }
        }
        string prefix=$"{scene.Id}.{strategy}";
        Save(prefix+".trace.json",new{SchemaVersion=1,ScenarioId=scene.Id,Strategy=strategy,Frames=export.Frames});
        Save(prefix+".decisions.json",new{SchemaVersion=1,ScenarioId=scene.Id,Strategy=strategy,Decisions=export.Planner.Decisions});
        var r=reference.Result;
        var summary=new RunSummary(scene.Id,strategy,scene.Seed,r.Ticks,r.Hits,r.Kills,r.DamageTaken,r.Survived,r.AvgEnemiesOnRing,
            r.FairyTicks,reference.Planner.Times.Count,
            new{Repeats=repeats,ExportRunAlsoCompared=true,AllTickHashesEqual=mismatch is null,HashChainTail=r.HashChainTail.ToString("x16"),FirstMismatchTick=mismatch},
            new{PlannerMs=Percentiles(measurements.SelectMany(m=>m.Planner.Times)),WholeSimMs=Percentiles(measurements.Select(m=>m.ElapsedMs))},
            reference.Planner.Work,prefix+".trace.json",prefix+".decisions.json",
            export.Frames.Count==0?0:export.Frames.Max(f=>f.Projectiles.Length),export.Frames.Count==0?0:export.Frames.Max(f=>f.Zones.Length));
        summaries.Add(summary);
        Console.WriteLine($"  hits={r.Hits}, damage={r.DamageTaken:F3}, ring={r.AvgEnemiesOnRing:F3}, deterministic={mismatch is null}");
    }
}
var comparisons=scenes.Select(scene=>
{
    var g=summaries.Single(r=>r.ScenarioId==scene.Id&&r.Strategy=="greedy");
    var f=summaries.Single(r=>r.ScenarioId==scene.Id&&r.Strategy=="lex-full");
    var c=summaries.Single(r=>r.ScenarioId==scene.Id&&r.Strategy=="lex-coarse4");
    return new{ScenarioId=scene.Id,FullVsGreedy=new{HitsDelta=f.Hits-g.Hits,DamageDelta=f.DamageTaken-g.DamageTaken},
        CoarseVsFull=new{HitsDelta=c.Hits-f.Hits,DamageDelta=c.DamageTaken-f.DamageTaken,RingDelta=c.AvgEnemiesOnRing-f.AvgEnemiesOnRing,
            RingVCheckReduction=1-c.Work.RingVChecks/(double)Math.Max(1,f.Work.RingVChecks),
            MovementStepRatio=c.Work.MovementSteps/(double)Math.Max(1,f.Work.MovementSteps)}};
}).ToArray();
var phase=PhaseProbe.Run();
Save("phase-probe.json",phase);
var navigation=NavigationProbe.Run();
Save("summary.json",new{SchemaVersion=1,Experiment="Fairy fixed-candidate greedy vs lexicographic trajectory probe",
    Environment=new{Runtime=RuntimeInformation.FrameworkDescription,OS=RuntimeInformation.OSDescription,Architecture=RuntimeInformation.ProcessArchitecture.ToString(),
        ProcessorCount=Environment.ProcessorCount,PerformanceScope="Desktop Release, not mobile; no cross-platform deterministic guarantee"},
    Configuration=new{WarmupsPerScenarioStrategy=warmups,MeasuredRepeats=repeats,ExportRunOutsideTiming=true,ScenarioTicks=241,FairyTicks=240,
        CandidateMoves=18,CandidateSigns=2,DecisionEveryTicks=6,HorizonTicks=60,CoarseStrideTicks=6,RefineTopK=4,P1Retention=0.85,
        DamageUnit="raw HP",PercentileMethod="nearest rank",WholeSimDefinition="wall clock around BattleSimulation.Run only; includes hash/metrics, excludes constructor and export capture",
        Strategies=strategies},
    Scenarios=scenes.Select(s=>new{ s.Id,s.Label,s.Description,s.Seed,s.Ticks,s.Width,s.Height,s.Walls }),Runs=summaries,Comparisons=comparisons,Navigation=navigation});
Console.WriteLine($"Done. {(noWrite?"--no-write":outputPath)}");

static object Percentiles(IEnumerable<double> input)
{
    var a=input.Order().ToArray();
    double Q(double p)=>a.Length==0?0:a[Math.Clamp((int)Math.Ceiling(a.Length*p)-1,0,a.Length-1)];
    return new{Count=a.Length,P50=Q(.50),P95=Q(.95),P99=Q(.99)};
}
static Execution Execute(SceneInput scene,string strategy,bool capture)
{
    var planner=new ExplorationPlanner(strategy,capture);
    var sim=new BattleSimulation(scene.Create(),scene.Seed,PolicyKind.Fairy,scene.Ticks,planner);
    var frames=new List<Frame>();
    if(capture)sim.Trace=(tick,w)=>
    {
        var p=w.Player;var command=sim.AppliedCommand;
        frames.Add(new Frame(tick,new{X=p.Pos.X,Y=p.Pos.Y,Vx=p.Vel.X,Vy=p.Vel.Y,p.Hp,p.SpinSign,Phase=p.Ring.Phase},
            new{X=command.Move.X,Y=command.Move.Y,command.SpinSign,command.RequestReverse},
            w.Enemies.Select(e=>(object)new{e.Id,Kind=e.Kind.ToString(),X=e.Pos.X,Y=e.Pos.Y,e.Hp,e.State}).ToArray(),
            w.Projectiles.Select(p=>(object)new{X=p.Pos.X,Y=p.Pos.Y,Vx=p.Vel.X,Vy=p.Vel.Y}).ToArray(),
            w.Zones.Select(z=>(object)new{z.X,z.Y,z.Width,z.Height,z.WarnTicksLeft}).ToArray(),sim.TickLog[tick].Hash.ToString("x16")));
    };
    long started=Stopwatch.GetTimestamp();var result=sim.Run();double elapsed=Stopwatch.GetElapsedTime(started).TotalMilliseconds;
    return new Execution(result,planner,frames,elapsed);
}

internal sealed record Frame(int Tick,object Player,object Command,object[] Enemies,object[] Projectiles,object[] Zones,string TickHash);
internal sealed record Execution(SimResult Result,ExplorationPlanner Planner,List<Frame> Frames,double ElapsedMs);
internal sealed record RunSummary(string ScenarioId,string Strategy,ulong Seed,int Ticks,int Hits,int Kills,double DamageTaken,bool Survived,
    double AvgEnemiesOnRing,int FairyTicks,int PlannerDecisionCount,object Determinism,object Timing,WorkCounts Work,string TraceFile,string DecisionsFile,
    int MaxVisibleProjectiles,int MaxVisibleZones);
