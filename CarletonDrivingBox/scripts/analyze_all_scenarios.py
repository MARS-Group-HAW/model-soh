#!/usr/bin/env python3
"""Analyze MARS results for scenarios 01-06."""
from __future__ import annotations

import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SCENARIOS = ("01", "02", "03", "04", "05", "06")


def main() -> int:
    analyze = ROOT / "scripts" / "analyze_run.py"
    failed = 0
    for sid in SCENARIOS:
        csv = ROOT / "results" / f"scenario_{sid}" / "CarDriver.csv"
        print(f"\n=== scenario_{sid} ===", flush=True)
        if not csv.is_file():
            print(f"MISSING: {csv}", file=sys.stderr)
            failed += 1
            continue
        rc = subprocess.call([sys.executable, str(analyze), str(csv)])
        if rc != 0:
            failed += 1
    if failed:
        print(f"\n{failed} scenario(s) missing or failed.", file=sys.stderr)
        return 1
    print("\nDone. Summaries in results/scenario_XX/")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
