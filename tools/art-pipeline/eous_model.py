#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""伊埃斯参数化模型：一个蛋形角色 + 按方位角投影的部件。

为什么要参数化
--------------
手画 5 个朝向 × 多帧，朝向之间一定会漂移（耳距、面罩宽度、围巾位置各画各的）。
这里把角色拆成一具「伪 3D」模型：

* 身体是一个左右宽 a、前后厚 b 的蛋；朝向 f 只改变各部件在屏幕上的横向位置。
* 部件都挂在身体表面的某个方位角 α 上（α=0 为正面，+ 为角色右侧）。
* 投影：screen_x = -a·sinα·cos f + b·cosα·sin f
        depth    =  a·sinα·sin f + b·cosα·cos f   （depth>0 朝观察者，可见）

于是 5 个朝向、走路的摆腿、死亡的塌缩都只是几组参数，形状本身只写一遍。

坐标
----
body units：x 以身体中线为 0，y 从耳尖 0 到脚底 1.0。
线宽仍是「最终像素」，所以 64 和 128 两档各自渲染、各自清晰。
"""

from __future__ import annotations

import math
from dataclasses import dataclass, field

from artlib import (
    Canvas,
    Palette,
    tapered_capsule,
    hex2rgb,
    _catmull_rom_closed,
)

# --------------------------------------------------------------------------- 色板

EOUS_COLORS = {
    "outline": "#14161a",
    "hood_light": "#fbf7ee",
    "hood": "#ebe4d8",
    "hood_shade": "#cfc6b8",
    "hood_deep": "#a89d8e",
    "visor": "#22282a",
    "visor_rim": "#3a4440",
    "eye": "#c9f24d",
    "eye_hi": "#e8ffa6",
    "eye_dark": "#8ec32c",
    "coat_light": "#4b5866",
    "coat": "#3a4552",
    "coat_shade": "#2c353f",
    "coat_deep": "#20272e",
    "scarf_light": "#e69347",
    "scarf": "#cf772c",
    "scarf_shade": "#a95a1e",
    "scarf_deep": "#7c3d12",
    "cream": "#f0e6cf",
    "rivet": "#b7b0a4",
    "rivet_dark": "#4a463e",
}
PALETTE = Palette(EOUS_COLORS)
C = {k: hex2rgb(v) + (255,) for k, v in EOUS_COLORS.items()}

# 死亡帧用到的暗色（按帧插值）
DEATH_TINT = {
    "hood": ["hood", "hood_shade", "hood_deep", "hood_shade"],
    "eye": ["eye", "eye_dark", "scarf_deep", "scarf_deep"],
}

# --------------------------------------------------------------------------- 布局

BODY_H = 0.94
BODY_TOP = 0.03
CENTER_X = 0.5
DEPTH = 0.82          # 前后厚度 / 左右宽度

# 蛋的外形（右半边，从上到下）
EGG_HALF = [
    (0.000, 0.164),
    (0.058, 0.169),
    (0.116, 0.186),
    (0.170, 0.212),
    (0.216, 0.252),
    (0.252, 0.304),
    (0.272, 0.362),
    (0.284, 0.422),
    (0.296, 0.482),
    (0.308, 0.548),
    (0.316, 0.612),
    (0.318, 0.674),
    (0.312, 0.732),
    (0.296, 0.786),
    (0.268, 0.836),
    (0.224, 0.882),
    (0.158, 0.918),
    (0.000, 0.942),
]
EGG = EGG_HALF + [(-x, y) for x, y in EGG_HALF[-2::-1]]

# 围巾横带（正面形状；在别的朝向里按宽度缩放）
SCARF = [
    (-0.314, 0.470), (-0.214, 0.448), (-0.086, 0.442), (0.048, 0.448),
    (0.164, 0.456), (0.262, 0.464), (0.314, 0.488), (0.306, 0.546),
    (0.258, 0.584), (0.184, 0.610), (0.098, 0.622), (0.012, 0.640),
    (-0.072, 0.620), (-0.162, 0.604), (-0.250, 0.578), (-0.310, 0.540),
]

SCARF_TOP = 0.470
COAT_TOP = 0.600
COAT_HEM = 0.818

# 部件挂载点（方位角，度；0=正面，+=角色右侧）
A_VISOR = 37.0        # 面罩开口半角
A_EYE = 16.0          # 眼中心
A_EAR = 30.0          # 耳根
A_FOOT = 30.0         # 脚
A_BUTTON = 0.0        # 纽扣
A_STITCH = 66.0       # 袖子 X 缝线
A_KNOT = -90.0        # 围巾结（角色左侧，侧挂）
A_GLYPH = 22.0        # 「01」所在方位

VISOR_TOP = 0.250
VISOR_BOTTOM = 0.516


# --------------------------------------------------------------------------- 投影与动画


def egg_half_width(by: float) -> float:
    """蛋在高度 by 处的半宽。"""
    pts = EGG_HALF
    for (x0, y0), (x1, y1) in zip(pts, pts[1:]):
        if min(y0, y1) <= by <= max(y0, y1):
            t = 0.0 if y1 == y0 else (by - y0) / (y1 - y0)
            return x0 + (x1 - x0) * t
    return 0.0


def proj(alpha_deg: float, a: float, f_deg: float, depth_ratio: float = DEPTH):
    """方位角 α 上的点 -> (屏幕 x 系数, 深度)。a 是该高度处的半宽。"""
    al, fr = math.radians(alpha_deg), math.radians(f_deg)
    b = a * depth_ratio
    x = -a * math.sin(al) * math.cos(fr) + b * math.cos(al) * math.sin(fr)
    d = a * math.sin(al) * math.sin(fr) + b * math.cos(al) * math.cos(fr)
    return x, d


def width_scale(f_deg: float, depth_ratio: float = DEPTH) -> float:
    fr = math.radians(f_deg)
    return math.sqrt(math.cos(fr) ** 2 + (depth_ratio * math.sin(fr)) ** 2)


@dataclass
class Pose:
    """一帧的全部可变参数。"""

    f: float = 0.0                 # 朝向角：0=S 45=SE 90=E 135=NE 180=N
    bob: float = 0.0               # 整体上下浮动（body units）
    sway: float = 0.0              # 围巾横向摆动
    ear_tilt: float = 0.0          # 耳朵前后摆（度，绕耳根）
    step: float = 0.0              # 走路的抬脚相位（-1..1）
    squash: float = 1.0            # 垂直压缩（死亡）
    drop: float = 0.0              # 整体下沉
    lean: float = 0.0              # 前倾剪切
    dim: int = 0                   # 死亡暗化档位 0..3
    extra: dict = field(default_factory=dict)


class Painter:
    """把 body units 画到 Canvas 上，并统一套用 Pose 的形变。"""

    def __init__(self, size: int, ss: int = 8, pose: Pose | None = None):
        self.c = Canvas(size, ss=ss)
        self.size = size
        self.pose = pose or Pose()
        self.eye_dark_switch = False

    # body units -> 画布归一化
    def P(self, bx: float, by: float):
        p = self.pose
        by2 = by * p.squash + p.drop + p.bob
        bx2 = bx + p.lean * by
        return (CENTER_X + bx2 * BODY_H, BODY_TOP + by2 * BODY_H)

    def px(self, n_px: float) -> float:
        """最终像素 -> 归一化长度。"""
        return n_px / self.size

    def col(self, name: str):
        p = self.pose
        if p.dim and name in DEATH_TINT:
            return C[DEATH_TINT[name][min(p.dim, 3)]]
        return C[name]

    # -- 画布原语（把 body 坐标转成画布坐标） ---------------------------------
    def blob(self, pts, **kw):
        self.c.blob([self.P(*p) for p in pts], **kw)

    def poly(self, pts, **kw):
        self.c.poly([self.P(*p) for p in pts], **kw)

    def line(self, pts, color, w=1.0, **kw):
        self.c.line([self.P(*p) for p in pts], color, w=w, **kw)

    def circle(self, bx, by, r, fill=None, stroke=None, w=1.0):
        cx, cy = self.P(bx, by)
        self.c.circle(cx, cy, r * BODY_H, fill=fill, stroke=stroke, w=w)

    def ellipse(self, bx, by, rx, ry, fill=None, stroke=None, w=1.0):
        cx, cy = self.P(bx, by)
        self.c.ellipse(cx, cy, rx * BODY_H, ry * BODY_H, fill=fill, stroke=stroke, w=w)

    def clipped(self, pts, fn):
        self.c.clipped([self.P(*p) for p in pts], lambda t: fn(Sub(self, t)))


class Sub:
    """裁剪回调里的画笔：body units -> 临时画布。"""

    def __init__(self, parent: Painter, canvas: Canvas):
        self.p = parent
        self.c = canvas

    def P(self, bx, by):
        return self.p.P(bx, by)

    def blob(self, pts, **kw):
        self.c.blob([self.P(*p) for p in pts], **kw)

    def poly(self, pts, **kw):
        self.c.poly([self.P(*p) for p in pts], **kw)

    def line(self, pts, color, w=1.0, **kw):
        self.c.line([self.P(*p) for p in pts], color, w=w, **kw)

    def circle(self, bx, by, r, fill=None, stroke=None, w=1.0):
        cx, cy = self.P(bx, by)
        self.c.circle(cx, cy, r * BODY_H, fill=fill, stroke=stroke, w=w)

    def clipped(self, pts, fn):
        self.c.clipped([self.P(*p) for p in pts], lambda cc: fn(Sub(self.p, cc)))


# 兼容旧调用
def P(bx, by):
    return (CENTER_X + bx * BODY_H, BODY_TOP + by * BODY_H)


__all__ = [
    "PALETTE", "C", "Pose", "Painter", "proj", "width_scale", "egg_half_width",
    "EGG", "SCARF", "A_VISOR", "A_EYE", "A_EAR", "A_FOOT", "A_BUTTON", "A_STITCH",
    "A_KNOT", "A_GLYPH", "BODY_H", "BODY_TOP", "CENTER_X", "DEPTH",
    "SCARF_TOP", "COAT_TOP", "COAT_HEM", "VISOR_TOP", "VISOR_BOTTOM",
]
