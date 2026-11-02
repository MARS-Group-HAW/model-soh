"""
Exports the road network of Germany's neighbouring countries as a *separate* GeoJSON that is loaded in addition
to the German base map (MARS merges several graph inputs and joins nodes that share the exact same coordinate).

Why a separate file: the edge IDs of the German GeoJSON are index based (start_edge_ids.json, road_closures_by_ID.csv,
route_cache.db), so the German file stays untouched and the neighbour network is simply added on top.

Coverage (motorway|trunk|primary unless noted):
1. Border zone: everything within BORDER_ZONE_KM of the German border, per neighbour country.
2. Capital / city discs: DISC_KM around the capitals and a few big cities (Vienna, Prague, Brussels, ...).
3. Corridors: motorway|trunk only, CORRIDOR_KM wide, from the German border to each capital.

How the connection to the German map works:
* The download also contains a STRIP_KM strip on the German side of the border.
* Every node whose coordinate (rounded to 7 digits) exists in the German GeoJSON is flagged `protected`. Protected
  nodes are kept as endpoints while simplifying, so the neighbour edges start exactly at German nodes.
* Edges that lie completely inside Germany are dropped again (they are already part of the German map).
* Only weakly connected components that contain a protected node are kept, so every remaining node is reachable.

Every node and edge gets a `country` (ISO 3166-1 alpha-2) attribute; edges also get `border_crossing`.

Usage:
    python export_Neighbor_Countries.py --german-geojson <german.geojson> --output neighbors_raw.geojson
    (optional: --countries AT CZ ...  to (re-)export only some countries; the OSMnx cache makes reruns cheap)

Next steps: enrich_Neighbor_Map.py (elevation, incline, capacity, maxspeed defaults).
"""

import argparse
import json
import os
import sys
import time

import geopandas as gpd
import networkx as nx
import osmnx as ox
import pandas as pd
import requests
from shapely.geometry import LineString, Point
from shapely.ops import nearest_points, unary_union
from shapely.prepared import prep
from shapely.strtree import STRtree
from shapely.validation import make_valid

from osm_pbf_extract import extract

BORDER_ZONE_KM = 50
DISC_KM = 20
CORRIDOR_KM = 25
STRIP_KM = 4
BORDER_BAND_KM = 5
SNAP_M = 1500
METRIC_CRS = "EPSG:3035"

COUNTRIES = {
    "AT": "Austria",
    "BE": "Belgium",
    "CH": "Switzerland",
    "CZ": "Czechia",
    "DK": "Denmark",
    "FR": "France",
    "LU": "Luxembourg",
    "NL": "Netherlands",
    "PL": "Poland",
}

# (name, country, lat, lon) - capitals and big logistics hubs that should be reachable
CITIES = [
    ("Vienna", "AT", 48.2082, 16.3738),
    ("Prague", "CZ", 50.0755, 14.4378),
    ("Brussels", "BE", 50.8503, 4.3517),
    ("Antwerp", "BE", 51.2194, 4.4025),
    ("Amsterdam", "NL", 52.3676, 4.9041),
    ("Rotterdam", "NL", 51.9244, 4.4777),
    ("Copenhagen", "DK", 55.6761, 12.5683),
    ("Warsaw", "PL", 52.2297, 21.0122),
    ("Poznan", "PL", 52.4064, 16.9252),
    ("Wroclaw", "PL", 51.1079, 17.0385),
    ("Paris", "FR", 48.8566, 2.3522),
    ("Strasbourg", "FR", 48.5734, 7.7521),
    ("Bern", "CH", 46.9480, 7.4474),
    ("Zurich", "CH", 47.3769, 8.5417),
    ("Basel", "CH", 47.5596, 7.5886),
    ("Luxembourg", "LU", 49.6116, 6.1319),
]

# Waypoints for corridors that must not be a straight line (water in between): Copenhagen lies on Zealand,
# the road from Germany runs over Jutland, Funen and the Great Belt bridge
CITY_VIA = {
    "Copenhagen": [(55.49, 9.47), (55.40, 10.39), (55.33, 11.14), (55.46, 11.80)],
}

