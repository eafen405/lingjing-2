#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
合成占位音效。**这些不是最终素材**——是为了先把反馈闭环起来，
真素材到位后只替换 Resources/Audio/Sfx 下的同名文件，代码与事件点都不用动。

规矩：短（≤0.6s）、干净、不刺耳；每个音只表达一件事。一共 14 条，对应 game-design §11 的事件点。

产物：Assets/_Project/Resources/Audio/Sfx/*.wav（44.1k mono 16-bit）

用法：python tools/audio/make_sfx.py
"""

import math
import os
import wave

import numpy as np

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT_DIR = os.path.join(ROOT, "Assets", "_Project", "Resources", "Audio", "Sfx")

RATE = 44100


def envelope(n, attack=0.005, release=0.6):
    """线性起音 + 指数衰减，避免爆音。"""
    t = np.linspace(0.0, 1.0, n, endpoint=False)
    att = np.clip(t / max(attack, 1e-6), 0.0, 1.0)
    rel = np.exp(-t / max(release, 1e-6))
    return att * rel


def tone(freq, seconds, shape="sine", decay=0.35, gain=0.5):
    n = int(RATE * seconds)
    t = np.arange(n) / RATE
    phase = 2.0 * math.pi * freq * t
    if shape == "square":
        data = np.sign(np.sin(phase))
    elif shape == "tri":
        data = 2.0 / math.pi * np.arcsin(np.sin(phase))
    else:
        data = np.sin(phase)
    return data * envelope(n, release=decay) * gain


def sweep(f0, f1, seconds, decay=0.4, gain=0.5, noise=0.0):
    n = int(RATE * seconds)
    t = np.arange(n) / RATE
    freq = f0 + (f1 - f0) * (t / max(seconds, 1e-6))
    phase = 2.0 * math.pi * np.cumsum(freq) / RATE
    data = np.sin(phase)
    if noise > 0.0:
        data = data * (1.0 - noise) + np.random.default_rng(7).normal(0.0, 1.0, n) * noise
    return data * envelope(n, release=decay) * gain


def noise_burst(seconds, decay=0.2, gain=0.4, lowpass=0.0):
    n = int(RATE * seconds)
    data = np.random.default_rng(11).normal(0.0, 1.0, n)
    if lowpass > 0.0:
        out = np.empty_like(data)
        acc = 0.0
        for i in range(n):
            acc += lowpass * (data[i] - acc)
            out[i] = acc
        data = out
    return data * envelope(n, release=decay) * gain


def silence(seconds):
    return np.zeros(int(RATE * seconds))


def at(seconds, part):
    """把一段音往后推 seconds 秒。"""
    return np.concatenate([silence(seconds), part])


def mix(*parts):
    length = max(len(p) for p in parts)
    out = np.zeros(length)
    for part in parts:
        out[: len(part)] += part
    return out


def save(name, data):
    peak = float(np.max(np.abs(data))) if len(data) else 0.0
    if peak > 0.99:
        data = data * (0.99 / peak)

    pcm = (np.clip(data, -1.0, 1.0) * 32767.0).astype("<i2")

    os.makedirs(OUT_DIR, exist_ok=True)
    path = os.path.join(OUT_DIR, name + ".wav")
    with wave.open(path, "wb") as handle:
        handle.setnchannels(1)
        handle.setsampwidth(2)
        handle.setframerate(RATE)
        handle.writeframes(pcm.tobytes())
    return os.path.getsize(path)


def main():
    cues = {
        # 战斗反馈
        "sfx_hit": mix(tone(1180, 0.05, "tri", decay=0.06, gain=0.45),
                       tone(1770, 0.03, "sine", decay=0.04, gain=0.20)),
        "sfx_kill": mix(sweep(880, 260, 0.12, decay=0.22, gain=0.50),
                        noise_burst(0.09, decay=0.12, gain=0.25, lowpass=0.5)),
        "sfx_hurt": mix(tone(120, 0.14, "sine", decay=0.18, gain=0.60),
                        noise_burst(0.07, decay=0.10, gain=0.20, lowpass=0.25)),

        # 拾取
        "sfx_pickup_health": mix(tone(660, 0.07, "sine", decay=0.10, gain=0.35),
                                 at(0.05, tone(990, 0.10, "sine", decay=0.14, gain=0.35))),
        "sfx_pickup_energy": mix(tone(880, 0.06, "tri", decay=0.09, gain=0.30),
                                 at(0.04, tone(1180, 0.06, "tri", decay=0.09, gain=0.30)),
                                 at(0.08, tone(1480, 0.09, "tri", decay=0.12, gain=0.30))),

        # 代行
        "sfx_fairy_start": mix(sweep(320, 1240, 0.34, decay=0.60, gain=0.45),
                               noise_burst(0.34, decay=0.50, gain=0.10, lowpass=0.08)),
        "sfx_fairy_end": sweep(1100, 300, 0.30, decay=0.50, gain=0.40),

        # 成长
        "sfx_level_up": mix(tone(660, 0.09, "tri", decay=0.12, gain=0.32),
                            at(0.07, tone(880, 0.09, "tri", decay=0.12, gain=0.32)),
                            at(0.14, tone(1320, 0.16, "tri", decay=0.20, gain=0.32))),
        "sfx_choose": tone(1046, 0.08, "sine", decay=0.10, gain=0.40),

        # 界面
        "sfx_ui_click": tone(2100, 0.025, "square", decay=0.03, gain=0.18),

        # 关卡
        "sfx_section_clear": mix(tone(523, 0.10, "sine", decay=0.14, gain=0.32),
                                 at(0.08, tone(784, 0.18, "sine", decay=0.22, gain=0.32))),
        "sfx_boss_intro": mix(tone(82, 0.55, "sine", decay=0.70, gain=0.60),
                              sweep(160, 420, 0.50, decay=0.60, gain=0.25)),
        "sfx_death": mix(sweep(420, 62, 0.62, decay=0.80, gain=0.55),
                         noise_burst(0.20, decay=0.25, gain=0.15, lowpass=0.12)),
        "sfx_win": mix(tone(523, 0.12, "tri", decay=0.16, gain=0.30),
                       at(0.10, tone(659, 0.12, "tri", decay=0.16, gain=0.30)),
                       at(0.20, tone(784, 0.12, "tri", decay=0.16, gain=0.30)),
                       at(0.30, tone(1046, 0.28, "tri", decay=0.34, gain=0.32))),
    }

    total = 0
    for name in sorted(cues):
        total += save(name, cues[name])

    print(f"cues  : {len(cues)}")
    print(f"output: {os.path.relpath(OUT_DIR, ROOT)}  ({total / 1024:.0f} KB total)")


if __name__ == "__main__":
    main()
