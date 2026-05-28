import osmnx as ox
import geopandas as gpd
import pandas as pd
import json

"""
This script downloads Germany's motorway and primary road network from OpenStreetMap using OSMnx,
including custom attributes such as maxspeed, maxheight, and maxweight. It then enriches a 
preprocessed GeoJSON file (containing elevation and geometry) with real or estimated maxspeed values 
for each road segment.

Steps:
1. Download and process the road network for Germany (motorways, trunks, and primary roads).
2. Extract relevant OSM attributes including 'maxspeed'.
3. Export a combined GeoJSON containing both nodes and edges.
4. Load a previously processed GeoJSON (with elevation data).
5. Merge the maxspeed values from the new network into the existing dataset.
   - If a real value is missing, assign a default based on highway type.
6. Save the enriched GeoJSON with updated maxspeed information.

This data can be used for traffic simulations, routing algorithms, and constraint-based vehicle modeling.
"""

# ============================================
# ⚙️ Configuration
# ============================================

# Additional OSM tags to include
additional_tags = [
    "maxheight", "maxlength", "surface",
    "maxweight", "maxwidth", "incline", "maxspeed"
]
ox.settings.useful_tags_way += additional_tags

# Filter for main road types only
custom_filter = '["highway"~"motorway|trunk|primary"]'

# Default speed values by highway type (km/h)
osm_speed_defaults = {
    'motorway': 130,
    'trunk': 100,
    'primary': 100,
    'secondary': 70,
    'tertiary': 50,
    'residential': 30,
    'living_street': 7
}

# ============================================
# 🛰️ Step 1: Download and process OSM road network
# ============================================

print("📥 Downloading road network from OSM...")

all_roads = ox.graph_from_place(
    ["Germany"],
    network_type="drive",
    custom_filter=custom_filter,
    simplify=True
)

# Extract nodes and edges from graph
gdf_nodes = ox.graph_to_gdfs(all_roads, edges=False, nodes=True)
gdf_edges = ox.graph_to_gdfs(all_roads, edges=True, nodes=False)

# Add type labels
gdf_nodes["type"] = "node"
gdf_edges["type"] = "edge"

# Combine into single GeoDataFrame
gdf_combined = pd.concat([gdf_nodes, gdf_edges], ignore_index=True)

# Save combined GeoJSON (including 'maxspeed')
geojson_with_maxspeed_path = "autobahn_und_bundesstrassen_with_maxspeed.geojson"
print(f"💾 Saving to {geojson_with_maxspeed_path}...")
gdf_combined.to_file(geojson_with_maxspeed_path, driver="GeoJSON")

print("✅ Step 1 Done. Network saved with maxspeed included.")

# ============================================
# 🧮 Step 2: Combine old dataset with maxspeed
# ============================================

def normalize_osmid(osmid):
    """Ensure consistent OSM ID formatting (handle list or single values)."""
    if isinstance(osmid, list):
        return tuple(sorted(osmid))
    return (osmid,)

def extract_valid_maxspeed(value):
    """Extract integer maxspeed from OSM value string."""
    try:
        if isinstance(value, list):
            value = value[0]
        speed = int(str(value).split()[0])
        return speed
    except:
        return None

# Load new data with maxspeed values
with open(geojson_with_maxspeed_path, "r", encoding="utf-8") as f:
    new_data = json.load(f)

# Load old data (with elevation, geometry, etc.)
with open("autobahn_und_bundesstrassen_deutschland_elevation_05.geojson", "r", encoding="utf-8") as f:
    old_data = json.load(f)

# Build lookup table: osmid → maxspeed
maxspeed_lookup = {}
for feature in new_data["features"]:
    props = feature.get("properties", {})
    osmid = normalize_osmid(props.get("osmid"))
    maxspeed = extract_valid_maxspeed(props.get("maxspeed"))
    if osmid and maxspeed:
        maxspeed_lookup[osmid] = maxspeed

# Merge maxspeed into the old dataset
for feature in old_data["features"]:
    props = feature.get("properties", {})
    osmid = normalize_osmid(props.get("osmid"))
    assigned_speed = maxspeed_lookup.get(osmid)

    if assigned_speed is None:
        # Use fallback based on highway type
        highway = props.get("highway", "")
        if isinstance(highway, list):
            fallback = min([osm_speed_defaults.get(h, 50) for h in highway]) if highway else 50
        else:
            fallback = osm_speed_defaults.get(highway, 50)
        props["maxspeed"] = fallback
    else:
        props["maxspeed"] = assigned_speed

    feature["properties"] = props

# Save the enriched dataset
output_path_final = "autobahn_und_bundesstrassen_deutschland_elevation_06.geojson"
with open(output_path_final, "w", encoding="utf-8") as f:
    json.dump(old_data, f, indent=2, ensure_ascii=False)

print(f"✅ Step 2 Done. Merged maxspeed saved to {output_path_final}.")
