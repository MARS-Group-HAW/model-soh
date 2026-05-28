"""
This script adds a capacity value (in Fahrzeugeinheiten - FE) to each edge feature in a GeoJSON file.
Assumption: 1 FE = 1 meter per lane.
Thus: capacity_fe = properties["length"] * number_of_lanes

Requirements:
- Only edge features ("type": "edge") are processed.
- Features must have a valid "length" property (in meters).
- If "lanes" is missing or invalid, a default is used.

Input:
- autobahn_und_bundesstrassen_merged_maxspeed.geojson

Output:
- autobahn_mit_kapazitaet.geojson
"""

import json

# Konfiguration
input_geojson = "autobahn_und_bundesstrassen_deutschland_elevation_06.geojson"
output_geojson = "autobahn_und_bundesstrassen_deutschland_elevation_07.geojson"
default_lanes = 1

# Funktion zur Kapazitätsberechnung aus property["length"]
def assign_capacity_from_property(feature, default_lanes=1):
    props = feature.get("properties", {})
    if feature.get("type") != "Feature":
        return feature
    if props.get("type") != "edge":
        return feature

    length = props.get("length")
    if length is None:
        return feature

    try:
        lanes = int(props.get("lanes", default_lanes))
    except:
        lanes = default_lanes

    try:
        capacity = round(float(length) * lanes)
    except:
        capacity = 0

    props["capacity_fe"] = capacity
    feature["properties"] = props
    return feature

# Lade GeoJSON
with open(input_geojson, "r", encoding="utf-8") as f:
    data = json.load(f)

# Verarbeite Features
for i, feature in enumerate(data["features"]):
    assign_capacity_from_property(feature)

# Speichere neue GeoJSON
with open(output_geojson, "w", encoding="utf-8") as f:
    json.dump(data, f, indent=2, ensure_ascii=False)

print(f"✅ Done. 'capacity_fe' added to edges and saved to: {output_geojson}")
