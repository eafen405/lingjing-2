#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""四种以骸的造型：尾灯 / 撞针 / 钉枪 / 坠物。

共同语言（取自 `source/reference/某个精英怪.png`）：
黑青装甲 + 酸绿的「以太」发光缝，预警时发光转为红色。
32×32 下细节会糊，所以每个个体只靠**剪影**与**一处发光焦点**辨认：

* 尾灯 —— 悬浮的钟形壳 + 下方透镜 + 垂须，没有脚
* 撞针 —— 尖头的六面楔形机体 + 下勾的喙与两侧獠牙 + 四条刀足
* 钉枪 —— 八面竖立机体 + 横置炮管 + 三脚架，整体像个炮台
* 坠物 —— 尖头朝下的六棱结晶 + 顶部外翻的鳍，没有腿

明度分工（32px 主要靠它，而不是靠细节）：
受光面比侧面的亮一档，细的四肢比躯干亮，最暗的一档只留给轮廓与缝。
"""

from __future__ import annotations

import math

from PIL import Image

from malice_model import C, Mob, Pose, measure_entity, render, render_entity

from malice_model import burst, disc, eye, gcol, glow_pts, leg


# --------------------------------------------------------------------------- 尾灯


def draw_taillight(m: Mob) -> None:
    """悬空的灯形以骸：钟形壳 + 透镜 + 四条垂须。永远正面朝人。"""
    p = m.pose
    shell = m.colour("shell_light")
    shell_d = m.colour("shell")
    limb = m.colour("plate")

    # ---- 垂须（先画，向外张开并与壳体错开，才看得见）
    for i, ang in enumerate((-0.95, -0.32, 0.32, 0.95)):
        sway = math.sin(p.bob * 60 + i * 1.1) * 0.016
        top = (ang * 0.11, 0.315, -0.03)
        mid = (ang * 0.235 + sway, 0.200, -0.01)
        tip = (ang * 0.295 + sway * 1.7, 0.150, 0.01)
        m.cap3(top, mid, 0.024, 0.016, limb)
        m.cap3(mid, tip, 0.016, 0.009, limb)

    # ---- 钟形壳
    u0, v0 = m.pt(0.0, 0.375, 0.0)
    ct = m.cam.toward((0.0, 0.0, 0.0))
    half, dome = 0.215 * m.scale, 0.275 * m.scale
    pts = []
    for i in range(21):
        a = math.pi * (1 - i / 20)
        pts.append((u0 + half * math.cos(a) * (0.90 + 0.10 * abs(math.sin(a))),
                    v0 - dome * math.sin(a) * (0.94 + 0.06 * math.cos(a))))
    m.c.poly(pts, fill=shell)
    # 受光的上左面
    m.c.poly([(u0 - half * 0.88, v0 - 0.02), (u0 - half * 0.50, v0 - dome * 0.72),
              (u0 - half * 0.06, v0 - dome * 0.94), (u0 - half * 0.11, v0 - dome * 0.58),
              (u0 - half * 0.43, v0 - dome * 0.13)], fill=m.colour("plate"))
    # 壳底阴影
    m.c.poly([(u0 - half * 0.95, v0 - 0.015), (u0 + half * 0.95, v0 - 0.015),
              (u0 + half * 0.80, v0 + 0.028), (u0 - half * 0.80, v0 + 0.028)],
             fill=shell_d)

    # ---- 透镜（整只以骸的视觉焦点）
    eye(m, (0.0, 0.300, 0.04), 0.100)

    # ---- 顶端尖刺
    m.cap3((0.0, 0.630, 0.0), (0.0, 0.735, -0.01), 0.022, 0.004, limb)


# --------------------------------------------------------------------------- 撞针


def draw_striker(m: Mob) -> None:
    """尖头楔形机体 + 下勾的喙 + 两侧獠牙 + 四条刀足。"""
    p = m.pose
    ext = p.extend
    zb = 0.135 + ext * 0.020          # 喙根随冲锋前移
    tip_z = zb + 0.172 + ext * 0.030  # 尖端：伸到画布安全边为止

    # ---- 四足
    legs = []
    for sx in (+1, -1):
        for sz in (+1, -1):
            hip = (sx * 0.095, 0.235, sz * 0.075)
            ft_z = sz * 0.175 + 0.022 * (1 if sz > 0 else -1) * p.step
            foot = (sx * 0.215, 0.0, ft_z)
            knee = (hip[0] + sx * 0.085, 0.125, hip[2] + (foot[2] - hip[2]) * 0.40)
            legs.append((m.z(*knee), hip, knee, foot))
    for _, hip, knee, foot in sorted(legs, key=lambda t: t[0]):
        leg(m, hip, knee, foot, 0.042, 0.014, joint=True)

    # ---- 肩甲（向后上方翘起，破掉方块剪影）
    for sx in (+1, -1):
        m.cap3((sx * 0.105, 0.395, -0.110), (sx * 0.170, 0.495, -0.235),
               0.046, 0.010, m.colour("shell"))

    # ---- 机体：六面截面，前端收成尖
    plan = [(-0.100, -0.185), (0.100, -0.185), (0.128, -0.050),
            (0.052, 0.135), (-0.052, 0.135), (-0.128, -0.050)]
    m.solid(plan, 0.190, 0.390, top_scale=0.74,
            top=m.colour("plate"), side_lit=m.colour("plate"),
            side_mid=m.colour("shell_light"), side_dark=m.colour("shell"))

    # ---- 喙：骨白，和机体拉开明度；向前下方勾，正面视角也保留纵向长度
    m.cap3((0.0, 0.300, zb - 0.060), (0.0, 0.150, tip_z),
           0.050, 0.006, m.colour("rivet"))


    # ---- 两侧獠牙
    for sx in (+1, -1):
        m.cap3((sx * 0.052, 0.300, zb - 0.080),
               (sx * 0.118, 0.205, zb + 0.112 + ext * 0.030),
               0.028, 0.005, m.colour("shell_light"))

    # ---- 一对眼
    for sx in (+1, -1):
        eye(m, (sx * 0.036, 0.302, 0.115), 0.025, ry=0.020)

    # ---- 脊线
    glow_pts(m, [(0.0, 0.400, -0.150), (0.0, 0.408, -0.020)], w=1.0, tone="dim")


# --------------------------------------------------------------------------- 钉枪


def draw_nailgun(m: Mob) -> None:
    """炮台形远程敌人：八面竖立机体 + 横置炮管 + 三脚架。"""
    p = m.pose
    ext = p.extend

    # ---- 三足（张开得比机体宽，才不会读成人形）
    legs = []
    for sx, sz in ((-1, 0.55), (1, 0.55), (0, -0.95)):
        hip = (sx * 0.065, 0.175, sz * 0.050)
        foot = (sx * 0.195, 0.0, sz * 0.155)
        knee = (sx * 0.150, 0.095, sz * 0.120)
        legs.append((m.z(*knee), hip, knee, foot))
    for _, hip, knee, foot in sorted(legs, key=lambda t: t[0]):
        leg(m, hip, knee, foot, 0.034, 0.013, joint=True)

    # ---- 后部弹鼓（先画，压在机体后面）
    disc(m, (0.0, 0.330, -0.150), 0.082, m.colour("shell"))
    disc(m, (0.0, 0.330, -0.140), 0.052, m.colour("shell_deep"))

    # ---- 机体：八面截面
    plan = [(-0.072, -0.105), (0.072, -0.105), (0.100, -0.030), (0.100, 0.060),
            (0.058, 0.110), (-0.058, 0.110), (-0.100, 0.060), (-0.100, -0.030)]
    m.solid(plan, 0.125, 0.420, top_scale=0.88,
            top=m.colour("plate"), side_lit=m.colour("plate"),
            side_mid=m.colour("shell_light"), side_dark=m.colour("shell"))

    # ---- 炮管（开火时像活塞一样伸长，并喷出枪口焰）
    b0 = (0.0, 0.285, 0.060 + ext * 0.020)
    b1 = (0.0, 0.285, 0.255 + ext * 0.040)
    m.cap3(b0, b1, 0.058, 0.046, m.colour("plate"))
    m.cap3((0.0, 0.285, 0.150), (0.0, 0.285, b1[2] - 0.040), 0.026, 0.026,
           m.colour("shell"))
    disc(m, b1, 0.044, m.colour("shell_deep"))
    disc(m, (b1[0], b1[1], b1[2] + 0.005), 0.025, gcol(m, "base"))
    if p.flash > 0:
        burst(m, b1, 0.028 + 0.022 * p.flash, 0.9 * p.flash)

    # ---- 正面横向灯带 + 瞄具眼
    u0, v0 = m.pt(0.0, 0.360, 0.098)
    m.c.line([(u0 - 0.062, v0), (u0 + 0.062, v0)], gcol(m, "base"), w=1.0)
    eye(m, (0.0, 0.450, 0.040), 0.028, ry=0.022)

    # ---- 机体两侧的竖缝
    for sx in (+1, -1):
        glow_pts(m, [(sx * 0.100, 0.350, -0.060), (sx * 0.098, 0.255, -0.060)],
                 w=1.0, tone="dim")


# --------------------------------------------------------------------------- 坠物


def draw_dropper(m: Mob) -> None:
    """从天而降的以太桩：宽顶盖 + 收束的锥身 + 一根直插地面的穿刺尖。

    剪影是「倒锥 + 平顶」——和另外三只的圆形/楔形/炮台形完全不重叠。
    """
    p = m.pose
    sq = p.squash
    big = [(-0.220, -0.130), (0.220, -0.130), (0.255, 0.010),
           (0.000, 0.215), (-0.255, 0.010)]
    small = [(x * 0.46, z * 0.46) for x, z in big]

    # ---- 顶部两片稳定鳍
    for sx in (+1, -1):
        m.cap3((sx * 0.100, 0.660 * sq, -0.020), (sx * 0.215, 0.790 * sq, -0.070),
               0.036, 0.006, m.colour("shell"))

    # ---- 穿刺尖（先画，桩体会盖住根部）
    m.cone(small, 0.300 * sq, 0.000, side_lit=m.colour("rivet"),
           side_mid=m.colour("plate"), side_dark=m.colour("shell_light"))

    # ---- 锥身：下窄上宽
    m.solid(small, 0.290 * sq, 0.610 * sq, top_scale=1.0 / 0.46,
            side_lit=m.colour("plate"), side_mid=m.colour("shell_light"),
            side_dark=m.colour("shell"))

    # ---- 顶盖
    m.solid(big, 0.600 * sq, 0.690 * sq, top_scale=0.90,
            top=m.colour("plate"), side_lit=m.colour("plate"),
            side_mid=m.colour("shell_light"), side_dark=m.colour("shell"))

    # ---- 三道螺旋环（用短横线暗示）
    for hh, wid in ((0.560, 0.60), (0.470, 0.48), (0.385, 0.36)):
        u, v = m.pt(0.0, hh * sq, 0.0)
        m.c.line([(u - 0.085 * wid, v - 0.012), (u + 0.085 * wid, v + 0.012)],
                 m.colour("shell"), w=1.0)

    # ---- 体内核心：正面十字缝透光
    if m.cam.cos > 0.15:
        u, v = m.pt(0.0, 0.500 * sq, 0.120)
        m.c.line([(u, v - 0.045), (u, v + 0.045)], gcol(m, "base"), w=1.0)
        m.c.line([(u - 0.034, v + 0.004), (u + 0.034, v + 0.004)], gcol(m, "base"), w=1.0)
        m.c.poly([(u - 0.009, v - 0.009), (u + 0.009, v - 0.009),
                  (u + 0.009, v + 0.009), (u - 0.009, v + 0.009)], fill=gcol(m, "hi"))

    # ---- 落地炸开
    if p.flash > 0:
        burst(m, (0.0, 0.130, 0.0), 0.045 + 0.045 * p.flash)


# --------------------------------------------------------------------------- 出口

ENTRY = {
    # 帧尺寸 / 宽高比 / 地平线（世界单位，从画布顶算起）/ 绘制函数
    #
    # 64×64 而不是 32×32：PPU 32 下 32px 只有 1 个世界单位，撑满也只有伊埃斯
    # 一半高。画布翻倍后同一份造型在世界里就是 2 倍高（约 1.5u），和小怪
    # 「比主角略小」的定位对得上，像素密度也与伊埃斯一致（都是 1px 描边）。
    "taillight": dict(fn=draw_taillight, frame=(64, 64), aspect=1.0, ground=0.9219, scale=1.00),
    "striker": dict(fn=draw_striker, frame=(64, 64), aspect=1.0, ground=0.9219, scale=1.18),
    "nailgun": dict(fn=draw_nailgun, frame=(64, 64), aspect=1.0, ground=0.9219, scale=1.30),
    "dropper": dict(fn=draw_dropper, frame=(64, 96), aspect=1.5, ground=1.4219, scale=0.95),
}
ENTITY = ENTRY


def measure(entity: str, poses: list[Pose], ss: int = 8):
    return measure_entity(ENTITY[entity], poses, ss=ss)


def render_mob(entity: str, pose: Pose, size: int | None = None, ss: int = 8):
    return render_entity(ENTITY[entity], pose, size=size, ss=ss)


# --------------------------------------------------------------------------- 姿态表

STATES = {
    "taillight": {
        "idle": dict(dirs=1, fps=12, loop=True, frames=[
            dict(bob=0.000),
            dict(bob=0.018),
        ]),
        "move": dict(dirs=1, fps=12, loop=True, frames=[
            dict(bob=0.055, camber=0.05),
            dict(bob=0.030, camber=0.09),
        ]),
        "death": dict(dirs=1, fps=12, loop=False, frames=[
            dict(squash=0.98, dim=1, bob=-0.020),
            dict(squash=0.92, dim=2, bob=-0.055),
            dict(squash=0.84, dim=3, bob=-0.085),
        ]),
    },
    "striker": {
        "idle": dict(dirs=3, fps=12, loop=True, frames=[
            dict(step=0.0, bob=0.004),
        ]),
        "move": dict(dirs=3, fps=12, loop=True, frames=[
            dict(step=1.0, bob=0.014),
            dict(step=-1.0, bob=0.014),
        ]),
        "telegraph": dict(dirs=3, fps=12, loop=True, frames=[
            dict(warn=1.0, squash=0.90, camber=-0.10),
            dict(warn=1.0, squash=0.86, camber=-0.16),
        ]),
        "charge": dict(dirs=3, fps=12, loop=True, frames=[
            dict(camber=0.10, extend=0.55),
            dict(camber=0.20, extend=1.00),
        ]),
        "death": dict(dirs=1, fps=12, loop=False, frames=[
            dict(squash=0.82, dim=1, camber=0.10),
            dict(squash=0.55, dim=2, camber=0.22),
            dict(squash=0.30, dim=3, camber=0.30),
        ]),
    },
    "nailgun": {
        "idle": dict(dirs=3, fps=12, loop=True, frames=[
            dict(step=0.0, bob=0.003),
        ]),
        "move": dict(dirs=3, fps=12, loop=True, frames=[
            dict(step=1.0, bob=0.012),
            dict(step=-1.0, bob=0.012),
        ]),
        "fire": dict(dirs=3, fps=12, loop=True, frames=[
            dict(extend=-0.35),
            dict(extend=1.00, flash=1.0),
        ]),
        "death": dict(dirs=1, fps=12, loop=False, frames=[
            dict(squash=0.80, dim=1),
            dict(squash=0.52, dim=2),
            dict(squash=0.28, dim=3),
        ]),
    },
    "dropper": {
        "idle": dict(dirs=1, fps=12, loop=True, frames=[
            dict(bob=0.020),
            dict(bob=0.034),
        ]),
        "telegraph": dict(dirs=1, fps=12, loop=True, frames=[
            dict(warn=1.0, bob=0.045, camber=0.03),
            dict(warn=1.0, bob=0.030, camber=-0.03),
        ]),
        "impact": dict(dirs=1, fps=12, loop=False, frames=[
            dict(squash=0.82, bob=0.000, flash=0.55),
            dict(squash=0.58, bob=-0.005, flash=0.90),
            dict(squash=0.40, bob=-0.010, dim=1, flash=0.35),
        ]),
        "death": dict(dirs=1, fps=12, loop=False, frames=[
            dict(squash=0.52, bob=-0.008, dim=2, flash=0.20),
            dict(squash=0.34, bob=-0.014, dim=3),
        ]),
    },
}
