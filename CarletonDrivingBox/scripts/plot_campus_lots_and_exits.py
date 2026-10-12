#!/usr/bin/env python3
"""Publication figure: parking lots P1–P7 and four campus leave exits.

Uses ``resources/campus_drive_graph_scenario_07.geojson`` (includes the Raven
emergency link) plus ``resources/parking_lot_spawns.csv`` box bounds, and the
canonical gate coordinates from ``analyze_run.py``.

Output (default): ``docs/campus_lots_and_exits.png``
"""
from __future__ import annotations

import argparse
import csv
import json
import math
from pathlib import Path

import matplotlib.pyplot as plt
from matplotlib.collections import LineCollection, PatchCollection
from matplotlib.patches import Rectangle

ROOT = Path(__file__).resolve().parents[1]
DEFAULT_GRAPH = ROOT / "resources" / "campus_drive_graph_scenario_07.geojson"
FALLBACK_GRAPH = ROOT / "resources" / "campus_drive_graph.geojson"
PARKING_SPAWNS = ROOT / "resources" / "parking_lot_spawns.csv"
DEFAULT_OUT = ROOT / "docs" / "campus_lots_and_exits.png"

LOT_ORDER = ["P1", "P2", "P3", "P4", "P5", "P6", "P7"]
LOT_COLORS = {
    "P1": "#e41a1c",
    "P2": "#377eb8",
    "P3": "#4daf4a",
    "P4": "#984ea3",
    "P5": "#ff7f00",
    "P6": "#a65628",
    "P7": "#f781bf",
}

# Four primary campus leave gates (lat, lon) — same as analyze_run.CAMPUS_EXIT_POINTS.
CAMPUS_EXITS: list[tuple[str, float, float, str]] = [
    # label, lat, lon, short legend text
    ("Colonel By\n(SW)", 45.3792575, -75.7004525, "Colonel By (SW)"),
    ("Bronson Ave &\nUniversity Dr", 45.3896198, -75.694494, "Bronson & University Dr"),
    ("Stadium Way", 45.3881843, -75.6925574, "Stadium Way"),
    ("Raven Rd\nemergency", 45.3850997, -75.6901823, "Raven Rd emergency"),
]

# Off-campus destinations (optional markers; not campus leave gates).
DESTINATIONS = {
    "Brewer Park (NE dest.)": (45.387983, -75.690183),
    # Hogs Back Plaza, 888 Meadowlands Dr E, Ottawa (SW sink; clearance = campus exit).
    "Hogs Back Plaza\n(SW dest.)": (45.367764, -75.702286),
}

EXIT_FACE = "#c0392b"
EXIT_EDGE = "#7b241c"
DEST_FACE = "#2c3e50"


def load_graph_segments(graph_path: Path) -> list[list[tuple[float, float]]]:
    with graph_path.open(encoding="utf-8") as f:
        feats = json.load(f)["features"]
    segments: list[list[tuple[float, float]]] = []
    for feat in feats:
        geom = feat.get("geometry") or {}
        if geom.get("type") != "LineString":
            continue
        # GeoJSON is [lon, lat]; store as (lon, lat) for plotting.
        segments.append([(c[0], c[1]) for c in geom["coordinates"]])
    return segments


def load_lots(path: Path) -> dict[str, dict]:
    lots: dict[str, dict] = {}
    with path.open(encoding="utf-8") as f:
        for row in csv.DictReader(f):
            lot = (row.get("lot") or "").strip()
            if not lot:
                continue
            lots[lot] = {
                "spawn": (float(row["spawn_lat"]), float(row["spawn_lon"])),
                "box": (
                    float(row["box_south"]),
                    float(row["box_west"]),
                    float(row["box_north"]),
                    float(row["box_east"]),
                ),
            }
    return lots


def _label_offset(ax, lon: float, lat: float, dx: float, dy: float, text: str, **kwargs):
    ax.annotate(
        text,
        xy=(lon, lat),
        xytext=(lon + dx, lat + dy),
        textcoords="data",
        fontsize=kwargs.get("fontsize", 8),
        fontweight=kwargs.get("fontweight", "bold"),
        color=kwargs.get("color", "#1a1a1a"),
        ha=kwargs.get("ha", "center"),
        va=kwargs.get("va", "center"),
        bbox=dict(
            boxstyle="round,pad=0.28",
            facecolor=kwargs.get("facecolor", "white"),
            edgecolor=kwargs.get("edgecolor", "#333333"),
            linewidth=kwargs.get("box_lw", 0.8),
            alpha=0.95,
        ),
        arrowprops=dict(
            arrowstyle="-",
            color=kwargs.get("edgecolor", "#333333"),
            lw=0.8,
            shrinkA=0,
            shrinkB=2,
        )
        if kwargs.get("arrow", True)
        else None,
        zorder=6,
    )


