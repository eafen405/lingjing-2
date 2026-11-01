# Fairy 层的视觉语言（取自 OurFairy）

本文件是**参考摘录**，不是规格。规格是 `docs/design/game-design.md` §10：世界层像素、Fairy 层非像素，反差刻意。

来源：`\\wsl.localhost\Ubuntu\home\eafen405\projects\OurFairy`（同一作者的另一项目）。
它是 Android Jetpack Compose（Material3）应用，**没有 CSS**，样式与字体常量都在 `android/app/src/main/java/me/rerere/rikkahub/ui/fairy/` 下的 Kotlin 里。

**它不是像素素材库。** 全仓没有一张像素图，风格是 CRT 工业 HUD。取的是它的语言，不是它的画面。

## 取色（`FairyTokens.kt:22-59`）

| 用途 | 色值 |
| --- | --- |
| 信号青（主操作、焦点、在线） | `#72D7FF` |
| 主按钮文字 / 底色 | `#EAF0FF` on `#0A1633` |
| Fairy 电光蓝 / 辉光 / 链接 | `#1F55E0` / `#3D8BFF` / `#8FB8FF` |
| 背景渐变 | `#08111F → #030711` |
| 面板 | `#0A1426`（96%） |
| 玻璃 / 胶囊 / 屏幕 | `#142238→#091321` / `#101C2E` / `#0D2257→#050B1F` |
| 文字 / 弱化文字 | `#EAF3FF` / `#A9B8D8` |
| 成功 / 危险 | `#5BE49B` / `#FF5A4E` |
| 描边 | 白 10%；气泡白 12% + 描边白 20% |

## 形状与排版

- 圆角：面板 6、控件 4、屏幕 6；气泡 6/2/6/6；控制台是 **切角**（chamfer `cut = 10dp`，可直接做成九宫格切角面板）。
- 描边统一 **1dp 发丝线**，没有 2px 以上的粗边。
- 层次靠亮度和 1dp 线，不靠阴影。

## 字体（`android/app/src/main/res/font/`）

| 文件 | 实读家族 |
| --- | --- |
| `display_industrial.ttf` | 阿里妈妈数黑体 Bold（name 表被改名） |
| `title_heavy.ttf` | MiSans Heavy |
| `jetbrains_mono.ttf` | JetBrains Mono Regular |
| `label_black.ttf` | Outfit Thin（文件名与字重不符） |

中文界面里：主标题用数黑体，需要更重的地方用 MiSans Heavy，数字（分数、等级、节号）用 JetBrains Mono，正文继续用 Noto Sans SC 子集。

## Fairy 徽记

角色本体没有位图，是 Compose Canvas 画的；但成品图标有：
`android/app/src/main/res/mipmap-xxxhdpi/ic_launcher_foreground.png`（432×432，透明底）。
这是她唯一可靠的「长相」，已复制成工程内的原图 `tools/ui/source/fairy_emblem.png`；
`python tools/ui/make_emblem.py` 按 alpha 包围盒裁掉自适应图标的安全边距，输出
`Assets/_Project/Resources/Fairy/fairy_emblem.png`（512×512）。

**不要照 SVG 里的圆参数手画。** 那四个圆只是标记的一部分——真正的徽记是
白色外环、蓝渐变环、**带四枚尖齿的白环**，以及偏在右下、白底蓝点的瞳。
只按同心圆重画会丢掉尖齿和偏瞳，图形就完全不对了。

## 工程里的落点

- 取色板：`Assets/_Project/Scripts/UI/UiPalette.cs`，界面里不再直接写 `new Color(...)`。
- 标题字体：`Assets/_Project/Resources/Fonts/Lingjing-Title.ttf`（就是上面那份数黑体），
  经 `UiKit.TitleFont` 取用；正文仍走 Noto 子集。
- 徽记：`python tools/ui/make_emblem.py` 从 `tools/ui/source/fairy_emblem.png` 裁出
  `Assets/_Project/Resources/Fairy/fairy_emblem.png`，由 `UiKit.Emblem` 现搓 Sprite，
  不依赖导入设置。HUD 里只在代行期间显示。

## CRT 质感

参数：24dp 网格 @3%、1px 扫描线 @1.2%，加暗角与泛光。

**只用在 Fairy 气泡/面板内部**，不要铺满整个屏幕——整屏扫描线会压掉像素世界的可读性，反而把两层揉成一坨。
