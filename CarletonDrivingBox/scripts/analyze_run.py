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
BASELINE_TARGET = 3200
LOT_COUNTS = {"P1": 100, "P2": 100, "P3": 200, "P4": 100, "P5": 700, "P6": 900, "P7": 1100}
LOT_ORDER = ["P1", "P2", "P3", "P4", "P5", "P6", "P7"]


def load_lot_coords() -> dict[str, tuple[float, float]]:
    coords: dict[str, tuple[float, float]] = {}
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


LOT_COORDS = load_lot_coords()


def scenario_id_from_path(path: Path) -> str | None:
    for part in reversed(path.parts):
        m = re.fullmatch(r"scenario_(\d+)", part)
        if m:
            return m.group(1)
    return None


def resolve_scenario_paths(csv_path: Path) -> tuple[Path, Path]:
    """Pick schedule + config for results/scenario_XX/ runs."""
    sid = scenario_id_from_path(csv_path)
    if sid:
        schedule = SCHEDULES_DIR / f"scenario_{sid}_schedule.csv"
        config = CONFIGS_DIR / f"config_scenario_{sid}.json"
        if schedule.is_file() and config.is_file():
            return schedule, config
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
    """MARS endTime is exclusive (SOHTrainBox README)."""
    start = parse_clock(row["startTime"])
    end = parse_clock(row["endTime"])
    interval = float(row["spawningIntervalInMinutes"])
    amount = int(row["spawningAmount"])
    if interval <= 0:
        return amount
    total = 0
    t = start
    step = timedelta(minutes=interval)
    while t < end:
        total += amount
        t += step
    return total


def lot_for_row(row) -> str:
    slat = float(row["startLat"])
    slon = float(row["startLon"])
    for lot, (lat, lon) in LOT_COORDS.items():
        if abs(slat - lat) < 1e-4 and abs(slon - lon) < 1e-4:
            return lot
    return "unknown"


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
            t = start
            step = timedelta(minutes=interval) if interval > 0 else None
            while t < end:
                spawn_dt = datetime.combine(sim_start.date(), t.time(), tzinfo=timezone.utc)
                sec = int((spawn_dt - sim_start).total_seconds())
                for _ in range(amount):
                    events.append(sec)
                if step is None:
                    break
                t += step
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


def extract_trip_records_stream(path: Path, sim_start_unix: int) -> list[dict]:
    """Stream trips geojson: travel time, exit-from-t0, and origin lot per trip.

    Coordinates are [lon, lat, z, unix_timestamp]. Does not load the full file.
    """
    records: list[dict] = []
    buf = ""
    feat_re = re.compile(r'"type"\s*:\s*"Feature"')
    coord_re = re.compile(
        r"\[(-?\d+(?:\.\d+)?),\s*(-?\d+(?:\.\d+)?),\s*(-?\d+(?:\.\d+)?),\s*(\d{10})\]"
    )
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
                coords = coord_re.findall(segment)
                if coords:
                    lon0, lat0, _, t0 = coords[0]
                    _, _, _, t1 = coords[-1]
                    start_u = int(t0)
                    end_u = int(t1)
                    travel_s = end_u - start_u
                    exit_from_t0_s = end_u - sim_start_unix
                    if travel_s >= 0 and exit_from_t0_s >= 0:
                        records.append(
                            {
                                "travel_s": travel_s,
                                "exit_from_t0_s": exit_from_t0_s,
                                "lot": nearest_lot(float(lat0), float(lon0)),
                            }
                        )
                buf = buf[props:]
    return records


