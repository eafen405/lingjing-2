#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""伊埃斯 master 绘制（front / S 视角），以《伊埃斯（凶）.png》为准。

设计空间：body units —— x 以身体中线为 0，y 从耳尖 0 到脚底 1.0。
纵向分区：
    耳尖 0.00 / 兜帽 0.175–0.575 / 面罩拱 0.252–0.514 / 围巾 0.452–0.590
    / 外套 0.500–0.835 / 脚 0.855–0.985
横向：兜帽 0.59，外套 0.63，整体含飘带 ≤ 0.70 —— 与参考图宽高比 0.65 一致。

master 输出 128×128（128 -> 64 -> 32 均为整数倍）。
"""

from __future__ import annotations

import math
import os

from PIL import Image, ImageDraw, ImageFont

from artlib import (
    Canvas,
    Palette,
    clean_specks,
    hex2rgb,
    on_checker,
    silhouette,
    tapered_capsule,
    to_pixels,
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

# --------------------------------------------------------------------------- 布局

BODY_H = 0.94
BODY_TOP = 0.03
CENTER_X = 0.5


def P(bx: float, by: float) -> tuple[float, float]:
    return (CENTER_X + bx * BODY_H, BODY_TOP + by * BODY_H)


def mirror(pts):
    return [(-x, y) for x, y in pts]


def half_to_full(pts):
    return pts + mirror(pts[-2::-1])


# 兜帽：高圆顶（宽高比 ≈ 1.1），下缘被围巾压住
# 整体蛋形剪影：兜帽/围巾/外套都是画在这个蛋上的横带，不是「圆头 + 圆身」两段
EGG = half_to_full([
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
])

# 面罩：拱形开口（画在蛋的正面）
VISOR = half_to_full([
    (0.000, 0.228),
    (0.050, 0.233),
    (0.096, 0.249),
    (0.134, 0.279),
    (0.158, 0.320),
    (0.167, 0.380),
    (0.167, 0.448),
    (0.159, 0.496),
    (0.098, 0.514),
    (0.000, 0.518),
])

# 围巾：横带 + 中间收尖 + 右侧打结
SCARF = [
    (-0.314, 0.470),
    (-0.214, 0.448),
    (-0.086, 0.442),
    (0.048, 0.448),
    (0.164, 0.456),
    (0.262, 0.464),
    (0.314, 0.488),
    (0.306, 0.546),
    (0.258, 0.584),
    (0.184, 0.610),
    (0.098, 0.622),
    (0.012, 0.640),
    (-0.072, 0.620),
    (-0.162, 0.604),
    (-0.250, 0.578),
    (-0.310, 0.540),
]

EAR_L = dict(base=(-0.134, 0.338), tip=(-0.158, 0.026), r0=0.096, r1=0.070)
EAR_R = dict(base=(0.136, 0.342), tip=(0.162, 0.018), r0=0.096, r1=0.072)

EYE = dict(x=0.077, y=0.392, rx=0.045, ry=0.045, pupil=0.023)

SCARF_TOP = 0.470      # 围巾上沿 = 兜帽白区下界
COAT_TOP = 0.600       # 外套上界
COAT_HEM = 0.818       # 下摆（露出下面白色蛋底与腿）


# --------------------------------------------------------------------------- 虚线


def _point_at(segs, t):
    acc = 0.0
    for a, b, d in segs:
        if d <= 0:
            continue
        if acc + d >= t:
            r = (t - acc) / d
            return (a[0] + (b[0] - a[0]) * r, a[1] + (b[1] - a[1]) * r)
        acc += d
    return segs[-1][1]


def _emit(c, segs, t0, t1, color, w):
    pts = [_point_at(segs, t0)]
    acc = 0.0
    for a, b, d in segs:
        if acc + d <= t0:
            acc += d
            continue
        if acc >= t1:
            break
        lo = max(0.0, (t0 - acc) / d) if d else 0.0
        hi = min(1.0, (t1 - acc) / d) if d else 0.0
        if hi > lo:
            pts.append((a[0] + (b[0] - a[0]) * lo, a[1] + (b[1] - a[1]) * lo))
            pts.append((a[0] + (b[0] - a[0]) * hi, a[1] + (b[1] - a[1]) * hi))
        acc += d
    pts.append(_point_at(segs, t1))
    if len(pts) >= 2:
        c.line(pts, color, w=w, cap=False)


def dashed_line(c: Canvas, pts, color, w=1.0, on=2.5, off=2.5, closed=False, phase=0.0):
    seq = list(pts)
    if closed:
        seq = seq + [seq[0]]
    segs = []
    total = 0.0
    for a, b in zip(seq[:-1], seq[1:]):
        d = math.hypot(b[0] - a[0], b[1] - a[1])
        segs.append((a, b, d))
        total += d
    if total <= 0:
        return
    unit = 1.0 / c.size
    on_u, off_u = on * unit, off * unit
    if on_u <= 0:
        return
    period = on_u + off_u
    t = (phase * unit) % period - period
    while t < total:
        a, b = max(0.0, t), min(total, t + on_u)
        if b > a:
            _emit(c, segs, a, b, color, w)
        t += period


def draw_eous_front(size: int, ss: int = 8) -> Canvas:
    from artlib import _catmull_rom_closed

    c = Canvas(size, ss=ss)

    # ---------------- 兔耳（在蛋后面）----------------
    for ear, outer, inner in (
        (EAR_L, C["coat_deep"], C["scarf"]),
        (EAR_R, C["hood"], C["scarf"]),
    ):
        p0, p1 = P(*ear["base"]), P(*ear["tip"])
        r0, r1 = ear["r0"] * BODY_H, ear["r1"] * BODY_H
        cap = tapered_capsule(p0, p1, r0, r1)
        c.poly(cap, fill=outer)
        c.line(cap + [cap[0]], C["outline"], w=1.0, cap=False)

        bx, by = ear["base"]
        tx, ty = ear["tip"]
        q0 = P(bx * 1.00, by - 0.085)
        q1 = P(bx + (tx - bx) * 0.88, ty + 0.060)
        inner_cap = tapered_capsule(q0, q1, r0 * 0.54, r1 * 0.58)
        c.poly(inner_cap, fill=inner)
        c.line(inner_cap + [inner_cap[0]], C["scarf_deep"], w=1.0, cap=False)

    # ---------------- 腿脚（蛋后面）----------------
    for sx in (-1, 1):
        a = P(sx * 0.094, 0.800)
        b = P(sx * 0.094, 0.926)
        leg = tapered_capsule(a, b, 0.072 * BODY_H, 0.066 * BODY_H)
        c.poly(leg, fill=C["hood"])
        c.line(leg + [leg[0]], C["outline"], w=1.0, cap=False)
        c.circle(*P(sx * 0.086, 0.912), 0.026 * BODY_H, fill=C["hood_light"])

    # ---------------- 蛋（兜帽白）----------------
    egg = [P(*p) for p in EGG]
    egg_dense = _catmull_rom_closed(egg, 16)
    c.blob(egg, fill=C["hood"])

    # 兜帽高光 / 暗部（裁进蛋里，只作用在围巾上沿以上）
    hood_zone = [P(*p) for p in [(x, y) for x, y in zip(
        [p[0] for p in EGG], [p[1] for p in EGG])]]
    hood_clip = [
        P(-0.36, 0.10), P(0.36, 0.10), P(0.36, SCARF_TOP), P(-0.36, SCARF_TOP),
    ]
    hood_mask = egg_dense

    def _hood_shade(t):
        t.poly(hood_clip, fill=C["hood"])
        t.clipped(egg_dense, lambda u: u.blob(
            [P(-0.34, 0.36), P(-0.20, 0.212), P(-0.02, 0.170), P(0.10, 0.177),
             P(-0.01, 0.250), P(-0.14, 0.326), P(-0.24, 0.420)],
            fill=C["hood_light"]))
        t.clipped(egg_dense, lambda u: u.blob(
            [P(0.05, 0.48), P(0.28, 0.44), P(0.315, 0.330), P(0.226, 0.300),
             P(0.150, 0.400), P(0.052, 0.440)], fill=C["hood_shade"]))

    c.clipped(hood_mask, _hood_shade)

    # ---------------- 外套（裁进蛋里）----------------
    def _coat(t):
        t.poly([P(-0.40, COAT_TOP), P(0.40, COAT_TOP), P(0.40, 1.02), P(-0.40, 1.02)],
               fill=C["coat"])
        # 右下暗部
        t.clipped(egg_dense, lambda u: u.blob(
            [P(0.04, 0.62), P(0.34, 0.66), P(0.36, 0.80), P(0.20, 0.92),
             P(0.00, 0.94), P(-0.05, 0.76)], fill=C["coat_shade"]))
        # 肩披
        t.clipped(egg_dense, lambda u: u.blob(
            [P(-0.40, 0.600), P(0.40, 0.600), P(0.40, 0.622),
             P(0.20, 0.640), P(0.00, 0.648), P(-0.22, 0.638), P(-0.40, 0.620)],
            fill=C["coat_light"]))
        t.line([P(-0.34, 0.646), P(-0.18, 0.652), P(0.00, 0.656),
                P(0.18, 0.648), P(0.34, 0.634)], C["coat_shade"], w=1.0, smooth=True)

    c.clipped(egg_dense, _coat)

    # 前襟虚线 + 纽扣 + 下摆
    dashed_line(c, [P(0.012, 0.622), P(0.012, COAT_HEM - 0.01)],
                C["coat_deep"], w=1.0, on=2.0, off=2.0)
    for by in (0.664, 0.716, 0.768, 0.820):
        c.circle(*P(-0.026, by), 0.009 * BODY_H, fill=C["coat_deep"])
        c.circle(*P(-0.026, by), 0.004 * BODY_H, fill=C["coat_light"])
    c.clipped(egg_dense, lambda t: t.poly(
        [P(-0.40, COAT_HEM - 0.018), P(0.40, COAT_HEM - 0.018),
         P(0.40, 1.02), P(-0.40, 1.02)], fill=C["coat_deep"]))

    # 袖子（并在外套剪影里）+ X 缝线
    for sx in (-1, 1):
        s0 = P(sx * 0.244, 0.646)
        s1 = P(sx * 0.272, 0.780)
        sleeve = tapered_capsule(s0, s1, 0.048 * BODY_H, 0.042 * BODY_H)
        c.clipped(egg_dense, lambda t, sl=sleeve: (
            t.poly(sl, fill=C["coat"]),
            t.line(sl + [sl[0]], C["coat_shade"], w=1.0, cap=False)))
        cx, cy = P(sx * 0.260, 0.724)
        arm = 0.015 * BODY_H
        c.line([(cx - arm, cy - arm), (cx + arm, cy + arm)], C["hood_light"], w=1.0)
        c.line([(cx - arm, cy + arm), (cx + arm, cy - arm)], C["hood_light"], w=1.0)

    # ---------------- 面罩 ----------------
    visor = [P(*p) for p in VISOR]
    c.blob(visor, fill=C["visor"])
    c.blob(visor, stroke=C["outline"], w=1.0)
    c.clipped(visor, lambda t: t.blob(
        [P(-0.17, 0.286), P(0.17, 0.286), P(0.17, 0.245), P(-0.17, 0.245)],
        fill=C["visor_rim"]))

    for sx in (-1, 1):
        ex, ey = P(sx * EYE["x"], EYE["y"])
        c.ellipse_rot(ex, ey, EYE["rx"] * BODY_H, EYE["ry"] * BODY_H, sx * 8.0,
                      fill=C["eye"])
        c.circle(*P(sx * EYE["x"], EYE["y"] + 0.006), EYE["pupil"] * BODY_H,
                 fill=C["visor"])
        hx, hy = c.to_px(P(sx * EYE["x"] - 0.028, EYE["y"] - 0.020))
        c.px_rect(hx, hy, 2, 2, C["eye_hi"])

    stitch = [P(x * 1.075, y - 0.012) for x, y in VISOR[1:-2]]
    dashed_line(c, stitch, C["hood_deep"], w=1.0, on=2.5, off=3.0)

    # ---------------- 围巾 ----------------
    # 右侧结与飘带（在蛋外面，先画）
    knot = [P(0.280, 0.480), P(0.342, 0.462), P(0.364, 0.496),
            P(0.322, 0.534), P(0.280, 0.530)]
    c.poly(knot, fill=C["scarf"])
    c.poly(knot, stroke=C["outline"], w=1.0)
    tail = [P(0.324, 0.472), P(0.398, 0.418), P(0.430, 0.442), P(0.366, 0.502)]
    c.poly(tail, fill=C["scarf"])
    c.poly(tail, stroke=C["outline"], w=1.0)
    c.poly([P(0.332, 0.474), P(0.390, 0.438), P(0.402, 0.452), P(0.344, 0.488)],
           fill=C["scarf_light"])

    scarf = [P(*p) for p in SCARF]
    scarf_inner = [P(x * 0.99, y) for x, y in SCARF]

    def _scarf(t):
        t.blob(scarf, fill=C["scarf"])
        # 下沿与右侧压暗
        t.clipped(scarf_inner, lambda u: u.blob(
            [P(-0.40, 0.604), P(0.40, 0.586), P(0.40, 0.72), P(-0.40, 0.74)],
            fill=C["scarf_shade"]))
        t.clipped(scarf_inner, lambda u: u.blob(
            [P(0.16, 0.444), P(0.36, 0.454), P(0.36, 0.68), P(0.16, 0.68)],
            fill=C["scarf_shade"]))
        # 上沿高光
        t.clipped(scarf_inner, lambda u: u.blob(
            [P(-0.30, 0.446), P(0.12, 0.452), P(0.12, 0.470), P(-0.30, 0.464)],
            fill=C["scarf_light"]))
        # 描边也裁进蛋里，避免侧面露出悬空的小尾巴
        t.blob(scarf, stroke=C["outline"], w=1.0)

    c.clipped(egg_dense, _scarf)
    # 蛋轮廓压在外套/围巾之上，保证剪影是干净的一圈
    c.line(egg_dense + [egg_dense[0]], C["outline"], w=1.0, cap=False)

    # 耳根圆盘
    for sx in (-1, 1):
        dx, dy = P(sx * 0.132, 0.282)
        c.circle(dx, dy, 0.022 * BODY_H, fill=C["rivet"])
        c.circle(dx, dy, 0.022 * BODY_H, stroke=C["outline"], w=1.0)
        c.circle(dx, dy, 0.009 * BODY_H, fill=C["rivet_dark"])

    # 「01」：左倾 45°、零带斜杠、占围巾左半
    gcx, gcy = -0.095, 0.542
    gh, gw, gap = 0.108, 0.066, 0.028
    ang = math.radians(-30.0)
    ca, sa = math.cos(ang), math.sin(ang)
    sw = 0.0185 * BODY_H * size            # 笔画宽：按最终像素等比，64/128 观感一致

    def G(u, v):
        return P(gcx + u * ca - v * sa, gcy + u * sa + v * ca)

    zero_u = -(gw + gap) * 0.5
    ring = [G(zero_u + gw * 0.5 * math.cos(2 * math.pi * i / 40),
              gh * 0.5 * math.sin(2 * math.pi * i / 40)) for i in range(40)]
    c.line(ring + [ring[0]], C["cream"], w=sw)
    c.line([G(zero_u - gw * 0.36, gh * 0.34), G(zero_u + gw * 0.36, -gh * 0.34)],
           C["cream"], w=sw)

    one_u = (gw + gap) * 0.5
    c.line([G(one_u, -gh * 0.5), G(one_u, gh * 0.5)], C["cream"], w=sw)
    c.line([G(one_u - gw * 0.42, -gh * 0.28), G(one_u, -gh * 0.5)], C["cream"], w=sw)
    c.line([G(one_u - gw * 0.34, gh * 0.5), G(one_u + gw * 0.34, gh * 0.5)],
           C["cream"], w=sw)

    return c


# --------------------------------------------------------------------------- 输出


def render(size: int, out_dir: str, tag: str = "eous_front_idle_01", ss: int = 8) -> Image.Image:
    canvas = draw_eous_front(size, ss)
    img = clean_specks(to_pixels(canvas, PALETTE), 1)
    img.save(os.path.join(out_dir, f"{tag}_{size}.png"))
    return img


def contact_sheet(items, path, cell=232, label_h=22, cols=None):
    n = len(items)
    cols = cols or n
    rows = (n + cols - 1) // cols
    W, H = cell * cols, (cell + label_h) * rows
    sheet = Image.new("RGBA", (W, H), (30, 30, 36, 255))
    d = ImageDraw.Draw(sheet)
    try:
        font = ImageFont.load_default(size=14)
    except TypeError:
        font = ImageFont.load_default()
    for i, (label, img) in enumerate(items):
        r, cx = divmod(i, cols)
        scale = max(1, (cell - 16) // img.width)
        big = img.resize((img.width * scale, img.height * scale), Image.NEAREST)
        ox = cx * cell + (cell - big.width) // 2
        oy = r * (cell + label_h) + (cell - big.height) // 2
        sheet.alpha_composite(big, (ox, oy))
        d.text((cx * cell + 8, r * (cell + label_h) + cell + 3), label,
               fill=(230, 230, 230, 255), font=font)
    sheet.save(path)


if __name__ == "__main__":
    out = os.path.join(os.path.dirname(os.path.abspath(__file__)), "out")
    os.makedirs(out, exist_ok=True)
    sizes = [128, 96, 64, 48, 32]
    imgs, sils = [], []
    for s in sizes:
        im = render(s, out)
        imgs.append((f"{s}px", im))
        sils.append((f"{s} 剪影", silhouette(im)))
        on_checker(im, 3 if s >= 64 else 6).save(os.path.join(out, f"preview_{s}.png"))
    contact_sheet(imgs, os.path.join(out, "_sheet.png"))
    contact_sheet(sils, os.path.join(out, "_sheet_silhouette.png"))
    print("done ->", out)
