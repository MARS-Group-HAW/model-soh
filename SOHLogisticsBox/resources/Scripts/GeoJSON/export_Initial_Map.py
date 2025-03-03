"""
This script downloads and processes road network data for Germany using OSMnx. 
It extracts motorway and major roads (autobahns and primary roads) while keeping 
important attributes for routing and transportation analysis.

Processing steps:
1. Define additional useful road attributes such as max height, max weight, surface type, etc.
2. Download the road network for Germany, filtering only highways, trunks, and primary roads.
3. Extract nodes (junction points) and edges (road segments) from the network.
4. Remove the "maxspeed" attribute from edges if it exists as it causes issues in MARS (to be fixed later).
5. Add a type identifier ("node" or "edge") to each element.
6. Combine nodes and edges into a single GeoDataFrame.
7. Save the processed data as a GeoJSON file for further analysis.

Output:
- "autobahn_und_bundesstrassen_germany.geojson"
  - Contains the extracted road network for Germany with attributes suitable for routing analysis.
"""

import osmnx as ox
import geopandas as gpd
import pandas as pd

# Define additional road attributes to retrieve
additional_tags = [
    "maxheight",
    "maxlength",
    "surface",
    "maxweight",
    "maxwidth",
    "incline",
]

# Add custom attributes to OSMnx settings
ox.settings.useful_tags_way += additional_tags

# Define a filter to extract only highways, trunks, and primary roads
custom_filter = '["highway"~"motorway|trunk|primary"]'

# Download road network for Germany (motorways and major roads)
all_roads = ox.graph_from_place(
    ["Germany"],
    network_type="drive",
    custom_filter=custom_filter,
    simplify=True
)

# Extract nodes (junction points)
gdf_nodes = ox.graph_to_gdfs(all_roads, edges=False, nodes=True)

# Extract edges (road segments)
gdf_edges = ox.graph_to_gdfs(all_roads, edges=True, nodes=False)

# Remove the "maxspeed" attribute from edges if it exists
if "maxspeed" in gdf_edges.columns:
    gdf_edges = gdf_edges.drop(columns=["maxspeed"])

# Add a type label for nodes and edges
gdf_nodes["type"] = "node"
gdf_edges["type"] = "edge"

# Combine nodes and edges into a single GeoDataFrame
gdf_combined = pd.concat([gdf_nodes, gdf_edges], ignore_index=True)

# Save the processed road network as a GeoJSON file
gdf_combined.to_file("autobahn_und_bundesstrassen_germany.geojson", driver="GeoJSON")
