#!/usr/bin/env python3
"""
Extract bus stations (stops) and bus route lines from OpenStreetMap (Overpass)
limited to an AOI polygon (GeoJSON). Supports filtering by route ref and sense.

Usage (Windows PowerShell one-liner):
  python .\aoi_osm_bus_extract.py --aoi .\aoi.geojson --outdir .\out --route-ref L300 --sense aller

Outputs in --outdir:
  routes.geojson          (bus route polylines intersecting AOI, grouped by relation id)
  stations.geojson        (bus stops/platforms/stations inside AOI)
  stations_route.geojson  (bus stops that are MEMBERS of the matched route relations)

Dependencies: requests, geopandas, shapely, fiona
  pip install requests geopandas shapely fiona
"""
import argparse
import json
from pathlib import Path
import requests
import geopandas as gpd
from shapely.geometry import Point, LineString, MultiLineString
from shapely.ops import unary_union, linemerge
from typing import Dict, Any, List, Tuple

def load_aoi(aoi_path: Path):
    gdf = gpd.read_file(aoi_path)
    if gdf.empty:
        raise SystemExit("AOI file is empty")
    aoi = gdf.to_crs(4326)  # WGS84
    geom = unary_union(aoi.geometry)
    return aoi, geom

def polygon_to_overpass_poly(geom) -> str:
    def ring_to_str(coords):
        # coords are (lon, lat); Overpass expects "lat lon"
        return " ".join(f"{lat} {lon}" for lon, lat in coords)
    polys = []
    if geom.geom_type == "Polygon":
        polys.append(geom.exterior.coords)
    elif geom.geom_type == "MultiPolygon":
        for p in geom.geoms:
            polys.append(p.exterior.coords)
    else:
        raise ValueError(f"Unsupported AOI geometry type: {geom.geom_type}")
    polys = sorted(polys, key=lambda c: len(c), reverse=True)
    return ring_to_str(polys[0])
def overpass_query(url: str, query: str) -> Dict[str, Any]:
    resp = requests.post(url, data={"data": query}, timeout=180)
    resp.raise_for_status()
    return resp.json()

def extract_generic_stations(overpass_url: str, poly: str) -> gpd.GeoDataFrame:
    # highway=bus_stop, public_transport=platform (bus=yes), amenity=bus_station
    q = f"""
    [out:json][timeout:120];
    (
      node["highway"="bus_stop"](poly:"{poly}");
      node["public_transport"="platform"]["bus"="yes"](poly:"{poly}");
      node["amenity"="bus_station"](poly:"{poly}");
    );
    out body;"""
    data = overpass_query(overpass_url, q)
    rows = []
    for el in data.get("elements", []):
        if el["type"] != "node":
            continue
        rows.append({
            "osm_id": el["id"],
            "name": el.get("tags", {}).get("name"),
            "ref": el.get("tags", {}).get("ref"),
            "tags": el.get("tags", {}),
            "geometry": Point(el["lon"], el["lat"])
        })
    return gpd.GeoDataFrame(rows, geometry="geometry", crs="EPSG:4326")

