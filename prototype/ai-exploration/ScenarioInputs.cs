using BattleSim.Combat;
using BattleSim.Core;
using BattleSim.Enemies;
using BattleSim.Nav;
using BattleSim.Scenarios;

namespace AiExploration;

public sealed record SpawnInput(int Tick, string Kind, double X, double Y);
public sealed record SceneInput(string Id, string Label, string Description, ulong Seed, int Width, int Height,
    double[] Player, int[][] Walls, SpawnInput[] Spawns)
{
    public int Ticks => 241;
    public Scenario Create()
    {
        var grid = new Grid(Width, Height);
        foreach (int[] w in Walls) grid.SetWall(w[0], w[1]);
        var s = new Scenario { Name = Id, Seed = Seed, Policy = PolicyKind.Fairy, PlayerScript = PlayerScriptKind.Orbit,
            InitialCharge = 100, DurationTicks = Ticks, PlayerStart = new Vec2(Player[0], Player[1]), Grid = grid };
        if (!grid.CircleFits(s.PlayerStart, BattleSim.Combat.Player.RadiusConst))
            throw new InvalidOperationException($"{Id}: player start does not fit.");
        foreach (var p in Spawns.OrderBy(p => p.Tick))
        {
            var kind = Enum.Parse<EnemyKind>(p.Kind);
            var position = new Vec2(p.X, p.Y);
            if (p.Tick < 0 || !grid.InBounds(Grid.CellOf(p.X), Grid.CellOf(p.Y)) ||
                (kind != EnemyKind.Dropper && !grid.CircleFits(position, DamageRules.EnemyRadius)))
                throw new InvalidOperationException($"{Id}: invalid spawn at tick {p.Tick}.");
            s.Spawns.Add(new ScenarioSpawn { Tick = p.Tick, Kind = kind, Pos = position, Order = s.Spawns.Count });
        }
        return s;
    }
}

public static class ScenarioInputs
{
    public static SceneInput[] All()
    {
        var wall = Enumerable.Range(2,12).Select(y => new[]{12,y}).ToArray();
        var trapWalls = new List<int[]>();
        for (int y=0;y<16;y++) for(int x=0;x<24;x++)
            if (x<9 || x>14 || y<5 || y>10) trapWalls.Add(new[]{x,y});
        return new[] {
            new SceneInput("single-tail","单敌追踪","尾灯持续追击；玩家从静止出发。",1,24,16,new[]{10.0,8.0},Array.Empty<int[]>(),
                new[]{new SpawnInput(0,"TailLight",13,8)}),
            new SceneInput("near-wall","近墙转向","玩家靠近竖墙，两只尾灯依次入场。",2,24,16,new[]{11.1,8.0},wall,
                new[]{new SpawnInput(0,"TailLight",8,8),new SpawnInput(42,"TailLight",10,11)}),
            new SceneInput("nails-zones","弹丸与落区","钉枪与坠物按原状态机生成弹丸和落区。",3,24,16,new[]{11.0,8.0},Array.Empty<int[]>(),
                new[]{new SpawnInput(0,"NailGun",5,8),new SpawnInput(0,"NailGun",17,8),new SpawnInput(0,"Dropper",11,11)}),
            new SceneInput("mixed","混合敌人","四种敌人共同入场，并在第90 tick增加一只尾灯。",7,24,16,new[]{12.0,8.0},new[]{new[]{8,5},new[]{8,6},new[]{8,7}},
                new[]{new SpawnInput(0,"TailLight",15,8),new SpawnInput(0,"Striker",12,12),new SpawnInput(0,"NailGun",6,9),new SpawnInput(0,"Dropper",11,5),new SpawnInput(90,"TailLight",16,10)}),
            new SceneInput("trap","拥挤陷阱","六乘六封闭空间，尾灯、撞针与坠物共同施压。",11,24,16,new[]{12.0,8.0},trapWalls.ToArray(),
                new[]{new SpawnInput(0,"TailLight",11.3,8),new SpawnInput(0,"TailLight",12.7,8),new SpawnInput(0,"TailLight",12,7.3),new SpawnInput(0,"TailLight",12,8.7),
                    new SpawnInput(0,"TailLight",10,6),new SpawnInput(0,"TailLight",14,10),new SpawnInput(0,"Dropper",12,9.5),new SpawnInput(0,"Striker",10,9.5)})
        };
    }
}
