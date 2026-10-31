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

3. **打补丁**
   ```powershell
   python tools/ui/patch_doozyui.py
   ```
   v3.1.3 用了 Unity 之后删掉的 `ScreenOrientation.Landscape`。这个分支在编辑器里不会编译到
   （`#if UNITY_EDITOR`），但**出 Android 包时是 CS0619 硬错误**，打包会直接失败。
   脚本幂等，重新导入资产后重跑即可。

## 与自研 UI 的关系

界面现在有两套并存：

- `Assets/_Project/Scripts/UI/*`：自研的 uGUI 界面（`UiKit` + 五个界面类），已经把整套流程跑通。
- `Assets/Doozy/*`：DoozyUI 本体，目前安装完成、能编译、能打包，但**还没有被界面代码使用**。

迁移时只需要替换界面层（`MainMenuScreen` / `LevelSelectPanel` / `HudView` / `PauseScreen` / `ResultScreen` / `UiBootstrap`），
`UiText`、`GameFlow`、`GameSave` 与游戏逻辑不动。
