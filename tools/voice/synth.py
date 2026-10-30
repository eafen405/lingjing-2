#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Fairy 语音离线合成流水线。

流程：解析 docs/specs/audio.md 的台词表 → 按标点切成短段 → 逐段合成 →
按停顿表拼接 → 44.1kHz mono / 峰值 -1 dBFS / -16 LUFS → Ogg Vorbis q5 → voice_manifest.json。

只在本地跑，需要 GPT-SoVITS 的内置 Python 环境：
  D:\\Agents\\GPT-Sovits\\GPT-SoVITS-1007-cu128\\runtime\\python.exe tools/voice/synth.py
"""

from __future__ import annotations

import argparse
import json
import os
import re
import subprocess
import sys
import time

import numpy as np
import yaml

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", ".."))
SPEC = os.path.join(REPO, "docs", "specs", "audio.md")

GSV_ROOT = r"D:\Agents\GPT-Sovits\GPT-SoVITS-1007-cu128"
FAIRY_DIR = r"D:\Agents\Fairy\Fairy\Fairy"
GPT_CKPT = os.path.join(FAIRY_DIR, "Fairy-e10.ckpt")
SOVITS_PTH = os.path.join(FAIRY_DIR, "Fairy_e10_s870.pth")

# 合成参数固定，保证同一句两次生成基本一致（见规格 §4）
SEED = 20261030
TOP_K = 5
TOP_P = 1.0
TEMPERATURE = 1.0
REPETITION_PENALTY = 1.35
TEXT_SPLIT_METHOD = "cut5"

# 停顿表：分段之间插入的静音（秒）。规格 §4.1。
# 逗号和句号都单独切成段，拼接静音随之有层级：逗号 0.40 < 句号 0.55。
PAUSE_DEFAULT = {"，": 0.40, "、": 0.40, "；": 0.45, "。": 0.55, "？": 0.55, "！": 0.55, "：": 0.35}
PAUSE_BEAT = 0.60  # （停顿）
TRIM_PEAK_DB = -45.0  # 相对峰值的静音门限
TRIM_PAD = 0.02  # 裁静音后保留的余量（秒）
GAP_WARN = 0.06  # 段内静音超过这个长度就报警：模型把词拆开了

# 个别句子固定 seed 时会被读成两个词（例如「恶名/以骸」之间插 80ms 停顿）。
# 这里按 id 覆盖，取验证过没有段内停顿的那版；批次默认 seed 不变。
SEED_OVERRIDES = {"vo_fairy_boss_intro": 7}

TARGET_LUFS = -16.0
TARGET_PEAK_DBFS = -1.0

ROW_RE = re.compile(
    r"^\|\s*`(?P<id>vo_fairy_[^`]+)`\s*"
    r"\|\s*`(?P<event>[^`]+)`\s*"
    r"\|\s*(?P<text>[^|]+?)\s*"
    r"\|\s*(?P<mood>[^|]+?)\s*"
    r"\|\s*(?P<priority>P[0-3])\s*"
    r"\|\s*(?P<cooldown>\d+)\s*"
    r"\|\s*(?P<interrupt>是|否)\s*\|\s*$"
)


# ---------------------------------------------------------------- 台词表


def load_lines(spec_path=SPEC):
    """从规格的 §2.1 表格里读出台词，本表是文本唯一权威。"""
    with open(spec_path, encoding="utf-8") as f:
        rows = [ROW_RE.match(line.rstrip("\n")) for line in f]
    lines = []
    for m in rows:
        if not m:
            continue
        lines.append(
            {
                "id": m["id"],
                "event": m["event"],
                "text": m["text"],
                "mood": m["mood"],
                "priority": m["priority"],
                "cooldown": int(m["cooldown"]),
                "interrupt": m["interrupt"] == "是",
            }
        )
    if not lines:
        raise SystemExit(f"没在 {spec_path} 里解析到台词行，检查 §2.1 表格格式")
    return lines


def strip_subtitle_marks(text):
    """字幕文本：去掉朗读标记，展开只上字幕的 〔…〕。"""
    text = text.replace("（停顿）", "")
    text = re.sub(r"〔(.*?)〕", r"\1", text)
    return text.strip()


def strip_audio_marks(text):
    """朗读文本：去掉只上字幕的 〔…〕 与朗读标记。"""
    text = re.sub(r"〔.*?〕", "", text)
    text = text.replace("（停顿）", "（停顿）")
    text = text.strip()
    # 只剩半句时补句号，避免以逗号结尾让模型拖尾
    if text.endswith(("，", ",", "、", "：", ":")):
        text = text[:-1] + "。"
    return text


def split_segments(text):
    """切段：先在 （停顿） 处切，再在逗号/句号类标点处切；返回 [(文本, 后接停顿)]。"""
    segs = []
    chunks = text.split("（停顿）")
    for idx, chunk in enumerate(chunks):
        buf = ""
        for ch in chunk:
            buf += ch
            if ch in PAUSE_DEFAULT:
                segs.append([buf.strip(), PAUSE_DEFAULT[ch]])
                buf = ""
        if buf.strip():
            segs.append([buf.strip(), 0.0])
        if idx < len(chunks) - 1:
            if not segs:
                raise SystemExit(f"（停顿）出现在句首：{text!r}")
            segs[-1][1] = max(segs[-1][1], PAUSE_BEAT)
    return [(s, p) for s, p in segs if s]


# ---------------------------------------------------------------- 合成


def load_pipeline(workdir):
    os.chdir(GSV_ROOT)
    sys.path.insert(0, GSV_ROOT)
    sys.path.insert(0, os.path.join(GSV_ROOT, "GPT_SoVITS"))
    import torch
    from GPT_SoVITS.TTS_infer_pack.TTS import TTS, TTS_Config

    device = "cuda:0" if torch.cuda.is_available() else "cpu"
    half = device != "cpu"
    cfg = {
        "bert_base_path": "GPT_SoVITS/pretrained_models/chinese-roberta-wwm-ext-large",
        "cnhuhbert_base_path": "GPT_SoVITS/pretrained_models/chinese-hubert-base",
        "device": device,
        "is_half": half,
        "version": "v2",  # Fairy 权重是 gsv-v2final 上微调的
        "t2s_weights_path": GPT_CKPT,
        "vits_weights_path": SOVITS_PTH,
    }
    cfg_path = os.path.join(workdir, "tts_infer_fairy.yaml")
    with open(cfg_path, "w", encoding="utf-8") as f:
        yaml.safe_dump({"custom": cfg}, f, allow_unicode=True)
    return TTS(TTS_Config(cfg_path)), device


def find_ref_audio():
    # 参考音频与参考文本都取自 Fairy 权重目录：只有一条 wav，文件名就是参考文本
    wavs = [f for f in os.listdir(FAIRY_DIR) if f.lower().endswith(".wav")]
    if len(wavs) != 1:
        raise SystemExit(f"{FAIRY_DIR} 里应当恰好有一条参考 wav，实际 {len(wavs)} 条")
    path = os.path.join(FAIRY_DIR, wavs[0])
    return path, os.path.splitext(wavs[0])[0]


def synth_segment(pipeline, text, ref_wav, prompt_text, seed=SEED):
    inputs = {
        "text": text,
        "text_lang": "zh",
        "ref_audio_path": ref_wav,
        "prompt_text": prompt_text,
        "prompt_lang": "zh",
        "top_k": TOP_K,
        "top_p": TOP_P,
        "temperature": TEMPERATURE,
        "text_split_method": TEXT_SPLIT_METHOD,
        "batch_size": 1,
        "speed_factor": 1.0,
        "return_fragment": False,
        "seed": seed,
        "parallel_infer": True,
        "repetition_penalty": REPETITION_PENALTY,
    }
    sr, audio = None, None
    for sr, audio in pipeline.run(inputs):
        pass
    if audio is None:
        raise RuntimeError(f"合成没有返回音频：{text!r}")
    return int(sr), np.asarray(audio, dtype=np.int16)


def internal_gaps(audio, sr, thresh_db=-35.0):
    """段内静音（首尾不算）：返回 [(长度秒, 起点秒)]，用于发现模型把词拆开。"""
    x = np.abs(audio.astype(np.float32))
    peak = float(x.max()) or 1.0
    th = peak * 10 ** (thresh_db / 20)
    silent = x < th
    gaps, start = [], None
    for i, s in enumerate(silent):
        if s and start is None:
            start = i
        elif not s and start is not None:
            gaps.append((start, i))
            start = None
    if start is not None:
        gaps.append((start, len(silent)))
    out = []
    for a, b in gaps:
        if a == 0 or b == len(silent):
            continue
        out.append(((b - a) / sr, a / sr))
    return out


def trim_silence(audio, sr):
    idx = np.flatnonzero(np.abs(audio.astype(np.int32)) > np.max(np.abs(audio.astype(np.int32))) * 10 ** (TRIM_PEAK_DB / 20))
    if idx.size == 0:
        return audio
    pad = int(TRIM_PAD * sr)
    return audio[max(0, idx[0] - pad) : min(len(audio), idx[-1] + pad + 1)]


def render_line(pipeline, line, ref_wav, prompt_text, out_dir):
    segs = split_segments(strip_audio_marks(line["text"]))
    seed = SEED_OVERRIDES.get(line["id"], SEED)
    sr_used = None
    parts, pauses, warns = [], [], []
    for text, pause in segs:
        sr, audio = synth_segment(pipeline, text, ref_wav, prompt_text, seed)
        sr_used = sr
        audio = trim_silence(audio, sr)
        for length, at in internal_gaps(audio, sr):
            if length >= GAP_WARN:
                warns.append(f"段内 {length:.2f}s 静音 @{at:.2f}s：「{text}」")
        parts.append(audio)
        pauses.append(pause)
    raw = [parts[0]]
    for part, pause in zip(parts[1:], pauses[:-1]):
        if pause > 0:
            raw.append(np.zeros(int(round(pause * sr_used)), dtype=np.int16))
        raw.append(part)
    joined = np.concatenate(raw)

    tmp_wav = os.path.join(out_dir, line["id"] + ".32k.wav")
    import soundfile as sf

    sf.write(tmp_wav, joined, sr_used, subtype="PCM_16")
    return tmp_wav, [s for s, _ in segs], seed, warns


# ---------------------------------------------------------------- 后处理


def ffmpeg_exe(name="ffmpeg"):
    return os.path.join(GSV_ROOT, "runtime", name + ".exe")


def measure(path):
    """读集成响度与采样峰值：返回 (LUFS, peak_dBFS)。"""
    out = subprocess.run(
        [
            ffmpeg_exe(),
            "-hide_banner",
            "-nostats",
            "-i",
            path,
            "-af",
            "astats=measure_perchannel=none:measure_overall=Peak_level,ebur128",
            "-f",
            "null",
            "-",
        ],
        capture_output=True,
        text=True,
        encoding="utf-8",
        errors="replace",
    ).stderr
    peak = [float(m) for m in re.findall(r"Peak level dB:\s*(-?\d+(?:\.\d+)?)", out)]
    lufs = [float(m) for m in re.findall(r"I:\s*(-?\d+(?:\.\d+)?)\s*LUFS", out)]
    if not peak or not lufs:
        raise RuntimeError(f"响度测量失败：{path}\n{out[-800:]}")
    return lufs[-1], peak[-1]


def to_ogg(wav, ogg):
    subprocess.run(
        [
            ffmpeg_exe(),
            "-hide_banner",
            "-loglevel",
            "error",
            "-y",
            "-i",
            wav,
            "-ar",
            "44100",
            "-ac",
            "1",
            "-c:a",
            "libvorbis",
            "-q:a",
            "5",
            ogg,
        ],
        check=True,
    )


def normalize_ogg(wav, ogg):
    """线性增益到 -16 LUFS，并保证峰值不超过 -1 dBFS。"""
    lufs, peak = measure(wav)
    gain = TARGET_LUFS - lufs
    if peak + gain > TARGET_PEAK_DBFS:
        gain = TARGET_PEAK_DBFS - peak
    out_wav = wav.replace(".32k.wav", ".norm.wav")
    subprocess.run(
        [ffmpeg_exe(), "-hide_banner", "-loglevel", "error", "-y", "-i", wav, "-af", f"volume={gain:.2f}dB", out_wav],
        check=True,
    )
    to_ogg(out_wav, ogg)
    os.remove(out_wav)
    return gain, lufs + gain


def probe_duration(path):
    out = subprocess.run(
        [ffmpeg_exe("ffprobe"), "-v", "error", "-show_entries", "format=duration", "-of", "default=nw=1:nk=1", path],
        capture_output=True,
        text=True,
    ).stdout.strip()
    return round(float(out), 3)


# ---------------------------------------------------------------- 主流程


def main(argv=None):
    ap = argparse.ArgumentParser(description="Fairy 语音批量合成")
    ap.add_argument("--only", help="只跑这些 id，逗号分隔")
    ap.add_argument("--out", default=os.path.join(HERE, "out", "v1"), help="输出目录")
    ap.add_argument("--dry-run", action="store_true", help="只解析台词表并打印分段，不加载模型")
    ap.add_argument("--keep-wav", action="store_true", help="保留中间 32k wav")
    args = ap.parse_args(argv)

    os.makedirs(args.out, exist_ok=True)
    lines = load_lines()
    if args.only:
        want = {s.strip() for s in args.only.split(",") if s.strip()}
        lines = [ln for ln in lines if ln["id"] in want]
        if not lines:
            raise SystemExit("--only 没有匹配到任何 id")

    if args.dry_run:
        for ln in lines:
            tts = strip_audio_marks(ln["text"])
            print(f"## {ln['id']}  [{ln['event']}]  P={ln['priority']}")
            print(f"   字幕: {strip_subtitle_marks(ln['text'])}")
            for text, pause in split_segments(tts):
                print(f"   段: {text!r}  后接 {pause:.2f}s")
        return 0

    ref_wav, prompt_text = find_ref_audio()
    t0 = time.time()
    pipeline, device = load_pipeline(args.out)
    print(f"[env] device={device} models loaded in {time.time() - t0:.1f}s", flush=True)

    manifest = {
        "generator": "tools/voice/synth.py",
        "version": "v1",
        "samplerate": 44100,
        "format": "ogg/vorbis q5",
        "lufs": TARGET_LUFS,
        "peak_dbfs": TARGET_PEAK_DBFS,
        "reference": os.path.basename(ref_wav),
        "seed": SEED,
        "seed_overrides": SEED_OVERRIDES,
        "lines": [],
    }

    for ln in lines:
        t0 = time.time()
        wav, segs, seed, warns = render_line(pipeline, ln, ref_wav, prompt_text, args.out)
        for w in warns:
            print(f"[warn] {ln['id']}: {w}", flush=True)
        ogg = os.path.join(args.out, ln["id"] + ".ogg")
        gain, lufs = normalize_ogg(wav, ogg)
        dur = probe_duration(ogg)
        if not args.keep_wav:
            os.remove(wav)
        manifest["lines"].append(
            {
                "id": ln["id"],
                "file": ln["id"] + ".ogg",
                "event": ln["event"],
                "priority": ln["priority"],
                "cooldown": ln["cooldown"],
                "interrupt": ln["interrupt"],
                "text": strip_subtitle_marks(ln["text"]),
                "mood": ln["mood"],
                "segments": segs,
                "seed": seed,
                "duration": dur,
                "lufs": round(lufs, 1),
                "gain_db": round(gain, 2),
                "warnings": warns,
            }
        )
        print(f"[ok] {ln['id']:<32} {dur:5.2f}s  {lufs:6.1f} LUFS  gain{gain:+6.2f}dB  {time.time() - t0:5.1f}s", flush=True)

    with open(os.path.join(args.out, "voice_manifest.json"), "w", encoding="utf-8") as f:
        json.dump(manifest, f, ensure_ascii=False, indent=2)
    print(f"[done] {len(manifest['lines'])} 条 -> {args.out}", flush=True)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
