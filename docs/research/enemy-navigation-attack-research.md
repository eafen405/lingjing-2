# 以骸群体导航与攻击机制：一手来源核查及原型建议

> 日期：2026-10-30。状态：研究结论与候选设计，**不是已确认玩法规格，也不是实现验收**。
> 范围：俯视实时飞刀游戏的敌人追击、站位、攻击门槛、预警与承诺、四种战斗职责、组合攻击限制。先让手动操控下的敌人行为成立，再考虑 Fairy 如何理解同一套规则。
> 本文记录一手资料核查与候选规则；可操作模型及其交互观察见[敌人行为原型说明](<../../prototype/enemy-behavior/README.md>)。资料证据不等同于玩法验收，正式规格保持原状。

## 0. 阅读方法与结论先行

全文使用三种标签：

- **来源事实**：本次实际核读的一手文档或源码明确包含的行为；句末链接直达拥有该事实的来源，函数名用于进一步定位。
- **本项目建议**：针对本游戏提出的设计选择，包括从来源得到启发后的迁移；不代表来源游戏采用了它。
- **尚待手感验证**：不能由源码或纸面推理决定的阈值、节奏与体验判断。

**本项目建议，压缩为五条可映射原型的决定：**

1. 共目标、同通行规则的追兵共享反向导航场；少量独立站位用 A*。全局绕路、连续路径跟随、局部避让与最终碰撞各管一件事。
2. 将“想攻击”“攻击几何可行”“冷却就绪”“获准开始前摇”“实际出手/命中”拆开；进入距离阈值不等于立即造成伤害。
3. 前摇允许早段跟踪、末段明确锁定；锁定的方向/落点与实际攻击共享一份数据。锁定后不能由追击或软避让偷偷改写攻击轨迹。
4. 尾灯负责近身持续压力，撞针负责可读的穿线威胁，钉枪负责射线与站位压力，坠物负责延迟区域封锁；不能只是四种外观都向玩家中心冲。
5. 组合攻击许可是**本项目候选规则**，不是本次来源已证明的游戏事实。先有独立攻击基线，再比较有限并发许可；“最多两次动作”不证明玩家一定有路可躲。

这些建议的证据基础分别是 Mindustry 的共享场与行为/武器分层、DOOM 的攻击条件判断，以及 Godot 对导航职责的明确区分；**预警末段锁定、公平组合攻击许可与本游戏四职责分配，均由本项目提出**。[Mindustry 共享场][MP-field]、[Mindustry 攻击意愿][MC-weapons]、[Mindustry 武器门槛][MW-fire]、[DOOM 源码][D]、[Godot 官方导航文档][G]

## 1. 项目边界：设计意图、待确认规格、草稿实现不是同一回事

