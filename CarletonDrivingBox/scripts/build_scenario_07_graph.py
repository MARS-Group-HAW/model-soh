#!/usr/bin/env python3
"""Add DEVS emergency link r28 (Raven Rd & University Dr -> Bronson Ave & Raven Rd) to the campus graph."""
from __future__ import annotations

import json
import math
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SRC = ROOT / "resources" / "campus_drive_graph.geojson"
DST = ROOT / "resources" / "campus_drive_graph_scenario_07.geojson"

# lon, lat (OGC CRS84)
RAVEN_UNIV = (-75.6940, 45.3840)
BRONSON_RAVEN = (-75.6903, 45.3851)
R28_OSMID = "carleton_scenario07_r28"


def dist_m(lon1: float, lat1: float, lon2: float, lat2: float) -> float:
    return math.hypot((lat2 - lat1) * 111_000, (lon2 - lon1) * 85_000)


def line_length_m(coords: list[list[float]]) -> float:
    total = 0.0
    for (lon1, lat1), (lon2, lat2) in zip(coords, coords[1:]):
        total += dist_m(lon1, lat1, lon2, lat2)
    return total


def nearest_point(
    lon: float, lat: float, features: list[dict], max_m: float = 100.0
) -> tuple[float, float] | None:
    best: tuple[float, float] | None = None
    best_d = max_m
    for feat in features:
        if feat.get("geometry", {}).get("type") != "LineString":
            continue
        for plon, plat in feat["geometry"]["coordinates"]:
            d = dist_m(lon, lat, plon, plat)
            if d < best_d:
                best_d = d
                best = (plon, plat)
    return best


def main() -> None:
    if not SRC.is_file():
        raise FileNotFoundError(SRC)

    data = json.loads(SRC.read_text(encoding="utf-8"))
    features: list[dict] = data["features"]

    start = nearest_point(*RAVEN_UNIV, features) or RAVEN_UNIV
    end = nearest_point(*BRONSON_RAVEN, features) or BRONSON_RAVEN
    coords = [list(start), list(RAVEN_UNIV), list(BRONSON_RAVEN), list(end)]
    # Drop duplicate consecutive points.
    deduped = [coords[0]]
    for pt in coords[1:]:
        if pt != deduped[-1]:
            deduped.append(pt)

    length = line_length_m(deduped)
    features.append(
        {
            "type": "Feature",
            "properties": {
                "u": 900070001,
                "v": 900070002,
                "key": 0,
                "osmid": R28_OSMID,
                "highway": "residential",
                "lanes": "2",
                "maxspeed": "40",
                "name": "Raven Rd emergency link (scenario 07)",
                "oneway": False,
                "reversed": "False",
                "length": length,
                "ref": "",
                "tunnel": "",
                "bridge": "",
                "width": "",
                "access": "",
                "junction": "",
                "scenario07_emergency": True,
            },
            "geometry": {"type": "LineString", "coordinates": deduped},
        }
    )

    DST.write_text(json.dumps(data), encoding="utf-8")
    print(f"Wrote {DST.name} ({len(features)} edges, r28 length ~{length:.1f} m)")


if __name__ == "__main__":
    main()
