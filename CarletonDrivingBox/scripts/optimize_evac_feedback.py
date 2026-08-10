#!/usr/bin/env python3
"""Feedback-loop evacuator: propose -> MARS sim -> analyze -> beat best clearance.

Closed loop over lot destinations + spawn start times. Each iteration writes a
candidate schedule/config, runs MARS + analyze_run, records metrics, then
proposes the next candidate from latest-run feedback (stuck / last-clearing
lots) aiming to beat best-so-far.

Search knobs ONLY:
  1. Destinations: each of P1-P7 -> Hogs Back Plaza (SW) or Brewer (NE)
     Defaults: P1/P2 SW, P3/P4 NE; P5/P6/P7 free (all lots may flip).
  2. Start times: one-shot HH:MM,HH:MM,-1 windows from
     {06:01, 06:30, 07:00, 07:30, 08:00} - never interval=1; never exact 06:00.

Objective:
  - Full clear (remaining==0) only counts as success.
  - Minimize evac_end_s among cleared runs.
  - Incomplete scored as horizon + remaining*1000 (worse than any clear).

Scenarios 01-12 are never modified. Candidates live under fb_candidates/.

No Optuna/scipy required - informed hill-climb + random restarts using
remaining_by_lot / clearance_by_lot_s from the latest metrics.json.

Examples:
  python scripts/optimize_evac_feedback.py --iterations 20 --horizon 14400
  python scripts/optimize_evac_feedback.py --iterations 5 --seed s10
  python scripts/optimize_evac_feedback.py --dry-run --iterations 8
  python scripts/optimize_evac_feedback.py --analyze-only fb_MMBBBNB_0000000

See docs/evac_route_optimization.md.
"""
from __future__ import annotations

import argparse
import csv
import json
import math
import random
import subprocess
import sys
from dataclasses import dataclass
from datetime import datetime, timedelta
from pathlib import Path
from typing import Any

from mars_agent_outputs import agent_output_path

ROOT = Path(__file__).resolve().parents[1]
SCHEDULES = ROOT / "resources" / "schedules" / "fb_candidates"
OUT_CONFIGS = ROOT / "configs" / "fb_candidates"
OUT_RESULTS = ROOT / "results" / "fb_candidates"
LEADERBOARD = OUT_RESULTS / "leaderboard.csv"
HISTORY = OUT_RESULTS / "history.jsonl"
PARKING_SPAWNS = ROOT / "resources" / "parking_lot_spawns.csv"
BASE_CONFIG = ROOT / "configs" / "config_scenario_12.json"
PROJECT = ROOT / "SOHCarletonDrivingBox.csproj"
ANALYZE_SCRIPT = ROOT / "scripts" / "analyze_run.py"

# Hogs Back Plaza (888 Meadowlands Dr E). Internal dest key "meadowlands" keeps
# candidate codes (M/B) stable; clearance metrics still use campus exits.
HOGS_BACK_PLAZA = ("45.367764", "-75.702286")
MEADOWLANDS = HOGS_BACK_PLAZA  # alias
BREWER = ("45.387983", "-75.690183")

LOT_COUNTS = {"P1": 100, "P2": 100, "P3": 200, "P4": 100, "P5": 700, "P6": 900, "P7": 1100}
LOT_ORDER = ["P1", "P2", "P3", "P4", "P5", "P6", "P7"]

# Default exits (still mutable in search).
DEFAULT_DEST = {
    "P1": "meadowlands",
    "P2": "meadowlands",
    "P3": "brewer",
    "P4": "brewer",
    "P5": "brewer",
    "P6": "brewer",
    "P7": "brewer",
}

START_TIMES = ("06:01", "06:30", "07:00", "07:30", "08:00")
DEST_CODE = {"meadowlands": "M", "brewer": "B"}
CODE_DEST = {v: k for k, v in DEST_CODE.items()}
TIME_IDX = {t: str(i) for i, t in enumerate(START_TIMES)}
IDX_TIME = {str(i): t for i, t in enumerate(START_TIMES)}

DEFAULT_HORIZON_S = 14400
REMAINING_PENALTY = 1000
RESTART_PROB = 0.18

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
    "spawningIntervalInMinutes": "-1",
    "carType": "Golf",
    "maxSpeed": "13.89",
    "averageSpeed": "13.89",
    "trafficCode": "german",
    "osmRoute": "",
    "nextTrafficLightPhase": "",
    "driveMode": "3",
}

LEADERBOARD_FIELDS = [
    "name",
    "iteration",
    "score",
    "evac_end_s",
    "remaining_on_campus",
    "campus_cleared",
    "dest",
    "starts",
    "changed",
    "eval_status",
]


