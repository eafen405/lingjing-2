#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""以骸血条：32×5 轨道 + 1×3 可平铺填充。

为什么单独一个脚本
------------------
血条不是「一组逐帧特效」，它的两张图要分别按不同方式使用：轨道固定 32×5、
填充按血量横向裁剪。放进 FX_GROUPS 会被那个 schema 的单一 frameSize 卡住，
所以单独出一组，产物落在 Art/Fx/bar/，运行期由场景生成器按固定路径烘进去。

规格见 docs/specs/art-assets.md §7.5。

重跑：
  python tools/art-pipeline/build_bar.py
输出：
  Assets/_Project/Art/Fx/bar/bar_track.png   32×5
  Assets/_Project/Art/Fx/bar/bar_fill.png    1×3
  tools/art-pipeline/out/bar_review.png      8 倍审阅图
"""

from __future__ import annotations

import json
import os

import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(HERE))
ART = os.path.join(ROOT, "Assets", "_Project", "Art")
FX_DIR = os.path.join(ART, "Fx")
BAR_DIR = os.path.join(FX_DIR, "bar")
OUT = os.path.join(HERE, "out")

TRACK_W, TRACK_H = 32, 5
FILL_W, FILL_H = 1, 3
FULL_FILL_W = 30  # 审阅图里按 30px 展开看满血的样子


def palette() -> dict[str, tuple[int, int, int, int]]:
    with open(os.path.join(FX_DIR, "effects.json"), encoding="utf-8") as fh:
        spec = json.load(fh)
    out = {}
    for name, value in spec["palette"].items():
        value = value.lstrip("#")
        out[name] = (int(value[0:2], 16), int(value[2:4], 16), int(value[4:6], 16), 255)
    return out


def rect(img: Image.Image, x0: int, y0: int, x1: int, y1: int, color) -> None:
    for y in range(y0, y1):
        for x in range(x0, x1):
            img.putpixel((x, y), color)


def draw_track(c: dict) -> Image.Image:
    """1px 描边 + 深色内槽；内槽比填充高 2px，填充贴上去后上下各留 1px 描边。"""
    img = Image.new("RGBA", (TRACK_W, TRACK_H), (0, 0, 0, 0))
    rect(img, 0, 0, TRACK_W, TRACK_H, c["outline"])
    rect(img, 1, 1, TRACK_W - 1, TRACK_H - 1, c["shell_deep"])
    return img


def draw_fill(c: dict) -> Image.Image:
    """1×3 的可平铺填充：上下各 1px 深红、中间 1px 亮红。宽度交给引擎按整数像素平铺。"""
    img = Image.new("RGBA", (FILL_W, FILL_H), (0, 0, 0, 0))
    rect(img, 0, 0, FILL_W, FILL_H, c["warn_deep"])
    rect(img, 0, 1, FILL_W, 2, c["warn"])
    return img


def validate(name: str, img: Image.Image, size: tuple[int, int], colors: set) -> None:
    assert img.size == size, f"{name}: {img.size} != {size}"
    a = np.asarray(img)
    alphas = set(np.unique(a[..., 3]).tolist())
    assert alphas <= {0, 255}, f"{name}: alpha={sorted(alphas)}"
    used = {tuple(int(v) for v in px[:3]) + (255,) for px in np.unique(a[a[..., 3] == 255], axis=0)}
    assert used <= colors, f"{name}: 越界颜色 {used - colors}"


def save(img: Image.Image, path: str) -> str:
    os.makedirs(os.path.dirname(path), exist_ok=True)
    img.save(path)
    return os.path.relpath(path, ART).replace("\\", "/")


def review(track: Image.Image, fill: Image.Image, zoom: int = 8) -> None:
    """三行：轨道、轨道+填充（满血）、填充按 30px 展开的样子。"""
    os.makedirs(OUT, exist_ok=True)
    expanded = fill.resize((FULL_FILL_W, FILL_H), Image.NEAREST)
    full = track.copy()
    full.paste(expanded, (1, 1))

    pad = 12
    gap = 10
    rows = [track, full, expanded]
    width = max(r.width for r in rows) * zoom + pad * 2
    height = pad * 2 + sum(r.height * zoom for r in rows) + gap * (len(rows) - 1)

    img = Image.new("RGB", (width, height), (11, 15, 18))
    y = pad
    for art in rows:
        big = art.resize((art.width * zoom, art.height * zoom), Image.NEAREST)
        img.paste(big, (pad, y), big)
        y += big.height + gap
    img.save(os.path.join(OUT, "bar_review.png"))


def main() -> None:
    c = palette()
    colors = set(c.values())
    track = draw_track(c)
    fill = draw_fill(c)
    validate("bar_track", track, (TRACK_W, TRACK_H), colors)
    validate("bar_fill", fill, (FILL_W, FILL_H), colors)
    print("track ->", save(track, os.path.join(BAR_DIR, "bar_track.png")))
    print("fill  ->", save(fill, os.path.join(BAR_DIR, "bar_fill.png")))
    review(track, fill)
    print("review -> tools/art-pipeline/out/bar_review.png")


if __name__ == "__main__":
    main()
