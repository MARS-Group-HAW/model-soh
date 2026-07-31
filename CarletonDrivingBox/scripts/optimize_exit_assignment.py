#!/usr/bin/env python3
"""Ranked closed-loop optimizer for Carleton lot→exit assignments.

Search space: fractions of P5/P6/P7 sent to Meadowlands (SW). P1/P2 always SW;
P3/P4 always NE. Default mode ranks by a fast demand-balance *filter* proxy
(NOT a clearance guarantee). Ranking prefers low SW overload, then closeness
to the scenario-12 seed, then weaker imbalance. Optional `--eval-sim` /
`--eval-top` / `--max-evals` run real MARS sims (hours each) — only those
prove campus clearance.

Examples:
  python scripts/optimize_exit_assignment.py --optimize --write-top 10
  python scripts/optimize_exit_assignment.py --eval-sim opt_p6sw1_p7sw05_p5sw0
  python scripts/optimize_exit_assignment.py --optimize --eval-top 3

Scenarios 01–12 are never modified. See docs/evac_route_optimization.md.
"""
from __future__ import annotations

import argparse
import csv
import json
import math
import subprocess
import sys
from dataclasses import dataclass
from pathlib import Path

from mars_agent_outputs import agent_output_path

ROOT = Path(__file__).resolve().parents[1]
SCHEDULES = ROOT / "resources" / "schedules"
OUT_SCHEDULES = SCHEDULES / "opt_candidates"
OUT_CONFIGS = ROOT / "configs" / "opt_candidates"
OUT_RESULTS = ROOT / "results" / "opt_candidates"
LEADERBOARD = OUT_RESULTS / "leaderboard.csv"
PARKING_SPAWNS = ROOT / "resources" / "parking_lot_spawns.csv"
BASE_CONFIG = ROOT / "configs" / "config_scenario_12.json"
PROJECT = ROOT / "SOHCarletonDrivingBox.csproj"
ANALYZE_SCRIPT = ROOT / "scripts" / "analyze_run.py"

MEADOWLANDS = (45.3675, -75.7040)
BREWER = (45.387983, -75.690183)

LOT_COUNTS = {"P1": 100, "P2": 100, "P3": 200, "P4": 100, "P5": 700, "P6": 900, "P7": 1100}
LOT_ORDER = ["P1", "P2", "P3", "P4", "P5", "P6", "P7"]

DEFAULT_FIXED = {
    "P1": "meadowlands",
    "P2": "meadowlands",
    "P3": "brewer",
    "P4": "brewer",
}

# Soft SW demand cap: old heavy-SW plans (≈P5 + half P7 + delays) jammed.
SW_SOFT_CAP = 1800
SW_HARD_CAP = 2000
OVERLOAD_SOFT_WEIGHT = 2.0
OVERLOAD_HARD_WEIGHT = 10.0
# Balanced ~1600/1600 with P5→SW still jammed SW approaches / trapped NE lots.
NEAR_SOFT_CAP_START = 1550
NEAR_SOFT_CAP_WEIGHT = 1.5
# Strong preference for plans near scenario 12 (known-good baseline).
S12_SEED_WEIGHT = 250.0
# Imbalance is a weaker filter term (proxy ≠ clearance).
IMBALANCE_WEIGHT = 0.15
# P5→SW historically correlates with corridor traps; keep default grid low.
P5_SW_MAX_DEFAULT = 0.25
P5_SW_PENALTY_WEIGHT = 80.0
# Extra hit when all three big lots leave the s12 pattern.
TRIPLE_DEVIATION_PENALTY = 120.0

GRID_FRACS = (0.0, 0.25, 0.5, 0.75, 1.0)
NEIGHBOR_STEP = 0.25

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

LEADERBOARD_FIELDS = [
    "name",
    "p5_sw",
    "p6_sw",
    "p7_sw",
    "vehicles_sw",
    "vehicles_ne",
    "sw_ne_imbalance",
    "overload_penalty",
    "s12_distance",
    "proxy_score",
    "evac_end_s",
    "eval_status",
]


def _snap(x: float) -> float:
    return round(float(x) * 100.0) / 100.0


