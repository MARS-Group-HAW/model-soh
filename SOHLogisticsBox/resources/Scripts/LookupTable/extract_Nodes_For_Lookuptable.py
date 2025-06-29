import json
import os

"""
This script extracts transition nodes (entry and exit points) on German motorways
based on topology derived from OpenStreetMap GeoJSON data.

Purpose:
- To minimize the number of routing nodes by only selecting essential junctions.
- Nodes are chosen where a non-motorway connects to a motorway via a "motorway_link" segment.
- Each identified node is stored as a coordinate (lon, lat).

Functionality:
1. Load a GeoJSON file with road data (including 'highway' tags).
2. Build an index mapping each coordinate to its associated highway tags.
3. Identify all "motorway_link" segments.
4. For each segment, detect transitions:
   - From non-motorway to motorway (entry)
   - From motorway to non-motorway (exit)
5. Save all found transition nodes to a JSON file.

Input:
- A GeoJSON file containing roads with elevation and highway tagging

Output:
- A JSON file with transition nodes as coordinate pairs

Example:
- input: "autobahn_und_bundesstrassen_deutschland_elevation_08.geojson"
- output: "entry_exit_nodes_topology_based.json"
"""


# ---------------------- Helper Functions ----------------------

def identity_coord(coord):
    """Returns a coordinate as a (lon, lat) tuple without rounding."""
    return tuple(coord)


def extract_linestring_coords(feature):
    """Extracts all coordinate lists from a LineString or MultiLineString geometry."""
    geometry = feature.get("geometry", {})
    if geometry["type"] == "LineString":
        return [geometry["coordinates"]]
    elif geometry["type"] == "MultiLineString":
        return geometry["coordinates"]
    return []


def is_motorway(tag):
    """Returns True if the highway tag refers to a motorway or a motorway_link."""
    return tag in ("motorway", "motorway_link")


def is_non_motorway(tag):
    """Returns True if the highway tag does not refer to a motorway or motorway_link."""
    return tag and not tag.startswith("motorway")


# ---------------------- Main Processing Logic ----------------------

def build_node_highway_index(features):
    """
    Builds a dictionary mapping each coordinate to the set of highway tags it appears in.
    This allows us to later determine whether a point is shared by motorway and non-motorway segments.
    """
    node_highway_map = {}

    for feature in features:
        highway = feature.get("properties", {}).get("highway")
        if not highway:
            continue

        tags = highway if isinstance(highway, list) else [highway]
        for line in extract_linestring_coords(feature):
            for coord in line:
                pt = identity_coord(coord)
                node_highway_map.setdefault(pt, set()).update(tags)

    return node_highway_map


def extract_transition_nodes(filepath):
    """Extracts entry and exit nodes from a GeoJSON file using motorway_link topology."""
    print(f"Loading file: {filepath}")
    with open(filepath, "r", encoding="utf-8") as f:
        data = json.load(f)

    features = data["features"]
    node_tags = build_node_highway_index(features)

    transition_nodes = set()
    motorway_link_count = 0

    print("Searching for motorway transition points...")

    for feature in features:
        props = feature.get("properties", {})
        highway = props.get("highway")
        if highway != "motorway_link":
            continue

        motorway_link_count += 1

        for line in extract_linestring_coords(feature):
            for i in range(len(line) - 1):
                a = identity_coord(line[i])
                b = identity_coord(line[i + 1])

                tags_a = node_tags.get(a, set())
                tags_b = node_tags.get(b, set())

                # Entry: from non-motorway to motorway
                if any(is_non_motorway(tag) for tag in tags_a) and any(is_motorway(tag) for tag in tags_b):
                    transition_nodes.add(a)

                # Exit: from motorway to non-motorway
                elif any(is_motorway(tag) for tag in tags_a) and any(is_non_motorway(tag) for tag in tags_b):
                    transition_nodes.add(b)

    print(f"Processed motorway_link segments: {motorway_link_count}")
    print(f"Identified transition nodes (entries/exits): {len(transition_nodes)}")

    return list(transition_nodes)


# ---------------------- Entry Point ----------------------

def main():
    input_path = "InputDirectory/autobahn_und_bundesstrassen_deutschland_elevation_08.geojson"
    output_path = "OutputDirectory/entry_exit_nodes_topology_based.json"

    print("Starting extraction of transition nodes...")
    transition_nodes = extract_transition_nodes(input_path)

    with open(output_path, "w", encoding="utf-8") as f:
        json.dump(transition_nodes, f, indent=2)

    print(f"Transition nodes saved to: {os.path.abspath(output_path)}")


if __name__ == "__main__":
    main()
