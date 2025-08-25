# make_casa_landuse_buildings.py
# Generate Landuse & Buildings for SOH Vector layers (Casablanca Sidi Maârouf)
# Requires: geopandas, shapely, pandas, osmnx

from pathlib import Path
import sys
import warnings
import pandas as pd
import geopandas as gpd
from shapely.geometry import shape
import osmnx as ox

warnings.filterwarnings("ignore", category=UserWarning)

# ---------------------------------------------------------------------
# Paths & Tunables
# ---------------------------------------------------------------------
RES_DIR = Path("resources")
RES_DIR.mkdir(exist_ok=True)

POI_PATH = RES_DIR / "casa_sidi_maarouf_pois.geojson"  # your existing POIs
AOI_PATH = RES_DIR / "casa_sidi_maarouf_aoi.geojson"   # optional; created if missing

OUT_BUILDINGS = RES_DIR / "casa_buildings.geojson"
OUT_LANDUSE   = RES_DIR / "casa_landuse.geojson"

BUFFER_METERS_FOR_POI_HULL = 800   # used only when deriving AOI from POIs
MIN_POLY_AREA_M2 = 100.0           # drop tiny slivers

# ---------------------------------------------------------------------
# OSMnx version compatibility
# ---------------------------------------------------------------------
def features_from_polygon_compat(polygon, tags):
    """
    OSMnx >= 2.x: ox.features.features_from_polygon
    OSMnx  1.x : ox.geometries_from_polygon
    """
    if hasattr(ox, "features") and hasattr(ox.features, "features_from_polygon"):
        return ox.features.features_from_polygon(polygon, tags)
    if hasattr(ox, "geometries_from_polygon"):
        return ox.geometries_from_polygon(polygon, tags)
    raise RuntimeError(
        "OSMnx API not found (neither features_from_polygon nor geometries_from_polygon). "
        "Please upgrade: pip install -U osmnx"
    )

# ---------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------
def _norm_tag(x: object) -> str:
    """Normalize OSM tag values to safe, lowercased strings."""
    if x is None:
        return ""
    try:
        if pd.isna(x):
            return ""
    except Exception:
        pass
    if isinstance(x, (list, tuple, set)):
        x = next(iter(x), "")
    return str(x).strip().lower()

def classify_landuse(row) -> tuple[str, int]:
    landuse = _norm_tag(row.get("landuse"))
    leisure = _norm_tag(row.get("leisure"))
    amenity = _norm_tag(row.get("amenity"))
    natural = _norm_tag(row.get("natural"))

    # Residential
    if landuse == "residential":
        return "residential", 100

    # Commercial / retail / industrial
    if landuse == "commercial" or amenity == "marketplace":
        return "commercial", 110
    if landuse == "retail":
        return "retail", 130
    if landuse == "industrial":
        return "industrial", 120

    # Education
    if amenity in {"university", "college", "school"}:
        return "education", 210

    # Green / recreation
    if landuse in {"recreation_ground", "grass"} or leisure in {"park", "garden", "pitch"}:
        return "green", 200
    if natural in {"wood", "grassland"} or landuse == "forest":
        return "green", 200

    # Catch-all
    return "other", 900

def read_aoi():
    """Load AOI if present; otherwise derive from POIs via convex hull + buffer."""
    if AOI_PATH.exists():
        aoi = gpd.read_file(AOI_PATH)
        if aoi.empty:
            raise RuntimeError("AOI file exists but is empty.")
        return aoi.to_crs(4326).geometry.unary_union

    pois = gpd.read_file(POI_PATH).to_crs(4326)
    if pois.empty:
        raise RuntimeError("POI file is empty; cannot derive AOI.")

    hull = pois.geometry.unary_union.convex_hull
    tmp = gpd.GeoDataFrame(geometry=[hull], crs=4326)
    utm = tmp.estimate_utm_crs()
    buffered = tmp.to_crs(utm).buffer(BUFFER_METERS_FOR_POI_HULL).to_crs(4326)

    buffered.to_file(AOI_PATH, driver="GeoJSON")
    print(f"[AOI] Saved derived AOI -> {AOI_PATH}")
    return buffered.geometry.iloc[0]

