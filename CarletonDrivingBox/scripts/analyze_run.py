#!/usr/bin/env python3
"""Summarize a MARS scheduler run — deployed vs completed, evacuation charts."""
import csv
import json
import math
import re
import sys
from collections import Counter
from datetime import datetime, timedelta, timezone
from pathlib import Path
from statistics import mean, median

import matplotlib.pyplot as plt

from mars_agent_outputs import agent_output_path

ROOT = Path(__file__).resolve().parents[1]
RESULTS = ROOT / "results"
SCHEDULES_DIR = ROOT / "resources" / "schedules"
CONFIGS_DIR = ROOT / "configs"
DEFAULT_SCENARIO = "01"
DEFAULT_RESULTS = RESULTS / f"scenario_{DEFAULT_SCENARIO}"
DEFAULT_SCHEDULE = SCHEDULES_DIR / f"scenario_{DEFAULT_SCENARIO}_schedule.csv"
DEFAULT_CONFIG = CONFIGS_DIR / f"config_scenario_{DEFAULT_SCENARIO}.json"
SCHEDULE_BASE = ROOT / "resources" / "schedule_base.csv"
PARKING_LOT_SPAWNS = ROOT / "resources" / "parking_lot_spawns.csv"
CAMPUS_BBOX_MARGIN_DEG = 0.0003
SPAWN_NOISE_M = 80.0
# Match schedule anchors to lot refs within this distance (legacy curb vs interior).
LOT_MATCH_TOL_M = 300.0
BASELINE_TARGET = 3200
LOT_COUNTS = {"P1": 100, "P2": 100, "P3": 200, "P4": 100, "P5": 700, "P6": 900, "P7": 1100}
LOT_ORDER = ["P1", "P2", "P3", "P4", "P5", "P6", "P7"]
LOT_MARKER_COLORS = {
    "P1": "#e41a1c",
    "P2": "#377eb8",
    "P3": "#4daf4a",
    "P4": "#984ea3",
    "P5": "#ff7f00",
    "P6": "#a65628",
    "P7": "#f781bf",
}

# Core campus footprint for exit detection — NOT the expanded OSM routing AOI
# (that bbox includes Meadowlands / NE destinations, so trips never "leave").
# East edge sits on Bronson (~-75.6910): Brewer Park (45.387983, -75.690183)
# is east of Bronson and must be OUTSIDE so NE/Brewer trips count as campus leave.
CAMPUS_CORE_BBOX = (45.3785, 45.3917, -75.7030, -75.6910)


def load_lot_coords() -> dict[str, tuple[float, float]]:
    """Interior aisle anchors for lot attribution (prefer parking_lot_spawns.csv)."""
    coords: dict[str, tuple[float, float]] = {}
    if PARKING_LOT_SPAWNS.is_file():
        with PARKING_LOT_SPAWNS.open(encoding="utf-8") as f:
            for row in csv.DictReader(f):
                lot = (row.get("lot") or "").strip()
                if lot:
                    coords[lot] = (float(row["spawn_lat"]), float(row["spawn_lon"]))
        if coords:
            return coords
    if not SCHEDULE_BASE.is_file():
        return coords
    lines = [
        line
        for line in SCHEDULE_BASE.read_text(encoding="utf-8").splitlines()
        if line.strip() and not line.strip().startswith("#")
    ]
    for row in csv.DictReader(lines):
        coords[row["lot"]] = (float(row["startLat"]), float(row["startLon"]))
    return coords


def _lot_clearance_items(clearance_by_lot: dict | None) -> list[tuple[str, float]]:
    """Return (lot, t_s) only for lots that fully left campus (last campus exit)."""
    items: list[tuple[str, float]] = []
    for lot in LOT_ORDER:
        val = (clearance_by_lot or {}).get(lot)
        if val is None or val == "":
            continue
        try:
            t = float(val)
        except (TypeError, ValueError):
            continue
        if t > 0:
            items.append((lot, t))
    items.sort(key=lambda x: x[1])
    return items


def annotate_lot_clearances(ax, clearance_by_lot: dict | None) -> None:
    """Dashed markers: last campus exit only when that lot fully left campus."""
    items = _lot_clearance_items(clearance_by_lot)
    if not items:
        return

    ymin, ymax = ax.get_ylim()
    x0, x1 = ax.get_xlim()
    x_span = max(x1 - x0, 1.0)
    cluster_thresh = 0.035 * x_span

    level = 0
    prev_t: float | None = None
    for lot, t in items:
        if prev_t is not None and abs(t - prev_t) < cluster_thresh:
            level = (level + 1) % 4
        else:
            level = 0
        prev_t = t
        color = LOT_MARKER_COLORS.get(lot, "#555555")
        ax.axvline(t, color=color, linestyle="--", linewidth=1.0, alpha=0.8, zorder=3)
        y = ymin + (ymax - ymin) * (0.96 - 0.10 * level)
        ax.text(
            t,
            y,
            f"{lot} left campus",
            rotation=90,
            va="top",
            ha="right",
            fontsize=7,
            color=color,
            clip_on=True,
            zorder=4,
        )


def congestion_note_for_clearances(
    output_dir: Path, clearance_by_lot: dict | None
) -> str | None:
    """Short caption linking late lot clearances to hottest aisle congestion, if data exists."""
    summary_path = output_dir / "congestion_heatmap" / "congestion_summary.json"
    if not summary_path.is_file():
        return None
    try:
        summary = json.loads(summary_path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError):
        return None

    top = summary.get("top_by_peak") or []
    hot_aisles = []
    for entry in top:
        road = str(entry.get("road") or "")
        if "aisles" not in road.lower():
            continue
        # "P6 aisles" -> P6
        lot = road.split()[0] if road else ""
        if lot in LOT_ORDER and lot not in hot_aisles:
            hot_aisles.append(lot)
        if len(hot_aisles) >= 3:
            break

    items = _lot_clearance_items(clearance_by_lot)
    late = [lot for lot, _ in items[-3:]] if items else []
    if not hot_aisles and not late:
        return None

    parts = []
    if hot_aisles:
        parts.append(f"hottest aisles: {', '.join(hot_aisles)}")
    if late:
        parts.append(f"latest fully-left lots: {', '.join(late)}")
    overlap = [lot for lot in late if lot in hot_aisles]
    if overlap:
        parts.append(f"overlap (hot + late): {', '.join(overlap)}")
    note = "Congestion vs lot campus-exit - " + "; ".join(parts)
    print(note)
    return note


LOT_COORDS = load_lot_coords()

# Brewer Park dest (Brewer Way east of Bronson; graph node 9900700001).
# Destination only — NOT a primary campus leave gate on exit_usage charts.
BREWER_PARK_DEST = (45.387983, -75.690183)
# Bronson Avenue @ Brewer Way (OSM node 901063900) — finish landmark only.
BRONSON_BREWER_WAY = (45.3869999, -75.6914227)

# Four primary campus leave gates (lat, lon).
# Stadium Way = Bronson Ave & Stadium Way (OSM node 7010355503).
# Raven Rd emergency = Raven→Bronson join (OSM node 7964597061 on way 1025157958).
STADIUM_WAY_EXIT_NAME = "Stadium Way"
STADIUM_WAY_EXIT_POINT = (45.3881843, -75.6925574)
EMERGENCY_EXIT_NAME = "Raven Rd emergency"
# Raven→Bronson join (OSM 7964597061). Raven attach 1435781052 is on the P3 curb —
# do not use it for gate proximity (false-positives ordinary Raven traffic).
EMERGENCY_EXIT_POINT = (45.3850997, -75.6901823)
EMERGENCY_CORRIDOR_TOL_M = 100.0
STADIUM_CORRIDOR_TOL_M = 80.0

CAMPUS_EXIT_POINTS = {
    "Colonel By": (45.3792575, -75.7004525),
    "Bronson Ave & University Dr": (45.3896198, -75.694494),
    STADIUM_WAY_EXIT_NAME: STADIUM_WAY_EXIT_POINT,
    EMERGENCY_EXIT_NAME: EMERGENCY_EXIT_POINT,
}
CAMPUS_EXIT_ORDER = list(CAMPUS_EXIT_POINTS.keys()) + ["other"]
# Stadium Way @ Bronson sits inside the core footprint on Bronson — cars bound for
# University Dr pass it. Do not treat proximity alone as a campus leave.
AT_GATE_EXIT_NAMES = frozenset(
    name for name in CAMPUS_EXIT_POINTS if name != STADIUM_WAY_EXIT_NAME
)

