from __future__ import annotations

import argparse
from pathlib import Path
from typing import Iterable, Optional

import geopandas as gpd
import osmnx as ox
import pandas as pd
from shapely.geometry import Point, Polygon, MultiPolygon
from shapely import wkt

# ------------------------ Defaults ------------------------------------------
RES_DIR = Path("resources")
RES_DIR.mkdir(parents=True, exist_ok=True)

DEFAULT_PREFIX = "casa_sidi_maarouf"
DEFAULT_AOI_FILE = RES_DIR / f"{DEFAULT_PREFIX}_aoi.geojson"

# reasonable default if we have to buffer a geocoded point (meters)
DEFAULT_BUFFER_M = 2500

# ------------------------ Helpers -------------------------------------------

def _ensure_wgs84(gdf: gpd.GeoDataFrame) -> gpd.GeoDataFrame:
    if gdf.crs is None:
        gdf.set_crs(4326, inplace=True)
    elif gdf.crs.to_epsg() != 4326:
        gdf = gdf.to_crs(4326)
    return gdf


def _to_point(geom) -> Optional[Point]:
    if geom is None or geom.is_empty:
        return None
    if isinstance(geom, Point):
        return geom
    # representative_point is more robust than centroid for narrow polygons
    try:
        return geom.representative_point()
    except Exception:
        return geom.centroid


def _represent_points(gdf: gpd.GeoDataFrame) -> gpd.GeoDataFrame:
    pts = gdf.copy()
    pts["geometry"] = pts["geometry"].apply(_to_point)
    return pts[~pts.geometry.isna() & ~pts.geometry.is_empty]


def _infer_count(row: pd.Series) -> int:
    """
    Choose an initial bike count for rental stations.
    Priority order: bikes, capacity:bicycle, capacity, capacity:vehicles → int
    Fallback: 10 (SOH default StandardAmount).
    """
    for k in ("bikes", "capacity:bicycle", "capacity", "capacity:vehicles"):
        if k in row and pd.notna(row[k]):
            try:
                # strip things like "12+" or "15;20"
                val = str(row[k]).split(";")[0].replace("+", "").strip()
                n = int(float(val))
                if n >= 0:
                    return n
            except Exception:
                pass
    return 10


def _dedupe_on(df: gpd.GeoDataFrame, keys: Iterable[str]) -> gpd.GeoDataFrame:
    for k in keys:
        if k in df.columns:
            return df.drop_duplicates(subset=[k])
    # fall back to geometry + name
    cols = [c for c in ("name",) if c in df.columns]
    return df.drop_duplicates(subset=cols + ["geometry"])


# ------------------------ AOI Resolution ------------------------------------

def load_aoi_from_geojson(path: Path) -> Polygon | MultiPolygon:
    gdf = gpd.read_file(path)
    if gdf.empty:
        raise ValueError(f"AOI file {path} is empty")
    gdf = _ensure_wgs84(gdf)
    geom = gdf.geometry.iloc[0]
    if not isinstance(geom, (Polygon, MultiPolygon)):
        raise ValueError("AOI geometry must be Polygon/MultiPolygon")
    return geom


def load_aoi_from_wkt(wkt_str: str) -> Polygon | MultiPolygon:
    geom = wkt.loads(wkt_str)
    if not isinstance(geom, (Polygon, MultiPolygon)):
        raise ValueError("Provided WKT must be a Polygon/MultiPolygon")
    return geom


def geocode_aoi(place: str, buffer_m: int = DEFAULT_BUFFER_M) -> Polygon | MultiPolygon:
    gdf = ox.geocode_to_gdf(place)
    if gdf.empty:
        raise ValueError(f"Could not geocode place: {place}")
    gdf = _ensure_wgs84(gdf)
    geom = gdf.geometry.iloc[0]
    if isinstance(geom, Point):
        # buffer in a local UTM then back to WGS84
        tmp = gpd.GeoDataFrame(geometry=[geom], crs=4326)
        utm = tmp.estimate_utm_crs()
        geom = tmp.to_crs(utm).buffer(buffer_m).to_crs(4326).iloc[0]
    return geom


# ------------------------ POI Retrieval -------------------------------------

