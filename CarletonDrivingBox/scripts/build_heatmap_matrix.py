#!/usr/bin/env python3
"""
Build MARS heatmap_matrix.csv for road occupancy over time.

Uses the campus drive graph for edge mapping and sim-road segment lengths
for cars-per-100m normalization.
"""
from __future__ import annotations

import argparse
import csv
import heapq
import json
import math
import re
from collections import defaultdict
from pathlib import Path

from mars_agent_outputs import agent_output_path

ROOT = Path(__file__).resolve().parents[1]
DEFAULT_CSV = agent_output_path(ROOT / "results", ".csv")
DEFAULT_GRAPH = ROOT / "resources" / "campus_drive_graph.geojson"
DEFAULT_LENGTHS = ROOT / "resources" / "sim_road_lengths.csv"
DEFAULT_OUT = ROOT / "results" / "heatmap_matrix.csv"

# Standard campus evacuation road columns for heatmap_matrix.csv
HEATMAP_ROADS = [
    "Campus Ave & Library Rd to Campus Ave & P2",
    "Campus Ave & Library Rd to Library Rd & P1",
    "Campus Ave & P2 to Campus Ave & University Dr",
    "Campus Ave & P6 to Campus Ave & Library Rd",
    "Campus Ave & P6 to Roundabout",
    "Campus Ave & University Dr to Library Rd & University Dr",
    "Campus Ave & University Dr to Raven Rd & University Dr",
    "Library Rd & P1 to Campus Ave & Library Rd",
    "Library Rd & P1 to Library Rd & University Dr",
    "Library Rd & University Dr to Colonel By Dr & University Dr",
    "P3 & Raven Rd to Bronson Ave & Raven Rd",
    "P4 & University Dr to Raven Rd & University Dr",
    "P4 & University Dr to Stadium Way & University Dr",
    "P5 & Stadium Way to Bronson Ave & Stadium Way",
    "P5 & Stadium Way to Stadium Way & University Dr",
    "Raven Rd & University Dr to Bronson Ave & Raven Rd",
    "Raven Rd & University Dr to Campus Ave & University Dr",
    "Raven Rd & University Dr to P4 & University Dr",
    "Roundabout to Bronson Ave & University Dr",
    "Stadium Way & University Dr to P5 & Stadium Way",
    "Stadium Way & University Dr to Roundabout",
]

# Synthetic / scenario-specific graph edges (osmid -> sim road name)
CUSTOM_OSM_ROADS = {
    "carleton_scenario07_r28": "Raven Rd & University Dr to Bronson Ave & Raven Rd",
}

PLACES = {
    "Library Rd & P1": (45.3813, -75.7007),
    "Library Rd & University Dr": (45.3793, -75.7005),
    "Campus Ave & Library Rd": (45.3855, -75.6964),
    "Campus Ave & P2": (45.3839, -75.6964),
    "Campus Ave & University Dr": (45.3825, -75.6958),
    "Raven Rd & University Dr": (45.3840, -75.6940),
    "P3 & Raven Rd": (45.3847, -75.6919),
    "Bronson Ave & Raven Rd": (45.3851, -75.6903),
    "Colonel By Dr & University Dr": (45.3790, -75.7008),
    "P4 & University Dr": (45.3857, -75.6950),
    "Stadium Way & University Dr": (45.3875, -75.6956),
    "P5 & Stadium Way": (45.3876, -75.6950),
    "Bronson Ave & Stadium Way": (45.3881, -75.6927),
    "Roundabout": (45.3889, -75.6960),
    "Bronson Ave & University Dr": (45.3896, -75.6945),
    "Campus Ave & P6": (45.3886, -75.6970),
}