@dataclass(frozen=True)
class Candidate:
    """Whole-lot destination + one-shot start time per parking lot."""

    dest: tuple[str, ...]  # meadowlands|brewer per LOT_ORDER
    start: tuple[str, ...]  # HH:MM per LOT_ORDER

    def __post_init__(self) -> None:
        if len(self.dest) != len(LOT_ORDER) or len(self.start) != len(LOT_ORDER):
            raise ValueError("dest/start must cover P1-P7")
        for d in self.dest:
            if d not in DEST_CODE:
                raise ValueError(f"bad dest {d!r}")
        for t in self.start:
            if t not in TIME_IDX:
                raise ValueError(f"bad start {t!r} (never use 06:00; use 06:01+)")

    @classmethod
    def from_maps(cls, dest: dict[str, str], start: dict[str, str]) -> Candidate:
        return cls(
            dest=tuple(dest[lot] for lot in LOT_ORDER),
            start=tuple(start[lot] for lot in LOT_ORDER),
        )

    def dest_map(self) -> dict[str, str]:
        return {lot: self.dest[i] for i, lot in enumerate(LOT_ORDER)}

    def start_map(self) -> dict[str, str]:
        return {lot: self.start[i] for i, lot in enumerate(LOT_ORDER)}

    def name(self) -> str:
        dcode = "".join(DEST_CODE[d] for d in self.dest)
        tcode = "".join(TIME_IDX[t] for t in self.start)
        return f"fb_{dcode}_{tcode}"

    def dest_str(self) -> str:
        return ",".join(f"{lot}={DEST_CODE[self.dest[i]]}" for i, lot in enumerate(LOT_ORDER))

    def start_str(self) -> str:
        return ",".join(f"{lot}={self.start[i]}" for i, lot in enumerate(LOT_ORDER))


@dataclass
class EvalResult:
    candidate: Candidate
    name: str
    score: float
    evac_end_s: int | None
    remaining_on_campus: int
    campus_cleared: bool
    remaining_by_lot: dict[str, int]
    clearance_by_lot_s: dict[str, int | None]
    eval_status: str
    changed: str
    iteration: int


def seed_s01() -> Candidate:
    """Baseline all-early like scenario_01."""
    return Candidate.from_maps(
        dict(DEFAULT_DEST),
        {lot: "06:01" for lot in LOT_ORDER},
    )


def seed_s10() -> Candidate:
    """Scenario-10 style: P6 -> Hogs Back Plaza (SW), others default, all early."""
    dest = dict(DEFAULT_DEST)
    dest["P6"] = "meadowlands"
    return Candidate.from_maps(dest, {lot: "06:01" for lot in LOT_ORDER})


def seed_s12_binary() -> Candidate:
    """Binary stand-in for s12 (no P7 split): P6->SW, P7 stays NE, all early."""
    return seed_s10()


SEEDS = {
    "s01": seed_s01,
    "s10": seed_s10,
    "s12": seed_s12_binary,
}


def parse_candidate_name(name: str) -> Candidate:
    raw = name.strip()
    if raw.endswith("_schedule.csv"):
        raw = raw[: -len("_schedule.csv")]
    if raw.startswith("config_"):
        raw = raw[len("config_") :]
    if not raw.startswith("fb_"):
        raise SystemExit(f"Cannot parse candidate name {name!r} (expected fb_…)")
    body = raw[len("fb_") :]
    parts = body.split("_")
    if len(parts) != 2 or len(parts[0]) != 7 or len(parts[1]) != 7:
        raise SystemExit(f"Cannot parse candidate name {name!r} (expected fb_DDDDDDD_TTTTTTT)")
    dcode, tcode = parts
    try:
        dest = tuple(CODE_DEST[c] for c in dcode)
        start = tuple(IDX_TIME[c] for c in tcode)
    except KeyError as exc:
        raise SystemExit(f"Cannot parse candidate name {name!r}: {exc}") from exc
    return Candidate(dest=dest, start=start)


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


def plan_rows(
    coords: dict[str, tuple[float, float]], cand: Candidate
) -> list[dict[str, str]]:
    rows: list[dict[str, str]] = []
    dest_m = cand.dest_map()
    start_m = cand.start_map()
    for lot in LOT_ORDER:
        slat, slon = coords[lot]
        hhmm = start_m[lot]
        exit_name = dest_m[lot]
        dlat, dlon = MEADOWLANDS if exit_name == "meadowlands" else BREWER
        row = {
            **CAR_DEFAULTS,
            "startTime": hhmm,
            "endTime": hhmm,
            "spawningAmount": str(LOT_COUNTS[lot]),
            "startLat": f"{slat:.7f}",
            "startLon": f"{slon:.7f}",
            "destLat": dlat,
            "destLon": dlon,
        }
        # Guard: one-shot only.
        if row["spawningIntervalInMinutes"] != "-1":
            raise SystemExit("BUG: interval must be -1 (one-shot)")
        if hhmm == "06:00":
            raise SystemExit("BUG: never spawn at exact 06:00")
        rows.append(row)
    return rows


