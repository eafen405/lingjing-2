# 开发工作文档 · 写作指引

> **这份文件不是成稿，也不该交上去。** 它是「写什么、按什么顺序写、每条事实去哪儿查」的清单。
> 考核原话是「文档简要记录自己的开发过程，**不要用 AI 大段生成**」——下面只给问题、事实和出处，
> 句子必须你自己写。凡是本文里读起来像句子的地方，都只是**事实陈述**，请改写成你的说法再落地。

## 0. 先把三件事定下来

| 项 | 建议 |
| --- | --- |
| 篇幅 | **1–2 页，1200–1800 字**。「简要」是硬要求，写长了反而像凑 |
| 人称 | 第一人称「我」。这是过程记录，不是 README |
| 读者 | 群内验收方（郑智文 / 缪延瑞）。他们只看两件事：**这是你自己做的吗**、**你知道自己代码里发生了什么吗** |
| 文件名 | 建议 `docs/delivery/dev-work.md`（现成的 `dev-work-skeleton.md` 是素材，别直接交） |

**三条禁忌**：不要贴大段代码；不要写功能说明书式的罗列（「本游戏有 A/B/C 功能」）；不要出现「作为一个 AI 助手」式的腔调。

---

## 1. 全文骨架（按这个顺序写，先写完再回头删）

| # | 小节 | 字数 | 这一节要证明什么 |
| --- | --- | --- | --- |
| 1 | 一句话：这是个什么游戏 | 120–150 | 你知道自己做的是什么、给谁玩、多久一局 |
| 2 | 怎么做的：工具链与工程结构 | 200–250 | 你不是乱写，有分层、有取舍 |
| 3 | **三个技术决定** | 450–600 | 你理解自己代码里最难的三处（**全文最值钱**） |
| 4 | **踩过的三个坑** | 400–500 | 你会排查问题，不是只会写 happy path（**第二值钱**） |
| 5 | AI 用在哪、哪些不是 AI 定的 | 200 | 你用 AI 有边界感，不是把活全丢给模型 |
| 6 | 交付了什么、怎么验收 | 120–150 | 材料齐全、能跑起来 |

写作顺序建议：**先写 3 和 4**（想清楚这两个，全文就立住了），再补 1、2、5、6。

---

## 2. 逐节写什么

### §1 一句话

**必须回答**：什么类型的游戏？玩家在干什么？一局多长？交付形态是什么（APK / 手机）？

**可引用事实**

| 事实 | 出处 |
| --- | --- |
| 横屏俯视像素动作游戏；环绕飞刀切以骸；Fairy 代行让你短暂交出控制权 | `GLOSSARY.md` |
| 两关 × 4 节；普通节 45 只；第 4 节是恶名以骸；一局 8–12 分钟 | `Assets/_Project/Data/levels.json`（每节三波、数量可数） |
| Android / IL2CPP / ARM64 / minSdk 22 / 横屏 | `ProjectSettings/ProjectSettings.asset`、`Editor/AndroidBuilder.cs` |

**别写**：世界观设定（伊埃斯、空洞的来历）。那是设计稿的事，这里一句带过就够。

---

### §2 工具链与工程结构

**必须回答**：用了什么？为什么这几个程序集要分开？哪些是第三方的、你怎么处理的？

**可引用事实**

| 事实 | 出处 |
| --- | --- |
| Unity 2022.3.62f2（中国版）、UGUI + DoozyUI v3.1.3、DOTween 1.2.790、PlayerPrefs、PlasticSCM + GitHub | `docs/agents/third-party-assets.md` |
| 9 个程序集：Core / Combat / AI / Level / Data / UI / View / Audio / Editor | `Assets/_Project/Scripts/*/Lingjing.*.asmdef` |
| **Core / Combat / AI / Level 四个程序集 `noEngineReferences: true`**，里面没有 `using UnityEngine`、没有 `Time`/`Random` | 同上，可 grep 自证 |
| 规模：85 个手写 `.cs`、约 10.7k 行；212 张像素 PNG | 统计即可 |
| 界面全部代码生成、不拖 Prefab，为的是无头构建与探针可验证 | `UI/UiKit.cs`、`docs/agents/third-party-assets.md` §与自研 UI 的关系 |

