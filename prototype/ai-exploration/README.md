# Fairy 代行算法探索原型

隔离的 .NET 8 算法实验与离线逻辑回放。回答三个问题：短时前瞻是否改善真实战斗表现、粗筛精评如何交换工作量与质量、相对角速度代理能否代表有限窗口命中。方法、实测与限制见[实验报告](../../docs/research/ai-exploration-findings.md)，外部依据见[一手资料核查](../../docs/research/ai-navigation-planning-sources.md)。

## 打开与复跑

直接双击[逻辑原型](fairy-algorithms.prototype.html)。所有记录已嵌入同一个文件，可选择场景、策略，逐帧/逐决策查看世界状态与候选筛选；决策沙盘只重排当前已精评候选，不改写回放。

从仓库根目录重新生成实验结果：

```powershell
dotnet run --project prototype/ai-exploration -c Release -- --out prototype/ai-exploration/results
# 本机 .NET 8 SDK 的等价入口：
& "$env:LOCALAPPDATA\dotnet-sdk8\dotnet.exe" run --project prototype/ai-exploration -c Release -- --out prototype/ai-exploration/results
# 可追加 --no-write，只打印汇总
```

重新嵌入离线页面，需要 Python 3，仅用标准库：

```powershell
python prototype/ai-exploration/build_demo.py
```

## 实验接缝与控制变量

- [项目引用](AiExploration.csproj)复用相邻[模拟器项目](../battlesim/BattleSim.csproj)。实际命中、伤害、运动、敌人状态机及逐 tick 哈希均由原[战斗模拟器](../battlesim/src/Sim/BattleSimulation.cs)执行。`Trace` 是只读观察回调。
- [场景输入](ScenarioInputs.cs)只使用公开 `Scenario` 位置、地图与刷怪序列；钉枪与坠物按真实状态机生成弹丸和落区。生成后的完整输入见[场景清单](results/scenario-manifest.json)。
- 每组 241 tick：tick 0 末接管，tick 1–240 共 240 Fairy tick；首次规划 tick 6，总共 40 次决策。前缀控制由原模拟器持有，所有组一致。
- 固定 16 等角方向、零输入、当前输入共 18 个移动候选，分别尝试两个旋转方向，共 36 个动作。重复方向保留，稳定 ID 决胜。
- 所有策略仅输出期望 `SpinSign`，`RequestReverse=false`，以保持一致的持有指令语义。

### 策略

| 标识 | 作用 |
| --- | --- |
| `greedy` | 同候选集合上的 0.25 秒短视加权对照，端点覆盖/输出代理与区间危险加权；不是原 `BaselinePlanner` 的逐字复刻。 |
| `lex-full` | 1 秒、60Hz 轨迹。P0 最小预测伤害，有零伤即先选零伤；P1 保留覆盖最优值的 85%；P2 最大化可命中环带内的相对角速度积分；最后连续性与稳定 ID。 |
| `lex-coarse4` | 同候选、同精评模型；所有动作的运动和已知危险仍是 60Hz，仅覆盖/输出代理每 6 tick 粗评，再选前四个动作精评。属于有损 top-k。 |

规划器位于[探索实现](ExplorationPlanner.cs)。移动轨迹复用原 `Motion.MoveTowards`、`Grid.ResolveCircle`；弹丸复用 `Projectile.Step`，落区复用 `HazardZone.ContainsCircle`。`damage` 单位为原始 HP。覆盖分数是以环半径为峰、宽度 0.65 的三角距离核积分，不等于模拟器 `[0.85r,1.15r]` 的占环统计。

敌速度从相邻可见快照位置差分估计，首见零速度；静止状态按可见状态置零，之后恒速并考虑墙。未来状态切换、追踪改变、分离与尚未出现的危险没有预测。预测不模拟击杀，可能过度保守；快照缺弹丸 TTL 等信息时使用共享默认值，可能过预测。**更多搜索不能弥补预测模型缺失。**

### 独立探针

- [相位探针](PhaseProbe.cs)：216 个距离/敌速/初始相位组合，每组十个动作，观察 **1 秒**几何命中。复用刀环、每刀冷却和伤害常量；目标保持存活、敌人匀速、开阔无墙。它与真实四秒战斗分开统计。
- [导航探针](NavigationProbe.cs)：读取原混战地图，比对实际流场后继与加权最短路；另观测多次 A* 和单源树回溯。原 A* 含平滑，实验树不平滑，二者计时不是等价输出的加速比。

## 复现和计时

每场景每策略预热 3 次，再独立计时 7 次；导出回放另跑一次，并与七次测量逐 tick 比较哈希。相同种子不是唯一条件，规划器历史、候选顺序与模型参数同样固定。

哈希沿用原模拟器的 1e-3 量化，不覆盖所有隐藏状态；同机一致不等于跨平台位级证明。耗时不回流进控制逻辑。

性能均为本机 .NET Release：分别报告规划耗时和整个 `BattleSimulation.Run` 耗时。后者含哈希/指标，不含构造与导出采样；两者都不是 Android 整帧 AI 预算证明。分位数采用最近秩，七个整场样本的尾部分位数仅供观察。

`work` 分别统计移动步、危险步、接触检查、覆盖/输出检查、精评与粗评候选；某一项减少，不代表总工作量或墙钟必然减少。

## 输出与源码

- [汇总](results/summary.json)：环境、配置、场景、十五组真实结果、确定性、计时、工作量、比较与导航探针。
- [相位扫描](results/phase-probe.json)：配置、216 组输入、各动作代理分与几何命中。
- 每组轨迹：`<场景>.<策略>.trace.json`，包含玩家、当前命令、敌人、弹丸、落区和 tick 哈希。
- 每组决策：`<场景>.<策略>.decisions.json`，包含 36 个候选的 `D/R/V`、连续性、精评标记、淘汰阶段、选中动作与预测路径。
- [执行入口](Program.cs)、[页面模板](demo.template.html)、[页面生成脚本](build_demo.py)。页面的纯筛选函数只重排记录分量，不实现另一份物理或伤害规则。

本轮结果只回答这些小场景的问题；正式玩法仍以[玩法设计](../../docs/design/game-design.md)与[待确认算法规格](../../docs/specs/ai.md)为准。研究结论经人工观察确认后，再决定哪些规则进入正式实现。