def _frac_tag(x: float) -> str:
    """Compact fraction tag: 0→0, 0.5→05, 0.25→025, 1→1."""
    r = _snap(x)
    if r <= 0:
        return "0"
    if r >= 1:
        return "1"
    hundredths = int(round(r * 100))
    if hundredths % 10 == 0:
        return f"{hundredths // 10:02d}"
    return f"{hundredths:03d}"


@dataclass(frozen=True)
class ExitFrac:
    """Fraction of a lot sent to Meadowlands (SW); remainder to Brewer (NE)."""

    p5_sw: float
    p6_sw: float
    p7_sw: float

    def __post_init__(self) -> None:
        object.__setattr__(self, "p5_sw", _snap(self.p5_sw))
        object.__setattr__(self, "p6_sw", _snap(self.p6_sw))
        object.__setattr__(self, "p7_sw", _snap(self.p7_sw))

    def name(self) -> str:
        # Example: opt_p6sw1_p7sw05_p5sw0  (s12 seed)
        return (
            f"opt_p6sw{_frac_tag(self.p6_sw)}"
            f"_p7sw{_frac_tag(self.p7_sw)}"
            f"_p5sw{_frac_tag(self.p5_sw)}"
        )

    def as_tuple(self) -> tuple[float, float, float]:
        return (self.p5_sw, self.p6_sw, self.p7_sw)


# Scenario 12 seed: P6→SW, P7 half/half, P5→NE
S12_SEED = ExitFrac(0.0, 1.0, 0.5)
S12_SEED_NAME = S12_SEED.name()  # opt_p6sw1_p7sw05_p5sw0

PROXY_BANNER = (
    "=" * 72 + "\n"
    "Proxy does NOT guarantee campus clears. Only --eval-sim / real metrics\n"
    "prove clearance. Prefer evaluating scenario-12 seed first.\n"
    f"  Seed: {S12_SEED_NAME}\n"
    + "=" * 72
)


def parse_frac_name(name: str) -> ExitFrac:
    """Parse opt_p6sw…_p7sw…_p5sw… (also accepts bare name without opt_)."""
    raw = name.strip()
    if raw.endswith("_schedule.csv"):
        raw = raw[: -len("_schedule.csv")]
    if raw.startswith("config_"):
        raw = raw[len("config_") :]
    if not raw.startswith("opt_"):
        raw = f"opt_{raw}" if raw.startswith("p6sw") else raw

    def tag_to_frac(tag: str) -> float:
        if tag == "0":
            return 0.0
        if tag == "1":
            return 1.0
        if len(tag) == 2:
            return int(tag) / 10.0
        if len(tag) == 3:
            return int(tag) / 100.0
        raise ValueError(f"bad fraction tag: {tag!r}")

    try:
        # opt_p6swX_p7swY_p5swZ
        body = raw[len("opt_") :]
        parts = body.split("_")
        got: dict[str, float] = {}
        for part in parts:
            if part.startswith("p5sw"):
                got["p5"] = tag_to_frac(part[4:])
            elif part.startswith("p6sw"):
                got["p6"] = tag_to_frac(part[4:])
            elif part.startswith("p7sw"):
                got["p7"] = tag_to_frac(part[4:])
        return ExitFrac(got["p5"], got["p6"], got["p7"])
    except (KeyError, ValueError) as exc:
        raise SystemExit(
            f"Cannot parse candidate name {name!r}. "
            f"Expected e.g. opt_p6sw1_p7sw05_p5sw0 ({exc})"
        ) from exc


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
        row = {**CAR_DEFAULTS, "spawningAmount": str(n)}
        row.update(
            {
                "startLat": f"{slat:.7f}",
                "startLon": f"{slon:.7f}",
            }
        )
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


def demand_totals(frac: ExitFrac) -> tuple[int, int]:
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
    return sw, ne


def overload_penalty(sw: int) -> float:
    if sw <= SW_SOFT_CAP:
        return 0.0
    if sw <= SW_HARD_CAP:
        return (sw - SW_SOFT_CAP) * OVERLOAD_SOFT_WEIGHT
    return (SW_HARD_CAP - SW_SOFT_CAP) * OVERLOAD_SOFT_WEIGHT + (
        sw - SW_HARD_CAP
    ) * OVERLOAD_HARD_WEIGHT


