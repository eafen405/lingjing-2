#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""伊埃斯全套素材构建：5 朝向 × (idle 2 / walk 4) + death 4。

输出：
  Assets/_Project/Art/Characters/eous/{state}/eous_{state}_{dir}_{frame:02}.png   64×64 游戏用
  Assets/_Project/Art/Characters/eous/eous_{state}.png                            图集（行=朝向）
  Assets/_Project/Art/Characters/eous/manifest.json                               程序唯一数据源
  tools/art-pipeline/out/master/                                                 128×128 母版
  tools/art-pipeline/out/sheet_{state}.png                                       审阅用
"""

from __future__ import annotations

import json
import os

from PIL import Image, ImageDraw, ImageFont

import eous_draw
from eous_model import EOUS_COLORS, Pose

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(HERE))
ASSETS = os.path.join(ROOT, "Assets", "_Project", "Art", "Characters", "eous")
OUT = os.path.join(HERE, "out")
MASTER = os.path.join(OUT, "master")

SHIP = 64
MASTER_SIZE = 128

DIRS = [("s", 0.0), ("se", 45.0), ("e", 90.0), ("ne", 135.0), ("n", 180.0)]
MIRROR = {"sw": "se", "w": "e", "nw": "ne"}

# 每帧的姿态参数
IDLE = [
    dict(bob=0.000, sway=0.000, ear_tilt=0.0),
    dict(bob=-0.016, sway=0.010, ear_tilt=2.5),
]

WALK = [
    dict(bob=0.000, sway=0.016, step=1.0, ear_tilt=5.0),
    dict(bob=-0.030, sway=0.000, step=0.0, ear_tilt=0.0),
    dict(bob=0.000, sway=-0.016, step=-1.0, ear_tilt=-5.0),
    dict(bob=-0.030, sway=0.000, step=0.0, ear_tilt=0.0),
]

# 死亡：整体压扁 + 下沉 + 逐帧变暗（脚底保持不动）
FOOT_Y = 0.992
DEATH = []
for _i, (sq, dim) in enumerate([(0.94, 1), (0.80, 1), (0.60, 2), (0.42, 3)]):
    DEATH.append(dict(squash=sq, drop=FOOT_Y * (1 - sq), dim=dim))

STATES = {
    "idle": dict(frames=IDLE, dirs=5, fps=12, loop=True),
    "walk": dict(frames=WALK, dirs=5, fps=12, loop=True),
    "death": dict(frames=DEATH, dirs=1, fps=12, loop=False),
}


def render_frame(state: str, frame: int, f: float, size: int) -> Image.Image:
    p = Pose(f=f, **STATES[state]["frames"][frame])
    return eous_draw.render(p, size)


def build():
    for d in (ASSETS, OUT, MASTER):
        os.makedirs(d, exist_ok=True)
    for state in STATES:
        os.makedirs(os.path.join(ASSETS, state), exist_ok=True)

    counts = {}
    for state, cfg in STATES.items():
        n_frames = len(cfg["frames"])
        n_dirs = cfg["dirs"]
        for di, (dname, f) in enumerate(DIRS[:n_dirs]):
            for fi in range(n_frames):
                img = render_frame(state, fi, f, SHIP)
                img.save(os.path.join(ASSETS, state,
                                      f"eous_{state}_{dname}_{fi:02}.png"))
                m = render_frame(state, fi, f, MASTER_SIZE)
                m.save(os.path.join(MASTER, f"eous_{state}_{dname}_{fi:02}.png"))
        counts[state] = dict(frames=n_frames, dirs=n_dirs)

        # 图集：行 = 朝向，列 = 帧
        atlas = Image.new("RGBA", (SHIP * n_frames, SHIP * n_dirs), (0, 0, 0, 0))
        for di in range(n_dirs):
            for fi in range(n_frames):
                atlas.alpha_composite(
                    Image.open(os.path.join(
                        ASSETS, state, f"eous_{state}_{DIRS[di][0]}_{fi:02}.png")),
                    (fi * SHIP, di * SHIP))
        atlas.save(os.path.join(ASSETS, f"eous_{state}.png"))

        # 审阅用：3× 放大 + 标签
        cell = SHIP * 3 + 10
        sheet = Image.new("RGB", (cell * n_frames + 10, cell * n_dirs + 26), (30, 30, 36))
        for di in range(n_dirs):
            for fi in range(n_frames):
                im = Image.open(os.path.join(
                    ASSETS, state, f"eous_{state}_{DIRS[di][0]}_{fi:02}.png"))
                sheet.paste(im.resize((SHIP * 3, SHIP * 3), Image.NEAREST),
                            (fi * cell + 5, di * cell + 5), im.resize((SHIP * 3, SHIP * 3), Image.NEAREST))
        d = ImageDraw.Draw(sheet)
        try:
            fnt = ImageFont.load_default(size=13)
        except TypeError:
            fnt = ImageFont.load_default()
        for di in range(n_dirs):
            d.text((2, di * cell + 8), DIRS[di][0].upper(), fill=(220, 220, 220), font=fnt)
        sheet.save(os.path.join(OUT, f"sheet_{state}.png"))

    manifest = {
        "entity": "eous",
        "displayName": "伊埃斯",
        "frameSize": [SHIP, SHIP],
        "masterSize": [MASTER_SIZE, MASTER_SIZE],
        "pivotPx": [SHIP // 2, 3],
        "pivotNorm": [0.5, round(3.0 / SHIP, 4)],
        "ppu": 32,
        "directions": {d: i for i, (d, _) in enumerate(DIRS)},
        "mirror": MIRROR,
        "states": {
            s: {
                "frames": counts[s]["frames"],
                "dirs": counts[s]["dirs"],
                "fps": STATES[s]["fps"],
                "loop": STATES[s]["loop"],
                "atlas": f"eous_{s}.png",
            }
            for s in STATES
        },
        "palette": {k: v for k, v in EOUS_COLORS.items()},
        "generator": "tools/art-pipeline/eous_draw.py",
    }
    with open(os.path.join(ASSETS, "manifest.json"), "w", encoding="utf-8") as fh:
        json.dump(manifest, fh, ensure_ascii=False, indent=2)
    print("built ->", ASSETS)
    return manifest


if __name__ == "__main__":
    build()
