import numpy as np
import geopandas as gpd
from shapely.geometry import Point
from shapely.validation import make_valid

def generate_points_in_aoi(
    aoi_path: str,
    n_points: int = 50,
    min_dist_m: float = 0.0,        # 0 disables spacing; otherwise enforce >= this distance
    rng_seed: int = 1,
    out_geojson: str = "random_points.geojson",
    out_csv: str = "random_points.csv",
):
    # 1) Load AOI (WGS84) and prep metric CRS for distance ops
    aoi_4326 = gpd.read_file(aoi_path).to_crs(4326)
    if aoi_4326.empty:
        raise ValueError("AOI file has no geometries.")
    aoi_union_4326 = make_valid(aoi_4326.unary_union)

    crs_m = aoi_4326.estimate_utm_crs() or "EPSG:3857"
    aoi_m = gpd.GeoSeries([aoi_union_4326], crs=4326).to_crs(crs_m).iloc[0]

    # 2) Rejection sampling inside AOI bbox (metric CRS), with optional spacing
    rng = np.random.default_rng(rng_seed)
    minx, miny, maxx, maxy = aoi_m.bounds

    pts_m = []
    attempts = 0
    max_attempts = max(10_000, n_points * 200)

    while len(pts_m) < n_points and attempts < max_attempts:
        attempts += 1
        x = rng.uniform(minx, maxx)
        y = rng.uniform(miny, maxy)
        p = Point(x, y)
        if not aoi_m.contains(p):
            continue
        if min_dist_m > 0:
            # simple spacing check; OK for up to a few thousand points
            too_close = any(p.distance(q) < min_dist_m for q in pts_m)
            if too_close:
                continue
        pts_m.append(p)

    if len(pts_m) < n_points:
        raise RuntimeError(
            f"Placed {len(pts_m)}/{n_points} points after {attempts} attempts. "
            f"Try lowering min_dist_m or increasing AOI size."
        )

    # 3) Back to WGS84 and export
    gdf_m = gpd.GeoDataFrame({"id": range(1, len(pts_m)+1)}, geometry=pts_m, crs=crs_m)
    gdf = gdf_m.to_crs(4326)
    gdf["lon"] = gdf.geometry.x
    gdf["lat"] = gdf.geometry.y
    gdf["wkt"] = gdf.geometry.apply(lambda g: g.wkt)

    gdf.to_file(out_geojson, driver="GeoJSON")
    gdf[["id", "lon", "lat", "wkt"]].to_csv(out_csv, index=False)

    print(f"Saved {len(gdf)} points to {out_geojson} and {out_csv}")
    return gdf

if __name__ == "__main__":
    # Example: your earlier AOI path
    gdf = generate_points_in_aoi(
        aoi_path="resources/casa_sidi_maarouf_aoi.geojson",
        n_points=60,
        min_dist_m=20.0,     # set 0 for pure uniform points
        rng_seed=3,
        out_geojson="aoi_points.geojson",
        out_csv="aoi_points.csv",
    )
    # show a few
    print(gdf.head(5)[["id","lon","lat"]])
