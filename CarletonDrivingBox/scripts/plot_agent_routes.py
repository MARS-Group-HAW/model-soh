#!/usr/bin/env python3
"""
Plot one PNG per completed agent trip from CarletonCarDriver_trips.geojson.

Use this to visually verify each car routes from lot (entrance) to campus exit.
Trips geojson = finished drives only (one LineString per agent).
"""
from __future__ import annotations

import argparse
import csv
import json
import math
from pathlib import Path

import matplotlib.pyplot as plt
from matplotlib.collections import LineCollection
from matplotlib.lines import Line2D

from mars_agent_outputs import agent_output_path

ROOT = Path(__file__).resolve().parents[1]
RESULTS = ROOT / "results"
DEFAULT_GRAPH = ROOT / "resources" / "campus_drive_graph.geojson"
VALID_SCENARIOS = tuple(f"{i:02d}" for i in range(1, 11))

COLONEL_BY = (45.3792575, -75.7004525)
BRONSON = (45.3896198, -75.694494)
# Observed trip terminus on emergency corridor (not the schedule WGS label alone).
BRONSON_RAVEN = (45.3846, -75.6922)
EXIT_TOL_M = 120.0

LOTS_BASE = {
    "P1": {"spawn": (45.3813098, -75.7006879), "exit": COLONEL_BY, "color": "#e41a1c"},
    "P2": {"spawn": (45.3836355, -75.6962699), "exit": COLONEL_BY, "color": "#377eb8"},
    "P3": {"spawn": (45.384003, -75.694052), "exit": COLONEL_BY, "color": "#4daf4a"},
    "P4": {"spawn": (45.3857, -75.6950), "exit": BRONSON, "color": "#984ea3"},
    "P5": {"spawn": (45.3876035, -75.6950176), "exit": BRONSON, "color": "#ff7f00"},
    "P6": {"spawn": (45.3885825, -75.6970087), "exit": BRONSON, "color": "#a65628"},
    "P7": {"spawn": (45.3888841, -75.6962336), "exit": BRONSON, "color": "#f781bf"},
}


def normalize_scenario(value: str) -> str:
    sid = value.strip().replace("scenario_", "")
    if not sid.isdigit():
        raise ValueError(f"Bad scenario id: {value!r}")
    sid = f"{int(sid):02d}"
    if sid not in VALID_SCENARIOS:
        raise ValueError(f"Scenario must be 01–10, got {value!r}")
    return sid


def lots_for_scenario(scenario_id: str) -> dict:
    lots = {k: dict(v) for k, v in LOTS_BASE.items()}
    if scenario_id == "07":
        lots["P3"]["exit"] = BRONSON_RAVEN
        lots["P4"]["exit"] = BRONSON_RAVEN
    elif scenario_id == "08":
        for lot in lots.values():
            lot["exit"] = COLONEL_BY
    elif scenario_id == "09":
        for lot in ("P1", "P2"):
            lots[lot]["exit"] = COLONEL_BY
        for lot in ("P3", "P4", "P5", "P6", "P7"):
            lots[lot]["exit"] = BRONSON_RAVEN
    elif scenario_id == "10":
        lots["P6"]["exit"] = COLONEL_BY
    return lots


def resolve_scenario_paths(scenario_id: str) -> tuple[Path, Path, Path]:
    """trips geojson, background graph, output folder for a scenario."""
    scenario_dir = RESULTS / f"scenario_{scenario_id}"
    trips = agent_output_path(scenario_dir, "_trips.geojson")
    out = scenario_dir / "agent_routes"
    graph_specific = ROOT / "resources" / f"campus_drive_graph_scenario_{scenario_id}.geojson"
    graph = graph_specific if graph_specific.is_file() else DEFAULT_GRAPH
    return trips, graph, out


def dist_m(lat1: float, lon1: float, lat2: float, lon2: float) -> float:
    return math.hypot((lat2 - lat1) * 111_000, (lon2 - lon1) * 85_000)


def nearest_lot(lat: float, lon: float, lots: dict) -> str:
    return min(lots, key=lambda k: dist_m(lat, lon, *lots[k]["spawn"]))


def flatten_coords(geom: dict) -> list[tuple[float, float]]:
    t = geom["type"]
    raw = geom["coordinates"]
    if t == "LineString":
        lines = [raw]
    elif t == "MultiLineString":
        lines = raw
    else:
        return []
    out: list[tuple[float, float]] = []
    for line in lines:
        for pt in line:
            out.append((pt[1], pt[0]))  # lat, lon
    return out


def path_metrics(coords: list[tuple[float, float]]) -> tuple[float, float, float]:
    if len(coords) < 2:
        return 0.0, 0.0, 1.0
    path = sum(dist_m(coords[i][0], coords[i][1], coords[i + 1][0], coords[i + 1][1]) for i in range(len(coords) - 1))
    chord = dist_m(coords[0][0], coords[0][1], coords[-1][0], coords[-1][1])
    ratio = path / max(chord, 1.0)
    return path, chord, ratio


