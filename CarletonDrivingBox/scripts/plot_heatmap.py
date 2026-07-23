#!/usr/bin/env python3
"""Plot MARS heatmap_matrix.csv — congestion for all named campus roads over time.

Road order and styling match the DEVS/Cadmium Campus Evacuation Heatmap:
  - non-custom roads sorted alphabetically (same as DEVS ``sorted(roads_seen)``)
  - custom Raven→Bronson (r28) corridors last when reading the Y-axis (visual bottom
    with ``origin="lower"``), labeled in red
  - plasma colormap with auto vmax = 99th percentile of densities (MARS scale);
    pass ``--vmax 20`` for a DEVS-comparable clip
  - trailing inactive samples masked so each road's activity bar ends when
    vehicles leave (variable-length rows)
"""
from __future__ import annotations

import argparse
import csv
import re
from pathlib import Path

import matplotlib.pyplot as plt
import numpy as np

ROOT = Path(__file__).resolve().parents[1]
DEFAULT_HEAT = ROOT / "results" / "heatmap_matrix.csv"
DEFAULT_PNG = ROOT / "results" / "heatmap_matrix.png"

# Custom emergency Raven→Bronson corridors (scenario_07+ graph / r28). Red labels,
# placed last on the Y-axis (visual bottom with origin="lower").
CUSTOM_R28_ROADS = [
    "P3 & Raven Rd to Bronson Ave & Raven Rd",
    "Raven Rd & University Dr to Bronson Ave & Raven Rd",
]
CUSTOM_R28_SET = set(CUSTOM_R28_ROADS)

# MARS default: robust auto scale (avoids a few peaks washing out the map).
DEFAULT_VMAX_PERCENTILE = 99.0


def scenario_id_from_path(path: Path) -> str | None:
    match = re.search(r"scenario_(\d+)", path.as_posix())
    return match.group(1) if match else None


def scenario_tag(scenario_id: str | None) -> str:
    return f"scenario_{scenario_id.zfill(2)}" if scenario_id else "run"


def short_road_label(name: str, max_len: int = 48) -> str:
    if len(name) <= max_len:
        return name
    return name[: max_len - 1] + "…"


def read_heatmap(path: Path):
    with path.open(encoding="utf-8", newline="") as f:
        reader = csv.reader(f)
        header = next(reader)
        roads = [c.strip() for c in header[1:]]
        times, rows = [], []
        for row in reader:
            if not row:
                continue
            times.append(float(row[0]))
            rows.append([float(x) for x in row[1:]])
    return np.array(times), roads, np.array(rows, dtype=float)


def order_roads_devs_style(roads: list[str]) -> list[int]:
    """Return column indices for DEVS-like Y-axis order.

    Shared/campus roads are alphabetical (same as DEVS ``sorted(roads_seen)``).
    Custom r28 corridors come first in the array so that with ``origin="lower"``
    they sit at the visual bottom (last when reading the Y-axis top→bottom).
    """
    custom_present = [r for r in CUSTOM_R28_ROADS if r in roads]
    # Any unexpected custom-named extras still treated as custom.
    extras = [r for r in roads if r in CUSTOM_R28_SET and r not in custom_present]
    custom = custom_present + extras
    custom_set = set(custom)
    normal = sorted(r for r in roads if r not in custom_set)
    ordered_names = custom + normal
    index = {name: i for i, name in enumerate(roads)}
    return [index[name] for name in ordered_names]


def mask_trailing_inactive(m: np.ndarray) -> np.ma.MaskedArray:
    """Mask samples after each road's last >0 occupancy (variable-length bars).

    Mid-run zeros stay visible as plasma(0); only the inactive tail is masked
    so the row visually ends when activity stops (DEVS-like cutoffs).
    """
    masked = np.ma.array(m, mask=False, copy=True)
    n_times, n_roads = m.shape
    for j in range(n_roads):
        col = m[:, j]
        nz = np.flatnonzero(col > 0)
        if nz.size == 0:
            masked.mask[:, j] = True
        else:
            last = int(nz[-1])
            if last + 1 < n_times:
                masked.mask[last + 1 :, j] = True
    return masked