# Final destinations including off-campus landmarks (finish classification only).
# Brewer Park / Brewer Way are destinations, not primary leave-campus gates.
EXIT_POINTS = {
    **CAMPUS_EXIT_POINTS,
    "Bronson Ave & Brewer Way": BRONSON_BREWER_WAY,
    "Brewer Park": BREWER_PARK_DEST,
    "SW evac (Meadowlands)": (45.3675, -75.7040),
    "NE evac box": (45.3925, -75.6875),
}
EXIT_ORDER = list(EXIT_POINTS.keys()) + ["other"]
EXIT_TOL_M = 120.0


def normalize_scenario_id(scenario_id: str | None) -> str | None:
    if scenario_id is None:
        return None
    s = str(scenario_id).strip()
    if s.isdigit():
        return s.zfill(2)
    m = re.fullmatch(r"scenario_(\d+)", s, flags=re.IGNORECASE)
    return m.group(1).zfill(2) if m else s


def scenario_chart_title(base: str, scenario_id: str | None) -> str:
    """Append scenario tag so charts are identifiable, e.g. '… — scenario_01'."""
    sid = normalize_scenario_id(scenario_id)
    if sid:
        return f"{base} — scenario_{sid}"
    return base


def campus_exit_points_for_scenario(
    scenario_id: str | None = None,
) -> dict[str, tuple[float, float]]:
    """Named gates used for leave-campus detection.

    Scenario 08: Bronson & University Dr approach is closed and the Raven emergency
    link is absent — do not attribute leaves to those gates (cars on Bronson toward
    Brewer would otherwise look like they used the closed UD exit).

    Scenario 09: same Bronson UD closure as 08, but Raven→Bronson emergency is open —
    keep the emergency gate; still drop Bronson & University Dr.
    """
    gates = dict(CAMPUS_EXIT_POINTS)
    sid = normalize_scenario_id(scenario_id)
    if sid == "08":
        gates.pop("Bronson Ave & University Dr", None)
        gates.pop(EMERGENCY_EXIT_NAME, None)
    elif sid == "09":
        gates.pop("Bronson Ave & University Dr", None)
    return gates


def scenario_id_from_path(path: Path) -> str | None:
    for part in reversed(path.parts):
        m = re.fullmatch(r"scenario_(\d+)", part)
        if m:
            return m.group(1)
    return None


def resolve_scenario_paths(csv_path: Path) -> tuple[Path, Path]:
    """Pick schedule + config for results/scenario_XX/ or opt_candidates/<name>/ runs.

    Prefer ``_run_config.json`` next to the agent outputs when present — that is
    the config MARS actually simulated (endPoint / horizon), not a later rewrite
    of ``configs/opt_candidates/``.
    """
    sid = scenario_id_from_path(csv_path)
    if sid:
        schedule = SCHEDULES_DIR / f"scenario_{sid}_schedule.csv"
        config = CONFIGS_DIR / f"config_scenario_{sid}.json"
        if schedule.is_file() and config.is_file():
            return schedule, config
    # Optimizer candidates: results/opt_candidates/<name>/
    parts = csv_path.resolve().parts
    if "opt_candidates" in parts:
        idx = parts.index("opt_candidates")
        if idx + 1 < len(parts):
            name = parts[idx + 1]
            # Agent CSV lives in results/opt_candidates/<name>/
            run_dir = csv_path.resolve().parent
            if run_dir.name != name:
                # trips path nested deeper — walk up to the candidate folder
                for parent in csv_path.resolve().parents:
                    if parent.name == name and parent.parent.name == "opt_candidates":
                        run_dir = parent
                        break
            schedule = SCHEDULES_DIR / "opt_candidates" / f"{name}_schedule.csv"
            run_cfg = run_dir / "_run_config.json"
            config = (
                run_cfg
                if run_cfg.is_file()
                else CONFIGS_DIR / "opt_candidates" / f"config_{name}.json"
            )
            if schedule.is_file() and config.is_file():
                return schedule, config
    # Any results dir with a sibling _run_config.json (generic MARS output folder).
    run_cfg = csv_path.parent / "_run_config.json"
    if run_cfg.is_file():
        return DEFAULT_SCHEDULE, run_cfg
    return DEFAULT_SCHEDULE, DEFAULT_CONFIG


def read_sim_times(config_path: Path) -> tuple[datetime, int | None]:
    """Return simulation start time (UTC) and optional end offset in seconds."""
    if not config_path.is_file():
        start = datetime(2021, 10, 11, 6, 0, 0, tzinfo=timezone.utc)
        return start, None
    cfg = json.loads(config_path.read_text(encoding="utf-8"))
    # Config times are naive but MARS trip timestamps are UTC unix — treat as UTC.
    start = datetime.fromisoformat(cfg["globals"]["startPoint"]).replace(tzinfo=timezone.utc)
    end = datetime.fromisoformat(cfg["globals"]["endPoint"]).replace(tzinfo=timezone.utc)
    return start, int((end - start).total_seconds())


def parse_clock(value: str) -> datetime:
    for fmt in ("%H:%M:%S", "%H:%M"):
        try:
            return datetime.strptime(value.strip(), fmt)
        except ValueError:
            continue
    raise ValueError(f"Bad time: {value}")


def spawns_for_row(row) -> int:
    """Count intended schedule spawns for a row.

    One-shot rows use spawningIntervalInMinutes <= 0 (Mars fires only when
    CurrentTimePoint == startTime, minute precision for clock-only CSV) or
    startTime == endTime. Dump schedules use 06:01 (not 06:00) so the first
    PreTick after startPoint can match; seconds in HH:MM:SS are stripped.
    Positive intervals are counted on inclusive [start, end] minute steps for
    *expected* deploy totals (intent).
    """
    start = parse_clock(row["startTime"])
    end = parse_clock(row["endTime"])
    interval = float(row["spawningIntervalInMinutes"])
    amount = int(row["spawningAmount"])
    if interval <= 0 or start == end:
        return amount
    total = 0
    t = start
    step = timedelta(minutes=interval)
    # Inclusive end matches Mars.Components 5.3.1 ScheduleForTime modulo path.
    while t <= end:
        total += amount
        t += step
    return total


def lot_for_row(row) -> str:
    """Map a schedule row to a lot via exact, then nearest interior spawn."""
    slat = float(row["startLat"])
    slon = float(row["startLon"])
    for lot, (lat, lon) in LOT_COORDS.items():
        if abs(slat - lat) < 1e-4 and abs(slon - lon) < 1e-4:
            return lot
    if not LOT_COORDS:
        return "unknown"
    best = "unknown"
    best_d = LOT_MATCH_TOL_M
    for lot, (lat, lon) in LOT_COORDS.items():
        d = math.hypot((slat - lat) * 111_000, (slon - lon) * 85_000)
        if d < best_d:
            best_d = d
            best = lot
    return best


def read_schedule(path: Path):
    rows = []
    with path.open(encoding="utf-8-sig") as f:
        for row in csv.DictReader(f):
            rows.append(row)
    per_lot = {lot: 0 for lot in LOT_ORDER}
    total = 0
    for row in rows:
        n = spawns_for_row(row)
        total += n
        lot = lot_for_row(row)
        if lot in per_lot:
            per_lot[lot] += n
    return total, per_lot


def spawn_event_times(schedule_path: Path, sim_start: datetime) -> list[int]:
    """Seconds from sim start when each scheduled car is released."""
    events: list[int] = []
    with schedule_path.open(encoding="utf-8-sig") as f:
        for row in csv.DictReader(f):
            start = parse_clock(row["startTime"])
            end = parse_clock(row["endTime"])
            interval = float(row["spawningIntervalInMinutes"])
            amount = int(row["spawningAmount"])
            one_shot = interval <= 0 or start == end
            t = start
            step = None if one_shot else timedelta(minutes=interval)
            while True:
                spawn_dt = datetime.combine(sim_start.date(), t.time(), tzinfo=timezone.utc)
                sec = int((spawn_dt - sim_start).total_seconds())
                for _ in range(amount):
                    events.append(sec)
                if one_shot or step is None:
                    break
                t += step
                if t > end:
                    break
    events.sort()
    return events


def resolve_path(arg: str | None, default: Path) -> Path:
    """CLI paths are relative to the project root, not the shell cwd."""
    if arg is None:
        return default
    p = Path(arg)
    return p if p.is_absolute() else ROOT / p