def load_graph_segments(graph_path: Path) -> list[list[tuple[float, float]]]:
    if not graph_path.is_file():
        return []
    with graph_path.open(encoding="utf-8") as f:
        feats = json.load(f)["features"]
    segments = []
    for feat in feats:
        geom = feat["geometry"]
        if geom["type"] != "LineString":
            continue
        segments.append([(c[1], c[0]) for c in geom["coordinates"]])
    return segments


def safe_name(value: str) -> str:
    return "".join(ch if ch.isalnum() or ch in "-_" else "_" for ch in value)[:40]


def plot_trip(
    coords: list[tuple[float, float]],
    lot: str,
    agent_id: str,
    exit_ok: bool,
    path_m: float,
    ratio: float,
    graph_segments: list[list[tuple[float, float]]],
    out_path: Path,
    dpi: int,
    lots: dict,
):
    lats = [c[0] for c in coords]
    lons = [c[1] for c in coords]
    spawn = lots[lot]["spawn"]
    expected_exit = lots[lot]["exit"]
    lot_color = lots[lot]["color"]
    route_color = "#1b9e77" if exit_ok and ratio >= 1.05 else "#d95f02"

    pad_lat = max(0.0015, (max(lats) - min(lats)) * 0.15 + 0.0008)
    pad_lon = max(0.0020, (max(lons) - min(lons)) * 0.15 + 0.0010)

    fig, ax = plt.subplots(figsize=(7, 7))

    if graph_segments:
        bg = LineCollection([[(lon, lat) for lat, lon in seg] for seg in graph_segments], colors="#dddddd", linewidths=0.4, zorder=1)
        ax.add_collection(bg)

    ax.plot(lons, lats, color=route_color, linewidth=2.2, zorder=3)
    ax.scatter([spawn[1]], [spawn[0]], c=lot_color, s=80, marker="o", zorder=4, edgecolors="black", linewidths=0.5)
    ax.scatter([expected_exit[1]], [expected_exit[0]], c="none", s=120, marker="*", zorder=4, edgecolors="#333333", linewidths=1.2)
    ax.scatter([lons[0]], [lats[0]], c="white", s=40, marker="o", zorder=5, edgecolors="black", linewidths=0.8)
    ax.scatter([lons[-1]], [lats[-1]], c=route_color, s=50, marker="s", zorder=5, edgecolors="black", linewidths=0.8)

    ax.set_xlim(min(lons + [spawn[1], expected_exit[1]]) - pad_lon, max(lons + [spawn[1], expected_exit[1]]) + pad_lon)
    ax.set_ylim(min(lats + [spawn[0], expected_exit[0]]) - pad_lat, max(lats + [spawn[0], expected_exit[0]]) + pad_lat)
    ax.set_aspect("equal", adjustable="box")
    ax.set_xticks([])
    ax.set_yticks([])
    ax.tick_params(left=False, bottom=False, labelleft=False, labelbottom=False)

    status = "OK" if exit_ok else "WRONG EXIT"
    ax.set_title(f"{lot}  {agent_id[:8]}…  {status}\npath {path_m:.0f} m  ratio {ratio:.2f}", fontsize=10)

    legend = [
        Line2D([0], [0], marker="o", color="w", markerfacecolor=lot_color, markersize=8, label=f"Lot {lot} spawn"),
        Line2D([0], [0], marker="*", color="w", markeredgecolor="#333", markersize=12, label="Expected exit"),
        Line2D([0], [0], color=route_color, linewidth=2, label="Agent route"),
    ]
    ax.legend(handles=legend, loc="upper right", fontsize=8)
    ax.grid(True, alpha=0.25)

    fig.tight_layout()
    out_path.parent.mkdir(parents=True, exist_ok=True)
    fig.savefig(out_path, dpi=dpi)
    plt.close(fig)