def write_schedule(path: Path, rows: list[dict[str, str]]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("w", newline="", encoding="utf-8") as f:
        w = csv.DictWriter(f, fieldnames=HEADER, lineterminator="\n")
        w.writeheader()
        for row in rows:
            w.writerow({k: row.get(k, "") for k in HEADER})


def opt_end_point(start_point: str, horizon_s: int) -> str:
    start_dt = datetime.fromisoformat(str(start_point).replace("Z", ""))
    end_dt = start_dt + timedelta(seconds=int(horizon_s))
    return end_dt.strftime("%Y-%m-%dT%H:%M:%S")


def write_config(name: str, schedule_rel: str, horizon_s: int) -> Path:
    OUT_CONFIGS.mkdir(parents=True, exist_ok=True)
    cfg = json.loads(BASE_CONFIG.read_text(encoding="utf-8"))
    cfg["id"] = name
    globals_ = cfg.setdefault("globals", {})
    start = globals_.get("startPoint", "2021-10-11T06:00:00")
    globals_["endPoint"] = opt_end_point(start, horizon_s)
    csv_opts = globals_.setdefault("csvOptions", {})
    csv_opts["outputPath"] = f"results/fb_candidates/{name}"
    for layer in cfg.get("layers") or []:
        if layer.get("name") == "CarletonCarDriverSchedulerLayer":
            layer["file"] = schedule_rel
    path = OUT_CONFIGS / f"config_{name}.json"
    path.write_text(json.dumps(cfg, indent=2) + "\n", encoding="utf-8")
    return path


def materialize(
    coords: dict[str, tuple[float, float]], cand: Candidate, horizon_s: int
) -> str:
    rows = plan_rows(coords, cand)
    total = sum(int(r["spawningAmount"]) for r in rows)
    if total != 3200:
        raise SystemExit(f"{cand.name()}: expected 3200 agents, got {total}")
    name = cand.name()
    sched_rel = f"resources/schedules/fb_candidates/{name}_schedule.csv"
    write_schedule(ROOT / sched_rel, rows)
    write_config(name, sched_rel, horizon_s)
    return name


def objective_score(
    *,
    campus_cleared: bool,
    evac_end_s: int | None,
    remaining: int,
    horizon_s: int,
) -> float:
    """Lower is better. Incomplete always worse than any full clear."""
    if campus_cleared and remaining == 0:
        # Cleared: minimize last leave. Missing evac_end -> treat as horizon.
        return float(evac_end_s if evac_end_s is not None else horizon_s)
    # Incomplete: large penalty (endPoint + remaining*1000).
    return float(horizon_s + max(0, remaining) * REMAINING_PENALTY)


def read_metrics(results_dir: Path) -> dict[str, Any]:
    metrics_path = results_dir / "metrics.json"
    if not metrics_path.is_file():
        return {}
    try:
        return json.loads(metrics_path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError):
        return {}


def extract_eval(
    cand: Candidate,
    payload: dict[str, Any],
    *,
    horizon_s: int,
    changed: str,
    iteration: int,
    eval_status: str = "sim_done",
) -> EvalResult:
    m = payload.get("metrics") or {}
    remaining = int(m.get("remaining_on_campus") or 0)
    cleared = bool(m.get("campus_cleared")) and remaining == 0
    evac = m.get("evac_end_s")
    try:
        evac_i = int(evac) if evac not in (None, "") else None
    except (TypeError, ValueError):
        evac_i = None

    rem_by_lot: dict[str, int] = {}
    completed = payload.get("completed_by_lot") or {}
    for lot in LOT_ORDER:
        entry = completed.get(lot) or {}
        rem_by_lot[lot] = int(entry.get("remaining_on_campus") or 0)

    clearance_raw = payload.get("clearance_by_lot_s") or {}
    clearance: dict[str, int | None] = {}
    for lot in LOT_ORDER:
        val = clearance_raw.get(lot)
        if val in (None, ""):
            clearance[lot] = None
        else:
            try:
                clearance[lot] = int(val)
            except (TypeError, ValueError):
                clearance[lot] = None

    score = objective_score(
        campus_cleared=cleared,
        evac_end_s=evac_i,
        remaining=remaining,
        horizon_s=horizon_s,
    )
    return EvalResult(
        candidate=cand,
        name=cand.name(),
        score=score,
        evac_end_s=evac_i,
        remaining_on_campus=remaining,
        campus_cleared=cleared,
        remaining_by_lot=rem_by_lot,
        clearance_by_lot_s=clearance,
        eval_status=eval_status,
        changed=changed,
        iteration=iteration,
    )


def fake_metrics_for_dry_run(cand: Candidate, horizon_s: int) -> dict[str, Any]:
    """Cheap stand-in so the loop can be exercised without MARS.

    Heuristic: SW overload + late NE spawn mix -> incomplete; otherwise a
    synthetic clearance time. Not used for real ranking decisions in prod.
    """
    dest = cand.dest_map()
    start = cand.start_map()
    sw = sum(LOT_COUNTS[lot] for lot, d in dest.items() if d == "meadowlands")
    # Prefer plans near scenario-10 demand (~1100 SW) with early large lots.
    imbalance = abs(sw - (3200 - sw))
    late_big = sum(
        LOT_COUNTS[lot]
        for lot in ("P5", "P6", "P7")
        if start[lot] >= "07:30"
    )
    remaining = 0
    # Baseline-like NE-heavy early plans clear in this toy model; jam on
    # heavy SW overload or extreme late big-lot releases.
    if sw > 2000:
        remaining = int((sw - 2000) * 0.4) + 50
    elif sw < 200 and late_big > 1800:
        remaining = 120
    elif sw > 1800 and late_big > 1000:
        remaining = 80

    rem_by_lot = {lot: 0 for lot in LOT_ORDER}
    if remaining:
        # Stick the largest destination-side lots first.
        order = sorted(LOT_ORDER, key=lambda L: LOT_COUNTS[L], reverse=True)
        left = remaining
        for lot in order:
            take = min(left, max(1, LOT_COUNTS[lot] // 10))
            rem_by_lot[lot] = take
            left -= take
            if left <= 0:
                break
        remaining = sum(rem_by_lot.values())

    clearance: dict[str, int | None] = {}
    base = 3500 + int(imbalance * 0.15) + int(late_big * 0.2)
    for i, lot in enumerate(LOT_ORDER):
        if rem_by_lot[lot] > 0:
            clearance[lot] = None
        else:
            # Later start -> later clearance.
            t_bonus = START_TIMES.index(start[lot]) * 900
            clearance[lot] = min(horizon_s - 1, base + i * 120 + t_bonus)

    evac_end = None if remaining else max(v for v in clearance.values() if v is not None)
    cleared = remaining == 0
    return {
        "metrics": {
            "remaining_on_campus": remaining,
            "campus_cleared": cleared,
            "evac_end_s": evac_end,
        },
        "completed_by_lot": {
            lot: {
                "remaining_on_campus": rem_by_lot[lot],
                "completed_trips": LOT_COUNTS[lot] - rem_by_lot[lot],
            }
            for lot in LOT_ORDER
        },
        "clearance_by_lot_s": clearance,
    }


def run_sim(name: str) -> int:
    cfg = OUT_CONFIGS / f"config_{name}.json"
    if not cfg.is_file():
        raise SystemExit(f"Missing config: {cfg}")
    out_dir = (OUT_RESULTS / name).resolve()
    out_dir.mkdir(parents=True, exist_ok=True)
    payload = json.loads(cfg.read_text(encoding="utf-8"))
    globals_ = payload.setdefault("globals", {})
    csv_opts = globals_.setdefault("csvOptions", {})
    csv_opts["outputPath"] = str(out_dir)
    run_cfg = out_dir / "_run_config.json"
    run_cfg.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")
    log = out_dir / "sim.log"
    cmd = ["dotnet", "run", "--project", str(PROJECT), "--", str(run_cfg)]
    print(f"WARNING: full MARS sim for {name} typically takes hours.", flush=True)
    print("+", " ".join(cmd), flush=True)
    with log.open("w", encoding="utf-8") as out:
        out.write("+ " + " ".join(cmd) + "\n")
        out.flush()
        proc = subprocess.run(
            cmd,
            cwd=ROOT,
            stdout=out,
            stderr=subprocess.STDOUT,
            text=True,
        )
    return proc.returncode


def run_analyze(name: str) -> int:
    results_dir = OUT_RESULTS / name
    csv_path = agent_output_path(results_dir, ".csv")
    cmd = [sys.executable, str(ANALYZE_SCRIPT), str(csv_path)]
    print("+", " ".join(cmd), flush=True)
    return subprocess.call(cmd, cwd=ROOT)


def focus_lots(last: EvalResult | None) -> list[str]:
    """Lots to mutate: stuck first, else latest-clearing."""
    if last is None:
        return list(LOT_ORDER)
    stuck = [lot for lot in LOT_ORDER if last.remaining_by_lot.get(lot, 0) > 0]
    if stuck:
        # Prefer largest remaining.
        stuck.sort(key=lambda L: last.remaining_by_lot.get(L, 0), reverse=True)
        return stuck

    scored: list[tuple[float, str]] = []
    for lot in LOT_ORDER:
        val = last.clearance_by_lot_s.get(lot)
        scored.append((float(val) if val is not None else -1.0, lot))
    scored.sort(reverse=True)
    # Top late clearers; fall back to all if missing.
    late = [lot for _, lot in scored if _ >= 0]
    return late[:3] if late else list(LOT_ORDER)


def flip_dest(cand: Candidate, lot: str) -> Candidate:
    dest = list(cand.dest)
    idx = LOT_ORDER.index(lot)
    dest[idx] = "brewer" if dest[idx] == "meadowlands" else "meadowlands"
    return Candidate(dest=tuple(dest), start=cand.start)


def shift_time(cand: Candidate, lot: str, *, later: bool = True) -> Candidate | None:
    start = list(cand.start)
    idx = LOT_ORDER.index(lot)
    cur = START_TIMES.index(start[idx])
    nxt = cur + (1 if later else -1)
    if nxt < 0 or nxt >= len(START_TIMES):
        return None
    start[idx] = START_TIMES[nxt]
    return Candidate(dest=cand.dest, start=tuple(start))


def random_candidate(rng: random.Random, *, bias_defaults: bool = True) -> Candidate:
    dest: dict[str, str] = {}
    start: dict[str, str] = {}
    for lot in LOT_ORDER:
        if bias_defaults and lot in ("P1", "P2", "P3", "P4") and rng.random() < 0.7:
            dest[lot] = DEFAULT_DEST[lot]
        else:
            dest[lot] = rng.choice(("meadowlands", "brewer"))
        # Bias early starts; still explore delays.
        weights = [0.45, 0.25, 0.15, 0.10, 0.05]
        start[lot] = rng.choices(START_TIMES, weights=weights, k=1)[0]
    return Candidate.from_maps(dest, start)


def mutate_informed(
    base: Candidate,
    last: EvalResult | None,
    rng: random.Random,
    seen: set[str],
) -> tuple[Candidate, str]:
    """Propose a neighbor of ``base`` using last-run stuck/late-lot feedback."""
    cleared = bool(last and last.campus_cleared)
    for _ in range(40):
        if rng.random() < RESTART_PROB:
            cand = random_candidate(rng)
            if cand.name() not in seen:
                return cand, "random_restart"

        focus = focus_lots(last)
        lot = rng.choice(focus)
        action = rng.random()

        # Incomplete: prefer redirect stuck lots. Cleared: prefer delay late lots.
        if cleared:
            # 55% delay, 25% flip, 20% combo/two-lot
            prefer_delay = action < 0.55
            prefer_flip = 0.55 <= action < 0.80
        else:
            prefer_flip = action < 0.55
            prefer_delay = 0.55 <= action < 0.80

        cand: Candidate | None
        changed: str
        if prefer_flip:
            cand = flip_dest(base, lot)
            changed = f"flip_dest {lot} -> {DEST_CODE[cand.dest_map()[lot]]}"
        elif prefer_delay:
            prefer_later = True
            if last and last.remaining_by_lot.get(lot, 0) == 0:
                if last.clearance_by_lot_s.get(lot) is not None:
                    prefer_later = True
            cand = shift_time(base, lot, later=prefer_later)
            if cand is None:
                cand = shift_time(base, lot, later=not prefer_later)
            if cand is None:
                cand = flip_dest(base, lot)
                changed = f"flip_dest {lot} -> {DEST_CODE[cand.dest_map()[lot]]}"
            else:
                changed = f"shift_time {lot} -> {cand.start_map()[lot]}"
        else:
            # Two-knob move: redirect + delay same stuck lot, or two focus lots.
            if cleared:
                # Cleared: delay primary late lot, optionally flip a second late lot.
                cand = shift_time(base, lot, later=True) or flip_dest(base, lot)
                lot2 = rng.choice(focus)
                if lot2 != lot and rng.random() < 0.5:
                    cand = flip_dest(cand, lot2)
                    changed = (
                        f"delay {lot} @{cand.start_map()[lot]}; "
                        f"flip {lot2} -> {DEST_CODE[cand.dest_map()[lot2]]}"
                    )
                else:
                    changed = f"shift_time {lot} -> {cand.start_map()[lot]}"
            else:
                cand = flip_dest(base, lot)
                delayed = shift_time(cand, lot, later=True)
                if delayed is not None and rng.random() < 0.6:
                    cand = delayed
                    changed = (
                        f"flip+delay {lot} -> {DEST_CODE[cand.dest_map()[lot]]} "
                        f"@ {cand.start_map()[lot]}"
                    )
                else:
                    lot2 = rng.choice(focus)
                    if lot2 != lot:
                        cand = flip_dest(cand, lot2)
                        changed = (
                            f"flip_dest {lot},{lot2} -> "
                            f"{DEST_CODE[cand.dest_map()[lot]]},"
                            f"{DEST_CODE[cand.dest_map()[lot2]]}"
                        )
                    else:
                        changed = f"flip_dest {lot} -> {DEST_CODE[cand.dest_map()[lot]]}"

        if cand.name() not in seen:
            return cand, changed

    # Exhausted neighbors - forced random.
    for _ in range(100):
        cand = random_candidate(rng, bias_defaults=False)
        if cand.name() not in seen:
            return cand, "forced_random"
    raise SystemExit("Could not propose an unseen candidate")


def describe_diff(prev: Candidate | None, cur: Candidate) -> str:
    if prev is None:
        return "seed"
    bits: list[str] = []
    for i, lot in enumerate(LOT_ORDER):
        if prev.dest[i] != cur.dest[i]:
            bits.append(f"{lot}:{DEST_CODE[prev.dest[i]]}->{DEST_CODE[cur.dest[i]]}")
        if prev.start[i] != cur.start[i]:
            bits.append(f"{lot}:{prev.start[i]}->{cur.start[i]}")
    return "; ".join(bits) if bits else "no_change"


def load_leaderboard() -> list[dict[str, str]]:
    if not LEADERBOARD.is_file():
        return []
    with LEADERBOARD.open(encoding="utf-8", newline="") as f:
        return list(csv.DictReader(f))


def write_leaderboard(rows: list[dict[str, Any]]) -> None:
    OUT_RESULTS.mkdir(parents=True, exist_ok=True)

    def sort_key(r: dict) -> tuple:
        try:
            sc = float(r.get("score", math.inf))
        except (TypeError, ValueError):
            sc = math.inf
        cleared = str(r.get("campus_cleared", "")).lower() in ("1", "true", "yes")
        # Cleared first (already encoded in score), then score, then name.
        return (0 if cleared else 1, sc, r.get("name") or "")

    ordered = sorted(rows, key=sort_key)
    with LEADERBOARD.open("w", newline="", encoding="utf-8") as f:
        w = csv.DictWriter(f, fieldnames=LEADERBOARD_FIELDS, lineterminator="\n")
        w.writeheader()
        for row in ordered:
            w.writerow({k: row.get(k, "") for k in LEADERBOARD_FIELDS})


def append_history(row: dict[str, Any]) -> None:
    OUT_RESULTS.mkdir(parents=True, exist_ok=True)
    with HISTORY.open("a", encoding="utf-8") as f:
        f.write(json.dumps(row) + "\n")


def result_to_row(ev: EvalResult) -> dict[str, Any]:
    return {
        "name": ev.name,
        "iteration": ev.iteration,
        "score": f"{ev.score:.1f}",
        "evac_end_s": "" if ev.evac_end_s is None else str(ev.evac_end_s),
        "remaining_on_campus": str(ev.remaining_on_campus),
        "campus_cleared": str(ev.campus_cleared).lower(),
        "dest": ev.candidate.dest_str(),
        "starts": ev.candidate.start_str(),
        "changed": ev.changed,
        "eval_status": ev.eval_status,
    }


def merge_leaderboard_row(ev: EvalResult) -> list[dict[str, Any]]:
    by_name = {r["name"]: r for r in load_leaderboard() if r.get("name")}
    by_name[ev.name] = result_to_row(ev)
    rows = list(by_name.values())
    write_leaderboard(rows)
    return rows


def best_from_rows(rows: list[dict[str, Any]]) -> EvalResult | None:
    if not rows:
        return None
    best_row = None
    best_score = math.inf
    for r in rows:
        try:
            sc = float(r.get("score", math.inf))
        except (TypeError, ValueError):
            continue
        if sc < best_score:
            best_score = sc
            best_row = r
    if best_row is None:
        return None
    try:
        cand = parse_candidate_name(best_row["name"])
    except SystemExit:
        return None
    cleared = str(best_row.get("campus_cleared", "")).lower() in ("1", "true", "yes")
    rem = int(float(best_row.get("remaining_on_campus") or 0))
    evac_raw = best_row.get("evac_end_s")
    evac = int(float(evac_raw)) if evac_raw not in (None, "") else None
    return EvalResult(
        candidate=cand,
        name=cand.name(),
        score=best_score,
        evac_end_s=evac,
        remaining_on_campus=rem,
        campus_cleared=cleared and rem == 0,
        remaining_by_lot={lot: 0 for lot in LOT_ORDER},
        clearance_by_lot_s={lot: None for lot in LOT_ORDER},
        eval_status=best_row.get("eval_status") or "leaderboard",
        changed=best_row.get("changed") or "",
        iteration=int(float(best_row.get("iteration") or 0)),
    )


def print_progress(
    iteration: int,
    total: int,
    current: EvalResult,
    best: EvalResult,
) -> None:
    best_label = (
        f"cleared evac_end={best.evac_end_s}s"
        if best.campus_cleared
        else f"incomplete rem={best.remaining_on_campus} score={best.score:.0f}"
    )
    cur_label = (
        f"cleared evac_end={current.evac_end_s}s"
        if current.campus_cleared
        else f"incomplete rem={current.remaining_on_campus} score={current.score:.0f}"
    )
    beat = current.score + 1e-9 < best.score and current.name != best.name
    print("=" * 72, flush=True)
    print(f"Iteration {iteration}/{total}: {current.name}", flush=True)
    print(f"  changed      : {current.changed}", flush=True)
    print(f"  current      : {cur_label}  (score={current.score:.1f})", flush=True)
    print(f"  best_so_far  : {best.name}  {best_label}  (score={best.score:.1f})", flush=True)
    if beat:
        print("  *** NEW BEST ***", flush=True)
    stuck = [f"{lot}:{n}" for lot, n in current.remaining_by_lot.items() if n]
    if stuck:
        print(f"  stuck_lots   : {', '.join(stuck)}", flush=True)
    late = []
    for lot in LOT_ORDER:
        val = current.clearance_by_lot_s.get(lot)
        if val is not None:
            late.append((val, lot))
    if late and current.campus_cleared:
        late.sort(reverse=True)
        top = ", ".join(f"{lot}@{t}s" for t, lot in late[:3])
        print(f"  last_clear   : {top}", flush=True)
    print("=" * 72, flush=True)


def evaluate_candidate(
    coords: dict[str, tuple[float, float]],
    cand: Candidate,
    *,
    horizon_s: int,
    iteration: int,
    changed: str,
    dry_run: bool,
    skip_sim: bool,
) -> EvalResult:
    name = materialize(coords, cand, horizon_s)
    out_dir = OUT_RESULTS / name
    out_dir.mkdir(parents=True, exist_ok=True)

    if dry_run:
        payload = fake_metrics_for_dry_run(cand, horizon_s)
        (out_dir / "metrics.json").write_text(
            json.dumps(payload, indent=2) + "\n", encoding="utf-8"
        )
        return extract_eval(
            cand,
            payload,
            horizon_s=horizon_s,
            changed=changed,
            iteration=iteration,
            eval_status="dry_run",
        )

    if not skip_sim:
        rc = run_sim(name)
        if rc != 0:
            # Treat sim failure as fully incomplete.
            payload = {
                "metrics": {
                    "remaining_on_campus": 3200,
                    "campus_cleared": False,
                    "evac_end_s": None,
                },
                "completed_by_lot": {
                    lot: {"remaining_on_campus": LOT_COUNTS[lot], "completed_trips": 0}
                    for lot in LOT_ORDER
                },
                "clearance_by_lot_s": {lot: None for lot in LOT_ORDER},
            }
            ev = extract_eval(
                cand,
                payload,
                horizon_s=horizon_s,
                changed=changed,
                iteration=iteration,
                eval_status=f"sim_failed_rc_{rc}",
            )
            merge_leaderboard_row(ev)
            append_history(result_to_row(ev))
            print(f"ERROR: dotnet run failed for {name} (rc={rc}); see {out_dir / 'sim.log'}")
            return ev

    rc = run_analyze(name)
    if rc != 0:
        payload = {
            "metrics": {
                "remaining_on_campus": 3200,
                "campus_cleared": False,
                "evac_end_s": None,
            },
            "completed_by_lot": {
                lot: {"remaining_on_campus": LOT_COUNTS[lot], "completed_trips": 0}
                for lot in LOT_ORDER
            },
            "clearance_by_lot_s": {lot: None for lot in LOT_ORDER},
        }
        ev = extract_eval(
            cand,
            payload,
            horizon_s=horizon_s,
            changed=changed,
            iteration=iteration,
            eval_status=f"analyze_failed_rc_{rc}",
        )
        merge_leaderboard_row(ev)
        append_history(result_to_row(ev))
        print(f"ERROR: analyze_run.py failed for {name} (rc={rc})")
        return ev

    payload = read_metrics(out_dir)
    if not payload:
        raise SystemExit(f"No metrics.json after analyze for {name}")
    return extract_eval(
        cand,
        payload,
        horizon_s=horizon_s,
        changed=changed,
        iteration=iteration,
        eval_status="sim_done",
    )


def run_loop(
    coords: dict[str, tuple[float, float]],
    *,
    iterations: int,
    horizon_s: int,
    seed_name: str,
    dry_run: bool,
    resume: bool,
    rng: random.Random,
) -> EvalResult:
    print(
        f"Feedback loop: iterations={iterations} horizon={horizon_s}s "
        f"seed={seed_name} dry_run={dry_run}",
        flush=True,
    )
    print(
        "Objective: min evac_end_s among remaining==0; "
        f"incomplete = horizon + remaining*{REMAINING_PENALTY}",
        flush=True,
    )
    print("Scenarios 01-12 untouched. Writing under */fb_candidates/.", flush=True)

    seen: set[str] = set()
    board = load_leaderboard()
    if resume:
        for row in board:
            if row.get("name"):
                seen.add(row["name"])

    best = best_from_rows(board) if resume else None
    last: EvalResult | None = None

    if seed_name == "best":
        if best is None:
            raise SystemExit("--seed best requires an existing fb leaderboard entry")
        seed = best.candidate
        changed = "resume_best"
    elif seed_name in SEEDS:
        seed = SEEDS[seed_name]()
        changed = f"seed:{seed_name}"
    else:
        raise SystemExit(f"Unknown seed {seed_name!r}; use s01|s10|s12|best")

    current = seed
    # If resuming and seed already evaluated, jump straight to proposing.
    start_iter = 1
    if resume and seed.name() in seen and best is not None:
        last = best
        current, changed = mutate_informed(best.candidate, last, rng, seen)
        print(f"Resume: starting from mutation of best {best.name}", flush=True)

    for it in range(start_iter, iterations + 1):
        if current.name() in seen and it > 1:
            base = best.candidate if best is not None else current
            current, changed = mutate_informed(base, last, rng, seen)

        seen.add(current.name())
        if last is not None and changed == f"seed:{seed_name}":
            changed = describe_diff(last.candidate, current)

        ev = evaluate_candidate(
            coords,
            current,
            horizon_s=horizon_s,
            iteration=it,
            changed=changed,
            dry_run=dry_run,
            skip_sim=False,
        )
        merge_leaderboard_row(ev)
        append_history(result_to_row(ev))

        if best is None or ev.score + 1e-9 < best.score:
            # Enrich best with full lot feedback from this eval.
            best = ev
        print_progress(it, iterations, ev, best)
        last = ev

        if it < iterations:
            current, changed = mutate_informed(best.candidate, last, rng, seen)

    assert best is not None
    print("\nDone.", flush=True)
    print(f"Leaderboard : {LEADERBOARD}", flush=True)
    print(f"Best        : {best.name}  score={best.score:.1f}  "
          f"cleared={best.campus_cleared}  rem={best.remaining_on_campus}  "
          f"evac_end_s={best.evac_end_s}", flush=True)
    if best.campus_cleared:
        print(
            "Promote to a future scenario 13+ only after beating scenario 12 "
            "in a real (non-dry-run) sim - never overwrite 01-12.",
            flush=True,
        )
    else:
        print(
            "No full clear yet (remaining>0). Incomplete scores use "
            f"horizon+remaining*{REMAINING_PENALTY}. Keep iterating.",
            flush=True,
        )
    return best


def main() -> int:
    ap = argparse.ArgumentParser(
        description=__doc__,
        formatter_class=argparse.RawDescriptionHelpFormatter,
    )
    ap.add_argument(
        "--iterations",
        type=int,
        default=20,
        help="Number of sim+analyze feedback iterations (default: 20)",
    )
    ap.add_argument(
        "--horizon",
        type=int,
        default=DEFAULT_HORIZON_S,
        help=f"Sim horizon seconds; endPoint=startPoint+horizon (default: {DEFAULT_HORIZON_S})",
    )
    ap.add_argument(
        "--seed",
        choices=["s01", "s10", "s12", "best"],
        default="s01",
        help="Starting candidate (default: s01 all-early). s12 is binary P6->SW stand-in.",
    )
    ap.add_argument(
        "--resume",
        action="store_true",
        help="Skip already-evaluated names on the fb leaderboard; seed=best if set",
    )
    ap.add_argument(
        "--dry-run",
        action="store_true",
        help="Exercise the loop with fake metrics (no MARS). For wiring tests only.",
    )
    ap.add_argument(
        "--analyze-only",
        metavar="NAME",
        help="Re-run analyze_run on an existing fb candidate and update leaderboard",
    )
    ap.add_argument(
        "--write-only",
        metavar="NAME",
        help="Materialize schedule+config for NAME (or seed alias) without simulating",
    )
    ap.add_argument("--rng-seed", type=int, default=None, help="RNG seed for mutations")
    args = ap.parse_args()

    coords = load_lot_coords()
    rng = random.Random(args.rng_seed)

    if args.write_only:
        alias = args.write_only.strip().lower()
        if alias in SEEDS:
            cand = SEEDS[alias]()
        else:
            cand = parse_candidate_name(args.write_only)
        name = materialize(coords, cand, args.horizon)
        print(json.dumps({"name": name, "dest": cand.dest_str(), "starts": cand.start_str()}, indent=2))
        return 0

    if args.analyze_only:
        cand = parse_candidate_name(args.analyze_only)
        materialize(coords, cand, args.horizon)
        ev = evaluate_candidate(
            coords,
            cand,
            horizon_s=args.horizon,
            iteration=0,
            changed="analyze_only",
            dry_run=False,
            skip_sim=True,
        )
        merge_leaderboard_row(ev)
        append_history(result_to_row(ev))
        print(json.dumps(result_to_row(ev), indent=2))
        return 0

    if args.iterations < 1:
        raise SystemExit("--iterations must be >= 1")

    seed_name = args.seed
    if args.resume and args.seed == "s01" and best_from_rows(load_leaderboard()) is not None:
        # Convenient: --resume alone continues from best.
        seed_name = "best"

    run_loop(
        coords,
        iterations=args.iterations,
        horizon_s=args.horizon,
        seed_name=seed_name,
        dry_run=args.dry_run,
        resume=args.resume,
        rng=rng,
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
