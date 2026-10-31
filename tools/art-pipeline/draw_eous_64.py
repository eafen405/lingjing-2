#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
伊埃斯像素绘制器 - 64×64 版本
画布 64×64，锚点 (32, 8)，PPU 64
所有尺寸从 32×32 版本翻倍
"""

import sys
import io
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8')

from PIL import Image

# 色板数据
PALETTE = [
    (  0,   0,   0,   0),  # 0: 透明
    ( 26,  26,  30, 255),  # 1: 描边
    ( 45,  45,  52, 255),  # 2: 中阴影
    ( 65,  65,  75, 255),  # 3: 浅阴影
    (245, 245, 248, 255),  # 4: 高光白
    (220, 220, 225, 255),  # 5: 基础白
    (190, 190, 198, 255),  # 6: 中间调白
    (160, 160, 170, 255),  # 7: 暗部白
    ( 95, 100, 108, 255),  # 8: 高光灰
    ( 70,  75,  85, 255),  # 9: 基础灰
    ( 50,  55,  65, 255),  # 10: 中间调灰
    ( 35,  40,  50, 255),  # 11: 暗部灰
    ( 80,  80,  88, 255),  # 12: 高光黑
    ( 45,  45,  52, 255),  # 13: 基础黑
    ( 28,  28,  32, 255),  # 14: 深黑
    (255, 180, 120, 255),  # 15: 高光橙
    (230, 140,  80, 255),  # 16: 基础橙
    (200, 110,  55, 255),  # 17: 中间调橙
    (160,  85,  40, 255),  # 18: 暗部橙
    (220, 255, 180, 255),  # 19: 高光发光
    (180, 240, 120, 255),  # 20: 基础发光
    (140, 200,  80, 255),  # 21: 中间调发光
    (100, 160,  50, 255),  # 22: 暗部发光
    ( 60,  60,  68, 255),  # 23: 以骸高光黑
    ( 35,  35,  42, 255),  # 24: 以骸基础黑
    ( 20,  20,  26, 255),  # 25: 以骸深黑
    (150, 255, 200, 255),  # 26: 高光以太绿
    ( 80, 220, 150, 255),  # 27: 基础以太绿
    ( 40, 180, 110, 255),  # 28: 暗部以太绿
    (140, 180, 200, 255),  # 29: 冷色环境光
    (200, 160, 140, 255),  # 30: 暖色环境光
    (255, 100, 100, 255),  # 31: 受伤红闪
]

T = 0   # 透明
O = 1   # 描边
W1, W2, W3, W4 = 4, 5, 6, 7      # 白色
G1, G2, G3, G4 = 8, 9, 10, 11    # 灰色
B1, B2, B3 = 12, 13, 14          # 黑色
OR1, OR2, OR3, OR4 = 15, 16, 17, 18  # 橙色
GL1, GL2, GL3, GL4 = 19, 20, 21, 22  # 黄绿发光

def draw_eous_idle_s_01():
    """
    伊埃斯 64×64 idle S 方向第 1 帧

    特征：
    - 兔耳头盔（左黑右白）
    - 黄绿色大眼睛
    - 橙色围巾
    - 深灰夹克，胸前发光徽章
    - 锚点 (32, 8) = 脚底 Y≈56
    """

    canvas = [[T for _ in range(64)] for _ in range(64)]

    def set_pixel(x, y, color):
        if 0 <= x < 64 and 0 <= y < 64:
            canvas[y][x] = color

    def fill_rect(x1, y1, x2, y2, color):
        """填充矩形区域"""
        for y in range(y1, y2 + 1):
            for x in range(x1, x2 + 1):
                set_pixel(x, y, color)

    # === 兔耳（左黑右白） ===

    # 左耳（黑色）Y: 12-20
    # 外轮廓
    for y in range(12, 21):
        set_pixel(19, y, O)
        set_pixel(24, y, O)
    for x in range(19, 25):
        set_pixel(x, 12, O)
        set_pixel(x, 20, O)

    # 左耳填充
    fill_rect(20, 13, 23, 19, B2)
    # 高光
    set_pixel(20, 14, B1)
    set_pixel(21, 14, B1)

    # 右耳（白色）
    for y in range(12, 21):
        set_pixel(39, y, O)
        set_pixel(44, y, O)
    for x in range(39, 45):
        set_pixel(x, 12, O)
        set_pixel(x, 20, O)

    # 右耳填充
    fill_rect(40, 13, 43, 19, W2)
    # 高光
    fill_rect(40, 14, 42, 15, W1)

    # === 头盔主体（圆形，黑白分割） ===

    # 顶部弧线 Y: 21-22
    for x in range(25, 39):
        set_pixel(x, 21, O)

    # 左右边缘 Y: 22-32
    for y in range(22, 33):
        set_pixel(20, y, O)
        set_pixel(43, y, O)

    # 底部弧线
    for x in range(21, 43):
        set_pixel(x, 33, O)

    # === 头盔填充（左黑右白） ===

    # 左半（黑色）X: 21-31
    for y in range(22, 33):
        for x in range(21, 32):
            if canvas[y][x] == T:
                if x < 24 and y < 27:
                    set_pixel(x, y, B1)  # 高光
                else:
                    set_pixel(x, y, B2)

    # 右半（白色）X: 32-42
    for y in range(22, 33):
        for x in range(32, 43):
            if canvas[y][x] == T:
                if x > 39 or y > 29:
                    set_pixel(x, y, W3)  # 暗部
                else:
                    set_pixel(x, y, W2)

    # === 眼睛（黄绿发光，椭圆形，更大更明显） ===

    # 左眼 中心 (26, 27)
    left_eye = [
        (25, 26), (26, 26), (27, 26),
        (24, 27), (25, 27), (26, 27), (27, 27), (28, 27),
        (24, 28), (25, 28), (26, 28), (27, 28), (28, 28),
        (25, 29), (26, 29), (27, 29),
    ]
    for x, y in left_eye:
        if x in [26, 27] and y in [27, 28]:
            set_pixel(x, y, GL1)  # 高光核心
        else:
            set_pixel(x, y, GL2)

    # 右眼 中心 (37, 27)
    right_eye = [
        (36, 26), (37, 26), (38, 26),
        (35, 27), (36, 27), (37, 27), (38, 27), (39, 27),
        (35, 28), (36, 28), (37, 28), (38, 28), (39, 28),
        (36, 29), (37, 29), (38, 29),
    ]
    for x, y in right_eye:
        if x in [36, 37] and y in [27, 28]:
            set_pixel(x, y, GL1)
        else:
            set_pixel(x, y, GL2)

    # === 橙色围巾（横向遮住下半脸） ===

    # 围巾主体 Y: 30-33
    for x in range(22, 42):
        set_pixel(x, 30, OR2)
        set_pixel(x, 31, OR2)

    # 高光
    for x in range(26, 38):
        set_pixel(x, 30, OR1)

    # 暗部
    for x in range(22, 26):
        set_pixel(x, 31, OR3)
    for x in range(38, 42):
        set_pixel(x, 31, OR3)

    # 围巾下沿
    for x in range(21, 43):
        set_pixel(x, 32, OR3)
    for x in range(23, 41):
        set_pixel(x, 33, OR4)

    # === 身体（深灰夹克 + 白色下身） ===

    # 夹克轮廓 Y: 34-48
    for x in range(25, 39):
        set_pixel(x, 34, O)

    for y in range(35, 48):
        set_pixel(22, y, O)
        set_pixel(41, y, O)

    # 夹克填充
    for y in range(35, 48):
        for x in range(23, 41):
            if canvas[y][x] == T:
                # 左上光源
                if x < 32 and y < 42:
                    set_pixel(x, y, G2)  # 亮部
                elif x < 32 or y < 42:
                    set_pixel(x, y, G3)
                else:
                    set_pixel(x, y, G4)  # 暗部

    # === 胸前发光徽章（黄绿圆形） ===

    badge_center = (31, 41)
    badge_pixels = [
        (30, 39), (31, 39), (32, 39),
        (29, 40), (30, 40), (31, 40), (32, 40), (33, 40),
        (29, 41), (30, 41), (31, 41), (32, 41), (33, 41),
        (29, 42), (30, 42), (31, 42), (32, 42), (33, 42),
        (30, 43), (31, 43), (32, 43),
    ]
    for x, y in badge_pixels:
        dist = abs(x - 31) + abs(y - 41)
        if dist <= 1:
            set_pixel(x, y, GL1)  # 核心高光
        elif dist <= 2:
            set_pixel(x, y, GL2)
        else:
            set_pixel(x, y, GL3)

    # === 白色下身露出 ===

    for x in range(26, 38):
        set_pixel(x, 48, O)

    fill_rect(27, 49, 36, 50, W3)

    # === 腿/脚 ===

    # 左脚
    for x in range(26, 30):
        set_pixel(x, 51, O)
    for y in range(52, 54):
        set_pixel(25, y, O)
        set_pixel(30, y, O)
    for x in range(26, 30):
        set_pixel(x, 54, O)

    fill_rect(26, 52, 29, 53, W3)

    # 右脚
    for x in range(34, 38):
        set_pixel(x, 51, O)
    for y in range(52, 54):
        set_pixel(33, y, O)
        set_pixel(38, y, O)
    for x in range(34, 38):
        set_pixel(x, 54, O)

    fill_rect(34, 52, 37, 53, W3)

    # 脚底阴影
    for x in range(26, 30):
        set_pixel(x, 55, 2)
    for x in range(34, 38):
        set_pixel(x, 55, 2)

    return canvas

def canvas_to_image(canvas):
    """将画布转换为 PIL Image"""
    size = len(canvas)
    img = Image.new('RGBA', (size, size))
    for y in range(size):
        for x in range(size):
            color_index = canvas[y][x]
            img.putpixel((x, y), PALETTE[color_index])
    return img

def save_with_preview(img, filename, scale=4):
    """保存原图和放大预览"""
    img.save(filename)
    size = img.size[0]
    preview = img.resize((size * scale, size * scale), Image.NEAREST)
    preview_name = filename.replace('.png', '_preview.png')
    preview.save(preview_name)
    print(f"✓ {filename}")
    print(f"✓ {preview_name} ({scale}x预览)")

if __name__ == '__main__':
    canvas = draw_eous_idle_s_01()
    img = canvas_to_image(canvas)
    save_with_preview(img, 'eous_idle_s_01_64x64.png', scale=4)
    print(f"\n画布尺寸：64×64")
    print(f"锚点位置：(32, 8) = 脚底中心")