def plot_map(
    graph_path: Path,
    lots: dict[str, dict],
    out_path: Path,
    dpi: int = 220,
    show_destinations: bool = True,
) -> None:
    segments = load_graph_segments(graph_path)

    fig, ax = plt.subplots(figsize=(8.8, 9.6))
    fig.patch.set_facecolor("white")
    ax.set_facecolor("#f7f7f5")

    if segments:
        bg = LineCollection(
            segments,
            colors="#b0b0b0",
            linewidths=0.55,
            zorder=1,
            alpha=0.9,
        )
        ax.add_collection(bg)

    # Lot footprints
    lot_patches = []
    lot_face = []
    for lot in LOT_ORDER:
        info = lots.get(lot)
        if not info:
            continue
        south, west, north, east = info["box"]
        rect = Rectangle((west, south), east - west, north - south)
        lot_patches.append(rect)
        lot_face.append(LOT_COLORS[lot])
    if lot_patches:
        pc = PatchCollection(
            lot_patches,
            facecolors=lot_face,
            edgecolors="#222222",
            linewidths=0.7,
            alpha=0.40,
            zorder=2,
        )
        ax.add_collection(pc)

    # Lot spawn markers + labels (offsets tuned for Carleton footprint).
    # P1 must stay NW of its spawn — south puts the callout on the Colonel By gate.
    lot_label_xy = {
        "P1": (-0.00155, 0.00055),
        "P2": (-0.00165, 0.00005),
        "P3": (-0.00010, 0.00085),
        "P4": (-0.00155, -0.00015),
        "P5": (-0.00170, 0.00045),
        "P6": (-0.00185, 0.00035),
        "P7": (0.00005, 0.00120),
    }
    for lot in LOT_ORDER:
        info = lots.get(lot)
        if not info:
            continue
        lat, lon = info["spawn"]
        color = LOT_COLORS[lot]
        ax.scatter(
            [lon],
            [lat],
            c=color,
            s=58,
            marker="o",
            edgecolors="black",
            linewidths=0.6,
            zorder=4,
        )
        dx, dy = lot_label_xy.get(lot, (0.0012, 0.0006))
        _label_offset(
            ax,
            lon,
            lat,
            dx,
            dy,
            lot,
            fontsize=9,
            facecolor="white",
            edgecolor=color,
            box_lw=1.2,
            color="#111111",
        )

    # Exit gates — push callouts outward so they clear lot labels / each other.
    # Colonel By sits at the SW campus gate; label goes further SW into expanded pad.
    exit_offsets = {
        "Colonel By\n(SW)": (-0.00135, -0.00185),
        "Bronson Ave &\nUniversity Dr": (0.00040, 0.00135),
        "Stadium Way": (0.00220, 0.00055),
        "Raven Rd\nemergency": (0.00225, -0.00105),
    }
    dest_offsets = {
        "Brewer Park (NE dest.)": (0.00035, -0.00125),
        "Hogs Back Plaza\n(SW dest.)": (0.00055, -0.00095),
    }
    for label, lat, lon, _ in CAMPUS_EXITS:
        is_sw = label.startswith("Colonel By")
        ax.scatter(
            [lon],
            [lat],
            c=EXIT_FACE,
            s=175 if is_sw else 120,
            marker="*",
            edgecolors=EXIT_EDGE,
            linewidths=0.85 if is_sw else 0.7,
            zorder=5,
        )
        dx, dy = exit_offsets.get(label, (0.0015, 0.0008))
        _label_offset(
            ax,
            lon,
            lat,
            dx,
            dy,
            label,
            fontsize=8.0 if is_sw else 7.5,
            facecolor="#fff5f5",
            edgecolor=EXIT_FACE,
            box_lw=1.25 if is_sw else 1.15,
            color=EXIT_EDGE,
            fontweight="bold",
        )

    if show_destinations:
        for name, (lat, lon) in DESTINATIONS.items():
            ax.scatter(
                [lon],
                [lat],
                c="none",
                s=90,
                marker="D",
                edgecolors=DEST_FACE,
                linewidths=1.2,
                zorder=5,
            )
            dx, dy = dest_offsets.get(name, (0.00035, -0.00125))
            _label_offset(
                ax,
                lon,
                lat,
                dx,
                dy,
                name,
                fontsize=7,
                facecolor="#eef2f5",
                edgecolor=DEST_FACE,
                color=DEST_FACE,
                fontweight="normal",
            )

    # Extent: campus core + off-campus destinations (Brewer NE, Hogs Back Plaza SW).
    # Include exit/lot label anchors so SW callouts are not clipped by axes.
    lats = [info["spawn"][0] for info in lots.values()]
    lons = [info["spawn"][1] for info in lots.values()]
    for _, lat, lon, _ in CAMPUS_EXITS:
        lats.append(lat)
        lons.append(lon)
    for lat, lon in DESTINATIONS.values():
        lats.append(lat)
        lons.append(lon)
    for info in lots.values():
        south, west, north, east = info["box"]
        lats.extend([south, north])
        lons.extend([west, east])
    for lot, (dx, dy) in lot_label_xy.items():
        info = lots.get(lot)
        if not info:
            continue
        lat, lon = info["spawn"]
        lats.append(lat + dy)
        lons.append(lon + dx)
    for label, lat, lon, _ in CAMPUS_EXITS:
        dx, dy = exit_offsets.get(label, (0.0, 0.0))
        lats.append(lat + dy)
        lons.append(lon + dx)

    # Extra SW pad: Colonel By is the southernmost/westernmost gate and was easy
    # to miss when west padding was cut to 0.55× and the P1 label sat on the star.
    pad_lat, pad_lon = 0.00240, 0.00260
    ax.set_xlim(min(lons) - pad_lon, max(lons) + pad_lon * 0.85)
    ax.set_ylim(min(lats) - pad_lat, max(lats) + pad_lat * 0.70)

    # Approximate local equal aspect (lon compressed at ~45.4°N)
    mid_lat = 0.5 * (min(lats) + max(lats))
    ax.set_aspect(1.0 / math.cos(math.radians(mid_lat)), adjustable="box")

    ax.set_xlabel("Longitude")
    ax.set_ylabel("Latitude")
    ax.set_title(
        "Carleton University campus — parking lots P1–P7 and leave exits",
        fontsize=11,
        pad=10,
    )
    ax.grid(True, color="#dddddd", linewidth=0.5, zorder=0)

    out_path.parent.mkdir(parents=True, exist_ok=True)
    fig.tight_layout()
    fig.savefig(out_path, dpi=dpi, bbox_inches="tight", facecolor="white")
    plt.close(fig)
    print(f"Wrote {out_path}  (graph={graph_path.name})")


