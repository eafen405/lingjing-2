#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""【已废弃】早期的 32 色色板生成器，保留作历史参考。

不要运行它。它内置的色值与 `eous_draw.py` 实际使用的 29 色**不一致**
（眼睛 #B4F078 vs #c9f24d、围巾 #E68C50 vs #cf772c），而且按当前工作目录
的相对路径写文件——在 tools/art-pipeline 下跑会生成一层嵌套的
`tools/art-pipeline/Assets/_Project/Art/`，把错色板铺到仓库里。

「全项目 32 色」这条上限已于 2026-10-31 撤销（见 docs/specs/art-assets.md §4）。
现在色板的唯一来源是各绘制模块里的色板常量（`EOUS_COLORS` / `MALICE_EXTRA` /
`TERRAIN_COLORS`），导出结果写进各实体的 manifest 与 `tileset.json`。

确需重跑请显式加 `--force`。
"""

import sys
import io

sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8')

from PIL import Image
import colorsys

def generate_palette():
    """生成符合规格的 32 色色板"""
    palette = []

    # 透明色（索引 0）
    palette.append((0, 0, 0, 0))

    # === 描边与阴影 (索引 1-3) ===
    palette.append((26, 26, 30, 255))      # 深灰描边 L≈15%
    palette.append((45, 45, 52, 255))      # 中阴影
    palette.append((65, 65, 75, 255))      # 浅阴影

    # === 伊埃斯白色身体 (索引 4-7) ===
    palette.append((245, 245, 248, 255))   # 高光白
    palette.append((220, 220, 225, 255))   # 基础白
    palette.append((190, 190, 198, 255))   # 中间调
    palette.append((160, 160, 170, 255))   # 暗部白

    # === 深灰夹克 (索引 8-11) ===
    palette.append((95, 100, 108, 255))    # 高光灰
    palette.append((70, 75, 85, 255))      # 基础灰
    palette.append((50, 55, 65, 255))      # 中间调
    palette.append((35, 40, 50, 255))      # 暗部灰

    # === 黑色耳朵/细节 (索引 12-14) ===
    palette.append((80, 80, 88, 255))      # 高光黑
    palette.append((45, 45, 52, 255))      # 基础黑
    palette.append((28, 28, 32, 255))      # 深黑

    # === 橙色围巾 (索引 15-18) ===
    palette.append((255, 180, 120, 255))   # 高光橙
    palette.append((230, 140, 80, 255))    # 基础橙
    palette.append((200, 110, 55, 255))    # 中间调
    palette.append((160, 85, 40, 255))     # 暗部橙

    # === 黄绿发光色（眼睛/徽章）(索引 19-22) ===
    palette.append((220, 255, 180, 255))   # 高光发光
    palette.append((180, 240, 120, 255))   # 基础发光
    palette.append((140, 200, 80, 255))    # 中间调
    palette.append((100, 160, 50, 255))    # 暗部发光

    # === 以骸黑色主体 (索引 23-25) ===
    palette.append((60, 60, 68, 255))      # 高光黑
    palette.append((35, 35, 42, 255))      # 基础黑
    palette.append((20, 20, 26, 255))      # 深黑

    # === 以太绿色发光 (索引 26-28) ===
    palette.append((150, 255, 200, 255))   # 高光以太绿
    palette.append((80, 220, 150, 255))    # 基础以太绿
    palette.append((40, 180, 110, 255))    # 暗部以太绿

    # === 环境/特效色 (索引 29-31) ===
    palette.append((140, 180, 200, 255))   # 冷色环境光
    palette.append((200, 160, 140, 255))   # 暖色环境光
    palette.append((255, 100, 100, 255))   # 受伤红闪（保留）

    return palette

def save_palette_formats(palette):
    """导出多种格式的色板"""

    import os
    os.makedirs('Assets/_Project/Art', exist_ok=True)

    # 1. PNG 色板条（32x1）
    img = Image.new('RGBA', (32, 1))
    for i, color in enumerate(palette):
        img.putpixel((i, 0), color)
    img.save('Assets/_Project/Art/palette.png')
    print("✓ 已保存 palette.png (32×1)")

    # 2. GPL 格式（GIMP）
    with open('Assets/_Project/Art/palette.gpl', 'w') as f:
        f.write("GIMP Palette\n")
        f.write("Name: Lingjing-2 Pixel Art\n")
        f.write("Columns: 8\n")
        f.write("#\n")
        for i, (r, g, b, a) in enumerate(palette):
            if a == 0:
                f.write(f"{r:3d} {g:3d} {b:3d}\tTransparent\n")
            else:
                f.write(f"{r:3d} {g:3d} {b:3d}\tColor_{i:02d}\n")
    print("✓ 已保存 palette.gpl (GIMP)")

    # 3. TXT 格式（人类可读）
    with open('Assets/_Project/Art/palette.txt', 'w') as f:
        f.write("灵境-2 像素美术色板（32色）\n")
        f.write("=" * 50 + "\n\n")
        categories = [
            (0, 1, "透明"),
            (1, 4, "描边与阴影"),
            (4, 8, "伊埃斯白色身体"),
            (8, 12, "深灰夹克"),
            (12, 15, "黑色耳朵/细节"),
            (15, 19, "橙色围巾"),
            (19, 23, "黄绿发光色"),
            (23, 26, "以骸黑色主体"),
            (26, 29, "以太绿色发光"),
            (29, 32, "环境/特效色")
        ]
        for start, end, name in categories:
            f.write(f"[{name}]\n")
            for i in range(start, end):
                r, g, b, a = palette[i]
                hex_code = f"#{r:02X}{g:02X}{b:02X}" if a > 0 else "Transparent"
                f.write(f"  {i:2d}: {hex_code:12s} RGB({r:3d},{g:3d},{b:3d}) A={a:3d}\n")
            f.write("\n")
    print("✓ 已保存 palette.txt (文档)")

    # 4. Python 数组（供绘制脚本使用）
    with open('Assets/_Project/Art/palette.py', 'w') as f:
        f.write("# 自动生成 - 请勿手动编辑\n")
        f.write("PALETTE = [\n")
        for r, g, b, a in palette:
            f.write(f"    ({r:3d}, {g:3d}, {b:3d}, {a:3d}),\n")
        f.write("]\n\n")
        f.write("# 色板索引常量\n")
        f.write("TRANSPARENT = 0\n")
        f.write("OUTLINE = 1\n")
        f.write("WHITE_BASE = 5\n")
        f.write("GREY_JACKET = 9\n")
        f.write("BLACK_BASE = 13\n")
        f.write("ORANGE_BASE = 16\n")
        f.write("GLOW_GREEN = 20\n")
        f.write("ETHEREAL_GREEN = 27\n")
    print("✓ 已保存 palette.py (代码)")

if __name__ == '__main__':
    if '--force' not in sys.argv:
        print("palette_generator 已废弃：色值与现有素材不一致，不要用它覆盖色板。")
        print("现在色板见 eous_model.EOUS_COLORS / malice_model.MALICE_EXTRA / "
              "terrain_draw.TERRAIN_COLORS。")
        print("确需重跑：python palette_generator.py --force")
        raise SystemExit(1)
    palette = generate_palette()
    save_palette_formats(palette)
    print(f"\n色板生成完成：共 {len(palette)} 色")