SIM_ROADS = [
    ("Library Rd & P1", "Library Rd & University Dr", "Library Rd & P1 to Library Rd & University Dr"),
    ("Library Rd & P1", "Campus Ave & Library Rd", "Library Rd & P1 to Campus Ave & Library Rd"),
    ("Library Rd & University Dr", "Library Rd & P1", "Library Rd & University Dr to Library Rd & P1"),
    ("Campus Ave & Library Rd", "Library Rd & P1", "Campus Ave & Library Rd to Library Rd & P1"),
    ("Campus Ave & Library Rd", "Campus Ave & P2", "Campus Ave & Library Rd to Campus Ave & P2"),
    ("Campus Ave & P2", "Campus Ave & University Dr", "Campus Ave & P2 to Campus Ave & University Dr"),
    ("Campus Ave & University Dr", "Library Rd & University Dr", "Campus Ave & University Dr to Library Rd & University Dr"),
    ("Library Rd & University Dr", "Campus Ave & University Dr", "Library Rd & University Dr to Campus Ave & University Dr"),
    ("Campus Ave & University Dr", "Raven Rd & University Dr", "Campus Ave & University Dr to Raven Rd & University Dr"),
    ("Raven Rd & University Dr", "Campus Ave & University Dr", "Raven Rd & University Dr to Campus Ave & University Dr"),
    ("Raven Rd & University Dr", "P3 & Raven Rd", "Raven Rd & University Dr to P3 & Raven Rd"),
    ("P3 & Raven Rd", "Raven Rd & University Dr", "P3 & Raven Rd to Raven Rd & University Dr"),
    ("P3 & Raven Rd", "Bronson Ave & Raven Rd", "P3 & Raven Rd to Bronson Ave & Raven Rd"),
    ("Raven Rd & University Dr", "Bronson Ave & Raven Rd", "Raven Rd & University Dr to Bronson Ave & Raven Rd"),
    ("Library Rd & University Dr", "Colonel By Dr & University Dr", "Library Rd & University Dr to Colonel By Dr & University Dr"),
    ("Raven Rd & University Dr", "P4 & University Dr", "Raven Rd & University Dr to P4 & University Dr"),
    ("P4 & University Dr", "Raven Rd & University Dr", "P4 & University Dr to Raven Rd & University Dr"),
    ("P4 & University Dr", "Stadium Way & University Dr", "P4 & University Dr to Stadium Way & University Dr"),
    ("Stadium Way & University Dr", "P4 & University Dr", "Stadium Way & University Dr to P4 & University Dr"),
    ("Stadium Way & University Dr", "P5 & Stadium Way", "Stadium Way & University Dr to P5 & Stadium Way"),
    ("P5 & Stadium Way", "Stadium Way & University Dr", "P5 & Stadium Way to Stadium Way & University Dr"),
    ("P5 & Stadium Way", "Bronson Ave & Stadium Way", "P5 & Stadium Way to Bronson Ave & Stadium Way"),
    ("Stadium Way & University Dr", "Roundabout", "Stadium Way & University Dr to Roundabout"),
    ("Roundabout", "Stadium Way & University Dr", "Roundabout to Stadium Way & University Dr"),
    ("Roundabout", "Bronson Ave & University Dr", "Roundabout to Bronson Ave & University Dr"),
    ("Roundabout", "Campus Ave & P6", "Roundabout to Campus Ave & P6"),
    ("Campus Ave & P6", "Roundabout", "Campus Ave & P6 to Roundabout"),
    ("Campus Ave & P6", "Campus Ave & Library Rd", "Campus Ave & P6 to Campus Ave & Library Rd"),
]


def norm_osmid(val):
    if val is None:
        return None
    if isinstance(val, list):
        return str(val[0]) if val else None
    s = str(val).strip()
    if not s or s == "-1":
        return None
    if s[0] == "[":
        inner = s.strip("[]")
        first = inner.split(",")[0].strip() if inner else ""
        return first or None
    return s


def build_simroad_midpoints(graph: RoadGraph) -> list[tuple[str, float, float]]:
    """Midpoint of each sim-road corridor edge from shortest-path mapping."""
    place_node = {name: graph.nearest_node(lat, lon) for name, (lat, lon) in PLACES.items()}
    midpoints: list[tuple[str, float, float]] = []
    seen: set[tuple[str, float, float]] = set()

    for a, b, sim_name in SIM_ROADS:
        for uvk in graph.shortest_path_edges(place_node[a], place_node[b]):
            lat, lon = graph.edge_mid[uvk]
            key = (sim_name, round(lat, 5), round(lon, 5))
            if key not in seen:
                seen.add(key)
                midpoints.append((sim_name, lat, lon))
    return midpoints