| 已读材料 | 本次采用的地位 |
| --- | --- |
| [仓库研发约定](<../../AGENTS.md#L5-L11>) | 复用优先、分清来源、按当前规模控制复杂度；不因研究引入第二套权威规则或通用框架。 |
| [游戏设计 §5](<../design/game-design.md#L89-L103>) | 项目设计意图：尾灯追击、撞针 0.6s 预警后直线突刺、钉枪站定发慢弹、坠物矩形落区；连环冲锋后硬直，骤雨技能有间隔且不叠放。敌人常态移速低于玩家是当前设计约束。 |
| [AI 规格状态](<../specs/ai.md#L1-L22>)、[导航与状态机细节](<../specs/ai.md#L52-L74>) | 文件明确标“待确认”。共享图/流场/A* 可以复用；其 0.25s 更新、5–7 单位环带、0.5s 瞄准等细项不是本次研究已经验证的玩法事实。 |
| [已有一手研究](<ai-navigation-planning-sources.md#L35-L49>)、[最小推荐](<ai-navigation-planning-sources.md#L101-L107>) | 复用共享图、按通行语义检查半径/禁角/拉直一致性、测量后再加算法复杂度的研究起点；本报告不重复 Fairy 轨迹优化研究，也不把旧报告中的代码观察当成本次新审查结果。 |
| [模拟器自述](<../../prototype/battlesim/README.md#L1-L6>)、[已知边界](<../../prototype/battlesim/README.md#L111-L114>) | 自述是原型、Fairy 为占位。本文没有重新审查全部 C# 实现，不能由现有代码反推正式玩法。 |

**本项目建议：**尾灯是否由接触伤害改成有短前摇的主动近战，是需要比较的玩法变更，不是对现有设计的“事实恢复”。同样，新增跟踪/锁定分段、普通敌人共享攻击许可，都应先留在原型候选中；不直接改写正式设计。

**本项目建议：**不要把“敌人常态较慢”升格为可无伤证明。墙角、狭道、已承诺突刺、弹丸和延迟落区仍需各自检查；也不要为了满足 Fairy 的规划器而反向限制敌人的合理行为。

## 2. 一手证据账本：六个主题条目

下表按六个一手主题条目组织；Mindustry 的辅助文件属于同一条源码调用链，明确列出，不把它们包装成多个独立游戏案例。所有重要内容均通过 `web_fetch` 取得正文；本轮不以搜索摘要或二手转述作证。

| 编号 | 作者/版本与实际核读位置 | 可以支持；不能支持 |
| --- | --- | --- |
| S1 | id Software，Linux DOOM 1.10，[`p_enemy.c` 固定提交正文][D]；提交 `a77dfb96cb91780ca334d0d4cfd86957558007e0` | `P_CheckMeleeRange`、`P_CheckMissileRange`、`A_Chase`、`P_NewChaseDir` 的实际判断；不能证明现代全局导航、统一固定冷却或公平攻击名额。 |
| S2 | Factorio 官方开发者 Oxyd，2019-10-18，[FFF #317][F] 的 Pathfinding / Contracting Chunks / Hierarchical Pathfinding | 基础 A* 与反向可恢复抽象搜索如何分工、复用抽象搜索状态；不能证明其用玩家中心流场、局部避让或预警公平调度。 |
| S3 | Mindustry 官方仓库 `v146`，[`Pathfinder` 共享缓存][MP-field]、[邻格查询][MP-next]、[反向传播][MP-frontier]、[目标集合][MP-goals] | 同队/同代价/同场类型共享导航数据，各单位读取方向；不是移动玩家追击案例，也不保证编队/无拥堵。 |
| S4 | Mindustry `v146`，[`GroundAI.updateMovement`][MG]、[`AIController.pathfind`][MC-path]、[`updateWeapons`][MC-weapons] | 接近目标场、单位运动意图和各武器射击意愿分层；不能据此声称所有远程单位都维护某个环带或都要求几何视线。 |
| S5 | Mindustry `v146`，[`WeaponsComp` 委托更新][MWC-update]、[`Weapon.update` 发射条件][MW-fire]、[`Weapon.shoot/bullet` 延迟发射][MW-delay]、[`Units.invalidateTarget`][MU-invalid] | 目标有效性、装填、弹药/状态/角度与实际启动射击分层；不能把目标搜索周期或视觉热量冷却当装填，也不证明通用预警锁定。 |
| S6 | Godot 官方 4.5，[Using NavigationAgents 完整源文][G] 的 Pathfollowing / Avoidance | 框架明确区分寻路、跟随、避让与用户运动代码；**不是某个游戏已采用这一套实现的事实**，也不是建议本 Unity 项目更换引擎。 |

## 3. 来源真正说了什么

### 3.1 DOOM：攻击条件不是单一距离阈值，追击尝试也不是全局寻路

- **来源事实 D1：**`P_CheckMeleeRange` 要求目标存在、近似距离低于含目标半径的门槛，并要求 `P_CheckSight`。因此该函数的“近战范围”同时具有目标、距离和视线条件，不是两个中心点足够近就成立。[S1：`P_CheckMeleeRange`][D]
- **来源事实 D2：**`P_CheckMissileRange` 首先检查视线；可见前提下，`MF_JUSTHIT` 会清标记并提前允许反击；普通分支在 `reactiontime` 非零时拒绝，然后按怪物类型、调整后的距离和随机值决定。不能将其压缩成“所有 DOOM 怪有相同固定射程/固定射击概率”，也不能把 `reactiontime` 冒称每发弹的统一装填冷却。[S1：`P_CheckMissileRange`][D]
- **来源事实 D3：**`A_Chase` 会递减 `reactiontime`，处理 `MF_JUSTATTACKED` 以避免立刻再次进入攻击判断；普通难度/非 fast 情况还以 `movecount` 限制远程攻击尝试。近战、远程分支都未采用时继续 `P_Move`，移动失败或计数到期再 `P_NewChaseDir`。这些是具体时序门槛，不是“看得见就每次追击更新都开火”。[S1：`A_Chase`][D]
- **来源事实 D4：**`P_NewChaseDir` 按目标相对方向尝试八向移动，尽量暂避立即反向，继而尝试旧方向、其他方向，最后才尝试反向；每次借助 `P_TryWalk/P_Move` 检验。此函数没有构造全局路径，不能拿它来证明 DOOM 为敌群共算流场。[S1：`P_NewChaseDir`、`P_TryWalk`、`P_Move`][D]
- **来源事实 D5：**`A_PosAttack` 出手时调用 `A_FaceTarget`，`A_Tracer` 则包含朝追踪目标调整方向的代码。本文件不是“所有攻击在预警开始时固定位置、所有弹丸永不转向”的证据；本次也没有由动画帧表核出一套通用固定锁定窗口。[S1：`A_PosAttack`、`A_Tracer`][D]

**本项目建议：**借鉴条件和时序分离，不复制 DOOM 的随机门槛或八向追击。先用明确距离、遮挡、冷却和行动阶段得到可解释行为；是否增加随机攻击倾向，等基本节奏成立后再决定。

### 3.2 Factorio：复用的是全局搜索知识，不是让局部转向代替绕路

- **来源事实 F1：**FFF #317 将路径定义为供单位依序跟随的路点；其基础寻路使用 A*。该文为大湖绕路问题引入块级抽象节点，抽象时主要看地面/水的连通性，忽略实体。[S2：Pathfinding / Contracting Chunks][F]
- **来源事实 F2：**抽象搜索从目标反向开始，以 Reverse Resumable A* 保留已探索节点；后续需要新的距离估计时直接查询或继续扩展。抽象结果只给基础 A* 提供启发估计，基础搜索仍能绕过抽象中忽略的实体。因此不能把粗抽象连通误当成最终可通行路径。[S2：Hierarchical Pathfinding][F]

**本项目建议：**可以迁移“复用昂贵的全局知识、保留精确可执行检查”这一分工，而不复制 Factorio 的分块结构。小型静态网格没有已测瓶颈前，不新增多级地图权威或反向可恢复 A*。

### 3.3 Mindustry：共享方向场、每单位行为与武器启动分层

- **来源事实 M1：**`getField(team,costType,fieldType)` 以 `cache[team.id][costType][fieldType]` 缓存并复用同一场；`EnemyCoreField.getPositions` 加入全部敌方核心，在特定波次/队伍条件下还加入刷怪点。它不是每单位一份路径，也不是本游戏“所有怪追同一个移动玩家”的现成规则。[S3：共享键][MP-field]、[目标集合][MP-goals]
- **来源事实 M2：**`updateFrontier` 从目标向四邻格传播权重，完成后复制到 `completeWeights`；`getTargetTile` 优先使用完成快照，在可通行八邻格中选权重下降的邻格，带斜穿不可通行夹角限制，找不到合适下降方向可返回原格。不要据此声称消费方向是本项目 8 邻接、1/√2 边权模型的最优后继。[S3：传播][MP-frontier]、[消费方向][MP-next]
- **来源事实 M3：**地面、腿足、舰船有不同代价函数；地面规则含不可通行条件和建筑/邻接地形等有限代价，建筑生命还经过打包缩放。这不是“所有墙都永久不可穿越”的二值模型；不能照抄代价到本游戏。[S3：代价函数][MP-cost]、[打包规则][MP-pack]
- **来源事实 M4：**`AIController.pathfind` 用自己的当前格、`pathType`、队伍和场类型查询共享场，再按本单位速度朝下一格调用 `movePref`。`GroundAI.updateMovement` 另有接近核心、近核重定向目标、波次刷怪区停止等逻辑；它不能被概括为“追当前武器目标，进任意射程即停”。这些函数也没有证明群体已进行联合避碰规划。[S4：路径消费][MC-path]、[地面行为][MG]
- **来源事实 M5：**`AIController.updateWeapons` 处理目标重选/失效、可控武器分支、各武器范围和预测瞄准点，将结果写为 `mount.shoot/rotate`；基类 `shouldShoot()` 默认只返回 `true`。其 `retarget` 的 40/90 是目标搜索间隔，**不是攻击冷却**。[S4：射击意愿][MC-weapons]、[重选周期][MC-retarget]
- **来源事实 M6：**`WeaponsComp.update` 把更新委托给每个 `Weapon.update`。武器层递减 `mount.reload`，启动射击还要求射击意愿、可射击状态、弹药或规则豁免、预热、速度、装填和角度等条件；部分条件有连续武器/常开武器例外。通过后调用 `shoot` 并重设 `reload`。AI 希望射击与武器获准启动不是一个事件。[S5：委托][MWC-update]、[装填推进][MW-reload]、[完整发射条件][MW-fire]
- **来源事实 M7：**`Weapon.shoot` 可以因 `firstShotDelay` 产生蓄力效果，并按模式给出的延迟排程子弹；通过发射门槛也未必立即生成子弹。这里的可配置延迟不能外推成每种敌人都有可读的预警、末段锁定或完整攻击承诺协议。[S5：`shoot/bullet`][MW-delay]
- **来源事实 M8：**本次所读 `AIController.updateWeapons`、`Units.invalidateTarget` 与 `Weapon` 这条通用链未见显式地形遮挡射线门槛；有效性检查包含范围/队伍/对象状态等，目标搜索又有迷雾筛选。因此既不能写“Mindustry 通用远程 AI 必须视线通才开火”，也不能扩写成“整个游戏完全没有可见性规则”。这只是所读调用链的结论，不覆盖所有派生 AI、武器、弹种和碰撞实现。[S4：意愿][MC-weapons]、[S5：失效判断][MU-invalid]、[迷雾筛选][MU-find]、[发射条件][MW-fire]

**本项目建议：**采用分层接口含义，不复刻 Mindustry 的核心攻防目标、建筑破坏代价、预测瞄准和弹药规则。本游戏钉枪的视线门槛需要由本项目明确规定，不能借 Mindustry 的目标有效性检查假称已经得到证明。

### 3.4 Godot：职责分离的官方框架说明，不能替游戏背书

- **来源事实 G1：**文档分别描述 pathfinding、pathfollowing 和 avoidance；取得 `get_next_path_position()` 后，要由用户自己的运动代码移动父节点，导航系统不会自动移动它。[S6：NavigationAgent Pathfollowing][G]
- **来源事实 G2：**文档明确说 avoidance 不影响 pathfinding，存在于独立空间，不知道导航网格或物理碰撞；只考虑已注册的避让对象，并说明 RVO 有行为假设和失败场景。这里的 `safe_velocity` 名称不能被迁移成对本游戏墙体、攻击伤害或所有拥堵情形的安全保证。[S6：NavigationAgent Avoidance][G]

**本项目建议：**复用这条责任边界，先使用轻量分离/软避让；没有实际拥堵证据前不引入 RVO/ORCA 或导航框架。软避让和最终几何碰撞解算必须分开。

## 4. 本项目建议：群体追击共享什么，各层分别负责什么

**以下整节均为本项目建议，不是任何来源游戏的原样架构。**理由来源是 S3 的共享场、S2 的粗细分工和 S6 的跟随/避让边界，具体方案由本项目选择。[S3][MP-field]、[S2][F]、[S6][G]

### 4.1 最小责任表

| 层 | 回答的问题 | 输出与边界 |
| --- | --- | --- |
| 可走图/几何 | 这个体型能不能经过这里？ | 单一障碍与通行规则；导航、拉直、运动和攻击扫掠使用一致半径/禁角语义，不另建互相矛盾的地图。 |
| 全局导航 | 隔墙、U 形障碍要从哪边绕？ | 同目标追兵读共享代价到达场；独立射击站位等读 A* 路径。结果不承诺邻居会让路，也不决定能否攻击。 |
| 路径跟随 | 当前该沿哪段连续运动？ | 下一路点/期望速度、进展与到达判定；不能每帧只朝玩家中心走而丢掉绕墙结果。 |
| 局部软避让 | 眼前几只怪怎样不完全重叠？ | 有界修改期望速度，处理分离与让行；不承担跨障碍拓扑搜索，不保证解决窄门死锁。 |
| 行动执行/碰撞 | 最终位移与命中是否合法？ | 按真实体积阻止穿墙，做突刺扫掠、弹丸与近战命中等；最终权威不交给“避让速度”。 |
| 敌人行为/攻击 | 现在该接近、站位、瞄准、出手还是恢复？ | 读取导航与攻击几何，决定状态；不能让路径跟随器顺便决定攻击冷却或伤害。 |

### 4.2 为什么共享，何时不能共享

- 同一目标位置、同一可走图与同类体型的追兵，重复求相同“从各格到目标的代价”没有玩法收益；共享结果后，各单位仍保留自己的速度、邻居、攻击阶段与冷却。**共享的是全局导航知识，不是所有敌人的动作和攻击时刻。**
- 缓存需要至少区分目标格、通行类别/必要的体型类别、地图版本；不要为了未来未知单位预先引入更多类别。目标换格或通行规则改变使派生结果需要更新；更新周期是可调预算，不是来源给出的 0.25s 真理。
- 共目标流场不必替代全部 A*。钉枪选择不同射击点、局部撤退点或不可共享的目标时，复用同一可走图做少量点到点查询即可；不为每种怪复制一套导航。
- 本项目若使用正交代价 1、对角代价 √2，后继要最小化“走到邻格的边代价 + 邻格到目标的代价”，或使用搜索时保存的后继；不能只因某个邻格数值最小就声称它是加权最短路。此为本项目数学/算法要求，不是对 Mindustry 消费规则的转述。
- 静态墙体适合进入全局图；不断移动的普通敌人先由局部软避让处理，不因每个邻居的位置变化重建地图。窄门挤住时记录进展、稳定让行/重选路径；不以随机瞬移或穿墙当恢复策略。
- 暂缓分块搜索、NavMesh、全时空多智能体联合规划和通用战斗导演。升级条件应是实际场景暴露的绕路/拥堵/耗时问题，而不是同屏几十只怪这个数字本身。

**尚待手感验证：**流场失效与更新频率、路径终点容差、分离强度、允许的身体重叠、窄门通行速度及拥堵兜底；不能从论文、官方代码或开发机平均耗时直接推得本游戏真机预算。

## 5. 本项目建议：攻击可行性、视线与冷却怎样决定攻击

**以下整节是候选规则。**它借鉴 DOOM 的条件/时序检查及 Mindustry 的意愿/武器门槛分层，不声称两者实现了本节状态机。[S1][D]、[S4][MC-weapons]、[S5][MW-fire]

### 5.1 先把五个不同问题拆开

1. **目标是否有效？**目标仍存在、可攻击；失效后停止尚未出手的意愿，不把空目标送进几何判断。
2. **当前攻击是否可行？**按该招式检查距离/角度、攻击体积与障碍、需要的视线，以及落点是否合法。几何可行不等于冷却已经结束。
3. **自己是否就绪？**不在死亡/受控/恢复等排他阶段，自己的冷却已到期。冷却与找目标周期、导航重算周期、动画热量不是同一时钟。
4. **现在是否允许开始？**先有“独立攻击”基线；如启用组合许可，只在这里仲裁是否进入前摇，不取代前面三个条件。
5. **出手与结算发生了什么？**进入前摇不是命中；锁定、发射/突刺启动、碰撞/命中、恢复分别是明确事件。

候选公共阶段：

```text
接近/重新站位 → 跟踪前摇 → 锁定承诺 → 出手/生效 → 恢复 → 再评估
                     ↘ 按明确中断规则取消
```

无需所有敌人用相同计时，也不要求为每条箭头建立通用框架。可复用共同阶段语义和只读攻击描述，差异仍放在各职责行为中。

### 5.2 视线不替代攻击体积检查

- **近战：**开始前检查近身距离和无墙阻隔；实际生效时再次检查真实攻击范围/形状与遮挡。玩家在前摇中走开可以打空，不能靠已选中目标直接扣血。
- **突刺：**“看得到玩家中心”不证明整个身体能冲过去。开始/锁定时检查带半径的突刺通道；执行时按完整身体扫掠并被墙阻挡。锁定后不拐弯追人、不借路径跟随绕柱子继续这一刺。
- **射击：**选择能到达且能形成有效发射线的位置。前摇开始时有视线；发射前可按规则复查枪口是否被挡，阻挡则取消这一发并进入短恢复，或仍向锁定线发出会撞墙的弹，二者需选定一种。无论哪种，都不能穿墙扣目标血或最后一帧重瞄到墙后。
- **落区：**需要的是目标采样和落点合法性，不天然需要发射者到落点的直线走廊。高空攻击是否无视地面掩体、矩形如何与墙/不可走地面相交，由本项目单独定义，不能套“所有招式同一条 LOS”或声称来源证明隔墙也公平。

### 5.3 冷却与取消的最小可解释语义

- 冷却就绪只允许尝试开启一轮动作；这轮仍可能因几何、中断或组合许可而没有出手。没有可行射击线时，钉枪应重新站位，而非永远站着等冷却。
- 跟踪前摇早段失去必要视线，可取消并短恢复；已经锁定后，原则上按所示方向/落点完成或按统一的死亡/硬中断规则取消，不瞬间重选目标。具体中断权限仍需原型比较。
- 实际出手即使打空，也正常进入恢复/冷却，给玩家的躲避产生真实收益。避免“未命中立即重新前摇”导致原地连续追打。
- 明确冷却从哪一个事件起算：建议先以出手/招式启动时记录下一次可尝试时刻，恢复阶段另行约束，只有两者都结束才再次前摇。前摇取消采用单独、简单的短重试间隔；不要用同一计时器混装跟踪、恢复与装填。
- 死亡应立即阻止该单位尚未生成的攻击；已经生成的弹丸/落区是否继续存在要显式约定，不能因为攻击者死亡就隐式改变此前可见承诺。

**尚待手感验证：**取消后惩罚、射击被挡时“取消或撞墙”的取舍、恢复能否移动、打空是否全额冷却、玩家贴脸时钉枪撤退与攻击谁优先。以上不能由本次来源直接定案。

## 6. 本项目建议：预警跟踪与锁定承诺

**证据边界：**本次没有获得能支持“某游戏统一采用早段跟踪、末段固定锁定，并以此保证公平”的直接来源。DOOM 的出手转向/追踪代码和 Mindustry 的可配置延迟，只能支持各自的具体事实，不能填补这个证据缺口。[S1][D]、[S5][MW-delay]

### 6.1 锁定的究竟是什么

| 对象 | 跟踪阶段可以变化的东西 | 锁定之后的承诺 |
| --- | --- | --- |
| 主动近战候选 | 面向玩家的方向；自身是否允许继续小幅接近需另定 | 固定攻击方向/扇区或短距离打击形状；生效时按真实几何结算，不“目标锁定即必命中”。 |
| 撞针突刺 | 朝向、候选突刺方向 | 固定起点、方向与最大距离/终止条件；墙体可截断，不再局部绕路或因避让折线转向。 |
| 钉枪射击 | 枪口瞄准方向 | 固定发射方向/瞄准点；本轮弹丸先用直线非追踪形式比较。弹丸生命周期由它自己的碰撞规则决定。 |
| 坠物落区 | 尚未公开落区前可采样玩家位置 | 公布后固定矩形与倒计时；不得让矩形追着玩家的脚漂移，延迟结束在该矩形生效。 |

锁目标身份、锁世界坐标、锁方向、锁攻击体积是不同选择。每招只保存必需的承诺数据；预警与伤害读取同一份数据，避免“画的位置”和“打的位置”两份权威。

### 6.2 两种适合 A/B 的候选，而不是既定答案

- **候选 A：预警开始即锁定。**最容易读懂，躲开后结果稳定；可能使慢速近战/突刺过容易躲。
- **候选 B：前摇早段跟踪，末段明确固定。**保留瞄准压力，但须有可辨的视觉变化说明已锁定；末段长度必须真能看见并响应，不能只有内部变量切换。早段跟踪也应有转向限制，避免表现瞬跳。
- 两者都先禁止锁定后补偿玩家的最后一帧移动；任何预测提前量、追踪弹或二次追击都应作为新的可读招式特征，而非暗中“修正命中率”。
- 现有设计的撞针 0.6s、骤雨 3s 是项目已有数字，不是外部游戏的验证结果；如何划分跟踪/固定窗口仍未确认。[项目设计 §5](<../design/game-design.md#L95-L103>)

**尚待手感验证：**锁定时刻与视觉提示、旋转速度、前摇总长、固定窗口、攻击宽度、出手后的可惩罚窗口。应让人手玩 A/B，并检查警示体积是否包含真实伤害体积；“固定窗口存在”本身不证明人可躲。

## 7. 本项目建议：四种职责与导航/攻击的分工

**整张表是本项目候选，名称和基本意图承接项目设计；不是 DOOM、Factorio 或 Mindustry 的敌人分类。**[项目设计 §5](<../design/game-design.md#L89-L103>)

| 职责 | 为什么移动，怎样选位置 | 攻击合同 | 不应顺手承担的职责 |
| --- | --- | --- | --- |
| 尾灯：近身压力 | 多数时候共享追击场；进入可攻击距离后减速/停下，避免穿过玩家中心反复抖动 | 比较原接触伤害与“短前摇→近身打击→短恢复”候选；主动打击允许被走位躲空 | 不额外承担远程封区，也不因贴身而每帧无条件扣血；接触模型是否保留须明确。 |
| 撞针：穿线/迫使横移 | 先通过常规导航到可发起突刺的位置；突刺通道不成立就继续换位置 | 跟踪/锁定→定向直刺→撞墙或到距停止→明显恢复；普通寻路只管前后接近，不接管突刺轨迹 | 不在冲刺中绕墙追人，不把软避让变成隐性追踪。 |
| 钉枪：射线与站位压力 | 在可达、可形成有效射线的候选位置中选站位；射程带内保持，过近再有界后退；退路被堵则换侧或暂缓，不穿墙倒退 | 需目标/射线/冷却就绪才开始瞄准；锁定后发慢直线弹，出手后恢复 | 不永久朝玩家中心贴近；不把地图直线距离合适误当可达/可射。环带上下界加滞回避免进退抖动。 |
| 坠物：延迟区域封锁 | 飞行接近只负责投放资格与自身分离；投放位置在允许地面内采样，不要求地面路径跟踪 | 固定矩形警示→倒计时→该区生效→结束；普通坠物投放频率、存活方式与能否再次投放另定 | 不把公开警示变成追踪判定；不混为一个没有墙体合同的地面追兵。 |

**本项目建议：**先让四种职责在单独场景成立，再混编；Boss 尽量复用“突刺合同”或“落区合同”，变化放在节奏、次数、召唤和窗口，不另造一套伤害/预警权威。保留设计中连环恢复、召唤数量上限，以及骤雨同一 Boss 技能不重叠的意图；普通敌群跨单位并发是否受限是另一层新提案，二者不能混写。[现有 Boss 意图](<../design/game-design.md#L99-L103>)

## 8. 组合攻击限制：是本项目提案，不是来源事实

**来源核查结论：**本次 DOOM 函数存在单体攻击时序与随机门槛，Mindustry 已读链存在单体/单武器发射条件；它们没有为本报告提供“全体敌人共享两个公平攻击槽”“危险预算保证可躲”“固定屏幕内最多几次预警”的直接证据。Factorio 本次引用部分仅支持寻路结论，Godot 这篇文档仅支持导航职责说明。不能由有限文件推断整个游戏绝不存在特殊调度，也不能把本项目提案写成它们已使用的事实。[S1][D]、[S2][F]、[S4][MC-weapons]、[S5][MW-fire]、[S6][G]

**本项目建议，按最小复杂度递进：**

1. **基线：独立攻击。**各单位满足几何、冷却和状态就自行前摇；用可复现的初始节奏偏移避免出生即同拍，但偏移不等于公平保证。
2. **可选实验：有限并发许可。**可从“同时最多 2 个准备/出手动作”作为比较值开始，明确计数阶段，例如进入跟踪前摇占用，出手阶段结束释放。这个“2”没有外部证据或手感结论；不能当正式规格。
3. 许可仅决定新动作何时开始，不在玩家已经看到锁定后临时改方向、取消攻击或瞬间降速；排队/重试需固定顺序或简单可复现规则，避免同一只怪长期饿死。尚未出手的单位死亡应释放许可。
4. 明确许可是限制**动作并发**还是**持续危险**：若弹丸/落区在动作结束后仍存在，释放许可不会清除那些危险。先把存量危险显示和记录出来，不立刻扩成多层通用资源系统。
5. 两条突刺加一道墙也可能堵死出口，少量矩形也可能完全封住窄道。因此名额上限不证明空间上有解；反过来，所有攻击都排队也可能让敌人显得礼貌、缺乏压力。是否需要进一步空间约束应由实际混战失败场景决定。

**尚待手感验证：**独立攻击/许可模式、许可作用范围、阶段边界、最多并发数、等待行为和存量弹丸压力。不要现在引入预测玩家所有未来路线的全局导演来保证无伤。

## 9. 下一步原型应回答的题，不是已经通过的验收

下列均为**本项目建议的比较场景与观察项**；本轮模型已覆盖其中的基础攻击、隔墙、锁定和组合节奏，实际交互记录与仍待体验的项目见[原型说明](<../../prototype/enemy-behavior/README.md>)。敌人规则确认前不以 Fairy 的高分或无伤日志替代判断。

| 场景 | 要回答的问题 | 观察/记录 |
| --- | --- | --- |
| 同目标多追兵绕 L/U 形墙与窄门 | 全局场是否稳定带路，软避让是否妨碍路径进展？ | 穿墙、卡住时长、绕路方向反复切换、近邻重叠；分别观测全局查询与局部转向成本。 |
| 单尾灯贴近/横移/绕柱 | 接触伤害还是主动短打更符合走位飞刀循环？ | 玩家能否读懂伤害来源，前摇中离开是否打空，恢复能否形成输出窗口。 |
| 单撞针，预警中持续横移 | 起点锁定与末段锁定的压力差异？ | 提示转向、实际冲刺线是否吻合；撞墙是否停止；软避让有无偷改承诺。 |
| 单钉枪，目标进出距离带、绕墙、冷却中贴近 | 视线、距离、就绪与站位是否真正独立？ | 冷却未好是否仍合理走位、无射线是否换位、锁定后能否靠掩体避弹、反复进退是否抖动。 |
| 单坠物，标记后改变移动方向 | 固定矩形能否可靠表达落点？ | 警示和伤害区域是否一致、倒计时是否准确、墙边合法性、是否存在最后一帧跟踪。 |
| 四职责混战，开阔地与狭道各一组 | 独立攻击与有限许可哪个压力合理？ | 预警可辨性、并发动作数、存量危险、出口被完全封死的具体组合，以及等待单位是否僵硬。 |

### 未决点清单

- **玩法：**尾灯接触还是主动近战；早锁/晚锁；钉枪预测瞄准与否；落区对掩体的合同；空中坠物本体的存活/攻击循环。
- **节奏：**前摇、锁定、恢复和冷却从何起算；取消/打空惩罚；移动与恢复能否并行；普通怪与 Boss 节奏有何区别。
- **组合：**是否需要全局许可，许可计动作还是危险，计数范围与释放时机；保留哪些设计明确要求的 Boss 隔断。
- **导航：**体型与通行半径、禁角/拉直一致性、射击点候选与退路、更新/失效规则、窄门拥堵的最小恢复方式。
- **证据：**本次没有直接来源证明统一公平性导演或预警末段锁定；没有本游戏手感、真机性能或无伤保证的测量结果。Mindustry 结论限于 `v146` 所读通用调用链，不能代表所有版本/子类。

## 10. 访问记录与复核入口

### 10.1 成功取得正文

- **DOOM：**最初普通 raw 地址与 [GitHub master 页面](<https://github.com/id-Software/DOOM/blob/master/linuxdoom-1.10/p_enemy.c>) 报 `TypeError: fetch failed`，没有 HTTP 状态。改用 [refs/heads/master raw](<https://raw.githubusercontent.com/id-Software/DOOM/refs/heads/master/linuxdoom-1.10/p_enemy.c>) 成功；再由 [官方提交 API](<https://api.github.com/repos/id-Software/DOOM/commits/master>) 固定提交，成功核读本文引用的[固定提交 raw][D]。不是资料不存在，也没有凭失败页面编写源码事实。
- **Factorio：**[FFF #317][F] HTTP 200，正文已读；仅使用其中寻路部分。
- **Godot：**[stable HTML 页面](<https://docs.godotengine.org/en/stable/tutorials/navigation/navigation_using_navigationagents.html>) 和 [4.5 HTML 页面](<https://docs.godotengine.org/en/4.5/tutorials/navigation/navigation_using_navigationagents.html#navigationagent-avoidance>) HTTP 200，但抓取结果被大量导航目录截断；因此实际依据是 HTTP 200、完整取得正文的[官方 4.5 RST 源文][G]，不是假称从截断页面读到了全部内容。
- **Mindustry：**下列官方 Contents API 全部 HTTP 200，返回 Base64 全文；在内存解码并逐行核读。报告中的 `blob/v146` 链接用于人读与行号定位，**本会话对应网页抓取失败，实际正文来自官方 API**。

| 已核读文件 | 成功 API 入口 | 返回的文件 blob SHA |
| --- | --- | --- |
| [`Pathfinder`][MP-field] | [Contents API][MP-api] | `8aba767dda6fa356abca4cfb753341c12cc47e0d` |
| [`AIController`][MC-path] | [Contents API][MC-api] | `6aef11e1043ec9145ff0078783b0e27694cfed7e` |
| [`GroundAI`][MG] | [Contents API][MG-api] | `602ceaea7c3b7a9e2838cac9cfa9ef71f5f055f2` |
| [`WeaponsComp`][MWC-update] | [Contents API][MWC-api] | `e3de8b484aebbb20bf5e75185a3217f77d9b8f55` |
| [`Weapon`][MW-fire] | [Contents API][MW-api] | `97ac3b45daf598caaa695c373bfbdbfa190d6e86` |
| [`Units`][MU-invalid] | [Contents API][MU-api] | `184203a9b7586335452a6f345f3fd60670ecc599` |

### 10.2 Mindustry 未成功的入口

以下请求均为 `TypeError: fetch failed`，未返回 HTTP 状态；不是“404”结论。成功 API 已覆盖所需事实，无需用二手材料补写：

1. [Pathfinder 普通 raw](<https://raw.githubusercontent.com/Anuken/Mindustry/v146/core/src/mindustry/ai/Pathfinder.java>)；[AIController 普通 raw](<https://raw.githubusercontent.com/Anuken/Mindustry/v146/core/src/mindustry/entities/units/AIController.java>)；[GroundAI 普通 raw](<https://raw.githubusercontent.com/Anuken/Mindustry/v146/core/src/mindustry/ai/types/GroundAI.java>)；[WeaponsComp 普通 raw](<https://raw.githubusercontent.com/Anuken/Mindustry/v146/core/src/mindustry/entities/comp/WeaponsComp.java>)。
2. [Pathfinder tag 页面](<https://github.com/Anuken/Mindustry/blob/v146/core/src/mindustry/ai/Pathfinder.java>)；[AIController tag 页面](<https://github.com/Anuken/Mindustry/blob/v146/core/src/mindustry/entities/units/AIController.java>)；[GroundAI tag 页面](<https://github.com/Anuken/Mindustry/blob/v146/core/src/mindustry/ai/types/GroundAI.java>)；[WeaponsComp tag 页面](<https://github.com/Anuken/Mindustry/blob/v146/core/src/mindustry/entities/comp/WeaponsComp.java>)。
3. [GroundAI GitHub refs/tags raw](<https://github.com/Anuken/Mindustry/raw/refs/tags/v146/core/src/mindustry/ai/types/GroundAI.java>)；[AIController raw refs/tags](<https://raw.githubusercontent.com/Anuken/Mindustry/refs/tags/v146/core/src/mindustry/entities/units/AIController.java>)；[WeaponsComp GitHub raw](<https://github.com/Anuken/Mindustry/raw/v146/core/src/mindustry/entities/comp/WeaponsComp.java>)；[Pathfinder raw refs/tags](<https://raw.githubusercontent.com/Anuken/Mindustry/refs/tags/v146/core/src/mindustry/ai/Pathfinder.java>)。

复核时应优先沿事实后的函数行号回到官方源码；若网页仍不可访问，可用同文件的 Contents API 与上述 SHA 核对。不能把“源码确有某个机制”扩写成“本游戏采用即可公平/最优/达到某性能”。

[D]: <https://raw.githubusercontent.com/id-Software/DOOM/a77dfb96cb91780ca334d0d4cfd86957558007e0/linuxdoom-1.10/p_enemy.c>
[F]: <https://www.factorio.com/blog/post/fff-317>
[G]: <https://raw.githubusercontent.com/godotengine/godot-docs/4.5/tutorials/navigation/navigation_using_navigationagents.rst>
[MP-field]: <https://github.com/Anuken/Mindustry/blob/v146/core/src/mindustry/ai/Pathfinder.java#L283-L295>
[MP-next]: <https://github.com/Anuken/Mindustry/blob/v146/core/src/mindustry/ai/Pathfinder.java#L298-L349>
[MP-frontier]: <https://github.com/Anuken/Mindustry/blob/v146/core/src/mindustry/ai/Pathfinder.java#L403-L452>
[MP-goals]: <https://github.com/Anuken/Mindustry/blob/v146/core/src/mindustry/ai/Pathfinder.java#L454-L468>
[MP-cost]: <https://github.com/Anuken/Mindustry/blob/v146/core/src/mindustry/ai/Pathfinder.java#L42-L64>
[MP-pack]: <https://github.com/Anuken/Mindustry/blob/v146/core/src/mindustry/ai/Pathfinder.java#L178-L191>
[MG]: <https://github.com/Anuken/Mindustry/blob/v146/core/src/mindustry/ai/types/GroundAI.java#L14-L49>
[MC-path]: <https://github.com/Anuken/Mindustry/blob/v146/core/src/mindustry/entities/units/AIController.java#L118-L128>
[MC-weapons]: <https://github.com/Anuken/Mindustry/blob/v146/core/src/mindustry/entities/units/AIController.java#L130-L207>
[MC-retarget]: <https://github.com/Anuken/Mindustry/blob/v146/core/src/mindustry/entities/units/AIController.java#L218-L228>
[MWC-update]: <https://github.com/Anuken/Mindustry/blob/v146/core/src/mindustry/entities/comp/WeaponsComp.java#L96-L102>
[MW-reload]: <https://github.com/Anuken/Mindustry/blob/v146/core/src/mindustry/type/Weapon.java#L277-L280>
[MW-fire]: <https://github.com/Anuken/Mindustry/blob/v146/core/src/mindustry/type/Weapon.java#L389-L415>
[MW-delay]: <https://github.com/Anuken/Mindustry/blob/v146/core/src/mindustry/type/Weapon.java#L430-L468>
[MU-invalid]: <https://github.com/Anuken/Mindustry/blob/v146/core/src/mindustry/entities/Units.java#L127-L143>
[MU-find]: <https://github.com/Anuken/Mindustry/blob/v146/core/src/mindustry/entities/Units.java#L245-L288>
[MP-api]: <https://api.github.com/repos/Anuken/Mindustry/contents/core/src/mindustry/ai/Pathfinder.java?ref=v146>
[MC-api]: <https://api.github.com/repos/Anuken/Mindustry/contents/core/src/mindustry/entities/units/AIController.java?ref=v146>
[MG-api]: <https://api.github.com/repos/Anuken/Mindustry/contents/core/src/mindustry/ai/types/GroundAI.java?ref=v146>
[MWC-api]: <https://api.github.com/repos/Anuken/Mindustry/contents/core/src/mindustry/entities/comp/WeaponsComp.java?ref=v146>
[MW-api]: <https://api.github.com/repos/Anuken/Mindustry/contents/core/src/mindustry/type/Weapon.java?ref=v146>
[MU-api]: <https://api.github.com/repos/Anuken/Mindustry/contents/core/src/mindustry/entities/Units.java?ref=v146>
