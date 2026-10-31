# BattleSim · 无头战斗模拟器（可复刻原型）

供「以骸寻路 + Fairy 代行」算法打磨用的纯 C# 无头模拟器。**不依赖 Unity、不使用 Unity 物理、不需要打开编辑器。**

- 需求来源：`docs/specs/ai.md`（导航 / 以骸状态机 / Fairy 规划 / 复刻协议）、`docs/design/game-design.md` §4、§5（伤害公式、敌人行为、玩家数值）。
- 本目录只放原型。它**不是**生产级 AI，也不是最终 Fairy 规划器。

## 一条命令跑通

```powershell
# 从仓库根目录（需要 .NET 8 SDK）
& "$env:LOCALAPPDATA\dotnet-sdk8\dotnet.exe" run --project prototype/battlesim -- --scenario prototype/battlesim/scenarios/mix_four.json --seed 7
```

跑完 scenarios/ 下全部场景：

```powershell
& "$env:LOCALAPPDATA\dotnet-sdk8\dotnet.exe" run --project prototype/battlesim
```

复刻自检（同场景同种子跑两遍，逐 tick 哈希比对，必须一致）：

```powershell
& "$env:LOCALAPPDATA\dotnet-sdk8\dotnet.exe" run --project prototype/battlesim -- --selftest
```

一次看两个策略的差异（同一场景，只换玩家指令来源）：

```powershell
& "$env:LOCALAPPDATA\dotnet-sdk8\dotnet.exe" run --project prototype/battlesim -- --scenario prototype/battlesim/scenarios/mix_four.json --policy scripted
```

### 参数

| 参数 | 说明 |
| --- | --- |
| `--scenario <file>` | 场景 JSON，可重复；缺省跑 `scenarios/` 下全部（按文件名排序） |
| `--seed <n>` | 覆盖场景里的 seed |
| `--policy scripted\|fairy` | 覆盖场景里的 policy（`fairy` 才会在满槽后触发代行） |
| `--ticks <n>` | 覆盖时长，只用于冒烟/性能测试 |
| `--out <dir>` | 结果目录，默认 `<项目目录>/results` |
| `--selftest` | 每个场景跑两遍并比对逐 tick 哈希链 |
| `--trace <n>` | 每 n tick 打印一行世界状态（调试叠层雏形） |
| `--no-write` | 只打印指标，不落盘 |

## 输出

- `results/<scenario>-<seed>.json`：汇总指标 + 哈希链尾值。
  `survived / hp / hpLost / hits / kills / damageDealt / damageTaken / avgEnemiesOnRing / avgDecisionMs / maxDecisionMs / fairyTakeovers / fairyTicks / enemiesSpawned / enemiesAlive / simElapsedMs / hashChainTail`。
- `results/<scenario>-<seed>.ticks.csv`：逐 tick 记录
  `tick, seed, playerCmdX/Y, playerSpin, playerReverse, fairyCmdX/Y, fairySpin, fairyActive, hash`。
  这就是复刻协议的 `{tick, seed, playerCmd, fairyCmd, enemyStatesHash}`；敌人状态已并入 hash 输入。

## 复刻约定（确定性）

- 1 单位 = 1 格 = 32px；固定步长 **1/60s**；AI 决策 **0.1s**（每 6 tick）；流场 **0.25s**（每 15 tick）。
- 全程整数 tick，模拟内**不读** `DateTime` / `Stopwatch`。耗时统计只用于指标，不回流进世界状态。
- 自研 xorshift64\* PRNG（`Prng`），种子来自 `--seed` 或场景文件；不用 `System.Random`。
- 集合只用数组 / `List`，不依赖哈希表遍历顺序。
- 每 tick 用 **FNV-1a 64** 串链：记录 `{tick, seed, playerCmd, fairyCmd}` + 玩家 / 敌人 / 弹丸 / 落区状态 + PRNG 状态，位置按 1e-3 量化后取位模式。
- 不用物理引擎：位置积分 + 圆 vs 网格墙自解，撞墙沿墙滑行。

## 手感约定（移动是平滑的，不是格子跳）

- 世界坐标是**连续浮点**，32px 网格只用于寻路（A\* / 流场），不做像素吸附。
- 移动走「速度插值」：每 tick 把速度朝目标速度逼近固定增量（球球大作战式俯视手感）——
  任意 360° 方向、松手会滑行、反向走弧线，速度上限不变。
  参数：玩家加速度 60 单位/秒²、以骸 20 单位/秒²（`Player.Acceleration` / `Enemy.Acceleration`）。
- 例外：撞针 `Windup`/`Recover` 由状态机强制 `Hold()`，速度立刻归零（规格要求「预警期间静止」）；`Dash` 是定速直线，绕过插值层。
- 像素只出现在两处：`32px = 1 单位` 的换算，以及坠物落区矩形（规格明确要求矩形像素对齐）。

## 工程结构

```
prototype/battlesim/
  BattleSim.csproj      net8.0，无外部包
  Program.cs            CLI / selftest / 结果落盘 / trace
  src/Core/             Vec2、SimConstants（步长）、Motion（速度插值）、Prng（xorshift64*）
  src/Nav/              Grid（可走图 + 圆 vs 网格）、AStar（8 邻接/√2/禁贴角/视线拉直）、
                        FlowField（Dijkstra 梯度）、PathFollower（路径跟随 + 16 方向贪心转向）
  src/Combat/           Player、BladeRing（+ 内圈占位）、Projectile、HazardZone、DamageRules
  src/Enemies/          Enemy 基类、TailLight、Striker、NailGun、Dropper、ChainBoss（占位）
  src/Fairy/            IPlanner、Command、BattleSnapshot、SnapshotFactory、BaselinePlanner（基线，非最终 P0–P3）
  src/Scenarios/        Scenario + ScenarioLoader（JSON）、ScriptedPlayerPolicy（对照组玩家脚本）
  src/Metrics/          TickHasher（FNV-1a 链）、MetricsCollector、SimResult、ResultWriter
  src/Sim/              WorldState（唯一可变权威）、BattleSimulation（固定步长推进）
  scenarios/            single_tail_light / mix_four / corridor_pressure
  results/              运行产物（不提交）
```

## 场景 JSON

```json
{
  "name": "mix_four",
  "seed": 7,
  "policy": "fairy",
  "script": "orbit",
  "durationTicks": 5400,
  "map": { "width": 40, "height": 24, "walls": [[x,y]], "wallRects": [[x,y,w,h]] },
  "player": { "pos": [20.5, 8.5], "script": "orbit", "initialCharge": 60 },
  "spawns": [ { "tick": 0, "type": "tail_light", "pos": [6.5, 4.5] } ]
}
```

- `type`：`tail_light` / `striker` / `nail_gun` / `dropper` / `chain_boss`。
- `policy`：`scripted`（纯脚本玩家，无代行）或 `fairy`（满槽后自动触发代行）。
- `script`：脚本玩家的走位策略，`flee`（直线逃跑）或 `orbit`（绕环风筝）。
- 加载时会校验玩家与刷新点可站（离墙太近直接报错），避免"跑起来才发现刷在墙里"。

## 已知边界

- Fairy 是**占位**：`BaselinePlanner` 只做 16 方向贪心 + 环带保持 + `|ω_rel|` 选向，没有候选点集合、没有 1s 前瞻、没有 P0–P3 字典序。接最终规划器只需替换 `src/Fairy/BaselinePlanner.cs`，模拟层不用动。
- 内圈飞刀、Boss（连环 / 骤雨）、道具与拾取均未实现，只留接口或占位。
