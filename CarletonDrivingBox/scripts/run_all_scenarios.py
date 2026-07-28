#!/usr/bin/env python3
"""Run campus evacuation scenarios 01–12 (sequential or in parallel).

Parallel mode isolates each run's working directory so MARS trip GeoJSON
files do not collide in the project root (that race caused CLR crashes
with exit code 0xE0434352 / 3762504530).

MARS's own console progress bar cannot run when stdout is shared/redirected,
so parallel mode draws a multi-scenario progress board in this script instead
(wall-clock vs configured sim horizon + agent CSV size as info).
"""
from __future__ import annotations

import argparse
import json
import re
import subprocess
import sys
import threading
import time
from concurrent.futures import ThreadPoolExecutor, as_completed
from dataclasses import dataclass, field
from datetime import datetime
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
PROJECT = ROOT / "SOHCarletonDrivingBox.csproj"
SCENARIOS = tuple(f"{i:02d}" for i in range(1, 13))
BAR_W = 24


def run(
    cmd: list[str],
    *,
    cwd: Path | None = None,
    log: Path | None = None,
) -> int:
    work = cwd or ROOT
    print("+", " ".join(cmd), flush=True)
    if log is None:
        return subprocess.call(cmd, cwd=work)
    log.parent.mkdir(parents=True, exist_ok=True)
    with log.open("w", encoding="utf-8") as out:
        out.write(f"cwd={work}\n")
        out.write("+ " + " ".join(cmd) + "\n")
        out.flush()
        proc = subprocess.run(
            cmd,
            cwd=work,
            stdout=out,
            stderr=subprocess.STDOUT,
            text=True,
        )
        return proc.returncode


def _parse_horizon_s(cfg: dict) -> float:
    g = cfg.get("globals") or {}
    try:
        start = datetime.fromisoformat(str(g.get("startPoint", "")).replace("Z", ""))
        end = datetime.fromisoformat(str(g.get("endPoint", "")).replace("Z", ""))
        return max((end - start).total_seconds(), 1.0)
    except ValueError:
        return 1.0


def _absolutize_config(sid: str) -> tuple[Path, Path, float]:
    """Write a per-scenario config with absolute paths; return (config, run_cwd, horizon_s)."""
    src = ROOT / "configs" / f"config_scenario_{sid}.json"
    out_dir = (ROOT / "results" / f"scenario_{sid}").resolve()
    out_dir.mkdir(parents=True, exist_ok=True)
    cfg = json.loads(src.read_text(encoding="utf-8"))
    horizon = _parse_horizon_s(cfg)

    globals_ = cfg.setdefault("globals", {})
    # Progress bar needs a real TTY; parallel jobs share one console — keep off.
    globals_["console"] = False
    csv_opts = globals_.setdefault("csvOptions", {})
    csv_opts["outputPath"] = str(out_dir)

    for section in ("layers", "entities"):
        for entry in cfg.get(section) or []:
            rel = entry.get("file")
            if rel:
                entry["file"] = str((ROOT / rel).resolve())

    cfg_path = out_dir / "_parallel_config.json"
    cfg_path.write_text(json.dumps(cfg, indent=2) + "\n", encoding="utf-8")
    return cfg_path, out_dir, horizon


def _bar(frac: float) -> str:
    frac = 0.0 if frac < 0 else 1.0 if frac > 1 else frac
    n = int(round(BAR_W * frac))
    return "#" * n + "-" * (BAR_W - n)


def _fmt_bytes_fixed(n: int) -> str:
    if n < 1024:
        return f"{n:6d}B"
    if n < 1024**2:
        return f"{n / 1024:5.1f}KB"
    if n < 1024**3:
        return f"{n / 1024**2:5.1f}MB"
    return f"{n / 1024**3:5.2f}GB"


@dataclass
class JobState:
    sid: str
    status: str = "queued"  # queued|running|ok|fail
    horizon_s: float = 1.0
    started: float = 0.0
    ended: float = 0.0
    rc: int = 0
    csv_bytes: int = 0
    out_dir: Path = field(default_factory=Path)