def extract_routes_and_member_stops(overpass_url: str, poly: str, route_ref: str=None, sense: str=None):
    ref_filter = f'["ref"="{route_ref}"]' if route_ref else ""
    sense_filter = f'["sense"="{sense}"]' if sense else ""
    q = f"""
    [out:json][timeout:180];
    rel["type"="route"]["route"="bus"]{ref_filter}{sense_filter}(poly:"{poly}");
    (._;>;);
    out body geom;"""
    data = overpass_query(overpass_url, q)

    way_geom: Dict[int, LineString] = {}
    rel_meta: Dict[int, Dict[str, Any]] = {}
    node_rows: List[Dict[str, Any]] = []

    for el in data.get("elements", []):
        if el["type"] == "way" and "geometry" in el:
            coords = [(n["lon"], n["lat"]) for n in el["geometry"]]
            if len(coords) >= 2:
                way_geom[el["id"]] = LineString(coords)
        elif el["type"] == "relation":
            tags = el.get("tags", {})
            rel_meta[el["id"]] = {
                "route_id": el["id"],
                "ref": tags.get("ref"),
                "name": tags.get("name"),
                "operator": tags.get("operator"),
                "from": tags.get("from"),
                "to": tags.get("to"),
                "sense": tags.get("sense"),
                "tags": tags
            }
        elif el["type"] == "node":
            tags = el.get("tags", {})
            if tags.get("highway") == "bus_stop" or (tags.get("public_transport") == "platform" and tags.get("bus") == "yes") or tags.get("amenity") == "bus_station":
                node_rows.append({
                    "osm_id": el["id"],
                    "name": tags.get("name"),
                    "ref": tags.get("ref"),
                    "tags": tags,
                    "geometry": Point(el["lon"], el["lat"])
                })

    # Map relation -> member ways and member stops
    features = []
    for el in data.get("elements", []):
        if el["type"] != "relation":
            continue
        rid = el["id"]
        lines: List[LineString] = []
        member_node_ids = set()
        for m in el.get("members", []):
            if m.get("type") == "way":
                geom = way_geom.get(m["ref"])
                if geom is not None:
                    lines.append(geom)
            elif m.get("type") == "node":
                member_node_ids.add(m["ref"])
        if not lines:
            continue
        merged = linemerge(MultiLineString(lines)) if len(lines) > 1 else lines[0]
        geom = merged if isinstance(merged, (LineString, MultiLineString)) else MultiLineString(lines)
        feat = rel_meta.get(rid, {"route_id": rid})
        feat["geometry"] = geom
        feat["relation_id"] = rid
        features.append(feat)

    routes_gdf = gpd.GeoDataFrame(features, geometry="geometry", crs="EPSG:4326")
    # Member stops
    member_nodes = [n for n in node_rows]  # all nodes in data; we'll clip to AOI later
    stops_gdf = gpd.GeoDataFrame(member_nodes, geometry="geometry", crs="EPSG:4326")

    return routes_gdf, stops_gdf

def clip_to_aoi(gdf: gpd.GeoDataFrame, aoi_geom):
    if gdf.empty:
        return gdf
    try:
        return gpd.overlay(gdf, gpd.GeoDataFrame(geometry=[aoi_geom], crs=4326), how="intersection")
    except Exception:
        return gdf[gdf.geometry.intersects(aoi_geom)]

def main():
    import sys
    import geopandas as gpd
    from shapely.ops import unary_union
    ap = argparse.ArgumentParser()
    ap.add_argument("--aoi", required=True, help="AOI polygon GeoJSON (Polygon/MultiPolygon)")
    ap.add_argument("--outdir", required=True, help="Output directory")
    ap.add_argument("--overpass", default="https://overpass-api.de/api/interpreter", help="Overpass API endpoint")
    ap.add_argument("--route-ref", default=None, help="Optional route 'ref' filter (e.g., L300)")
    ap.add_argument("--sense", default=None, help="Optional sense filter (e.g., aller or retour)")
    args = ap.parse_args()

    outdir = Path(args.outdir); outdir.mkdir(parents=True, exist_ok=True)

    aoi, aoi_geom = load_aoi(Path(args.aoi))
    poly = polygon_to_overpass_poly(aoi_geom)

    print("[1/3] Downloading generic bus stops in AOI…")
    stations = extract_generic_stations(args.overpass, poly)
    stations = clip_to_aoi(stations, aoi_geom)
    (outdir / "stations.geojson").write_text(stations.to_json())

    print("[2/3] Downloading route relations (route=bus)…")
    routes, route_member_stops = extract_routes_and_member_stops(args.overpass, poly, args.route_ref, args.sense)
    routes = clip_to_aoi(routes, aoi_geom)
    (outdir / "routes.geojson").write_text(routes.to_json())

    print("[3/3] Writing stations_route.geojson (member stops) clipped to AOI…")
    route_member_stops = clip_to_aoi(route_member_stops, aoi_geom)
    (outdir / "stations_route.geojson").write_text(route_member_stops.to_json())

    print(f"Done. Wrote:\n - {outdir/'routes.geojson'}\n - {outdir/'stations.geojson'}\n - {outdir/'stations_route.geojson'}")

if __name__ == "__main__":
    main()