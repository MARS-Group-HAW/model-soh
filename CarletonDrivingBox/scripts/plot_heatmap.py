#!/usr/bin/env python3
"""Plot heatmap_matrix.csv — campus road congestion over time.

Default:
  - plasma, vmax = 99th percentile of positive densities
  - trailing inactive samples masked
  - display-only temporal upsample + bilinear (DEVS-like soft streaks)
  - writes ``heatmap_matrix.png``

``--comparable`` — fixed color scale for side-by-side figures:
  - plasma, vmax=20
  - no trailing mask; optional ``--roads-from`` for shared Y-axis
  - same display smoothing (matrix CSV unchanged)
  - writes ``heatmap_matrix_comparable.png``
"""
from __future__ import annotations

import argparse
import csv
import math
import re
from pathlib import Path

import matplotlib.pyplot as plt
import numpy as np

ROOT = Path(__file__).resolve().parents[1]
DEFAULT_HEAT = ROOT / "results" / "heatmap_matrix.csv"

FIXED_VMAX = 20.0
COMPARE_VMAX = FIXED_VMAX  # alias for compare helpers
COMPARE_FIG_WIDTH = 12.0
COMPARE_FIG_H_PER_ROAD = 0.35
COMPARE_FIG_H_MIN = 6.0
COMPARE_DPI = 200

CUSTOM_R28_ROADS = [
    "P3 & Raven Rd to Bronson Ave & Raven Rd",
    "Raven Rd & University Dr to Bronson Ave & Raven Rd",
]
CUSTOM_R28_SET = set(CUSTOM_R28_ROADS)

DEFAULT_VMAX_PERCENTILE = 99.0

# Display-only rendering (matrix CSV / metric unchanged).
# MARS samples are often dt≈10s; DEVS heatmaps use dt≈1s — upsample + bilinear
# so coarse bins do not render as solid blocks.
DISPLAY_TARGET_DT = 1.0
DISPLAY_INTERPOLATION = "bilinear"
DISPLAY_TIME_SMOOTH_SIGMA = 0.9  # samples after upsample; 0 disables


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


def read_road_names(path: Path) -> list[str]:
    return read_road_names_from_heatmap(path)


def read_road_names_from_heatmap(path: Path) -> list[str]:
    with path.open(encoding="utf-8", newline="") as f:
        header = next(csv.reader(f))
    return [c.strip() for c in header[1:] if c.strip()]


def order_roads_compare_style(roads: list[str]) -> list[int]:
    """Alphabetical shared roads; custom r28 first in array → visual bottom."""
    custom_present = [r for r in CUSTOM_R28_ROADS if r in roads]
    extras = [r for r in roads if r in CUSTOM_R28_SET and r not in custom_present]
    custom = custom_present + extras
    custom_set = set(custom)
    normal = sorted(r for r in roads if r not in custom_set)
    ordered_names = custom + normal
    index = {name: i for i, name in enumerate(roads)}
    return [index[name] for name in ordered_names]


def filter_roads(
    roads: list[str],
    m: np.ndarray,
    *,
    keep_names: set[str] | None = None,
    active_only: bool = False,
) -> tuple[list[str], np.ndarray]:
    keep_idx = list(range(len(roads)))
    if keep_names is not None:
        keep_idx = [i for i in keep_idx if roads[i] in keep_names]
    if active_only:
        keep_idx = [i for i in keep_idx if float(np.nanmax(m[:, i])) > 0]
    if not keep_idx:
        return [], m[:, :0]
    return [roads[i] for i in keep_idx], m[:, keep_idx]