def near_soft_cap_penalty(sw: int) -> float:
    """Penalize SW demand approaching soft cap (1600/1600 can still jam)."""
    if sw <= NEAR_SOFT_CAP_START:
        return 0.0
    # Cap contribution once hard soft-cap overload kicks in (avoid double-count).
    ceiling = min(sw, SW_SOFT_CAP)
    return (ceiling - NEAR_SOFT_CAP_START) * NEAR_SOFT_CAP_WEIGHT


def s12_distance(frac: ExitFrac) -> float:
    """L1 distance in fraction-space from scenario 12 seed."""
    return (
        abs(frac.p5_sw - S12_SEED.p5_sw)
        + abs(frac.p6_sw - S12_SEED.p6_sw)
        + abs(frac.p7_sw - S12_SEED.p7_sw)
    )


def pattern_penalty(frac: ExitFrac) -> float:
    """Penalize P5→SW and plans that leave s12 on all three big lots."""
    p5_pen = P5_SW_PENALTY_WEIGHT * frac.p5_sw
    d5 = abs(frac.p5_sw - S12_SEED.p5_sw) > 1e-9
    d6 = abs(frac.p6_sw - S12_SEED.p6_sw) > 1e-9
    d7 = abs(frac.p7_sw - S12_SEED.p7_sw) > 1e-9
    triple = TRIPLE_DEVIATION_PENALTY if (d5 and d6 and d7) else 0.0
    return p5_pen + triple


def proxy_score(frac: ExitFrac) -> dict:
    sw, ne = demand_totals(frac)
    imbalance = abs(sw - ne)
    overload = overload_penalty(sw)
    near = near_soft_cap_penalty(sw)
    dist = s12_distance(frac)
    pattern = pattern_penalty(frac)
    # Scalar used by hill-climb; mirrors rank priority (overload ≫ s12 ≫ imbalance).
    score = (
        overload
        + near
        + S12_SEED_WEIGHT * dist
        + IMBALANCE_WEIGHT * float(imbalance)
        + pattern
    )
    return {
        "name": frac.name(),
        "p5_sw": frac.p5_sw,
        "p6_sw": frac.p6_sw,
        "p7_sw": frac.p7_sw,
        "vehicles_sw": sw,
        "vehicles_ne": ne,
        "total": sw + ne,
        "sw_ne_imbalance": imbalance,
        "overload_penalty": round(overload + near, 3),
        "s12_distance": round(dist, 3),
        "proxy_score": round(score, 3),
        "evac_end_s": "",
        "eval_status": "proxy_only",
    }


def proxy_rank_key(row: dict) -> tuple:
    """Sort: (1) low overload, (2) close to s12, (3) weaker imbalance."""
    try:
        overload = float(row.get("overload_penalty", math.inf))
    except (TypeError, ValueError):
        overload = math.inf
    try:
        dist = float(row.get("s12_distance", math.inf))
    except (TypeError, ValueError):
        dist = math.inf
    try:
        imb = float(row.get("sw_ne_imbalance", math.inf))
    except (TypeError, ValueError):
        imb = math.inf
    return (overload, dist, imb, row.get("name") or "")


def prefer_s12_first(rows: list[dict]) -> list[dict]:
    """Always place scenario-12 seed first regardless of proxy score."""
    seed = [r for r in rows if r.get("name") == S12_SEED_NAME]
    rest = [r for r in rows if r.get("name") != S12_SEED_NAME]
    if not seed:
        seed = [proxy_score(S12_SEED)]
    return seed + rest


def coarse_grid() -> list[ExitFrac]:
    out: list[ExitFrac] = []
    for p5 in GRID_FRACS:
        if p5 > P5_SW_MAX_DEFAULT + 1e-9:
            continue
        for p6 in GRID_FRACS:
            for p7 in GRID_FRACS:
                out.append(ExitFrac(p5, p6, p7))
    return out


def neighbors(frac: ExitFrac, step: float = NEIGHBOR_STEP) -> list[ExitFrac]:
    seen = {frac.as_tuple()}
    out: list[ExitFrac] = []
    for axis in range(3):
        for delta in (-step, step):
            vals = list(frac.as_tuple())
            vals[axis] = _snap(vals[axis] + delta)
            if vals[axis] < 0.0 or vals[axis] > 1.0:
                continue
            key = (vals[0], vals[1], vals[2])
            if key in seen:
                continue
            seen.add(key)
            out.append(ExitFrac(*vals))
    return out


