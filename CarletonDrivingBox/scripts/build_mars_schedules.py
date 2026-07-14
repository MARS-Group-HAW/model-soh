#!/usr/bin/env python3
"""Build MARS car_driver schedule CSVs from DEVS-style parking_lot_schedules delays.

Each lot spawns exactly totalEvents cars at 12/minute (DEVS 1 car / 5 s).
A final partial minute uses spawningAmount < 12 when totalEvents is not divisible by 12.
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
CARS_PER_MINUTE = 12


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


def load_base_rows(scenario_id: str) -> dict[str, dict[str, str]]:
    specific = ROOT / "resources" / f"schedule_base_{scenario_id}.csv"
    path = specific if specific.is_file() else BASE_SCHEDULE
    rows: dict[str, dict[str, str]] = {}
    lines = [
        line
        for line in path.read_text(encoding="utf-8").splitlines()
        if line.strip() and not line.strip().startswith("#")
    ]
    for row in csv.DictReader(lines):
        rows[row["lot"]] = row
    return rows


def graph_file_for_scenario(scenario_id: str) -> str | None:
    specific = ROOT / "resources" / f"campus_drive_graph_scenario_{scenario_id}.geojson"
    if specific.is_file():
        return f"resources/campus_drive_graph_scenario_{scenario_id}.geojson"
    return None


def load_parking_lot(path: Path) -> tuple[dict[str, int], dict[str, int]]:
    delays: dict[str, int] = {}
    totals: dict[str, int] = {}
    with path.open(encoding="utf-8") as f:
        for row in csv.DictReader(f):
            delays[row["id"]] = int(row["initEventInSec"])
            totals[row["id"]] = int(row["totalEvents"])
    return delays, totals


def make_row(template: dict[str, str], start: datetime, end: datetime, amount: int) -> dict[str, str]:
    row = {k: v for k, v in template.items() if k != "lot"}
    row["startTime"] = format_clock(start)
    row["endTime"] = format_clock(end)
    row["spawningIntervalInMinutes"] = "1"
    row["spawningAmount"] = str(amount)
    return row


def lot_schedule_rows(
    template: dict[str, str], lot_open: datetime, total_events: int
) -> tuple[list[dict[str, str]], datetime]:
    """12 cars/minute. MARS endTime is exclusive (see SOHTrainBox README)."""
    rows: list[dict[str, str]] = []
    latest = lot_open
    cursor = lot_open
    full_minutes = total_events // CARS_PER_MINUTE
    remainder = total_events % CARS_PER_MINUTE

    if full_minutes:
        end = cursor + timedelta(minutes=full_minutes)
        rows.append(make_row(template, cursor, end, CARS_PER_MINUTE))
        latest = max(latest, end - timedelta(minutes=1))
        cursor = end

    if remainder:
        end = cursor + timedelta(minutes=1)
        rows.append(make_row(template, cursor, end, remainder))
        latest = max(latest, cursor)

    return rows, latest


def build_schedule_rows(
    base: dict[str, dict[str, str]], delays: dict[str, int], totals: dict[str, int]
) -> tuple[list[dict[str, str]], datetime, int]:
    out: list[dict[str, str]] = []
    latest_end = SIM_START
    spawn_total = 0
    for lot in LOT_ORDER:
        src = base[lot]
        lot_open = SIM_START + timedelta(seconds=delays.get(lot, 0))
        lot_rows, lot_end = lot_schedule_rows(src, lot_open, totals[lot])
        out.extend(lot_rows)
        latest_end = max(latest_end, lot_end)
        spawn_total += totals[lot]
    return out, latest_end, spawn_total


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


def write_config(
    scenario_id: str,
    schedule_rel: str,
    end_dt: datetime,
    agent_count: int,
    graph_rel: str | None = None,
) -> None:
    if BASE_CONFIG.is_file():
        cfg = json.loads(BASE_CONFIG.read_text(encoding="utf-8"))
    else:
        raise FileNotFoundError(f"Missing base config: {BASE_CONFIG}")

    cfg["id"] = f"scenario_{scenario_id}"
    cfg["globals"]["deltaT"] = 1
    cfg["globals"]["endPoint"] = (SIM_START + (end_dt - SIM_START) + EVAC_BUFFER).isoformat(timespec="seconds")
    if "csvOptions" not in cfg["globals"]:
        cfg["globals"]["csvOptions"] = {}
    cfg["globals"]["csvOptions"]["outputPath"] = f"results/scenario_{scenario_id}"

    for layer in cfg.get("layers", []):
        if layer.get("name") == "CarletonCarDriverSchedulerLayer":
            layer["file"] = schedule_rel.replace("\\", "/")
        if graph_rel and layer.get("name") == "CarLayer":
            layer["file"] = graph_rel.replace("\\", "/")

    for agent in cfg.get("agents", []):
        if agent.get("name") == "CarDriver":
            agent["count"] = agent_count

    CONFIGS_DIR.mkdir(parents=True, exist_ok=True)
    out = CONFIGS_DIR / f"config_scenario_{scenario_id}.json"
    out.write_text(json.dumps(cfg, indent=2) + "\n", encoding="utf-8")


def main() -> None:
    OUT_DIR.mkdir(parents=True, exist_ok=True)

    for delay_file in sorted(DELAYS_DIR.glob("scenario_*.csv")):
        scenario_id = delay_file.stem.replace("scenario_", "")
        base = load_base_rows(scenario_id)
        delays, totals = load_parking_lot(delay_file)
        rows, latest_end, spawn_total = build_schedule_rows(base, delays, totals)
        schedule_path = OUT_DIR / f"scenario_{scenario_id}_schedule.csv"
        write_schedule(schedule_path, rows)
        rel = f"resources/schedules/scenario_{scenario_id}_schedule.csv"
        graph_rel = graph_file_for_scenario(scenario_id)
        write_config(scenario_id, rel, latest_end, spawn_total, graph_rel)
        extra = f", graph={graph_rel}" if graph_rel else ""
        print(
            f"scenario_{scenario_id}: {spawn_total} spawns, deploy ends ~{format_clock(latest_end)}, "
            f"wrote {schedule_path.name}{extra}"
        )

    base01 = load_base_rows("01")
    delays01, totals01 = load_parking_lot(DELAYS_DIR / "scenario_01.csv")
    rows01, _, _ = build_schedule_rows(base01, delays01, totals01)
    legacy = ROOT / "resources" / "car_driver_schedule.csv"
    write_schedule(legacy, rows01)
    print(f"Updated legacy {legacy.name}")


if __name__ == "__main__":
    main()
