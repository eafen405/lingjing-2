#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
把合成好的 Fairy 语音装进工程。

为什么不直接把 out/ 整个入库：那份音频是从第三方音色权重推出来的，
规格 §10 要求产物不随仓库分发（见 tools/voice/README.md）。
但运行时要按名字从 Resources 加载，所以这里做一次「装」的动作：
绝音 → Assets/_Project/Resources/Audio/Voice/。

这一份是要进 APK 的，所以它随工程走；out/ 仍然是可复现的中间产物。

用法：python tools/voice/install.py
"""

import os
import shutil
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
SRC_DIR = os.path.join(ROOT, "tools", "voice", "out", "v1")
DST_DIR = os.path.join(ROOT, "Assets", "_Project", "Resources", "Audio", "Voice")


def main():
    if not os.path.isdir(SRC_DIR):
        sys.exit(f"找不到合成产物：{SRC_DIR}（先跑 tools/voice/synth.py）")

    files = sorted(f for f in os.listdir(SRC_DIR) if f.endswith(".ogg"))
    if not files:
        sys.exit(f"{SRC_DIR} 里没有 .ogg")

    os.makedirs(DST_DIR, exist_ok=True)

    # 先清掉旧的，避免改名后残留一份孤儿音频。Unity 的 .meta 一并删。
    for name in os.listdir(DST_DIR):
        if name.endswith(".ogg") or name.endswith(".ogg.meta"):
            os.remove(os.path.join(DST_DIR, name))

    total = 0
    for name in files:
        src = os.path.join(SRC_DIR, name)
        dst = os.path.join(DST_DIR, name)
        shutil.copyfile(src, dst)
        total += os.path.getsize(dst)

    print(f"lines : {len(files)}")
    print(f"output: {os.path.relpath(DST_DIR, ROOT)}  ({total / 1024:.0f} KB)")


if __name__ == "__main__":
    main()
