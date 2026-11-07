# 第三方资产：DoozyUI 与 DOTween

考核点名要用 **DoozyUI** 做多层界面（主菜单、关卡选择等），DoozyUI 又硬依赖 **DOTween**。
两者都不是本项目自己写的，处理方式记录在这里，避免下次换机时重新踩一遍。

## 现状

| 资产 | 版本 | 安装位置 | 进 Git 镜像 | 进 Plastic |
| --- | --- | --- | --- | --- |
| DoozyUI | v3.1.3 | `Assets/Doozy/{Engine,Editor,Integrations,Logos}` | 否 | 是 |
| DOTween | 1.2.790 | `Assets/Plugins/Demigiant/DOTween` | 否 | 是 |
| Doozy 图标 | 随包 | `Assets/Gizmos/Doozy` | 否 | 是 |

- **只进 Plastic 不进 GitHub**：DoozyUI 是付费资产，公开镜像里不放它的源码；`.gitignore` 已排除上述三处及其目录 `.meta`。Plastic 是考核对象，那边保留完整工程。
- DoozyUI 的 `Examples/` 已删除（3054 个文件、53 MB 的演示场景与素材，用不到）。剩下 `Engine` 4.7 MB + `Editor` 12.1 MB。

## 安装步骤（换机器时照这个来）

1. **DOTween（先装）**
   ```
   https://dotween.demigiant.com/downloads/DOTween_1_2_790.zip
   解压后把 DOTween/ 整个复制到 Assets/Plugins/Demigiant/DOTween
   ```
   注意：DOTween 自带 `Modules/DOTweenModulePhysics.cs`，依赖 `UnityEngine.PhysicsModule`，
   所以 `Packages/manifest.json` 里必须有 `com.unity.modules.physics`（本项目已加）。

2. **DoozyUI**
   ```
   source/DoozyUI Complete UI Management System v3.1.3/*.unitypackage
   ```
   这个包本身是**安装器**，真正的资产在它内部的
   `Assets/DoozyInstaller/Editor/Data/Data.unitypackage`。直接把内层包喂给 Unity 更省事：
   ```powershell
   Unity.exe -batchmode -quit -projectPath . -importPackage "<内层包路径>"
   Unity.exe -batchmode -quit -projectPath . -importPackage "<内层包路径>"   # 第二次让 DOTWEEN 宏生效
   ```
   导入后 Unity 会自动给所有平台加 `DOTWEEN` 脚本宏（写在 `ProjectSettings.asset` 的 `scriptingDefineSymbols`）。

3. **打补丁 + 补程序集定义**
   ```powershell
   python tools/ui/patch_doozyui.py
   ```
   脚本做两件事，幂等，重新导入资产后重跑即可：

   - **补 asmdef**：两个包原本都没有程序集定义，默认编进 `Assembly-CSharp`；而 asmdef 程序集
     **不能引用 Assembly-CSharp**，所以 `Lingjing.UI` 里 `using Doozy.Engine.UI;` 会直接编译失败。
     脚本会补三个：
     `Assets/Doozy/Engine/Doozy.Engine.asmdef`、
     `Assets/Doozy/Editor/Doozy.Editor.asmdef`（Editor-only）、
     `Assets/Plugins/Demigiant/DOTween/Modules/DOTween.Modules.asmdef`（`DOAnchorPos`/`DOFade` 这些扩展方法在这里）。
     `DOTween.dll` 仍是 plugin，**不要在它旁边放同名 asmdef**，否则报 CS1704「同名程序集已导入」。

   - **修废弃 API**：v3.1.3 用了 Unity 之后删掉的 `ScreenOrientation.Landscape`。这个分支在编辑器里
     不会编译到（`#if UNITY_EDITOR`），但**出 Android 包时是 CS0619 硬错误**，打包会直接失败。

## 与自研 UI 的关系

界面现在是「DoozyUI 管显隐 + 自研 UiKit 管布局」的混合：

- `Assets/_Project/Scripts/UI/*`：`UiKit` 负责在运行时长出所有控件（960×540 参考分辨率），
  `DoozyViewHost` 把每个界面根节点包成 Doozy 的 `UIView`，显隐全走 `Show()/Hide()`。
- 六个视图名固定为 `MainMenu` / `LevelSelect` / `Hud` / `Pause` / `Choice` / `Result`，
  层级靠嵌套 Canvas 的 `overrideSorting + sortingOrder`（10 / 20 / 20 / 100 / 105 / 110）压住，
  HUD 常驻，暂停、三选一与结算依次叠在上面。
- 没有用 Nody 流程图，也没有在编辑器里拖 Prefab：界面依旧全部由代码生成，这样才能继续无头构建与探针验证。

> 2026-11-04 校正：本段此前写「五个视图名 / 10-20-20-100-110」，漏了三选一视图 `Choice`（105）；以 `Assets/_Project/Scripts/UI/DoozyViewHost.cs` 的常量为准。

Doozy 的运行时默认值和编辑器默认值不一致（`ViewCategory` 为 null、三个 `Disable*WhenHidden` 全 false、
`ShowBehavior.InstantAnimation` 关闭时 `Show()` 会静默 return），这些坑都封在 `DoozyViewHost.Attach` 里，
改界面时不要绕过它直接 `AddComponent<UIView>()`。

