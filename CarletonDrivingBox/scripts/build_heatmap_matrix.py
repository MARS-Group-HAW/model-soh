#!/usr/bin/env python3
"""
Build MARS heatmap_matrix.csv — campus congestion by named road over time.

Maps agent positions onto every corridor in resources/sim_road_lengths.csv
and writes vehicles-per-100m for the full evacuation.

Corridor mapping uses shortest paths on the scenario graph between named
places. If a corridor is absent from the graph (e.g. the Raven→Bronson
emergency link only in scenario_07+), Dijkstra would otherwise detour through
unrelated campus roads and mis-label them. Paths whose length exceeds
MAX_PATH_LENGTH_RATIO × expected LENGTH_M are skipped so those columns stay 0.
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

# Reject place-to-place shortest paths that are clearly detours (missing edge).
MAX_PATH_LENGTH_RATIO = 1.75

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

# (from_place, to_place, sim_road_name) — covers all campus corridors in sim_road_lengths.csv
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


def dist_m(lat1, lon1, lat2, lon2):
    return math.hypot((lat2 - lat1) * 111_000, (lon2 - lon1) * 85_000)


def norm_osmid(val):
    """Normalize CurrentEdgeId; multi-id values use the first OSM id."""
    if val is None:
        return None
    if isinstance(val, list):
        return str(val[0]).strip() if val else None
    s = str(val).strip()
    if not s or s == "-1":
        return None
    if s[0] == "[":
        inner = s.strip("[]")
        first = inner.split(",")[0].strip().strip("'\"") if inner else ""
        return first or None
    if "," in s:
        s = s.split(",", 1)[0].strip()
    return s or None


def repair_csv_row(header: list[str], row: list[str]) -> list[str] | None:
    """Fix rows where unquoted multi-id CurrentEdgeId shifts columns."""
    if len(row) == len(header):
        return row
    try:
        edge_i = header.index("CurrentEdgeId")
    except ValueError:
        return None
    if len(row) == len(header) + 1 and edge_i + 1 < len(row):
        merged = f"{row[edge_i]},{row[edge_i + 1]}"
        return row[:edge_i] + [merged] + row[edge_i + 2 :]
    return None


class RoadGraph:
    def __init__(self, geojson_path: Path):
        with geojson_path.open(encoding="utf-8") as f:
            feats = json.load(f)["features"]

        self.adj: dict[int, list[tuple[int, int, int, int, float]]] = defaultdict(list)
        self.node_xy: dict[int, tuple[float, float]] = {}
        self.edge_osm: dict[tuple[int, int, int], str | None] = {}
        self.edge_mid: dict[tuple[int, int, int], tuple[float, float]] = {}
        self.edge_len: dict[tuple[int, int, int], float] = {}

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
            self.edge_len[uvk] = length
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


def path_length_m(graph: RoadGraph, edges: list[tuple[int, int, int]]) -> float:
    return sum(graph.edge_len.get(uvk, 0.0) for uvk in edges)


def corridor_edges_in_graph(
    graph: RoadGraph,
    place_node: dict[str, int],
    a: str,
    b: str,
    sim_name: str,
    expected_lengths: dict[str, float] | None = None,
) -> list[tuple[int, int, int]] | None:
    """Return shortest-path edges for a named corridor, or None if missing/detour."""
    edges = graph.shortest_path_edges(place_node[a], place_node[b])
    if not edges:
        return None
    if expected_lengths:
        expected = expected_lengths.get(sim_name)
        if expected and expected > 0:
            plen = path_length_m(graph, edges)
            if plen > expected * MAX_PATH_LENGTH_RATIO:
                return None
    return edges


def build_simroad_midpoints(
    graph: RoadGraph,
    expected_lengths: dict[str, float] | None = None,
) -> list[tuple[str, float, float]]:
    place_node = {name: graph.nearest_node(lat, lon) for name, (lat, lon) in PLACES.items()}
    midpoints: list[tuple[str, float, float]] = []
    seen: set[tuple[str, float, float]] = set()

    for a, b, sim_name in SIM_ROADS:
        edges = corridor_edges_in_graph(
            graph, place_node, a, b, sim_name, expected_lengths
        )
        if not edges:
            continue
        for uvk in edges:
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


def load_road_lengths(path: Path) -> dict[str, float]:
    lengths: dict[str, float] = {}
    if not path.is_file():
        return lengths
    with path.open(encoding="utf-8", newline="") as f:
        for row in csv.DictReader(f):
            name = (row.get("ROAD") or "").strip()
            if name:
                lengths[name] = float(row["LENGTH_M"])
    return lengths


def load_heatmap_roads(lengths_path: Path) -> list[str]:
    """Every named campus corridor (full bidirectional list from lengths CSV)."""
    roads: list[str] = []
    if lengths_path.is_file():
        with lengths_path.open(encoding="utf-8", newline="") as f:
            for row in csv.DictReader(f):
                name = (row.get("ROAD") or "").strip()
                if name and name not in roads:
                    roads.append(name)
    if roads:
        return roads
    # Fallback if lengths file missing
    return [name for _, _, name in SIM_ROADS]


def resolve_graph_path(csv_path: Path, graph_path: Path) -> Path:
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


def build_osm_mapping(
    graph: RoadGraph,
    expected_lengths: dict[str, float] | None = None,
):
    place_node = {name: graph.nearest_node(lat, lon) for name, (lat, lon) in PLACES.items()}
    osm_to_sim: dict[str, set[str]] = defaultdict(set)
    osm_mid: dict[str, list[tuple[str, float, float]]] = defaultdict(list)
    osm_primary: dict[str, str] = {}
    corridor: list[tuple[str, float, float]] = []
    skipped: list[str] = []

    for a, b, sim_name in SIM_ROADS:
        edges = corridor_edges_in_graph(
            graph, place_node, a, b, sim_name, expected_lengths
        )
        if not edges:
            skipped.append(sim_name)
            continue
        for uvk in edges:
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

    # Only bind synthetic OSM ids that actually exist in this scenario graph.
    present_osm = {oid for oid in graph.edge_osm.values() if oid}
    for oid, sim_name in CUSTOM_OSM_ROADS.items():
        if oid not in present_osm:
            continue
        osm_to_sim[oid].add(sim_name)
        osm_primary[oid] = sim_name

    return osm_to_sim, osm_primary, osm_mid, corridor, skipped


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
    last = 0
    with csv_path.open(encoding="utf-8-sig", newline="") as f:
        reader = csv.reader(f)
        header = next(reader)
        for raw in reader:
            row_list = repair_csv_row(header, raw)
            if row_list is None:
                continue
            row = dict(zip(header, row_list))
            if row.get("CurrentlyCarDriving", "").lower() != "true":
                continue
            if row.get("GoalReached", "").lower() == "true":
                continue
            step_s = row.get("Step") or row.get("Tick") or "0"
            try:
                step = int(float(step_s))
            except ValueError:
                continue
            if step > last:
                last = step
    return last


def build_heatmap(
    csv_path: Path,
    graph_path: Path,
    lengths_path: Path,
    out_path: Path,
    dt: float = 10.0,
    max_time: float | None = None,
    snap_m: float = 50.0,
):
    graph_path = resolve_graph_path(csv_path, graph_path)
    print(f"Graph: {graph_path.relative_to(ROOT)}")
    graph = RoadGraph(graph_path)
    road_length_m = load_road_lengths(lengths_path)
    osm_to_sim, osm_primary, osm_mid, corridor, skipped = build_osm_mapping(
        graph, road_length_m
    )
    midpoints = build_simroad_midpoints(graph, road_length_m)
    roads = load_heatmap_roads(lengths_path)
    road_set = set(roads)

    if max_time is None:
        max_step = detect_last_active_step(csv_path)
        print(f"Auto end time: last active driver at t={max_step}s")
    else:
        max_step = int(max_time)
        print(f"Using fixed end time: t={max_step}s")

    sample_dt = max(int(dt), 1)
    print(f"Blueprint edges: {len(graph.edge_osm)}, mapped osm ids: {len(osm_to_sim)}")
    print(f"Sim-road midpoints: {len(midpoints)}, position snap: {snap_m}m")
    if skipped:
        print(
            f"Skipped {len(skipped)} corridor(s) absent/detoured in this graph "
            f"(path > {MAX_PATH_LENGTH_RATIO}× expected length):"
        )
        for name in skipped:
            print(f"  - {name}")
    print(f"Road columns (all campus): {len(roads)}, lengths: {len(road_length_m)}")
    print(f"Sample interval dt={sample_dt}s")

    step_counts: dict[int, dict[str, int]] = defaultdict(lambda: defaultdict(int))
    peak_by_road: dict[str, int] = defaultdict(int)
    rows_read = mapped_rows = osm_mapped = pos_fallback = unmapped = repaired = 0

    with csv_path.open(encoding="utf-8-sig", newline="") as f:
        reader = csv.reader(f)
        header = next(reader)
        for raw in reader:
            rows_read += 1
            row_list = repair_csv_row(header, raw)
            if row_list is None:
                unmapped += 1
                continue
            if len(raw) != len(header):
                repaired += 1
            row = dict(zip(header, row_list))
            step_s = row.get("Step") or row.get("Tick") or "0"
            try:
                step = int(float(step_s))
            except ValueError:
                continue
            if step > max_step:
                break
            if row.get("CurrentlyCarDriving", "").lower() != "true":
                continue
            if row.get("GoalReached", "").lower() == "true":
                continue
            try:
                lat, lon = float(row["Latitude"]), float(row["Longitude"])
            except (KeyError, ValueError):
                continue

            edge = norm_osmid(row.get("CurrentEdgeId", ""))
            sim = None
            if edge:
                sim = pick_sim_road(
                    edge, lat, lon, osm_to_sim, osm_primary, osm_mid, corridor
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
            if step % sample_dt != 0:
                continue
            step_counts[step][sim] += 1
            if step_counts[step][sim] > peak_by_road[sim]:
                peak_by_road[sim] = step_counts[step][sim]

    active = [r for r in roads if peak_by_road.get(r, 0) > 0]
    print(
        f"CSV rows read: {rows_read:,}, mapped samples: {mapped_rows:,} "
        f"(osm: {osm_mapped:,}, position fallback: {pos_fallback:,}, "
        f"repaired: {repaired:,}, unmapped: {unmapped:,})"
    )
    print(f"Active roads {len(active)}/{len(roads)} (all roads still written to matrix)")

    eps = 1e-9
    out_path.parent.mkdir(parents=True, exist_ok=True)
    with out_path.open("w", encoding="utf-8", newline="") as f:
        writer = csv.writer(f)
        writer.writerow(["time"] + roads)
        t = 0
        while t <= max_step:
            counts = step_counts.get(t, {})
            row_out = [float(t)]
            for road in roads:
                occ = counts.get(road, 0)
                length_m = max(road_length_m.get(road, 0.0), eps)
                row_out.append(occ / (length_m / 100.0))
            writer.writerow(row_out)
            t += sample_dt

    print(f"Wrote {out_path}")

    summary_path = out_path.with_name("heatmap_road_peaks.csv")
    with summary_path.open("w", encoding="utf-8", newline="") as f:
        w = csv.writer(f)
        w.writerow(["road", "peak_cars_in_sample", "length_m", "peak_cars_per_100m"])
        for road in roads:
            length_m = max(road_length_m.get(road, 0.0), eps)
            peak = peak_by_road.get(road, 0)
            w.writerow([road, peak, round(length_m, 2), round(peak / (length_m / 100.0), 3)])
    hottest = sorted(peak_by_road.items(), key=lambda kv: kv[1], reverse=True)[:10]
    if hottest:
        print("Hottest corridors (peak cars in a sample):")
        for road, peak in hottest:
            print(f"  {peak:4d}  {road}")
    print(f"Wrote {summary_path}")


def main():
    ap = argparse.ArgumentParser(
        description="Build full-campus road congestion heatmap_matrix.csv from MARS agent CSV"
    )
    ap.add_argument("csv", nargs="?", type=Path, default=DEFAULT_CSV)
    ap.add_argument("--csv", dest="csv_flag", type=Path, help=argparse.SUPPRESS)
    ap.add_argument("--graph", type=Path, default=DEFAULT_GRAPH)
    ap.add_argument("--lengths", type=Path, default=DEFAULT_LENGTHS)
    ap.add_argument("--out", type=Path, default=None)
    ap.add_argument(
        "--dt",
        type=float,
        default=10.0,
        help="Sample interval in seconds (default 10)",
    )
    ap.add_argument("--max-time", type=float, default=None)
    ap.add_argument(
        "--snap-m",
        type=float,
        default=50.0,
        help="Max meters to snap lat/lon to a corridor when edge id is missing",
    )
    args = ap.parse_args()
    csv_path = args.csv_flag or args.csv
    out_path = args.out or (csv_path.parent / "heatmap_matrix.csv")
    build_heatmap(
        csv_path, args.graph, args.lengths, out_path, args.dt, args.max_time, args.snap_m
    )


if __name__ == "__main__":
    main()
