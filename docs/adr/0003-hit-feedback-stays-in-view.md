# 打击感只发生在表现层，模拟不感知

> 状态：**部分过期**（2026-11-04 标注）：第 3 条「顿帧只在击杀」与「`LevelProbe` 逐 tick 断言」的表述已与实现不符，其余结论仍成立。偏差见文末「与实现的偏差（2026-11-04）」。

命中火花、受击闪白、顿帧、震屏、受击抖动全部是 `BattleRunner`（表现层）对 `BattleWorld` 事件的反应；模拟层（Core / Combat / AI）不认识其中任何一个。世界仍然是唯一权威：打击感不改位置、不改 HP、不改命中冷却、不动随机序列。

## 背景

`hits/s = N·|ω_rel|/2π` 这条规则要求「伤害只由相对速度决定」。任何加在模拟里的额外位移（击退、顿帧时冻结冷却、震屏带动的坐标抖动）都会偷偷改变相对速度，把走位收益搅浑；同时 `LevelProbe` 靠「两次运行逐 tick 一致」做确定性断言，模拟里多一个表现变量，这条断言就废了。

而打击感恰恰全都想要改这些东西：闪白要改颜色、抖动要改位置、顿帧要停时间。

## 决定

1. **抖动只偏移渲染位置**：以骸受击时精灵 ±1px 抖 0.1s，`Enemy.Pos` 一动不动。否决了「击退」——那是给模拟加了第二个位移源。
2. **闪白走 shader + MaterialPropertyBlock**：一个约 20 行的 `Lingjing/SpriteFlash`，per-renderer 写 `_Flash`，不换材质实例、不破合批。shader 放 `Resources/Shaders/`，避免被构建裁掉。
3. **顿帧只在击杀**：`Time.timeScale = 0` 3–4 帧（60–70ms）。命中不给顿帧——静止时单只以骸就有约 1.6 次命中/秒，多只贴环时每击都停会变成幻灯片，而且会掩盖「顺扫更疼」的节奏感。
4. **震屏只震世界相机，且只加整数像素**：相机本来就吸附 1/32，整数像素震动不会让画面糊；衰减走不缩放时间，所以顿帧期间震动继续，这一下才够实。
5. **血条归表现层**：以骸血条是贴头的世界层像素条，数据只读 `Enemy.Hp`；恶名以骸的顶部血条在 Fairy 层。

## Considered Options

- **每击顿帧**：最直接，但命中频率太高。否决。
- **击退**：反馈最"重"，但要改模拟位置与相对速度的输入。否决。
- **白剪影覆盖帧**：不需要 shader，但每种以骸都要另画一套白色剪影，美术成本比 20 行 shader 高。否决。
- **粒子系统 / 后处理**：`docs/specs/art-assets.md` 的 Out of Scope 已排除，且像素风里逐帧小图更贴合。否决。

## Consequences

- 关掉表现层（无头探针、纯模拟跑分）时打击感完全不存在，模拟结果一字不差 —— `LevelProbe` 的确定性断言继续成立。
- 加新特效只需要往 `effects.json` 加一组、`ArtLibrary` 重跑一次，不动模拟与主循环。
- 顿帧期间 UI 动画与精灵动画一起停（同一个 `timeScale`）。将来若要做「世界停、UI 继续动」，得给表现层单独一个时钟，不在本 ADR 范围内。

## 与实现的偏差（2026-11-04）

- **顿帧不再只在击杀**：ADR-0004 之后命中也有顿帧。`Assets/_Project/Scripts/View/BattleRunner.cs` 中击杀顿帧 `KillFreezeSeconds = 0.06f`（约 3–4 帧），命中顿帧 `HitFreezeSeconds = 0.035f`（约 2 帧）、限频 `HitFreezeCooldownSeconds = 0.12f`。本文第 3 条「命中不给顿帧」不再成立。
- 「顿帧只发生在表现层、模拟不感知」这一层结论不变：`Freeze()` 只改 `Time.timeScale`，模拟层（Core / Combat / AI）不读它。
- **确定性断言的位置与口径**：`Assets/_Project/Scripts/Editor/LevelProbe.cs:67` 比较的是两次运行的聚合量——`kills`、`ticks`、`Pos`；逐 tick 哈希断言在 `Assets/_Project/Scripts/Editor/KernelSmokeTest.cs`（`TickHasher.HashTick`，第 48–57 行）。本文「`LevelProbe` 靠逐 tick 一致做断言」的表述不准确，实际是「流程层比聚合量、内核层比逐 tick 哈希」；确定性结论本身成立。
- 第 1 / 2 / 4 / 5 条（抖动只偏移渲染、闪白走 shader、震屏只加整数像素、血条归表现层）与实现一致。
