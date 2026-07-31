#!/usr/bin/env python3
"""Enumerate lot→exit assignment candidates for Carleton evacuation (scaffold).

Does NOT run MARS simulations. Writes schedule CSVs (or prints plans) so you
can evaluate later with existing runners:

  dotnet run --project SOHCarletonDrivingBox.csproj -- <candidate-config.json>
  python3 scripts/analyze_run.py results/opt_candidates/<name>/

See docs/evac_route_optimization.md.
"""
from __future__ import annotations

import argparse
import csv
import json
from dataclasses import dataclass
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SCHEDULES = ROOT / "resources" / "schedules"
OUT_DIR = SCHEDULES / "opt_candidates"
PARKING_SPAWNS = ROOT / "resources" / "parking_lot_spawns.csv"

# Destinations used by scenarios 01–12
MEADOWLANDS = (45.3675, -75.7040)  # SW / Colonel By / University Dr
BREWER = (45.387983, -75.690183)  # NE / Bronson & University

LOT_COUNTS = {"P1": 100, "P2": 100, "P3": 200, "P4": 100, "P5": 700, "P6": 900, "P7": 1100}
LOT_ORDER = ["P1", "P2", "P3", "P4", "P5", "P6", "P7"]

# Defaults: P1/P2 always SW; P3/P4 always NE (scenario 10/12 pattern)
DEFAULT_FIXED = {
    "P1": "meadowlands",
    "P2": "meadowlands",
    "P3": "brewer",
    "P4": "brewer",
}

HEADER = [
    "startTime",
    "endTime",
    "spawningIntervalInMinutes",
    "spawningAmount",
    "carType",
    "maxSpeed",
    "averageSpeed",
    "trafficCode",
    "startLat",
    "startLon",
    "destLat",
    "destLon",
    "osmRoute",
    "nextTrafficLightPhase",
    "driveMode",
]

CAR_DEFAULTS = {
    "startTime": "06:01",
    "endTime": "06:01",
    "spawningIntervalInMinutes": "-1",
    "carType": "Golf",
    "maxSpeed": "13.89",
    "averageSpeed": "13.89",
    "trafficCode": "german",
    "osmRoute": "",
    "nextTrafficLightPhase": "",
    "driveMode": "3",
}


@dataclass(frozen=True)
class ExitFrac:
    """Fraction of a lot sent to Meadowlands (SW); remainder to Brewer (NE)."""

    p5_sw: float
    p6_sw: float
    p7_sw: float

    def name(self) -> str:
        def pct(x: float) -> str:
            return f"{int(round(x * 100)):03d}"

        return f"p5sw{pct(self.p5_sw)}_p6sw{pct(self.p6_sw)}_p7sw{pct(self.p7_sw)}"


# Small hand-picked grid around scenarios 10–12 (not an exhaustive solver)
CANDIDATES: list[ExitFrac] = [
    ExitFrac(0.0, 0.0, 0.0),  # all P5–P7 NE (like 01 for those lots)
    ExitFrac(0.0, 1.0, 0.0),  # scenario 10: P6 SW
    ExitFrac(0.0, 0.0, 0.5),  # scenario 11-ish: P7 split, P6 NE
    ExitFrac(0.0, 1.0, 0.5),  # scenario 12
    ExitFrac(0.0, 1.0, 0.75),
    ExitFrac(0.0, 0.5, 0.5),
    ExitFrac(0.25, 1.0, 0.5),
    ExitFrac(0.5, 1.0, 0.5),
    ExitFrac(0.0, 1.0, 1.0),  # P6+P7 all SW
]


def load_lot_coords() -> dict[str, tuple[float, float]]:
    coords: dict[str, tuple[float, float]] = {}
    with PARKING_SPAWNS.open(encoding="utf-8") as f:
        for row in csv.DictReader(f):
            lot = (row.get("lot") or "").strip()
            if lot:
                coords[lot] = (float(row["spawn_lat"]), float(row["spawn_lon"]))
    missing = [lot for lot in LOT_ORDER if lot not in coords]
    if missing:
        raise SystemExit(f"Missing lot coords in {PARKING_SPAWNS}: {missing}")
    return coords


def dest_for(exit_name: str) -> tuple[float, float]:
    if exit_name == "meadowlands":
        return MEADOWLANDS
    if exit_name == "brewer":
        return BREWER
    raise ValueError(exit_name)


def split_count(total: int, frac_sw: float) -> tuple[int, int]:
    """Return (n_sw, n_ne) summing to total."""
    if frac_sw <= 0:
        return 0, total
    if frac_sw >= 1:
        return total, 0
    n_sw = int(round(total * frac_sw))
    n_sw = max(0, min(total, n_sw))
    return n_sw, total - n_sw


