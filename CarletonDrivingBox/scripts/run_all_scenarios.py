#!/usr/bin/env python3
"""Run MARS campus evacuation scenarios 01-06 (Windows batch wrapper).

On Windows, use scripts/run_all_scenarios.cmd directly.
"""
from __future__ import annotations

import argparse
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def main() -> int:
    ap = argparse.ArgumentParser(description="Run CarletonDrivingBox scenarios (delegates to .cmd on Windows).")
    ap.add_argument("--no-build", action="store_true", help="Skip dotnet build")
    args = ap.parse_args()

    cmd_path = ROOT / "scripts" / "run_all_scenarios.cmd"
    if not cmd_path.is_file():
        print(f"Missing runner: {cmd_path}", file=sys.stderr)
        return 1

    cmd = ["cmd.exe", "/c", str(cmd_path)]
    if args.no_build:
        cmd.append("--no-build")

    print("+", " ".join(cmd), flush=True)
    return subprocess.call(cmd, cwd=ROOT)


if __name__ == "__main__":
    raise SystemExit(main())