def count_features_fast(path: Path) -> int:
    """Count trip features without parsing the full geojson."""
    pat = re.compile(rb'"type"\s*:\s*"Feature"')
    count = 0
    overlap = b""
    with path.open("rb") as f:
        while block := f.read(4 * 1024 * 1024):
            data = overlap + block
            count += len(pat.findall(data))
            overlap = data[-64:]
    if overlap:
        count += len(pat.findall(overlap))
    return count


def dist_m(lat1: float, lon1: float, lat2: float, lon2: float) -> float:
    return math.hypot((lat1 - lat2) * 111_000, (lon1 - lon2) * 85_000)


def load_campus_bbox(
    margin_deg: float = CAMPUS_BBOX_MARGIN_DEG,
) -> tuple[float, float, float, float]:
    """Return (min_lat, max_lat, min_lon, max_lon) for campus-interior detection.

    Uses a fixed core footprint. Deriving this from campus_drive_graph.geojson is
    wrong after the expanded OSM download — that AOI includes off-campus destinations.
    """
    min_lat, max_lat, min_lon, max_lon = CAMPUS_CORE_BBOX
    return (
        min_lat + margin_deg,
        max_lat - margin_deg,
        min_lon + margin_deg,
        max_lon - margin_deg,
    )


CAMPUS_BBOX = load_campus_bbox()


def inside_campus(lat: float, lon: float) -> bool:
    min_lat, max_lat, min_lon, max_lon = CAMPUS_BBOX
    return min_lat <= lat <= max_lat and min_lon <= lon <= max_lon


def near_campus_exit(
    lat: float,
    lon: float,
    exit_points: dict[str, tuple[float, float]] | None = None,
    max_dist_m: float = EXIT_TOL_M,
) -> bool:
    points = exit_points if exit_points is not None else CAMPUS_EXIT_POINTS
    return any(
        name in AT_GATE_EXIT_NAMES
        and dist_m(lat, lon, elat, elon) <= max_dist_m
        for name, (elat, elon) in points.items()
    )


def nearest_campus_exit(
    lat: float,
    lon: float,
    exit_points: dict[str, tuple[float, float]] | None = None,
    max_dist_m: float = EXIT_TOL_M,
) -> str:
    """Classify by nearest campus gate for this scenario's exit set."""
    points = exit_points if exit_points is not None else CAMPUS_EXIT_POINTS
    best, best_d = "other", max_dist_m
    for name, (elat, elon) in points.items():
        d = dist_m(lat, lon, elat, elon)
        if d < best_d:
            best_d, best = d, name
    return best


def _trajectory_near_point(
    coords: list[tuple[float, float, int]],
    point: tuple[float, float],
    max_dist_m: float,
    upto_idx: int | None = None,
) -> bool:
    end = len(coords) if upto_idx is None else min(upto_idx + 1, len(coords))
    plat, plon = point
    for i in range(end):
        lat, lon, _ = coords[i]
        if dist_m(lat, lon, plat, plon) <= max_dist_m:
            return True
    return False


def classify_campus_exit(
    lat: float,
    lon: float,
    *,
    coords: list[tuple[float, float, int]] | None = None,
    leave_idx: int | None = None,
    end_lat: float | None = None,
    end_lon: float | None = None,
    exit_points: dict[str, tuple[float, float]] | None = None,
    left_campus: bool = True,
) -> str:
    """Attribute leave-campus to one of the four primary gates.

    Priority:
      1. Emergency corridor — only when EMERGENCY_EXIT_NAME is in ``points``
         (absent for scenario 08): trajectory near Raven→Bronson join, or
         finish at Brewer Park while leave is nearer that join than Stadium Way.
      2. Stadium Way — only if the trip used the Stadium Way @ Bronson corridor
         (passed / left near that junction); not merely nearest-after any NE leave.
      3. Nearest remaining primary gate (Colonel By / Bronson & University Dr /
         Raven Rd emergency when those gates are in ``points``).

    Scenario 08 pops Bronson UD + emergency from ``points``; Brewer-bound traffic
    near Bronson then classifies as Stadium Way (or Colonel By), never UD/emergency.
    """
    points = exit_points if exit_points is not None else CAMPUS_EXIT_POINTS
    trail = coords or []
    idx = leave_idx if leave_idx is not None else (len(trail) - 1 if trail else None)

    use_emergency = EMERGENCY_EXIT_NAME in points
    use_ud = "Bronson Ave & University Dr" in points
    d_em = (
        dist_m(lat, lon, *EMERGENCY_EXIT_POINT) if use_emergency else float("inf")
    )
    d_st = dist_m(lat, lon, *STADIUM_WAY_EXIT_POINT)
    d_ud = (
        dist_m(lat, lon, *points["Bronson Ave & University Dr"])
        if use_ud
        else float("inf")
    )

    # Bronson join only (not Raven attach): attach sits on the P3 curb.
    passed_emergency = bool(
        use_emergency
        and trail
        and idx is not None
        and _trajectory_near_point(
            trail, EMERGENCY_EXIT_POINT, EMERGENCY_CORRIDOR_TOL_M, upto_idx=idx
        )
    )
    if use_emergency and (passed_emergency or d_em <= EXIT_TOL_M):
        return EMERGENCY_EXIT_NAME

    finish_brewer = (
        end_lat is not None
        and end_lon is not None
        and dist_m(end_lat, end_lon, *BREWER_PARK_DEST) <= EXIT_TOL_M
    )
    if use_emergency and finish_brewer and d_em <= d_st:
        return EMERGENCY_EXIT_NAME

    passed_stadium = bool(
        trail
        and idx is not None
        and _trajectory_near_point(
            trail, STADIUM_WAY_EXIT_POINT, STADIUM_CORRIDOR_TOL_M, upto_idx=idx
        )
    )
    if (passed_stadium or d_st <= EXIT_TOL_M) and d_st <= d_ud:
        return STADIUM_WAY_EXIT_NAME

    # Stadium Way is corridor-gated; omit from bare nearest so NE leavers that
    # never used Stadium Way map to Bronson & University Dr (or Colonel By).
    candidates = {
        name: coord
        for name, coord in points.items()
        if name != STADIUM_WAY_EXIT_NAME
    }
    if use_emergency and d_em > EXIT_TOL_M and not passed_emergency:
        candidates.pop(EMERGENCY_EXIT_NAME, None)

    max_dist = float("inf") if left_campus else EXIT_TOL_M
    return nearest_campus_exit(lat, lon, candidates, max_dist_m=max_dist)


def detect_campus_exit(
    coords: list[tuple[float, float, int]],
    start_lat: float,
    start_lon: float,
    sim_start_unix: int,
    scenario_id: str | None = None,
) -> dict | None:
    """First moment the vehicle leaves the university (not destination arrival).

    Exit = first trajectory point that either:
      - leaves the core campus footprint (east edge on Bronson; Brewer Park outside), or
      - reaches a named campus gate (Colonel By / Bronson & University Dr /
        Stadium Way / Raven Rd emergency).

    Gate attribution uses corridor proximity (emergency Raven→Bronson join and
    Stadium Way @ Bronson) then nearest of the four primary gates. Brewer Park is
    a finish destination only — leave-campus labels map to one of the four gates.
    """
    if not coords:
        return None
    gates = campus_exit_points_for_scenario(scenario_id)
    end_lat, end_lon = coords[-1][0], coords[-1][1]
    for i, (lat, lon, ts) in enumerate(coords):
        if dist_m(lat, lon, start_lat, start_lon) < SPAWN_NOISE_M:
            continue
        left_campus = not inside_campus(lat, lon)
        at_gate = near_campus_exit(lat, lon, gates)
        if not left_campus and not at_gate:
            continue
        exit_name = classify_campus_exit(
            lat,
            lon,
            coords=coords,
            leave_idx=i,
            end_lat=end_lat,
            end_lon=end_lon,
            exit_points=gates,
            left_campus=left_campus,
        )
        return {
            "campus_exit": exit_name,
            "campus_exit_from_t0_s": ts - sim_start_unix,
            "campus_exit_travel_s": ts - coords[0][2],
            "campus_exit_lat": lat,
            "campus_exit_lon": lon,
            "campus_exit_detected": True,
        }
    return None


def nearest_lot(lat: float, lon: float) -> str:
    """Assign a trip to the nearest parking lot by spawn coordinate."""
    if not LOT_COORDS:
        return "unknown"
    best = "unknown"
    best_d = float("inf")
    for lot, (llat, llon) in LOT_COORDS.items():
        d = math.hypot((lat - llat) * 111_000, (lon - llon) * 85_000)
        if d < best_d:
            best_d = d
            best = lot
    return best


