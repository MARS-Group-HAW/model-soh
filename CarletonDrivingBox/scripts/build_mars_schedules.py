#!/usr/bin/env python3
"""Build MARS car_driver schedule CSVs from DEVS-style parking_lot_schedules delays.

Each scenario differs only in when lots start spawning. Delayed lots shift both
startTime and endTime by initEventInSec so deploy-window length stays the same.
"""
from __future__ import annotations

import csv
import json
from datetime import datetime, timedelta
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
DELAYS_DIR = ROOT / "resources" / "parking_lot_schedules"
BASE_SCHEDULE = ROOT / "resources" / "schedule_base.csv"
OUT_DIR = ROOT / "resources" / "schedules"
CONFIGS_DIR = ROOT / "configs"
BASE_CONFIG = ROOT / "config.json"

LOT_ORDER = ["P1", "P2", "P3", "P4", "P5", "P6", "P7"]
SIM_START = datetime(2021, 10, 11, 6, 0, 0)
EVAC_BUFFER = timedelta(hours=2, minutes=30)


def parse_clock(value: str) -> datetime:
    for fmt in ("%H:%M:%S", "%H:%M"):
        try:
            t = datetime.strptime(value.strip(), fmt)
            return SIM_START.replace(hour=t.hour, minute=t.minute, second=t.second)
        except ValueError:
            continue
    raise ValueError(f"Bad time: {value}")


def format_clock(dt: datetime) -> str:
    return dt.strftime("%H:%M")


def load_base_rows() -> dict[str, dict[str, str]]:
    rows: dict[str, dict[str, str]] = {}
    lines = [
        line
        for line in BASE_SCHEDULE.read_text(encoding="utf-8").splitlines()
        if line.strip() and not line.strip().startswith("#")
    ]
    for row in csv.DictReader(lines):
        rows[row["lot"]] = row
    return rows


def load_delays(path: Path) -> dict[str, int]:
    delays: dict[str, int] = {}
    with path.open(encoding="utf-8") as f:
        for row in csv.DictReader(f):
            delays[row["id"]] = int(row["initEventInSec"])
    return delays


def build_schedule_rows(base: dict[str, dict[str, str]], delays: dict[str, int]) -> tuple[list[dict[str, str]], datetime]:
    out: list[dict[str, str]] = []
    latest_end = SIM_START
    for lot in LOT_ORDER:
        src = base[lot]
        delay = timedelta(seconds=delays.get(lot, 0))
        start = parse_clock(src["startTime"]) + delay
        end = parse_clock(src["endTime"]) + delay
        latest_end = max(latest_end, end)
        row = {k: v for k, v in src.items() if k != "lot"}
        row["startTime"] = format_clock(start)
        row["endTime"] = format_clock(end)
        out.append(row)
    return out, latest_end


def write_schedule(path: Path, rows: list[dict[str, str]]) -> None:
    fieldnames = [
        "startTime", "endTime", "spawningIntervalInMinutes", "spawningAmount",
        "carType", "maxSpeed", "averageSpeed", "trafficCode",
        "startLat", "startLon", "destLat", "destLon",
        "osmRoute", "nextTrafficLightPhase", "driveMode",
    ]
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("w", newline="", encoding="utf-8") as f:
        w = csv.DictWriter(f, fieldnames=fieldnames)
        w.writeheader()
        w.writerows(rows)


def write_config(scenario_id: str, schedule_rel: str, end_dt: datetime) -> None:
    if BASE_CONFIG.is_file():
        cfg = json.loads(BASE_CONFIG.read_text(encoding="utf-8"))
    else:
        raise FileNotFoundError(f"Missing base config: {BASE_CONFIG}")

    cfg["id"] = f"scenario_{scenario_id}"
    cfg["globals"]["endPoint"] = (SIM_START + (end_dt - SIM_START) + EVAC_BUFFER).isoformat(timespec="seconds")

    for layer in cfg.get("layers", []):
        if layer.get("type") == "CarletonCarDriverSchedulerLayer":
            layer["file"] = schedule_rel.replace("\\", "/")

    CONFIGS_DIR.mkdir(parents=True, exist_ok=True)
    out = CONFIGS_DIR / f"config_scenario_{scenario_id}.json"
    out.write_text(json.dumps(cfg, indent=2) + "\n", encoding="utf-8")


def main() -> None:
    base = load_base_rows()
    OUT_DIR.mkdir(parents=True, exist_ok=True)

    for delay_file in sorted(DELAYS_DIR.glob("scenario_*.csv")):
        scenario_id = delay_file.stem.replace("scenario_", "")
        delays = load_delays(delay_file)
        rows, latest_end = build_schedule_rows(base, delays)
        schedule_path = OUT_DIR / f"scenario_{scenario_id}_schedule.csv"
        write_schedule(schedule_path, rows)
        rel = f"resources/schedules/scenario_{scenario_id}_schedule.csv"
        write_config(scenario_id, rel, latest_end)
        print(f"scenario_{scenario_id}: deploy ends ~{format_clock(latest_end)}, wrote {schedule_path.name}")

    # Keep legacy default in sync with scenario_01
    rows01, _ = build_schedule_rows(base, load_delays(DELAYS_DIR / "scenario_01.csv"))
    legacy = ROOT / "resources" / "car_driver_schedule.csv"
    write_schedule(legacy, rows01)
    print(f"Updated legacy {legacy.name}")


if __name__ == "__main__":
    main()
