#!/usr/bin/env python3
"""Analyze MARS results for scenarios 01–10 (sequential or in parallel).

Default: run ``analyze_run.py`` only.
Pass ``--heatmap`` to also build and plot the congestion matrix (same as
``analyze_and_heatmap.py`` per scenario).
"""
from __future__ import annotations

import argparse
import subprocess
import sys
from concurrent.futures import ThreadPoolExecutor, as_completed
from pathlib import Path

from mars_agent_outputs import agent_output_path

ROOT = Path(__file__).resolve().parents[1]
SCENARIOS = tuple(f"{i:02d}" for i in range(1, 11))
ANALYZE = ROOT / "scripts" / "analyze_run.py"
BUILD_HEAT = ROOT / "scripts" / "build_heatmap_matrix.py"
PLOT_HEAT = ROOT / "scripts" / "plot_heatmap.py"


def analyze_scenario(sid: str, *, heatmap: bool, dt: int) -> tuple[str, int]:
    out_dir = ROOT / "results" / f"scenario_{sid}"
    csv = agent_output_path(out_dir, ".csv")
    log = out_dir / "analyze.log"
    out_dir.mkdir(parents=True, exist_ok=True)

    if not csv.is_file():
        msg = f"MISSING: {csv}\n"
        print(f"[skip]  scenario_{sid} — no agent CSV", flush=True)
        log.write_text(msg, encoding="utf-8")
        return sid, 2

    steps: list[list[str]] = [[sys.executable, str(ANALYZE), str(csv)]]
    if heatmap:
        matrix = csv.parent / "heatmap_matrix.csv"
        steps.append(
            [sys.executable, str(BUILD_HEAT), str(csv), "--dt", str(dt)]
        )
        steps.append([sys.executable, str(PLOT_HEAT), str(matrix)])

    print(f"[start] scenario_{sid}", flush=True)
    with log.open("w", encoding="utf-8") as out:
        for cmd in steps:
            line = "+ " + " ".join(cmd) + "\n"
            out.write(line)
            out.flush()
            print(line.strip(), flush=True)
            proc = subprocess.run(
                cmd,
                cwd=ROOT,
                stdout=out,
                stderr=subprocess.STDOUT,
                text=True,
            )
            if proc.returncode != 0:
                print(
                    f"[done]  scenario_{sid} FAIL({proc.returncode})  (log: {log})",
                    flush=True,
                )
                return sid, proc.returncode

    print(f"[done]  scenario_{sid} ok  (log: {log})", flush=True)
    return sid, 0


def main() -> int:
    ap = argparse.ArgumentParser(
        description="Analyze CarletonDrivingBox scenarios 01–10 (optionally in parallel)."
    )
    ap.add_argument(
        "--parallel",
        "-p",
        action="store_true",
        help="Analyze selected scenarios at the same time.",
    )
    ap.add_argument(
        "--jobs",
        "-j",
        type=int,
        default=0,
        help="Max parallel jobs with --parallel (default: all selected).",
    )
    ap.add_argument(
        "--heatmap",
        action="store_true",
        help="Also build heatmap_matrix.csv and heatmap_matrix.png per scenario.",
    )
    ap.add_argument(
        "--dt",
        type=int,
        default=10,
        help="Heatmap sample interval in seconds (with --heatmap). Default: 10.",
    )
    ap.add_argument(
        "scenarios",
        nargs="*",
        help="Scenario ids (e.g. 01 03 10). Default: all 01–10.",
    )
    args = ap.parse_args()

    selected: list[str] = []
    for sid in args.scenarios or list(SCENARIOS):
        sid = f"{int(sid):02d}"
        if sid not in SCENARIOS:
            print(f"Unknown scenario: {sid}", file=sys.stderr)
            return 1
        selected.append(sid)

    if not args.parallel:
        failed = 0
        for sid in selected:
            print(f"\n=== scenario_{sid} ===", flush=True)
            _sid, rc = analyze_scenario(sid, heatmap=args.heatmap, dt=args.dt)
            if rc != 0:
                failed += 1
        if failed:
            print(f"\n{failed} scenario(s) missing or failed.", file=sys.stderr)
            return 1
        print("\nDone. Summaries in results/scenario_XX/")
        return 0

    jobs = args.jobs if args.jobs > 0 else len(selected)
    jobs = max(1, min(jobs, len(selected)))
    print(
        f"\nParallel analyze: {len(selected)} scenarios, jobs={jobs}"
        + (" + heatmap" if args.heatmap else ""),
        flush=True,
    )

    failures: list[str] = []
    missing: list[str] = []
    with ThreadPoolExecutor(max_workers=jobs) as pool:
        futures = {
            pool.submit(analyze_scenario, sid, heatmap=args.heatmap, dt=args.dt): sid
            for sid in selected
        }
        for fut in as_completed(futures):
            sid, rc = fut.result()
            if rc == 2:
                missing.append(sid)
            elif rc != 0:
                failures.append(sid)

    if missing:
        print(f"\nMissing CSV: {', '.join(sorted(missing))}", file=sys.stderr)
    if failures:
        print(f"Failed: {', '.join(sorted(failures))}", file=sys.stderr)
    if missing or failures:
        return 1
    print("\nDone. Summaries in results/scenario_XX/ (see each analyze.log)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
