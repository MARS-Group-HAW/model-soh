# save as make_casa_sidi_maarouf_graphs.py
from pathlib import Path
import json
import osmnx as ox
import geopandas as gpd
from shapely.geometry import shape
from shapely import wkt

RES_DIR = Path("resources")
RES_DIR.mkdir(exist_ok=True)

# ---- Tunables ---------------------------------------------------------------
NAME_PREFIX = "casa_sidi_maarouf"
BUFFER_METERS = 3500         # radius if we must buffer a point
MAX_AOI_AREA_KM2 = 25        # reject anything bigger (Casablanca ~385 km²)
OPTIONAL_AOI_WKT = None      # paste a WKT polygon here to force AOI
# Example bbox WKT you can use instead of None:
# OPTIONAL_AOI_WKT = (
#     "POLYGON((-7.67884 33.49402, -7.67884 33.53402, "
#     "-7.62884 33.53402, -7.62884 33.49402, -7.67884 33.49402))"
# )
# ---------------------------------------------------------------------------

QUERIES = [
    # Keep the most specific strings first; dicts can resolve to big admin areas
    "Sidi Maârouf, Ain Chock, Casablanca, Morocco",
    "Sidi Maarouf, Ain Chock, Casablanca, Morocco",
    "Sidi Maârouf, Casablanca, Morocco",
    "Sidi Maarouf, Casablanca, Morocco",
]

# Fallback center (Sidi Maârouf approx.)
FALLBACK_POINT = (33.51402, -7.65384)  # (lat, lon)

def area_km2(geom):
    gdf = gpd.GeoDataFrame(geometry=[geom], crs=4326)
    utm = gdf.estimate_utm_crs()
    return gdf.to_crs(utm).area.iloc[0] / 1_000_000.0

def buffer_point(lat, lon, meters):
    gdf = gpd.GeoDataFrame(geometry=gpd.points_from_xy([lon], [lat]), crs=4326)
    utm = gdf.estimate_utm_crs()
    return gdf.to_crs(utm).buffer(meters).to_crs(4326).iloc[0]

def resolve_aoi():
    # 0) User-forced WKT wins
    if OPTIONAL_AOI_WKT:
        return wkt.loads(OPTIONAL_AOI_WKT)

    # 1) Try specific geocoding strings
    for q in QUERIES:
        try:
            gdf = ox.geocode_to_gdf(q)
            if gdf.empty:
                continue
            geom = gdf.geometry.iloc[0]

            if geom.geom_type.lower() == "point":
                return buffer_point(geom.y, geom.x, BUFFER_METERS)

            # Reject polygons that are clearly too large
            if area_km2(geom) <= MAX_AOI_AREA_KM2:
                return geom
            # else: too big → try next query
        except Exception:
            pass

    # 2) Final fallback: point buffer around known center
    lat, lon = FALLBACK_POINT
    return buffer_point(lat, lon, BUFFER_METERS)

def export_aoi(geom):
    gdf = gpd.GeoDataFrame(geometry=[geom], crs=4326)
    out = RES_DIR / f"{NAME_PREFIX}_aoi.geojson"
    gdf.to_file(out, driver="GeoJSON")
    return out

def export_graphs(geom):
    # Keep graphs clipped to AOI boundary
    G_drive = ox.graph_from_polygon(
        geom, network_type="drive", simplify=True, retain_all=False, truncate_by_edge=True
    )
    G_walk = ox.graph_from_polygon(
        geom, network_type="walk", simplify=True, retain_all=False, truncate_by_edge=True
    )

    edges_drive = ox.graph_to_gdfs(G_drive, nodes=False, edges=True).to_crs(4326)
    edges_walk  = ox.graph_to_gdfs(G_walk,  nodes=False, edges=True).to_crs(4326)

    (RES_DIR / f"{NAME_PREFIX}_drive_graph.geojson").write_text(edges_drive.to_json())
    (RES_DIR / f"{NAME_PREFIX}_walk_graph.geojson").write_text(edges_walk.to_json())

if __name__ == "__main__":
    ox.settings.log_console = True
    ox.settings.use_cache = True
    ox.settings.overpass_rate_limit = True
    ox.settings.timeout = 180

    aoi = resolve_aoi()
    aoi_path = export_aoi(aoi)
    print(f"AOI saved -> {aoi_path}  (area ≈ {area_km2(aoi):.2f} km²)")
    export_graphs(aoi)
    print(f"OK -> {RES_DIR}/{NAME_PREFIX}_{{drive,walk}}_graph.geojson")
