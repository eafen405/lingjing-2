#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""以骸（Malice）家族的共用模型层：一台固定机位的伪 3D 投影 + 统一收边。

为什么和伊埃斯不共用一套
------------------------
伊埃斯是「蛋壳 + 贴在表面上的部件」，用方位角 α 投影最省事；以骸是有厚度、
有腿、有前伸武器的机体，必须能在三个视角下都对得上，所以这里直接放一台
极简的三维相机：骨架写成 (x, h, z) 的空间点，投影一次就得到任意朝向。

坐标
----
* ``x``：角色右手方向为 +。正面视角时它出现在**画面左侧**，与伊埃斯一致。
* ``h``：离地高度，0 = 脚底 / 地面，1 = 一个画布边长。
* ``z``：角色前方为 +。
* 画布：``u = 0.5 + screen_x``，``v = (ground - h + tilt·toward) / aspect``。
  ``tilt`` 让靠近观众的点落得更低，于是地面会往上「退」，看得出俯视感。

朝向角 f 与伊埃斯的约定相同：0=正面(S)、90=侧面(E，鼻尖朝画面右)、180=背面(N)。
镜像是 W←E，不另外出图。
"""

from __future__ import annotations

import math
from dataclasses import dataclass

import numpy as np
from PIL import Image

from artlib import (
    Canvas,
    Palette,
    clean_specks,
    hex2rgb,
    tapered_capsule,
    to_pixels,
)
from eous_model import EOUS_COLORS

# --------------------------------------------------------------------------- 色板

# 以骸只新增 8 色：黑青装甲一档四阶 + 酸绿沿用伊埃斯的眼睛三色 + 预警用的红。
# 项目色板因此是 21 + 8 = 29 色，仍在 32 色预算内。
MALICE_EXTRA = {
    "shell_deep": "#10171e",
    "shell": "#1a232d",
    "shell_light": "#33455a",
    "plate": "#4f6b82",
    "rim": "#a8cbd6",
    "warn": "#ff415f",
    "warn_hi": "#ffb0bf",
    "warn_deep": "#7d1330",
}

_SHARED = ("outline", "eye", "eye_hi", "eye_dark", "rivet", "rivet_dark")
MALICE_COLORS = {k: EOUS_COLORS[k] for k in _SHARED}
MALICE_COLORS.update(MALICE_EXTRA)

PALETTE = Palette(MALICE_COLORS)
C = {k: hex2rgb(v) + (255,) for k, v in MALICE_COLORS.items()}

# 死亡时逐档变暗：名字 -> 四阶
DIM = {
    "shell": ["shell", "shell_deep", "outline", "outline"],
    "shell_light": ["shell_light", "shell", "shell_deep", "shell_deep"],
    "plate": ["plate", "shell_light", "shell", "shell_deep"],
    "rim": ["rim", "shell_light", "shell", "shell_deep"],
    "eye": ["eye", "eye_dark", "eye_dark", "warn_deep"],
    "eye_hi": ["eye_hi", "eye", "eye_dark", "eye_dark"],
    "eye_dark": ["eye_dark", "warn_deep", "warn_deep", "shell_deep"],
    "rivet": ["rivet", "plate", "shell_light", "shell"],
}

# --------------------------------------------------------------------------- 相机

GROUND = 0.875     # 方形画布上地平线的归一化 y（pivot 在画布底往上 4px）
TILT = 0.16        # 深度 -> 屏幕纵向位移（俯视感）


class Cam:
    """朝向角 -> 三维点到画布归一化坐标的投影。"""

    def __init__(self, yaw: float, aspect: float = 1.0, ground: float = GROUND,
                 tilt: float = TILT):
        self.f = math.radians(yaw)
        self.cos, self.sin = math.cos(self.f), math.sin(self.f)
        self.aspect = aspect
        self.ground = ground
        self.tilt = tilt

    def toward(self, p) -> float:
        """点到观众的距离（>0 表示朝观众，越大越靠前）。"""
        x, _h, z = p
        return x * self.sin + z * self.cos

    def __call__(self, p):
        x, h, z = p
        sx = -x * self.cos + z * self.sin
        return (0.5 + sx, (self.ground - h + self.tilt * self.toward(p)) / self.aspect)


# --------------------------------------------------------------------------- 凸包


def _tone(cam: "Cam", nx: float, nz: float) -> str:
    """受光判定：主光在**画面上左方、略偏向观众**。

    侧面法线按朝向转到屏幕后打一个带偏置的分值，再切三档。偏置是必要的：
    正面两个斜面关于视轴对称，对称打分必然把相邻两个面推到明暗两端，
    看上去就像「一半涂白一半涂黑」；加上偏置后它们差一档，才像同一个物体的两个面。
    """
    nsx = -nx * cam.cos + nz * cam.sin      # 法线在屏幕上的横向分量
    nsd = nx * cam.sin + nz * cam.cos       # 法线朝向观众的分量
    t = -0.8 * nsx + 0.35 * nsd + 0.25
    if t > 0.35:
        return "lit"
    return "mid" if t > 0.05 else "dark"


def _face(lit, mid, dark, tone: str):
    """三档明度；没给 mid 时退化成两档。"""
    if tone == "lit":
        return lit or mid or dark
    if tone == "mid":
        return mid or lit or dark
    return dark or mid or lit


def convex_hull(pts):
    """Andrew monotone chain；投影后的角点用它收成一个轮廓。"""
    pts = sorted(set((round(x, 6), round(y, 6)) for x, y in pts))
    if len(pts) <= 2:
        return pts
    lo, up = [], []
    for p in pts:
        while len(lo) >= 2 and _cross(lo[-2], lo[-1], p) <= 0:
            lo.pop()
        lo.append(p)
    for p in reversed(pts):
        while len(up) >= 2 and _cross(up[-2], up[-1], p) <= 0:
            up.pop()
        up.append(p)
    return lo[:-1] + up[:-1]


def _cross(o, a, b):
    return (a[0] - o[0]) * (b[1] - o[1]) - (a[1] - o[1]) * (b[0] - o[0])


# --------------------------------------------------------------------------- 姿态


@dataclass
class Pose:
    """一帧的全部可变参数。"""

    yaw: float = 0.0        # 0=S 90=E 180=N
    bob: float = 0.0        # 整体上下浮动（世界单位）
    camber: float = 0.0     # 前倾（越高的点越往前）
    extend: float = 0.0     # 前伸（冲锋/开火的伸长量）
    squash: float = 1.0     # 垂直压缩
    step: float = 0.0       # 步态相位 -1..1
    dim: int = 0            # 死亡暗化 0..3
    warn: float = 0.0       # 预警强度 0..1（绿 -> 红）
    flash: float = 0.0      # 爆发强度 0..1（枪口 / 落点）
    seed: int = 0           # 帧间随机（碎裂）


# --------------------------------------------------------------------------- 画笔


class Mob:
    """把 (x,h,z) 形体画进画布，并在最后统一描边。"""

    def __init__(self, width: int, height: int | None = None, ss: int = 8):
        self.c = Canvas(width, height, ss=ss)
        self.cam = Cam(0.0)
        self.pose = Pose()
        self.scale = 1.0

    # -- 坐标 ---------------------------------------------------------------
    def bind(self, pose: Pose, aspect: float = 1.0, ground: float = GROUND,
             scale: float = 1.0):
        self.pose = pose
        self.scale = scale
        self.cam = Cam(pose.yaw, aspect=aspect, ground=ground)
        return self

    def W(self, x, h, z):
        """世界点 -> 画布点，套用缩放 / bob / 前倾 / 压缩。"""
        p = self.pose
        s = self.scale
        h2 = h * s * p.squash + p.bob
        # 前倾：以脚底为轴，越高的点越往前
        z2 = z * s + p.camber * h2
        return (x * s, h2, z2)

    def pt(self, x, h, z):
        return self.cam(self.W(x, h, z))

    def z(self, x, h, z) -> float:
        return self.cam.toward(self.W(x, h, z))

    # -- 形体 ---------------------------------------------------------------
    def poly3(self, pts, fill=None, **kw):
        self.c.poly([self.pt(*p) for p in pts], fill=fill, **kw)

    def cap3(self, p0, p1, r0, r1, fill):
        a, b = self.pt(*p0), self.pt(*p1)
        s = self.scale
        self.c.poly(tapered_capsule(a, b, r0 * s, r1 * s), fill=fill)

    def hull(self, pts3, fill):
        """一组空间点的凸包（用于机体这种有厚度的块）。"""
        self.c.poly(convex_hull([self.pt(*p) for p in pts3]), fill=fill)

    def solid(self, poly, h0, h1, top=None, side_lit=None, side_mid=None,
              side_dark=None, top_scale: float = 1.0):
        """多边形截面的棱柱：顶面 + 逐个可见侧面。

        ``poly`` 是俯视 (x, z) 的凸多边形。盒子的两个侧面永远平行于视线，
        不可能出现「转折面」；换成六边形截面后，正面/侧面视角都会露出
        2–3 个不同明度的面，32px 下才像有体积的装甲，而不是一个方块。
        """
        cx = sum(x for x, _ in poly) / len(poly)
        cz = sum(z for _, z in poly) / len(poly)

        def ring(h, s):
            return [(x * s, h, z * s) for x, z in poly]

        r0, r1 = ring(h0, 1.0), ring(h1, top_scale)
        self.c.poly(convex_hull([self.pt(*p) for p in r0 + r1]),
                    fill=side_dark or side_lit)

        # 侧面：法线朝观众的那些才看得见，远的先画
        faces = []
        for i in range(len(poly)):
            j = (i + 1) % len(poly)
            (ax, az), (bx, bz) = poly[i], poly[j]
            nx, nz = bz - az, ax - bx
            mx, mz = (ax + bx) * 0.5 - cx, (az + bz) * 0.5 - cz
            if nx * mx + nz * mz < 0:
                nx, nz = -nx, -nz
            if self.cam.toward((nx, 0.0, nz)) <= 0.02:
                continue
            faces.append((self.cam.toward((mx, 0.0, mz)), nx, nz,
                          [r0[i], r0[j], r1[j], r1[i]]))
        for _d, nx, nz, quad in sorted(faces, key=lambda t: t[0]):
            self.poly3(quad, fill=_face(side_lit, side_mid, side_dark,
                                        _tone(self.cam, nx, nz)))

        if top is not None:
            self.c.poly([self.pt(*p) for p in r1], fill=top)

    def cone(self, poly, h0, h1, top=None, side_lit=None, side_mid=None,
             side_dark=None):
        """多边锥：底面 ring 收到一点（坠物的穿刺锥）。"""
        cx = sum(x for x, _ in poly) / len(poly)
        cz = sum(z for _, z in poly) / len(poly)
        r0 = [(x, h0, z) for x, z in poly]
        apex = (0.0, h1, 0.0)
        self.c.poly(convex_hull([self.pt(*p) for p in r0] + [self.pt(*apex)]),
                    fill=side_dark or side_lit)
        faces = []
        for i in range(len(poly)):
            j = (i + 1) % len(poly)
            (ax, az), (bx, bz) = poly[i], poly[j]
            nx, nz = bz - az, ax - bx
            mx, mz = (ax + bx) * 0.5 - cx, (az + bz) * 0.5 - cz
            if nx * mx + nz * mz < 0:
                nx, nz = -nx, -nz
            if self.cam.toward((nx, 0.0, nz)) <= 0.02:
                continue
            faces.append((self.cam.toward((mx, 0.0, mz)), nx, nz,
                          [r0[i], r0[j], apex]))
        for _d, nx, nz, tri in sorted(faces, key=lambda t: t[0]):
            self.poly3(tri, fill=_face(side_lit, side_mid, side_dark,
                                       _tone(self.cam, nx, nz)))
        if top is not None:
            self.c.poly([self.pt(*p) for p in r0], fill=top)

    # -- 收尾 ---------------------------------------------------------------
    def finish(self, rim: bool = False) -> Image.Image:
        img = to_pixels(self.c, PALETTE)
        return pixel_outline(img, rim=rim)

    def colour(self, name: str):
        p = self.pose
        if p.dim and name in DIM:
            return C[DIM[name][min(p.dim, 3)]]
        return C[name]


# --------------------------------------------------------------------------- 收边


def pixel_outline(img: Image.Image, rim: bool = False) -> Image.Image:
    """在最终像素网格上加 1px 描边（不参与超采样，所以永远是 1px）。

    ``rim=True`` 时另加一圈左上侧的冷色边光；目前默认关掉——32px 下它会
    吃掉大半个轮廓，反而让机体显得更糊。
    """
    a = np.asarray(img).copy()
    on = a[..., 3] > 0
    p = np.pad(on, 1)
    neigh = p[:-2, 1:-1] | p[2:, 1:-1] | p[1:-1, :-2] | p[1:-1, 2:]
    edge = neigh & ~on
    a[edge] = C["outline"]

    if rim:
        left = np.zeros_like(on)
        left[:, 1:] = on[:, :-1] & on[:, 1:]
        up = np.zeros_like(on)
        up[1:, :] = on[:-1, :] & on[1:, :]
        a[on & ~(left & up)] = C["rim"]

    return Image.fromarray(a, "RGBA")


# --------------------------------------------------------------------------- 工具


def gcol(m: Mob, tone: str = "base"):
    """以太发光色：平时酸绿，预警转红。"""
    if m.pose.warn >= 0.5:
        return {"base": C["warn"], "hi": C["warn_hi"], "dim": C["warn_deep"]}[tone]
    return {"base": C["eye"], "hi": C["eye_hi"], "dim": C["eye_dark"]}[tone]


def leg(m: Mob, hip, knee, foot, r0=0.048, r1=0.018, tone="plate", joint=False):
    """两节腿：细的四肢用亮色，才不会和躯干糊成一团。"""
    m.cap3(hip, knee, r0, (r0 + r1) * 0.55, m.colour(tone))
    m.cap3(knee, foot, (r0 + r1) * 0.55, r1, m.colour(tone))
    if joint:
        disc(m, knee, (r0 + r1) * 0.26, gcol(m, "dim"))


def glow_pts(m: Mob, pts, w=1.0, tone="base"):
    m.c.line([m.pt(*p) for p in pts], gcol(m, tone), w=w, cap=True)


def disc(m: Mob, p, r, fill, ry=None):
    u, v = m.pt(*p)
    r = r * m.scale
    m.c.ellipse(u, v, r, ry * m.scale if ry is not None else r, fill=fill)


def eye(m: Mob, p, r, ry=None):
    """一处发光焦点：暗圈 + 亮芯，保证在 32px 下仍是一个实心亮块。"""
    disc(m, p, r * 1.60, m.colour("shell_deep"), ry=(ry * 1.60 if ry else None))
    disc(m, p, r, gcol(m, "base"), ry=ry)
    disc(m, (p[0] - r * 0.24, p[1] + r * 0.26, p[2]), r * 0.40, gcol(m, "hi"),
         ry=(ry * 0.40 if ry else None))


def burst(m: Mob, p, r, strength: float = 1.0):
    """以太喷发：一圈长短不一的短线 + 亮芯（枪口焰、落点炸开）。

    落点炸开时喷到地面以下会露出画布外，所以按地面线把向下的射线截掉：
    爆炸效果本来也不该穿进地板。
    """
    u, v = m.pt(*p)
    gy = m.pt(0.0, 0.0, 0.0)[1]
    r = r * m.scale
    for i in range(8):
        a = 2 * math.pi * i / 8 + 0.35
        r0 = r * (0.35 + 0.10 * strength)
        r1 = r * (0.85 + 0.45 * ((i % 3) / 2.0)) * (0.7 + 0.3 * strength)
        m.c.line([(u + r0 * math.cos(a), min(v + r0 * math.sin(a), gy)),
                  (u + r1 * math.cos(a), min(v + r1 * math.sin(a), gy))],
                 gcol(m, "hi"), w=1.0, cap=True)
    disc(m, p, r * 0.42 * strength, gcol(m, "base"))
    disc(m, p, r * 0.20 * strength, C["warn_hi"])


def render(mob: Mob) -> Image.Image:
    return clean_specks(mob.finish(), 1)


def render_entity(cfg: dict, pose: Pose, size: int | None = None, ss: int = 8):
    """按 ENTITY 配置渲染一帧。size 给的是「1x 帧宽」，决定画布倍率。"""
    w, h = cfg["frame"]
    k = 1 if size is None else max(1, size // w)
    m = Mob(w * k, h * k, ss=ss)
    m.bind(pose, aspect=cfg["aspect"], ground=cfg["ground"], scale=cfg["scale"] * k)
    cfg["fn"](m)
    return render(m)


def measure_entity(cfg: dict, poses: list[Pose], ss: int = 8):
    """所有帧的并集外框（1x 像素）。造型改动后先跑这个，确认没有贴边。"""
    w, h = cfg["frame"]
    box = None
    for pose in poses:
        bb = render_entity(cfg, pose, ss=ss).getbbox()
        if bb is None:
            continue
        box = bb if box is None else (min(box[0], bb[0]), min(box[1], bb[1]),
                                      max(box[2], bb[2]), max(box[3], bb[3]))
    return box or (0, 0, w, h)


__all__ = [
    "PALETTE", "C", "Cam", "Pose", "Mob", "render", "render_entity",
    "gcol", "leg", "glow_pts", "disc", "eye", "burst",
    "measure_entity", "pixel_outline", "MALICE_COLORS", "MALICE_EXTRA",
    "GROUND", "convex_hull",
]