def nearest_exit(lat: float, lon: float, max_dist_m: float = EXIT_TOL_M) -> str:
    """Classify trip finish by nearest named destination (includes evac boxes)."""
    best, best_d = "other", max_dist_m
    for name, (elat, elon) in EXIT_POINTS.items():
        d = dist_m(lat, lon, elat, elon)
        if d < best_d:
            best_d, best = d, name
    return best


def extract_trip_records_stream(
    path: Path,
    sim_start_unix: int,
    scenario_id: str | None = None,
) -> list[dict]:
    """Stream trips geojson: campus leave time vs destination finish time.

    Coordinates are [lon, lat, z, unix_timestamp].
    `campus_exit_*` = left the university.
    `exit` / `exit_from_t0_s` / `travel_s` = reached scheduled destination.
    """
    records: list[dict] = []
    buf = ""
    feat_re = re.compile(r'"type"\s*:\s*"Feature"')
    coord_re = re.compile(
        r"\[(-?\d+(?:\.\d+)?),\s*(-?\d+(?:\.\d+)?),\s*(-?\d+(?:\.\d+)?),\s*(\d{10})\]"
    )
    missing_campus = 0
    sid = normalize_scenario_id(scenario_id) or scenario_id_from_path(path)
    with path.open(encoding="utf-8", errors="ignore") as f:
        while True:
            chunk = f.read(8 * 1024 * 1024)
            if not chunk:
                break
            buf += chunk
            while True:
                m = feat_re.search(buf)
                if not m:
                    buf = buf[-256:]
                    break
                props = buf.find('"properties"', m.end())
                if props < 0:
                    break
                segment = buf[m.start() : props]
                coords_raw = coord_re.findall(segment)
                if coords_raw:
                    parsed = [
                        (float(lat), float(lon), int(ts))
                        for lon, lat, _, ts in coords_raw
                    ]
                    start_lat, start_lon, start_u = parsed[0]
                    end_lat, end_lon, end_u = parsed[-1]
                    travel_s = end_u - start_u
                    finish_from_t0_s = end_u - sim_start_unix
                    if travel_s >= 0 and finish_from_t0_s >= 0:
                        campus = detect_campus_exit(
                            parsed,
                            start_lat,
                            start_lon,
                            sim_start_unix,
                            scenario_id=sid,
                        )
                        if campus is None:
                            # Do NOT fall back to destination time — that is not a campus exit.
                            missing_campus += 1
                            campus = {
                                "campus_exit": "other",
                                "campus_exit_from_t0_s": None,
                                "campus_exit_travel_s": None,
                                "campus_exit_lat": None,
                                "campus_exit_lon": None,
                                "campus_exit_detected": False,
                            }
                        records.append(
                            {
                                "travel_s": travel_s,
                                "exit_from_t0_s": finish_from_t0_s,
                                "lot": nearest_lot(start_lat, start_lon),
                                "exit": nearest_exit(end_lat, end_lon),
                                **campus,
                            }
                        )
                buf = buf[props:]
    if missing_campus:
        print(
            f"WARNING: {missing_campus} trips never left campus footprint/gates "
            f"(campus exit left unset; destination time not used as exit)."
        )
    return records


def load_trips_summary(
    path: Path,
    sim_start: datetime,
    scenario_id: str | None = None,
) -> tuple[int, list[int], list[dict]]:
    """Trip count, campus-leave times (sim s), and per-trip travel records."""
    if not path.is_file():
        print(f"WARNING: trips file not found: {path}")
        return 0, [], []

    size = path.stat().st_size
    fast_count = count_features_fast(path)
    sim_start_unix = int(sim_start.timestamp())
    sid = normalize_scenario_id(scenario_id) or scenario_id_from_path(path)
    records = extract_trip_records_stream(path, sim_start_unix, scenario_id=sid)
    depart_times = sorted(
        int(r["campus_exit_from_t0_s"])
        for r in records
        if r.get("campus_exit_detected") and r.get("campus_exit_from_t0_s") is not None
    )
    trip_count = max(fast_count, len(records))

    print(
        f"Trips geojson: {size:,} bytes | feature scan={fast_count} | "
        f"trip records={len(records)} | campus exits={len(depart_times)}"
    )

    if size > 100_000 and trip_count == 0:
        head = path.read_bytes()[:400].decode("utf-8", errors="replace")
        print(f"ERROR: geojson is non-empty but 0 trips counted. Head: {head[:200]!r}")

    return trip_count, depart_times, records


def cumulative_curve(event_times: list[int], max_t: int) -> list[tuple[int, int]]:
    if max_t < 0:
        return []
    if not event_times:
        return [(t, 0) for t in range(max_t + 1)]
    curve: list[tuple[int, int]] = []
    idx = 0
    n = len(event_times)
    for t in range(max_t + 1):
        while idx < n and event_times[idx] <= t:
            idx += 1
        curve.append((t, idx))
    return curve


def analyze_csv(path: Path):
    completed_ids = set()
    cars_per_step = Counter()
    goal_true_rows = 0
    csv_rows = 0

    with path.open(encoding="utf-8-sig") as f:
        for row in csv.DictReader(f):
            csv_rows += 1
            agent_id = row.get("ID", "")
            if row.get("GoalReached", "").lower() == "true":
                goal_true_rows += 1
                if agent_id:
                    completed_ids.add(agent_id)
            if row.get("CurrentlyCarDriving", "").lower() != "true":
                continue
            if row.get("GoalReached", "").lower() == "true":
                continue
            cars_per_step[int(row["Step"])] += 1

    curve = sorted(cars_per_step.items())
    return len(completed_ids), goal_true_rows, curve, csv_rows


