import geopandas as gpd
import pandas as pd

"""
This script extracts geographic coordinate data for either rest areas or gas stations
from a preprocessed GeoJSON file and saves them into two separate CSV files.

It supports both:
- Rest areas: nodes with source_tag "rest_area" or "services"
- Gas stations: nodes with source_tag "fuel" or "services"

Steps:
1. Load the GeoJSON file containing POIs and road network data.
2. Extract latitude and longitude from each point or polygon (centroid).
3. Filter entries based on their 'source_tag' to separate rest areas and fuel stations.
4. Sort by longitude and latitude for consistency.
5. Write the results into two CSV files with columns: id, lat, lon.

The output can be used for plotting, spatial queries, routing services, or simulation input.
"""

# ============================================
# File Paths
# ============================================

input_path = r"InputDirectory/output_with_split_edge_new_fixed_01.geojson"
output_rest_path = r"OutputDirectory/rest_areas.csv"
output_gas_path = r"OutputDirectory/gas_stations.csv"

# ============================================
# Step 1: Load the processed GeoJSON file
# ============================================

print("Loading GeoJSON...")
gdf = gpd.read_file(input_path)

# ============================================
# Step 2: Extract latitude and longitude
# ============================================

def extract_coords(geom):
    """Extract (lat, lon) from Point or centroid of geometry."""
    if geom.geom_type == "Point":
        return geom.y, geom.x
    else:
        centroid = geom.centroid
        return centroid.y, centroid.x

# ============================================
# Step 3: Filter and export rest areas
# ============================================

# Includes both rest_area and services POIs
gdf_rest = gdf[gdf["source_tag"].isin(["rest_area", "services"])].copy()
gdf_rest["lat"], gdf_rest["lon"] = zip(*gdf_rest.geometry.apply(extract_coords))
gdf_rest_sorted = gdf_rest.sort_values(by=["lon", "lat"])

# Prepare clean output DataFrame
df_rest_output = gdf_rest_sorted[["lat", "lon"]].copy()
df_rest_output.insert(0, "id", range(1, len(df_rest_output) + 1))

# Write to CSV
df_rest_output.to_csv(output_rest_path, index=False)
print(f"Rest areas saved to: {output_rest_path} ({len(df_rest_output)} entries)")

# ============================================
# Step 4: Filter and export gas stations
# ============================================

# Includes both fuel and services POIs
gdf_gas = gdf[gdf["source_tag"].isin(["fuel", "services"])].copy()
gdf_gas["lat"], gdf_gas["lon"] = zip(*gdf_gas.geometry.apply(extract_coords))
gdf_gas_sorted = gdf_gas.sort_values(by=["lon", "lat"])

df_gas_output = gdf_gas_sorted[["lat", "lon"]].copy()
df_gas_output.insert(0, "id", range(1, len(df_gas_output) + 1))

# Write to CSV
df_gas_output.to_csv(output_gas_path, index=False)
print(f"Gas stations saved to: {output_gas_path} ({len(df_gas_output)} entries)")
