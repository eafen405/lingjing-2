#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""空洞地形：边界墙 + 两种挡路物 + 传送门。

只画三样
--------
* ``wall``    边界 8 块（4 直边 + 4 外角），把场地圈起来
* ``prop``    两种挡路物，64×64：以太晶簇 / 矮墙残段
* ``portal``  空洞传送门，96×96、4 帧循环

**没有地板、没有背景色。** 场地里面就是空的，靠边界、障碍物和角色自己
把空间读出来；铺装、贴花、天穹、城市剪影都不做。这样既省素材，也不会
让画面比玩法更吵。

尺寸为什么这么定
----------------
伊埃斯在 PPU 32 下高 56px（1.75 世界单位），以骸 44–54px。地形物件如果按
32×32 画，撑满也只有半个角色高，站在旁边像地上的花纹而不是挡路的实体，
所以障碍与传送门按「和角色同量级」给到 64 与 96。

技术约定
--------
* 与角色共用 ``artlib``：超采样 -> 主导色降采样 -> 色板吸附，
  输出天然无抗锯齿、alpha 只有 0/255。
* 倒角走 ``px_rect``，落点是最终像素网格，拼图不会错位。
* 色板不再凑「32 色」这个数字：新增色在 ``TERRAIN_COLORS`` 登记即可。
"""

from __future__ import annotations

import math

from PIL import Image

from artlib import Canvas, Palette, clean_specks, hex2rgb, to_pixels

TILE = 32
PROP = 64
PORTAL_SIZE = 96
PORTAL_FRAMES = 4

# --------------------------------------------------------------------------- 色板

TERRAIN_COLORS = {
    # 虚空：只用于审阅图的底，不是素材的一部分
    "void": "#0b0f12",
    "chasm": "#161d22",
    # 冷灰蓝：暗边 / 墙身 / 亮边
    "grout": "#232e36",
    "shade": "#384651",
    "base": "#556b78",
    "light": "#77909c",
    "hi": "#97aeb8",
    "rim": "#b4c9d2",
    # 以太晶簇：沿用伊埃斯眼睛的酸绿，与以骸的缝同一套语言
    "shell": "#1a232d",
    "shell_lt": "#33455a",
    "eye": "#c9f24d",
    "eye_hi": "#e8ffa6",
    # 传送门：画面里唯一的洋红/青，参考 source/reference/空洞传送门参考.png
    "portal_deep": "#2a0f3a",
    "portal_body": "#6a2fa8",
    "portal_light": "#a35ce8",
    "portal_cyan": "#5ce0ff",
    "portal_pink": "#ff7ad9",
    "portal_rim": "#ffe6f7",
}

PALETTE = Palette(TERRAIN_COLORS)
C = {k: hex2rgb(v) + (255,) for k, v in TERRAIN_COLORS.items()}


def _px(pts, size: int):
    """像素坐标 -> artlib 的归一化坐标。"""
    return [(x / size, y / size) for x, y in pts]


def finish(c: Canvas) -> Image.Image:
    return clean_specks(to_pixels(c, PALETTE), 1)


# --------------------------------------------------------------------------- 边界

N, E, S, W = 1, 2, 4, 8
OPPOSITE = {N: S, S: N, W: E, E: W}

# 边界是一道实心墙，不是一个框：外沿按光照分亮/暗，内沿留一道转折。
# 每块瓦片只有「真正朝外的那一侧」和它对面才画边——上下左右全画就又变回网格了。
OUTER = {N: "hi", W: "hi", S: "grout", E: "grout"}
INNER = {N: "chasm", W: "chasm", S: "light", E: "light"}


def _edge_band(c: Canvas, side: int, color, width: int = 2) -> None:
    for k in range(width):
        if side == N:
            c.px_rect(0, k, TILE, 1, color)
        elif side == S:
            c.px_rect(0, TILE - 1 - k, TILE, 1, color)
        elif side == W:
            c.px_rect(k, 0, 1, TILE, color)
        else:
            c.px_rect(TILE - 1 - k, 0, 1, TILE, color)


def draw_wall(mask: int) -> Image.Image:
    c = Canvas(TILE)
    c.px_rect(0, 0, TILE, TILE, C["shade"])
    for side in (N, E, S, W):
        if not mask & side:
            continue
        _edge_band(c, side, C[OUTER[side]], 2)
        _edge_band(c, OPPOSITE[side], C[INNER[side]], 1)
    return finish(c)


WALL_MASKS = {
    "wall_n": N,
    "wall_e": E,
    "wall_s": S,
    "wall_w": W,
    "wall_nw": N | W,
    "wall_ne": N | E,
    "wall_sw": S | W,
    "wall_se": S | E,
}


# --------------------------------------------------------------------------- 挡路物


def draw_crystal() -> Image.Image:
    """以太晶簇：四根黑青晶体扎在压暗的底上，下半段透酸绿。

    剪影是「一高两矮的尖」，和方头的矮墙残段不会混。
    """
    c = Canvas(PROP)

    c.poly(_px([(6, 56), (58, 56), (58, 61), (6, 61)], PROP), fill=C["shade"])
    c.px_rect(6, 56, 52, 1, C["grout"])
    c.px_rect(6, 61, 52, 1, C["chasm"])

    shards = [
        # 轮廓, 受光面, 绿色缝（只留下半段：整根发亮会变成霓虹棒）
        ([(10, 56), (14, 27), (20, 38), (21, 56)],
         [(14, 27), (16, 38), (16, 56), (10, 56)],
         [(15, 44), (16, 55)]),
        ([(21, 56), (26, 12), (33, 26), (34, 56)],
         [(26, 12), (28, 27), (28, 56), (21, 56)],
         [(27.5, 38), (28.5, 55)]),
        ([(33, 56), (39, 31), (45, 43), (46, 56)],
         [(39, 31), (41, 44), (41, 56), (33, 56)],
         [(40, 45), (41, 55)]),
        ([(46, 56), (50, 42), (54, 50), (55, 56)],
         [(50, 42), (51, 51), (51, 56), (46, 56)],
         [(50.5, 49), (51, 55)]),
    ]
    for body, lit, _seam in shards:
        c.poly(_px(body, PROP), fill=C["shell"])
        c.poly(_px(lit, PROP), fill=C["shell_lt"])
    for _body, _lit, seam in shards:
        c.line(_px([(round(x), round(y)) for x, y in seam], PROP),
               C["eye"], w=1.0, cap=True)
    c.px_rect(27, 40, 1, 2, C["eye_hi"])

    return finish(c)


def draw_block() -> Image.Image:
    """矮墙残段：一段塌掉的砌体。顶面受光、正面压暗，用来切走位。

    场地没有地板、背景是近黑的清屏色，所以挡路物必须先有一圈亮边（``rim``）把轮廓从背景里
    剥出来，再用比墙更亮的两级面区分顶/正面；否则在手机上就是一团和背景糊在一起的黑色。
    """
    c = Canvas(PROP)

    top = [(6, 30), (12, 26), (20, 30), (28, 25), (36, 29), (44, 24),
           (52, 29), (58, 27), (58, 40), (6, 40)]
    front = [(6, 40), (58, 40), (58, 58), (6, 58)]

    # 1px 亮边：把同一组轮廓往四个方向各偏 1px 先画一遍，再由本体盖住中间。
    for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
        c.poly(_px([(x + dx, y + dy) for x, y in top], PROP), fill=C["rim"])
        c.poly(_px([(x + dx, y + dy) for x, y in front], PROP), fill=C["rim"])

    c.poly(_px(top, PROP), fill=C["light"])
    c.poly(_px(front, PROP), fill=C["base"])

    c.line(_px(top[:6], PROP), C["rim"], w=1.0, cap=False)
    c.px_rect(6, 40, 1, 18, C["hi"])
    c.px_rect(6, 58, 53, 1, C["shade"])
    c.px_rect(57, 27, 1, 32, C["grout"])
    # 两道竖缝：暗示是砌起来的，不是一整块
    c.px_rect(24, 41, 1, 17, C["shade"])
    c.px_rect(42, 41, 1, 17, C["shade"])
    c.px_rect(7, 40, 50, 1, C["shade"])

    return finish(c)


# --------------------------------------------------------------------------- 传送门

# 每帧的气泡：(x, y, 半径, 颜色名)，均为 96px 画布上的归一化值。
BUBBLES = [
    [(0.36, 0.34, 0.042, "portal_cyan"), (0.62, 0.30, 0.030, "portal_pink"),
     (0.70, 0.55, 0.038, "portal_cyan"), (0.42, 0.66, 0.032, "portal_pink"),
     (0.54, 0.47, 0.024, "portal_pink")],
    [(0.44, 0.30, 0.036, "portal_pink"), (0.66, 0.41, 0.040, "portal_cyan"),
     (0.58, 0.66, 0.030, "portal_cyan"), (0.34, 0.52, 0.028, "portal_pink"),
     (0.52, 0.53, 0.024, "portal_light")],
    [(0.34, 0.43, 0.040, "portal_cyan"), (0.60, 0.33, 0.034, "portal_pink"),
     (0.68, 0.60, 0.028, "portal_cyan"), (0.46, 0.66, 0.036, "portal_pink"),
     (0.50, 0.47, 0.026, "portal_cyan")],
    [(0.40, 0.36, 0.030, "portal_pink"), (0.64, 0.37, 0.036, "portal_cyan"),
     (0.58, 0.62, 0.040, "portal_pink"), (0.36, 0.58, 0.028, "portal_cyan"),
     (0.50, 0.50, 0.022, "portal_pink")],
]

# 咬边用的暗团：贴着轮廓挖暗缺口，团块才不会像一颗光滑的球
CLUMPS = [(0.50, 0.14, 0.050), (0.86, 0.44, 0.044), (0.28, 0.80, 0.046),
          (0.78, 0.80, 0.036)]

# 体内的亮涌：(x, y, 半径)。位置偏、面积小，才是「翻涌」而不是同心圆
WISPS = [(0.40, 0.56, 0.115), (0.62, 0.38, 0.085), (0.55, 0.68, 0.070)]


def _blob_pts(frame: int, r_scale: float = 1.0, lobes: int = 9):
    """带相位漂移的九瓣团块：换帧时瓣的位置会动，读起来像在翻涌。"""
    pts = []
    for i in range(lobes):
        a = 2 * math.pi * i / lobes
        r = 0.400
        r += 0.048 * math.sin(3 * a + frame * 0.9)
        r += 0.030 * math.sin(5 * a - frame * 1.3)
        r *= r_scale
        pts.append((0.5 + r * math.cos(a), 0.5 + r * math.sin(a)))
    return pts


def draw_portal(frame: int) -> Image.Image:
    """洋红亮边 + 深紫内部 + 青粉气泡。

    内部压到最暗、亮的东西只留在边和气泡上——这样它是一团「发光的洞」，
    而不是一块紫色贴纸。
    """
    c = Canvas(PORTAL_SIZE)
    outer = _blob_pts(frame, 1.05)
    c.blob(outer, fill=C["portal_pink"], samples=18)

    def inside(t: Canvas) -> None:
        t.blob(_blob_pts(frame, 1.00), fill=C["portal_deep"], samples=18)
        for k, (wx, wy, wr) in enumerate(WISPS):
            drift = 0.018 * math.sin(frame * 1.2 + k * 2.1)
            t.circle(wx + drift, wy - drift, wr, fill=C["portal_body"])
        for k, (bx, by, br) in enumerate(CLUMPS):
            phase = (k + frame) % 4
            t.circle(bx + 0.012 * phase, by - 0.012 * phase, br,
                     fill=C["portal_deep"])
        for bx, by, br, name in BUBBLES[frame]:
            t.circle(bx, by, br * 1.3, fill=C[name])
            t.circle(bx - br * 0.42, by - br * 0.42, br * 0.32,
                     fill=C["portal_rim"])

    c.clipped(outer, inside)
    return finish(c)


# --------------------------------------------------------------------------- 出口

WALLS = {name: (lambda m=mask: draw_wall(m)) for name, mask in WALL_MASKS.items()}
PROPS = {"prop_crystal": draw_crystal, "prop_block": draw_block}

__all__ = [
    "TILE", "PROP", "PORTAL_SIZE", "PORTAL_FRAMES", "WALL_MASKS",
    "TERRAIN_COLORS", "draw_wall", "draw_crystal", "draw_block",
    "draw_portal", "WALLS", "PROPS",
]
