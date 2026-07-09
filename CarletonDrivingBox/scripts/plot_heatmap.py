#!/usr/bin/env python3
"""Plot MARS heatmap_matrix.csv (same style as DEVS visualize_processed.py)."""
import argparse
import csv
from pathlib import Path

import matplotlib.pyplot as plt
import numpy as np

ROOT = Path(__file__).resolve().parents[1]
DEFAULT_HEAT = ROOT / "results" / "heatmap_matrix.csv"
DEFAULT_PNG = ROOT / "results" / "heatmap_matrix.png"


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
    ap.add_argument("--heat", type=Path, default=DEFAULT_HEAT)
    ap.add_argument("--out", type=Path, default=DEFAULT_PNG)
    ap.add_argument("--vmax", type=float, default=20.0)
    args = ap.parse_args()

    times, roads, m = read_heatmap(args.heat)
    plt.figure(figsize=(12, 6))
    plt.imshow(m.T, aspect="auto", origin="lower", cmap="plasma", vmax=args.vmax)
    plt.xlabel("Time (s)")
    plt.ylabel("Roads")
    plt.title("Campus Evacuation Heatmap")
    plt.colorbar(label="Vehicles per 100 m")
    if len(roads) <= 20:
        plt.yticks(range(len(roads)), roads, fontsize=7)
    else:
        plt.yticks([])
    plt.tight_layout()
    args.out.parent.mkdir(parents=True, exist_ok=True)
    plt.savefig(args.out, dpi=200)
    plt.close()
    print(f"Wrote {args.out}")


if __name__ == "__main__":
    main()
