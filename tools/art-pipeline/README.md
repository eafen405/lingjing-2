# 像素素材管线

一条管线出四批素材：**伊埃斯**（主角）、**四种以骸**（尾灯 / 撞针 / 钉枪 / 坠物）、
**空洞地形**（边界 / 障碍 / 传送门）与 **T23 小件**（飞刀 / 掉落 / 警示区 / 特效）。

角色共用 `artlib.py`，造型层各自独立——伊埃斯是「蛋壳 + 贴在表面的部件」，
用以方位角投影最省事；以骸是有厚度、有腿、有前伸武器的机体，用一台极简三维相机。
地形与小件只有平面图形，直接走 `artlib` 的像素对齐原语。

## 现行入口

```
python build_eous.py        # 伊埃斯：5 朝向 × (idle 2 / walk 4) + death 4，共 34 帧
python build_malice.py      # 四种小怪 + 两个 Boss，共 89 帧
python build_malice.py chain   # 只重出某一个实体
python build_terrain.py     # 空洞地形：边界 8 + 挡路物 2 + 传送门 4 帧，共 14 张
python build_misc.py        # T23：飞刀 8 + 掉落 6 + 警示区 2 + 特效 14 帧
```

输出：
- `Assets/_Project/Art/Characters/eous/…`（64×64，图集 + manifest）
- `Assets/_Project/Art/Enemies/{entity}/…`（64×64，坠物 64×96，图集 + manifest）
- `Assets/_Project/Art/Bosses/{entity}/…`（96×96，图集 + manifest）
- `Assets/_Project/Art/Terrain/…`（边界 32×32、挡路物 64×64、传送门 96×96 + `tileset.json`）
- `Assets/_Project/Art/Items/…`（飞刀 16×16、掉落 32×32 + `items.json`）
- `Assets/_Project/Art/Fx/…`（警示区 32×32、命中/碎裂/拾取/代行 + `effects.json`）
- `out/master/…`（伊埃斯 128×128 母版）、`out/sheet_*.png` / `out/mob_*.png`（审阅用）
- `out/mob_review.png`（全部并排）、`out/mob_scale.png`（与伊埃斯同世界尺度对照）
- `out/terrain_review.png`（地形全部素材）、`out/terrain_room.png`（合成场地，透明底）
- `out/misc_items.png` / `out/misc_fx.png` / `out/misc_scene.png`（T23 小件的审阅与实景）

## 分层

| 文件 | 职责 |
| --- | --- |
| `artlib.py` | 分辨率无关画布：超采样光栅化 → 主导色降采样 → OKLab 色板吸附 |
| `eous_model.py` | 伊埃斯的伪 3D 模型：蛋形身体、方位角投影、Pose 形变 |
| `eous_draw.py` | 按 Pose 把伊埃斯画出来（耳/面罩/围巾/外套/脚/「01」） |
| `build_eous.py` | 批量渲染、拼图集、写 manifest |
| `malice_model.py` | 以骸共用层：(x,高,前后) 相机、多边形截面棱柱/锥、明度分档、死亡调色、统一收边，以及画具（`leg`/`eye`/`disc`/`burst`/`glow_pts`） |
| `malice_draw.py` | 四种小怪的造型与姿态表 |
| `boss_draw.py` | 两个恶名以骸的造型与姿态表 |
| `build_malice.py` | 小怪 + Boss 的批量渲染、拼图集、写 manifest、出审阅图 |
| `terrain_draw.py` | 空洞地形：边界墙掩码、挡路物、传送门团块 |
| `build_terrain.py` | 地形的批量渲染、拼图集、写 `tileset.json`、出审阅图与合成场地 |
| `misc_draw.py` | T23 小件：飞刀旋转帧、掉落、九宫格警示区、四种特效 |
| `build_misc.py` | 小件的批量渲染、拼图集、写 `items.json` / `effects.json`、出审阅图与实景 |
| `eous_master.py` | 伊埃斯早期的单体版本，保留作参考 |

## 形状怎么改

**伊埃斯**：控制点都在 body units（x 以身体中线为 0，y 耳尖 0 → 脚底 1.0），
只改 `eous_draw.py` / `eous_model.py` 里的常量，5 个朝向和所有帧一起重算。

**以骸**：骨架写成 `(x, 高, 前后)` 空间点，`Mob.pt()` 投影出屏幕坐标；
`m.solid()` 画多边形截面的棱柱（顶面 + 逐个可见侧面，按法线分三档明度），
`m.cone()` 画收尖的锥。改尺寸只要改 `ENTITY` 里的 `scale`（位置与半径一起放大），
改完跑一次 `build_malice.py`，输出的 `bbox` 必须留在画布内 2px 以上。

## 已废弃

`draw_eous.py` / `draw_eous_64.py` 是最早的逐像素摆点方案（32×32 / 64×64），
细节量不够、无法覆盖多朝向，仅作历史留存，不要再往里加东西。

`palette_generator.py` 是早期的 32 色色板生成器，内置色值与现有素材不一致，
已加 `--force` 拦截；色板的唯一来源是各绘制模块的色板常量。
