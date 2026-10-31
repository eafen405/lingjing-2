#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""T23 导出：飞刀 / 三种掉落 / 警示区 / 四种特效 + 审阅图 + 实景合成。

输出：
  Assets/_Project/Art/Items/{knife,pickup_*}/…           单帧 + 图集
  Assets/_Project/Art/Items/items.json                   程序唯一数据源
  Assets/_Project/Art/Fx/{warn,hit,break,pickup,proxy}/… 单帧 + 图集
  Assets/_Project/Art/Fx/effects.json                    程序唯一数据源
  tools/art-pipeline/out/misc_items.png                  道具与飞刀放大并排
  tools/art-pipeline/out/misc_fx.png                     特效逐帧并排
  tools/art-pipeline/out/misc_scene.png                  实景：伊埃斯 + 8 把飞刀 + 掉落 + 落区

规格见 docs/specs/art-assets.md §7.5。
"""

from __future__ import annotations

import json
import math
import os

import numpy as np
from PIL import Image, ImageDraw, ImageFont

from misc_draw import (
    ITEMS,
    FX_GROUPS,
    MISC_COLORS,
    PICKUP,
    PROXY,
    WARN,
    WARN_BORDER,
    draw_warn,
)

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(HERE))
ART = os.path.join(ROOT, "Assets", "_Project", "Art")
ITEMS_DIR = os.path.join(ART, "Items")
FX_DIR = os.path.join(ART, "Fx")
OUT = os.path.join(HERE, "out")


def _font(size: int = 13):
    try:
        return ImageFont.load_default(size=size)
    except TypeError:
        return ImageFont.load_default()


def _allowed():
    from misc_draw import PALETTE
    return {tuple(int(v) for v in c[:3]) for c in PALETTE.rgb[1:]}


# --------------------------------------------------------------------------- 校验


def check(name: str, img: Image.Image, size: tuple[int, int],
          border_free: bool = True) -> None:
    """尺寸 / 只用登记色板 / alpha 只有 0 或 255 / 不能贴边。"""
    assert img.size == size, f"{name}: {img.size} != {size}"
    a = np.asarray(img)
    alphas = np.unique(a[..., 3])
    assert set(alphas.tolist()) <= {0, 255}, f"{name}: alpha={alphas.tolist()}"
    opaque = a[a[..., 3] == 255]
    used = {tuple(int(v) for v in px) for px in np.unique(opaque[:, :3], axis=0)}
    assert used <= _allowed(), f"{name}: 越界颜色 {used - _allowed()}"
    if border_free:
        # 贴边会让引擎里的轮廓被切掉；警示区是九宫格边框，本身就贴边
        edge = np.concatenate([a[0, :, 3], a[-1, :, 3], a[:, 0, 3], a[:, -1, 3]])
        assert (edge == 0).all(), f"{name}: 有像素贴到了画布边"


def _save(img: Image.Image, path: str) -> str:
    os.makedirs(os.path.dirname(path), exist_ok=True)
    img.save(path)
    return os.path.relpath(path, ART).replace("\\", "/")


# --------------------------------------------------------------------------- 导出


def build_items() -> dict:
    entries = {}
    for name, cfg in ITEMS.items():
        w, h = cfg["size"]
        root = os.path.join(ITEMS_DIR, name)
        frames = []
        atlas = Image.new("RGBA", (w * cfg["frames"], h))
        for i in range(cfg["frames"]):
            img = cfg["fn"](i) if name == "knife" else cfg["fn"](i == 1)
            check(f"{name}_{i:02}", img, (w, h))
            frames.append(f"{name}/{name}_{i:02}.png")
            _save(img, os.path.join(root, f"{name}_{i:02}.png"))
            atlas.alpha_composite(img, (i * w, 0))
        _save(atlas, os.path.join(root, f"{name}.png"))
        entries[name] = dict(frames=cfg["frames"], frameSize=[w, h],
                             pivot=list(cfg["pivot"]), pattern=f"{name}/{name}_{{frame:02}}.png",
                             atlas=f"{name}/{name}.png")
    spec = {"kind": "item_set", "ppu": 32, "items": entries,
            "palette": dict(MISC_COLORS),
            "generator": "tools/art-pipeline/build_misc.py"}
    with open(os.path.join(ITEMS_DIR, "items.json"), "w", encoding="utf-8") as fh:
        json.dump(spec, fh, ensure_ascii=False, indent=2)
    return spec


def build_fx() -> dict:
    groups = {}
    for group, cfg in FX_GROUPS.items():
        w, h = cfg["size"]
        root = os.path.join(FX_DIR, group)
        names = list(cfg["frames"])
        atlas = Image.new("RGBA", (w * len(names), h))
        for i, name in enumerate(names):
            img = cfg["frames"][name]()
            check(name, img, (w, h), border_free=(group != "warn"))
            _save(img, os.path.join(root, f"{name}.png"))
            atlas.alpha_composite(img, (i * w, 0))
        _save(atlas, os.path.join(root, f"fx_{group}.png"))
        groups[group] = dict(frameSize=[w, h], frames=len(names),
                             fps=cfg["fps"], loop=cfg["loop"],
                             pattern=f"{group}/{{frame}}.png",
                             atlas=f"{group}/fx_{group}.png",
                             names=names)
    spec = {"kind": "fx_set", "ppu": 32, "groups": groups,
            "palette": dict(MISC_COLORS),
            "generator": "tools/art-pipeline/build_misc.py"}
    with open(os.path.join(FX_DIR, "effects.json"), "w", encoding="utf-8") as fh:
        json.dump(spec, fh, ensure_ascii=False, indent=2)
    return spec


# --------------------------------------------------------------------------- 审阅图


def review_items(zoom: int = 4) -> Image.Image:
    rows = [(name, cfg) for name, cfg in ITEMS.items()]
    cell = max(cfg["size"][0] for _n, cfg in rows) * zoom + 16
    img = Image.new("RGB", (cell * 9 + 80, len(rows) * (cell + 18) + 16), (11, 15, 18))
    d = ImageDraw.Draw(img)
    fnt = _font(13)
    for r, (name, cfg) in enumerate(rows):
        d.text((8, 12 + r * (cell + 18) + cell // 2), name,
               fill=(201, 242, 77), font=fnt)
        for i in range(cfg["frames"]):
            art = cfg["fn"](i) if name == "knife" else cfg["fn"](i == 1)
            big = art.resize((art.width * zoom, art.height * zoom), Image.NEAREST)
            img.paste(big, (80 + i * cell, 12 + r * (cell + 18)), big)
    img.save(os.path.join(OUT, "misc_items.png"))
    return img


def review_fx(zoom: int = 3) -> Image.Image:
    rows = [(g, cfg) for g, cfg in FX_GROUPS.items()]
    label_w = 74
    heights, widths = [], []
    for _g, cfg in rows:
        w, h = cfg["size"]
        z = zoom if w <= 32 else 2
        widths.append(label_w + len(cfg["frames"]) * (w * z + 12))
        heights.append(h * z + 34)
    img = Image.new("RGB", (max(widths), sum(heights) + 12), (11, 15, 18))
    d = ImageDraw.Draw(img)
    fnt = _font(13)
    y = 8
    for (group, cfg), row_h in zip(rows, heights):
        w, h = cfg["size"]
        z = zoom if w <= 32 else 2
        d.text((10, y + h * z // 2), group, fill=(201, 242, 77), font=fnt)
        x = label_w
        for i, name in enumerate(cfg["frames"]):
            art = cfg["frames"][name]()
            big = art.resize((art.width * z, art.height * z), Image.NEAREST)
            img.paste(big, (x, y), big)
            if group != "warn":
                d.text((x, y + big.height + 3), str(i), fill=(150, 160, 165), font=fnt)
            else:
                d.text((x, y + big.height + 3), name[5:], fill=(150, 160, 165), font=fnt)
            x += big.width + 12
        y += row_h
    img.save(os.path.join(OUT, "misc_fx.png"))
    return img


# --------------------------------------------------------------------------- 实景合成


def _tile(dst: Image.Image, src: Image.Image, box, region) -> None:
    sx0, sy0, sx1, sy1 = box
    tw, th = sx1 - sx0, sy1 - sy0
    patch = src.crop(box)
    rx0, ry0, rx1, ry1 = region
    for y in range(ry0, ry1, th):
        for x in range(rx0, rx1, tw):
            dst.paste(patch, (x, y), patch)


def nine_slice(src: Image.Image, w: int, h: int, border: int = WARN_BORDER):
    """把 32×32 的九宫格样板拉成任意矩形；中心与边条按 Tiled 平铺。"""
    sw, sh = src.size
    out = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    _tile(out, src, (0, 0, border, border), (0, 0, border, border))
    _tile(out, src, (sw - border, 0, sw, border), (w - border, 0, w, border))
    _tile(out, src, (0, sh - border, border, sh), (0, h - border, border, h))
    _tile(out, src, (sw - border, sh - border, sw, sh), (w - border, h - border, w, h))
    _tile(out, src, (border, 0, sw - border, border), (border, 0, w - border, border))
    _tile(out, src, (border, sh - border, sw - border, sh),
          (border, h - border, w - border, h))
    _tile(out, src, (0, border, border, sh - border), (0, border, border, h - border))
    _tile(out, src, (sw - border, border, sw, sh - border),
          (w - border, border, w, h - border))
    _tile(out, src, (border, border, sw - border, sh - border),
          (border, border, w - border, h - border))
    return out


def compose_scene() -> Image.Image:
    """960×540 实景：伊埃斯带 8 把飞刀、三种掉落、两种落区、一次命中。"""
    from eous_draw import render as eous_render
    from eous_model import Pose as EousPose

    W, H = 960, 540
    img = Image.new("RGBA", (W, H), (11, 15, 18, 255))

    # 落区：普通（橙）与危险（红）
    img.paste(nine_slice(draw_warn("normal"), 224, 128), (110, 300))
    img.paste(nine_slice(draw_warn("danger"), 160, 96), (640, 96))

    # 掉落
    for name, pos in (("pickup_health", (300, 250)),
                      ("pickup_energy", (660, 300)),
                      ("pickup_blade", (760, 220))):
        cfg = ITEMS[name]
        art = cfg["fn"](False)
        img.paste(art, (pos[0] - cfg["pivot"][0], pos[1] - 0), art)

    # 伊埃斯 + 8 把环绕飞刀（半径 2 单位 = 64px）
    cx, cy = 470, 300
    eous = eous_render(EousPose(f=0.0), 64)
    img.paste(eous, (cx - 32, cy - 61), eous)
    for i in range(8):
        a = 2 * math.pi * i / 8 - math.pi / 2
        kx = cx + 64 * math.cos(a)
        ky = cy - 24 + 48 * math.sin(a)
        art = ITEMS["knife"]["fn"](i)
        img.paste(art, (int(kx) - 8, int(ky) - 8), art)

    # 命中特效：挂在最右那把刀上
    hit = FX_GROUPS["hit"]["frames"]["hit_01"]()
    img.paste(hit, (cx + 64 - 12, cy - 64), hit)

    img.convert("RGB").save(os.path.join(OUT, "misc_scene.png"))
    # 手机实际是 2 倍整数缩放（1080p），1 倍看着小的东西在真机上是这个尺寸
    img.resize((W * 2, H * 2), Image.NEAREST).convert("RGB").save(
        os.path.join(OUT, "misc_scene_2x.png"))
    return img


def main() -> None:
    items = build_items()
    fx = build_fx()
    n_items = sum(cfg["frames"] for cfg in items["items"].values())
    n_fx = sum(cfg["frames"] for cfg in fx["groups"].values())
    print(f"items: {len(items['items'])} 组 / {n_items} 帧；"
          f"fx: {len(fx['groups'])} 组 / {n_fx} 帧")
    review_items()
    review_fx()
    compose_scene()
    print("review -> out/misc_items.png, out/misc_fx.png")
    print("scene  -> out/misc_scene.png")


if __name__ == "__main__":
    main()
