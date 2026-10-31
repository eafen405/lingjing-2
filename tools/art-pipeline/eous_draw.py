#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""伊埃斯的绘制层：把 eous_model 的方位角投影落成具体像素。

约定：所有形状函数吃的是 **body units**（x 以中线为 0，y 耳尖 0 → 脚底 1.0），
Painter 负责套用 Pose 形变并转成画布坐标。
"""

from __future__ import annotations

import math

from PIL import Image

from artlib import _catmull_rom_closed, clean_specks, tapered_capsule, to_pixels
from eous_model import (
    Sub,
    A_BUTTON, A_EAR, A_EYE, A_FOOT, A_GLYPH, A_KNOT, A_STITCH, A_VISOR,
    BODY_H, C, COAT_HEM, COAT_TOP, EGG, PALETTE, Painter, Pose, SCARF,
    SCARF_TOP, VISOR_BOTTOM, VISOR_TOP, egg_half_width, proj, width_scale,
)


# --------------------------------------------------------------------------- 形状工具


def catmull_body(pts, n=16):
    return _catmull_rom_closed([(x, y) for x, y in pts], n)


def capsule_body(x0, y0, x1, y1, r0, r1):
    return tapered_capsule((x0, y0), (x1, y1), r0, r1)


# --------------------------------------------------------------------------- 投影部件


def visor_polygon(f: float, ws: float):
    """面罩开口：沿方位角采样再投影，天然得到透视收窄、拱顶与侧视包边。"""
    n = 18
    a = egg_half_width((VISOR_TOP + VISOR_BOTTOM) * 0.5)
    # 侧视时帽檐视觉上会"翻出来"，纯几何投影只剩一条缝，这里按注视角度放宽
    a_v = A_VISOR + 36.0 * abs(math.sin(math.radians(f)))
    top, bot = [], []
    for i in range(n + 1):
        al = -a_v + 2 * a_v * i / n
        x, d = proj(al, a, f)
        if d <= 0:
            continue
        k = 1 - math.cos(math.radians(al))
        top.append((x, VISOR_TOP + 0.085 * k))
        bot.append((x, VISOR_BOTTOM - 0.050 * k))
    if len(top) < 2:
        return None
    return top + bot[::-1]


def az(alpha: float, by: float, f: float, ws: float, dfwd: float = 0.0):
    """方位角 + 可选前后偏移 -> (屏幕 x, 深度)。dfwd 沿角色朝向，正值朝前。"""
    a = egg_half_width(by)
    x, d = proj(alpha, a, f)
    if dfwd:
        fr = math.radians(f)
        x += dfwd * math.sin(fr)
        d += dfwd * math.cos(fr)
    return x, d


# --------------------------------------------------------------------------- 绘制


def draw_eous(pose: Pose, size: int, ss: int = 8):
    p = Painter(size, ss, pose)
    c = p.c
    f = pose.f
    ws = width_scale(f)

    egg = [(x * ws, y) for x, y in EGG]
    egg_dense = catmull_body(egg)

    # ---------------- 兔耳 ----------------
    ears = []
    for side in (+1, -1):                       # +1 = 角色右耳
        a_base = egg_half_width(0.335)
        xb, db = proj(A_EAR * side, a_base, f)
        xt, dt = proj(A_EAR * side, a_base + 0.046, f)
        ears.append(dict(side=side, base=(xb, 0.340 + pose.ear_tilt * 0.0015),
                         tip=(xt, 0.030 - abs(pose.ear_tilt) * 0.0015),
                         depth=db))
    ears.sort(key=lambda e: e["depth"])

    for e in ears:
        base = e["base"]
        tip = e["tip"]
        cap = capsule_body(base[0], base[1], tip[0], tip[1], 0.094, 0.068)
        c.poly([p.P(*q) for q in cap],
               fill=C["coat_deep"] if e["side"] > 0 else C["hood"])
        c.line([p.P(*q) for q in cap] + [p.P(*cap[0])], C["outline"], w=1.0, cap=False)
        q0 = (base[0] + (tip[0] - base[0]) * 0.20, base[1] + (tip[1] - base[1]) * 0.20)
        q1 = (base[0] + (tip[0] - base[0]) * 0.86, base[1] + (tip[1] - base[1]) * 0.86)
        inner = capsule_body(q0[0], q0[1], q1[0], q1[1], 0.047, 0.038)
        c.poly([p.P(*q) for q in inner], fill=C["scarf"])
        c.line([p.P(*q) for q in inner] + [p.P(*inner[0])], C["scarf_deep"], w=1.0, cap=False)

    # ---------------- 蛋体 ----------------
    c.poly([p.P(*q) for q in egg_dense], fill=p.col("hood"))

    def hood_shade(t: Sub):
        t.poly([(-0.40, 0.10), (0.40, 0.10), (0.40, SCARF_TOP + 0.02),
                (-0.40, SCARF_TOP + 0.02)], fill=p.col("hood"))
        t.clipped(egg_dense, lambda u: u.blob(
            [(-0.34, 0.36), (-0.20, 0.212), (-0.02, 0.170), (0.10, 0.177),
             (-0.01, 0.250), (-0.14, 0.326), (-0.24, 0.420)],
            fill=p.col("hood_light")))
        t.clipped(egg_dense, lambda u: u.blob(
            [(0.05 * ws, 0.48), (0.28 * ws, 0.44), (0.315 * ws, 0.330),
             (0.226 * ws, 0.300), (0.150 * ws, 0.400), (0.052 * ws, 0.440)],
            fill=p.col("hood_shade")))

    p.clipped(egg_dense, hood_shade)

    # ---------------- 脚 ----------------
    # 脚挂在体底，不按身体表面投影（否则 45° 时会甩到轮廓外）
    feet = []
    for side in (+1, -1):
        lat = 0.132 * side
        fwd = 0.020 * side + 0.030 * side * pose.step
        fr = math.radians(f)
        xf = -lat * math.cos(fr) + fwd * math.sin(fr)
        df = lat * math.sin(fr) + fwd * math.cos(fr)
        lift = 0.0
        if abs(pose.step) > 0.01 and side * pose.step > 0:
            lift = 0.034
            xf += 0.012 * (1 if side > 0 else -1)
        feet.append((df, xf, lift))
    for df, xf, lift in sorted(feet):
        top = 0.800 - lift
        bot = 0.930 - lift
        leg = capsule_body(xf, top, xf, bot, 0.062, 0.058)
        c.poly([p.P(*q) for q in leg], fill=p.col("hood"))
        c.line([p.P(*q) for q in leg] + [p.P(*leg[0])], C["outline"], w=1.0, cap=False)
        p.c.circle(*p.P(xf + 0.006, top + 0.034), 0.026 * BODY_H, fill=p.col("hood_light"))

    # ---------------- 外套 ----------------
    def coat(t: Sub):
        t.poly([(-0.40, COAT_TOP), (0.40, COAT_TOP), (0.40, 1.02), (-0.40, 1.02)],
               fill=p.col("coat"))
        t.clipped(egg_dense, lambda u: u.blob(
            [(0.04 * ws, 0.62), (0.34 * ws, 0.66), (0.36 * ws, 0.80),
             (0.20 * ws, 0.92), (0.00, 0.94), (-0.05 * ws, 0.76)],
            fill=p.col("coat_shade")))
        t.clipped(egg_dense, lambda u: u.blob(
            [(-0.40, 0.600), (0.40, 0.600), (0.40, 0.622),
             (0.20 * ws, 0.640), (0.00, 0.648), (-0.22 * ws, 0.638), (-0.40, 0.620)],
            fill=p.col("coat_light")))
        t.line([(-0.34 * ws, 0.646), (-0.18 * ws, 0.652), (0.00, 0.656),
                (0.18 * ws, 0.648), (0.34 * ws, 0.634)], p.col("coat_shade"),
               w=1.0, smooth=True)

    p.clipped(egg_dense, coat)

    p.clipped(egg_dense, lambda t: t.poly(
        [(-0.40, COAT_HEM - 0.018), (0.40, COAT_HEM - 0.018),
         (0.40, 1.02), (-0.40, 1.02)], fill=p.col("coat_deep")))

    # 袖子 + X 缝线
    for side in (-1, 1):
        xs, ds = az(A_STITCH * side, 0.72, f, ws)
        if ds <= 0.04:
            continue
        sleeve = capsule_body(xs * 0.78, 0.646, xs * 0.97, 0.780, 0.048, 0.042)
        sleeve_c = [p.P(*q) for q in sleeve]
        p.clipped(egg_dense, lambda t: (
            t.c.poly(sleeve_c, fill=p.col("coat")),
            t.c.line(sleeve_c + [sleeve_c[0]], p.col("coat_shade"), w=1.0, cap=False)))
        cx, cy = p.P(xs * 0.92, 0.724)
        arm = 0.015 * BODY_H
        c.line([(cx - arm, cy - arm), (cx + arm, cy + arm)], p.col("hood_light"), w=1.0)
        c.line([(cx - arm, cy + arm), (cx + arm, cy - arm)], p.col("hood_light"), w=1.0)

    # 前襟 + 纽扣
    xb, db = az(A_BUTTON, 0.72, f, ws)
    if db > 0.02:
        seam = [(xb, 0.622 + i * (COAT_HEM - 0.632) / 4) for i in range(5)]
        dashed(p, seam, p.col("coat_deep"), w=1.0, on=2.0, off=2.0)
        for by in (0.664, 0.716, 0.768, 0.820):
            x2, _ = az(A_BUTTON - 4.0, by, f, ws)
            p.c.circle(*p.P(x2, by), 0.009 * BODY_H, fill=p.col("coat_deep"))
            p.c.circle(*p.P(x2, by), 0.004 * BODY_H, fill=p.col("coat_light"))

    # ---------------- 面罩 ----------------
    vis = visor_polygon(f, ws)
    if vis:
        vp = [p.P(*q) for q in vis]
        c.blob(vp, fill=p.col("visor"))
        c.blob(vp, stroke=C["outline"], w=1.0)

        def eyes(t):
            vmax = max(abs(q[0]) for q in vis)
            for side in (-1, +1):
                ex_, ey_ = az(A_EYE * side, 0.392, f, ws)
                if ey_ <= 0.05:
                    continue
                squeeze = max(0.62, min(1.0, ey_ / 0.24))
                # 钳在面罩内部，避免被边缘削成月牙
                ex_ = max(-vmax + 0.052, min(vmax - 0.052, ex_))
                t.ellipse(*p.P(ex_, 0.392), 0.045 * BODY_H * squeeze,
                            0.045 * BODY_H, fill=p.col("eye"))
                t.ellipse(*p.P(ex_, 0.392), 0.023 * BODY_H * squeeze,
                            0.023 * BODY_H, fill=p.col("visor"))
            for side in (-1, +1):
                ex_, ey_ = az(A_EYE * side, 0.392, f, ws)
                if ey_ <= 0.05:
                    continue
                ex_ = max(-vmax + 0.052, min(vmax - 0.052, ex_))
                hx, hy = c.to_px(p.P(ex_ - 0.014, 0.372))
                c.px_rect(hx, hy, 1, 1, C["eye_hi"])

        c.clipped(vp, eyes)

        c.clipped(vp, lambda u: u.blob(
            [(-0.19 * ws, VISOR_TOP + 0.034), (0.19 * ws, VISOR_TOP + 0.034),
             (0.19 * ws, VISOR_TOP - 0.006), (-0.19 * ws, VISOR_TOP - 0.006)],
            fill=C["visor_rim"]))

        upper = [(q[0] * 1.07, q[1] - 0.012) for q in vis[:len(vis) // 2 + 1]]
        dashed(p, upper, p.col("hood_deep"), w=1.0, on=2.5, off=3.0)

    # ---------------- 围巾 ----------------
    knot_x, knot_d = az(A_KNOT, 0.52, f, ws)
    knot_x += pose.sway
    if knot_d > -0.05:
        draw_knot(p, knot_x, knot_d)

    def scarf_front(t: Sub):
        sc = [(x * ws + pose.sway, y) for x, y in SCARF]
        t.blob(sc, fill=p.col("scarf"))
        inner = [(x * ws * 0.99 + pose.sway, y) for x, y in SCARF]
        t.clipped(inner, lambda u: u.blob(
            [(-0.40, 0.604), (0.40, 0.586), (0.40, 0.72), (-0.40, 0.74)],
            fill=p.col("scarf_shade")))
        t.clipped(inner, lambda u: u.blob(
            [(0.16 * ws, 0.444), (0.36 * ws, 0.454), (0.36 * ws, 0.68),
             (0.16 * ws, 0.68)], fill=p.col("scarf_shade")))
        t.clipped(inner, lambda u: u.blob(
            [(-0.30 * ws, 0.446), (0.12 * ws, 0.452), (0.12 * ws, 0.470),
             (-0.30 * ws, 0.464)], fill=p.col("scarf_light")))
        t.blob(sc, stroke=C["outline"], w=1.0)

    p.clipped(egg_dense, scarf_front)

    # ---------------- 背面细节（参考《伊埃斯（背）》）----------------
    # 兜帽中缝 + 背后拉链带 + 外套绿圆徽，只在这一面朝观察者时出现
    sx_b, d_b = az(180.0, 0.40, f, ws)
    if d_b > 0.10 * ws:
        seam = [(sx_b + 0.004 * math.sin(i / 6 * math.pi), 0.185 + i * 0.038)
                for i in range(6)]
        dashed(p, seam, p.col("hood_deep"), w=1.0, on=3.0, off=2.5)
        # 背上拉链带
        p.clipped(egg_dense, lambda t: t.poly(
            [(sx_b - 0.028, 0.648), (sx_b + 0.028, 0.648),
             (sx_b + 0.024, 0.862), (sx_b - 0.024, 0.862)],
            fill=p.col("coat_deep")))
        for i in range(5):
            yy = 0.664 + i * 0.040
            p.c.circle(*p.P(sx_b, yy), 0.006 * BODY_H, fill=p.col("coat_light"))

        # 外套背部的充电口：黄绿环 + 灰色芯
        exb, edb = az(152.0, 0.780, f, ws)
        if edb > 0.06:
            p.c.circle(*p.P(exb, 0.780), 0.050 * BODY_H, fill=C["eye_dark"])
            p.c.circle(*p.P(exb, 0.780), 0.042 * BODY_H, fill=C["eye"])
            p.c.circle(*p.P(exb, 0.780), 0.028 * BODY_H, fill=C["rivet"])
            p.c.circle(*p.P(exb, 0.780), 0.028 * BODY_H, stroke=C["rivet_dark"], w=1.0)
            p.c.circle(*p.P(exb, 0.780), 0.008 * BODY_H, fill=C["rivet_dark"])

    c.line([p.P(*q) for q in egg_dense] + [p.P(*egg_dense[0])],
           C["outline"], w=1.0, cap=False)

    for side in (-1, 1):
        xr, dr = az(A_EAR * side, 0.282, f, ws)
        if dr <= 0.03:
            continue
        p.c.circle(*p.P(xr, 0.282), 0.022 * BODY_H, fill=C["rivet"])
        p.c.circle(*p.P(xr, 0.282), 0.022 * BODY_H, stroke=C["outline"], w=1.0)
        p.c.circle(*p.P(xr, 0.282), 0.009 * BODY_H, fill=C["rivet_dark"])

    # ---------------- 「01」 ----------------
    gx, gd = az(A_GLYPH, 0.542, f, ws)
    squeeze = max(0.0, min(1.0, gd / 0.30))
    if squeeze >= 0.45 and pose.dim < 2:
        draw_glyph(p, gx, 0.542, squeeze)
    elif gd > 0.10 and pose.dim < 2:      # 侧面：标签只剩一条斜纹
        for k in range(3):
            xo = gx - 0.020 + k * 0.016
            p.c.line([p.P(xo, 0.508), p.P(xo + 0.014, 0.574)],
                     C["cream"], w=1.0, cap=False)

    return c


# --------------------------------------------------------------------------- 细节


def draw_knot(p: Painter, x: float, depth: float):
    """围巾结：贴着轮廓的一团 + 一条短而宽的飘带，不做细长尖刺。"""
    c = p.c
    s = 1.0 if x >= 0 else -1.0
    knot = [(x, 0.474), (x + s * 0.072, 0.452), (x + s * 0.098, 0.492),
            (x + s * 0.056, 0.536), (x, 0.530)]
    c.poly([p.P(*q) for q in knot], fill=p.col("scarf"))
    c.poly([p.P(*q) for q in knot], stroke=C["outline"], w=1.0)
    tail = [(x + s * 0.048, 0.470), (x + s * 0.196, 0.402),
            (x + s * 0.206, 0.470), (x + s * 0.096, 0.520)]
    c.poly([p.P(*q) for q in tail], fill=p.col("scarf"))
    c.poly([p.P(*q) for q in tail], stroke=C["outline"], w=1.0)
    c.poly([p.P(*q) for q in [(x + s * 0.058, 0.472), (x + s * 0.102, 0.452),
                              (x + s * 0.116, 0.468), (x + s * 0.070, 0.490)]],
           fill=p.col("scarf_light"))


def draw_glyph(p: Painter, gx: float, gy: float, squeeze: float):
    c = p.c
    ang = math.radians(-30.0)
    ca, sa = math.cos(ang), math.sin(ang)
    sw = 0.0185 * BODY_H * p.size
    gh, gw, gap = 0.108, 0.066, 0.028

    def G(u, v):
        uu = u * squeeze
        return p.P(gx + uu * ca - v * sa, gy + uu * sa + v * ca)

    zero_u = -(gw + gap) * 0.5 * 0.95
    ring = [G(zero_u + gw * 0.5 * math.cos(2 * math.pi * i / 40),
              gh * 0.5 * math.sin(2 * math.pi * i / 40)) for i in range(40)]
    c.line(ring + [ring[0]], C["cream"], w=sw)
    c.line([G(zero_u - gw * 0.36, gh * 0.34), G(zero_u + gw * 0.36, -gh * 0.34)],
           C["cream"], w=sw)
    one_u = (gw + gap) * 0.5 * 0.95
    c.line([G(one_u, -gh * 0.5), G(one_u, gh * 0.5)], C["cream"], w=sw)
    c.line([G(one_u - gw * 0.42, -gh * 0.28), G(one_u, -gh * 0.5)], C["cream"], w=sw)
    c.line([G(one_u - gw * 0.34, gh * 0.5), G(one_u + gw * 0.34, gh * 0.5)],
           C["cream"], w=sw)


def dashed(p: Painter, body_pts, color, w=1.0, on=2.5, off=3.0):
    c = p.c
    seq = [p.P(x, y) for x, y in body_pts]
    unit = 1.0 / c.size
    segs, total = [], 0.0
    for a, b in zip(seq[:-1], seq[1:]):
        d = math.hypot(b[0] - a[0], b[1] - a[1])
        segs.append((a, b, d))
        total += d
    if total <= 0:
        return
    on_u, off_u = on * unit, off * unit
    period = on_u + off_u
    t = -period
    while t < total:
        a0, b0 = max(0.0, t), min(total, t + on_u)
        if b0 > a0:
            pts = [_at(segs, a0)]
            acc = 0.0
            for a, b, d in segs:
                if acc + d < a0 or acc > b0:
                    acc += d
                    continue
                lo = max(0.0, (a0 - acc) / d) if d else 0.0
                hi = min(1.0, (b0 - acc) / d) if d else 0.0
                if hi > lo:
                    pts.append((a[0] + (b[0] - a[0]) * lo, a[1] + (b[1] - a[1]) * lo))
                    pts.append((a[0] + (b[0] - a[0]) * hi, a[1] + (b[1] - a[1]) * hi))
                acc += d
            pts.append(_at(segs, b0))
            if len(pts) >= 2:
                c.line(pts, color, w=w, cap=False)
        t += period


def _at(segs, t):
    acc = 0.0
    for a, b, d in segs:
        if acc + d >= t:
            r = (t - acc) / d if d else 0.0
            return (a[0] + (b[0] - a[0]) * r, a[1] + (b[1] - a[1]) * r)
        acc += d
    return segs[-1][1]


# --------------------------------------------------------------------------- 输出


def render(pose: Pose, size: int, ss: int = 8) -> Image.Image:
    canvas = draw_eous(pose, size, ss)
    return clean_specks(to_pixels(canvas, PALETTE), 1)
