#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
把系统里的 Noto Sans SC 变量字体裁成 UI 用的小字体。

为什么要裁：完整 Noto Sans SC 变量字体 17.7 MB，而 UI 只会用到几百个字；
直接塞进 APK 不划算，而且每次加新文案都要重新导入。

取字范围（三处并集）：
1. docs/ 下所有 markdown 里出现过的汉字 —— UI 文案用的就是这套词汇；
2. 脚本里写死的 UI 字表；
3. ASCII 可打印字符 + CJK 标点 + 全角符号。

产物：Assets/_Project/Resources/Fonts/NotoSansSC-UI.ttf（OFL 授权，随仓库分发）

用法（仓库根目录）：
    python tools/ui/subset_font.py
    python tools/ui/subset_font.py --source "C:/Windows/Fonts/NotoSansSC-VF.ttf"
"""

import argparse
import os
import sys

from fontTools import subset
from fontTools.ttLib import TTFont

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT_DIR = os.path.join(ROOT, "Assets", "_Project", "Resources", "Fonts")
OUT_FILE = os.path.join(OUT_DIR, "NotoSansSC-UI.ttf")

DEFAULT_SOURCES = [
    r"C:\Windows\Fonts\NotoSansSC-VF.ttf",
    r"C:\Windows\Fonts\NotoSansCJKsc-Regular.otf",
]

# 脚本里写死的 UI 字表：菜单、HUD、结算、暂停、关卡名。
UI_TEXT = (
    "灵境空洞伊埃斯邦布飞刀以骸恶名连环骤雨"
    "开始游戏关卡选择继续重新返回退出设置暂停"
    "第一关第二关未解锁已通关最高分得分生命充能"
    "命中击杀代行接管中剩余秒天赋升级选择确认取消"
    "通关失败胜利死亡主菜单重新开始退出游戏"
    "操作提示移动摇杆技能键正反切"
)

ASCII = "".join(chr(c) for c in range(0x20, 0x7F))
PUNCT = "，。、；：？！“”‘’（）《》〈〉—…·〔〕【】￥％＋－×÷＝／＼｜～"


def collect_doc_chars() -> set[str]:
    chars: set[str] = set()
    docs = os.path.join(ROOT, "docs")
    for base, _, files in os.walk(docs):
        for name in files:
            if not name.endswith(".md"):
                continue
            path = os.path.join(base, name)
            try:
                with open(path, "r", encoding="utf-8") as handle:
                    for line in handle:
                        chars.update(line)
            except OSError:
                continue
    return {c for c in chars if c.strip()}


def pick_source(explicit: str | None) -> str:
    if explicit:
        if not os.path.exists(explicit):
            sys.exit(f"找不到字体源文件：{explicit}")
        return explicit

    for candidate in DEFAULT_SOURCES:
        if os.path.exists(candidate):
            return candidate

    sys.exit("找不到 Noto Sans SC：请用 --source 指定路径（可从 https://fonts.google.com/noto 下载）")


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--source", default=None, help="字体源文件路径")
    args = parser.parse_args()

    source = pick_source(args.source)
    charset = set(UI_TEXT) | set(ASCII) | set(PUNCT) | collect_doc_chars()
    text = "".join(sorted(charset))

    os.makedirs(OUT_DIR, exist_ok=True)

    # 变量字体先实例化到单一字重，否则子集里会带着整条轴。
    instance = TTFont(source)
    if "fvar" in instance:
        from fontTools.varLib.instancer import instantiateVariableFont

        instance = instantiateVariableFont(instance, {"wght": 500}, inplace=False)

    options = subset.Options()
    options.layout_features = ["*"]
    options.name_IDs = ["*"]
    options.notdef_outline = True
    options.recalc_bounds = True
    options.drop_tables += ["DSIG"]

    subsetter = subset.Subsetter(options=options)
    subsetter.populate(text=text)
    subsetter.subset(instance)
    instance.save(OUT_FILE)

    size_kb = os.path.getsize(OUT_FILE) / 1024
    print(f"source   : {source}")
    print(f"chars    : {len(set(text))}")
    print(f"output   : {os.path.relpath(OUT_FILE, ROOT)}  ({size_kb:.0f} KB)")


if __name__ == "__main__":
    main()
