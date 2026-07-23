#!/usr/bin/env python3
"""Plot every campus corridor from sim_road_lengths.csv as a titled PNG map."""
from __future__ import annotations

import csv
import heapq
import json
import math
import re
from collections import defaultdict
from pathlib import Path

import matplotlib.pyplot as plt
from matplotlib.collections import LineCollection
from matplotlib.lines import Line2D

ROOT = Path(__file__).resolve().parents[1]
# Prefer scenario_07 graph: includes Raven→Bronson emergency link used by heatmap corridors.
GRAPH_CANDIDATES = [
    ROOT / "resources" / "campus_drive_graph_scenario_07.geojson",
    ROOT / "resources" / "campus_drive_graph.geojson",
]
LENGTHS = ROOT / "resources" / "sim_road_lengths.csv"
OUT_DIR = ROOT / "results" / "road_corridor_maps"

# Same place coordinates as build_heatmap_matrix.py
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


def dist_m(lat1, lon1, lat2, lon2):
    return math.hypot((lat2 - lat1) * 111_000, (lon2 - lon1) * 85_000)


def safe_filename(road_name: str) -> str:
    """Derive a filesystem-safe PNG stem from the sim road name."""
    s = road_name.strip()
    s = s.replace(" & ", "_and_")
    s = s.replace(" to ", "_to_")
    s = re.sub(r"[^\w\-]+", "_", s, flags=re.UNICODE)
    s = re.sub(r"_+", "_", s).strip("_")
    return s or "road"


def load_roads(path: Path) -> list[tuple[str, str, str]]:
    """Return [(road_name, from_place, to_place), ...] in CSV order (heatmap columns)."""
    roads: list[tuple[str, str, str]] = []
    with path.open(encoding="utf-8", newline="") as f:
        for row in csv.DictReader(f):
            name = (row.get("ROAD") or "").strip()
            frm = (row.get("FROM_PLACE") or "").strip()
            to = (row.get("TO_PLACE") or "").strip()
            if name and frm and to:
                roads.append((name, frm, to))
    return roads


def load_graph(path: Path):
    with path.open(encoding="utf-8") as f:
        feats = json.load(f)["features"]

    adj: dict[int, list[tuple[int, int, int, int, float]]] = defaultdict(list)
    node_xy: dict[int, tuple[float, float]] = {}
    edge_geom: dict[tuple[int, int, int], list] = {}

    for feat in feats:
        p = feat["properties"]
        u, v, k = int(p["u"]), int(p["v"]), int(p["key"])
        coords = feat["geometry"]["coordinates"]
        lon0, lat0 = coords[0][0], coords[0][1]
        lon1, lat1 = coords[-1][0], coords[-1][1]
        node_xy.setdefault(u, (lat0, lon0))
        node_xy.setdefault(v, (lat1, lon1))
        length = float(p.get("length") or 0) or dist_m(lat0, lon0, lat1, lon1)
        adj[u].append((v, u, v, k, length))
        adj[v].append((u, u, v, k, length))
        edge_geom[(u, v, k)] = coords

    # Prefer the main campus drive component (parking-lot islands can steal snaps).
    seen: set[int] = set()
    components: list[set[int]] = []
    for n in node_xy:
        if n in seen:
            continue
        stack = [n]
        comp: set[int] = set()
        seen.add(n)
        while stack:
            u = stack.pop()
            comp.add(u)
            for nxt, *_ in adj.get(u, []):
                if nxt not in seen:
                    seen.add(nxt)
                    stack.append(nxt)
        components.append(comp)
    main_nodes = max(components, key=len) if components else set(node_xy)

    return adj, node_xy, edge_geom, main_nodes


def nearest_node(node_xy, lat: float, lon: float, candidates: set[int] | None = None) -> int:
    pool = candidates if candidates else set(node_xy)
    return min(pool, key=lambda n: dist_m(lat, lon, *node_xy[n]))


def shortest_path_edges(adj, src: int, dst: int):
    if src == dst:
        return [], 0.0
    dist = {src: 0.0}
    prev = {src: None}
    heap = [(0.0, src)]
    seen: set[int] = set()
    while heap:
        d, u = heapq.heappop(heap)
        if u in seen:
            continue
        seen.add(u)
        if u == dst:
            break
        for nxt, eu, ev, k, w in adj.get(u, []):
            nd = d + w
            if nxt not in dist or nd < dist[nxt]:
                dist[nxt] = nd
                prev[nxt] = (u, eu, ev, k)
                heapq.heappush(heap, (nd, nxt))
    if dst not in prev:
        return [], float("inf")
    edges = []
    cur = dst
    while cur != src:
        parent, eu, ev, k = prev[cur]
        edges.append((eu, ev, k))
        cur = parent
    edges.reverse()
    return edges, dist[dst]


