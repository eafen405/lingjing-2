# 语音合成（Fairy / GPT-SoVITS v2）

台词文本的唯一权威是 `docs/specs/audio.md` §2.1 的表格；这个脚本只负责把它变成音频，**不维护第二份文案**。

## 跑

```
D:\Agents\GPT-Sovits\GPT-SoVITS-1007-cu128\runtime\python.exe tools\voice\synth.py
```

- 需要本地有：`D:\Agents\Fairy\Fairy\Fairy\`（GPT/SoVITS 权重 + 唯一一条参考 wav）与 `D:\Agents\GPT-Sovits\GPT-SoVITS-1007-cu128\`（程序 + runtime）。
- `--dry-run` 只解析台词表、打印切段与停顿，不加载模型。
- `--only vo_fairy_low_hp` 单条重跑；多个 id 用逗号分隔。
- `--keep-wav` 保留中间的 32kHz wav。

## 产物

`tools/voice/out/v1/`：10 条 `.ogg`（44.1k mono，−16 LUFS，q5）+ `voice_manifest.json`。
整目录不入库（音色源自第三方权重，见规格 §10），音频可一条命令复现。

## 两个容易踩的点

1. **切段**：按 `，、；。？！：` 切、逐段合成，再用固定静音拼接（逗号 0.40s / 句号 0.55s / 冒号 0.35s / `（停顿）` 0.60s）。模型自己给的句读太短，所以间隔由这里的表决定。
2. **段内拆词**：模型偶尔会把一个词读成两个（`恶名以骸` 中间插 80ms）。脚本会打 `[warn]` 并把警告写进 manifest；处理办法是给该条换 seed，登记到 `SEED_OVERRIDES`，再单条重跑。