def align_to_reference_roads(
    times: np.ndarray,
    roads: list[str],
    m: np.ndarray,
    ref_roads: list[str],
    *,
    append_r28: bool = False,
) -> tuple[list[str], np.ndarray]:
    """Pad/reorder to a reference road list for 1:1 Y-axis alignment."""
    by_name = {name: m[:, i] for i, name in enumerate(roads)}
    n_t = len(times)
    ordered = list(ref_roads)
    if append_r28:
        extras = [r for r in CUSTOM_R28_ROADS if r in by_name and r not in ordered]
        ordered = extras + ordered
    cols = []
    for name in ordered:
        cols.append(by_name[name] if name in by_name else np.zeros(n_t, dtype=float))
    return ordered, np.column_stack(cols) if cols else np.zeros((n_t, 0))


def mask_trailing_inactive(m: np.ndarray) -> np.ma.MaskedArray:
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
    vals = np.asarray(m, dtype=float).ravel()
    vals = vals[np.isfinite(vals)]
    if vals.size == 0:
        return 1.0
    positive = vals[vals > 0]
    base = positive if positive.size else vals
    return max(float(np.percentile(base, percentile)), 1.0)


def _gaussian_kernel1d(sigma: float) -> np.ndarray:
    """Unit-sum 1D Gaussian; radius ~ 3σ (numpy-only, no scipy)."""
    if sigma <= 0:
        return np.array([1.0], dtype=float)
    radius = max(1, int(math.ceil(3.0 * sigma)))
    x = np.arange(-radius, radius + 1, dtype=float)
    k = np.exp(-0.5 * (x / sigma) ** 2)
    k /= k.sum()
    return k


def _smooth_along_time(data: np.ndarray, sigma: float) -> np.ndarray:
    """Convolve each road column along time; edges reflect-padded. Display only."""
    if sigma <= 0 or data.shape[0] < 2:
        return data
    kernel = _gaussian_kernel1d(sigma)
    pad = len(kernel) // 2
    out = np.empty_like(data, dtype=float)
    for j in range(data.shape[1]):
        col = np.pad(data[:, j].astype(float), pad, mode="edge")
        out[:, j] = np.convolve(col, kernel, mode="valid")
    return out


def prepare_display_matrix(
    times: np.ndarray,
    m: np.ndarray | np.ma.MaskedArray,
    *,
    target_dt: float = DISPLAY_TARGET_DT,
    time_smooth_sigma: float = DISPLAY_TIME_SMOOTH_SIGMA,
) -> tuple[np.ndarray, np.ndarray | np.ma.MaskedArray]:
    """Upsample + light time-axis smooth for rendering. Does not alter source CSV values.

    Linear interp between real bin centers; optional mild Gaussian only along time
    (anti-aliases coarse steps). Road ordering / metric unchanged.
    """
    times = np.asarray(times, dtype=float)
    is_masked = np.ma.isMaskedArray(m)
    data = np.ma.filled(m, 0.0).astype(float) if is_masked else np.asarray(m, dtype=float)
    mask = np.ma.getmaskarray(m) if is_masked else None

    if times.size < 2 or data.size == 0:
        return times, m

    native_dt = float(np.median(np.diff(times)))
    if native_dt <= 0:
        return times, m

    # Only upsample when bins are coarser than the display target (e.g. 10s → 1s).
    if native_dt > target_dt * 1.05:
        n_up = int(math.floor((times[-1] - times[0]) / target_dt)) + 1
        n_up = max(n_up, times.size)
        times_up = np.linspace(times[0], times[-1], n_up)
        data_up = np.empty((n_up, data.shape[1]), dtype=float)
        for j in range(data.shape[1]):
            data_up[:, j] = np.interp(times_up, times, data[:, j])
        if mask is not None:
            # Nearest-time mask so trailing inactive stays masked (no invented glow).
            idx = np.clip(
                np.searchsorted(times, times_up, side="left"),
                0,
                len(times) - 1,
            )
            # Prefer left bin when equally between samples
            mid = (times[np.minimum(idx, len(times) - 1)] + times[np.maximum(idx - 1, 0)]) / 2.0
            use_left = (idx > 0) & (times_up < mid)
            idx = np.where(use_left, idx - 1, idx)
            mask_up = mask[idx, :]
        else:
            mask_up = None
        times, data, mask = times_up, data_up, mask_up

    data = _smooth_along_time(data, time_smooth_sigma)
    # After smooth, keep masked cells at plasma floor via mask (not zero bleed).
    if mask is not None:
        return times, np.ma.array(data, mask=mask)
    return times, data