def main() -> None:
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument(
        "--graph",
        type=Path,
        default=None,
        help="Drive-graph GeoJSON (default: scenario_07 graph so Raven link is visible)",
    )
    ap.add_argument("--out", type=Path, default=DEFAULT_OUT, help="Output PNG path")
    ap.add_argument("--dpi", type=int, default=220)
    ap.add_argument(
        "--no-destinations",
        action="store_true",
        help="Omit off-campus destination markers (Brewer Park, Hogs Back Plaza)",
    )
    args = ap.parse_args()

    graph = args.graph
    if graph is None:
        graph = DEFAULT_GRAPH if DEFAULT_GRAPH.is_file() else FALLBACK_GRAPH
    if not graph.is_file():
        raise SystemExit(f"Missing drive graph: {graph}")
    if not PARKING_SPAWNS.is_file():
        raise SystemExit(f"Missing parking spawns: {PARKING_SPAWNS}")

    lots = load_lots(PARKING_SPAWNS)
    missing = [lot for lot in LOT_ORDER if lot not in lots]
    if missing:
        raise SystemExit(f"parking_lot_spawns.csv missing lots: {missing}")

    plot_map(
        graph_path=graph,
        lots=lots,
        out_path=args.out,
        dpi=args.dpi,
        show_destinations=not args.no_destinations,
    )


if __name__ == "__main__":
    main()