def main():
    ap = argparse.ArgumentParser(
        description="Export one route PNG per completed agent trip (scenarios 01–10)."
    )
    ap.add_argument(
        "scenario",
        nargs="?",
        default="01",
        help="Scenario id 01–10 (default: 01). Resolves trips/graph/output under results/scenario_XX/",
    )
    ap.add_argument(
        "--trips",
        type=Path,
        default=None,
        help="Override CarletonCarDriver_trips.geojson (CarDriver_trips.geojson fallback)",
    )
    ap.add_argument("--graph", type=Path, default=None, help="Override background drive graph")
    ap.add_argument("--out", type=Path, default=None, help="Override output folder for PNGs")
    ap.add_argument("--limit", type=int, default=0, help="Max trips to plot (0 = all)")
    ap.add_argument("--lot", type=str, default="", help="Only plot this lot, e.g. P5")
    ap.add_argument(
        "--one-per-lot",
        action="store_true",
        help="Plot exactly one trip per parking lot (P1–P7), then stop",
    )
    ap.add_argument("--suspicious-only", action="store_true", help="Only wrong exit or nearly straight routes")
    ap.add_argument("--no-graph", action="store_true", help="Skip background road network")
    ap.add_argument("--dpi", type=int, default=120)
    args = ap.parse_args()

    try:
        scenario_id = normalize_scenario(args.scenario)
    except ValueError as exc:
        raise SystemExit(str(exc)) from exc

    default_trips, default_graph, default_out = resolve_scenario_paths(scenario_id)
    trips_path = args.trips or default_trips
    graph_path = args.graph or default_graph
    out_dir = args.out or default_out
    lots = lots_for_scenario(scenario_id)

    if not trips_path.is_file():
        raise SystemExit(
            f"Missing trips file: {trips_path}\n"
            f"Run scenario {scenario_id} first:\n"
            f"  dotnet run --project SOHCarletonDrivingBox.csproj -- configs/config_scenario_{scenario_id}.json"
        )

    print(f"Scenario       : {scenario_id}")
    print(f"Loading {trips_path} …")
    with trips_path.open(encoding="utf-8") as f:
        data = json.load(f)
    features = data.get("features", [])
    print(f"Trips loaded   : {len(features)}")
    print(f"Graph          : {graph_path}")
    print(f"Output folder  : {out_dir}")

    graph_segments = [] if args.no_graph else load_graph_segments(graph_path)
    if graph_segments:
        print(f"Background graph segments: {len(graph_segments)}")

    summary_rows = []
    plotted = 0
    skipped = 0
    lots_done: set[str] = set()

    for idx, feat in enumerate(features):
        if args.limit and plotted >= args.limit:
            break
        if args.one_per_lot and len(lots_done) >= len(lots):
            break

        props = feat.get("properties") or {}
        agent_id = str(props.get("creation_id") or props.get("ID") or props.get("StableId") or f"trip_{idx}")
        coords = flatten_coords(feat["geometry"])
        if len(coords) < 2:
            skipped += 1
            continue

        lot = nearest_lot(coords[0][0], coords[0][1], lots)
        if args.lot and lot != args.lot.upper():
            continue
        if args.one_per_lot and lot in lots_done:
            continue

        expected_exit = lots[lot]["exit"]
        end_lat, end_lon = coords[-1]
        exit_dist = dist_m(end_lat, end_lon, expected_exit[0], expected_exit[1])
        exit_ok = exit_dist <= EXIT_TOL_M
        path_m, chord_m, ratio = path_metrics(coords)
        suspicious = (not exit_ok) or ratio < 1.05

        if args.suspicious_only and not suspicious:
            continue

        if args.one_per_lot:
            fname = f"{lot}_to_exit.png"
        else:
            fname = f"{plotted + 1:04d}_{lot}_{safe_name(agent_id)}.png"
        out_path = out_dir / fname
        plot_trip(coords, lot, agent_id, exit_ok, path_m, ratio, graph_segments, out_path, args.dpi, lots)

        summary_rows.append(
            {
                "file": fname,
                "agent_id": agent_id,
                "lot": lot,
                "exit_ok": exit_ok,
                "exit_dist_m": round(exit_dist, 1),
                "spawn_dist_m": round(dist_m(coords[0][0], coords[0][1], *lots[lot]["spawn"]), 1),
                "path_m": round(path_m, 1),
                "chord_m": round(chord_m, 1),
                "path_chord_ratio": round(ratio, 3),
                "n_points": len(coords),
            }
        )
        plotted += 1
        if args.one_per_lot:
            lots_done.add(lot)
        if plotted % 100 == 0:
            print(f"  plotted {plotted} …")

    summary_path = out_dir / "route_summary.csv"
    out_dir.mkdir(parents=True, exist_ok=True)
    with summary_path.open("w", encoding="utf-8", newline="") as f:
        if summary_rows:
            writer = csv.DictWriter(f, fieldnames=list(summary_rows[0].keys()))
            writer.writeheader()
            writer.writerows(summary_rows)

    ok = sum(1 for r in summary_rows if r["exit_ok"])
    bad = len(summary_rows) - ok
    straight = sum(1 for r in summary_rows if r["path_chord_ratio"] < 1.05)

    print()
    print(f"PNG folder     : {out_dir}")
    print(f"Summary CSV    : {summary_path}")
    print(f"Plotted        : {plotted}")
    print(f"Skipped (empty): {skipped}")
    print(f"Exit OK        : {ok}")
    print(f"Wrong exit     : {bad}")
    print(f"Nearly straight (ratio<1.05): {straight}")
    print()
    print("Legend: circle=lot spawn, star=expected exit, white dot=route start, square=route end.")


if __name__ == "__main__":
    main()