def plot_congestion_heatmap(
    times: np.ndarray,
    roads: list[str],
    m: np.ndarray,
    out_path: Path,
    *,
    scenario_id: str | None = None,
    vmax: float,
    mask_trailing: bool,
    comparable: bool,
    title: str | None = None,
    truncate_labels: bool = True,
) -> float:
    plot_m: np.ndarray | np.ma.MaskedArray = np.asarray(m, dtype=float)
    cmap = plt.cm.plasma.copy()
    if mask_trailing:
        plot_m = mask_trailing_inactive(m)
        cmap.set_bad(cmap(0.0))

    disp_times, plot_m = prepare_display_matrix(times, plot_m)

    if comparable:
        fig_w, fig_h = COMPARE_FIG_WIDTH, max(COMPARE_FIG_H_MIN, len(roads) * COMPARE_FIG_H_PER_ROAD)
        ylabel = "Roads"
        if title is None:
            title = f"Campus Evacuation Heatmap — {scenario_tag(scenario_id)} (fixed vmax={FIXED_VMAX:g})"
    else:
        fig_w, fig_h = 14.0, max(8.0, len(roads) * 0.34)
        ylabel = "Campus road"
        if title is None:
            title = f"Campus congestion by road — {scenario_tag(scenario_id)}"

    fig, ax = plt.subplots(figsize=(fig_w, fig_h))
    im = ax.imshow(
        plot_m.T,
        aspect="auto",
        origin="lower",
        cmap=cmap,
        vmin=0.0,
        vmax=vmax,
        extent=[float(disp_times[0]), float(disp_times[-1]), -0.5, len(roads) - 0.5]
        if len(disp_times)
        else None,
        interpolation=DISPLAY_INTERPOLATION,
        resample=True,
    )
    ax.set_xlabel("Time (s)")
    ax.set_ylabel(ylabel)
    ax.set_title(title)
    fig.colorbar(im, ax=ax, label="Vehicles per 100 m")

    if roads:
        ax.set_yticks(range(len(roads)))
        names = roads if not truncate_labels else [short_road_label(r) for r in roads]
        labels = ax.set_yticklabels(names, fontsize=7)
        for i, tick in enumerate(labels):
            if roads[i] in CUSTOM_R28_SET:
                tick.set_color("crimson")
                tick.set_fontweight("bold")
            else:
                tick.set_color("black")

    if len(disp_times) and disp_times[-1] >= 600 and not comparable:
        minute_step = 600 if disp_times[-1] >= 3600 else 300
        xticks = np.arange(0, disp_times[-1] + 1, minute_step)
        ax.set_xticks(xticks)
        ax.set_xticklabels([f"{int(t // 60)}m" for t in xticks], fontsize=8)

    fig.tight_layout()
    out_path.parent.mkdir(parents=True, exist_ok=True)
    fig.savefig(out_path, dpi=COMPARE_DPI)
    plt.close(fig)
    return float(vmax)