# Fallback speed limits (km/h) when OSM has no usable maxspeed, by country and highway type
DEFAULT_SPEEDS = {
    "AT": (130, 100, 100), "BE": (120, 90, 70), "CH": (120, 100, 80), "CZ": (130, 90, 90),
    "DK": (130, 90, 80), "FR": (130, 110, 80), "LU": (130, 90, 90), "NL": (100, 80, 80),
    "PL": (140, 100, 90), "DE": (130, 100, 100),
}

EXTRA_TAGS = ["maxheight", "maxlength", "surface", "maxweight", "maxwidth", "incline", "maxspeed", "lanes"]
ox.settings.useful_tags_way = sorted(set(ox.settings.useful_tags_way) | set(EXTRA_TAGS))
ox.settings.use_cache = True
ox.settings.requests_timeout = 600
ox.settings.max_query_area_size = 10 ** 9  # motorway|trunk queries are light enough; primary roads only in city discs


def log(msg):
    print(msg, flush=True)


def load_german_nodes(path):
    """Returns the set of (x, y) node coordinates of the German GeoJSON, rounded to 7 digits."""
    log(f"Reading German nodes from {path} ...")
    with open(path, "r", encoding="utf-8") as f:
        data = json.load(f)
    nodes = {
        (round(c[0], 7), round(c[1], 7))
        for c in (ft["geometry"]["coordinates"] for ft in data["features"] if ft["geometry"]["type"] == "Point")
    }
    log(f"  {len(nodes)} German nodes")
    return nodes


def country_polygons():
    """Land polygons (metric CRS) for Germany and all neighbours, clipped to continental Europe."""
    names = {"DE": "Germany", **COUNTRIES}
    polys = {}
    for iso, name in names.items():
        gdf = ox.geocode_to_gdf(name)
        gdf = gdf.clip([-10.0, 41.0, 25.0, 60.0]).to_crs(METRIC_CRS)
        polys[iso] = make_valid(unary_union(gdf.geometry))
    return polys


def build_jobs(polys):
    """Per country: list of (polygon in WGS84, highway filter) download jobs."""
    de = polys["DE"]
    border_ring = de.buffer(BORDER_ZONE_KM * 1000).difference(de)
    jobs = {}
    for iso in COUNTRIES:
        country = polys[iso]
        main_zone = border_ring.intersection(country)
        strip = de.intersection(main_zone.buffer(STRIP_KM * 1000))
        parts = [(unary_union([main_zone, strip]), "motorway|trunk"),
                 # primary roads right at the border: many crossings are B-roads/national roads on the German side
                 (unary_union([main_zone.intersection(de.buffer(BORDER_BAND_KM * 1000)), strip]),
                  "motorway|trunk|primary")]
        for name, city_iso, lat, lon in CITIES:
            if city_iso != iso:
                continue
            city = gpd.GeoSeries([Point(lon, lat)], crs="EPSG:4326").to_crs(METRIC_CRS).iloc[0]
            start = nearest_points(de.boundary, city)[0]
            via = gpd.GeoSeries([Point(vlon, vlat) for vlat, vlon in CITY_VIA.get(name, [])],
                                crs="EPSG:4326").to_crs(METRIC_CRS)
            corridor = LineString([start, *via, city]).buffer(CORRIDOR_KM * 1000).intersection(country)
            disc = city.buffer(DISC_KM * 1000).intersection(country)
            german_end = de.intersection(start.buffer(STRIP_KM * 1000))
            parts.append((unary_union([corridor.intersection(country), german_end]), "motorway|trunk"))
            parts.append((disc, "motorway|trunk|primary"))
        jobs[iso] = [(gpd.GeoSeries([p], crs=METRIC_CRS).to_crs("EPSG:4326").iloc[0], f) for p, f in parts]
    return jobs