def trip_time_stats(records: list[dict], per_lot: dict | None = None) -> dict:
    """Mean/median campus-leave travel time and leave time (from t=0).

    Finish (destination) stats and finish_exit_usage only include trips that
    left campus. Geojson often still has a Feature for cars stuck inside the
    footprint at endPoint — those must not look like successful finishes.

    clearance_by_lot_s is set only when *all* cars from that lot have a campus
    exit (no stuck cars; completed exits >= schedule expected, or all trip
    records from that lot exited). Value is max campus-leave time among that
    lot's cars — not lot departure, and not last successful exit while others
    remain stuck.
    """
    empty = {
        "n_trips": 0,
        "mean_trip_s": None,
        "median_trip_s": None,
        "mean_exit_from_t0_s": None,
        "median_exit_from_t0_s": None,
        "mean_campus_exit_travel_s": None,
        "median_campus_exit_travel_s": None,
        "mean_campus_exit_from_t0_s": None,
        "median_campus_exit_from_t0_s": None,
        "mean_finish_travel_s": None,
        "median_finish_travel_s": None,
        "mean_finish_from_t0_s": None,
        "median_finish_from_t0_s": None,
        "clearance_by_lot_s": {lot: None for lot in LOT_ORDER},
        "completed_by_lot": {lot: 0 for lot in LOT_ORDER},
        "remaining_by_lot": {lot: 0 for lot in LOT_ORDER},
        "exit_usage": {name: 0 for name in CAMPUS_EXIT_ORDER},
        "finish_exit_usage": {name: 0 for name in EXIT_ORDER},
        "travel_times": [],
        "records": [],
        "campus_exits": 0,
        "remaining_on_campus": 0,
    }
    if not records:
        return empty

    left = [
        r
        for r in records
        if r.get("campus_exit_detected") and r.get("campus_exit_from_t0_s") is not None
    ]
    stuck = [
        r
        for r in records
        if not (r.get("campus_exit_detected") and r.get("campus_exit_from_t0_s") is not None)
    ]
    campus_travel = [int(r["campus_exit_travel_s"]) for r in left]
    campus_from_t0 = [int(r["campus_exit_from_t0_s"]) for r in left]
    # Destination arrival only for cars that actually left campus.
    finish_travel = [r["travel_s"] for r in left]
    finish_from_t0 = [r["exit_from_t0_s"] for r in left]
    last_leave: dict[str, int] = {}
    completed_by_lot = Counter()
    remaining_by_lot = Counter()
    exit_usage = Counter()
    finish_exit_usage = Counter()
    for r in left:
        lot = r["lot"]
        if lot in LOT_ORDER:
            last_leave[lot] = max(last_leave.get(lot, 0), int(r["campus_exit_from_t0_s"]))
            completed_by_lot[lot] += 1
        exit_name = r.get("campus_exit", "other")
        if exit_name not in CAMPUS_EXIT_ORDER:
            exit_name = "other"
        exit_usage[exit_name] += 1
        finish_name = r.get("exit", "other")
        if finish_name not in EXIT_ORDER:
            finish_name = "other"
        finish_exit_usage[finish_name] += 1
    for r in stuck:
        lot = r.get("lot")
        if lot in LOT_ORDER:
            remaining_by_lot[lot] += 1

    # Only report clearance when the lot fully left campus.
    clearance_by_lot_s: dict[str, int | None] = {}
    for lot in LOT_ORDER:
        done = int(completed_by_lot.get(lot, 0))
        rem = int(remaining_by_lot.get(lot, 0))
        expected_lot = int((per_lot or {}).get(lot, 0))
        records_from_lot = done + rem
        need = expected_lot if expected_lot > 0 else records_from_lot
        fully_left = (
            rem == 0
            and done > 0
            and lot in last_leave
            and (need <= 0 or done >= need)
        )
        clearance_by_lot_s[lot] = last_leave[lot] if fully_left else None

    out = {
        "n_trips": len(left),
        "campus_exits": len(left),
        "remaining_on_campus": len(stuck),
        "mean_trip_s": round(mean(campus_travel), 2) if campus_travel else None,
        "median_trip_s": round(median(campus_travel), 2) if campus_travel else None,
        "mean_exit_from_t0_s": round(mean(campus_from_t0), 2) if campus_from_t0 else None,
        "median_exit_from_t0_s": round(median(campus_from_t0), 2) if campus_from_t0 else None,
        "mean_campus_exit_travel_s": round(mean(campus_travel), 2) if campus_travel else None,
        "median_campus_exit_travel_s": round(median(campus_travel), 2) if campus_travel else None,
        "mean_campus_exit_from_t0_s": round(mean(campus_from_t0), 2) if campus_from_t0 else None,
        "median_campus_exit_from_t0_s": round(median(campus_from_t0), 2) if campus_from_t0 else None,
        "mean_finish_travel_s": round(mean(finish_travel), 2) if finish_travel else None,
        "median_finish_travel_s": round(median(finish_travel), 2) if finish_travel else None,
        "mean_finish_from_t0_s": round(mean(finish_from_t0), 2) if finish_from_t0 else None,
        "median_finish_from_t0_s": round(median(finish_from_t0), 2) if finish_from_t0 else None,
        "clearance_by_lot_s": clearance_by_lot_s,
        "completed_by_lot": {lot: int(completed_by_lot.get(lot, 0)) for lot in LOT_ORDER},
        "remaining_by_lot": {lot: int(remaining_by_lot.get(lot, 0)) for lot in LOT_ORDER},
        "exit_usage": {name: int(exit_usage.get(name, 0)) for name in CAMPUS_EXIT_ORDER},
        "finish_exit_usage": {
            name: int(finish_exit_usage.get(name, 0)) for name in EXIT_ORDER
        },
        "travel_times": campus_travel,
        "records": records,
    }
    return out


def plot_lot_completion_and_exits(
    output_dir: Path,
    per_lot: dict,
    stats: dict,
    scenario_id: str | None = None,
) -> None:
    """Write completion_by_lot.png and exit_usage.png."""
    if not stats.get("n_trips"):
        print("No trip records — skipping lot/exit charts.")
        return

    scheduled = [int(per_lot.get(lot, 0)) for lot in LOT_ORDER]
    completed = [int((stats.get("completed_by_lot") or {}).get(lot, 0)) for lot in LOT_ORDER]
    x = list(range(len(LOT_ORDER)))
    width = 0.38

    plt.figure(figsize=(8, 4.5))
    b1 = plt.bar([i - width / 2 for i in x], scheduled, width, color="#4c72b0", label="Scheduled")
    b2 = plt.bar([i + width / 2 for i in x], completed, width, color="#c44e52", label="Completed")
    plt.xticks(x, LOT_ORDER)
    plt.ylabel("Vehicles")
    plt.xlabel("Parking lot")
    plt.title(scenario_chart_title("Per-lot completion (scheduled vs campus exits detected)", scenario_id))
    plt.legend()
    for bars in (b1, b2):
        for bar in bars:
            h = bar.get_height()
            if h:
                plt.text(
                    bar.get_x() + bar.get_width() / 2,
                    h,
                    f"{int(h)}",
                    ha="center",
                    va="bottom",
                    fontsize=7,
                )
    plt.tight_layout()
    plt.savefig(output_dir / "completion_by_lot.png", dpi=200)
    plt.close()

    usage = stats.get("exit_usage") or {}
    # Always show the four primary gates; "other" only when nonzero.
    labels = [n for n in CAMPUS_EXIT_ORDER if n != "other"]
    if usage.get("other", 0):
        labels.append("other")
    values = [int(usage.get(name, 0)) for name in labels]
    short = {
        "Colonel By": "Colonel By",
        "Bronson Ave & University Dr": "Bronson\n(University Dr)",
        STADIUM_WAY_EXIT_NAME: "Stadium Way",
        EMERGENCY_EXIT_NAME: "Raven Rd\nemergency",
        "other": "Other / unmatched",
    }
    plt.figure(figsize=(8, 4.5))
    bars = plt.bar([short.get(n, n) for n in labels], values, color="#8172b2")
    plt.ylabel("Campus exits")
    plt.title(scenario_chart_title("Campus exit usage", scenario_id))
    for bar, val in zip(bars, values):
        plt.text(
            bar.get_x() + bar.get_width() / 2,
            bar.get_height(),
            f"{val}",
            ha="center",
            va="bottom",
            fontsize=8,
        )
    plt.tight_layout()
    plt.savefig(output_dir / "exit_usage.png", dpi=200)
    plt.close()


def plot_trip_time_charts(
    output_dir: Path,
    stats: dict,
    scenario_id: str | None = None,
) -> None:
    """Write trip_time_stats.png, trip_time_hist.png, clearance_by_lot.png."""
    if not stats.get("n_trips"):
        print("No trip records — skipping travel-time charts.")
        return

    travel = stats["travel_times"]
    values = [
        stats["mean_trip_s"],
        stats["median_trip_s"],
        stats["mean_exit_from_t0_s"],
        stats["median_exit_from_t0_s"],
    ]
    colors = ["#4c72b0", "#4c72b0", "#55a868", "#55a868"]
    bar_labels = [
        "Mean\ndrive to exit",
        "Median\ndrive to exit",
        "Mean\ncampus exit",
        "Median\ncampus exit",
    ]

    from matplotlib.patches import Patch

    plt.figure(figsize=(8, 4.8))
    bars = plt.bar(bar_labels, values, color=colors)
    plt.ylabel("Seconds")
    plt.title(scenario_chart_title("Travel-time summary", scenario_id))
    plt.legend(
        handles=[
            Patch(
                facecolor="#4c72b0",
                label="Blue = drive time (spawn → leave university)",
            ),
            Patch(
                facecolor="#55a868",
                label="Green = leave-campus time (sim t=0 → leave university)",
            ),
        ],
        loc="upper left",
        fontsize=8,
    )
    for bar, val in zip(bars, values):
        plt.text(
            bar.get_x() + bar.get_width() / 2,
            bar.get_height(),
            f"{val:.0f}s",
            ha="center",
            va="bottom",
            fontsize=8,
        )
    plt.tight_layout()
    plt.savefig(output_dir / "trip_time_stats.png", dpi=200)
    plt.close()

    plt.figure(figsize=(8, 4.5))
    plt.hist(travel, bins=40, color="#4c72b0", edgecolor="white", linewidth=0.4)
    plt.axvline(stats["mean_trip_s"], color="#c44e52", linestyle="--", linewidth=1.5, label=f"Mean {stats['mean_trip_s']:.0f}s")
    plt.axvline(stats["median_trip_s"], color="#8172b2", linestyle="-", linewidth=1.5, label=f"Median {stats['median_trip_s']:.0f}s")
    plt.xlabel("Trip duration (s) — spawn to leave university")
    plt.ylabel("Trips")
    plt.title(
        scenario_chart_title(
            "Travel-time distribution (to leave campus, not destination)",
            scenario_id,
        )
    )
    plt.legend()
    plt.grid(True, axis="y", alpha=0.3)
    plt.tight_layout()
    plt.savefig(output_dir / "trip_time_hist.png", dpi=200)
    plt.close()

    lots = LOT_ORDER
    clearance = [stats["clearance_by_lot_s"].get(lot) or 0 for lot in lots]
    plt.figure(figsize=(7, 4.2))
    bars = plt.bar(lots, clearance, color="#dd8452")
    plt.ylabel("Last campus-exit time (s from t=0)")
    plt.xlabel("Parking lot")
    plt.title(
        scenario_chart_title(
            "Lot fully left campus (last campus exit; blank if stuck remain)",
            scenario_id,
        )
    )
    for bar, val in zip(bars, clearance):
        if val:
            plt.text(
                bar.get_x() + bar.get_width() / 2,
                bar.get_height(),
                f"{val}",
                ha="center",
                va="bottom",
                fontsize=8,
            )
    plt.tight_layout()
    plt.savefig(output_dir / "clearance_by_lot.png", dpi=200)
    plt.close()