def nearest_simroad(
    lat: float,
    lon: float,
    midpoints: list[tuple[str, float, float]],
    road_set: set[str],
    max_dist_m: float = 35.0,
) -> str | None:
    best, best_d = None, max_dist_m
    for sim_name, slat, slon in midpoints:
        if sim_name not in road_set:
            continue
        d = dist_m(lat, lon, slat, slon)
        if d < best_d:
            best_d, best = d, sim_name
    return best


def dist_m(lat1, lon1, lat2, lon2):
    return math.hypot((lat2 - lat1) * 111_000, (lon2 - lon1) * 85_000)


def load_heatmap_roads(lengths_path: Path) -> list[str]:
    if lengths_path.resolve() == DEFAULT_LENGTHS.resolve():
        return HEATMAP_ROADS
    roads: list[str] = []
    with lengths_path.open(encoding="utf-8", newline="") as f:
        for row in csv.DictReader(f):
            roads.append(row["ROAD"].strip())
    return roads or HEATMAP_ROADS


def load_road_lengths(path: Path) -> dict[str, float]:
    lengths: dict[str, float] = {}
    if not path.is_file():
        return lengths
    with path.open(encoding="utf-8", newline="") as f:
        for row in csv.DictReader(f):
            lengths[row["ROAD"].strip()] = float(row["LENGTH_M"])
    return lengths