**可能被问到的一处**：`Lingjing.Data` 目前是空程序集——`AI/Sim/ScriptedPlayerPolicy.cs` 与 `AI/Sim/Scenario.cs` 写了 `namespace Lingjing.Data`，但文件在 AI 目录下、编进 `Lingjing.AI`。直说「分层预留、还没落地」即可，别绕。

**这一段最该写的一句判断**：为什么把「模拟」关在四个不依赖引擎的程序集里。这是你全篇结构上的主动选择，值得单独说一句。

**别写**：逐个程序的清单式介绍。

---

### §3 三个技术决定（挑这三个，正好对应三份 ADR）

> 写法建议：每个决定 150–200 字，固定四拍——**原来怎么做 → 出了什么问题 → 改成什么 → 代价是什么**。

#### 决定一：模拟与表现分家（ADR-0003）

| 要点 | 出处 |
| --- | --- |
| 伤害/命中/AI 全在固定 1/60 步的模拟层；顿帧、震屏、闪白、伤害数字只活在表现层 | `docs/adr/0003-hit-feedback-stays-in-view.md` |
| 好处：无头探针能逐 tick 复现同一局 | `Editor/KernelSmokeTest.cs`（两次运行比 hash）、`AI/Sim/TickHasher.cs` |
| 代价：打击感要单独做一遍（模拟层给不了） | `View/BattleRunner.cs` 的 `OnBladeHit` / `Freeze` / `SyncEnemyFlash` |

**必须自己回答的一句**：分家之后，你为什么还愿意承担「打击感要重做一遍」的成本？

#### 决定二：飞刀判定改成扫掠线段（ADR-0004，全篇最有故事的一个）

| 要点 | 出处 |
| --- | --- |
| 旧：只判刀尖一个点 → 以骸贴身到距离 ≈0 时永远打不到（可见范围只有 1.7–2.7 的窄环） | `docs/adr/0004-blade-sweep-hit-model.md` §背景 1 |
| 旧：命中间隔按 `2π/|ω_rel|` 算，侧向走时 `ω_rel` 可以掉到 0.32，两次移速成长后撞上共振点 → **单刀对单敌冷却 139 秒**，玩家只觉得「打着打着突然不掉血」 | 同上 §背景 2 |
| 新：判定体 = 圆心→刀尖线段（外扩 0.18）；间隔固定 `2π/ω`；相对速度降级为伤害倍率 0.8–1.3 | `Combat/BladeRing.cs`、`Combat/DamageRules.cs` |
| 连带：AI 规划器的命中模型也要同步改，否则她按旧模型走位 | `AI/Fairy/LexPlanner.cs` |
| 验证：`LevelProbe` 基线从 L1 21477 tick 降到 6227 tick，两关仍确定性通关、kills 159/159 不变 | `Editor/LevelProbe.cs`、ADR-0004 §Consequences |

#### 决定三：AI 代行 = 同一套动作空间的规划器

| 要点 | 出处 |
| --- | --- |
| 优先级栈 + 候选点采样 + 1 秒前瞻，每 100ms 重规划 | `AI/Fairy/IPlanner.cs`、`AI/Fairy/LexPlanner.cs` |
| 不无敌、不加速、不传送——她和玩家用同一条 `Command` 通路 | `AI/Fairy/IPlanner.cs` 的 `Command`、`AI/Sim/BattleWorld.cs` |
| 为了让她可信，先做了无头模拟器原型（同种子逐 tick 决策一致） | `prototype/battlesim/`、`docs/research/fairy-simulation-findings.md` |

**必须自己回答的一句**：为什么给她加约束（不无敌/不加速）反而更难写，你还是选了这条路？

---

### §4 踩过的三个坑（每个坑固定四拍：现象 → 怎么查 → 怎么改 → 怎么验证）

> 这一节是全文最能证明「你真的在写代码」的地方。**别写「遇到困难，后来解决了」**——要写你怎么定位的。

#### 坑 A（推荐）：真机试玩说「手感一般」，查下来是两个结构性缺陷