def peak_on_campus(on_campus_curve) -> int | None:
    if not on_campus_curve:
        return None
    return int(max(c for _, c in on_campus_curve))


def build_metrics_payload(
    *,
    scenario_id: str | None,
    expected: int,
    per_lot: dict,
    completed_csv: int,
    goal_rows: int,
    trips: int,
    csv_rows: int,
    evac_end_s: int,
    trip_stats: dict,
    on_campus_curve,
    geojson_features: int | None = None,
    remaining_on_campus: int | None = None,
    config_end_s: int | None = None,
) -> dict:
    """Stable metrics schema for later cross-framework joins (join on scenario_id).

    ``completed_trips`` / ``completion_rate_pct`` are campus *leaves*, not geojson
    Feature count. MARS still emits a Feature for cars stuck at endPoint.
    """
    campus_exits = int(trip_stats.get("campus_exits") or trips)
    remaining = int(
        remaining_on_campus
        if remaining_on_campus is not None
        else trip_stats.get("remaining_on_campus") or max(0, BASELINE_TARGET - campus_exits)
    )
    features = int(geojson_features if geojson_features is not None else campus_exits)
    # Never report 100% while anyone remains on campus (schedule N0 / exits).
    denom = max(int(expected) or 0, campus_exits + remaining, 1)
    rate = round((campus_exits / denom * 100.0), 2)
    if remaining > 0 and rate >= 100.0:
        rate = round((campus_exits / (campus_exits + remaining) * 100.0), 2) if (
            campus_exits + remaining
        ) else 0.0
    clearance = trip_stats.get("clearance_by_lot_s") or {}
    campus_cleared = remaining == 0 and campus_exits > 0
    warning = None
    if remaining > 0:
        t_label = f"t={config_end_s}s" if config_end_s else "endPoint"
        warning = (
            f"INCOMPLETE — {remaining} cars still on campus at {t_label} "
            f"(geojson_features={features}, campus_exits={campus_exits}). "
            "Treat as failed/jammed candidate unless a longer endPoint re-sim clears."
        )
    return {
        "schema_version": 2,
        "framework": "mars",
        "scenario_id": scenario_id,
        "units": {
            "time": "seconds",
            "counts": "vehicles",
        },
        "metrics": {
            "expected_deployed": int(expected),
            "baseline_target": int(BASELINE_TARGET),
            "completed_trips": campus_exits,
            "campus_exits": campus_exits,
            "geojson_features": features,
            "remaining_on_campus": remaining,
            "campus_cleared": campus_cleared,
            "completed_unique_ids_csv": int(completed_csv),
            "goal_reached_rows_csv": int(goal_rows),
            "completion_rate_pct": rate,
            "csv_tick_rows": int(csv_rows),
            # Last campus-leave time among leavers; only a clearance time if campus_cleared.
            "evac_end_s": int(evac_end_s) if evac_end_s else None,
            "peak_on_campus": peak_on_campus(on_campus_curve),
            "mean_trip_s": trip_stats.get("mean_campus_exit_travel_s"),
            "median_trip_s": trip_stats.get("median_campus_exit_travel_s"),
            "mean_exit_from_t0_s": trip_stats.get("mean_campus_exit_from_t0_s"),
            "median_exit_from_t0_s": trip_stats.get("median_campus_exit_from_t0_s"),
            "mean_campus_exit_travel_s": trip_stats.get("mean_campus_exit_travel_s"),
            "median_campus_exit_travel_s": trip_stats.get("median_campus_exit_travel_s"),
            "mean_campus_exit_from_t0_s": trip_stats.get("mean_campus_exit_from_t0_s"),
            "median_campus_exit_from_t0_s": trip_stats.get("median_campus_exit_from_t0_s"),
            "mean_finish_travel_s": trip_stats.get("mean_finish_travel_s"),
            "median_finish_travel_s": trip_stats.get("median_finish_travel_s"),
            "mean_finish_from_t0_s": trip_stats.get("mean_finish_from_t0_s"),
            "median_finish_from_t0_s": trip_stats.get("median_finish_from_t0_s"),
            "n_trips_timed": trip_stats.get("n_trips") or 0,
            "warning": warning,
        },
        "lot_deploy_plan": {
            lot: {
                "schedule_spawns": int(per_lot.get(lot, 0)),
                "baseline_target": int(LOT_COUNTS[lot]),
            }
            for lot in LOT_ORDER
        },
        "clearance_by_lot_s": {
            lot: clearance.get(lot) for lot in LOT_ORDER
        },
        "completed_by_lot": {
            lot: {
                "schedule_spawns": int(per_lot.get(lot, 0)),
                "completed_trips": int((trip_stats.get("completed_by_lot") or {}).get(lot, 0)),
                "remaining_on_campus": int(
                    (trip_stats.get("remaining_by_lot") or {}).get(lot, 0)
                ),
            }
            for lot in LOT_ORDER
        },
        "exit_usage": trip_stats.get("exit_usage") or {name: 0 for name in CAMPUS_EXIT_ORDER},
        "finish_exit_usage": trip_stats.get("finish_exit_usage")
        or {name: 0 for name in EXIT_ORDER},
        "artifacts": {
            "summary_csv": "summary.csv",
            "evac_curve_csv": "evac_curve.csv",
            "clearance_by_lot_csv": "clearance_by_lot.csv",
            "completion_by_lot_csv": "completion_by_lot.csv",
            "exit_usage_csv": "exit_usage.csv",
            "campus_exits_csv": "campus_exits.csv",
            "metrics_json": "metrics.json",
        },
    }


def write_metrics_json(output_dir: Path, payload: dict) -> Path:
    path = output_dir / "metrics.json"
    path.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")
    return path


