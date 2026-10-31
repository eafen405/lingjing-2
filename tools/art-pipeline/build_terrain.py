#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""空洞地形导出：边界墙 / 挡路物 / 传送门 + 审阅图 + 合成场地。

输出：
  Assets/_Project/Art/Terrain/wall/wall_{side}.png          边界 32×32 × 8
  Assets/_Project/Art/Terrain/wall/terrain_wall.png         图集（一行）
  Assets/_Project/Art/Terrain/props/prop_{name}.png         挡路物 64×64 × 2
  Assets/_Project/Art/Terrain/portal/portal_{frame:02}.png  传送门 96×96 × 4
  Assets/_Project/Art/Terrain/tileset.json                  程序唯一数据源
  tools/art-pipeline/out/terrain_review.png                 全部素材放大并排
  tools/art-pipeline/out/terrain_room.png                   合成场地（验收图，透明底）

没有地板、没有背景色：场地里面是空的。规格见 docs/specs/terrain-tileset.md。
"""

from __future__ import annotations

import json
import os
import shutil

import numpy as np
from PIL import Image, ImageDraw, ImageFont

from terrain_draw import (
    C,
    E,
    N,
    PALETTE,
    PORTAL_FRAMES,
    PORTAL_SIZE,
    PROP,
    PROPS,
    S,
    TILE,
    W,
    WALL_MASKS,
    WALLS,
    TERRAIN_COLORS,
    draw_portal,
)

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(HERE))
ART = os.path.join(ROOT, "Assets", "_Project", "Art", "Terrain")
OUT = os.path.join(HERE, "out")

# 一屏 960×540 / 32px = 30×17 瓦片（合成场地用同一尺寸）
ROOM_W, ROOM_H = 30, 17
KEEP = ("wall", "props", "portal")


def _font(size: int = 13):
    try:
        return ImageFont.load_default(size=size)
    except TypeError:
        return ImageFont.load_default()


# --------------------------------------------------------------------------- 校验


def check(name: str, img: Image.Image, size: tuple[int, int], full: bool) -> None:
    """① 尺寸 ② 只用登记色板 ③ alpha 只有 0/255 ④ 边界墙必须铺满。"""
    assert img.size == size, f"{name}: {img.size} != {size}"
    a = np.asarray(img)
    alphas = np.unique(a[..., 3])
    assert set(alphas.tolist()) <= {0, 255}, f"{name}: alpha={alphas.tolist()}"
    allowed = {tuple(int(v) for v in c[:3]) for c in PALETTE.rgb[1:]}
    opaque = a[a[..., 3] == 255]
    used = {tuple(int(v) for v in px) for px in np.unique(opaque[:, :3], axis=0)}
    assert used <= allowed, f"{name}: 越界颜色 {used - allowed}"
    if full:
        assert (a[..., 3] == 255).all(), f"{name}: 边界墙必须铺满"


# --------------------------------------------------------------------------- 导出


def _save(img: Image.Image, *parts: str) -> str:
    path = os.path.join(ART, *parts)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    img.save(path)
    return "/".join(parts)


def _clean() -> None:
    """清掉上一版生成物：角色目录只有 wall / props / portal 三种。"""
    assert os.path.basename(ART) == "Terrain" and ROOT in ART
    for name in os.listdir(ART):
        path = os.path.join(ART, name)
        if os.path.isdir(path) and name not in KEEP:
            shutil.rmtree(path)
        elif name == "tileset.json":
            os.remove(path)


def build() -> dict:
    os.makedirs(OUT, exist_ok=True)
    _clean()

    walls = []
    names = list(WALLS)
    atlas = Image.new("RGBA", (TILE * len(names), TILE))
    for i, name in enumerate(names):
        img = WALLS[name]()
        check(name, img, (TILE, TILE), full=True)
        _save(img, "wall", f"{name}.png")
        atlas.alpha_composite(img, (i * TILE, 0))
        walls.append(dict(name=name, mask=WALL_MASKS[name], size=[TILE, TILE],
                          file=f"wall/{name}.png", atlas="wall/terrain_wall.png",
                          cell=[i, 0]))
    _save(atlas, "wall", "terrain_wall.png")

    props = []
    for name, fn in PROPS.items():
        img = fn()
        check(name, img, (PROP, PROP), full=False)
        props.append(dict(name=name, size=[PROP, PROP], pivot=[PROP // 2, 0],
                          file=_save(img, "props", f"{name}.png")))

    patlas = Image.new("RGBA", (PORTAL_SIZE * PORTAL_FRAMES, PORTAL_SIZE))
    for f in range(PORTAL_FRAMES):
        img = draw_portal(f)
        check(f"portal_{f:02}", img, (PORTAL_SIZE, PORTAL_SIZE), full=False)
        _save(img, "portal", f"portal_{f:02}.png")
        patlas.alpha_composite(img, (f * PORTAL_SIZE, 0))
    _save(patlas, "portal", "terrain_portal.png")

    spec = {
        "kind": "tileset",
        "name": "hollow_arena",
        "tileSize": [TILE, TILE],
        "ppu": 32,
        "outline": False,
        "floorTile": None,
        "background": None,
        "edgeMask": {"n": N, "e": E, "s": S, "w": W},
        "walls": walls,
        "props": props,
        "portal": {
            "frameSize": [PORTAL_SIZE, PORTAL_SIZE],
            "pivot": [PORTAL_SIZE // 2, 0],
            "frames": PORTAL_FRAMES,
            "fps": 10,
            "loop": True,
            "atlas": "portal/terrain_portal.png",
            "pattern": "portal/portal_{frame:02}.png",
        },
        "palette": dict(TERRAIN_COLORS),
        "generator": "tools/art-pipeline/build_terrain.py",
    }
    with open(os.path.join(ART, "tileset.json"), "w", encoding="utf-8") as fh:
        json.dump(spec, fh, ensure_ascii=False, indent=2)
    return spec


# --------------------------------------------------------------------------- 审阅图


def review(zoom: int = 5, pzoom: int = 3) -> Image.Image:
    rows = [
        ("wall", [(n, WALLS[n]()) for n in WALLS], zoom),
        ("prop", [(n, f()) for n, f in PROPS.items()], pzoom),
        ("portal", [(f"portal_{f:02}", draw_portal(f))
                    for f in range(PORTAL_FRAMES)], pzoom),
    ]
    label_w, pad, cap = 62, 10, 24
    width = 0
    for _role, arts, z in rows:
        width = max(width, label_w + sum(a.width * z + pad for _n, a in arts))
    heights = [max(a.height for _n, a in arts) * z + cap + 14
               for _role, arts, z in rows]
    img = Image.new("RGB", (width, sum(heights) + 12), (11, 15, 18))
    d = ImageDraw.Draw(img)
    fnt = _font(13)

    y = 8
    for (role, arts, z), row_h in zip(rows, heights):
        d.text((10, y + 6), role, fill=(201, 242, 77), font=fnt)
        x = label_w
        for name, art in arts:
            big = art.resize((art.width * z, art.height * z), Image.NEAREST)
            img.paste(big, (x, y), big)
            d.text((x, y + big.height + 4), name, fill=(150, 160, 165), font=fnt)
            x += big.width + pad
        y += row_h

    img.save(os.path.join(OUT, "terrain_review.png"))
    return img


# --------------------------------------------------------------------------- 合成场地


def compose_room() -> Image.Image:
    """30×17 的合成场地：边界 + 挡路物 + 传送门 + 主角与四种以骸。

    底是透明的（游戏里由相机清屏色决定），这张图只回答一个问题：
    白色伊埃斯和深色以骸站在空场地上，100% 缩放下是否都还一眼认得出。
    """
    from eous_draw import render as eous_render
    from eous_model import Pose as EousPose
    from malice_draw import ENTITY as MOB_ENTITY
    from malice_model import Pose as MobPose, render_entity

    Wp, Hp = ROOM_W * TILE, ROOM_H * TILE
    img = Image.new("RGBA", (Wp, Hp), (0, 0, 0, 0))
    walls = {mask: WALLS[name]() for name, mask in WALL_MASKS.items()}

    x0, y0, x1, y1 = 1, 1, ROOM_W - 2, ROOM_H - 2
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            mask = 0
            if y == y0:
                mask |= N
            if y == y1:
                mask |= S
            if x == x0:
                mask |= W
            if x == x1:
                mask |= E
            if mask:
                img.paste(walls[mask], (x * TILE, y * TILE))

    portal = draw_portal(0)
    img.paste(portal, (14 * TILE, 2 * TILE), portal)

    for name, tx, ty in (("prop_block", 8, 7), ("prop_crystal", 20, 6),
                         ("prop_block", 12, 12), ("prop_crystal", 24, 13)):
        art = PROPS[name]()
        img.paste(art, (tx * TILE + TILE // 2 - PROP // 2,
                        ty * TILE + TILE - PROP), art)

    fx, fy = 15 * TILE + 16, 14 * TILE + 24
    eous = eous_render(EousPose(f=0.0), 64)
    img.paste(eous, (fx - 32, fy - 61), eous)

    mobs = [("striker", 9.5, 9.5, 90.0), ("nailgun", 20.5, 10.0, 90.0),
            ("taillight", 12.0, 8.5, 0.0), ("dropper", 25.0, 9.0, 0.0)]
    for ent, tx, ty, yaw in mobs:
        art = render_entity(MOB_ENTITY[ent], MobPose(yaw=yaw))
        feet = (int(tx * TILE), int(ty * TILE))
        img.paste(art, (feet[0] - 32, feet[1] - (art.height - 5)), art)

    img.save(os.path.join(OUT, "terrain_room.png"))
    # 2 倍图垫一层深色底，方便直接肉眼验收（素材本身仍是透明底）
    flat = Image.new("RGBA", (Wp * 2, Hp * 2), C["void"])
    flat.alpha_composite(img.resize((Wp * 2, Hp * 2), Image.NEAREST))
    flat.convert("RGB").save(os.path.join(OUT, "terrain_room_2x.png"))
    return img


def main() -> None:
    spec = build()
    n = len(spec["walls"]) + len(spec["props"]) + spec["portal"]["frames"]
    print(f"tileset {spec['name']}: {n} 张"
          f"（{len(spec['walls'])} 边界 + {len(spec['props'])} 挡路物"
          f" + {spec['portal']['frames']} 帧传送门），无地板")
    review()
    compose_room()
    print("review  -> out/terrain_review.png")
    print("room    -> out/terrain_room.png")


if __name__ == "__main__":
    main()