class ProgressBoard:
    """Live multi-line progress board for parallel scenario runs.

    Important: only redraws *its own* lines (clear-line each row). Does not
    cursor-up into earlier build/log output — that was causing the garble.
    """

    def __init__(self, sids: list[str]):
        self._lock = threading.Lock()
        self.jobs = {sid: JobState(sid=sid) for sid in sids}
        self._order = list(sids)
        self._stop = threading.Event()
        self._drawn = 0
        self._started_board = False
        self._thread: threading.Thread | None = None
        self._tty = sys.stdout.isatty()

    def update(self, sid: str, **kwargs) -> None:
        with self._lock:
            job = self.jobs[sid]
            for k, v in kwargs.items():
                setattr(job, k, v)

    def start(self) -> None:
        # Separate board from any prior build warnings.
        print(flush=True)
        print("=" * 72, flush=True)
        print("Parallel progress (wall-clock vs sim horizon; CSV size is info only)", flush=True)
        print("=" * 72, flush=True)
        self._thread = threading.Thread(target=self._loop, name="progress-board", daemon=True)
        self._thread.start()

    def stop(self) -> None:
        self._stop.set()
        if self._thread:
            self._thread.join(timeout=2.0)
        self._render(final=True)

    def _loop(self) -> None:
        while not self._stop.wait(0.5):
            self._refresh_csv_sizes()
            self._render()

    def _refresh_csv_sizes(self) -> None:
        with self._lock:
            items = list(self.jobs.values())
        for job in items:
            if job.status != "running":
                continue
            csv_path = job.out_dir / "CarletonCarDriver.csv"
            try:
                size = csv_path.stat().st_size if csv_path.is_file() else 0
            except OSError:
                size = 0
            self.update(job.sid, csv_bytes=size)

    def _line(self, job: JobState, now: float) -> str:
        label = f"scenario_{job.sid}"
        if job.status == "queued":
            return f"  {label}  [{_bar(0.0)}]   queued"
        if job.status == "running":
            elapsed = max(0.0, now - job.started)
            # ONLY wall / horizon — do not use CSV size (it jumps to ~99% wrongly).
            frac = min(0.99, elapsed / job.horizon_s) if job.horizon_s else 0.0
            return (
                f"  {label}  [{_bar(frac)}]  {frac * 100:5.1f}%  "
                f"csv={_fmt_bytes_fixed(job.csv_bytes)}  "
                f"elapsed={elapsed / 60:5.1f}m"
            )
        elapsed = max(0.0, (job.ended or now) - job.started) if job.started else 0.0
        if job.status == "ok":
            return (
                f"  {label}  [{_bar(1.0)}]  100.0%  "
                f"csv={_fmt_bytes_fixed(job.csv_bytes)}  "
                f"elapsed={elapsed / 60:5.1f}m  done"
            )
        code = job.rc
        if code < 0:
            code = code + (1 << 32)
        return (
            f"  {label}  [{_bar(0.0)}]  FAIL({code})  "
            f"csv={_fmt_bytes_fixed(job.csv_bytes)}  "
            f"elapsed={elapsed / 60:5.1f}m"
        )

    def _render(self, *, final: bool = False) -> None:
        now = time.time()
        with self._lock:
            lines = [self._line(self.jobs[sid], now) for sid in self._order]

        if not self._tty:
            # Non-TTY: print a compact one-shot every few renders would spam;
            # only print finals / status changes via stop().
            if final:
                print("\n".join(lines), flush=True)
            return

        # Move up only over previously drawn *board* lines, then clear each line.
        if self._drawn > 0:
            sys.stdout.write(f"\033[{self._drawn}A")
        for line in lines:
            sys.stdout.write("\033[2K\r" + line + "\n")
        sys.stdout.flush()
        self._drawn = len(lines)
        if final:
            sys.stdout.write("\n")
            sys.stdout.flush()


_ITER_RE = re.compile(r"Executed iterations\s+(\d+)", re.I)