def write_outputs(
    output_dir: Path,
    expected,
    per_lot,
    completed_csv,
    goal_rows,
    trips,
    on_campus_curve,
    spawn_curve,
    depart_curve,
    csv_rows,
    evac_end_s=0,
    trip_stats: dict | None = None,
    scenario_id: str | None = None,
    geojson_features: int | None = None,
    remaining_on_campus: int | None = None,
    config_end_s: int | None = None,
):
    output_dir.mkdir(parents=True, exist_ok=True)
    trip_stats = trip_stats or trip_time_stats([])
    campus_exits = int(trip_stats.get("campus_exits") or trips)
    features = int(geojson_features if geojson_features is not None else campus_exits)
    remaining = int(
        remaining_on_campus
        if remaining_on_campus is not None
        else trip_stats.get("remaining_on_campus") or 0
    )
    denom = max(int(expected) or 0, campus_exits + remaining, 1)
    rate = campus_exits / denom * 100.0
    if remaining > 0 and rate >= 100.0:
        rate = (
            campus_exits / (campus_exits + remaining) * 100.0
            if (campus_exits + remaining)
            else 0.0
        )
    # Keep chart/lot helpers aligned with the authoritative remaining count.
    trip_stats = {**trip_stats, "remaining_on_campus": remaining, "campus_exits": campus_exits}

    with (output_dir / "summary.csv").open("w", newline="", encoding="utf-8") as f:
        w = csv.DictWriter(
            f,
            fieldnames=[
                "scenario_id",
                "expected_deployed",
                "baseline_target",
                "completed_trips_campus_exits",
                "geojson_features",
                "remaining_on_campus",
                "completed_unique_ids_csv",
                "goal_reached_rows_csv",
                "completion_rate_pct",
                "csv_tick_rows",
                "evac_end_s",
                "peak_on_campus",
                "mean_campus_exit_travel_s",
                "median_campus_exit_travel_s",
                "mean_campus_exit_from_t0_s",
                "median_campus_exit_from_t0_s",
                "mean_finish_travel_s",
                "median_finish_travel_s",
                "mean_finish_from_t0_s",
                "median_finish_from_t0_s",
            ],
        )
        w.writeheader()
        w.writerow(
            {
                "scenario_id": scenario_id or "",
                "expected_deployed": expected,
                "baseline_target": BASELINE_TARGET,
                "completed_trips_campus_exits": campus_exits,
                "geojson_features": features,
                "remaining_on_campus": remaining,
                "completed_unique_ids_csv": completed_csv,
                "goal_reached_rows_csv": goal_rows,
                "completion_rate_pct": round(rate, 2),
                "csv_tick_rows": csv_rows,
                "evac_end_s": evac_end_s,
                "peak_on_campus": peak_on_campus(on_campus_curve) or "",
                "mean_campus_exit_travel_s": trip_stats.get("mean_campus_exit_travel_s") or "",
                "median_campus_exit_travel_s": trip_stats.get("median_campus_exit_travel_s") or "",
                "mean_campus_exit_from_t0_s": trip_stats.get("mean_campus_exit_from_t0_s") or "",
                "median_campus_exit_from_t0_s": trip_stats.get("median_campus_exit_from_t0_s") or "",
                "mean_finish_travel_s": trip_stats.get("mean_finish_travel_s") or "",
                "median_finish_travel_s": trip_stats.get("median_finish_travel_s") or "",
                "mean_finish_from_t0_s": trip_stats.get("mean_finish_from_t0_s") or "",
                "median_finish_from_t0_s": trip_stats.get("median_finish_from_t0_s") or "",
            }
        )

    metrics = build_metrics_payload(
        scenario_id=scenario_id,
        expected=expected,
        per_lot=per_lot,
        completed_csv=completed_csv,
        goal_rows=goal_rows,
        trips=campus_exits,
        csv_rows=csv_rows,
        evac_end_s=evac_end_s,
        trip_stats=trip_stats,
        on_campus_curve=on_campus_curve,
        geojson_features=features,
        remaining_on_campus=remaining,
        config_end_s=config_end_s,
    )
    write_metrics_json(output_dir, metrics)

    with (output_dir / "lot_deploy_plan.csv").open("w", newline="", encoding="utf-8") as f:
        w = csv.DictWriter(f, fieldnames=["lot", "schedule_spawns", "baseline_target"])
        w.writeheader()
        for lot in LOT_ORDER:
            w.writerow(
                {
                    "lot": lot,
                    "schedule_spawns": per_lot.get(lot, 0),
                    "baseline_target": LOT_COUNTS[lot],
                }
            )

    with (output_dir / "clearance_by_lot.csv").open("w", newline="", encoding="utf-8") as f:
        w = csv.DictWriter(f, fieldnames=["lot", "clearance_s_from_t0"])
        w.writeheader()
        for lot in LOT_ORDER:
            val = (trip_stats.get("clearance_by_lot_s") or {}).get(lot)
            w.writerow({"lot": lot, "clearance_s_from_t0": val if val is not None else ""})

    with (output_dir / "completion_by_lot.csv").open("w", newline="", encoding="utf-8") as f:
        w = csv.DictWriter(f, fieldnames=["lot", "schedule_spawns", "completed_trips"])
        w.writeheader()
        completed_by_lot = trip_stats.get("completed_by_lot") or {}
        for lot in LOT_ORDER:
            w.writerow(
                {
                    "lot": lot,
                    "schedule_spawns": per_lot.get(lot, 0),
                    "completed_trips": completed_by_lot.get(lot, 0),
                }
            )

    with (output_dir / "exit_usage.csv").open("w", newline="", encoding="utf-8") as f:
        w = csv.DictWriter(f, fieldnames=["exit", "campus_exits"])
        w.writeheader()
        usage = trip_stats.get("exit_usage") or {}
        for name in CAMPUS_EXIT_ORDER:
            w.writerow({"exit": name, "campus_exits": usage.get(name, 0)})

    with (output_dir / "campus_exits.csv").open("w", newline="", encoding="utf-8") as f:
        w = csv.DictWriter(
            f,
            fieldnames=[
                "lot",
                "campus_exit",
                "campus_exit_from_t0_s",
                "campus_exit_travel_s",
                "finish_exit",
                "finish_from_t0_s",
            ],
        )
        w.writeheader()
        for r in trip_stats.get("records") or []:
            w.writerow(
                {
                    "lot": r.get("lot", ""),
                    "campus_exit": r.get("campus_exit", "") if r.get("campus_exit_detected") else "",
                    "campus_exit_from_t0_s": r.get("campus_exit_from_t0_s")
                    if r.get("campus_exit_detected")
                    else "",
                    "campus_exit_travel_s": r.get("campus_exit_travel_s")
                    if r.get("campus_exit_detected")
                    else "",
                    "finish_exit": r.get("exit", ""),
                    "finish_from_t0_s": r.get("exit_from_t0_s", ""),
                }
            )

    with (output_dir / "evac_curve.csv").open("w", newline="", encoding="utf-8") as f:
        w = csv.writer(f)
        w.writerow(
            [
                "time_s",
                "cars_on_campus",
                "cumulative_spawned",
                "cumulative_exited",
            ]
        )
        spawn_by_t = dict(spawn_curve)
        depart_by_t = dict(depart_curve)
        campus_by_t = dict(on_campus_curve)
        all_times = sorted(set(spawn_by_t) | set(depart_by_t) | set(campus_by_t))
        for t in all_times:
            w.writerow(
                [
                    t,
                    campus_by_t.get(t, ""),
                    spawn_by_t.get(t, ""),
                    depart_by_t.get(t, ""),
                ]
            )

    plt.figure(figsize=(6, 4))
    completed = int(trip_stats.get("campus_exits") or trips)
    remaining = int(trip_stats.get("remaining_on_campus") or 0)
    plt.bar(
        ["Expected\n(schedule)", "Baseline\ntarget", "Campus\nexits"],
        [int(expected), int(BASELINE_TARGET), completed],
        color=["#4c72b0", "#8172b2", "#c44e52"],
    )
    plt.ylabel("Vehicles")
    summary_title = "Deployment vs campus exits"
    if remaining > 0:
        summary_title = f"INCOMPLETE — {remaining} still on campus | {summary_title}"
    plt.title(scenario_chart_title(summary_title, scenario_id))
    if completed == 0:
        plt.text(2, max(int(expected), int(BASELINE_TARGET)) * 0.05, "0 — check trips geojson path", ha="center", fontsize=8)
    if remaining > 0:
        plt.text(
            2,
            completed + max(int(expected), int(BASELINE_TARGET)) * 0.03,
            f"{remaining} still on campus",
            ha="center",
            fontsize=8,
            color="#333333",
        )
    plt.tight_layout()
    plt.savefig(output_dir / "summary.png", dpi=200)
    plt.close()

    if on_campus_curve:
        clearance = trip_stats.get("clearance_by_lot_s") or {}
        cong_note = congestion_note_for_clearances(output_dir, clearance)

        fig, ax = plt.subplots(figsize=(9, 4.8))
        times, cars = zip(*on_campus_curve)
        avg_all = sum(cars) / len(cars)
        avg_early = sum(c for t, c in on_campus_curve if t <= 60) / max(
            1, sum(1 for t, _ in on_campus_curve if t <= 60)
        )
        start_n = int(cars[0]) if cars else 0
        end_n = int(cars[-1]) if cars else 0
        n0 = max(int(expected) or 0, int(BASELINE_TARGET), start_n)
        ax.plot(times, cars, label="Left to evacuate", color="#4c72b0", linewidth=1.5)
        # Always show full 0..N0 scale so curves start at schedule deployment.
        ax.set_ylim(0, n0 * 1.02)
        annotate_lot_clearances(ax, clearance)

        ax.set_xlabel("Time (s)")
        ax.set_ylabel("Cars left to evacuate")
        curve_title = "Evacuation curve — cars that have not yet left campus"
        if remaining > 0:
            curve_title = (
                f"INCOMPLETE — {remaining} still on campus at endPoint\n{curve_title}"
            )
        ax.set_title(scenario_chart_title(curve_title, scenario_id))
        subtitle = (
            f"Start={start_n} (N0={n0})   End={end_n}   "
            f"Avg (t≤60s)={avg_early:.2f}   Avg (all)={avg_all:.2f}"
            "   |   Dashed: lot fully left campus (last campus exit)"
        )
        if remaining > 0:
            subtitle = (
                f"INCOMPLETE — {remaining} still on campus at endPoint "
                f"(not a successful clear)\n{subtitle}"
            )
        if cong_note:
            subtitle = f"{subtitle}\n{cong_note}"
        fig.suptitle(subtitle, fontsize=8, y=0.98)
        ax.legend(loc="upper left")
        ax.grid(True, alpha=0.3)
        fig.tight_layout()
        fig.savefig(output_dir / "evac_curve.png", dpi=200)
        plt.close(fig)

    plot_trip_time_charts(output_dir, trip_stats, scenario_id=scenario_id)
    plot_lot_completion_and_exits(output_dir, per_lot, trip_stats, scenario_id=scenario_id)

