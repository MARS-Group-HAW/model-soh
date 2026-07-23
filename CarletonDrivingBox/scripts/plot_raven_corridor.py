#!/usr/bin/env python3
"""Highlight Raven Rd & University Dr ↔ P3 & Raven Rd on the campus drive graph."""
from __future__ import annotations

import heapq
import json
import math
from collections import defaultdict
from pathlib import Path

import matplotlib.pyplot as plt
from matplotlib.collections import LineCollection
from matplotlib.lines import Line2D

ROOT = Path(__file__).resolve().parents[1]
GRAPH = ROOT / "resources" / "campus_drive_graph.geojson"
OUT = ROOT / "results" / "raven_rd_university_to_p3.png"

FROM_PLACE = "Raven Rd & University Dr"
TO_PLACE = "P3 & Raven Rd"
PLACES = {
    FROM_PLACE: (45.3840, -75.6940),
    TO_PLACE: (45.3847, -75.6919),
}


def dist_m(lat1, lon1, lat2, lon2):
    return math.hypot((lat2 - lat1) * 111_000, (lon2 - lon1) * 85_000)


def main() -> None:
    with GRAPH.open(encoding="utf-8") as f:
        feats = json.load(f)["features"]

    adj: dict[int, list[tuple[int, int, int, int, float]]] = defaultdict(list)
    node_xy: dict[int, tuple[float, float]] = {}
    edge_geom: dict[tuple[int, int, int], list] = {}
    edge_props: dict[tuple[int, int, int], dict] = {}

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
        uvk = (u, v, k)
        edge_geom[uvk] = coords
        edge_props[uvk] = p

    def nearest_node(lat: float, lon: float) -> int:
        return min(node_xy, key=lambda n: dist_m(lat, lon, *node_xy[n]))

    def shortest_path_edges(src: int, dst: int):
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

    src = nearest_node(*PLACES[FROM_PLACE])
    dst = nearest_node(*PLACES[TO_PLACE])
    path_edges, path_len = shortest_path_edges(src, dst)
    path_set = set(path_edges)

    print(f"FROM {FROM_PLACE}: OSM node {src} @ {node_xy[src]}")
    print(f"TO   {TO_PLACE}: OSM node {dst} @ {node_xy[dst]}")
    print(f"path_length_m={path_len:.2f} n_edges={len(path_edges)}")
    print("--- edges ---")
    for i, uvk in enumerate(path_edges):
        p = edge_props[uvk]
        print(
            f"{i}: u={uvk[0]} v={uvk[1]} key={uvk[2]} "
            f"osmid={p.get('osmid')} name={p.get('name')!r} "
            f"highway={p.get('highway')} length={float(p.get('length') or 0):.1f}m"
        )

    OUT.parent.mkdir(parents=True, exist_ok=True)
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
        (FROM_PLACE, PLACES[FROM_PLACE], "#1d3557"),
        (TO_PLACE, PLACES[TO_PLACE], "#2a9d8f"),
    ]:
        n = nearest_node(lat, lon)
        nlat, nlon = node_xy[n]
        ax.scatter(
            [nlon], [nlat], s=90, c=color, zorder=5, edgecolors="white", linewidths=1.2
        )
        dx = -0.0004 if "University" in label else 0.0003
        dy = -0.00035 if "University" in label else 0.00028
        ax.annotate(
            label,
            (nlon, nlat),
            xytext=(nlon + dx, nlat + dy),
            fontsize=8,
            fontweight="bold",
            color=color,
            ha="right" if "University" in label else "left",
            arrowprops=dict(arrowstyle="-", color=color, lw=0.8),
            zorder=6,
        )

    lons = [c[0] for uvk in path_edges for c in edge_geom[uvk]]
    lats = [c[1] for uvk in path_edges for c in edge_geom[uvk]]
    pad = 0.0022
    ax.set_xlim(min(lons) - pad, max(lons) + pad)
    ax.set_ylim(min(lats) - pad, max(lats) + pad)
    ax.set_aspect("equal", adjustable="box")
    ax.set_xlabel("Longitude")
    ax.set_ylabel("Latitude")
    ax.set_title(
        "Campus corridor: Raven Rd & University Dr → P3 & Raven Rd\n"
        "(Raven Road between University Drive and the P3 parking area)",
        fontsize=11,
    )
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
            label=FROM_PLACE,
        ),
        Line2D(
            [0],
            [0],
            marker="o",
            color="w",
            markerfacecolor="#2a9d8f",
            markersize=9,
            label=TO_PLACE,
        ),
    ]
    ax.legend(handles=legend, loc="lower left", fontsize=8, framealpha=0.92)
    ax.text(
        0.98,
        0.02,
        "Sim name is directional; reverse exists as\n"
        "'P3 & Raven Rd to Raven Rd & University Dr'\n"
        f"(~{path_len:.0f} m; sim FROM_NODE=9, TO_NODE=15)",
        transform=ax.transAxes,
        ha="right",
        va="bottom",
        fontsize=7.5,
        color="#333",
        bbox=dict(boxstyle="round,pad=0.35", fc="white", ec="#ccc", alpha=0.9),
    )

    fig.tight_layout()
    fig.savefig(OUT, bbox_inches="tight")
    print(f"SAVED {OUT}")


if __name__ == "__main__":
    main()
