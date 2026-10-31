# 两种远程小怪身体接触：源码核查

> 2026-10-31；仅核查仓库一手设计与源码。本文区分旧定义、新确认规则与建议，不是正式规格或实现验收；不重述既有 Fairy 算法/批量实验报告。本次只新增本文件。

## 1. 用户本轮确认（新增世界规则）

对象是 `shooter`（钉枪）和 `dropper`（坠物）；命名对应见[原模型](<../../prototype/enemy-behavior/enemy-behavior.prototype.html#L42-L46>)。两者身体触碰伊埃斯后：**伊埃斯扣血、朝边上震开，随后无敌 0.5–1 秒；敌人继续存活**。

**用户进一步确认：这个受伤机制可以全局复用。**伊埃斯所有有效受击统一经历“扣血 → 震开 → 短暂无敌”，覆盖近/远身体、弹丸、落区，手动与 Fairy 代行共用。各来源的伤害值和敌人后续行为独立：两远程不因接触自毁，近战的既有自毁等行为仍按其来源规则处理。这些是本轮用户确认，不是以下旧源码已实现的事实；具体伤害量、震开距离及保护区间内的取值仍属实验参数。

## 2. 逐项核查：旧定义不能替代新规则

| 核查项 | 一手来源与结论 |
| --- | --- |
| 钉枪的职责 | [设计 §5](<../design/game-design.md#L93-L103>)只定站定射击；[待确认 AI 规格](<../specs/ai.md#L64-L73>)定站位、瞄准、贴近撤退。C# 的[撤退](<../../prototype/battlesim/src/Enemies/NailGun.cs#L62-L87>)和[开火](<../../prototype/battlesim/src/Enemies/NailGun.cs#L146-L166>)也没有“身体碰撞即伤害/震开”的定义。撤退行为不等于身体碰撞规则。 |
| 坠物的空域 | [AI 规格](<../specs/ai.md#L59-L69>)规定飞行、空域分离；C# 的[`Flying`](<../../prototype/battlesim/src/Enemies/Dropper.cs#L8-L11>)及[移动实现](<../../prototype/battlesim/src/Enemies/Enemy.cs#L70-L82>)只让它无视地面墙、受地图边界约束。[原 JS 移动](<../../prototype/enemy-behavior/enemy-behavior.prototype.html#L85-L87>)按空/地邻居软避让，[坠物接近](<../../prototype/enemy-behavior/enemy-behavior.prototype.html#L113-L117>)也走空中分支；**这些都不能推出“与伊埃斯免身体碰撞”**。旧规格没有高度碰撞层合同。 |
| 身体碰撞与存活 | C# [`ApplyContactDamage`](<../../prototype/battlesim/src/Sim/BattleSimulation.cs#L359-L394>)虽先检查圆重叠，但只有尾灯与冲刺撞针伤害分支，没有钉枪/坠物。原 JS 的[敌人更新与步进](<../../prototype/enemy-behavior/enemy-behavior.prototype.html#L102-L130>)只有短打、突刺、弹丸、落区伤害，没有通用远程身体事件；因此“碰后存活”不能用来证明旧版已经会碰伤。两远程不自毁是本轮明确合同，不应套用近战自毁。 |
| 伤害数值 | C# [伤害常量](<../../prototype/battlesim/src/Combat/DamageRules.cs#L22-L32>)的钉枪 **8**、坠物 **12**分别属于弹丸、落区；原 JS [弹丸/落区结算](<../../prototype/enemy-behavior/enemy-behavior.prototype.html#L129-L130>)为 **9/14**。它们均不是远程身体伤害。[设计未决项](<../design/game-design.md#L251-L256>)仍列敌人 HP/伤害，不能因常量存在就称为正式平衡规格。 |
| 受伤无敌 | C# [`Player.TakeDamage`](<../../prototype/battlesim/src/Combat/Player.cs#L41-L51>)与原 JS [`damage`](<../../prototype/enemy-behavior/enemy-behavior.prototype.html#L96>)只扣血/计数，没有保护计时。[美术的 0.1 秒闪白](<../specs/art-assets.md#L99-L101>)只是表现，不是无敌。 |
| 震开 | 上述两处伤害函数不改位置或速度。C# [同类分离](<../../prototype/battlesim/src/Sim/BattleSimulation.cs#L257-L302>)仅处理敌人与敌人，JS [局部软避让](<../../prototype/enemy-behavior/enemy-behavior.prototype.html#L85-L87>)只是方向选择；均不是伊埃斯受伤震开。 |

**规格边界：**[设计](<../design/game-design.md#L84>)要求 Fairy 不获得额外无敌；[旧 AI 规格](<../specs/ai.md#L183-L185>)更直写“不允许无敌帧”，与新增受伤保护存在字面冲突。不能宣称旧规格已支持；本轮应按用户新确认的**伊埃斯世界规则、手动与代行一致**理解，不是给 Fairy 单独的保护。该 AI 规格本身仍[待确认](<../specs/ai.md#L1-L6>)，本次不修改正式文档或 C#。

## 3. 可复用能力与最小缺口

- **几何直接复用：**原 JS [`circleTime`](<../../prototype/enemy-behavior/enemy-behavior.prototype.html#L53>)已有运动段/圆首次相交时间；[`firstWall` / `clipped`](<../../prototype/enemy-behavior/enemy-behavior.prototype.html#L55-L63>)已有带半径的墙与地图边界截断。无需新增物理系统或改导航。
- **规则只留一份：**[共享构建器](<../../prototype/enemy-behavior/build-fairy-response.prototype.cjs>)已有 `resolveCombat` 接触/刀刃结算入口与 `observe/fromObservation`；[规划器预测](<../../prototype/enemy-behavior/fairy-planner.prototype.js#L110-L128>)逐 tick 调用同一 `lab.step`。统一受伤反应应收口到共享世界的 `damage(s,amount,source,direction)`，调用方仅提供来源伤害值和向外方向，真实执行和预测共用。本文不评价本轮新增实现，结果由主任务另行记录。
- **相对原模型的必要增量：**远程身体事件及其独立伤害参数、全来源共用的受伤保护期限与震开/墙裁剪，以及保护状态的公开观察和重建。两远程身体事件本身不改敌人 HP、不增加自毁数，不能把“继续存活”理解为不会被飞刀击杀（既有[存活条件](<../../prototype/battlesim/src/Enemies/Enemy.cs#L40-L57>)仍是 HP）；各来源的敌方后续行为独立于伊埃斯受伤反应。原模型[玩家创建](<../../prototype/enemy-behavior/enemy-behavior.prototype.html#L146>)和[伤害入口](<../../prototype/enemy-behavior/enemy-behavior.prototype.html#L96>)可作为缺口基线。

## 4. 最小参数建议（实验，不是正式定值）

| 参数/语义 | 最小建议与依据 |
| --- | --- |
| 身体相交范围 | 沿用 `player.r + enemy.r` 的相对运动扫掠；原 JS 半径为 **0.36 + 0.33**，见[现有参数](<../../prototype/enemy-behavior/enemy-behavior.prototype.html#L42>)，不混入 [C# 玩家半径 0.4](<../../prototype/battlesim/src/Combat/Player.cs#L8>)。坠物也参与是本轮新确认规则，不从“空中”推定豁免。 |
| 来源伤害 | 各来源保留各自伤害值；两远程身体伤害试验 **4 HP**。统一的是受伤反应，不是伤害数值；用户尚未定量，不拿弹丸/落区伤害冒充身体规格。 |
| 保护期限 | 所有有效受击统一触发单一玩家期限，实验参数 `hurtInvulnerability` 为 **45 tick = 0.75 秒**，处于用户给定的 0.5–1 秒内；固定 60Hz 来自[模型步长](<../../prototype/enemy-behavior/enemy-behavior.prototype.html#L42>)。近/远身体、弹丸、落区共用 `damage` 门控；保护内不扣血/震开/续期，期限保存在 `player.invulnerableUntil` 并随观察重建保留。手动与 Fairy 一致。 |
| 震开 | 所有有效受击共用实验参数 `hurtKnockback`，位移上限 **0.8 单位**；由伤害来源提供向外方向，身体接触使用**接触时刻**远离敌人的法线，方向退化时用确定性回退。建议 tick 末一次性用现有 [`clipped`](<../../prototype/enemy-behavior/enemy-behavior.prototype.html#L55-L63>) 按玩家体积截墙并将速度归零，实际位移可缩短；不穿墙/边界，不做连续冲量或额外刀刃扫掠。此时序与位移都是实验建议，不新增导航/刚体机制，不改正式设计。 |

最小验证围绕统一受伤反应：近/远身体、弹丸、落区各自有效受击均触发扣血/震开/保护；两远程不因接触自毁，各来源敌方后续行为保持独立；保护内多来源不重复伤害、期限边界后可再受击、墙边震开不穿墙、观察重建后的真实/预测结算一致。**本审计没有运行这些验证，也不把建议参数或主任务后续测试结果当成正式规格。**

未编辑输出窗口、导航、原敌人页、正式 C# 或任何其他文件；未提交、清理工作区，未复制私人配置。