def write_schedule(path: Path, rows: list[dict[str, str]]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("w", newline="", encoding="utf-8") as f:
        w = csv.DictWriter(f, fieldnames=HEADER, lineterminator="\n")
        w.writeheader()
        for row in rows:
            w.writerow({k: row.get(k, "") for k in HEADER})


def write_config(name: str, schedule_rel: str) -> Path:
    OUT_CONFIGS.mkdir(parents=True, exist_ok=True)
    cfg = json.loads(BASE_CONFIG.read_text(encoding="utf-8"))
    cfg["id"] = name
    globals_ = cfg.setdefault("globals", {})
    csv_opts = globals_.setdefault("csvOptions", {})
    # Relative path is portable in committed configs; run_sim absolutizes for MARS.
    csv_opts["outputPath"] = f"results/opt_candidates/{name}"
    for layer in cfg.get("layers") or []:
        if layer.get("name") == "CarletonCarDriverSchedulerLayer":
            layer["file"] = schedule_rel
    path = OUT_CONFIGS / f"config_{name}.json"
    path.write_text(json.dumps(cfg, indent=2) + "\n", encoding="utf-8")
    return path


def materialize_candidate(
    coords: dict[str, tuple[float, float]], frac: ExitFrac
) -> dict:
    rows = plan_rows(coords, frac)
    total = sum(int(r["spawningAmount"]) for r in rows)
    if total != 3200:
        raise SystemExit(f"{frac.name()}: expected 3200 agents, got {total}")
    summary = proxy_score(frac)
    name = summary["name"]
    sched_rel = f"resources/schedules/opt_candidates/{name}_schedule.csv"
    sched_path = ROOT / sched_rel
    write_schedule(sched_path, rows)
    write_config(name, sched_rel)
    return summary


def load_leaderboard() -> dict[str, dict]:
    rows: dict[str, dict] = {}
    if not LEADERBOARD.is_file():
        return rows
    with LEADERBOARD.open(encoding="utf-8", newline="") as f:
        for row in csv.DictReader(f):
            name = (row.get("name") or "").strip()
            if name:
                rows[name] = row
    return rows


def write_leaderboard(rows: list[dict]) -> Path:
    OUT_RESULTS.mkdir(parents=True, exist_ok=True)
    # Prefer real evals when sorting for display: lower evac_end_s first, else proxy.
    def sort_key(r: dict) -> tuple:
        evac = r.get("evac_end_s")
        try:
            evac_f = float(evac) if evac not in (None, "") else math.inf
        except (TypeError, ValueError):
            evac_f = math.inf
        try:
            proxy = float(r.get("proxy_score", math.inf))
        except (TypeError, ValueError):
            proxy = math.inf
        return (evac_f, proxy, r.get("name") or "")

    ordered = sorted(rows, key=sort_key)
    with LEADERBOARD.open("w", newline="", encoding="utf-8") as f:
        w = csv.DictWriter(f, fieldnames=LEADERBOARD_FIELDS, lineterminator="\n")
        w.writeheader()
        for row in ordered:
            w.writerow({k: row.get(k, "") for k in LEADERBOARD_FIELDS})
    return LEADERBOARD


def merge_leaderboard(updates: list[dict]) -> list[dict]:
    by_name = load_leaderboard()
    for upd in updates:
        name = upd["name"]
        prev = by_name.get(name, {})
        merged = {**prev, **{k: upd.get(k, prev.get(k, "")) for k in LEADERBOARD_FIELDS}}
        # Preserve prior evac_end_s if new update is proxy-only and old had a value
        if (
            upd.get("eval_status") == "proxy_only"
            and prev.get("evac_end_s") not in (None, "")
            and upd.get("evac_end_s") in (None, "")
        ):
            merged["evac_end_s"] = prev["evac_end_s"]
            merged["eval_status"] = prev.get("eval_status", "sim_done")
        by_name[name] = merged
    rows = list(by_name.values())
    write_leaderboard(rows)
    return rows


def read_evac_end_s(results_dir: Path) -> int | None:
    metrics = results_dir / "metrics.json"
    if metrics.is_file():
        try:
            payload = json.loads(metrics.read_text(encoding="utf-8"))
            val = (payload.get("metrics") or {}).get("evac_end_s")
            if val is not None and val != "":
                return int(val)
        except (OSError, json.JSONDecodeError, TypeError, ValueError):
            pass
    summary = results_dir / "summary.csv"
    if summary.is_file():
        with summary.open(encoding="utf-8", newline="") as f:
            for row in csv.DictReader(f):
                val = row.get("evac_end_s")
                if val not in (None, ""):
                    try:
                        return int(float(val))
                    except ValueError:
                        return None
    return None


def run_sim(name: str) -> int:
    cfg = OUT_CONFIGS / f"config_{name}.json"
    if not cfg.is_file():
        raise SystemExit(f"Missing config: {cfg}")
    out_dir = (OUT_RESULTS / name).resolve()
    out_dir.mkdir(parents=True, exist_ok=True)
    # Absolutize outputPath (and leave a copy under the results dir) so MARS
    # always writes CSV/trips into <name>/ even if cwd differs.
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
    # Pass the agent CSV so output_dir is <name>/ (not its parent). Directory
    # args also work after analyze_run's dir handling, but CSV is unambiguous.
    csv_path = agent_output_path(results_dir, ".csv")
    cmd = [sys.executable, str(ANALYZE_SCRIPT), str(csv_path)]
    print("+", " ".join(cmd), flush=True)
    return subprocess.call(cmd, cwd=ROOT)


def eval_sim_candidate(
    coords: dict[str, tuple[float, float]], name: str, *, skip_sim: bool = False
) -> dict:
    frac = parse_frac_name(name)
    summary = materialize_candidate(coords, frac)
    canonical = summary["name"]
    if not skip_sim:
        rc = run_sim(canonical)
        if rc != 0:
            summary["eval_status"] = f"sim_failed_rc_{rc}"
            merge_leaderboard([summary])
            raise SystemExit(f"dotnet run failed for {canonical} (rc={rc}); see results/opt_candidates/{canonical}/sim.log")
    rc = run_analyze(canonical)
    if rc != 0:
        summary["eval_status"] = f"analyze_failed_rc_{rc}"
        merge_leaderboard([summary])
        raise SystemExit(f"analyze_run.py failed for {canonical} (rc={rc})")
    evac = read_evac_end_s(OUT_RESULTS / canonical)
    summary["evac_end_s"] = evac if evac is not None else ""
    summary["eval_status"] = "sim_done" if evac is not None else "analyze_no_evac"
    merge_leaderboard([summary])
    print(
        json.dumps(
            {
                "name": canonical,
                "proxy_score": summary["proxy_score"],
                "evac_end_s": summary["evac_end_s"],
                "eval_status": summary["eval_status"],
            },
            indent=2,
        )
    )
    return summary


def rank_all() -> list[dict]:
    scored = [proxy_score(f) for f in coarse_grid()]
    scored.sort(key=proxy_rank_key)
    return prefer_s12_first(scored)


def hill_climb_proxy(start: ExitFrac, rounds: int = 8) -> list[ExitFrac]:
    """Local search around start using proxy score; returns unique visited bests path."""
    current = start
    best = start
    best_score = proxy_score(best)["proxy_score"]
    visited = {best.as_tuple()}
    path = [best]
    for _ in range(rounds):
        improved = False
        for nb in neighbors(current):
            if nb.as_tuple() in visited:
                continue
            visited.add(nb.as_tuple())
            sc = proxy_score(nb)["proxy_score"]
            if sc < best_score - 1e-9:
                best = nb
                best_score = sc
                current = nb
                path.append(nb)
                improved = True
                break
        if not improved:
            # Try best neighbor even if not improving global best (explore)
            cands = [nb for nb in neighbors(current) if nb.as_tuple() not in visited]
            if not cands:
                break
            cands.sort(key=lambda f: proxy_score(f)["proxy_score"])
            current = cands[0]
            visited.add(current.as_tuple())
            path.append(current)
            sc = proxy_score(current)["proxy_score"]
            if sc < best_score - 1e-9:
                best = current
                best_score = sc
    return path


def optimize_and_write(
    coords: dict[str, tuple[float, float]],
    write_top: int,
    eval_top: int,
    max_evals: int,
) -> list[dict]:
    print(PROXY_BANNER, flush=True)
    ranked = rank_all()
    # Enrich with hill-climb around s12 and around current proxy best (after seed)
    extra: list[ExitFrac] = []
    extra.extend(hill_climb_proxy(S12_SEED))
    # First non-seed row is the best proxy alternative under the new rank
    alt = next((r for r in ranked if r["name"] != S12_SEED_NAME), None)
    if alt is not None:
        best_frac = ExitFrac(alt["p5_sw"], alt["p6_sw"], alt["p7_sw"])
        extra.extend(hill_climb_proxy(best_frac))
    by_name = {r["name"]: r for r in ranked}
    for frac in extra:
        s = proxy_score(frac)
        by_name[s["name"]] = s
    ranked = prefer_s12_first(sorted(by_name.values(), key=proxy_rank_key))

    top_n = max(write_top, eval_top, 0)
    # Always write s12 first, then remaining by new rank (skip duplicate seed).
    selected_names: list[str] = [S12_SEED_NAME]
    for row in ranked:
        if row["name"] == S12_SEED_NAME:
            continue
        if len(selected_names) >= max(top_n, 1):
            break
        selected_names.append(row["name"])

    written: list[dict] = []
    for name in selected_names:
        frac = parse_frac_name(name)
        written.append(materialize_candidate(coords, frac))

    # Always ensure s12 seed is on disk even if somehow not in top-K
    seed_row = materialize_candidate(coords, S12_SEED)
    if seed_row["name"] not in {r["name"] for r in written}:
        written = [seed_row] + written

    merge_leaderboard(written + ranked[:50])  # keep a wider proxy view on board

    print(f"Wrote {len(written)} candidate schedules under {OUT_SCHEDULES}")
    print(f"Wrote configs under {OUT_CONFIGS}")
    print(f"Leaderboard: {LEADERBOARD}")
    print("\nTop proxy-ranked candidates (s12 seed forced #1):")
    for i, row in enumerate(ranked[: max(write_top, 10)], 1):
        print(
            f"  {i:2d}. {row['name']}  proxy={row['proxy_score']:.1f}  "
            f"SW={row['vehicles_sw']} NE={row['vehicles_ne']}  "
            f"imbalance={row['sw_ne_imbalance']} overload={row['overload_penalty']}  "
            f"s12_dist={row['s12_distance']}"
        )

    to_eval: list[str] = []
    if eval_top > 0:
        print(
            f"\nWARNING: --eval-top {eval_top} will run full MARS sims "
            f"(typically hours each).",
            flush=True,
        )
        # Always eval s12 seed first, then remaining by new rank.
        to_eval = [S12_SEED_NAME]
        for row in ranked:
            if row["name"] == S12_SEED_NAME:
                continue
            if len(to_eval) >= eval_top:
                break
            to_eval.append(row["name"])
        print(f"Eval order: {to_eval}", flush=True)
    elif max_evals > 0:
        print(
            f"\nWARNING: --max-evals {max_evals} hill-climb with real sims "
            f"(typically hours each).",
            flush=True,
        )
        # Start from s12 seed, then climb using real/proxy objective
        evaluated: dict[str, dict] = {}
        # Always eval seed first
        order = [S12_SEED] + sorted(
            neighbors(S12_SEED), key=lambda f: proxy_score(f)["proxy_score"]
        )
        seen_eval: set[str] = set()
        queue = list(order)
        while queue and len(seen_eval) < max_evals:
            frac = queue.pop(0)
            if frac.name() in seen_eval:
                continue
            seen_eval.add(frac.name())
            summary = eval_sim_candidate(coords, frac.name())
            evaluated[frac.name()] = summary
            # Expand around best real clearance (or proxy if missing)
            def real_key(s: dict) -> tuple:
                ev = s.get("evac_end_s")
                try:
                    ev_f = float(ev) if ev not in (None, "") else math.inf
                except (TypeError, ValueError):
                    ev_f = math.inf
                return (ev_f, float(s.get("proxy_score", math.inf)))

            best_name = min(evaluated, key=lambda n: real_key(evaluated[n]))
            best_frac = parse_frac_name(best_name)
            for nb in sorted(
                neighbors(best_frac), key=lambda f: proxy_score(f)["proxy_score"]
            ):
                if nb.name() not in seen_eval and nb not in queue:
                    queue.append(nb)
        return list(evaluated.values())

    for name in to_eval:
        eval_sim_candidate(coords, name)

    return written


def main() -> int:
    ap = argparse.ArgumentParser(
        description=__doc__,
        formatter_class=argparse.RawDescriptionHelpFormatter,
    )
    ap.add_argument(
        "--optimize",
        action="store_true",
        help="Rank coarse grid (+ hill-climb) by proxy and write top candidates",
    )
    ap.add_argument(
        "--write-top",
        type=int,
        default=10,
        help="How many top proxy candidates to materialize (default: 10)",
    )
    ap.add_argument(
        "--eval-top",
        type=int,
        default=0,
        help="After ranking, run full MARS sim+analyze on top N (hours each)",
    )
    ap.add_argument(
        "--max-evals",
        type=int,
        default=0,
        help="Hill-climb with up to N real sims (uses proxy until evals exist)",
    )
    ap.add_argument(
        "--eval-sim",
        metavar="NAME",
        help="Run one candidate through MARS + analyze_run (hours)",
    )
    ap.add_argument(
        "--analyze-only",
        metavar="NAME",
        help="Re-run analyze_run + update leaderboard without simulating",
    )
    ap.add_argument(
        "--write",
        action="store_true",
        help="Legacy: write a small hand-picked candidate set (prefer --optimize)",
    )
    ap.add_argument(
        "--list",
        action="store_true",
        help="Print proxy ranking (top 30) without writing",
    )
    ap.add_argument(
        "--baseline-check",
        action="store_true",
        help="Print scenario_12 seed naming / demand",
    )
    args = ap.parse_args()

    coords = load_lot_coords()

    if args.baseline_check:
        s = proxy_score(S12_SEED)
        print(json.dumps(s, indent=2))
        print(f"scenario_12 seed name: {S12_SEED.name()}")

    if args.list or (
        not args.optimize
        and not args.eval_sim
        and not args.analyze_only
        and not args.write
        and not args.baseline_check
    ):
        # Default useful action when no flags: same as --optimize --write-top 10
        if not args.list and not args.baseline_check:
            args.optimize = True

    if args.list:
        print(PROXY_BANNER, flush=True)
        ranked = rank_all()
        print(json.dumps(ranked[:30], indent=2))
        return 0

    if args.analyze_only:
        name = parse_frac_name(args.analyze_only).name()
        # Ensure artifacts exist
        materialize_candidate(coords, parse_frac_name(name))
        eval_sim_candidate(coords, name, skip_sim=True)
        return 0

    if args.eval_sim:
        eval_sim_candidate(coords, args.eval_sim)
        return 0

    if args.optimize:
        optimize_and_write(
            coords,
            write_top=args.write_top,
            eval_top=args.eval_top,
            max_evals=args.max_evals,
        )
        print(
            "\nNext (overnight clearance search — eval s12 seed first):\n"
            f"  python scripts/optimize_exit_assignment.py --eval-sim {S12_SEED_NAME}\n"
            "  python scripts/optimize_exit_assignment.py --optimize --eval-top 3\n"
            "Rank by evac_end_s in results/opt_candidates/leaderboard.csv "
            "(lower is better). Proxy is a filter only. Do not overwrite scenarios 01-12."
        )
        return 0

    if args.write:
        # Legacy small set around s10–s12
        legacy = [
            ExitFrac(0.0, 0.0, 0.0),
            ExitFrac(0.0, 1.0, 0.0),
            ExitFrac(0.0, 0.0, 0.5),
            ExitFrac(0.0, 1.0, 0.5),
            ExitFrac(0.0, 1.0, 0.75),
            ExitFrac(0.0, 0.5, 0.5),
            ExitFrac(0.25, 1.0, 0.5),
            ExitFrac(0.5, 1.0, 0.5),
            ExitFrac(0.0, 1.0, 1.0),
        ]
        written = [materialize_candidate(coords, f) for f in legacy]
        merge_leaderboard(written)
        print(json.dumps(written, indent=2))
        return 0

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
