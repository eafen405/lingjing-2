#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""artlib —— 分辨率无关的像素美术绘制层。

思路
----
1. 绘图代码只用 0..1 归一化坐标描述造型（见 eous_v2.py），与最终尺寸无关。
2. 光栅化时按 ``size * SS`` 超采样，所以曲线边缘在超采样层面是平滑的。
3. 降采样到 ``size × size`` 网格时，每个目标像素取「覆盖其面积的主导色」
   （占多数的调色板索引），而不是 RGB 平均值。这样不会出现糊掉的中性灰边缘，
   也不需要抗锯齿——符合规格「只允许 0 或 255 的透明度」。
4. 最后把颜色吸附到固定色板（OKLab 最近邻），并做透明孤点清理。

于是同一份绘图代码可以输出 32 / 48 / 64 / 96 任意尺寸，对比后再定案。

单位约定
--------
* 坐标：归一化 0..1，x 向右，y 向下。
* 线宽 / 位移：以「最终像素」为单位（1 = 目标图上 1 个像素），
  因此 1px 描边在任何尺寸下都是 1px。
"""

from __future__ import annotations

import math

import numpy as np
from PIL import Image, ImageDraw

SS_DEFAULT = 8  # 超采样倍数


# --------------------------------------------------------------------------- 颜色


def hex2rgb(s: str) -> tuple[int, int, int]:
    s = s.lstrip("#")
    if len(s) == 3:
        s = "".join(ch * 2 for ch in s)
    return (int(s[0:2], 16), int(s[2:4], 16), int(s[4:6], 16))


def _srgb_to_linear(c):
    c = c / 255.0
    return np.where(c <= 0.04045, c / 12.92, ((c + 0.055) / 1.055) ** 2.4)


def rgb_to_oklab(rgb: np.ndarray) -> np.ndarray:
    r, g, b = (_srgb_to_linear(rgb[..., i]) for i in range(3))
    l = 0.4122214708 * r + 0.5363325363 * g + 0.0514459929 * b
    m = 0.2119034982 * r + 0.6806995451 * g + 0.1073969566 * b
    s = 0.0883024619 * r + 0.2817188376 * g + 0.6299787005 * b
    l_, m_, s_ = np.cbrt(l), np.cbrt(m), np.cbrt(s)
    return np.stack(
        [
            0.2104542553 * l_ + 0.7936177850 * m_ - 0.0040720468 * s_,
            1.9779984951 * l_ - 2.4285922050 * m_ + 0.4505937099 * s_,
            0.0259040371 * l_ + 0.7827717662 * m_ - 0.8086757660 * s_,
        ],
        -1,
    )


class Palette:
    """固定色板 + OKLab 最近邻吸附。"""

    def __init__(self, colors: dict[str, str], transparent_index: int = 0):
        self.names = ["transparent"] + list(colors.keys())
        self.rgb = np.array([[0, 0, 0]] + [hex2rgb(v) for v in colors.values()], np.int16)
        self.lab = rgb_to_oklab(self.rgb.astype(np.float64))
        self.transparent_index = transparent_index

    def __len__(self) -> int:
        return len(self.names)

    def name_index(self, name: str) -> int:
        return self.names.index(name)

    def nearest(self, rgb: np.ndarray) -> np.ndarray:
        """rgb: (...,3) uint8/int -> 调色板索引；透明索引 0 永不命中。

        分块计算 OKLab 距离，避免在 1024² 超采样图上产生 500MB 临时数组。
        """
        shape = rgb.shape[:-1]
        flat = rgb.reshape(-1, 3)
        pal = self.lab[1:]  # 跳过透明
        out = np.empty(flat.shape[0], np.int16)
        chunk = 65536
        for i in range(0, flat.shape[0], chunk):
            lab = rgb_to_oklab(flat[i : i + chunk].astype(np.float64))
            d = ((lab[:, None, :] - pal[None, :, :]) ** 2).sum(-1)
            out[i : i + chunk] = d.argmin(-1) + 1
        return out.reshape(shape)


# --------------------------------------------------------------------------- 几何


def _catmull_rom_closed(points, samples: int = 14):
    """闭合 Catmull-Rom，用于把少量控制点变成光滑外形。"""
    pts = [(float(x), float(y)) for x, y in points]
    n = len(pts)
    dense = []
    for i in range(n):
        p0 = pts[(i - 1) % n]
        p1 = pts[i]
        p2 = pts[(i + 1) % n]
        p3 = pts[(i + 2) % n]
        for j in range(samples):
            t = j / samples
            t2, t3 = t * t, t * t * t
            x = 0.5 * (
                (2 * p1[0])
                + (-p0[0] + p2[0]) * t
                + (2 * p0[0] - 5 * p1[0] + 4 * p2[0] - p3[0]) * t2
                + (-p0[0] + 3 * p1[0] - 3 * p2[0] + p3[0]) * t3
            )
            y = 0.5 * (
                (2 * p1[1])
                + (-p0[1] + p2[1]) * t
                + (2 * p0[1] - 5 * p1[1] + 4 * p2[1] - p3[1]) * t2
                + (-p0[1] + 3 * p1[1] - 3 * p2[1] + p3[1]) * t3
            )
            dense.append((x, y))
    return dense


def _catmull_rom_open(points, samples: int = 14):
    pts = [(float(x), float(y)) for x, y in points]
    ext = [pts[0]] + pts + [pts[-1]]
    dense = []
    for i in range(len(pts) - 1):
        p0, p1, p2, p3 = ext[i : i + 4]
        for j in range(samples):
            t = j / samples
            t2, t3 = t * t, t * t * t
            x = 0.5 * (
                (2 * p1[0])
                + (-p0[0] + p2[0]) * t
                + (2 * p0[0] - 5 * p1[0] + 4 * p2[0] - p3[0]) * t2
                + (-p0[0] + 3 * p1[0] - 3 * p2[0] + p3[0]) * t3
            )
            y = 0.5 * (
                (2 * p1[1])
                + (-p0[1] + p2[1]) * t
                + (2 * p0[1] - 5 * p1[1] + 4 * p2[1] - p3[1]) * t2
                + (-p0[1] + 3 * p1[1] - 3 * p2[1] + p3[1]) * t3
            )
            dense.append((x, y))
    dense.append(pts[-1])
    return dense


def tapered_capsule(p0, p1, r0: float, r1: float, segments: int = 18):
    """两端半径不同的胶囊形（兔耳、四肢、脚掌都用它）。"""
    (x0, y0), (x1, y1) = p0, p1
    dx, dy = x1 - x0, y1 - y0
    length = math.hypot(dx, dy) or 1e-9
    ux, uy = dx / length, dy / length
    nx, ny = -uy, ux
    left, right = [], []
    for i in range(segments + 1):
        t = i / segments
        cx, cy = x0 + dx * t, y0 + dy * t
        r = r0 + (r1 - r0) * t
        left.append((cx + nx * r, cy + ny * r))
        right.append((cx - nx * r, cy - ny * r))
    # 顶端 / 底端半圆
    a0 = math.atan2(ny, nx)
    cap_top = [
        (x1 + r1 * math.cos(a0 - math.pi * k / 12), y1 + r1 * math.sin(a0 - math.pi * k / 12))
        for k in range(1, 12)
    ]
    a1 = math.atan2(-ny, -nx)
    cap_bottom = [
        (x0 + r0 * math.cos(a1 - math.pi * k / 12), y0 + r0 * math.sin(a1 - math.pi * k / 12))
        for k in range(1, 12)
    ]
    return left + cap_top + list(reversed(right)) + cap_bottom


# --------------------------------------------------------------------------- 画布


class Canvas:
    """归一化坐标的画布；所有绘制先落在 w*SS × h*SS 的离屏图上。

    ``Canvas(32)`` 是正方形；``Canvas(32, 48)`` 是 32 宽 48 高的非方形画布
    （坠物这类比宽高的素材用）。归一化坐标 x 按宽、y 按高，互不干扰。
    """

    def __init__(self, width: int, height: int | None = None, ss: int = SS_DEFAULT):
        self.w = int(width)
        self.h = int(height if height is not None else width)
        self.size = self.w          # 兼容旧调用：方形画布的边长
        self.ss = int(ss)
        self.nx = self.w * self.ss
        self.ny = self.h * self.ss
        self.img = Image.new("RGBA", (self.nx, self.ny), (0, 0, 0, 0))
        self._sync()

    # -- 内部 ---------------------------------------------------------------
    def _sync(self):
        self.d = ImageDraw.Draw(self.img)

    def _pt(self, p):
        return (p[0] * self.nx, p[1] * self.ny)

    def _w(self, w_in_px: float) -> float:
        """最终像素宽 -> 离屏像素宽。"""
        return w_in_px * self.ss

    # -- 图元 ---------------------------------------------------------------
    def poly(self, pts, fill=None, stroke=None, w: float = 1.0):
        dpts = [self._pt(p) for p in pts]
        if fill is not None:
            self.d.polygon(dpts, fill=fill)
        if stroke is not None and w > 0:
            self.stroke(pts, stroke, w, closed=True)

    def blob(self, pts, fill=None, stroke=None, w: float = 1.0, samples: int = 14):
        self.poly(_catmull_rom_closed(pts, samples), fill=fill, stroke=stroke, w=w)

    def stroke(self, pts, color, w: float = 1.0, closed: bool = False):
        pts = list(pts)
        if closed:
            pts = pts + [pts[0]]
        self.line(pts, color, w=w, cap=True)

    def line(self, pts, color, w: float = 1.0, smooth: bool = False, samples: int = 12,
             cap: bool = True):
        if smooth and len(pts) > 2:
            pts = _catmull_rom_open(pts, samples)
        dpts = [self._pt(p) for p in pts]
        lw = max(1, int(round(self._w(w))))
        self.d.line(dpts, fill=color, width=lw, joint="curve")
        if cap:
            r = lw / 2.0
            for q in (dpts[0], dpts[-1]):
                self.d.ellipse([q[0] - r, q[1] - r, q[0] + r, q[1] + r], fill=color)

    def ellipse(self, cx, cy, rx, ry, fill=None, stroke=None, w: float = 1.0):
        pts = [
            (
                cx + rx * math.cos(2 * math.pi * i / 48),
                cy + ry * math.sin(2 * math.pi * i / 48),
            )
            for i in range(48)
        ]
        self.poly(pts, fill=fill, stroke=stroke, w=w)

    def circle(self, cx, cy, r, fill=None, stroke=None, w: float = 1.0):
        self.ellipse(cx, cy, r, r, fill=fill, stroke=stroke, w=w)

    def ellipse_rot(self, cx, cy, rx, ry, angle_deg=0.0, fill=None, stroke=None, w: float = 1.0):
        a = math.radians(angle_deg)
        ca, sa = math.cos(a), math.sin(a)
        pts = []
        for i in range(48):
            t = 2 * math.pi * i / 48
            x, y = rx * math.cos(t), ry * math.sin(t)
            pts.append((cx + x * ca - y * sa, cy + x * sa + y * ca))
        self.poly(pts, fill=fill, stroke=stroke, w=w)

    def arc(self, cx, cy, rx, ry, a0, a1, color, w: float = 1.0, n: int = 40):
        pts = []
        for i in range(n + 1):
            a = math.radians(a0 + (a1 - a0) * i / n)
            pts.append((cx + rx * math.cos(a), cy + ry * math.sin(a)))
        self.line(pts, color, w=w, cap=True)

    # -- 像素对齐原语（用于 1px 级细节：缝线、纽扣、字）------------------------
    def px_rect(self, x: int, y: int, wd: int, ht: int, color):
        """按最终像素网格落矩形，保证缩到目标尺寸后仍是整数像素。"""
        s = self.ss
        self.d.rectangle([x * s, y * s, (x + wd) * s - 1, (y + ht) * s - 1], fill=color)

    def px_dot(self, x: int, y: int, color):
        self.px_rect(x, y, 1, 1, color)

    def to_px(self, p) -> tuple[int, int]:
        """归一化坐标 -> 最终像素坐标（取整）。"""
        return (int(round(p[0] * self.w)), int(round(p[1] * self.h)))

    # -- 遮罩裁剪（做投影/明暗时用）-----------------------------------------
    def clipped(self, pts, draw_fn):
        """把 draw_fn(tmp_canvas) 的结果裁进 pts 多边形里再叠到本画布。"""
        tmp = Canvas(self.w, self.h, ss=self.ss)
        draw_fn(tmp)
        mask = Image.new("L", (self.nx, self.ny), 0)
        ImageDraw.Draw(mask).polygon([self._pt(p) for p in pts], fill=255)
        tmp.img.putalpha(Image.composite(tmp.img.getchannel("A"), Image.new("L", mask.size, 0), mask))
        self.img = Image.alpha_composite(self.img, tmp.img)
        self._sync()


# --------------------------------------------------------------------------- 降采样


def to_pixels(canvas: Canvas, palette: Palette) -> Image.Image:
    """超采样图 -> 目标网格：主导色 + 硬度透明度 + 色板吸附 + 孤点清理。"""
    arr = np.asarray(canvas.img)
    rgb = arr[..., :3].astype(np.int16)
    covered = arr[..., 3] >= 128
    idx = palette.nearest(rgb)

    w, h, s = canvas.w, canvas.h, canvas.ss
    idx_b = idx.reshape(h, s, w, s).transpose(0, 2, 1, 3).reshape(h, w, s * s)
    cov_b = covered.reshape(h, s, w, s).transpose(0, 2, 1, 3).reshape(h, w, s * s)

    counts = np.zeros((h, w, len(palette)), np.int16)
    for k in range(1, len(palette)):
        counts[..., k] = ((idx_b == k) & cov_b).sum(-1)
    dominant = counts.argmax(-1)
    coverage = cov_b.mean(-1)

    out = np.zeros((h, w, 4), np.uint8)
    out[..., :3] = palette.rgb[dominant]
    out[..., 3] = np.where(coverage >= 0.5, 255, 0)
    out[out[..., 3] == 0, :3] = 0
    return Image.fromarray(out, "RGBA")


def clean_specks(img: Image.Image, keep_threshold: int = 1) -> Image.Image:
    """去掉四邻域全空的孤立像素（1px 噪点）。"""
    a = np.asarray(img).copy()
    alpha = a[..., 3] > 0
    p = np.pad(alpha, 1)
    neigh = (
        p[:-2, 1:-1].astype(int)
        + p[2:, 1:-1].astype(int)
        + p[1:-1, :-2].astype(int)
        + p[1:-1, 2:].astype(int)
    )
    lonely = alpha & (neigh <= keep_threshold)
    a[lonely] = 0
    return Image.fromarray(a, "RGBA")


# --------------------------------------------------------------------------- 预览


def silhouette(img: Image.Image, color=(0, 0, 0)) -> Image.Image:
    a = np.asarray(img).copy()
    a[..., :3] = color
    return Image.fromarray(a, "RGBA")


def on_checker(img: Image.Image, scale: int, cell: int = 8) -> Image.Image:
    """放大并垫棋盘格背景，便于肉眼看透明边缘。"""
    w, h = img.size
    big = Image.new("RGBA", (w * scale, h * scale))
    d = ImageDraw.Draw(big)
    for y in range(0, h * scale, cell):
        for x in range(0, w * scale, cell):
            c = (58, 58, 64, 255) if ((x // cell) + (y // cell)) % 2 else (44, 44, 50, 255)
            d.rectangle([x, y, x + cell - 1, y + cell - 1], fill=c)
    big.alpha_composite(img.resize((w * scale, h * scale), Image.NEAREST))
    return big