GEOFABRIK = "https://download.geofabrik.de/europe/"
PBF_SOURCES = {
    "AT": ["austria"], "BE": ["belgium"], "CH": ["switzerland"], "CZ": ["czech-republic"], "DK": ["denmark"],
    "LU": ["luxembourg"], "NL": ["netherlands"], "PL": ["poland"],
    # France is huge; only the regions towards Germany, Belgium, Luxembourg and Paris are needed
    "FR": ["france/alsace", "france/lorraine", "france/champagne-ardenne", "france/nord-pas-de-calais",
           "france/picardie", "france/ile-de-france"],
}


def pbf_files(iso, pbf_dir):
    """Downloads the Geofabrik extracts of a country (cached in pbf_dir) and returns their paths."""
    paths = []
    for src in PBF_SOURCES[iso]:
        path = os.path.join(pbf_dir, src.split("/")[-1] + ".osm.pbf")
        if not os.path.exists(path):
            log(f"[{iso}] downloading {src}")
            with requests.get(f"{GEOFABRIK}{src}-latest.osm.pbf", stream=True, timeout=(30, 120)) as r:
                r.raise_for_status()
                with open(path + ".part", "wb") as f:
                    for chunk in r.iter_content(1 << 20):
                        f.write(chunk)
            os.replace(path + ".part", path)
        paths.append(path)
    return paths


def load_country_pbf(iso, jobs, pbf_dir):
    """Builds the graph of a country from local OSM extracts, restricted to the download jobs (polygon + filter)."""
    boxes = [p.bounds for p, _ in jobs if not p.is_empty]
    bounds = (min(b[0] for b in boxes), min(b[1] for b in boxes), max(b[2] for b in boxes), max(b[3] for b in boxes))
    xml_path = os.path.join(pbf_dir, f"{iso}.osm")
    nodes, ways = extract(pbf_files(iso, pbf_dir), xml_path, bounds)
    log(f"[{iso}] {ways} ways / {nodes} nodes read from extracts")
    G = ox.graph_from_xml(xml_path, bidirectional=False, simplify=False, retain_all=True)
    keep = set()
    for poly, highway_filter in jobs:
        if poly.is_empty:
            continue
        allowed = set(highway_filter.split("|")) | {f"{h}_link" for h in highway_filter.split("|")}
        prepared = prep(poly)
        inside = {n: prepared.contains(Point(d["x"], d["y"])) for n, d in G.nodes(data=True)}
        for u, v, k, d in G.edges(keys=True, data=True):
            hw = d.get("highway")
            if (set(hw) if isinstance(hw, list) else {hw}) & allowed and (inside[u] or inside[v]):
                keep.add((u, v, k))
    H = G.edge_subgraph(keep).copy()
    return H


def download_country(iso, jobs):
    graphs = []
    for i, (poly, highway_filter) in enumerate(jobs):
        if poly.is_empty:
            continue
        log(f"[{iso}] download part {i + 1}/{len(jobs)} ({highway_filter})")
        for attempt in range(8):  # the public Overpass server is often at capacity
            try:
                graphs.append(ox.graph_from_polygon(
                    poly, network_type="drive", custom_filter=f'["highway"~"{highway_filter}"]',
                    simplify=False, retain_all=True, truncate_by_edge=True))
                break
            except Exception as e:
                wait = 30 * 2 ** attempt
                log(f"[{iso}] Overpass error ({type(e).__name__}), retry in {wait}s")
                time.sleep(wait)
        else:
            raise RuntimeError(f"[{iso}] download failed repeatedly")
    return nx.compose_all(graphs)


def build_german_index(german_nodes):
    """STRtree over the German node coordinates in a metric CRS (for snapping)."""
    coords = list(german_nodes)
    pts = gpd.GeoSeries([Point(c) for c in coords], crs="EPSG:4326").to_crs(METRIC_CRS)
    return STRtree(list(pts.values)), coords


