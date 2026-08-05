#!/usr/bin/env python3
"""Re-run MARS + analyze/heatmap + one-route-per-lot for scenarios 02-12.

Usage (from CarletonDrivingBox/, PowerShell or bash):

  # Overnight default: scenarios 02-12 (sim + analyze/heatmap + agent routes)
  python scripts/rerun_scenarios_02_12.py

  # Subset by range or explicit list
  python scripts/rerun_scenarios_02_12.py --from 2 --to 12
  python scripts/rerun_scenarios_02_12.py --scenarios 2,5,10

  # Analyze/plot only (skip dotnet sim)
  python scripts/rerun_scenarios_02_12.py --skip-sim
  python scripts/rerun_scenarios_02_12.py --scenarios 11 --skip-sim

  # Skip build / skip agent-route PNGs
  python scripts/rerun_scenarios_02_12.py --no-build
  python scripts/rerun_scenarios_02_12.py --skip-routes

Per scenario this script:
  1. Runs:  dotnet run --project SOHCarletonDrivingBox.csproj -- configs/config_scenario_XX.json
  2. Runs:  analyze_and_heatmap.py on results/scenario_XX/CarletonCarDriver.csv
  3. Runs:  plot_agent_routes.py XX --one-per-lot

Failures are logged; the loop continues. A summary is printed at the end.
Horizon/endPoint are left as written in each scenario config.
"""
from __future__ import annotations

import argparse
import subprocess
import sys
import time
from dataclasses import dataclass, field
from pathlib import Path

from mars_agent_outputs import agent_output_path

ROOT = Path(__file__).resolve().parents[1]
PROJECT = ROOT / "SOHCarletonDrivingBox.csproj"
CONFIGS = ROOT / "configs"
RESULTS = ROOT / "results"
SCRIPTS = ROOT / "scripts"
VALID = tuple(f"{i:02d}" for i in range(1, 13))
DEFAULT_FROM = 2
DEFAULT_TO = 12


@dataclass
class StepResult:
    name: str
    ok: bool
    detail: str = ""
    seconds: float = 0.0


@dataclass
class ScenarioResult:
    sid: str
    steps: list[StepResult] = field(default_factory=list)
    skipped_missing: bool = False

    @property
    def ok(self) -> bool:
        if self.skipped_missing:
            return False
        return all(s.ok for s in self.steps) if self.steps else False


def print_usage_banner() -> None:
    print("=" * 72, flush=True)
    print("rerun_scenarios_02_12.py - sim + analyze/heatmap + one route per lot", flush=True)
    print("=" * 72, flush=True)
    print(
        "Examples:\n"
        "  python scripts/rerun_scenarios_02_12.py\n"
        "  python scripts/rerun_scenarios_02_12.py --from 2 --to 12\n"
        "  python scripts/rerun_scenarios_02_12.py --scenarios 2,5,10\n"
        "  python scripts/rerun_scenarios_02_12.py --skip-sim\n"
        "Configs: configs/config_scenario_XX.json\n"
        "Outputs: results/scenario_XX/\n"
        "Project: SOHCarletonDrivingBox.csproj",
        flush=True,
    )
    print("=" * 72, flush=True)


def normalize_sid(value: str | int) -> str:
    text = str(value).strip().replace("scenario_", "")
    if not text.isdigit():
        raise ValueError(f"Bad scenario id: {value!r}")
    sid = f"{int(text):02d}"
    if sid not in VALID:
        raise ValueError(f"Scenario must be 01-12, got {value!r}")
    return sid


def parse_scenario_list(raw: str) -> list[str]:
    parts = [p.strip() for p in raw.replace(";", ",").split(",") if p.strip()]
    if not parts:
        raise ValueError("--scenarios is empty")
    return [normalize_sid(p) for p in parts]


def select_scenarios(args: argparse.Namespace) -> list[str]:
    if args.scenarios:
        return parse_scenario_list(args.scenarios)
    lo = args.from_id if args.from_id is not None else DEFAULT_FROM
    hi = args.to_id if args.to_id is not None else DEFAULT_TO
    if lo > hi:
        raise ValueError(f"--from {lo} > --to {hi}")
    return [normalize_sid(i) for i in range(lo, hi + 1)]


def run_cmd(cmd: list[str], *, cwd: Path, log: Path | None = None) -> tuple[int, float]:
    print("+", " ".join(cmd), flush=True)
    t0 = time.time()
    if log is None:
        rc = subprocess.call(cmd, cwd=cwd)
        return rc, time.time() - t0
    log.parent.mkdir(parents=True, exist_ok=True)
    with log.open("a", encoding="utf-8") as out:
        out.write("+ " + " ".join(cmd) + "\n")
        out.flush()
        proc = subprocess.run(
            cmd,
            cwd=cwd,
            stdout=out,
            stderr=subprocess.STDOUT,
            text=True,
        )
        out.write(f"exit={proc.returncode}\n")
        return proc.returncode, time.time() - t0


