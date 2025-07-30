import json
import math
from shapely.geometry import Point, LineString
from copy import deepcopy
from tqdm import tqdm
import geopandas as gpd

"""
This script links service/rest area POIs (Points of Interest) to the German road network by:
1. Snapping each POI to the nearest road segment (within 500 meters).
2. Splitting that road segment at the projection point.
3. Creating forward and reverse edges for the two resulting segments.
4. Connecting the POI to the road network with a service link.
5. Adding new nodes and edges to the network.
6. After all insertions, the script assigns correct 'source_tag' metadata to all node points
   by comparing coordinates with the original POI dataset.

Input:
- Original road network with elevation data (GeoJSON)
- POI dataset including rest areas, fuel stations, etc. (GeoJSON)

Output:
- Enhanced GeoJSON including:
  • Split edges,
  • POI-to-road connectors,
  • new node points,
  • correct source tags for analysis or routing.

This data is suitable for simulations, facility access analysis, and customized routing logic.
"""

# ============================================
# File Paths
# ============================================

main_geojson_path = r"InputDirectory/autobahn_und_bundesstrassen_deutschland_elevation_11.geojson"
restarea_geojson_path = r"InputDirectory/services_restareas_fuel_nodes_bundeslaender.geojson"
intermediate_geojson_path = r"OutputDirectory/output_with_split_edge_new.geojson"
final_output_path = r"OutputDirectory/output_with_split_edge_new_fixed_source_tag.geojson"

# ============================================
# Geometry Utilities
# ============================================

def haversine(coord1, coord2):
    """Compute haversine distance in meters between two (lon, lat) coordinates."""
    lon1, lat1 = coord1
    lon2, lat2 = coord2
    R = 6371000  # Earth radius in meters
    phi1, phi2 = math.radians(lat1), math.radians(lat2)
    dphi = math.radians(lat2 - lat1)
    dlambda = math.radians(lon2 - lon1)
    a = math.sin(dphi / 2)**2 + math.cos(phi1) * math.cos(phi2) * math.sin(dlambda / 2)**2
    return R * (2 * math.atan2(math.sqrt(a), math.sqrt(1 - a)))

def calculate_length(coords):
    """Compute total length of a linestring given as list of (lon, lat) tuples."""
    return sum(haversine(coords[i], coords[i+1]) for i in range(len(coords) - 1))

def insert_point_in_linestring(coords, point):
    """Insert a projected point into the nearest segment of a LineString."""
    min_dist = float("inf")
    insert_index = None
    for i in range(len(coords) - 1):
        segment = LineString([coords[i], coords[i + 1]])
        dist = segment.distance(point)
        if dist < min_dist:
            min_dist = dist
            insert_index = i
    if insert_index is None:
        return coords
    return coords[:insert_index + 1] + [tuple(point.coords)[0]] + coords[insert_index + 1:]

# ============================================
# 🛠Feature Builders
# ============================================

def make_edge_properties(base_props, coords, partial_osmids):
    """Generate properties dictionary for a new edge feature."""
    props = deepcopy(base_props)
    props["osmid"] = partial_osmids
    props["length"] = calculate_length(coords)
    props["x"], props["y"] = coords[0]
    props["elevation"] = [0.0 for _ in coords]
    props["capacity_fe"] = 100
    props["maxspeed"] = props.get("maxspeed", 30)
    props["overtaking"] = props.get("overtaking", "yes")
    props["shoulder"] = props.get("shoulder", "no")
    props["lanes"] = props.get("lanes", ["1"])
    props["oneway"] = str(props.get("oneway", False))
    props["reversed"] = str(props.get("reversed", False))
    return props

def make_node(coord, elevation):
    """Create a node feature at given coordinates."""
    return {
        "type": "Feature",
        "properties": {
            "type": "node",
            "x": coord[0],
            "y": coord[1],
            "elevation": elevation,
            "highway": None,
            "ref": None,
            "street_count": None,
            "junction": None,
            "railway": None,
            "lanes": "1",
            "oneway": None,
            "surface": None,
            "reversed": None,
            "length": None,
            "name": None,
            "bridge": None,
            "maxheight": None,
            "tunnel": None,
            "incline": None,
            "maxlength": None,
            "width": None,
            "access": None,
            "maxweight": None,
            "service": None,
            "est_width": None,
            "maxwidth": None,
            "area": None,
            "maxspeed": 50
        },
        "geometry": {
            "type": "Point",
            "coordinates": coord
        }
    }

def match_osmid_set(f_osmid, target_osmids):
    """Check if a feature's osmid matches a target list (robust to type)."""
    if isinstance(f_osmid, list):
        return set(str(o) for o in f_osmid) == set(target_osmids)
    return str(f_osmid) in target_osmids

# ============================================
# Step 1: Add POI connectors to road network
# ============================================

