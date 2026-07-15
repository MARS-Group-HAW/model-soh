#!/usr/bin/env python3
"""Plot MARS heatmap_matrix.csv — road occupancy heatmap."""
import argparse
import csv
import re
from pathlib import Path

import matplotlib.pyplot as plt
import numpy as np

ROOT = Path(__file__).resolve().parents[1]
DEFAULT_HEAT = ROOT / "results" / "heatmap_matrix.csv"
DEFAULT_PNG = ROOT / "results" / "heatmap_matrix.png"

DEFAULT_CAMPUS_EXITS = {
    "P3 & Raven Rd to Bronson Ave & Raven Rd",
    "Library Rd & University Dr to Colonel By Dr & University Dr",
    "P5 & Stadium Way to Bronson Ave & Stadium Way",
    "Roundabout to Bronson Ave & University Dr",
}
EMERGENCY_ROAD = "Raven Rd & University Dr to Bronson Ave & Raven Rd"
SCENARIO_07_EXITS = DEFAULT_CAMPUS_EXITS | {EMERGENCY_ROAD}


def scenario_id_from_path(path: Path) -> str | None:
    match = re.search(r"scenario_(\d+)", path.as_posix())
    return match.group(1) if match else None


def scenario_tag(scenario_id: str | None) -> str:
    return f"scenario_{scenario_id.zfill(2)}" if scenario_id else "run"


def highlight_roads(scenario_id: str | None, roads: list[str]) -> list[str]:
    if scenario_id == "7" or scenario_id == "07":
        return [r for r in sorted(SCENARIO_07_EXITS - DEFAULT_CAMPUS_EXITS) if r in roads]
    return []


def read_heatmap(path: Path):
    with path.open(encoding="utf-8", newline="") as f:
        reader = csv.reader(f)
        header = next(reader)
        roads = [c.strip() for c in header[1:]]
        times, rows = [], []
        for row in reader:
            times.append(float(row[0]))
            rows.append([float(x) for x in row[1:]])
    return np.array(times), roads, np.array(rows)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("heat", nargs="?", type=Path, default=DEFAULT_HEAT, help="heatmap_matrix.csv path")
    ap.add_argument("--heat", dest="heat_flag", type=Path, help=argparse.SUPPRESS)
    ap.add_argument("--out", type=Path, default=None)
    ap.add_argument("--vmax", type=float, default=20.0)
    ap.add_argument("--scenario", help="Scenario id for title/highlights (e.g. 07); inferred from path if omitted")
    args = ap.parse_args()

    heat_path = args.heat_flag or args.heat
    out_path = args.out or (heat_path.parent / "heatmap_matrix.png")
    scenario_id = args.scenario or scenario_id_from_path(heat_path)

    times, roads, m = read_heatmap(heat_path)
    highlight = set(highlight_roads(scenario_id, roads))

    height = max(6.0, len(roads) * 0.28)
    fig, ax = plt.subplots(figsize=(12, height))
    im = ax.imshow(m.T, aspect="auto", origin="lower", cmap="plasma", vmax=args.vmax)
    ax.set_xlabel("Time (s)")
    ax.set_ylabel("Roads")
    ax.set_title(f"Campus Evacuation Heatmap — {scenario_tag(scenario_id)}")
    fig.colorbar(im, ax=ax, label="Vehicles per 100 m")

    if roads:
        ax.set_yticks(range(len(roads)))
        labels = ax.set_yticklabels(roads, fontsize=7)
        for i, tick in enumerate(labels):
            if roads[i] in highlight:
                tick.set_color("red")
                tick.set_fontweight("bold")

    fig.tight_layout()
    out_path.parent.mkdir(parents=True, exist_ok=True)
    fig.savefig(out_path, dpi=200)
    plt.close(fig)
    print(f"Wrote {out_path}")


if __name__ == "__main__":
    main()