def config_path(sid: str) -> Path:
    return CONFIGS / f"config_scenario_{sid}.json"


def discover_preflight(selected: list[str]) -> list[str]:
    """Return warnings (e.g. missing config). Does not abort the run."""
    warnings: list[str] = []
    for sid in selected:
        cfg = config_path(sid)
        if not cfg.is_file():
            warnings.append(f"MISSING config: {cfg}")
            continue
        out_dir = RESULTS / f"scenario_{sid}"
        print(f"  scenario_{sid}: config={cfg.name}  output={out_dir}", flush=True)
    return warnings


def run_sim(sid: str, log: Path) -> StepResult:
    cfg = config_path(sid)
    if not cfg.is_file():
        return StepResult("sim", False, f"missing config {cfg}")
    # Relative path matches existing box / notebook pattern (cwd = project root).
    rel = f"configs/config_scenario_{sid}.json"
    cmd = [
        "dotnet",
        "run",
        "--no-build",
        "--project",
        str(PROJECT),
        "--",
        rel,
    ]
    rc, elapsed = run_cmd(cmd, cwd=ROOT, log=log)
    if rc != 0:
        return StepResult("sim", False, f"exit {rc}", elapsed)
    csv = agent_output_path(RESULTS / f"scenario_{sid}", ".csv")
    if not csv.is_file():
        return StepResult("sim", False, f"sim ok but missing {csv.name}", elapsed)
    return StepResult("sim", True, str(csv), elapsed)


def run_analyze_heatmap(sid: str, log: Path) -> StepResult:
    out_dir = RESULTS / f"scenario_{sid}"
    csv = agent_output_path(out_dir, ".csv")
    if not csv.is_file():
        return StepResult("analyze_heatmap", False, f"missing {csv}")
    cmd = [sys.executable, str(SCRIPTS / "analyze_and_heatmap.py"), str(csv)]
    rc, elapsed = run_cmd(cmd, cwd=ROOT, log=log)
    if rc != 0:
        return StepResult("analyze_heatmap", False, f"exit {rc}", elapsed)
    return StepResult("analyze_heatmap", True, str(out_dir), elapsed)


def run_agent_routes(sid: str, log: Path) -> StepResult:
    trips = agent_output_path(RESULTS / f"scenario_{sid}", "_trips.geojson")
    if not trips.is_file():
        return StepResult("agent_routes", False, f"missing {trips.name}")
    cmd = [
        sys.executable,
        str(SCRIPTS / "plot_agent_routes.py"),
        sid,
        "--one-per-lot",
    ]
    rc, elapsed = run_cmd(cmd, cwd=ROOT, log=log)
    if rc != 0:
        return StepResult("agent_routes", False, f"exit {rc}", elapsed)
    return StepResult(
        "agent_routes",
        True,
        str(RESULTS / f"scenario_{sid}" / "agent_routes"),
        elapsed,
    )


def process_scenario(
    sid: str,
    *,
    skip_sim: bool,
    skip_routes: bool,
) -> ScenarioResult:
    result = ScenarioResult(sid=sid)
    out_dir = RESULTS / f"scenario_{sid}"
    out_dir.mkdir(parents=True, exist_ok=True)
    log = out_dir / "rerun.log"
    log.write_text(
        f"scenario_{sid}\nconfig={config_path(sid)}\n",
        encoding="utf-8",
    )

    print(f"\n=== scenario_{sid} ===", flush=True)

    if not config_path(sid).is_file():
        print(f"[fail] missing config {config_path(sid)}", flush=True)
        result.skipped_missing = True
        result.steps.append(StepResult("preflight", False, "missing config"))
        return result

    if not skip_sim:
        step = run_sim(sid, log)
        result.steps.append(step)
        status = "ok" if step.ok else f"FAIL ({step.detail})"
        print(f"[sim]   scenario_{sid} {status}  ({step.seconds / 60:.1f} min)", flush=True)
        if not step.ok:
            # Still try analyze if a previous CSV exists; otherwise continue to next scenario.
            csv = agent_output_path(out_dir, ".csv")
            if not csv.is_file():
                print(f"[skip]  analyze/routes - no CSV after sim failure", flush=True)
                return result

    step = run_analyze_heatmap(sid, log)
    result.steps.append(step)
    status = "ok" if step.ok else f"FAIL ({step.detail})"
    print(f"[anal]  scenario_{sid} {status}  ({step.seconds / 60:.1f} min)", flush=True)

    if not skip_routes:
        step = run_agent_routes(sid, log)
        result.steps.append(step)
        status = "ok" if step.ok else f"FAIL ({step.detail})"
        print(f"[route] scenario_{sid} {status}  ({step.seconds:.1f} s)", flush=True)

    return result


