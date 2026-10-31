#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
导入 DoozyUI / DOTween 之后跑一次，把工程需要的东西补齐（幂等，可重复运行）。

做两件事：

1. 补程序集定义（asmdef）
   DoozyUI v3.1.3 与 DOTween 都没有 asmdef，默认编进 Assembly-CSharp。
   而 asmdef 程序集**不能引用 Assembly-CSharp**，所以 `Lingjing.UI` 里
   `using Doozy.Engine.UI;` 会直接编译失败。这里补三个 asmdef：

     Assets/Doozy/Engine/Doozy.Engine.asmdef      运行时（引用 DOTween.Modules）
     Assets/Doozy/Editor/Doozy.Editor.asmdef      Editor-only（引用上面两个）
     Assets/Plugins/Demigiant/DOTween/Modules/DOTween.Modules.asmdef
                                                  DOAnchorPos/DOFade 等扩展方法在这里

   DOTween.dll 本身仍是 plugin，保持自动引用，不要在它旁边再放同名 asmdef
   （会撞 CS1704「同名程序集已导入」）。

2. 修 v3.1.3 的废弃 API
   `ScreenOrientation.Landscape` 在播放器脚本里是 CS0619 硬错误：
   编辑器能编过（那段在 `#if UNITY_EDITOR` 的 else 分支），出 Android 包才炸。

用法（仓库根目录）：
    python tools/ui/patch_doozyui.py
重新导入资产后再跑一次即可。
"""

import json
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

DOOZY_TARGET = os.path.join(ROOT, "Assets", "Doozy", "Engine", "Orientation", "OrientationDetector.cs")
LEGACY_PATTERN = re.compile(r"\(Screen\.orientation == ScreenOrientation\.Landscape \|\|\s*\r?\n\s*")

ENGINE_ASMDEF = os.path.join(ROOT, "Assets", "Doozy", "Engine", "Doozy.Engine.asmdef")
EDITOR_ASMDEF = os.path.join(ROOT, "Assets", "Doozy", "Editor", "Doozy.Editor.asmdef")
MODULES_ASMDEF = os.path.join(ROOT, "Assets", "Plugins", "Demigiant", "DOTween", "Modules", "DOTween.Modules.asmdef")


def asmdef(name, references, editor_only=False):
    return {
        "name": name,
        "rootNamespace": "",
        "references": references,
        "includePlatforms": ["Editor"] if editor_only else [],
        "excludePlatforms": [],
        "allowUnsafeCode": False,
        "overrideReferences": False,
        "precompiledReferences": [],
        "autoReferenced": True,
        "defineConstraints": [],
        "versionDefines": [],
        "noEngineReferences": False,
    }


def write_if_missing(path, payload, label):
    if os.path.exists(path):
        print(f"OK     : {label} 已存在")
        return
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "w", encoding="utf-8", newline="\n") as handle:
        json.dump(payload, handle, indent=4)
        handle.write("\n")
    print(f"CREATED: {label}")


def patch_legacy_api():
    if not os.path.exists(DOOZY_TARGET):
        print("SKIP   : 没有 Assets/Doozy，跳过废弃 API 补丁")
        return

    with open(DOOZY_TARGET, "r", encoding="utf-8-sig", newline="") as handle:
        text = handle.read()

    patched, count = LEGACY_PATTERN.subn("(", text)
    if count == 0:
        print("OK     : 废弃 API 已打过补丁")
        return

    with open(DOOZY_TARGET, "w", encoding="utf-8", newline="") as handle:
        handle.write(patched)
    print(f"PATCHED: 去掉 ScreenOrientation.Landscape 比较（{count} 处）")


def main() -> int:
    has_doozy = os.path.isdir(os.path.join(ROOT, "Assets", "Doozy"))
    has_dotween = os.path.isdir(os.path.join(ROOT, "Assets", "Plugins", "Demigiant", "DOTween"))

    if not has_doozy and not has_dotween:
        print("SKIP   : 工程里没有 DoozyUI / DOTween")
        return 0

    if has_dotween:
        write_if_missing(MODULES_ASMDEF, asmdef("DOTween.Modules", []), "DOTween.Modules.asmdef")

    if has_doozy:
        write_if_missing(ENGINE_ASMDEF, asmdef("Doozy.Engine", ["DOTween.Modules"]), "Doozy.Engine.asmdef")
        write_if_missing(
            EDITOR_ASMDEF,
            asmdef("Doozy.Editor", ["Doozy.Engine", "DOTween.Modules"], editor_only=True),
            "Doozy.Editor.asmdef",
        )
        patch_legacy_api()

    print("完成。回到 Unity 让它重新编译一次。")
    return 0


if __name__ == "__main__":
    sys.exit(main())
