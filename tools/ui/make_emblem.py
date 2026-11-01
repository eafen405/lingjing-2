#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
按 OurFairy 的徽记几何稿生成 Fairy 徽记 PNG。

几何来源：OurFairy 仓库 work/implementation/evidence/ui-217/*.svg（108×108）：
  白环     r 30.66  描 4.675
  蓝渐变环 r 24.89  描 6.875   #3D8AE6 → #1A35D6
  瞳       r 11.14
  高光     r 4.675

这是 Fairy 层的非像素元素（game-design §10），不要按像素规则重绘。
产物：Assets/_Project/Resources/Fairy/fairy_emblem.png（216×216，透明底）

用法：python tools/ui/make_emblem.py
"""

import math
import os

from PIL import Image, ImageDraw

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT_DIR = os.path.join(ROOT, "Assets", "_Project", "Resources", "Fairy")
OUT_FILE = os.path.join(OUT_DIR, "fairy_emblem.png")

# 几何稿的基准尺寸与各圆参数（单位与 SVG 一致）。
BASE = 108.0
OUT_SIZE = 216
SUPERSAMPLE = 4

WHITE_RING_R = 30.66
WHITE_RING_W = 4.675
BLUE_RING_R = 24.89
BLUE_RING_W = 6.875
PUPIL_R = 11.14
HIGHLIGHT_R = 4.675

BLUE_TOP = (0x3D, 0x8A, 0xE6)
BLUE_BOTTOM = (0x1A, 0x35, 0xD6)
PUPIL_COLOR = (0x0A, 0x16, 0x33)

SEGMENTS = 96


def lerp(a, b, t):
    return tuple(round(a[i] + (b[i] - a[i]) * t) for i in range(3))


def main():
    scale = OUT_SIZE * SUPERSAMPLE / BASE
    size = int(BASE * scale)
    image = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    draw = ImageDraw.Draw(image)
    center = size / 2.0

    def bbox(radius):
        r = radius * scale
        return [center - r, center - r, center + r, center + r]

    def width(stroke):
        return max(1, round(stroke * scale))

    # 1) 外白环
    draw.arc(bbox(WHITE_RING_R), 0, 360, fill=(255, 255, 255, 255), width=width(WHITE_RING_W))

    # 2) 蓝渐变环：分段画，按纵向位置取色
    step = 360.0 / SEGMENTS
    for i in range(SEGMENTS):
        start = i * step
        mid = math.radians(start + step * 0.5)
        t = 0.5 - 0.5 * math.sin(mid)          # 上亮下暗
        color = lerp(BLUE_TOP, BLUE_BOTTOM, t)
        draw.arc(bbox(BLUE_RING_R), start, start + step + 1.0, fill=color + (255,), width=width(BLUE_RING_W))

    # 3) 瞳
    draw.ellipse(bbox(PUPIL_R), fill=PUPIL_COLOR + (255,))

    # 4) 高光：左上方偏一点，读起来像一颗反光的镜头
    hx = center - PUPIL_R * 0.42 * scale
    hy = center - PUPIL_R * 0.42 * scale
    r = HIGHLIGHT_R * scale
    draw.ellipse([hx - r, hy - r, hx + r, hy + r], fill=(255, 255, 255, 210))

    image = image.resize((OUT_SIZE, OUT_SIZE), Image.LANCZOS)

    os.makedirs(OUT_DIR, exist_ok=True)
    image.save(OUT_FILE)
    print(f"output: {os.path.relpath(OUT_FILE, ROOT)}  ({os.path.getsize(OUT_FILE) / 1024:.1f} KB)")


if __name__ == "__main__":
    main()