def process_pois():
    """Split roads near POIs and create connector links between POIs and network."""
    with open(main_geojson_path, "r", encoding="utf-8") as f:
        main_data = json.load(f)
    with open(restarea_geojson_path, "r", encoding="utf-8") as f:
        restarea_data = json.load(f)

    features = main_data["features"]
    new_features = []

    for poi in tqdm(restarea_data["features"], desc="Processing POIs"):
        point_A = poi["geometry"]["coordinates"]
        point_osmid = str(poi["properties"].get("osmid"))
        source_tag = poi["properties"].get("source_tag", "poi")
        point_geom = Point(point_A)

        # Find nearest LineString
        min_dist = float("inf")
        closest_feature = None
        projected_point = None
        closest_line = None

        for feature in features:
            if feature["geometry"]["type"] != "LineString":
                continue
            line = LineString(feature["geometry"]["coordinates"])
            proj_point = line.interpolate(line.project(point_geom))
            dist = haversine(point_geom.coords[0], proj_point.coords[0])
            if dist < min_dist:
                min_dist = dist
                closest_feature = feature
                projected_point = proj_point
                closest_line = line

        if not closest_feature or not projected_point or min_dist > 500:
            continue  # Skip POIs that are too far from the network

        point_B = list(projected_point.coords)[0]
        osmid_raw = closest_feature["properties"].get("osmid", [])
        osmid_list = osmid_raw if isinstance(osmid_raw, list) else [osmid_raw]
        osmid_str_list = [str(o) for o in osmid_list]

        # Remove the original edge (to replace it with split parts)
        features = [
            f for f in features
            if not (
                f["geometry"]["type"] == "LineString" and
                match_osmid_set(f["properties"].get("osmid", []), osmid_str_list)
            )
        ]

        line_coords = list(closest_line.coords)
        if point_B not in line_coords:
            line_coords = insert_point_in_linestring(line_coords, projected_point)

        b_index = line_coords.index(point_B)
        coords1 = line_coords[:b_index + 1]
        coords2 = line_coords[b_index:]

        if len(coords1) < 2 or len(coords2) < 2:
            continue  # Skip invalid splits

        midpoint = len(osmid_list) // 2 or 1
        osmids1 = osmid_list[:midpoint]
        osmids2 = osmid_list[midpoint:]

        props = closest_feature["properties"]

        edge1 = {
            "type": "Feature",
            "properties": make_edge_properties(props, coords1, osmids1),
            "geometry": {"type": "LineString", "coordinates": coords1}
        }
        edge2 = {
            "type": "Feature",
            "properties": make_edge_properties(props, coords2, osmids2),
            "geometry": {"type": "LineString", "coordinates": coords2}
        }

        reverse1 = deepcopy(edge1)
        reverse1["properties"]["reversed"] = "True"
        reverse1["geometry"]["coordinates"] = list(reversed(coords1))

        reverse2 = deepcopy(edge2)
        reverse2["properties"]["reversed"] = "True"
        reverse2["geometry"]["coordinates"] = list(reversed(coords2))

        connector_coords = [point_A, point_B]
        connector_props = {
            "type": "edge",
            "osmid": [f"connector_{point_osmid}"],
            "length": haversine(point_A, point_B),
            "x": point_A[0],
            "y": point_A[1],
            "elevation": [0.0, 0.0],
            "highway": "service",
            "lanes": ["1"],
            "oneway": "False",
            "reversed": "False",
            "name": "restarea_link",
            "maxspeed": 30,
            "capacity_fe": 30,
            "overtaking": "yes",
            "shoulder": "no",
            "source_tag": source_tag,
            "poi_osmid": point_osmid
        }

        connector = {
            "type": "Feature",
            "properties": connector_props,
            "geometry": {"type": "LineString", "coordinates": connector_coords}
        }

        reverse_connector = deepcopy(connector)
        reverse_connector["properties"]["reversed"] = "True"
        reverse_connector["geometry"]["coordinates"] = list(reversed(connector_coords))

        node_a = make_node(point_A, 0.0)
        node_b = make_node(point_B, 0.0)

        new_features.extend([
            edge1, reverse1, edge2, reverse2,
            connector, reverse_connector,
            node_a, node_b
        ])

    # Append new features and save result
    main_data["features"] = features + new_features
    with open(intermediate_geojson_path, "w", encoding="utf-8") as f:
        json.dump(main_data, f, indent=2)

# ============================================
# 🏷Step 2: Transfer source_tag to new nodes
# ============================================

def transfer_source_tags():
    """Match each node's coordinates to POIs and assign source_tag accordingly."""
    print("Loading GeoJSON files...")
    gdf_base = gpd.read_file(intermediate_geojson_path)
    gdf_tags = gpd.read_file(restarea_geojson_path)

    def get_coord_key(point, precision=6):
        return (round(point.x, precision), round(point.y, precision))

    # Build coordinate → source_tag dictionary from POIs
    tag_lookup = {
        get_coord_key(row.geometry): row.get("source_tag")
        for _, row in gdf_tags.iterrows()
        if row.geometry.geom_type == "Point" and row.get("source_tag")
    }

    print("Assigning source tags to new nodes...")
    updated_tags = []
    for _, row in gdf_base.iterrows():
        if row.geometry.geom_type == "Point":
            key = get_coord_key(row.geometry)
            updated_tags.append(tag_lookup.get(key, row.get("source_tag")))
        else:
            updated_tags.append(row.get("source_tag"))

    gdf_base["source_tag"] = updated_tags
    gdf_base.to_file(final_output_path, driver="GeoJSON")
    print(f"Enriched GeoJSON saved to: {final_output_path}")

# ============================================
#  Main Execution
# ============================================

if __name__ == "__main__":
    process_pois()
    transfer_source_tags()
