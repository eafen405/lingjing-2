#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
把 OurFairy 的 Fairy 徽记裁成工程里用的图。

来源：OurFairy 仓库 android/app/src/main/res/mipmap-xxxhdpi/ic_launcher_foreground.png（432×432）。
那份是自适应图标的前景层，四周留了安全边距，所以这里按 alpha 包围盒裁掉空白再补一点边距。

上一版是照着 SVG 里的四个同心圆重画的，画错了——真正的徽记是
「白环 + 蓝环 + 带四枚尖齿的白环 + 偏右下的瞳（白底蓝点）」。
徽记是 Fairy 层唯一的实体图形（game-design §10），不要手画，用原图。

产物：Assets/_Project/Resources/Fairy/fairy_emblem.png（512×512，透明底）

用法：python tools/ui/make_emblem.py
      python tools/ui/make_emblem.py --source <另一份 png>
"""

import argparse
import os
import sys

from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
SOURCE = os.path.join(ROOT, "tools", "ui", "source", "fairy_emblem.png")
OUT_DIR = os.path.join(ROOT, "Assets", "_Project", "Resources", "Fairy")
OUT_FILE = os.path.join(OUT_DIR, "fairy_emblem.png")

OUT_SIZE = 512
MARGIN = 0.04


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--source", default=SOURCE, help="徽记原图路径")
    args = parser.parse_args()

    if not os.path.exists(args.source):
        sys.exit(f"找不到徽记原图：{args.source}")

    image = Image.open(args.source).convert("RGBA")
    bbox = image.getbbox()
    if bbox is None:
        sys.exit("原图是全透明的")

    cropped = image.crop(bbox)

    # 补一圈透明边距，避免图标贴着控件边缘。
    side = round(max(cropped.size) * (1.0 + MARGIN * 2))
    canvas = Image.new("RGBA", (side, side), (0, 0, 0, 0))
    canvas.paste(cropped, ((side - cropped.width) // 2, (side - cropped.height) // 2), cropped)

    canvas = canvas.resize((OUT_SIZE, OUT_SIZE), Image.LANCZOS)

    os.makedirs(OUT_DIR, exist_ok=True)
    canvas.save(OUT_FILE)
    print(f"source: {os.path.relpath(args.source, ROOT)}  bbox={bbox}")
    print(f"output: {os.path.relpath(OUT_FILE, ROOT)}  ({os.path.getsize(OUT_FILE) / 1024:.1f} KB)")


if __name__ == "__main__":
    main()