def snap_dead_ends(G, german_index, de):
    """
    Geofabrik extracts are cut at the border, so roads often end a few metres before the German node they continue
    at. Dead-end nodes outside Germany that lie within SNAP_M of a German node are joined to it by a connector edge
    (both directions) and the German node is added as `protected` node.
    """
    tree, coords = german_index
    # dead ends, plus the closest node of every component that has no German connection yet
    cand = set(n for n in G.nodes if not G.nodes[n].get("protected")
               and len(set(G.predecessors(n)) | set(G.successors(n))) == 1)
    for comp in nx.weakly_connected_components(G):
        if any(G.nodes[n].get("protected") for n in comp):
            continue
        comp = list(comp)
        cp = gpd.GeoSeries([Point(G.nodes[n]["x"], G.nodes[n]["y"]) for n in comp], crs="EPSG:4326").to_crs(METRIC_CRS)
        pair, d = tree.query_nearest(list(cp.values), max_distance=SNAP_M, return_distance=True, all_matches=False)
        if len(d):
            cand.add(comp[int(pair[0][int(d.argmin())])])
    dead = list(cand)
    if not dead:
        return 0
    pts = gpd.GeoSeries([Point(G.nodes[n]["x"], G.nodes[n]["y"]) for n in dead], crs="EPSG:4326").to_crs(METRIC_CRS)
    idx, dist = tree.query_nearest(list(pts.values), max_distance=SNAP_M, return_distance=True, all_matches=False)
    count = 0
    for q, t, d in zip(idx[0], idx[1], dist):
        n = dead[q]
        if de.contains(pts.iloc[q]):
            continue
        x, y = coords[t]
        g = f"de_{x}_{y}"
        if g not in G:
            G.add_node(g, x=x, y=y, protected=True)
        length = max(float(d), 1.0)
        for u, v, rev in ((n, g, False), (g, n, True)):
            G.add_edge(u, v, osmid=[-1], highway="primary", oneway=False, reversed=rev, length=length)
        count += 1
    return count


def process_country(iso, G, german_nodes, german_index, polys):
    """Flags German nodes, simplifies, drops purely German edges and unreachable components."""
    de = polys["DE"]
    protected = 0
    for n, data in G.nodes(data=True):
        if (round(data["x"], 7), round(data["y"], 7)) in german_nodes:
            data["protected"] = True
            protected += 1
    log(f"[{iso}] {len(G.nodes)} raw nodes, {protected} coincide with German nodes")

    G = ox.simplification.simplify_graph(G, node_attrs_include=["protected"], remove_rings=False)
    nx.set_node_attributes(G, ox.stats.count_streets_per_node(G), "street_count")
    nodes, edges = ox.graph_to_gdfs(G)

    edges_m = edges.to_crs(METRIC_CRS)
    inside_de = edges_m.geometry.within(de.buffer(25))
    G.remove_edges_from([(u, v, k) for (u, v, k), drop in zip(edges.index, inside_de) if drop])
    G.remove_nodes_from(list(nx.isolates(G)))

    snapped = snap_dead_ends(G, german_index, de)
    log(f"[{iso}] {snapped} dead ends snapped to German nodes (<= {SNAP_M} m)")

    keep = set()
    crossings = 0
    for comp in nx.weakly_connected_components(G):
        prot = [n for n in comp if G.nodes[n].get("protected")]
        if prot:
            keep |= comp
            crossings += len(prot)
    dropped = len(G.nodes) - len(keep)
    G = G.subgraph(keep).copy()
    nx.set_node_attributes(G, ox.stats.count_streets_per_node(G), "street_count")
    log(f"[{iso}] {len(G.nodes)} nodes kept, {dropped} unreachable nodes dropped, "
        f"{crossings} connections to the German map")
    return G


def first(value):
    return value[0] if isinstance(value, list) and value else value


def parse_speed(value, fallback):
    for v in value if isinstance(value, list) else [value]:
        try:
            return int(str(v).split()[0])
        except (ValueError, IndexError):
            continue
    return fallback