def plan_rows(
    coords: dict[str, tuple[float, float]], frac: ExitFrac
) -> list[dict[str, str]]:
    rows: list[dict[str, str]] = []

    def add(lot: str, n: int, exit_name: str) -> None:
        if n <= 0:
            return
        slat, slon = coords[lot]
        dlat, dlon = dest_for(exit_name)
        row = {**CAR_DEFAULTS, "spawningAmount": str(n)}
        row.update(
            {
                "startLat": f"{slat:.7f}",
                "startLon": f"{slon:.7f}",
                "destLat": f"{dlat:.6f}" if dlat != MEADOWLANDS[0] else f"{dlat:.4f}",
                "destLon": f"{dlon:.4f}",
            }
        )
        # Match existing schedule formatting for known exits
        if exit_name == "meadowlands":
            row["destLat"], row["destLon"] = "45.3675", "-75.7040"
        else:
            row["destLat"], row["destLon"] = "45.387983", "-75.690183"
        rows.append(row)

    for lot, exit_name in DEFAULT_FIXED.items():
        add(lot, LOT_COUNTS[lot], exit_name)

    for lot, sw_frac in (
        ("P5", frac.p5_sw),
        ("P6", frac.p6_sw),
        ("P7", frac.p7_sw),
    ):
        n_sw, n_ne = split_count(LOT_COUNTS[lot], sw_frac)
        add(lot, n_sw, "meadowlands")
        add(lot, n_ne, "brewer")

    return rows


def summarize(frac: ExitFrac) -> dict:
    sw = ne = 0
    for lot, fixed in DEFAULT_FIXED.items():
        n = LOT_COUNTS[lot]
        if fixed == "meadowlands":
            sw += n
        else:
            ne += n
    for lot, sw_frac in (
        ("P5", frac.p5_sw),
        ("P6", frac.p6_sw),
        ("P7", frac.p7_sw),
    ):
        n_sw, n_ne = split_count(LOT_COUNTS[lot], sw_frac)
        sw += n_sw
        ne += n_ne
    return {
        "name": frac.name(),
        "p5_sw": frac.p5_sw,
        "p6_sw": frac.p6_sw,
        "p7_sw": frac.p7_sw,
        "vehicles_sw": sw,
        "vehicles_ne": ne,
        "total": sw + ne,
        "sw_ne_imbalance": abs(sw - ne),
    }


def write_schedule(path: Path, rows: list[dict[str, str]]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("w", newline="", encoding="utf-8") as f:
        w = csv.DictWriter(f, fieldnames=HEADER, lineterminator="\n")
        w.writeheader()
        for row in rows:
            w.writerow({k: row.get(k, "") for k in HEADER})


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument(
        "--write",
        action="store_true",
        help=f"Write CSVs under {OUT_DIR} (default: print plans only)",
    )
    ap.add_argument(
        "--out-dir",
        type=Path,
        default=OUT_DIR,
        help="Output directory for candidate schedules",
    )
    ap.add_argument(
        "--baseline-check",
        action="store_true",
        help="Also print how scenario_12 maps onto the candidate naming",
    )
    args = ap.parse_args()

    coords = load_lot_coords()
    plans = []
    for frac in CANDIDATES:
        rows = plan_rows(coords, frac)
        summary = summarize(frac)
        total_agents = sum(int(r["spawningAmount"]) for r in rows)
        assert total_agents == 3200, f"expected 3200 agents, got {total_agents}"
        plans.append({"summary": summary, "rows": rows})

    print(json.dumps([p["summary"] for p in plans], indent=2))

    if args.baseline_check:
        s12 = summarize(ExitFrac(0.0, 1.0, 0.5))
        print("\nscenario_12 equivalent candidate:", s12["name"])

    if args.write:
        out = args.out_dir
        out.mkdir(parents=True, exist_ok=True)
        for p in plans:
            name = p["summary"]["name"]
            path = out / f"{name}_schedule.csv"
            write_schedule(path, p["rows"])
            print(f"wrote {path}")
        manifest = out / "manifest.json"
        manifest.write_text(
            json.dumps([p["summary"] for p in plans], indent=2) + "\n",
            encoding="utf-8",
        )
        print(f"wrote {manifest}")
        print(
            "\nEvaluate later (do not overwrite scenarios 01–12):\n"
            "  1. Point a copy of configs/config_scenario_12.json at the candidate CSV\n"
            "  2. Set csvOptions.outputPath to results/opt_candidates/<name>/\n"
            "  3. dotnet run --project SOHCarletonDrivingBox.csproj -- <that-config>\n"
            "  4. python3 scripts/analyze_run.py results/opt_candidates/<name>/\n"
        )
    else:
        print("\n(dry-run; pass --write to emit CSVs under resources/schedules/opt_candidates/)")

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