def auto_vmax(m: np.ndarray, percentile: float = DEFAULT_VMAX_PERCENTILE) -> float:
    """99th-percentile scale used by earlier MARS heatmaps (typically ~80+)."""
    vals = np.asarray(m, dtype=float).ravel()
    vals = vals[np.isfinite(vals)]
    if vals.size == 0:
        return 1.0
    # Prefer positive densities so empty cells don't pull the scale down.
    positive = vals[vals > 0]
    base = positive if positive.size else vals
    vmax = float(np.percentile(base, percentile))
    return max(vmax, 1.0)


def main():
    ap = argparse.ArgumentParser(description="Plot full-campus road congestion heatmap")
    ap.add_argument("heat", nargs="?", type=Path, default=DEFAULT_HEAT)
    ap.add_argument("--heat", dest="heat_flag", type=Path, help=argparse.SUPPRESS)
    ap.add_argument("--out", type=Path, default=None)
    ap.add_argument(
        "--vmax",
        type=float,
        default=None,
        help=(
            "Color max (vehicles per 100 m). Default: 99th percentile of positive "
            "densities (MARS scale). Use --vmax 20 for a DEVS-comparable clip."
        ),
    )
    ap.add_argument(
        "--active-only",
        action="store_true",
        help="Hide roads that stay zero (default: show ALL campus roads)",
    )
    ap.add_argument("--scenario", help="Scenario id for title (inferred from path if omitted)")
    args = ap.parse_args()

    heat_path = args.heat_flag or args.heat
    out_path = args.out or (heat_path.parent / "heatmap_matrix.png")
    scenario_id = args.scenario or scenario_id_from_path(heat_path)

    times, roads, m = read_heatmap(heat_path)
    if m.size == 0:
        raise SystemExit(f"Empty heatmap matrix: {heat_path}")

    order = order_roads_devs_style(roads)
    roads = [roads[i] for i in order]
    m = m[:, order]

    if args.active_only:
        keep = [i for i, _ in enumerate(roads) if float(np.nanmax(m[:, i])) > 0]
        roads = [roads[i] for i in keep]
        m = m[:, keep]

    plot_m = mask_trailing_inactive(m)

    if args.vmax is not None:
        vmax = max(float(args.vmax), 1.0)
        vmax_note = "fixed"
    else:
        vmax = auto_vmax(m)
        vmax_note = f"p{DEFAULT_VMAX_PERCENTILE:g}"

    cmap = plt.cm.plasma.copy()
    # Trailing inactive (masked) matches zero-density plasma so bars "end" cleanly.
    cmap.set_bad(cmap(0.0))

    height = max(8.0, len(roads) * 0.34)
    fig, ax = plt.subplots(figsize=(14, height))
    im = ax.imshow(
        plot_m.T,
        aspect="auto",
        origin="lower",
        cmap=cmap,
        vmin=0.0,
        vmax=vmax,
        extent=[times[0], times[-1], -0.5, len(roads) - 0.5],
        interpolation="nearest",
    )
    ax.set_xlabel("Time (s)")
    ax.set_ylabel("Campus road")
    ax.set_title(f"Campus congestion by road — {scenario_tag(scenario_id)}")
    fig.colorbar(im, ax=ax, label="Vehicles per 100 m")

    if roads:
        ax.set_yticks(range(len(roads)))
        labels = ax.set_yticklabels([short_road_label(r) for r in roads], fontsize=7)
        for i, tick in enumerate(labels):
            if roads[i] in CUSTOM_R28_SET:
                tick.set_color("crimson")
                tick.set_fontweight("bold")
            else:
                tick.set_color("black")

    if len(times) and times[-1] >= 600:
        minute_step = 600 if times[-1] >= 3600 else 300
        xticks = np.arange(0, times[-1] + 1, minute_step)
        ax.set_xticks(xticks)
        ax.set_xticklabels([f"{int(t // 60)}m" for t in xticks], fontsize=8)

    fig.tight_layout()
    out_path.parent.mkdir(parents=True, exist_ok=True)
    fig.savefig(out_path, dpi=200)
    plt.close(fig)
    print(f"Wrote {out_path} (roads={len(roads)}, vmax={vmax:.2f} [{vmax_note}])")


if __name__ == "__main__":
    main()
