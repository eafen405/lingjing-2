"""Embed the recorded C# experiment in one double-clickable, offline HTML file.

Run from the repository root:
    python prototype/ai-exploration/build_demo.py
The page is a replay/inspection shell, never a second battle simulator.
"""
from __future__ import annotations

import argparse
from datetime import datetime, timezone
import hashlib
import json
from pathlib import Path


def load(path: Path):
    return json.loads(path.read_text(encoding="utf-8-sig"))


def result_path(directory: Path, value: str) -> Path:
    path = Path(value)
    if path.is_absolute() and path.is_file():
        return path
    candidate = directory / path
    return candidate if candidate.is_file() else directory / path.name


def main() -> None:
    base = Path(__file__).resolve().parent
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--results", type=Path, default=base / "results")
    parser.add_argument("--output", type=Path, default=base / "fairy-algorithms.prototype.html")
    args = parser.parse_args()
    summary_path = args.results / "summary.json"
    summary = load(summary_path)
    payload = {
        "summary": summary,
        "phase": load(args.results / "phase-probe.json"),
        "traces": {},
        "decisions": {},
        "generatedAt": datetime.now(timezone.utc).isoformat(timespec="seconds"),
        "summarySha256": hashlib.sha256(summary_path.read_bytes()).hexdigest(),
    }
    frame_count = 0
    decision_count = 0
    for run in summary["runs"]:
        key = f'{run["scenarioId"]}|{run["strategy"]}'
        trace = load(result_path(args.results, run["traceFile"]))
        decisions = load(result_path(args.results, run["decisionsFile"]))
        payload["traces"][key] = trace
        payload["decisions"][key] = decisions
        frame_count += len(trace.get("frames", []))
        decision_count += len(decisions.get("decisions", []))
    template = (base / "demo.template.html").read_text(encoding="utf-8")
    marker = "__PROTOTYPE_DATA__"
    if template.count(marker) != 1:
        raise ValueError("Expected exactly one payload marker in the prototype template")
    encoded = json.dumps(payload, ensure_ascii=False, separators=(",", ":"), allow_nan=False)
    encoded = encoded.replace("</", "<\\/")
    args.output.write_text(template.replace(marker, encoded), encoding="utf-8")
    print(f"Embedded {len(summary['runs'])} runs, {frame_count} frames, {decision_count} decisions")
    print(f"HTML: {args.output.resolve()} ({args.output.stat().st_size:,} bytes)")
    print(f"Summary SHA-256: {payload['summarySha256']}")


if __name__ == "__main__":
    main()