def main():
    ap = argparse.ArgumentParser(
        description=(
            "Plot campus road congestion heatmap. Default = MARS percentile scale; "
            "use --comparable for a fixed vmax=20 shared color scale."
        )
    )
    ap.add_argument("heat", nargs="?", type=Path, default=DEFAULT_HEAT)
    ap.add_argument("--heat", dest="heat_flag", type=Path, help=argparse.SUPPRESS)
    ap.add_argument("--out", type=Path, default=None)
    ap.add_argument(
        "--vmax",
        type=str,
        default=None,
        help=(
            "Color max. Default: 99th percentile (MARS) or 20 with --comparable. "
            "Pass a number, or 'auto'/'p99' for percentile."
        ),
    )
    ap.add_argument(
        "--comparable",
        dest="comparable",
        action="store_true",
        help=(
            "Fixed vmax=20 encoding (no trailing mask) for cross-model comparison. "
            "Default output: heatmap_matrix_comparable.png. "
            "Pass --roads-from another heatmap_matrix.csv for 1:1 Y-axis."
        ),
    )
    ap.add_argument(
        "--mask-trailing",
        action="store_true",
        help="Force trailing mask on (already default in non-comparable mode).",
    )
    ap.add_argument(
        "--no-mask-trailing",
        action="store_true",
        help="Disable trailing mask even in MARS default mode.",
    )
    ap.add_argument(
        "--roads-from",
        type=Path,
        default=None,
        help="Align Y-axis to another heatmap_matrix.csv (road column order/set).",
    )
    ap.add_argument(
        "--append-r28",
        action="store_true",
        help="With --roads-from: also show MARS-only r28 rows at visual bottom.",
    )
    ap.add_argument(
        "--active-only",
        action="store_true",
        help="Hide roads that stay zero (ignored when --roads-from is set).",
    )
    ap.add_argument("--scenario", help="Scenario id for title (inferred from path if omitted)")
    args = ap.parse_args()

    heat_path = args.heat_flag or args.heat
    comparable = bool(args.comparable)
    scenario_id = args.scenario or scenario_id_from_path(heat_path)

    if args.out is not None:
        out_path = args.out
    elif comparable:
        out_path = heat_path.parent / "heatmap_matrix_comparable.png"
    else:
        out_path = heat_path.parent / "heatmap_matrix.png"

    times, roads, m = read_heatmap(heat_path)
    if m.size == 0:
        raise SystemExit(f"Empty heatmap matrix: {heat_path}")

    if args.roads_from is not None:
        ref_roads = read_road_names_from_heatmap(args.roads_from)
        if not ref_roads:
            raise SystemExit(f"No road columns in --roads-from: {args.roads_from}")
        roads, m = align_to_reference_roads(
            times, roads, m, ref_roads, append_r28=bool(args.append_r28)
        )
        print(f"Aligned Y-axis to {len(roads)} roads from {args.roads_from}")
    else:
        order = order_roads_compare_style(roads)
        roads = [roads[i] for i in order]
        m = m[:, order]
        if args.active_only:
            roads, m = filter_roads(roads, m, active_only=True)

    # Resolve vmax
    vmax_arg = args.vmax
    if vmax_arg is None:
        if comparable:
            vmax, vmax_note = FIXED_VMAX, "fixed vmax=20"
        else:
            vmax, vmax_note = auto_vmax(m), f"p{DEFAULT_VMAX_PERCENTILE:g}"
    else:
        s = str(vmax_arg).strip().lower()
        if s in ("auto", "p99", "percentile", "mars"):
            vmax, vmax_note = auto_vmax(m), f"p{DEFAULT_VMAX_PERCENTILE:g}"
        else:
            try:
                vmax = max(float(s), 1.0)
            except ValueError as exc:
                raise SystemExit(f"Invalid --vmax {vmax_arg!r}") from exc
            vmax_note = "fixed vmax=20" if abs(vmax - FIXED_VMAX) < 1e-9 else "fixed"

    if args.no_mask_trailing:
        mask_trailing = False
    elif args.mask_trailing:
        mask_trailing = True
    else:
        mask_trailing = not comparable

    used = plot_congestion_heatmap(
        times,
        roads,
        m,
        out_path,
        scenario_id=scenario_id,
        vmax=vmax,
        mask_trailing=mask_trailing,
        comparable=comparable,
        truncate_labels=not comparable,
    )
    print(f"Wrote {out_path} (roads={len(roads)}, vmax={used:.2f} [{vmax_note}])")


if __name__ == "__main__":
    main()
