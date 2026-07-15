#!/usr/bin/env python3
"""Summarize a MARS scheduler run — deployed vs completed, charts like DEVS."""
import csv
import json
import re
import sys
from collections import Counter
from datetime import datetime, timedelta
from pathlib import Path

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
DEVS_TARGET = 3200
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
    """Return simulation start time and optional end offset in seconds."""
    if not config_path.is_file():
        start = datetime(2021, 10, 11, 6, 0, 0)
        return start, None
    cfg = json.loads(config_path.read_text(encoding="utf-8"))
    start = datetime.fromisoformat(cfg["globals"]["startPoint"])
    end = datetime.fromisoformat(cfg["globals"]["endPoint"])
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
                spawn_dt = datetime.combine(sim_start.date(), t.time())
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


def extract_depart_times_stream(path: Path, sim_start_unix: int) -> list[int]:
    """Exit time (sim seconds) from the last coordinate timestamp in each feature."""
    depart: list[int] = []
    buf = ""
    feat_re = re.compile(r'"type"\s*:\s*"Feature"')
    ts_re = re.compile(r",(\d{10})\]")
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
                matches = ts_re.findall(segment)
                if matches:
                    depart.append(int(matches[-1]) - sim_start_unix)
                buf = buf[props:]
    depart.sort()
    return depart


def load_trips_summary(path: Path, sim_start: datetime) -> tuple[int, list[int]]:
    """Trip count and exit time (sim seconds) for each completed trip."""
    if not path.is_file():
        print(f"WARNING: trips file not found: {path}")
        return 0, []

    size = path.stat().st_size
    fast_count = count_features_fast(path)
    sim_start_unix = int(sim_start.timestamp())
    depart_times = extract_depart_times_stream(path, sim_start_unix)
    trip_count = max(fast_count, len(depart_times))

    print(f"Trips geojson: {size:,} bytes | feature scan={fast_count} | exit timestamps={len(depart_times)}")

    if size > 100_000 and trip_count == 0:
        head = path.read_bytes()[:400].decode("utf-8", errors="replace")
        print(f"ERROR: geojson is non-empty but 0 trips counted. Head: {head[:200]!r}")

    return trip_count, depart_times


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
):
    output_dir.mkdir(parents=True, exist_ok=True)

    with (output_dir / "summary.csv").open("w", newline="", encoding="utf-8") as f:
        w = csv.DictWriter(
            f,
            fieldnames=[
                "expected_deployed",
                "devs_target",
                "completed_trips_geojson",
                "completed_unique_ids_csv",
                "goal_reached_rows_csv",
                "completion_rate_pct",
                "csv_tick_rows",
                "evac_end_s",
            ],
        )
        w.writeheader()
        rate = (trips / expected * 100.0) if expected else 0.0
        w.writerow(
            {
                "expected_deployed": expected,
                "devs_target": DEVS_TARGET,
                "completed_trips_geojson": trips,
                "completed_unique_ids_csv": completed_csv,
                "goal_reached_rows_csv": goal_rows,
                "completion_rate_pct": round(rate, 2),
                "csv_tick_rows": csv_rows,
                "evac_end_s": evac_end_s,
            }
        )

    with (output_dir / "lot_deploy_plan.csv").open("w", newline="", encoding="utf-8") as f:
        w = csv.DictWriter(f, fieldnames=["lot", "schedule_spawns", "devs_target"])
        w.writeheader()
        for lot in LOT_ORDER:
            w.writerow(
                {
                    "lot": lot,
                    "schedule_spawns": per_lot.get(lot, 0),
                    "devs_target": LOT_COUNTS[lot],
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
    completed = int(trips)
    plt.bar(
        ["Expected\n(schedule)", "DEVS\ntarget", "Completed\n(trips)"],
        [int(expected), int(DEVS_TARGET), completed],
        color=["#4c72b0", "#8172b2", "#c44e52"],
    )
    plt.ylabel("Vehicles")
    plt.title("Deployment vs completion")
    if completed == 0:
        plt.text(2, max(int(expected), int(DEVS_TARGET)) * 0.05, "0 — check trips geojson path", ha="center", fontsize=8)
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
    trips, depart_times = load_trips_summary(trips_path, sim_start)
    spawn_times = spawn_event_times(schedule_path, sim_start)

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
    )

    print("=== MARS run summary ===")
    print(f"Expected deployed (schedule) : {expected}")
    print(f"DEVS scenario 01 target      : {DEVS_TARGET}")
    print(f"Completed (trips geojson)    : {trips}")
    print(f"Completion rate              : {trips / expected * 100:.2f}%" if expected else "n/a")
    print(f"CSV tick rows                : {csv_rows}")
    if evac_end_s:
        print(f"Evac end (last car on campus): t={evac_end_s}s")
    print(f"Output folder                : {output_dir}")
    print()
    print("Note: trips geojson = finished drives only.")
    print("      Red line = cumulative cars exited; blue = on campus.")
    print("      Without CSV, on-campus is estimated as spawned − exited.")


if __name__ == "__main__":
    main()