- 现象：玩家抱怨「贴身砍不到」「打着打着不掉血」
- 怎么查：把 `hits/s = N·|ω_rel|/2π` 代进实际移速算，发现 `ω_rel` 会被侧向速度压到接近 0；再算共振点，得到 139 秒这种不可能被玩家感知的冷却
- 怎么改：见 §3 决定二
- 怎么验证：无头探针跑两关，记录 tick 数与击杀数（21477 → 6227，kills 159/159）
- 出处：`docs/adr/0004-blade-sweep-hit-model.md` 全文

#### 坑 B（推荐）：DoozyUI 在运行时会往工程里写文件、`Show()` 会静默不生效

- 现象：编辑器里挂 `UIView` 之后，`Assets/Doozy/` 下凭空多出配置文件；有时调 `Show()` 界面不出来，也不报错
- 怎么查：读 Doozy 源码——`AddComponent` 触发 `Reset()` 时会取 `UIViewSettings.s_instance`，没有资产就现场创建；`ShowBehavior.InstantAnimation` 关闭且没有动画预设时 `Show()` 直接 `return`
- 怎么改：用反射先塞一个内存实例占住 `s_instance`；统一在 `DoozyViewHost.Attach` 里把 `ViewCategory`、起点、三个 `Disable*WhenHidden`、`InstantAnimation` 一次性配好
- 怎么验证：探针断言六个视图的 `ViewName` 与 `sortingOrder`（`Editor/UiProbe.cs`）
- 出处：`UI/DoozyViewHost.cs`、`docs/agents/third-party-assets.md` §与自研 UI 的关系（还记录了「v3.1.3 用了被删掉的 `ScreenOrientation.Landscape`，出 Android 包时是硬错误」）

#### 坑 C（推荐，且最新）：第二次进关卡时，每次命中都抛异常

- 现象：暂停重开 / 失败重开 / 通关进第二关之后，命中不再弹伤害数字；如果那一击致死，怪不结算、不掉落
- 怎么查：伤害数字是**静态池**，切场景时池里的对象被销毁但静态引用还在 → 第二次进场景 `Pop()` 拿到已销毁对象；异常从命中事件里抛出，把整个 tick 打断
- 怎么改：`SubsystemRegistration` 阶段清池、根节点假 null 时整池清空、`Pop()` 后判空重建、补间持有并在 `OnDestroy` 里 Kill
- 怎么验证：写了一个专门的回归探针，**先构造「池非空 + 引用已销毁」的条件**再切场景，证明重建成功（`Editor/DamagePoolProbe.cs`）
- 出处：`View/DamageNumber.cs`

> 备选坑（想换可以换）：Android 首次出包要切平台/装 Build Support、场景顺序写死 MainMenu 第 0（`Editor/AndroidBuilder.cs`、`Editor/SceneCatalog.cs`）；贴身伤害每 tick 触发导致红屏刷屏（`View/BattleRunner.cs` 的 `HurtFeedbackMinInterval`）。

---

### §5 AI 用在哪

**必须回答**：AI 具体产出了什么？哪些是你判断的？你靠什么确认 AI 写的东西是对的？

**可引用事实**

| 用途 | 具体产出 | 出处 |
| --- | --- | --- |
| 游戏内 AI | Fairy 代行的规划器（优先级栈 / 候选点 / 前瞻 / 与玩家同构的指令） | `Assets/_Project/Scripts/AI/Fairy/*` |
| 制作侧·美术 | Python 像素绘制管线，17 个脚本，产出 212 张 PNG | `tools/art-pipeline/` |
| 制作侧·语音 | gptsovits 离线合成，10 条台词，分段合成 + 响度归一 | `tools/voice/`、`Resources/Audio/Voice/` |
| 编程侧 | AI 辅助写代码与查 bug；但**架构与取舍由我拍板**（ADR 是决策记录，不是 AI 生成物） | `docs/adr/0001`–`0004` |

**必须自己回答的一句**：「理解 AI 辅助生成的代码」这条怎么自证——建议点名一个你亲自改过的 AI 产出（比如 `LexPlanner` 的评分函数、或 `DoozyViewHost` 的反射兜底），说明你改了什么、为什么。

**别写**：把「用了 ChatGPT / Claude」当卖点。验收方关心的是你有没有失控。

