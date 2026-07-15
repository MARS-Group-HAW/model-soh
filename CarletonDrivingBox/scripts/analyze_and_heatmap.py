#!/usr/bin/env python3
"""Analyze one run and build + plot its heatmap."""
from __future__ import annotations

import subprocess
import sys
from pathlib import Path

from mars_agent_outputs import agent_output_path

ROOT = Path(__file__).resolve().parents[1]
RESULTS = ROOT / "results"
DEFAULT_RESULTS = RESULTS / "scenario_01"


def main() -> int:
    csv = (
        Path(sys.argv[1])
        if len(sys.argv) > 1
        else agent_output_path(DEFAULT_RESULTS, ".csv")
    )
    if not csv.is_file():
        print(f"CSV not found: {csv}", file=sys.stderr)
        return 1

    matrix = csv.parent / "heatmap_matrix.csv"
    steps = [
        [sys.executable, str(ROOT / "scripts" / "analyze_run.py"), str(csv)],
        [sys.executable, str(ROOT / "scripts" / "build_heatmap_matrix.py"), str(csv)],
        [sys.executable, str(ROOT / "scripts" / "plot_heatmap.py"), str(matrix)],
    ]
    for cmd in steps:
        print("+", " ".join(cmd), flush=True)
        rc = subprocess.call(cmd, cwd=ROOT)
        if rc != 0:
            return rc
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
