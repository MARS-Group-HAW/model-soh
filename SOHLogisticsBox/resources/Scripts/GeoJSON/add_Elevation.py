import json
import requests
import time
from tqdm import tqdm

"""
This script fetches elevation data from the OpenTopoData API and updates a GeoJSON file 
with missing elevation values for Points and LineStrings.

API Usage:
- The API has a limited number of free queries.
- To avoid exceeding limits, consider using a temporary file, spreading requests over time, 
  paying for additional queries, or using an alternative API.

Functionality:
1. Loads a GeoJSON file and identifies missing elevations.
2. Sends batch requests (up to 100 coordinates at a time) to the API.
3. Handles rate limits with exponential backoff.
4. Assigns elevation to Point features and ensures LineStrings have list of elevation for every coordinate.
5. Saves the updated GeoJSON file.

Input:
- "InputDirectory/autobahn_und_bundesstrassen_deutschland_no_elevation.geojson"

Output:
- "OutputDirectory/autobahn_und_bundesstrassen_deutschland_with_elevation.geojson"

Run the script to process the input file and generate an updated version with elevation data.
"""



# API CONFIGURATION
#Important, this API only has limited Free queries, either work with temp file and
#download elevation for multiple days or pay for queries or use different API
ELEVATION_API_URL = "https://api.opentopodata.org/v1/eudem25m"

# FILE PATHS
INPUT_FILE = "InputDirectory/autobahn_und_bundesstrassen_deutschland_no_elevation.geojson"
OUTPUT_FILE = "OutputDirectory/autobahn_und_bundesstrassen_deutschland_with_elevation.geojson"

# API SETTINGS
BATCH_SIZE = 100  # Max batch size supported by API
INITIAL_WAIT_TIME = 2
MAX_WAIT_TIME = 60


def load_geojson(file_path):
    """Load a GeoJSON file."""
    with open(file_path, 'r', encoding='utf-8') as file:
        return json.load(file)


def fetch_elevation_batch(coordinates_batch):
    """Fetch elevation for a batch of coordinates."""
    url = f"{ELEVATION_API_URL}?locations={'|'.join(coordinates_batch)}"
    wait_time = INITIAL_WAIT_TIME

    while True:
        try:
            response = requests.get(url, timeout=10)

            if response.status_code == 200:
                return [result.get('elevation') for result in response.json().get('results', [])]

            elif response.status_code == 429:
                print(f"Rate limit hit. Retrying in {wait_time}s...")
                time.sleep(wait_time)
                wait_time = min(wait_time * 2, MAX_WAIT_TIME)

            else:
                print(f"API Error {response.status_code}: {response.text}")
                return [None] * len(coordinates_batch)

        except requests.exceptions.RequestException as e:
            print(f"⏳ Network error: {e}. Retrying in {wait_time}s...")
            time.sleep(wait_time)


def process_features(geojson_data):
    """Ensure all features have elevation and process LineStrings completely."""
    features = geojson_data.get("features", [])

    coordinates_batch = []
    coordinate_mapping = []  # To map each coordinate back to its feature and index

    for feature in tqdm(features, desc="Processing Features"):
        geometry = feature.get("geometry")

        # Skip if no geometry exists
        if not geometry:
            continue

        # Process Points
        if geometry["type"] == "Point":
            lon, lat = geometry["coordinates"]
            elevation = feature["properties"].get("elevation")

            if elevation is None or not isinstance(elevation, (int, float)):
                coordinates_batch.append(f"{lat},{lon}")
                coordinate_mapping.append((feature, None))  # No index needed for Points

        # Process LineStrings
        elif geometry["type"] == "LineString":
            coordinates = geometry["coordinates"]

            # Ensure we have an elevation list for every coordinate
            if "elevation" not in feature["properties"]:
                feature["properties"]["elevation"] = [None] * len(coordinates)

            for idx, (lon, lat) in enumerate(coordinates):
                if feature["properties"]["elevation"][idx] is None:
                    coordinates_batch.append(f"{lat},{lon}")
                    coordinate_mapping.append((feature, idx))

                # Process batch when limit is reached
                if len(coordinates_batch) >= BATCH_SIZE:
                    update_elevations(coordinates_batch, coordinate_mapping)
                    coordinates_batch, coordinate_mapping = [], []

    # Process any remaining batch
    if coordinates_batch:
        update_elevations(coordinates_batch, coordinate_mapping)

    return geojson_data


def update_elevations(coordinates_batch, coordinate_mapping):
    """Fetch and update elevations for a batch of coordinates."""
    elevations = fetch_elevation_batch(coordinates_batch)

    for (feature, idx), elevation in zip(coordinate_mapping, elevations):
        if idx is None:
            feature["properties"]["elevation"] = elevation  # Point elevation
        else:
            feature["properties"]["elevation"][idx] = elevation  # LineString elevation


def save_geojson(geojson_data, output_file):
    """Save updated GeoJSON with complete elevation data."""
    with open(output_file, "w", encoding="utf-8") as file:
        json.dump(geojson_data, file, indent=4)

    print(f"Final output saved as: {output_file}")


def main():
    print("🔍 Starting elevation processing...")
    geojson_data = load_geojson(INPUT_FILE)

    updated_geojson = process_features(geojson_data)

    save_geojson(updated_geojson, OUTPUT_FILE)


if __name__ == "__main__":
    main()
