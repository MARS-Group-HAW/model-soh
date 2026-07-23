#!/usr/bin/env python3
"""Run campus evacuation scenarios 01–11."""
from __future__ import annotations

import argparse
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
PROJECT = "SOHCarletonDrivingBox.csproj"
SCENARIOS = tuple(f"{i:02d}" for i in range(1, 12))


def run(cmd: list[str]) -> int:
    print("+", " ".join(cmd), flush=True)
    return subprocess.call(cmd, cwd=ROOT)


def main() -> int:
    ap = argparse.ArgumentParser(description="Run CarletonDrivingBox scenarios 01–11.")
    ap.add_argument("--no-build", action="store_true", help="Skip dotnet build")
    ap.add_argument(
        "scenarios",
        nargs="*",
        help="Scenario ids to run (e.g. 01 03 11). Default: all 01–11.",
    )
    args = ap.parse_args()

    selected = args.scenarios or list(SCENARIOS)
    for sid in selected:
        sid = f"{int(sid):02d}"
        if sid not in SCENARIOS:
            print(f"Unknown scenario: {sid}", file=sys.stderr)
            return 1

    if not args.no_build:
        rc = run(["dotnet", "build", PROJECT])
        if rc != 0:
            return rc

    for sid in selected:
        sid = f"{int(sid):02d}"
        config = f"configs/config_scenario_{sid}.json"
        print(f"\n=== scenario_{sid} ===", flush=True)
        rc = run(["dotnet", "run", "--project", PROJECT, "--", config])
        if rc != 0:
            return rc

    print("\nDone. Results under results/scenario_XX/")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