def run_scenario_parallel(sid: str, board: ProgressBoard) -> tuple[str, int]:
    cfg_path, cwd, horizon = _absolutize_config(sid)
    log = cwd / "run.log"
    csv_path = cwd / "CarletonCarDriver.csv"
    if csv_path.is_file():
        try:
            csv_path.unlink()
        except OSError:
            pass

    cmd = [
        "dotnet",
        "run",
        "--no-build",
        "--verbosity",
        "quiet",
        "--project",
        str(PROJECT),
        "--",
        str(cfg_path),
    ]
    board.update(
        sid,
        status="running",
        horizon_s=horizon,
        started=time.time(),
        out_dir=cwd,
        csv_bytes=0,
    )

    log.parent.mkdir(parents=True, exist_ok=True)
    env = {
        **dict(**{k: v for k, v in __import__("os").environ.items()}),
        "DOTNET_CLI_TELEMETRY_OPTOUT": "1",
        "NUGET_AUDIT": "false",
    }
    with log.open("w", encoding="utf-8") as out:
        out.write(f"cwd={cwd}\n")
        out.write("+ " + " ".join(cmd) + "\n")
        out.flush()
        proc = subprocess.Popen(
            cmd,
            cwd=cwd,
            stdout=subprocess.PIPE,
            stderr=subprocess.STDOUT,
            text=True,
            bufsize=1,
            env=env,
        )
        assert proc.stdout is not None
        for line in proc.stdout:
            out.write(line)
            out.flush()
            if _ITER_RE.search(line):
                pass
        rc = proc.wait()

    size = csv_path.stat().st_size if csv_path.is_file() else 0
    board.update(
        sid,
        status="ok" if rc == 0 else "fail",
        rc=rc,
        ended=time.time(),
        csv_bytes=size,
    )
    return sid, rc


def run_scenario_sequential(sid: str) -> tuple[str, int]:
    print(f"[start] scenario_{sid}", flush=True)
    cfg = f"configs/config_scenario_{sid}.json"
    cmd = [
        "dotnet",
        "run",
        "--no-build",
        "--project",
        str(PROJECT),
        "--",
        cfg,
    ]
    print("+", " ".join(cmd), flush=True)
    rc = subprocess.call(cmd, cwd=ROOT)
    status = "ok" if rc == 0 else f"FAIL({rc})"
    print(f"[done]  scenario_{sid} {status}", flush=True)
    return sid, rc


def main() -> int:
    ap = argparse.ArgumentParser(
        description="Run CarletonDrivingBox scenarios 01–12 (optionally in parallel)."
    )
    ap.add_argument("--no-build", action="store_true", help="Skip dotnet build")
    ap.add_argument(
        "--parallel",
        "-p",
        action="store_true",
        help=(
            "Run selected scenarios at the same time (after one shared build). "
            "Uses an isolated cwd per scenario and a multi-scenario progress board."
        ),
    )
    ap.add_argument(
        "--jobs",
        "-j",
        type=int,
        default=0,
        help="Max parallel runs with --parallel (default: all selected scenarios).",
    )
    ap.add_argument(
        "scenarios",
        nargs="*",
        help="Scenario ids to run (e.g. 01 03 12). Default: all 01–12.",
    )
    args = ap.parse_args()

    selected: list[str] = []
    for sid in args.scenarios or list(SCENARIOS):
        sid = f"{int(sid):02d}"
        if sid not in SCENARIOS:
            print(f"Unknown scenario: {sid}", file=sys.stderr)
            return 1
        selected.append(sid)

    if not args.no_build:
        # Quiet restore/audit noise so it does not pollute a later progress board.
        rc = run(
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
            return rc

    if not args.parallel:
        for sid in selected:
            print(f"\n=== scenario_{sid} ===", flush=True)
            _sid, rc = run_scenario_sequential(sid)
            if rc != 0:
                return rc
        print("\nDone. Results under results/scenario_XX/")
        return 0

    jobs = args.jobs if args.jobs > 0 else len(selected)
    jobs = max(1, min(jobs, len(selected)))
    print(f"\nParallel run: {len(selected)} scenarios, jobs={jobs}", flush=True)

    board = ProgressBoard(selected)
    board.start()
    failures: list[str] = []
    try:
        with ThreadPoolExecutor(max_workers=jobs) as pool:
            futures = {
                pool.submit(run_scenario_parallel, sid, board): sid for sid in selected
            }
            for fut in as_completed(futures):
                sid, rc = fut.result()
                if rc != 0:
                    failures.append(sid)
    finally:
        board.stop()

    if failures:
        print(f"\nFailed: {', '.join(sorted(failures))}", file=sys.stderr)
        print("See results/scenario_XX/run.log for details.", file=sys.stderr)
        return 1
    print("\nDone. Results under results/scenario_XX/ (see each run.log)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
