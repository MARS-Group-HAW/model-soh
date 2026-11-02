"""
Enriches the raw neighbour-country GeoJSON (export_Neighbor_Countries.py) so it has the same schema as the German
base map: elevation (per node and per edge coordinate), incline (max segment grade in %, only if OSM gave none),
capacity_fe (length * lanes) and all property keys of the German file.

Elevation comes from the public SRTM 1 arc-second tiles of the AWS Terrain Tiles dataset (no API key, no quota),
sampled locally with bilinear interpolation. Tiles are cached in --tile-dir. Voids (-32768) are filled by None.
SRTM covers up to 60 N, so Denmark/Copenhagen (55.7 N) is fine.

Usage:
    python enrich_Neighbor_Map.py --input neighbors_raw.geojson --output neighbors_elevation.geojson
"""

import argparse
import gzip
import json
import math
import os

import numpy as np
import requests

TILE_URL = "https://elevation-tiles-prod.s3.amazonaws.com/skadi/{ns}{lat:02d}/{ns}{lat:02d}{ew}{lon:03d}.hgt.gz"
EARTH_RADIUS = 6371000
tiles = {}


def tile_name(lat, lon):
    return ("N" if lat >= 0 else "S", abs(math.floor(lat)), "E" if lon >= 0 else "W", abs(math.floor(lon)))


def load_tile(key, tile_dir):
    if key in tiles:
        return tiles[key]
    ns, lat, ew, lon = key
    path = os.path.join(tile_dir, f"{ns}{lat:02d}{ew}{lon:03d}.hgt.gz")
    if not os.path.exists(path):
        r = requests.get(TILE_URL.format(ns=ns, lat=lat, ew=ew, lon=lon), timeout=120)
        r.raise_for_status()
        with open(path, "wb") as f:
            f.write(r.content)
    with gzip.open(path, "rb") as f:
        raw = np.frombuffer(f.read(), dtype=">i2")
    size = int(math.sqrt(raw.size))
    tiles[key] = raw.reshape((size, size)).astype(np.float32)
    return tiles[key]


def elevation(lon, lat, tile_dir):
    ns, tlat, ew, tlon = tile_name(lat, lon)
    grid = load_tile((ns, tlat, ew, tlon), tile_dir)
    n = grid.shape[0] - 1
    row = (1 - (lat - math.floor(lat))) * n
    col = (lon - math.floor(lon)) * n
    r0, c0 = min(int(row), n - 1), min(int(col), n - 1)
    fr, fc = row - r0, col - c0
    vals = grid[r0:r0 + 2, c0:c0 + 2]
    if (vals == -32768).any():
        return None
    top = vals[0, 0] * (1 - fc) + vals[0, 1] * fc
    bottom = vals[1, 0] * (1 - fc) + vals[1, 1] * fc
    return float(round(top * (1 - fr) + bottom * fr, 2))


def haversine(a, b):
    lon1, lat1, lon2, lat2 = map(math.radians, (a[0], a[1], b[0], b[1]))
    h = math.sin((lat2 - lat1) / 2) ** 2 + math.cos(lat1) * math.cos(lat2) * math.sin((lon2 - lon1) / 2) ** 2
    return 2 * EARTH_RADIUS * math.asin(math.sqrt(h))


MIN_GRADE_WINDOW_M = 100
MAX_GRADE_PERCENT = 12  # steeper values on motorways/primaries are SRTM artefacts (bridges, cuttings)


def max_incline(coords, elevs):
    """Max grade in %, measured over windows of at least MIN_GRADE_WINDOW_M to damp 30 m SRTM noise."""
    best = 0.0
    for i in range(len(coords) - 1):
        if elevs[i] is None:
            continue
        d = 0.0
        for j in range(i + 1, len(coords)):
            d += haversine(coords[j - 1], coords[j])
            if d >= MIN_GRADE_WINDOW_M or j == len(coords) - 1:
                if elevs[j] is not None and d >= 1.0:
                    best = max(best, abs(elevs[j] - elevs[i]) / d * 100)
                break
    return min(best, MAX_GRADE_PERCENT)


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--input", required=True)
    parser.add_argument("--output", required=True)
    parser.add_argument("--tile-dir", default="srtm_tiles")
    args = parser.parse_args()
    os.makedirs(args.tile_dir, exist_ok=True)

    with open(args.input, "r", encoding="utf-8") as f:
        data = json.load(f)

    missing = 0
    for ft in data["features"]:
        p, g = ft["properties"], ft["geometry"]
        if g["type"] == "Point":
            e = elevation(*g["coordinates"], args.tile_dir)
            p["elevation"] = str(e) if e is not None else None
            p["maxspeed"] = 50
            missing += e is None
        else:
            coords = g["coordinates"]
            elevs = [elevation(c[0], c[1], args.tile_dir) for c in coords]
            missing += sum(e is None for e in elevs)
            # Incline is a numeric string in the German file; keep OSM's value where present
            p["elevation"] = elevs
            try:
                p["incline"] = str(abs(float(str(p["incline"]).rstrip("%"))))
            except (KeyError, ValueError):  # missing or non-numeric OSM values such as "up"
                p["incline"] = str(max_incline(coords, elevs))
            for tag in ("maxheight", "maxweight", "maxwidth", "surface"):
                p.pop(tag, None)  # not part of the German schema; free-text values like "default" are unsafe
            p["capacity_fe"] = float(round(p["length"] * int(p.get("lanes", 1))))

    with open(args.output, "w", encoding="utf-8") as f:
        json.dump(data, f, ensure_ascii=False)
    print(f"Done, {missing} coordinates without elevation (SRTM voids). Wrote {args.output}")


if __name__ == "__main__":
    main()
