import json
import math


"""
This script calculates the maximum incline percentage for each feature in a GeoJSON file 
based on elevation and coordinate data, then updates the file with the computed values.

Functionality:
1. Reads a GeoJSON file containing road network data with elevation values.
2. Calculates the maximum incline for each feature:
   - Uses the Haversine formula to determine segment distances.
   - Computes incline percentages based on elevation differences.
   - Ignores invalid or missing elevation data.
3. Updates the GeoJSON file with the calculated incline values.
4. Saves the modified GeoJSON file.

Methods:
- `haversine_distance(coord1, coord2)`: Computes the distance between two latitude/longitude coordinates.
- `clean_elevations(elevations)`: Converts elevation values to floats and removes invalid entries.
- `calculate_max_incline(elevations, coordinates)`: Determines the steepest incline percentage in a given set of points.
- `process_geojson(input_geojson, output_geojson)`: Reads, processes, and writes the updated GeoJSON file.

Input:
- "InputDirectory/autobahn_und_bundesstrassen_deutschland.geojson"

Output:
- "OutputDirectory/autobahn_und_bundesstrassen_deutschland_calculated_incline.geojson"

Run the script to process the input file and generate an updated version with incline data.
"""


# Constants
EARTH_RADIUS = 6371000  # Earth radius in meters

def haversine_distance(coord1, coord2):
    """Calculate the Haversine distance between two lat/lon coordinates in meters."""
    lon1, lat1 = math.radians(coord1[0]), math.radians(coord1[1])
    lon2, lat2 = math.radians(coord2[0]), math.radians(coord2[1])

    dlat = lat2 - lat1
    dlon = lon2 - lon1

    a = math.sin(dlat / 2) ** 2 + math.cos(lat1) * math.cos(lat2) * math.sin(dlon / 2) ** 2
    c = 2 * math.atan2(math.sqrt(a), math.sqrt(1 - a))

    return EARTH_RADIUS * c  # Distance in meters

def clean_elevations(elevations):
    """Convert elevations to floats and remove None values."""
    return [float(e) if isinstance(e, (int, float)) else None for e in elevations]

def calculate_max_incline(elevations, coordinates):
    """Calculate the maximum incline percentage for a given list of elevations and coordinates."""
    if len(elevations) < 2 or len(coordinates) < 2 or len(elevations) != len(coordinates):
        return 0.0

    # Clean elevation data (remove None values)
    elevations = clean_elevations(elevations)

    max_incline = 0.0

    for i in range(len(elevations) - 1):
        if elevations[i] is None or elevations[i + 1] is None:
            continue  # Skip invalid elevation pairs

        elevation_diff = elevations[i + 1] - elevations[i]

        # Ensure coordinates are valid
        if not (isinstance(coordinates[i], (list, tuple)) and len(coordinates[i]) == 2):
            continue

        segment_distance = haversine_distance(coordinates[i], coordinates[i + 1])

        if segment_distance < 1e-3:
            segment_distance = 1.0  # Avoid division by zero

        incline = (elevation_diff / segment_distance) * 100
        max_incline = max(max_incline, abs(incline))

    return max_incline

def process_geojson(input_geojson, output_geojson):
    """Read GeoJSON, calculate missing inclines, and write updated GeoJSON."""
    with open(input_geojson, "r", encoding="utf-8") as file:
        data = json.load(file)

    for feature in data.get("features", []):
        properties = feature.get("properties", {})
        geometry = feature.get("geometry", {})

        # Check if incline is null and needs to be calculated
        if "incline" in properties and properties["incline"] is not None:
            continue  # Skip features where incline is already set

        # Extract elevations and coordinates
        elevations = properties.get("elevation", [])
        coordinates = geometry.get("coordinates", [])

        # Ensure valid data
        if not isinstance(elevations, list) or not isinstance(coordinates, list) or len(elevations) != len(coordinates):
            continue

        # Ensure each coordinate is a valid tuple
        coordinates = [tuple(coord) for coord in coordinates if isinstance(coord, list) and len(coord) == 2]

        # Skip if coordinates are still invalid
        if len(coordinates) != len(elevations):
            continue

        # Calculate max incline
        max_incline = calculate_max_incline(elevations, coordinates)

        # Update the feature with the calculated incline
        properties["incline"] = max_incline

    # Write updated GeoJSON
    with open(output_geojson, "w", encoding="utf-8") as file:
        json.dump(data, file, indent=4, ensure_ascii=False)

# Paths, change according to needed in- and output files
input_geojson = "InputDirectory/autobahn_und_bundesstrassen_deutschland.geojson" 
output_geojson = "OutputDirectory/autobahn_und_bundesstrassen_deutschland_calculated_incline.geojson"

# Run the function
process_geojson(input_geojson, output_geojson)
print("Processing complete. Updated GeoJSON saved.")
