import overpy
import geopandas as gpd
from shapely.geometry import Point
import pandas as pd
import time

"""
This script uses the Overpass API to download highway-related POIs (points of interest)
from OpenStreetMap for all 16 German federal states (Bundesländer).

The following POI types are extracted:
- highway=services (rest stops with services)
- highway=rest_area (basic rest stops)
- amenity=fuel (gas stations)

Steps:
1. For each German state:
   - Query all relevant POI nodes within the state's boundary.
   - Extract coordinates and relevant tags.
   - Assign a standardized 'source_tag' for consistent downstream processing.
2. Store the data in a GeoDataFrame.
3. Export the combined result to a GeoJSON file.

This dataset is intended to be used for traffic simulations, access analysis, and integration
with road networks (e.g., for connecting POIs to nearby roads).
"""

# ============================================
# List of German federal states (as named in OSM)
# ============================================

bundeslaender = [
    "Baden-Württemberg", "Bayern", "Berlin", "Brandenburg", "Bremen",
    "Hamburg", "Hessen", "Mecklenburg-Vorpommern", "Niedersachsen",
    "Nordrhein-Westfalen", "Rheinland-Pfalz", "Saarland",
    "Sachsen", "Sachsen-Anhalt", "Schleswig-Holstein", "Thüringen"
]

# Initialize Overpass API
api = overpy.Overpass()
features = []

# ============================================
# Step 1: Query POIs from OSM for each state
# ============================================

for name in bundeslaender:
    print(f"Processing state: {name}")
    
    # Overpass query for highway services, rest areas, and fuel stations
    query = f"""
    [out:json][timeout:600];
    area["name"="{name}"]["admin_level"="4"]->.searchArea;
    (
      node["highway"="services"](area.searchArea);
      node["highway"="rest_area"](area.searchArea);
      node["amenity"="fuel"](area.searchArea);
    );
    out body;
    """
    
    try:
        result = api.query(query)
        print(f"Found {len(result.nodes)} nodes in {name}")
        
        # Process each returned node
        for node in result.nodes:
            geom = Point(float(node.lon), float(node.lat))
            props = node.tags.copy()
            props["osmid"] = node.id
            props["type"] = "node"

            # Assign a normalized tag for the node type
            if props.get("highway") == "services":
                props["source_tag"] = "services"
            elif props.get("highway") == "rest_area":
                props["source_tag"] = "rest_area"
            elif props.get("amenity") == "fuel":
                props["source_tag"] = "fuel"
            else:
                props["source_tag"] = "unknown"

            features.append({
                "geometry": geom,
                "properties": props
            })

        # Respect API rate limits
        time.sleep(1.5)
        
    except Exception as e:
        print(f"Error in {name}: {e}")
        continue

# ============================================
# Step 2: Convert to GeoDataFrame and clean up
# ============================================

# Build GeoDataFrame from collected features
gdf = gpd.GeoDataFrame(
    [f["properties"] for f in features],
    geometry=[f["geometry"] for f in features],
    crs="EPSG:4326"
)

# Keep only desired columns (ensure missing ones are filled with None)
columns = ["osmid", "name", "source_tag", "type", "geometry"]
for col in columns:
    if col not in gdf.columns:
        gdf[col] = None
gdf = gdf[columns]

# ============================================
# Step 3: Export to GeoJSON
# ============================================

output_path = "services_restareas_fuel_nodes_bundeslaender.geojson"
gdf.to_file(output_path, driver="GeoJSON")

print(f"\nDone. Total {len(gdf)} points saved to {output_path}")
