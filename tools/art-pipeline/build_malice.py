#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""以骸全套导出：四种小怪 + 两个恶名以骸（Boss）。

输出：
  Assets/_Project/Art/{Enemies|Bosses}/{entity}/{state}/{entity}_{state}_{dir}_{frame:02}.png
  Assets/_Project/Art/{Enemies|Bosses}/{entity}/{entity}_{state}.png     图集（行=朝向）
  Assets/_Project/Art/{Enemies|Bosses}/{entity}/manifest.json            程序唯一数据源
  tools/art-pipeline/out/mob_{entity}.png                               审阅用大图
  tools/art-pipeline/out/mob_review.png                                 全部并排

帧数与规格见 docs/specs/art-assets.md §3。
"""

from __future__ import annotations

import argparse
import json
import os

from PIL import Image, ImageDraw, ImageFont

from boss_draw import ENTRY as BOSS_ENTITY, STATES as BOSS_STATES
from malice_draw import ENTITY as MOB_ENTITY, STATES as MOB_STATES
from malice_model import MALICE_COLORS, Pose, measure_entity, render_entity

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(HERE))
ART = os.path.join(ROOT, "Assets", "_Project", "Art")
OUT = os.path.join(HERE, "out")

DIRS = [("s", 0.0), ("e", 90.0), ("n", 180.0)]

ENTITY = {**MOB_ENTITY, **BOSS_ENTITY}
STATES = {**MOB_STATES, **BOSS_STATES}
GROUP = {e: "Enemies" for e in MOB_ENTITY} | {e: "Bosses" for e in BOSS_ENTITY}


def all_poses(entity: str) -> list[Pose]:
    out = []
    for cfg in STATES[entity].values():
        for _d, f in DIRS[: cfg["dirs"]]:
            for fr in cfg["frames"]:
                out.append(Pose(yaw=f, **fr))
    return out


# --------------------------------------------------------------------------- 导出


def build_only(entity: str, preview: bool) -> dict:
    cfg = ENTITY[entity]
    w, h = cfg["frame"]
    box = measure_entity(cfg, all_poses(entity))
    pivot = [w // 2, round(h - cfg["ground"] / cfg["aspect"] * h)]
    root = os.path.join(ART, GROUP[entity], entity)
    for d in (root, OUT):
        os.makedirs(d, exist_ok=True)

    states = {}
    for state, scfg in STATES[entity].items():
        os.makedirs(os.path.join(root, state), exist_ok=True)
        n_frames, n_dirs = len(scfg["frames"]), scfg["dirs"]
        for di, (dname, f) in enumerate(DIRS[:n_dirs]):
            for fi, fr in enumerate(scfg["frames"]):
                img = render_entity(cfg, Pose(yaw=f, **fr))
                img.save(os.path.join(root, state,
                                      f"{entity}_{state}_{dname}_{fi:02}.png"))
        atlas = Image.new("RGBA", (w * n_frames, h * n_dirs), (0, 0, 0, 0))
        for di, (dname, _f) in enumerate(DIRS[:n_dirs]):
            for fi in range(n_frames):
                atlas.alpha_composite(
                    Image.open(os.path.join(root, state,
                                            f"{entity}_{state}_{dname}_{fi:02}.png")),
                    (fi * w, di * h))
        atlas.save(os.path.join(root, f"{entity}_{state}.png"))
        states[state] = dict(frames=n_frames, dirs=n_dirs, fps=scfg["fps"],
                             loop=scfg["loop"], atlas=f"{entity}_{state}.png")

    manifest = {
        "entity": entity,
        "group": GROUP[entity],
        "frameSize": [w, h],
        "pivotPx": pivot,
        "pivotNorm": [round(pivot[0] / w, 4), round(pivot[1] / h, 4)],
        "ppu": 32,
        "directions": {d: i for i, (d, _f) in enumerate(DIRS[:max(s["dirs"] for s in states.values())])},
        "mirror": {"w": "e"} if max(s["dirs"] for s in states.values()) > 1 else {},
        "states": states,
        "palette": {k: v for k, v in MALICE_COLORS.items()},
        "generator": "tools/art-pipeline/boss_draw.py" if GROUP[entity] == "Bosses"
                     else "tools/art-pipeline/malice_draw.py",
        "bbox": list(box),
    }
    with open(os.path.join(root, "manifest.json"), "w", encoding="utf-8") as fh:
        json.dump(manifest, fh, ensure_ascii=False, indent=2)

    if preview:
        sheet(entity)
    return manifest


def sheet(entity: str, zoom: int = 4) -> Image.Image:
    """审阅大图：行 = 状态，列 = 方向 × 帧。"""
    cfg = ENTITY[entity]
    w, h = cfg["frame"]
    zoom = max(1, 384 // max(w, h))
    cw, ch = w * zoom + 8, h * zoom + 8
    rows = list(STATES[entity].items())
    cols = max(len(s["frames"]) * s["dirs"] for _n, s in rows)
    img = Image.new("RGB", (cw * cols + 60, ch * len(rows) + 20), (34, 30, 42))
    d = ImageDraw.Draw(img)
    try:
        fnt = ImageFont.load_default(size=12)
    except TypeError:
        fnt = ImageFont.load_default()
    for r, (state, scfg) in enumerate(rows):
        d.text((4, 20 + r * ch + h * zoom // 2), state, fill=(220, 220, 220), font=fnt)
        k = 0
        for di, (dname, f) in enumerate(DIRS[: scfg["dirs"]]):
            for fi, fr in enumerate(scfg["frames"]):
                im = render_entity(cfg, Pose(yaw=f, **fr))
                big = im.resize((w * zoom, h * zoom), Image.NEAREST)
                x = 60 + k * cw
                img.paste(big, (x, 20 + r * ch), big)
                k += 1
    path = os.path.join(OUT, f"mob_{entity}.png")
    img.save(path)
    return img


REVIEW_PICKS = {
    "taillight": [("idle", 0, 0), ("move", 0, 0), ("idle", 0, 1), ("death", 0, 1)],
    "striker": [("idle", 0, 0), ("idle", 1, 0), ("telegraph", 0, 0), ("death", 0, 1)],
    "nailgun": [("idle", 0, 0), ("idle", 1, 0), ("fire", 1, 1), ("death", 0, 1)],
    "dropper": [("idle", 0, 0), ("telegraph", 0, 0), ("impact", 0, 1), ("death", 0, 1)],
    "chain": [("idle", 0, 0), ("idle", 1, 0), ("charge", 1, 1), ("death", 0, 1)],
    "downpour": [("idle", 0, 0), ("cast", 0, 1), ("cast", 0, 2), ("death", 0, 1)],
}


def review(zoom: int = 3) -> Image.Image:
    """全部实体并排的验收图：每个一列，行 = 待机 / 侧面 / 招牌动作 / 死亡。"""
    from eous_draw import render as eous_render
    from eous_model import Pose as EousPose

    cols = list(STATES)
    colw = 96 * zoom + 22
    rowh = 96 * zoom + 24
    img = Image.new("RGB", (colw * (len(cols) + 1) + 20, rowh * 4 + 40), (34, 28, 46))
    d = ImageDraw.Draw(img)
    try:
        fnt = ImageFont.load_default(size=13)
    except TypeError:
        fnt = ImageFont.load_default()

    eous = eous_render(EousPose(f=0.0), 64)
    big = eous.resize((64 * zoom, 64 * zoom), Image.NEAREST)
    img.paste(big, (10, rowh * 2), big)
    d.text((10, 8), "eous (2u)", fill=(230, 230, 230), font=fnt)

    for ci, e in enumerate(cols):
        x = 10 + (ci + 1) * colw
        d.text((x, 8), e, fill=(230, 230, 230), font=fnt)
        for ri, (state, di, fi) in enumerate(REVIEW_PICKS[e]):
            cfg = ENTITY[e]
            pose = Pose(yaw=DIRS[di][1], **STATES[e][state]["frames"][fi])
            im = render_entity(cfg, pose)
            b = im.resize((im.width * zoom, im.height * zoom), Image.NEAREST)
            img.paste(b, (x, 34 + ri * rowh), b)
    path = os.path.join(OUT, "mob_review.png")
    img.save(path)
    return img


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("entities", nargs="*", default=[])
    ap.add_argument("--no-sheet", action="store_true")
    a = ap.parse_args()
    if not a.no_sheet:
        review()
    for e in a.entities or list(STATES):
        m = build_only(e, preview=not a.no_sheet)
        n = sum(s["frames"] * s["dirs"] for s in m["states"].values())
        print(f"{e:10s} {n:3d} frames  {m['frameSize'][0]}x{m['frameSize'][1]}  "
              f"bbox={tuple(m['bbox'])}")


if __name__ == "__main__":
    main()
