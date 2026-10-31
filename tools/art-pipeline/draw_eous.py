#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
伊埃斯像素绘制器
32×32 画布，锚点 (16, 4)
"""

import sys
import io
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8')

from PIL import Image

# 内联色板数据
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

TRANSPARENT = 0
OUTLINE = 1

# 色板索引快捷方式
T = TRANSPARENT
O = OUTLINE  # 描边
W1, W2, W3, W4 = 4, 5, 6, 7  # 白色身体（高光到暗部）
G1, G2, G3, G4 = 8, 9, 10, 11  # 灰色夹克
B1, B2, B3 = 12, 13, 14  # 黑色耳朵
OR1, OR2, OR3, OR4 = 15, 16, 17, 18  # 橙色围巾
GL1, GL2, GL3, GL4 = 19, 20, 21, 22  # 黄绿发光

def draw_eous_idle_s_01():
    """
    伊埃斯 idle 状态 S 方向（正面朝下）第 1 帧

    特征：
    - 兔耳头盔：左黑右白，顶部圆弧
    - 黄绿色圆形发光眼睛
    - 橙色围巾横向遮住下半脸
    - 深灰夹克，胸前黄绿圆形徽章
    - Q版短身比例，脚底锚点 (16, 4)
    """

    # 32×32 画布，每个像素用色板索引表示
    # Y轴：0=顶部，31=底部；锚点 (16, 4) 意味着脚在 Y=28 附近
    canvas = [[T for _ in range(32)] for _ in range(32)]

    def set_pixel(x, y, color):
        """安全设置像素"""
        if 0 <= x < 32 and 0 <= y < 32:
            canvas[y][x] = color

    # === 第1层：轮廓与描边 ===

    # 头部轮廓（Y: 6-16，兔耳到下巴）
    # 兔耳（左黑右白）
    for y in range(6, 10):  # 左耳
        set_pixel(10, y, O)
        set_pixel(11, y, B3)
        set_pixel(12, y, O)

    for y in range(6, 10):  # 右耳
        set_pixel(19, y, O)
        set_pixel(20, y, W2)
        set_pixel(21, y, O)

    # 头盔主体（圆形）
    head_outline = [
        (13, 10), (14, 10), (15, 10), (16, 10), (17, 10), (18, 10),  # 顶部
        (12, 11), (19, 11),  # 左右边
        (11, 12), (20, 12),
        (10, 13), (21, 13),
        (10, 14), (21, 14),
        (11, 15), (20, 15),
        (12, 16), (13, 16), (14, 16), (15, 16), (16, 16), (17, 16), (18, 16), (19, 16),  # 底部
    ]
    for x, y in head_outline:
        set_pixel(x, y, O)

    # === 第2层：头部填充 ===

    # 左耳内部（黑色）
    set_pixel(11, 7, B2)
    set_pixel(11, 8, B2)

    # 右耳内部（白色）
    set_pixel(20, 7, W2)
    set_pixel(20, 8, W1)

    # 头盔主体填充（白色，带阴影）
    for y in range(11, 16):
        for x in range(13, 19):
            if canvas[y][x] == T:
                # 左侧光源，右侧较暗
                if x < 16:
                    set_pixel(x, y, W2 if y < 13 else W3)
                else:
                    set_pixel(x, y, W3 if y < 13 else W4)

    # 眼睛（黄绿发光，圆形）
    set_pixel(13, 13, GL2)
    set_pixel(14, 13, GL1)
    set_pixel(18, 13, GL1)
    set_pixel(17, 13, GL2)

    # 橙色围巾（横向遮住下半脸）
    scarf_pixels = [
        (12, 14, OR3), (13, 14, OR2), (14, 14, OR1), (15, 14, OR2),
        (16, 14, OR2), (17, 14, OR1), (18, 14, OR2), (19, 14, OR3),
        (11, 15, OR4), (12, 15, OR3), (13, 15, OR2), (14, 15, OR2),
        (15, 15, OR2), (16, 15, OR2), (17, 15, OR2), (18, 15, OR3), (19, 15, OR4),
    ]
    for x, y, c in scarf_pixels:
        set_pixel(x, y, c)

    # === 第3层：身体 ===

    # 夹克主体轮廓（Y: 17-26）
    body_outline = [
        (13, 17), (14, 17), (15, 17), (16, 17), (17, 17), (18, 17),
        (12, 18), (19, 18),
        (11, 19), (20, 19),
        (11, 20), (20, 20),
        (11, 21), (20, 21),
        (11, 22), (20, 22),
        (12, 23), (19, 23),
        (13, 24), (14, 24), (17, 24), (18, 24),
    ]
    for x, y in body_outline:
        set_pixel(x, y, O)

    # 夹克填充（深灰色）
    for y in range(18, 24):
        for x in range(12, 20):
            if canvas[y][x] == T:
                # 左上光源
                if x < 16 and y < 21:
                    set_pixel(x, y, G2)
                elif x < 16 or y < 21:
                    set_pixel(x, y, G3)
                else:
                    set_pixel(x, y, G4)

    # 胸前发光徽章（黄绿圆形，"1"号）
    set_pixel(15, 20, GL2)
    set_pixel(16, 20, GL1)
    set_pixel(15, 21, GL3)
    set_pixel(16, 21, GL2)

    # 白色身体露出部分（夹克下方）
    for x in range(14, 18):
        set_pixel(x, 24, W3)

    # === 第4层：腿/脚 ===

    # 脚轮廓（简化为两个白色小圆）
    set_pixel(13, 25, O)
    set_pixel(14, 25, W3)
    set_pixel(15, 25, O)

    set_pixel(16, 25, O)
    set_pixel(17, 25, W3)
    set_pixel(18, 25, O)

    # 脚底阴影
    set_pixel(14, 26, 2)  # 中阴影
    set_pixel(17, 26, 2)

    return canvas

def canvas_to_image(canvas):
    """将画布转换为 PIL Image"""
    img = Image.new('RGBA', (32, 32))
    for y in range(32):
        for x in range(32):
            color_index = canvas[y][x]
            img.putpixel((x, y), PALETTE[color_index])
    return img

def save_with_preview(img, filename):
    """保存原图和 8x 放大预览"""
    img.save(filename)
    preview = img.resize((256, 256), Image.NEAREST)
    preview_name = filename.replace('.png', '_preview.png')
    preview.save(preview_name)
    print(f"✓ {filename}")
    print(f"✓ {preview_name} (8x预览)")

if __name__ == '__main__':
    canvas = draw_eous_idle_s_01()
    img = canvas_to_image(canvas)
    save_with_preview(img, 'eous_idle_s_01.png')