def main():
    # argv[1] may be a run output directory or an agent CSV path.
    # Directory args must mean that folder is the run output dir (not its parent).
    arg = sys.argv[1] if len(sys.argv) > 1 else None
    if arg is None:
        csv_path = agent_output_path(DEFAULT_RESULTS, ".csv")
        output_dir = csv_path.parent
    else:
        resolved = resolve_path(arg, agent_output_path(DEFAULT_RESULTS, ".csv"))
        if resolved.is_dir():
            output_dir = resolved
            csv_path = agent_output_path(output_dir, ".csv")
        else:
            csv_path = resolved
            output_dir = csv_path.parent
    trips_path = resolve_path(
        sys.argv[2] if len(sys.argv) > 2 else None,
        agent_output_path(output_dir, "_trips.geojson"),
    )

    print(f"Project root : {ROOT}")
    print(f"Output dir   : {output_dir}")
    print(f"Trips file   : {trips_path} ({'found' if trips_path.is_file() else 'MISSING'})")
    print(f"CSV file     : {csv_path} ({'found' if csv_path.is_file() else 'not used'})")

    schedule_path, config_path = resolve_scenario_paths(csv_path)
    scenario_id = (
        scenario_id_from_path(csv_path)
        or scenario_id_from_path(output_dir)
        or scenario_id_from_path(trips_path)
    )
    print(f"Schedule     : {schedule_path}")
    print(f"Config       : {config_path}")
    if scenario_id:
        print(f"Scenario     : {scenario_id}")
        gates = campus_exit_points_for_scenario(scenario_id)
        print("Campus gates : " + " | ".join(gates.keys()))

    sim_start, config_end_s = read_sim_times(config_path)
    expected, per_lot = read_schedule(schedule_path)
    trips, depart_times, trip_records = load_trips_summary(
        trips_path, sim_start, scenario_id=scenario_id
    )
    spawn_times = spawn_event_times(schedule_path, sim_start)
    stats = trip_time_stats(trip_records, per_lot=per_lot)

    # CSV GoalReached = destination arrival — useful for completion counts only.
    # On-campus occupancy / evacuation end must use campus LEAVE times, not dest.
    if csv_path.is_file():
        completed_csv, goal_rows, _csv_on_campus, csv_rows = analyze_csv(csv_path)
        print(
            f"Using {csv_path.name} for completion counts only "
            f"(on-campus curve uses campus leave times, not GoalReached)."
        )
    else:
        completed_csv, goal_rows, csv_rows = 0, 0, 0
        print(f"Note: {csv_path.name} not found — completion counts from trips only.")

    max_t = max(
        [config_end_s or 0]
        + ([depart_times[-1]] if depart_times else [])
        + ([spawn_times[-1]] if spawn_times else [])
    )

    spawn_curve = cumulative_curve(spawn_times, max_t)
    depart_curve = cumulative_curve(depart_times, max_t)

    # Cars left to evacuate = N0 − cumulative campus exits (not destination arrivals).
    # N0 is schedule deployment (expected); falls back to baseline 3200.
    # Includes not-yet-spawned / delayed-lot cars. Do NOT use spawned−exited
    # (that rises with delayed lots and understates remaining at t=0).
    n0 = int(expected) if expected else int(BASELINE_TARGET)
    on_campus_curve = [
        (t, max(0, n0 - exited)) for t, exited in depart_curve
    ]
    if depart_times:
        evac_end_s = depart_times[-1]
    else:
        evac_end_s = 0

    remaining = max(0, n0 - len(depart_times))
    if remaining > 0:
        t_label = f"t={config_end_s}s" if config_end_s else "endPoint"
        print(
            f"WARNING: INCOMPLETE — {remaining} cars still on campus at {t_label}. "
            f"Geojson still has Features for stuck agents — "
            f"completed_trips counts campus exits only ({len(depart_times)}). "
            f"Do NOT treat as 100% clear; jam or extend endPoint and re-sim."
        )

    # Evacuation completion = campus leaves. Do NOT inflate with geojson Feature
    # count: MARS writes a Feature for cars still inside the footprint at endPoint.
    geojson_features = int(trips)
    campus_exits = len(depart_times)
    completed = campus_exits
    if geojson_features != campus_exits or remaining > 0:
        print(
            f"NOTE: geojson Features={geojson_features}, campus exits={campus_exits}, "
            f"remaining_on_campus={remaining}, campus_cleared={remaining == 0}"
        )

    write_outputs(
        output_dir,
        expected,
        per_lot,
        completed_csv,
        goal_rows,
        completed,
        on_campus_curve,
        spawn_curve,
        depart_curve,
        csv_rows,
        evac_end_s,
        trip_stats=stats,
        scenario_id=scenario_id,
        geojson_features=geojson_features,
        remaining_on_campus=remaining,
        config_end_s=config_end_s,
    )

    print("=== MARS run summary ===")
    print(f"Expected deployed (schedule) : {expected}")
    print(f"Baseline deployment target   : {BASELINE_TARGET}")
    print(f"N0 (curve / remaining)       : {n0}")
    print(f"Campus exits (completed)     : {completed}")
    print(f"Geojson Features             : {geojson_features}")
    print(f"Remaining on campus          : {remaining}")
    print(f"Campus cleared               : {remaining == 0 and completed > 0}")
    rate_denom = max(int(expected) or 0, completed + remaining, 1)
    print(f"Completion rate (exits/N)    : {completed / rate_denom * 100:.2f}%")
    print(f"CSV tick rows                : {csv_rows}")
    if remaining == 0 and evac_end_s:
        print(f"Campus clear (last leave)    : t={evac_end_s}s")
    elif evac_end_s:
        print(f"Last campus leave (incomplete): t={evac_end_s}s (NOT a clear time)")
    if stats.get("n_trips"):
        print(f"Mean drive to leave campus  : {stats['mean_campus_exit_travel_s']:.1f}s")
        print(f"Median drive to leave campus: {stats['median_campus_exit_travel_s']:.1f}s")
        print(f"Mean leave campus (from t=0): {stats['mean_campus_exit_from_t0_s']:.1f}s")
        print(f"Median leave campus (t=0)   : {stats['median_campus_exit_from_t0_s']:.1f}s")
        if stats.get("mean_finish_from_t0_s") is not None:
            print(f"Mean reach destination (t=0): {stats['mean_finish_from_t0_s']:.1f}s")
        print("Lot fully left campus (last campus exit; n/a if stuck remain):")
        for lot in LOT_ORDER:
            val = stats["clearance_by_lot_s"].get(lot)
            print(f"  {lot}: {val if val is not None else 'n/a'}s")
        print("Completed by lot (campus leaves detected):")
        for lot in LOT_ORDER:
            rem = (stats.get("remaining_by_lot") or {}).get(lot, 0)
            rem_s = f" (remaining {rem})" if rem else ""
            print(f"  {lot}: {stats['completed_by_lot'].get(lot, 0)}{rem_s}")
        print("Campus exit gate usage:")
        for name in CAMPUS_EXIT_ORDER:
            n = stats["exit_usage"].get(name, 0)
            if n or name != "other":
                print(f"  {name}: {n}")
    print(f"Output folder                : {output_dir}")
    print()
    print("Charts: summary.png, evac_curve.png, trip_time_stats.png,")
    print("        trip_time_hist.png, clearance_by_lot.png,")
    print("        completion_by_lot.png, exit_usage.png")
    print("Metrics : metrics.json (schema_version=2, join on scenario_id)")
    print("Per-trip: campus_exits.csv (leave-campus time != destination time)")
    print("Note: trips geojson Features != completed evacuations.")
    print("      Campus exit = left university (bbox/gate), NOT destination.")
    print("      Evac curve = N0 - cumulative campus exits (left to evacuate).")
    print("      Drive time = campus-leave timestamp - spawn timestamp.")
    print("      From t=0 = campus-leave unix - simulation start.")
    print("      finish_* columns = reached scheduled destination (leavers only).")


if __name__ == "__main__":
    main()