class RoadGraph:
    def __init__(self, geojson_path: Path):
        with geojson_path.open(encoding="utf-8") as f:
            feats = json.load(f)["features"]

        self.adj: dict[int, list[tuple[int, int, int, int, float]]] = defaultdict(list)
        self.node_xy: dict[int, tuple[float, float]] = {}
        self.edge_osm: dict[tuple[int, int, int], str | None] = {}
        self.edge_mid: dict[tuple[int, int, int], tuple[float, float]] = {}

        for feat in feats:
            p = feat["properties"]
            u, v, k = int(p["u"]), int(p["v"]), int(p["key"])
            coords = feat["geometry"]["coordinates"]
            lon0, lat0 = coords[0][0], coords[0][1]
            lon1, lat1 = coords[-1][0], coords[-1][1]
            self.node_xy.setdefault(u, (lat0, lon0))
            self.node_xy.setdefault(v, (lat1, lon1))
            length = float(p.get("length") or 0)
            if length <= 0:
                length = dist_m(lat0, lon0, lat1, lon1)
            self.adj[u].append((v, u, v, k, length))
            self.adj[v].append((u, u, v, k, length))
            uvk = (u, v, k)
            self.edge_osm[uvk] = norm_osmid(p.get("osmid"))
            mid = coords[len(coords) // 2]
            self.edge_mid[uvk] = (mid[1], mid[0])

    def nearest_node(self, lat: float, lon: float) -> int:
        return min(self.node_xy, key=lambda n: dist_m(lat, lon, *self.node_xy[n]))

    def shortest_path_edges(self, src: int, dst: int) -> list[tuple[int, int, int]]:
        if src == dst:
            return []
        dist: dict[int, float] = {src: 0.0}
        prev: dict[int, tuple[int, int, int, int] | None] = {src: None}
        heap = [(0.0, src)]
        seen: set[int] = set()

        while heap:
            d, u = heapq.heappop(heap)
            if u in seen:
                continue
            seen.add(u)
            if u == dst:
                break
            for nxt, eu, ev, _k, w in self.adj.get(u, []):
                nd = d + w
                if nxt not in dist or nd < dist[nxt]:
                    dist[nxt] = nd
                    prev[nxt] = (u, eu, ev, _k)
                    heapq.heappush(heap, (nd, nxt))

        if dst not in prev:
            return []

        edges: list[tuple[int, int, int]] = []
        cur = dst
        while cur != src:
            step = prev[cur]
            if step is None:
                return []
            parent, eu, ev, _k = step
            edges.append((eu, ev, _k))
            cur = parent
        edges.reverse()
        return edges


def resolve_graph_path(csv_path: Path, graph_path: Path) -> Path:
    """Use per-scenario graph when results/scenario_XX/ and a matching geojson exists."""
    if graph_path.resolve() != DEFAULT_GRAPH.resolve():
        return graph_path
    match = re.search(r"scenario_(\d+)", csv_path.as_posix())
    if not match:
        return graph_path
    sid = f"{int(match.group(1)):02d}"
    scenario_graph = ROOT / "resources" / f"campus_drive_graph_scenario_{sid}.geojson"
    if scenario_graph.is_file():
        return scenario_graph
    return graph_path


def build_osm_mapping(graph: RoadGraph):
    place_node = {name: graph.nearest_node(lat, lon) for name, (lat, lon) in PLACES.items()}
    osm_to_sim: dict[str, set[str]] = defaultdict(set)
    osm_mid: dict[str, list[tuple[str, float, float]]] = defaultdict(list)
    osm_primary: dict[str, str] = {}
    corridor: list[tuple[str, float, float]] = []

    for a, b, sim_name in SIM_ROADS:
        for uvk in graph.shortest_path_edges(place_node[a], place_node[b]):
            lat, lon = graph.edge_mid[uvk]
            corridor.append((sim_name, lat, lon))
            oid = graph.edge_osm.get(uvk)
            if not oid:
                continue
            osm_to_sim[oid].add(sim_name)
            osm_mid[oid].append((sim_name, lat, lon))

    for oid, sims in osm_to_sim.items():
        if len(sims) == 1:
            osm_primary[oid] = next(iter(sims))

    for oid, sim_name in CUSTOM_OSM_ROADS.items():
        osm_to_sim[oid].add(sim_name)
        osm_primary[oid] = sim_name

    return osm_to_sim, osm_primary, osm_mid, corridor


def pick_sim_road(osm_id, lat, lon, osm_to_sim, osm_primary, osm_mid, corridor, max_dist_m=120.0) -> str | None:
    if osm_id in CUSTOM_OSM_ROADS:
        return CUSTOM_OSM_ROADS[osm_id]
    if osm_id in osm_primary:
        return osm_primary[osm_id]
    sims = osm_to_sim.get(osm_id)
    if sims:
        if len(sims) == 1:
            return next(iter(sims))
        best, best_d = None, float("inf")
        for sim_name, slat, slon in osm_mid.get(osm_id, []):
            if sim_name not in sims:
                continue
            d = dist_m(lat, lon, slat, slon)
            if d < best_d:
                best_d, best = d, sim_name
        if best:
            return best

    best, best_d = None, max_dist_m
    for sim_name, slat, slon in corridor:
        d = dist_m(lat, lon, slat, slon)
        if d < best_d:
            best_d, best = d, sim_name
    return best


def detect_last_active_step(csv_path: Path) -> int:
    """
    Last simulation second any car is still evacuating (driving, goal not reached).
    Matches 'all cars have left campus' for the heatmap time axis.
    """
    last = 0
    with csv_path.open(encoding="utf-8-sig", newline="") as f:
        for row in csv.DictReader(f):
            if row.get("CurrentlyCarDriving", "").lower() != "true":
                continue
            if row.get("GoalReached", "").lower() == "true":
                continue
            step = int(float(row["Step"]))
            if step > last:
                last = step
    return last


def build_heatmap(
    csv_path: Path,
    graph_path: Path,
    lengths_path: Path,
    out_path: Path,
    dt: float = 1.0,
    max_time: float | None = None,
    snap_m: float = 35.0,
):
    graph_path = resolve_graph_path(csv_path, graph_path)
    print(f"Graph: {graph_path.relative_to(ROOT)}")
    graph = RoadGraph(graph_path)
    osm_to_sim, osm_primary, osm_mid, corridor = build_osm_mapping(graph)
    midpoints = build_simroad_midpoints(graph)
    road_length_m = load_road_lengths(lengths_path)
    roads = load_heatmap_roads(lengths_path)
    road_set = set(roads)

    if max_time is None:
        max_step = detect_last_active_step(csv_path)
        print(f"Auto end time: last active driver at t={max_step}s")
    else:
        max_step = int(max_time)
        print(f"Using fixed end time: t={max_step}s")

    print(f"Blueprint edges: {len(graph.edge_osm)}, mapped osm ids: {len(osm_to_sim)}")
    print(f"Sim-road midpoints: {len(midpoints)}, position snap: {snap_m}m")
    print(f"Road columns: {len(roads)}, segment lengths loaded: {len(road_length_m)}")

    step_counts: dict[int, dict[str, int]] = defaultdict(lambda: defaultdict(int))
    rows_read = mapped_rows = osm_mapped = pos_fallback = unmapped = 0

    with csv_path.open(encoding="utf-8-sig", newline="") as f:
        for row in csv.DictReader(f):
            rows_read += 1
            step = int(float(row["Step"]))
            if step > max_step:
                break
            if row.get("CurrentlyCarDriving", "").lower() != "true":
                continue
            if row.get("GoalReached", "").lower() == "true":
                continue
            lat, lon = float(row["Latitude"]), float(row["Longitude"])
            edge = norm_osmid(row.get("CurrentEdgeId", ""))
            sim = None
            if edge:
                sim = pick_sim_road(
                    edge,
                    lat,
                    lon,
                    osm_to_sim,
                    osm_primary,
                    osm_mid,
                    corridor,
                )
                if sim is not None:
                    osm_mapped += 1
            if sim is None:
                sim = nearest_simroad(lat, lon, midpoints, road_set, snap_m)
                if sim is not None:
                    pos_fallback += 1
            if sim not in road_set:
                unmapped += 1
                continue
            mapped_rows += 1
            step_counts[step][sim] += 1

    active = [r for r in roads if any(step_counts[t].get(r, 0) for t in step_counts)]
    print(
        f"CSV rows read: {rows_read:,}, mapped samples: {mapped_rows:,} "
        f"(osm: {osm_mapped:,}, position fallback: {pos_fallback:,}, unmapped: {unmapped:,}), "
        f"steps: 0–{max_step}"
    )
    print(f"Active roads ({len(active)}): {active}")

    eps = 1e-9
    out_path.parent.mkdir(parents=True, exist_ok=True)
    with out_path.open("w", encoding="utf-8", newline="") as f:
        writer = csv.writer(f)
        writer.writerow(["time"] + roads)
        t = 0
        while t <= max_step:
            counts = step_counts.get(t, {})
            row = [float(t)]
            for road in roads:
                occ = counts.get(road, 0)
                length_m = max(road_length_m.get(road, 0.0), eps)
                row.append(occ / (length_m / 100.0))
            writer.writerow(row)
            t += int(dt)

    print(f"Wrote {out_path}")


def main():
    ap = argparse.ArgumentParser(
        description="Build heatmap_matrix.csv from MARS CarletonCarDriver.csv"
    )
    ap.add_argument(
        "csv",
        nargs="?",
        type=Path,
        default=DEFAULT_CSV,
        help="CarletonCarDriver.csv path (CarDriver.csv fallback)",
    )
    ap.add_argument("--csv", dest="csv_flag", type=Path, help=argparse.SUPPRESS)
    ap.add_argument("--graph", type=Path, default=DEFAULT_GRAPH)
    ap.add_argument("--lengths", type=Path, default=DEFAULT_LENGTHS)
    ap.add_argument("--out", type=Path, default=None)
    ap.add_argument("--dt", type=float, default=1.0)
    ap.add_argument(
        "--max-time",
        type=float,
        default=None,
        help="Cap time axis in seconds (default: auto — last tick any car is still driving)",
    )
    ap.add_argument(
        "--snap-m",
        type=float,
        default=35.0,
        help="Max distance (m) to snap lat/lon to sim-road when CurrentEdgeId is missing",
    )
    args = ap.parse_args()
    csv_path = args.csv_flag or args.csv
    out_path = args.out or (csv_path.parent / "heatmap_matrix.csv")
    build_heatmap(
        csv_path, args.graph, args.lengths, out_path, args.dt, args.max_time, args.snap_m
    )


if __name__ == "__main__":
    main()