def fetch_pois(aoi_geom) -> gpd.GeoDataFrame:
    """
    Pull a broad set of useful POIs for an urban mobility model.
    You can trim/extend tags as needed.
    """
    tags = {
        # mobility & support
        "amenity": True,              # any amenity
        "shop": True,                 # shops (bike shops, groceries, etc.)
        "leisure": True,
        "tourism": True,
        "public_transport": True,
        "railway": True,              # stations/stops
        "highway": ["bus_stop", "platform"],  # some bus stop nodes live here
        "office": True,
        "healthcare": True,
        "sport": True
    }

    # OSMnx name differences across versions
    try:
        pois = ox.features.features_from_polygon(aoi_geom, tags)
    except AttributeError:
        pois = ox.features_from_polygon(aoi_geom, tags)  # older API

    if isinstance(pois, pd.DataFrame):
        # Ensure GeoDataFrame
        pois = gpd.GeoDataFrame(pois, geometry="geometry", crs="EPSG:4326")
    pois = _ensure_wgs84(pois)

    # keep only useful columns + geometry
    keep = [c for c in [
        "osmid", "name", "amenity", "shop", "leisure", "tourism",
        "public_transport", "railway", "highway", "office", "healthcare",
        "sport", "brand", "operator", "network", "ref", "opening_hours",
        "capacity", "capacity:bicycle", "capacity:vehicles", "bikes"
    ] if c in pois.columns]
    pois = pois[keep + ["geometry"]]
    pois = _dedupe_on(pois, keys=("osmid", "id", "@id"))
    return pois


def extract_bicycle_rental_stations(pois: gpd.GeoDataFrame) -> gpd.GeoDataFrame:
    # Filter amenity=bicycle_rental
    mask = (pois.get("amenity") == "bicycle_rental")
    stations = pois[mask].copy()

    if stations.empty:
        # nothing found; return empty GeoDataFrame with expected schema
        return gpd.GeoDataFrame(columns=["name", "count", "geometry"], geometry="geometry", crs=4326)

    # Convert to points (centroid/representative point for polygons)
    stations_pt = _represent_points(stations)

    # Prepare schema for SOH BicycleRentalLayer: name + count (initial stock)
    stations_pt["name"] = stations_pt.get("name").fillna("").replace("", None)
    stations_pt["name"] = stations_pt["name"].fillna(stations_pt.get("operator")).fillna("Bicycle Rental")

    stations_pt["count"] = stations_pt.apply(_infer_count, axis=1)
    stations_pt = stations_pt[["name", "count", "geometry"]]
    stations_pt = _ensure_wgs84(stations_pt)

    # drop duplicates by location+name to avoid multi-tag duplicates
    stations_pt = _dedupe_on(stations_pt, keys=())
    return stations_pt


# ------------------------ Main ----------------------------------------------

def main():
    parser = argparse.ArgumentParser(description="Generate POIs (and bicycle rental stations) for an AOI.")
    parser.add_argument("--name-prefix", default=DEFAULT_PREFIX, help="Prefix for output files.")
    aoi_group = parser.add_mutually_exclusive_group()
    aoi_group.add_argument("--aoi-geojson", type=str, default=str(DEFAULT_AOI_FILE),
                           help="Path to AOI GeoJSON (Polygon/MultiPolygon).")
    aoi_group.add_argument("--aoi-wkt", type=str, help="WKT polygon for AOI.")
    aoi_group.add_argument("--place", type=str, help="Geocoding string (will buffer if point).")
    parser.add_argument("--buffer-m", type=int, default=DEFAULT_BUFFER_M, help="Buffer meters if geocode returns a point.")
    parser.add_argument("--export-bicycle-parking", action="store_true",
                        help="Also export amenity=bicycle_parking points as a convenience.")
    args = parser.parse_args()

    # Resolve AOI geometry
    if args.aoi_wkt:
        aoi_geom = load_aoi_from_wkt(args.aoi_wkt)
    elif args.place:
        aoi_geom = geocode_aoi(args.place, buffer_m=args.buffer_m)
    else:
        aoi_geom = load_aoi_from_geojson(Path(args.aoi_geojson))

    # Fetch POIs
    ox.settings.log_console = True
    ox.settings.use_cache = True
    pois = fetch_pois(aoi_geom)

    # Export: all POIs (mixed geometry types)
    out_all = RES_DIR / f"{args.name_prefix}_pois.geojson"
    _ensure_wgs84(pois).to_file(out_all, driver="GeoJSON")
    print(f"[OK] POIs -> {out_all}  (n={len(pois)})")

    # Export: bicycle rental stations (points with count)
    stations = extract_bicycle_rental_stations(pois)
    out_stations = RES_DIR / f"{args.name_prefix}_bicycle_rental_stations.geojson"
    stations.to_file(out_stations, driver="GeoJSON")
    print(f"[OK] Bicycle rental stations -> {out_stations}  (n={len(stations)})")

    # Optional convenience: bicycle parking points
    if args.export_bicycle_parking:
        mask_bp = (pois.get("amenity") == "bicycle_parking")
        bike_parking = pois[mask_bp].copy()
        bike_parking = _represent_points(bike_parking)[["name", "geometry"]]
        out_bp = RES_DIR / f"{args.name_prefix}_bicycle_parking.geojson"
        bike_parking.to_file(out_bp, driver="GeoJSON")
        print(f"[OK] Bicycle parking -> {out_bp}  (n={len(bike_parking)})")

    print("Done.")


if __name__ == "__main__":
    main()
