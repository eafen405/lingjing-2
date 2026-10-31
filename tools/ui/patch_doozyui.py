#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
给导入后的 DoozyUI 打本地补丁（幂等，可重复运行）。

背景：DoozyUI v3.1.3 是 2020 年的资产，用到了 Unity 之后删掉的
`ScreenOrientation.Landscape`。编辑器里能编过，但出 Android 包时是 CS0619 硬错误：

    Assets/Doozy/Engine/Orientation/OrientationDetector.cs(191,39):
    error CS0619: 'ScreenOrientation.Landscape' is obsolete

处理方式：把那条多余的 `== ScreenOrientation.Landscape ||` 整个去掉，
保留紧随其后的 LandscapeLeft / LandscapeRight 两个判断，语义不变。

用法（仓库根目录）：
    python tools/ui/patch_doozyui.py
重新导入导出包后再跑一次即可；已经打过的文件不会重复改。
"""

import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
TARGET = os.path.join(ROOT, "Assets", "Doozy", "Engine", "Orientation", "OrientationDetector.cs")

PATTERN = re.compile(r"\(Screen\.orientation == ScreenOrientation\.Landscape \|\|\s*\r?\n\s*")


def main() -> int:
    if not os.path.exists(TARGET):
        print("SKIP   : 没有找到 DoozyUI（先导入 Assets/Doozy）")
        return 0

    with open(TARGET, "r", encoding="utf-8-sig", newline="") as handle:
        text = handle.read()

    patched, count = PATTERN.subn("(", text)
    if count == 0:
        print("OK     : 已经打过补丁，无需改动")
        return 0

    with open(TARGET, "w", encoding="utf-8", newline="") as handle:
        handle.write(patched)

    print(f"PATCHED: 去掉 ScreenOrientation.Landscape 比较（{count} 处）")
    return 0


if __name__ == "__main__":
    sys.exit(main())