def explode_and_clean_polys(gdf: gpd.GeoDataFrame, aoi_geom):
    """Keep polygonal features, fix invalids, clip to AOI, explode multipolys, drop tiny slivers."""
    if gdf.empty:
        return gdf

    gdf = gdf.to_crs(4326)
    gdf = gdf[gdf.geometry.type.isin(["Polygon", "MultiPolygon"])].copy()
    if gdf.empty:
        return gdf

    # Fix invalids
    gdf["geometry"] = gdf.geometry.buffer(0)
    gdf = gdf[~gdf.geometry.is_empty & gdf.geometry.is_valid].copy()
    if gdf.empty:
        return gdf

    # Clip to AOI
    aoi_gdf = gpd.GeoDataFrame(geometry=[aoi_geom], crs=4326)
    gdf = gpd.clip(gdf, aoi_gdf)
    if gdf.empty:
        return gdf

    # Explode multipolygons
    gdf = gdf.explode(index_parts=False, ignore_index=True)
    if gdf.empty:
        return gdf

    # Drop tiny slivers by area
    utm = gdf.estimate_utm_crs()
    gdf_m = gdf.to_crs(utm)
    gdf["area_m2"] = gdf_m.area
    gdf = gdf[gdf["area_m2"] >= MIN_POLY_AREA_M2].drop(columns=["area_m2"])

    return gdf.to_crs(4326)

# ---------------------------------------------------------------------
# Fetchers
# ---------------------------------------------------------------------
def fetch_landuse(aoi_geom) -> gpd.GeoDataFrame:
    tags = {
        "landuse": True,
        "leisure": True,
        "amenity": ["university", "college", "school", "marketplace"],
        "natural": ["wood", "grassland"]
    }
    g = features_from_polygon_compat(aoi_geom, tags)
    if g.empty:
        return g

    g = explode_and_clean_polys(g, aoi_geom)
    if g.empty:
        return g

    # Ensure columns exist and are strings
    keep_cols = ["landuse", "leisure", "amenity", "natural"]
    for c in keep_cols:
        if c not in g.columns:
            g[c] = ""
    g[keep_cols] = g[keep_cols].fillna("")

    # Classify
    g["kind"], g["kind_code"] = zip(*g.apply(classify_landuse, axis=1))

    # De-dup by geometry
    g["_wkb"] = g.geometry.apply(lambda x: x.wkb_hex)
    g = g.drop_duplicates(subset=["_wkb"]).drop(columns=["_wkb"])

    cols = ["kind", "kind_code"] + keep_cols + ["geometry"]
    return g[cols]

def fetch_buildings(aoi_geom) -> gpd.GeoDataFrame:
    tags = {"building": True}
    g = features_from_polygon_compat(aoi_geom, tags)
    if g.empty:
        return g

    g = explode_and_clean_polys(g, aoi_geom)
    if g.empty:
        return g

    # Ensure columns exist
    for c in ["building", "building:use", "name"]:
        if c not in g.columns:
            g[c] = ""

    # De-dup geometry
    g["_wkb"] = g.geometry.apply(lambda x: x.wkb_hex)
    g = g.drop_duplicates(subset=["_wkb"]).drop(columns=["_wkb"])

    return g[["building", "building:use", "name", "geometry"]]

# ---------------------------------------------------------------------
# Main
# ---------------------------------------------------------------------
def main():
    # OSMnx settings
    ox.settings.log_console = True
    ox.settings.use_cache = True
    ox.settings.overpass_rate_limit = True
    ox.settings.timeout = 180

    aoi_geom = read_aoi()
    print("[AOI] bbox:", gpd.GeoSeries([aoi_geom], crs=4326).total_bounds)

    # Landuse
    landuse = fetch_landuse(aoi_geom)
    if landuse.empty:
        print("[WARN] No landuse polygons found.")
    else:
        landuse.to_file(OUT_LANDUSE, driver="GeoJSON")
        print(f"[OK] Landuse -> {OUT_LANDUSE} ({len(landuse)} features)")
        try:
            print("[Landuse kinds]")
            print(landuse["kind"].value_counts().to_string())
        except Exception:
            pass

    # Buildings
    buildings = fetch_buildings(aoi_geom)
    if buildings.empty:
        print("[WARN] No buildings found.")
    else:
        buildings.to_file(OUT_BUILDINGS, driver="GeoJSON")
        print(f"[OK] Buildings -> {OUT_BUILDINGS} ({len(buildings)} features)")

if __name__ == "__main__":
    try:
        main()
    except Exception as e:
        print("ERROR:", e)
        sys.exit(1)