def plot_corridor(
    road_name: str,
    from_place: str,
    to_place: str,
    adj,
    node_xy,
    edge_geom,
    main_nodes: set[int],
    out_path: Path,
) -> float:
    if from_place not in PLACES or to_place not in PLACES:
        raise KeyError(f"Unknown place for {road_name!r}: {from_place!r} -> {to_place!r}")

    src = nearest_node(node_xy, *PLACES[from_place], main_nodes)
    dst = nearest_node(node_xy, *PLACES[to_place], main_nodes)
    path_edges, path_len = shortest_path_edges(adj, src, dst)
    if not path_edges and path_len == float("inf"):
        raise RuntimeError(f"No path for {road_name}")

    path_set = set(path_edges)
    fig, ax = plt.subplots(figsize=(10, 10), dpi=160)

    bg_segs, hi_segs = [], []
    for uvk, coords in edge_geom.items():
        seg = [(c[0], c[1]) for c in coords]
        if uvk in path_set:
            hi_segs.append(seg)
        else:
            bg_segs.append(seg)

    ax.add_collection(
        LineCollection(bg_segs, colors="#b0b7c0", linewidths=0.7, alpha=0.45, zorder=1)
    )
    ax.add_collection(
        LineCollection(hi_segs, colors="#e63946", linewidths=3.8, alpha=0.95, zorder=3)
    )

    for label, (lat, lon), color in [
        (f"FROM: {from_place}", PLACES[from_place], "#1d3557"),
        (f"TO: {to_place}", PLACES[to_place], "#2a9d8f"),
    ]:
        n = nearest_node(node_xy, lat, lon, main_nodes)
        nlat, nlon = node_xy[n]
        ax.scatter(
            [nlon], [nlat], s=90, c=color, zorder=5, edgecolors="white", linewidths=1.2
        )
        ax.annotate(
            label,
            (nlon, nlat),
            xytext=(8, 8),
            textcoords="offset points",
            fontsize=7.5,
            fontweight="bold",
            color=color,
            ha="left",
            arrowprops=dict(arrowstyle="-", color=color, lw=0.8),
            zorder=6,
        )

    if path_edges:
        lons = [c[0] for uvk in path_edges for c in edge_geom[uvk]]
        lats = [c[1] for uvk in path_edges for c in edge_geom[uvk]]
    else:
        lons = [PLACES[from_place][1], PLACES[to_place][1]]
        lats = [PLACES[from_place][0], PLACES[to_place][0]]
    pad = 0.0022
    ax.set_xlim(min(lons) - pad, max(lons) + pad)
    ax.set_ylim(min(lats) - pad, max(lats) + pad)
    ax.set_aspect("equal", adjustable="box")
    ax.set_xlabel("Longitude")
    ax.set_ylabel("Latitude")
    ax.set_title(road_name, fontsize=12, fontweight="bold", pad=12)
    ax.grid(True, alpha=0.25, linestyle=":")

    legend = [
        Line2D([0], [0], color="#e63946", lw=3.5, label="Highlighted corridor"),
        Line2D(
            [0], [0], color="#b0b7c0", lw=1.2, alpha=0.7, label="Other campus drive edges"
        ),
        Line2D(
            [0],
            [0],
            marker="o",
            color="w",
            markerfacecolor="#1d3557",
            markersize=9,
            label=from_place,
        ),
        Line2D(
            [0],
            [0],
            marker="o",
            color="w",
            markerfacecolor="#2a9d8f",
            markersize=9,
            label=to_place,
        ),
    ]
    ax.legend(handles=legend, loc="lower left", fontsize=7.5, framealpha=0.92)
    ax.text(
        0.98,
        0.02,
        f"~{path_len:.0f} m · {len(path_edges)} edge(s)",
        transform=ax.transAxes,
        ha="right",
        va="bottom",
        fontsize=7.5,
        color="#333",
        bbox=dict(boxstyle="round,pad=0.35", fc="white", ec="#ccc", alpha=0.9),
    )

    fig.tight_layout()
    out_path.parent.mkdir(parents=True, exist_ok=True)
    fig.savefig(out_path, bbox_inches="tight")
    plt.close(fig)
    return path_len


def main() -> None:
    roads = load_roads(LENGTHS)
    if not roads:
        raise SystemExit(f"No roads found in {LENGTHS}")

    graph_path = next((p for p in GRAPH_CANDIDATES if p.is_file()), None)
    if graph_path is None:
        raise SystemExit("No campus drive graph found under resources/")
    print(f"Graph: {graph_path.relative_to(ROOT)}")

    adj, node_xy, edge_geom, main_nodes = load_graph(graph_path)
    OUT_DIR.mkdir(parents=True, exist_ok=True)

    # Clear prior PNGs so the folder matches exactly this run
    for old in OUT_DIR.glob("*.png"):
        old.unlink()

    written: list[Path] = []
    for i, (road_name, frm, to) in enumerate(roads, 1):
        out = OUT_DIR / f"{safe_filename(road_name)}.png"
        path_len = plot_corridor(
            road_name, frm, to, adj, node_xy, edge_geom, main_nodes, out
        )
        written.append(out)
        print(f"[{i:02d}/{len(roads)}] {road_name}  (~{path_len:.0f} m) -> {out.name}")

    print(f"\nWrote {len(written)} PNGs to {OUT_DIR}")


if __name__ == "__main__":
    main()
