#!/usr/bin/env python3
"""Plot heatmap_matrix.csv — campus road congestion over time.

Default:
  - plasma, vmax = MARS_GLOBAL_VMAX (global max across scenarios 01-12)
  - trailing inactive samples masked
  - display-only temporal upsample + bilinear (DEVS-like soft streaks)
  - DEVS road list / labels + comparable figure sizing when DEVS CSV exists
  - CUSTOM_R28 (Raven emergency) at visual bottom
  - writes ``heatmap_matrix.png``

``--comparable`` — fixed color scale for side-by-side figures:
  - plasma, vmax=20
  - no trailing mask; optional ``--roads-from`` for shared Y-axis
  - same labels / sizing / r28-at-bottom as default
  - writes ``heatmap_matrix_comparable.png``

``--xmax`` — shared time axis for cross-scenario stacks (e.g. BASELINE_XMAX=13030):
  - pads shorter matrices with zero/masked cells so plasma floor fills to xmax
  - writes same default / comparable output names
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
# DEVS campus-evacuation heatmaps (source of truth for shared road Y-axis order).
DEVS_RESULTS = Path(
    r"\\wsl$\Ubuntu-24.04\home\doria\Cadmium_Projects\model-campus-evacuation-main\results"
)
DEVS_RESULTS_CANDIDATES = [
    Path(r"\\wsl$\Ubuntu-24.04\home\doria\Cadmium_Projects\model-campus-evacuation-main\results"),
    Path("/home/doria/Cadmium_Projects/model-campus-evacuation-main/results"),
]

FIXED_VMAX = 20.0
# Global max vehicles/100m across MARS scenarios 01-12 heatmap_matrix.csv
# (exact GLOBAL_MAX=184.04040445029833 from scenario_07; ceil for colorbar).
MARS_GLOBAL_VMAX = 185.0
COMPARE_VMAX = FIXED_VMAX  # alias for compare helpers
COMPARE_FIG_WIDTH = 12.0
COMPARE_FIG_H_PER_ROAD = 0.35
COMPARE_FIG_H_MIN = 6.0
COMPARE_DPI = 200
# Last timestamp in scenario_01/heatmap_matrix.csv. Use this shared x-axis
# maximum when stacking Scenario 01 against shorter scenarios for fair comparison.
BASELINE_XMAX = 13030.0

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


def scenario_chart_title(
    base: str,
    scenario_id: str | None,
    *,
    framework: str = "MARS",
) -> str:
    """Title format: '{What it shows} — Scenario XX (MARS)'."""
    if scenario_id:
        sid = str(scenario_id).strip()
        if sid.isdigit():
            sid = sid.zfill(2)
        else:
            m = re.fullmatch(r"scenario_(\d+)", sid, flags=re.IGNORECASE)
            sid = m.group(1).zfill(2) if m else sid
        return f"{base} — Scenario {sid} ({framework})"
    return f"{base} ({framework})"


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



def normalize_road_name(name: str) -> str:
    """Canonicalize connectors/whitespace for matching (→/-> to ' to ')."""
    s = (name or "").strip()
    s = s.replace("→", " to ").replace("->", " to ").replace("⇒", " to ")
    s = re.sub(r"\s+", " ", s)
    s = re.sub(r"\s+to\s+", " to ", s, flags=re.IGNORECASE)
    return s


def split_corridor(name: str) -> tuple[str, str] | None:
    n = normalize_road_name(name)
    if " to " not in n:
        return None
    a, b = n.split(" to ", 1)
    return a.strip(), b.strip()


def reverse_corridor_name(name: str) -> str | None:
    parts = split_corridor(name)
    if not parts:
        return None
    a, b = parts
    return f"{b} to {a}"


def order_roads_compare_style(roads: list[str]) -> list[int]:
    """DEVS-equivalent bake order: CUSTOM_R28 first, then sorted alphabetical normals.

    With origin=lower, first array index is visual bottom (r28 at bottom).
    Fallback when no DEVS reference heatmap is available.
    """
    custom_present = [r for r in CUSTOM_R28_ROADS if r in roads]
    extras = [r for r in roads if r in CUSTOM_R28_SET and r not in custom_present]
    custom = custom_present + extras
    custom_set = set(custom)
    normal = sorted(r for r in roads if r not in custom_set)
    ordered_names = custom + normal
    index = {name: i for i, name in enumerate(roads)}
    return [index[name] for name in ordered_names]


def _r28_indices_first(roads: list[str], indices: list[int]) -> tuple[list[int], list[int]]:
    """Split indices into (CUSTOM_R28 in preferred order, remaining in given order)."""
    by_name = {roads[i]: i for i in indices}
    r28: list[int] = []
    seen: set[int] = set()
    for name in CUSTOM_R28_ROADS:
        if name in by_name:
            i = by_name[name]
            r28.append(i)
            seen.add(i)
    for i in indices:
        if i not in seen and roads[i] in CUSTOM_R28_SET:
            r28.append(i)
            seen.add(i)
    rest = [i for i in indices if i not in seen]
    return r28, rest


def _order_with_extras_at_visual_bottom(
    roads: list[str],
    shared_idx: list[int],
    extra_idx: list[int],
) -> list[int]:
    """Place MARS-only extras + r28 at visual bottom (array start, origin=lower).

    Final array order: [all CUSTOM_R28, other MARS-only extras, shared non-r28].
    Matches DEVS ``order_roads_r28_at_bottom`` for emergency corridors.
    """
    r28_shared, shared_rest = _r28_indices_first(roads, shared_idx)
    r28_extra, other_extra = _r28_indices_first(roads, extra_idx)
    # Prefer CUSTOM_R28_ROADS sequence across shared+extra hits
    r28_all, _ = _r28_indices_first(roads, r28_shared + r28_extra)
    return r28_all + other_extra + shared_rest


def order_roads_devs_style(
    roads: list[str],
    ref_roads: list[str] | None = None,
) -> list[int]:
    """Reorder MARS roads to match DEVS display/CSV order (no padding).

    Preferred: ``ref_roads`` from a DEVS ``heatmap_matrix.csv`` header.
    Shared names follow DEVS order; MARS-only roads (including r28) are placed
    at the array start so they render at the visual BOTTOM with origin=lower.

    Without ``ref_roads``, falls back to ``order_roads_compare_style``
    (custom r28 + sorted normals), matching DEVS data_analysis bake order.
    """
    if not ref_roads:
        return order_roads_compare_style(roads)
    used: set[int] = set()
    shared_idx: list[int] = []
    ref_norms = {normalize_road_name(x) for x in ref_roads}
    for ref in ref_roads:
        nref = normalize_road_name(ref)
        hit = None
        for i, r in enumerate(roads):
            if i in used:
                continue
            if normalize_road_name(r) == nref:
                hit = i
                break
        if hit is not None:
            used.add(hit)
            shared_idx.append(hit)
            rev = reverse_corridor_name(ref)
            if rev:
                nrev = normalize_road_name(rev)
                if nrev not in ref_norms:
                    for i, r in enumerate(roads):
                        if i in used:
                            continue
                        if normalize_road_name(r) == nrev:
                            used.add(i)
                            shared_idx.append(i)
                            break
    extra_idx = [i for i in range(len(roads)) if i not in used]
    return _order_with_extras_at_visual_bottom(roads, shared_idx, extra_idx)


def align_mars_roads_to_devs(
    roads: list[str],
    m: np.ndarray,
    ref_roads: list[str] | None,
) -> tuple[list[str], np.ndarray]:
    """Map MARS→DEVS canonical names where equivalent; order like DEVS.

    - Match by normalize_road_name (handles to/→/->).
    - Shared: DEVS order, display label = DEVS canonical string.
    - After each DEVS road, insert MARS-only reverse corridor (if present) so pairs stay adjacent.
    - Remaining MARS-only roads + CUSTOM_R28 at visual bottom (array start, origin=lower).
    - No zero-padding.
    """
    if not ref_roads:
        order = order_roads_compare_style(roads)
        display = [roads[i] for i in order]
        return display, m[:, order]

    used: set[int] = set()
    shared_idx: list[int] = []
    shared_labels: list[str] = []
    ref_norms = {normalize_road_name(x) for x in ref_roads}
    for ref in ref_roads:
        nref = normalize_road_name(ref)
        hit = None
        for i, r in enumerate(roads):
            if i in used:
                continue
            if normalize_road_name(r) == nref:
                hit = i
                break
        if hit is not None:
            used.add(hit)
            shared_idx.append(hit)
            shared_labels.append(ref)  # DEVS canonical label
            rev = reverse_corridor_name(ref)
            if rev:
                nrev = normalize_road_name(rev)
                if nrev not in ref_norms:
                    for i, r in enumerate(roads):
                        if i in used:
                            continue
                        if normalize_road_name(r) == nrev:
                            used.add(i)
                            shared_idx.append(i)
                            shared_labels.append(r)
                            break
    extra_idx = [i for i in range(len(roads)) if i not in used]
    label_by_idx = {i: lab for i, lab in zip(shared_idx, shared_labels)}
    for i in extra_idx:
        label_by_idx[i] = roads[i]
    ordered_idx = _order_with_extras_at_visual_bottom(roads, shared_idx, extra_idx)
    display = [label_by_idx[i] for i in ordered_idx]
    return display, m[:, ordered_idx]


def resolve_devs_heatmap_for_scenario(scenario_id: str | None) -> Path | None:
    """Locate DEVS results/scenario_XX/heatmap_matrix.csv for the same scenario id."""
    if not scenario_id:
        return None
    sid = str(scenario_id).strip()
    m = re.fullmatch(r"scenario_(\d+)", sid, flags=re.IGNORECASE)
    if m:
        sid = m.group(1)
    if sid.isdigit():
        sid = sid.zfill(2)
    for base in DEVS_RESULTS_CANDIDATES:
        cand = base / f"scenario_{sid}" / "heatmap_matrix.csv"
        try:
            if cand.is_file():
                return cand
        except OSError:
            continue
    return None



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
    by_norm: dict[str, np.ndarray] = {}
    for i, name in enumerate(roads):
        n = normalize_road_name(name)
        if n not in by_norm:
            by_norm[n] = m[:, i]
    n_t = len(times)
    ordered = list(ref_roads)
    if append_r28:
        extras = [r for r in CUSTOM_R28_ROADS if r in by_name and r not in ordered]
        ordered = extras + ordered
    cols = []
    for name in ordered:
        if name in by_name:
            cols.append(by_name[name])
        else:
            n = normalize_road_name(name)
            cols.append(by_norm[n] if n in by_norm else np.zeros(n_t, dtype=float))
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


def ensure_custom_r28_at_visual_bottom(
    roads: list[str],
    m: np.ndarray,
) -> tuple[list[str], np.ndarray]:
    """Move CUSTOM_R28 corridors to array start → visual bottom (origin=lower)."""
    if not roads:
        return roads, m
    custom_present = [r for r in CUSTOM_R28_ROADS if r in roads]
    extras = [r for r in roads if r in CUSTOM_R28_SET and r not in custom_present]
    custom = custom_present + extras
    custom_set = set(custom)
    rest = [r for r in roads if r not in custom_set]
    ordered = custom + rest
    index = {name: i for i, name in enumerate(roads)}
    idxs = [index[name] for name in ordered]
    return ordered, m[:, idxs]


def pad_display_to_xmax(
    times: np.ndarray,
    m: np.ndarray | np.ma.MaskedArray,
    xmax: float,
    *,
    dt: float = DISPLAY_TARGET_DT,
) -> tuple[np.ndarray, np.ndarray | np.ma.MaskedArray]:
    """Extend time axis + zero-fill so imshow covers [t0, xmax] (no white void)."""
    times = np.asarray(times, dtype=float)
    if times.size == 0 or xmax is None:
        return times, m
    t_last = float(times[-1])
    if xmax <= t_last + 1e-9:
        return times, m
    step = float(dt) if dt and dt > 0 else 1.0
    if times.size >= 2:
        native = float(np.median(np.diff(times)))
        if native > 0:
            step = native
    pad_times = np.arange(t_last + step, xmax + 0.5 * step, step, dtype=float)
    if pad_times.size == 0 or float(pad_times[-1]) < xmax - 1e-9:
        # Ensure the last sample lands on xmax so extent matches set_xlim.
        pad_times = np.concatenate([pad_times, np.array([xmax], dtype=float)])
    n_pad = int(pad_times.size)
    n_roads = int(m.shape[1]) if getattr(m, "ndim", 0) == 2 else 0
    if n_roads == 0:
        return np.concatenate([times, pad_times]), m
    zeros = np.zeros((n_pad, n_roads), dtype=float)
    if np.ma.isMaskedArray(m):
        # Mask padded cells → plasma floor via set_bad (same as trailing inactive).
        pad = np.ma.array(zeros, mask=True)
        out_m = np.ma.concatenate([m, pad], axis=0)
    else:
        out_m = np.vstack([np.asarray(m, dtype=float), zeros])
    return np.concatenate([times, pad_times]), out_m


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
    xmax: float | None = None,
    title: str | None = None,
    truncate_labels: bool = True,
) -> float:
    plot_m: np.ndarray | np.ma.MaskedArray = np.asarray(m, dtype=float)
    cmap = plt.cm.plasma.copy()
    if mask_trailing:
        plot_m = mask_trailing_inactive(m)
        cmap.set_bad(cmap(0.0))

    disp_times, plot_m = prepare_display_matrix(times, plot_m)
    if xmax is not None:
        disp_times, plot_m = pad_display_to_xmax(disp_times, plot_m, float(xmax))

    # Default and comparable share DEVS-comparable layout (labels + figure sizing).
    # Only vmax / trailing-mask encoding differ between modes.
    fig_w = COMPARE_FIG_WIDTH
    fig_h = max(COMPARE_FIG_H_MIN, len(roads) * COMPARE_FIG_H_PER_ROAD)
    ylabel = "Roads"
    if title is None:
        title = scenario_chart_title("Campus congestion heatmap", scenario_id)

    t0 = float(disp_times[0]) if len(disp_times) else 0.0
    t1 = float(xmax) if xmax is not None else (
        float(disp_times[-1]) if len(disp_times) else 1.0
    )
    fig, ax = plt.subplots(figsize=(fig_w, fig_h))
    im = ax.imshow(
        plot_m.T,
        aspect="auto",
        origin="lower",
        cmap=cmap,
        vmin=0.0,
        vmax=vmax,
        extent=[t0, t1, -0.5, len(roads) - 0.5] if len(disp_times) else None,
        interpolation=DISPLAY_INTERPOLATION,
        resample=True,
    )
    if xmax is not None:
        ax.set_xlim(t0, float(xmax))
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

    fig.tight_layout()
    out_path.parent.mkdir(parents=True, exist_ok=True)
    fig.savefig(out_path, dpi=COMPARE_DPI)
    plt.close(fig)
    return float(vmax)


def main():
    ap = argparse.ArgumentParser(
        description=(
            "Plot campus road congestion heatmap. Default = MARS global vmax "
            "(MARS_GLOBAL_VMAX=185); use --comparable for fixed vmax=20; "
            "use --vmax auto/p99 for per-file percentile."
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
            "Color max. Default: MARS_GLOBAL_VMAX=185 (shared across scenarios) "
            "or 20 with --comparable. Pass a number, or 'auto'/'p99' "
            "for 99th-percentile scale."
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
    ap.add_argument(
        "--xmax",
        type=float,
        default=None,
        help=(
            "Optional shared x-axis maximum (seconds). Use BASELINE_XMAX=13030 "
            "(scenario_01 heatmap_matrix.csv last time) for fair Scenario 01 "
            "versus 10/11/12 stacks. Pads shorter runs with zero / masked cells "
            "so the plasma floor fills to xmax (no white void)."
        ),
    )
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
        print(
            f"Aligned Y-axis to {len(roads)} roads from {args.roads_from} "
            f"(append_r28={bool(args.append_r28)})"
        )
    else:
        # Default + comparable: same DEVS road list / labels when available.
        ref_path = resolve_devs_heatmap_for_scenario(scenario_id)
        ref_roads = read_road_names_from_heatmap(ref_path) if ref_path is not None else None
        if ref_roads:
            roads, m = align_to_reference_roads(
                times, roads, m, ref_roads, append_r28=False
            )
            print(f"Y-axis: DEVS road list from {ref_path} (n={len(roads)})")
        else:
            roads, m = align_mars_roads_to_devs(roads, m, None)
            print("Y-axis: DEVS-equivalent order (custom r28 + sorted normals; no DEVS CSV found)")
        if args.active_only:
            roads, m = filter_roads(roads, m, active_only=True)

    # Emergency Raven corridors last on Y (visual bottom), matching DEVS heatmaps.
    roads, m = ensure_custom_r28_at_visual_bottom(roads, m)

    # Resolve vmax
    vmax_arg = args.vmax
    if vmax_arg is None:
        if comparable:
            vmax, vmax_note = FIXED_VMAX, "fixed vmax=20"
        else:
            vmax, vmax_note = MARS_GLOBAL_VMAX, f"MARS_GLOBAL_VMAX={MARS_GLOBAL_VMAX:g}"
    else:
        s = str(vmax_arg).strip().lower()
        if s in ("auto", "p99", "percentile"):
            vmax, vmax_note = auto_vmax(m), f"p{DEFAULT_VMAX_PERCENTILE:g}"
        elif s in ("mars", "global"):
            vmax, vmax_note = MARS_GLOBAL_VMAX, f"MARS_GLOBAL_VMAX={MARS_GLOBAL_VMAX:g}"
        else:
            try:
                vmax = max(float(s), 1.0)
            except ValueError as exc:
                raise SystemExit(f"Invalid --vmax {vmax_arg!r}") from exc
            if abs(vmax - FIXED_VMAX) < 1e-9:
                vmax_note = "fixed vmax=20"
            elif abs(vmax - MARS_GLOBAL_VMAX) < 1e-9:
                vmax_note = f"MARS_GLOBAL_VMAX={MARS_GLOBAL_VMAX:g}"
            else:
                vmax_note = "fixed"

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
        xmax=args.xmax,
        truncate_labels=False,
    )
    print(f"Wrote {out_path} (roads={len(roads)}, vmax={used:.2f} [{vmax_note}])")


if __name__ == "__main__":
    main()