def print_summary(results: list[ScenarioResult]) -> int:
    print("\n" + "=" * 72, flush=True)
    print("SUMMARY", flush=True)
    print("=" * 72, flush=True)
    ok_n = 0
    fail_n = 0
    for r in results:
        if r.ok:
            ok_n += 1
            parts = ", ".join(f"{s.name}={s.seconds:.0f}s" for s in r.steps)
            print(f"  OK   scenario_{r.sid}  ({parts})", flush=True)
        else:
            fail_n += 1
            fails = [
                f"{s.name}: {s.detail or 'failed'}"
                for s in r.steps
                if not s.ok
            ] or ["unknown failure"]
            print(f"  FAIL scenario_{r.sid}  - {'; '.join(fails)}", flush=True)
            print(f"       see results/scenario_{r.sid}/rerun.log", flush=True)

    print(
        f"\n{ok_n} ok, {fail_n} failed/partial of {len(results)} scenario(s).",
        flush=True,
    )
    print(
        "Per-scenario outputs under results/scenario_XX/:\n"
        "  CarletonCarDriver.csv, CarletonCarDriver_trips.geojson\n"
        "  metrics.json, summary.csv, evac_curve.csv/.png, lot/exit CSVs + PNGs\n"
        "  heatmap_matrix.csv/.png\n"
        "  agent_routes/ (one PNG per lot P1-P7) + route_summary.csv\n"
        "  rerun.log",
        flush=True,
    )
    return 0 if fail_n == 0 else 1


def main() -> int:
    ap = argparse.ArgumentParser(
        description=(
            "Re-run MARS scenarios (default 02-12), then analyze+heatmap and "
            "plot one agent route per parking lot. Continues on failure."
        ),
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog=(
            "Examples:\n"
            "  python scripts/rerun_scenarios_02_12.py\n"
            "  python scripts/rerun_scenarios_02_12.py --from 2 --to 12\n"
            "  python scripts/rerun_scenarios_02_12.py --scenarios 2,5,10\n"
            "  python scripts/rerun_scenarios_02_12.py --skip-sim\n"
        ),
    )
    ap.add_argument(
        "--from",
        dest="from_id",
        type=int,
        default=None,
        help=f"First scenario number (default: {DEFAULT_FROM})",
    )
    ap.add_argument(
        "--to",
        dest="to_id",
        type=int,
        default=None,
        help=f"Last scenario number (default: {DEFAULT_TO})",
    )
    ap.add_argument(
        "--scenarios",
        type=str,
        default="",
        help="Comma-separated scenario ids (overrides --from/--to), e.g. 2,5,10",
    )
    ap.add_argument(
        "--skip-sim",
        action="store_true",
        help="Skip dotnet simulation; analyze/plot existing results only",
    )
    ap.add_argument(
        "--skip-routes",
        action="store_true",
        help="Skip plot_agent_routes.py --one-per-lot",
    )
    ap.add_argument(
        "--no-build",
        action="store_true",
        help="Skip initial dotnet build (still uses --no-build on each run)",
    )
    args = ap.parse_args()
    print_usage_banner()

    try:
        selected = select_scenarios(args)
    except ValueError as exc:
        print(str(exc), file=sys.stderr)
        return 2

    print(f"\nSelected: {', '.join('scenario_' + s for s in selected)}", flush=True)
    print(f"Project : {PROJECT.name}", flush=True)
    print(f"skip-sim={args.skip_sim}  skip-routes={args.skip_routes}", flush=True)
    print("\nPreflight:", flush=True)
    warnings = discover_preflight(selected)
    for w in warnings:
        print(f"  WARNING: {w}", flush=True)

    # Note scenario 11: config + schedule exist even if results/scenario_11 was empty before.
    sid11 = "11"
    if sid11 in selected:
        cfg11 = config_path(sid11)
        sched11 = ROOT / "resources" / "schedules" / "scenario_11_schedule.csv"
        print(
            f"  scenario_11 check: config={'OK' if cfg11.is_file() else 'MISSING'}, "
            f"schedule={'OK' if sched11.is_file() else 'MISSING'}",
            flush=True,
        )

    if not args.skip_sim and not args.no_build:
        print("\nBuilding...", flush=True)
        rc, _ = run_cmd(
            [
                "dotnet",
                "build",
                str(PROJECT),
                "--verbosity",
                "minimal",
                "-p:NuGetAudit=false",
            ],
            cwd=ROOT,
        )
        if rc != 0:
            print("dotnet build failed; aborting.", file=sys.stderr)
            return rc

    results: list[ScenarioResult] = []
    for sid in selected:
        results.append(
            process_scenario(
                sid,
                skip_sim=args.skip_sim,
                skip_routes=args.skip_routes,
            )
        )

    return print_summary(results)


if __name__ == "__main__":
    raise SystemExit(main())
