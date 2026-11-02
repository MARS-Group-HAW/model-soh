"""
Exports fuel stations (amenity=fuel) and rest areas (highway=services|rest_area) of Germany's neighbouring countries
and tags every row with its ISO country code, so stations can be attributed to the country they belong to.

The country is determined by point-in-polygon against the country boundaries (nearest country as fallback for points
on the border). Output has the same columns as the German CSVs plus `country`:
    refuel_stations_neighbors.csv   id,lat,lon,country
    rest_areas_neighbors.csv        id,lat,lon,country

Only stations within BORDER_ZONE_KM of the German border or DISC_KM of the listed cities are kept, matching the
coverage of export_Neighbor_Countries.py. IDs start at ID_OFFSET to avoid clashes with the German files.

Usage:
    python export_Neighbor_Stations.py --pbf-dir DIR   (reuses the Geofabrik extracts of export_Neighbor_Countries.py)
"""

import argparse
import csv

import geopandas as gpd
import osmium
import osmnx as ox
from shapely import prepared
from shapely.geometry import Point
from shapely.ops import unary_union

from export_Neighbor_Countries import (BORDER_ZONE_KM, CITIES, COUNTRIES, DISC_KM, METRIC_CRS, country_polygons,
                                       pbf_files)

ID_OFFSET = 100000


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--pbf-dir", required=True)
    args = parser.parse_args()

    polys = country_polygons()
    de = polys["DE"]
    area = unary_union([de.buffer(BORDER_ZONE_KM * 1000)] + [
        gpd.GeoSeries([Point(lon, lat)], crs="EPSG:4326").to_crs(METRIC_CRS).iloc[0].buffer(DISC_KM * 1000)
        for _, _, lat, lon in CITIES])
    area = area.difference(de)

    fuel, rest = [], []
    for iso in COUNTRIES:
        zone = gpd.GeoSeries([area.intersection(polys[iso])], crs=METRIC_CRS).to_crs("EPSG:4326").iloc[0]
        if zone.is_empty:
            continue
        zone = prepared.prep(zone)
        counts = [0, 0]
        for path in pbf_files(iso, args.pbf_dir):
            fp = osmium.FileProcessor(path, osmium.osm.NODE | osmium.osm.WAY).with_locations()
            for o in fp:
                tags = o.tags
                is_fuel = tags.get("amenity") == "fuel"
                is_rest = tags.get("highway") in ("services", "rest_area")
                if not (is_fuel or is_rest):
                    continue
                loc = o.location if o.is_node() else next((n.location for n in o.nodes if n.location.valid()), None)
                if loc is None or not loc.valid() or not zone.contains(Point(loc.lon, loc.lat)):
                    continue
                (fuel if is_fuel else rest).append((loc.lat, loc.lon, iso))
                counts[0 if is_fuel else 1] += 1
        print(f"[{iso}] {counts[0]} fuel stations, {counts[1]} rest areas", flush=True)

    for name, rows in (("refuel_stations_neighbors.csv", fuel), ("rest_areas_neighbors.csv", rest)):
        with open(name, "w", newline="", encoding="utf-8") as f:
            w = csv.writer(f)
            w.writerow(["id", "lat", "lon", "country"])
            for i, (lat, lon, iso) in enumerate(rows, start=ID_OFFSET + 1):
                w.writerow([i, round(lat, 7), round(lon, 7), iso])
        print(f"Wrote {len(rows)} rows to {name}")


if __name__ == "__main__":
    main()
