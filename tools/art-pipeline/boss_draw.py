#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""两个恶名以骸（Boss）的造型：连环 / 骤雨。

和小怪同一套语言（黑青装甲 + 酸绿以太缝，预警转红），但大一号：
96×96 画布 = 3 个世界单位，站在伊埃斯（1.75u）旁边接近两倍高。

* 连环 —— 驼背的四足冲撞兽：厚重的躯干 + 后掠的双角 + 缠在身上的锁链，
           冲锋时整条锁链被拉直拖在身后（「连环」）
* 骤雨 —— 三条细腿撑起的立柱，顶上是一圈张开的「花瓣」，中心是发光核心；
           施法时花瓣张开、核心全亮（雨是程序侧的弹幕，不出在图里）

两者都只在剪影层面区分：一个横着压过来，一个竖着立在那里。
"""

from __future__ import annotations

import math

from malice_model import (
    C, Mob, Pose, burst, disc, eye, gcol, glow_pts, leg, measure_entity,
    render_entity,
)

# --------------------------------------------------------------------------- 链


def chain(m: Mob, p0, p1, p2, links: int = 7, r: float = 0.030, tone: str = "plate"):
    """沿二次贝塞尔铺一串链环；链环本身不投影厚度，只按中心点放小圈。"""
    col = m.colour(tone)
    for i in range(links):
        t = i / max(1, links - 1)
        x = (1 - t) ** 2 * p0[0] + 2 * (1 - t) * t * p1[0] + t * t * p2[0]
        h = (1 - t) ** 2 * p0[1] + 2 * (1 - t) * t * p1[1] + t * t * p2[1]
        z = (1 - t) ** 2 * p0[2] + 2 * (1 - t) * t * p1[2] + t * t * p2[2]
        u, v = m.pt(x, h, z)
        m.c.circle(u, v, r * m.scale, stroke=col, w=1.0)
        if i % 2 == 0:
            m.c.circle(u, v, r * 0.35 * m.scale, fill=gcol(m, "dim"))


# --------------------------------------------------------------------------- 连环


def draw_chain_beast(m: Mob) -> None:
    """恶名以骸·连环：驼背四足兽，双角后掠，断链拖在身后。"""
    p = m.pose
    ext = p.extend
    lunge = ext * 0.10

    # ---- 四足
    legs = []
    for sx in (+1, -1):
        for sz in (+1, -1):
            hip = (sx * 0.130, 0.330, sz * 0.100)
            ft_z = sz * 0.200 + 0.028 * (1 if sz > 0 else -1) * p.step
            foot = (sx * 0.255, 0.0, ft_z + lunge)
            knee = (hip[0] + sx * 0.095, 0.170, hip[2] + (foot[2] - hip[2]) * 0.42)
            legs.append((m.z(*knee), hip, knee, foot))
    for _, hip, knee, foot in sorted(legs, key=lambda t: t[0]):
        leg(m, hip, knee, foot, 0.050, 0.017, joint=True)

    # ---- 拖在身后的断链（先画，压在身体下面）
    sway = p.bob * 1.2 + ext * 0.10
    chain(m, (0.03, 0.500, -0.280), (0.11, 0.280 + sway, -0.380),
          (0.17, 0.070, -0.400), links=7, r=0.026)

    # ---- 躯干（后高前低）
    plan = [(-0.145, -0.240), (0.145, -0.240), (0.180, -0.070),
            (0.075, 0.150), (-0.075, 0.150), (-0.180, -0.070)]
    m.solid(plan, 0.330, 0.700, top_scale=0.72,
            top=m.colour("plate"), side_lit=m.colour("plate"),
            side_mid=m.colour("shell_light"), side_dark=m.colour("shell"))

    # ---- 头：亮一档 + 骨甲，和躯干分得开
    head = [(-0.070, 0.120), (0.070, 0.120), (0.086, 0.235),
            (0.000, 0.320), (-0.086, 0.235)]
    m.solid(head, 0.340 + ext * 0.03, 0.520, top_scale=0.70,
            top=m.colour("rivet"), side_lit=m.colour("shell_light"),
            side_mid=m.colour("shell"), side_dark=m.colour("shell_deep"))

    # ---- 双角：从头的两侧向前上方顶出去
    for sx in (+1, -1):
        m.cap3((sx * 0.076, 0.520, 0.180), (sx * 0.200, 0.630, 0.262),
               0.036, 0.006, m.colour("rivet"))

    # ---- 背上的三根骨刺
    for i, z in enumerate((-0.170, -0.040, 0.075)):
        hgt = (0.780, 0.805, 0.760)[i]
        m.cap3((0.0, 0.690, z + 0.030), (0.0, hgt, z - 0.060),
               0.032, 0.005, m.colour("rivet"))

    # ---- 眼（压在头的正面）
    for sx in (+1, -1):
        eye(m, (sx * 0.052, 0.452, 0.238), 0.030, ry=0.024)

    # ---- 缠在胸口的锁链
    chain(m, (-0.190, 0.400, 0.080), (0.000, 0.480, 0.120),
          (0.185, 0.560, -0.020), links=6, r=0.024, tone="shell_light")

    # ---- 背脊以太缝
    glow_pts(m, [(0.0, 0.705, -0.140), (0.0, 0.705, 0.040)], w=1.0, tone="dim")


# --------------------------------------------------------------------------- 骤雨


def draw_rain_shrine(m: Mob) -> None:
    """恶名以骸·骤雨：三腿立柱 + 顶部张开的花瓣与核心。"""
    p = m.pose
    open_amt = 1.0 + (0.18 if p.warn >= 0.5 else 0.0) + p.flash * 0.22

    # ---- 三足
    legs = []
    for sx, sz in ((-1, 0.75), (1, 0.75), (0, -0.95)):
        hip = (sx * 0.105, 0.345, sz * 0.080)
        foot = (sx * 0.285, 0.0, sz * 0.205)
        knee = (sx * 0.210, 0.175, sz * 0.165)
        legs.append((m.z(*knee), hip, knee, foot))
    for _, hip, knee, foot in sorted(legs, key=lambda t: t[0]):
        leg(m, hip, knee, foot, 0.050, 0.017, joint=True)

    # ---- 立柱
    plan = [(-0.075, -0.090), (0.075, -0.090), (0.105, -0.020), (0.105, 0.070),
            (0.060, 0.120), (-0.060, 0.120), (-0.105, 0.070), (-0.105, -0.020)]
    m.solid(plan, 0.380, 0.640, top_scale=0.90,
            top=m.colour("shell_light"), side_lit=m.colour("plate"),
            side_mid=m.colour("shell_light"), side_dark=m.colour("shell"))

    # ---- 正面灯带 + 眼
    u0, v0 = m.pt(0.0, 0.470, 0.108)
    m.c.line([(u0 - 0.070, v0), (u0 + 0.070, v0)], gcol(m, "base"), w=1.0)
    eye(m, (0.0, 0.545, 0.095), 0.032, ry=0.026)

    # ---- 顶部花瓣（6 片，向外向上张开）
    for i in range(6):
        a = math.pi * (0.15 + 0.70 * i / 5)
        bx, bz = -0.105 * math.cos(a), 0.105 * math.sin(a)
        tx = bx * (3.1 * open_amt)
        tz = bz * (3.1 * open_amt)
        m.cap3((bx, 0.630, bz), (tx, 0.620 + 0.165 * open_amt, tz),
               0.045, 0.010, m.colour("shell_light"))

    # ---- 核心：花瓣中间的光团
    disc(m, (0.0, 0.690, 0.0), 0.120, m.colour("shell_deep"))
    disc(m, (0.0, 0.690, 0.0), 0.092, gcol(m, "dim"))
    disc(m, (0.0, 0.690, 0.0), 0.060 + 0.030 * p.flash, gcol(m, "base"))
    disc(m, (0.0, 0.700, 0.0), 0.028 + 0.020 * p.flash, gcol(m, "hi"))

    # ---- 立柱上的两道竖向以太缝
    for sx in (+1, -1):
        glow_pts(m, [(sx * 0.104, 0.610, -0.030), (sx * 0.104, 0.410, -0.030)],
                 w=1.0, tone="dim")

    # ---- 施法时从花瓣向上溢出的光
    if p.flash > 0:
        burst(m, (0.0, 0.715, 0.0), 0.050 + 0.050 * p.flash, 0.8 * p.flash)


# --------------------------------------------------------------------------- 出口

ENTRY = {
    "chain": dict(fn=draw_chain_beast, frame=(96, 96), aspect=1.0,
                       ground=0.9167, scale=1.00),
    "downpour": dict(fn=draw_rain_shrine, frame=(96, 96), aspect=1.0,
                       ground=0.9167, scale=1.00),
}

# 与小怪的命名保持一致：恶名以骸也用英文 id
ALIAS = {"chain": "连环", "downpour": "骤雨"}


# --------------------------------------------------------------------------- 姿态表

STATES = {
    "chain": {
        "idle": dict(dirs=3, fps=10, loop=True, frames=[
            dict(bob=0.000),
            dict(bob=0.010),
        ]),
        "charge": dict(dirs=3, fps=10, loop=True, frames=[
            dict(warn=1.0, squash=0.94, camber=0.12),
            dict(camber=0.26, extend=1.00),
        ]),
        "summon": dict(dirs=3, fps=10, loop=True, frames=[
            dict(camber=-0.15),
            dict(camber=-0.22, flash=0.7),
        ]),
        "death": dict(dirs=1, fps=10, loop=False, frames=[
            dict(squash=0.90, dim=1, camber=0.06),
            dict(squash=0.72, dim=1, camber=0.14),
            dict(squash=0.52, dim=2, camber=0.20),
            dict(squash=0.36, dim=3, camber=0.24),
        ]),
    },
    "downpour": {
        "idle": dict(dirs=1, fps=10, loop=True, frames=[
            dict(bob=0.008),
            dict(bob=0.022),
        ]),
        "cast": dict(dirs=1, fps=10, loop=True, frames=[
            dict(warn=1.0, squash=0.96),
            dict(warn=1.0, flash=0.55, camber=-0.08),
            dict(flash=1.00, camber=-0.12),
        ]),
        "death": dict(dirs=1, fps=10, loop=False, frames=[
            dict(squash=0.86, dim=1),
            dict(squash=0.66, dim=2),
            dict(squash=0.46, dim=2),
            dict(squash=0.30, dim=3),
        ]),
    },
}


def measure(entity: str, poses: list[Pose], ss: int = 8):
    return measure_entity(ENTRY[entity], poses, ss=ss)


def render_boss(entity: str, pose: Pose, size: int | None = None, ss: int = 8):
    return render_entity(ENTRY[entity], pose, size=size, ss=ss)