def to_features(G, iso, polys):
    nodes, edges = ox.graph_to_gdfs(G)
    nodes_m = nodes.to_crs(METRIC_CRS)
    centroids = gpd.GeoDataFrame(geometry=edges.to_crs(METRIC_CRS).interpolate(0.5, normalized=True), crs=METRIC_CRS)

    country_gdf = gpd.GeoDataFrame({"country": list(polys)}, geometry=list(polys.values()), crs=METRIC_CRS)

    def assign(points):
        joined = gpd.sjoin_nearest(points[["geometry"]], country_gdf, how="left")
        return joined[~joined.index.duplicated()]["country"]

    node_country = assign(nodes_m)
    edge_country = assign(centroids)
    ends = pd.DataFrame(edges.index.tolist(), columns=["u", "v", "k"])
    border_crossing = (ends["u"].map(node_country).values != ends["v"].map(node_country).values)

    features = []
    for (n, row), c in zip(nodes.iterrows(), node_country.loc[nodes.index]):
        features.append({
            "type": "Feature",
            "properties": {"type": "node", "x": row["x"], "y": row["y"], "street_count": float(row["street_count"]),
                           "country": c},
            "geometry": {"type": "Point", "coordinates": [row["x"], row["y"]]},
        })
    speeds = DEFAULT_SPEEDS[iso]
    for i, ((u, v, k), row) in enumerate(edges.iterrows()):
        highway = first(row.get("highway"))
        fallback = speeds[0] if highway == "motorway" else speeds[1] if highway == "trunk" else speeds[2]
        lanes = parse_speed(row.get("lanes"), None) if "lanes" in row and pd.notna(first(row.get("lanes"))) else None
        props = {
            "type": "edge",
            "osmid": row["osmid"] if isinstance(row["osmid"], list) else [row["osmid"]],
            "oneway": bool(first(row["oneway"])),
            "reversed": bool(first(row["reversed"])),
            "length": float(row["length"]),
            "maxspeed": parse_speed(row.get("maxspeed"), fallback),
            "highway": highway,
            "country": edge_country.iloc[i],
            "border_crossing": bool(border_crossing[i]),
            "overtaking": "yes",
        }
        for tag in ("maxheight", "maxlength", "maxweight", "maxwidth", "surface"):
            if tag in row and pd.notna(first(row[tag])):
                props[tag] = str(first(row[tag]))
        if "incline" in row and pd.notna(first(row["incline"])):
            props["incline"] = str(first(row["incline"]))
        if lanes:
            props["lanes"] = lanes
        features.append({
            "type": "Feature",
            "properties": props,
            "geometry": {"type": "LineString", "coordinates": [list(c) for c in row.geometry.coords]},
        })
    return features


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--german-geojson", required=True, help="German base map (defines the connection nodes)")
    parser.add_argument("--output", default="neighbors_raw.geojson")
    parser.add_argument("--pbf-dir", help="use Geofabrik extracts (downloaded to this directory) instead of Overpass")
    parser.add_argument("--overpass-url", help="alternative Overpass endpoint (default: overpass-api.de)")
    parser.add_argument("--countries", nargs="*", default=list(COUNTRIES), choices=list(COUNTRIES))
    args = parser.parse_args()

    if args.overpass_url:
        ox.settings.overpass_url = args.overpass_url
    german_nodes = load_german_nodes(args.german_geojson)
    german_index = build_german_index(german_nodes)
    polys = country_polygons()
    jobs = build_jobs(polys)

    features = []
    seen_nodes = set()
    for iso in args.countries:
        raw = (load_country_pbf(iso, jobs[iso], args.pbf_dir) if args.pbf_dir
               else download_country(iso, jobs[iso]))
        G = process_country(iso, raw, german_nodes, german_index, polys)
        if len(G) == 0:
            log(f"[{iso}] WARNING: no network connected to Germany, country skipped")
            continue
        for ft in to_features(G, iso, polys):
            if ft["geometry"]["type"] == "Point":
                key = tuple(ft["geometry"]["coordinates"])
                if key in seen_nodes:
                    continue
                seen_nodes.add(key)
            features.append(ft)

    out = {"type": "FeatureCollection", "name": "neighbor_countries",
           "crs": {"type": "name", "properties": {"name": "urn:ogc:def:crs:OGC:1.3:CRS84"}}, "features": features}
    with open(args.output, "w", encoding="utf-8") as f:
        json.dump(out, f, ensure_ascii=False)
    log(f"Wrote {len(features)} features to {args.output}")


if __name__ == "__main__":
    sys.exit(main())
