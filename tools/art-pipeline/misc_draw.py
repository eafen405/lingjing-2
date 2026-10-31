#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""T23 素材：飞刀、三种掉落、矩形警示区、四种特效。

为什么放在一个模块
------------------
这些东西共同点是「单帧、小尺寸、只为读得清」：没有骨架、没有朝向表、不需要
伪 3D。真正需要复用的是 ``artlib`` 的画布与色板吸附，不是造型层。所以这一批
单独一个薄模块，不再往 eous/malice/terrain 里塞。

尺寸依据
--------
* 飞刀 16×16：规格 §2 已定，pivot (8,8)，**8 个旋转帧**（引擎按角度取帧，
  比旋转贴图更脆，像素不会被重采样糊掉）。
* 掉落 32×32：场地里要和 56px 高的伊埃斯同屏，16×16 会小到看不清；pivot
  (16,6)，脚底落在 32×32 画布底往上 6px。
* 警示区 32×32 九宫格（border 8）：矩形尺寸由代码拉伸，中间走 Tiled 平铺，
  所以内部抖动图案的周期必须整除 16。
* 特效：命中 24×24×3、碎裂 16×16×4、拾取 24×24×3、代行进入/结束各 128×128×4。

色板
----
**不新增颜色。** 全部复用既有素材的色：钢色用台地灰阶、刀柄用以骸黑青、
血包用兜帽米白 + 预警红、能量球用眼睛酸绿、代行用同一套绿。
"""

from __future__ import annotations

import math

from PIL import Image

from artlib import Canvas, Palette, clean_specks, hex2rgb, to_pixels
from eous_model import EOUS_COLORS
from malice_model import MALICE_EXTRA
from terrain_draw import TERRAIN_COLORS

# --------------------------------------------------------------------------- 色板

# 从三套既有色板里挑，键名统一，避免同色异名
_PICK = {
    # 描边／米白（伊埃斯）
    "outline": EOUS_COLORS,
    "hood_light": EOUS_COLORS,
    "hood": EOUS_COLORS,
    "hood_shade": EOUS_COLORS,
    "hood_deep": EOUS_COLORS,
    # 酸绿（伊埃斯眼睛 = 以太色）
    "eye": EOUS_COLORS,
    "eye_hi": EOUS_COLORS,
    "eye_dark": EOUS_COLORS,
    # 围巾橙（普通预警）
    "scarf_light": EOUS_COLORS,
    "scarf_deep": EOUS_COLORS,
    # 预警红（危险预警 / 血包十字）
    "warn": MALICE_EXTRA,
    "warn_hi": MALICE_EXTRA,
    "warn_deep": MALICE_EXTRA,
    # 黑青（刀柄、暗部）
    "shell": MALICE_EXTRA,
    "shell_deep": MALICE_EXTRA,
    "shell_light": MALICE_EXTRA,
    # 冷灰蓝（钢色，与地形同一套灰阶）
    "steel": TERRAIN_COLORS,
    "steel_hi": TERRAIN_COLORS,
    "steel_dark": TERRAIN_COLORS,
}
_SRC = {"steel": "hi", "steel_hi": "rim", "steel_dark": "shade"}

MISC_COLORS = {k: _PICK[k][_SRC.get(k, k)] for k in _PICK}

PALETTE = Palette(MISC_COLORS)
C = {k: hex2rgb(v) + (255,) for k, v in MISC_COLORS.items()}


def _n(pts, w: float, h: float | None = None):
    h = h if h is not None else w
    return [(x / w, y / h) for x, y in pts]


def _rot(pts, cx: float, cy: float, deg: float):
    a = math.radians(deg)
    ca, sa = math.cos(a), math.sin(a)
    out = []
    for x, y in pts:
        dx, dy = x - cx, y - cy
        out.append((cx + dx * ca - dy * sa, cy + dx * sa + dy * ca))
    return out


def finish(c: Canvas, keep: int = 1) -> Image.Image:
    """``keep`` 是孤点清理阈值：特效里的 1–2px 火星要留，所以给 0。"""
    return clean_specks(to_pixels(c, PALETTE), keep)


# --------------------------------------------------------------------------- 飞刀

KNIFE = 16
KNIFE_FRAMES = 8

# 局部坐标里刀尖朝上（-y），之后整把刀绕画布中心旋转。
# 16px 下刀刃必须给到 4px 宽，否则 1:1 就是一根白线，看不出是刀。
_BLADE = [(0.0, -7.2), (2.0, -1.0), (1.8, 1.0), (-1.8, 1.0), (-2.0, -1.0)]
_SPINE = [(0.0, -7.2), (-2.0, -1.0), (-0.2, 1.0)]        # 背光的一侧
_EDGE = [(0.3, -6.4), (2.0, -1.0), (1.8, 1.0)]           # 受光的一侧
_GUARD = [(-3.4, 1.0), (3.4, 1.0), (3.4, 2.6), (-3.4, 2.6)]
_GRIP = [(-1.3, 2.6), (1.3, 2.6), (1.3, 6.0), (-1.3, 6.0)]


def _dagger(c: Canvas, cx: float, cy: float, deg: float, k: float, size: int) -> None:
    """按中心点、角度、缩放画一把刀；飞刀与掉落共用同一套轮廓。"""
    def r(pts):
        return _n(_rot([(cx + x * k, cy + y * k) for x, y in pts], cx, cy, deg), size)

    c.poly(r(_BLADE), fill=C["steel_hi"])
    c.poly(r(_SPINE), fill=C["steel_dark"])
    c.line(r(_EDGE), C["hood_light"], w=1.0, cap=False)
    c.poly(r(_GUARD), fill=C["steel_dark"])
    c.poly(r(_GRIP), fill=C["shell"])
    c.line(r([(-0.8, 3.2), (0.8, 3.2), (0.8, 5.6), (-0.8, 5.6)]),
           C["shell_light"], w=1.0, cap=False)


def draw_knife(i: int) -> Image.Image:
    c = Canvas(KNIFE)
    cx = cy = KNIFE / 2
    _dagger(c, cx, cy + 0.4, i * 360.0 / KNIFE_FRAMES, 0.92, KNIFE)
    return finish(c)


# --------------------------------------------------------------------------- 掉落

PICKUP = 32
PICKUP_PIVOT = (16, 6)


def _pickup_outline(c: Canvas, pts) -> None:
    """1px 描边：规格 §4 允许可拾取物描边。"""
    c.stroke(_n(pts, PICKUP), C["outline"], w=1.0, closed=True)


def draw_pickup_health(hi: bool = False) -> Image.Image:
    """血包：一枚立方体，顶面画红十字。"""
    c = Canvas(PICKUP)
    top = [(16, 8), (25, 13), (16, 18), (7, 13)]
    left = [(7, 13), (16, 18), (16, 26), (7, 21)]
    right = [(25, 13), (16, 18), (16, 26), (25, 21)]

    c.poly(_n(left, PICKUP), fill=C["hood_shade"])
    c.poly(_n(right, PICKUP), fill=C["hood_deep"])
    c.poly(_n(top, PICKUP), fill=C["hood_light"] if hi else C["hood"])

    cross = C["warn"] if hi else C["warn_deep"]
    c.line(_n([(16, 10.4), (16, 15.6)], PICKUP), cross, w=2.0, cap=True)
    c.line(_n([(13.4, 13.0), (18.6, 13.0)], PICKUP), cross, w=2.0, cap=True)

    _pickup_outline(c, [(16, 8), (25, 13), (25, 21), (16, 26), (7, 21), (7, 13)])
    return finish(c)


def draw_pickup_energy(hi: bool = False) -> Image.Image:
    """能量球：悬浮的以太球，外面套一道横环。"""
    c = Canvas(PICKUP)
    # 横环：先整圈画在球后面，再把前半圈压回球面上，才有「套着」的层次
    c.ellipse(16 / PICKUP, 21 / PICKUP, 11 / PICKUP, 3.6 / PICKUP,
              stroke=C["eye_dark"], w=1.0)
    c.circle(16 / PICKUP, 16 / PICKUP, 7.4 / PICKUP, fill=C["eye_dark"])
    c.circle(16 / PICKUP, 16 / PICKUP, 5.8 / PICKUP,
             fill=C["eye_hi"] if hi else C["eye"])
    c.circle(13.4 / PICKUP, 13.4 / PICKUP, 2.0 / PICKUP, fill=C["hood_light"])
    c.arc(16 / PICKUP, 21 / PICKUP, 11 / PICKUP, 3.6 / PICKUP, 10, 170,
          C["eye_dark"], w=1.0)
    return finish(c)


def draw_pickup_blade(hi: bool = False) -> Image.Image:
    """掉落的飞刀：斜躺在地上的一把刀，轮廓和环绕用的那把完全一致。"""
    c = Canvas(PICKUP)
    _dagger(c, 16.0, 17.5, 38.0, 1.55, PICKUP)
    if hi:
        c.circle(16 / PICKUP, 17.5 / PICKUP, 1.2 / PICKUP, fill=C["eye_hi"])
    return finish(c)


# --------------------------------------------------------------------------- 警示区

WARN = 32
WARN_BORDER = 8


def draw_warn(tier: str) -> Image.Image:
    """九宫格警示区：2px 亮边 + 8px 周期的点阵填充。

    tier = "normal"（橙，普通落区）/ "danger"（红，Boss 落区）。
    点是 2×2、每 8px 一颗——铺满一块 224×128 的落区时刚好读成「虚线框里
    有一片网」，而不是一整块晃眼的色块。周期 8 整除中心格 16，Tiled 平铺不错位。
    """
    bright = C["scarf_light"] if tier == "normal" else C["warn"]
    c = Canvas(WARN)

    for x in range(WARN_BORDER, WARN - WARN_BORDER, 8):
        for y in range(WARN_BORDER, WARN - WARN_BORDER, 8):
            c.px_rect(x, y, 2, 2, bright)

    c.px_rect(0, 0, WARN, 2, bright)
    c.px_rect(0, WARN - 2, WARN, 2, bright)
    c.px_rect(0, 0, 2, WARN, bright)
    c.px_rect(WARN - 2, 0, 2, WARN, bright)
    return finish(c)


# --------------------------------------------------------------------------- 特效

HIT = 24
BREAK = 16
PICKUP_FX = 24
PROXY = 128


def draw_hit(f: int) -> Image.Image:
    """命中：白色十字爆点 -> 八角迸开 -> 只剩碎点。全用角色白 + 酸绿两色。"""
    c = Canvas(HIT)
    cx = cy = HIT // 2
    if f == 0:
        c.px_rect(cx - 2, cy - 2, 4, 4, C["hood_light"])
        for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            c.px_rect(cx + dx * 4 - (1 if dx else 0), cy + dy * 4 - (1 if dy else 0),
                      2 if dx else 1, 2 if dy else 1, C["eye_hi"])
    elif f == 1:
        c.px_rect(cx - 2, cy - 2, 4, 4, C["hood_light"])
        for i in range(8):
            a = math.pi * i / 4
            r0, r1 = 3.0, 8.0
            c.line(_n([(cx + r0 * math.cos(a), cy + r0 * math.sin(a)),
                       (cx + r1 * math.cos(a), cy + r1 * math.sin(a))], HIT),
                   C["hood_light"] if i % 2 else C["eye_hi"], w=1.0)
    else:
        for i in range(8):
            a = math.pi * i / 4 + 0.2
            r = 9.0
            c.px_rect(int(round(cx + r * math.cos(a))) - 1,
                      int(round(cy + r * math.sin(a))) - 1, 2, 2,
                      C["eye_dark"] if i % 2 else C["eye"])
    return finish(c, 0)


def draw_break(f: int) -> Image.Image:
    """飞刀碎裂：刀身先闪白，再炸成三片往外飞。"""
    c = Canvas(BREAK)
    cx = cy = BREAK / 2
    if f == 0:
        c.poly(_n([(cx, cy - 7), (cx + 2, cy), (cx - 2, cy)], BREAK),
               fill=C["hood_light"])
        c.poly(_n([(cx - 1.4, cy), (cx + 1.4, cy), (cx + 1, cy + 6), (cx - 1, cy + 6)],
                  BREAK), fill=C["shell"])
        return finish(c, 0)

    spread = (2.8, 4.2, 5.2)[min(f - 1, 2)]
    for k, (dx, dy, rot) in enumerate(((-1.0, -1.0, -30), (1.0, -0.6, 25),
                                       (0.2, 1.0, 10))):
        px = cx + dx * spread
        py = cy + dy * spread
        size = max(1.1, 2.2 - 0.45 * (f - 1))
        shard = _rot([(px, py - size * 1.8), (px + size, py + size),
                      (px - size, py + size)], px, py, rot)
        c.poly(_n(shard, BREAK),
               fill=C["steel_hi"] if f == 1 else C["steel_dark"])
    return finish(c, 0)


def draw_pickup_fx(f: int) -> Image.Image:
    """拾取：一圈往外扩的光环 + 四粒上浮的碎光。"""
    c = Canvas(PICKUP_FX)
    cx = cy = PICKUP_FX / 2
    r = (4.0, 7.0, 10.0)[f]
    c.circle(0.5, 0.5, r / PICKUP_FX,
             stroke=C["eye_hi"] if f < 2 else C["eye_dark"], w=1.0)
    for i in range(4):
        a = math.pi / 2 * i + math.pi / 4
        px = cx + r * 0.9 * math.cos(a)
        py = cy + r * 0.9 * math.sin(a) - f * 2.5
        c.px_rect(int(round(px)), int(round(py)), 1, 1,
                  C["hood_light"] if f < 2 else C["eye_dark"])
    return finish(c, 0)


def draw_proxy(f: int, entering: bool = True) -> Image.Image:
    """代行进入 / 结束：酸绿双环 + 八道刻度。

    128×128（4 个世界单位），刚好套住 2 单位高的伊埃斯。
    粗环 2px、细环 1px；不做半透明，靠环的疏密表现「在消失」。
    """
    c = Canvas(PROXY)
    t = f / 3.0
    # 最外圈 50 + 刻度 11 = 61，留 3px 不贴边
    r = (18 + 32 * t) if entering else (50 - 32 * t)
    ring = C["eye"] if entering else C["eye_dark"]
    hi = C["eye_hi"] if entering else C["eye"]

    c.circle(0.5, 0.5, r / PROXY, stroke=ring, w=2.0)
    c.circle(0.5, 0.5, (r - 6) / PROXY, stroke=hi, w=1.0)
    for i in range(8):
        a = math.pi * i / 4 + (0.2 if entering else -0.2)
        c.line(_n([(64 + (r + 3) * math.cos(a), 64 + (r + 3) * math.sin(a)),
                   (64 + (r + 11) * math.cos(a), 64 + (r + 11) * math.sin(a))],
                  PROXY), hi if i % 2 else ring, w=2.0)
    return finish(c, 0)


# --------------------------------------------------------------------------- 出口

ITEMS = {
    "knife": {"frames": KNIFE_FRAMES, "size": (KNIFE, KNIFE), "pivot": (8, 8),
              "fn": draw_knife},
    "pickup_health": {"frames": 2, "size": (PICKUP, PICKUP), "pivot": PICKUP_PIVOT,
                      "fn": draw_pickup_health},
    "pickup_energy": {"frames": 2, "size": (PICKUP, PICKUP), "pivot": PICKUP_PIVOT,
                      "fn": draw_pickup_energy},
    "pickup_blade": {"frames": 2, "size": (PICKUP, PICKUP), "pivot": PICKUP_PIVOT,
                     "fn": draw_pickup_blade},
}

FX_GROUPS = {
    "warn": {"size": (WARN, WARN), "loop": False, "fps": 0,
             "frames": {"warn_normal": lambda: draw_warn("normal"),
                        "warn_danger": lambda: draw_warn("danger")}},
    "hit": {"size": (HIT, HIT), "loop": False, "fps": 20,
            "frames": {f"hit_{i:02}": (lambda k=i: draw_hit(k)) for i in range(3)}},
    "break": {"size": (BREAK, BREAK), "loop": False, "fps": 18,
              "frames": {f"break_{i:02}": (lambda k=i: draw_break(k))
                         for i in range(4)}},
    "pickup": {"size": (PICKUP_FX, PICKUP_FX), "loop": False, "fps": 20,
               "frames": {f"pickup_fx_{i:02}": (lambda k=i: draw_pickup_fx(k))
                          for i in range(3)}},
    "proxy": {"size": (PROXY, PROXY), "loop": False, "fps": 14,
              "frames": {**{f"proxy_enter_{i:02}": (lambda k=i: draw_proxy(k, True))
                            for i in range(4)},
                         **{f"proxy_exit_{i:02}": (lambda k=i: draw_proxy(k, False))
                            for i in range(4)}}},
}

__all__ = ["MISC_COLORS", "ITEMS", "FX_GROUPS", "PICKUP", "PICKUP_PIVOT",
           "WARN", "WARN_BORDER", "PROXY", "draw_knife", "draw_pickup_health",
           "draw_pickup_energy", "draw_pickup_blade", "draw_warn", "draw_hit",
           "draw_break", "draw_pickup_fx", "draw_proxy"]