def load_trips_summary(path: Path, sim_start: datetime) -> tuple[int, list[int], list[dict]]:
    """Trip count, exit times (sim s), and per-trip travel records."""
    if not path.is_file():
        print(f"WARNING: trips file not found: {path}")
        return 0, [], []

    size = path.stat().st_size
    fast_count = count_features_fast(path)
    sim_start_unix = int(sim_start.timestamp())
    records = extract_trip_records_stream(path, sim_start_unix)
    depart_times = sorted(r["exit_from_t0_s"] for r in records)
    trip_count = max(fast_count, len(records))

    print(
        f"Trips geojson: {size:,} bytes | feature scan={fast_count} | "
        f"trip records={len(records)}"
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


def trip_time_stats(records: list[dict]) -> dict:
    """Mean/median travel time (from lot) and exit time (from t=0)."""
    empty = {
        "n_trips": 0,
        "mean_trip_s": None,
        "median_trip_s": None,
        "mean_exit_from_t0_s": None,
        "median_exit_from_t0_s": None,
        "clearance_by_lot_s": {lot: None for lot in LOT_ORDER},
    }
    if not records:
        return empty

    travel = [r["travel_s"] for r in records]
    from_t0 = [r["exit_from_t0_s"] for r in records]
    clearance: dict[str, int] = {}
    for r in records:
        lot = r["lot"]
        if lot not in LOT_ORDER:
            continue
        clearance[lot] = max(clearance.get(lot, 0), r["exit_from_t0_s"])

    return {
        "n_trips": len(records),
        "mean_trip_s": round(mean(travel), 2),
        "median_trip_s": round(median(travel), 2),
        "mean_exit_from_t0_s": round(mean(from_t0), 2),
        "median_exit_from_t0_s": round(median(from_t0), 2),
        "clearance_by_lot_s": {lot: clearance.get(lot) for lot in LOT_ORDER},
        "travel_times": travel,
    }


def plot_trip_time_charts(output_dir: Path, stats: dict) -> None:
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
        "Mean\ndrive time",
        "Median\ndrive time",
        "Mean\nfinish time",
        "Median\nfinish time",
    ]

    from matplotlib.patches import Patch

    plt.figure(figsize=(8, 4.8))
    bars = plt.bar(bar_labels, values, color=colors)
    plt.ylabel("Seconds")
    plt.title("Travel-time summary")
    plt.legend(
        handles=[
            Patch(
                facecolor="#4c72b0",
                label="Blue = drive time (leave lot → campus exit)",
            ),
            Patch(
                facecolor="#55a868",
                label="Green = finish time (sim start t=0 → campus exit)",
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
    plt.xlabel("Trip duration (s) — spawn to exit")
    plt.ylabel("Trips")
    plt.title("Travel-time distribution")
    plt.legend()
    plt.grid(True, axis="y", alpha=0.3)
    plt.tight_layout()
    plt.savefig(output_dir / "trip_time_hist.png", dpi=200)
    plt.close()

    lots = LOT_ORDER
    clearance = [stats["clearance_by_lot_s"].get(lot) or 0 for lot in lots]
    plt.figure(figsize=(7, 4.2))
    bars = plt.bar(lots, clearance, color="#dd8452")
    plt.ylabel("Clearance time (s from t=0)")
    plt.xlabel("Parking lot")
    plt.title("Clearance time by lot (last exit)")
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
):
    output_dir.mkdir(parents=True, exist_ok=True)
    trip_stats = trip_stats or trip_time_stats([])

    with (output_dir / "summary.csv").open("w", newline="", encoding="utf-8") as f:
        w = csv.DictWriter(
            f,
            fieldnames=[
                "expected_deployed",
                "baseline_target",
                "completed_trips_geojson",
                "completed_unique_ids_csv",
                "goal_reached_rows_csv",
                "completion_rate_pct",
                "csv_tick_rows",
                "evac_end_s",
                "mean_trip_s",
                "median_trip_s",
                "mean_exit_from_t0_s",
                "median_exit_from_t0_s",
            ],
        )
        w.writeheader()
        rate = (trips / expected * 100.0) if expected else 0.0
        w.writerow(
            {
                "expected_deployed": expected,
                "baseline_target": BASELINE_TARGET,
                "completed_trips_geojson": trips,
                "completed_unique_ids_csv": completed_csv,
                "goal_reached_rows_csv": goal_rows,
                "completion_rate_pct": round(rate, 2),
                "csv_tick_rows": csv_rows,
                "evac_end_s": evac_end_s,
                "mean_trip_s": trip_stats.get("mean_trip_s") or "",
                "median_trip_s": trip_stats.get("median_trip_s") or "",
                "mean_exit_from_t0_s": trip_stats.get("mean_exit_from_t0_s") or "",
                "median_exit_from_t0_s": trip_stats.get("median_exit_from_t0_s") or "",
            }
        )

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
    completed = int(trips)
    plt.bar(
        ["Expected\n(schedule)", "Baseline\ntarget", "Completed\n(trips)"],
        [int(expected), int(BASELINE_TARGET), completed],
        color=["#4c72b0", "#8172b2", "#c44e52"],
    )
    plt.ylabel("Vehicles")
    plt.title("Deployment vs completion")
    if completed == 0:
        plt.text(2, max(int(expected), int(BASELINE_TARGET)) * 0.05, "0 — check trips geojson path", ha="center", fontsize=8)
    plt.tight_layout()
    plt.savefig(output_dir / "summary.png", dpi=200)
    plt.close()

    if on_campus_curve:
        plt.figure(figsize=(9, 4.5))
        times, cars = zip(*on_campus_curve)
        avg_all = sum(cars) / len(cars)
        avg_spawn = sum(c for t, c in on_campus_curve if t <= 60) / max(
            1, sum(1 for t, _ in on_campus_curve if t <= 60)
        )
        plt.plot(times, cars, label="On campus", color="#4c72b0", linewidth=1.5)

        plt.xlabel("Time (s)")
        plt.ylabel("Vehicles")
        plt.title("Evacuation Curve")
        plt.suptitle(
            f"Avg on campus (t≤60s)={avg_spawn:.2f}   Avg on campus (all)={avg_all:.2f}",
            fontsize=9,
            y=0.98,
        )
        plt.legend(loc="upper left")
        plt.grid(True, alpha=0.3)
        plt.tight_layout()
        plt.savefig(output_dir / "evac_curve.png", dpi=200)
        plt.close()

    plot_trip_time_charts(output_dir, trip_stats)

def main():
    csv_path = resolve_path(
        sys.argv[1] if len(sys.argv) > 1 else None,
        agent_output_path(DEFAULT_RESULTS, ".csv"),
    )
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
    print(f"Schedule     : {schedule_path}")
    print(f"Config       : {config_path}")

    sim_start, config_end_s = read_sim_times(config_path)
    expected, per_lot = read_schedule(schedule_path)
    trips, depart_times, trip_records = load_trips_summary(trips_path, sim_start)
    spawn_times = spawn_event_times(schedule_path, sim_start)
    stats = trip_time_stats(trip_records)

    if csv_path.is_file():
        completed_csv, goal_rows, on_campus_curve, csv_rows = analyze_csv(csv_path)
        evac_end_s = max((t for t, c in on_campus_curve if c > 0), default=0)
        print(f"Using {csv_path.name} for on-campus curve (active drivers per tick).")
    else:
        completed_csv, goal_rows, on_campus_curve, csv_rows = 0, 0, [], 0
        evac_end_s = 0
        print(f"Note: {csv_path.name} not found — on-campus curve estimated from schedule minus exits.")

    max_t = max(
        [config_end_s or 0]
        + ([on_campus_curve[-1][0]] if on_campus_curve else [])
        + ([depart_times[-1]] if depart_times else [])
        + ([spawn_times[-1]] if spawn_times else [])
    )

    spawn_curve = cumulative_curve(spawn_times, max_t)
    depart_curve = cumulative_curve(depart_times, max_t)

    completed = max(
        trips,
        len(depart_times),
        depart_curve[-1][1] if depart_curve else 0,
    )
    if completed != trips:
        print(f"NOTE: completed count = {completed} (features={trips}, exits={len(depart_times)})")
        trips = completed

    if not on_campus_curve:
        on_campus_curve = [
            (t, max(0, spawned - exited))
            for (t, spawned), (_, exited) in zip(spawn_curve, depart_curve)
        ]
        if depart_times:
            evac_end_s = max(
                (
                    t
                    for t, spawned, exited in (
                        (t, s, d) for (t, s), (_, d) in zip(spawn_curve, depart_curve)
                    )
                    if spawned > exited
                ),
                default=depart_times[-1],
            )

    write_outputs(
        output_dir,
        expected,
        per_lot,
        completed_csv,
        goal_rows,
        trips,
        on_campus_curve,
        spawn_curve,
        depart_curve,
        csv_rows,
        evac_end_s,
        trip_stats=stats,
    )

    print("=== MARS run summary ===")
    print(f"Expected deployed (schedule) : {expected}")
    print(f"Baseline deployment target   : {BASELINE_TARGET}")
    print(f"Completed (trips geojson)    : {trips}")
    print(f"Completion rate              : {trips / expected * 100:.2f}%" if expected else "n/a")
    print(f"CSV tick rows                : {csv_rows}")
    if evac_end_s:
        print(f"Evac end (last car on campus): t={evac_end_s}s")
    if stats.get("n_trips"):
        print(f"Mean trip (from lot)         : {stats['mean_trip_s']:.1f}s")
        print(f"Median trip (from lot)       : {stats['median_trip_s']:.1f}s")
        print(f"Mean exit (from t=0)         : {stats['mean_exit_from_t0_s']:.1f}s")
        print(f"Median exit (from t=0)       : {stats['median_exit_from_t0_s']:.1f}s")
        print("Clearance by lot (last exit):")
        for lot in LOT_ORDER:
            val = stats["clearance_by_lot_s"].get(lot)
            print(f"  {lot}: {val if val is not None else 'n/a'}s")
    print(f"Output folder                : {output_dir}")
    print()
    print("Charts: summary.png, evac_curve.png, trip_time_stats.png,")
    print("        trip_time_hist.png, clearance_by_lot.png")
    print("Note: trips geojson = finished drives only.")
    print("      Travel time = last coord timestamp - first (spawn to exit).")
    print("      From t=0 = exit unix - simulation start.")


if __name__ == "__main__":
    main()