---

### §6 交付与验收

**必须回答**：四样材料在哪？怎么跑起来？哪些是明知没做的？

| 项 | 位置 |
| --- | --- |
| APK | `Build/Lingjing.apk`（包名 `cn.lingjing.eous`，minSdk 22，ARM64，调试证书侧载） |
| 演示视频 | 待录（分镜见 `docs/delivery/demo-video-script.md`） |
| Plastic 项目与节点 | 云仓 `lingjing_2@tj_eafen405@cloud.cn`；人读清单见 `docs/delivery/submission-notes.md` |
| 开发工作文档 | 本文改写的正文 |

**操作一句话**：按住屏幕拖动移动；右下「代行」满槽可放；右下「正反切」翻转刀环。

**未做项**（写出来是加分，不是减分）：天赋页、飞刀内圈与磨损、暴击、第二关的恶名以骸·骤雨、地面掉落的「刀」、BGM——按 `docs/design/game-design.md` §0.1 冻结。

---

## 3. 两个敏感项，怎么写才不失分

### 3.1 Plastic 节点是补录的（这条最容易被挑）

事实：云仓 2026-11-01 才接通；**节点 3–24 是 11-01 用 Git 快照回放补录的**，文件树为真、时间戳不是真实开发时刻；节点 25 起恢复「先 `cm checkin` 再 `git commit`」。

建议写法（三句话，不辩解）：
1. 说明为什么：云端仓库接通得晚，前期开发只存在于 Git 里。
2. 说明怎么补的：按 Git 提交逐个还原工作区再 `cm checkin`，**每个节点的文件树是当时真实状态**。
3. 说明后果：这 22 个节点的时间戳集中在 11-01 19:27–19:33，不是真实开发时刻；Git 镜像保留了真实时间戳可对照。

**不要**：不提这件事等对方从 `cm log` 里自己发现（22 个节点挤在 6 分钟内，一眼可见）；也不要拿它当解释一切的理由。

### 3.2 「不要用 AI 大段生成」怎么落地

- 正文自己写，AI 只用来查事实（本文就是这个用途）。
- 保留**你的判断痕迹**：犹豫过什么、否决过什么方案、改主意的地方。这些是 AI 写不出来的。
- ADR 里已有 `Considered Options`（否决方案），可以当作「判断痕迹」的索引。

---

## 4. 交稿前自检（逐条打勾）

- [ ] 全文 1200–1800 字，能两页内看完
- [ ] §3 三个决定每个都有「原来 → 问题 → 改成什么 → 代价」
- [ ] §4 三个坑都有「怎么查」这一步，且提到具体数字或具体文件
- [ ] 至少一处写出「我否决过什么方案」
- [ ] §5 点名一个你亲手改过的 AI 产出
- [ ] §6 四样材料位置齐全，未做项写了
- [ ] Plastic 补录这件事有交代（3 句话即可）
- [ ] 随机挑三段读一遍：像不像人写的？像不像 README？

---

## 5. 素材索引（写的时候按需查）

| 要找什么 | 去哪 |
| --- | --- |
| 玩法与规则 | `docs/design/game-design.md` |
| 术语 | `GLOSSARY.md` |
| 技术决策与否决方案 | `docs/adr/0001`–`0004` |
| 数值 | `Assets/_Project/Scripts/Core/Tuning.cs`、`AI/EnemyTuning.cs`、`Level/Growth.cs` |
| 关卡与波次 | `Assets/_Project/Data/levels.json` |
| AI 代行的研究过程 | `docs/research/fairy-*.md`、`prototype/battlesim/` |
| 第三方资产踩坑 | `docs/agents/third-party-assets.md` |
| 版本管理与补录 | `docs/agents/plastic-workflow.md`、`docs/adr/0001` |
| 节点清单 | `docs/delivery/submission-notes.md` |
| 真实提交时间线 | `git log --format='%ad %s' --date=format:'%m-%d %H:%M'`（77 次提交：10-29 6 / 10-30 6 / 10-31 45 / 11-01 19 / 11-07 1） |
| 探针能复现什么 | `Assets/_Project/Scripts/Editor/*Probe.cs`、`KernelSmokeTest.cs` |
