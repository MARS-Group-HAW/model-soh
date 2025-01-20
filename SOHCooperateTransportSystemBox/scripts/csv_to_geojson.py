import csv
import json
from tqdm import tqdm  # Install with `pip install tqdm`

def csv_to_geojson(csv_file, geojson_file):
    # Initialize an empty GeoJSON structure
    geojson = {
        "type": "FeatureCollection",
        "features": []
    }

    # Read the CSV file and create features
    with open(csv_file, newline='') as csvfile:
        reader = list(csv.DictReader(csvfile))
        total_rows = len(reader)

        # Use tqdm to show progress
        for row in tqdm(reader, desc="Processing rows", unit="row", total=total_rows):
            # Convert row to GeoJSON feature
            feature = {
                "type": "Feature",
                "geometry": {
                    "type": "Point",
                    "coordinates": [
                        float(row["Longitude"]),
                        float(row["Latitude"])
                    ]
                },
                "properties": {key: row[key] for key in row if key not in ["Latitude", "Longitude"]}
            }
            geojson["features"].append(feature)

    # Write the GeoJSON to a file
    with open(geojson_file, 'w') as f:
        json.dump(geojson, f, indent=2)

    print(f"GeoJSON file '{geojson_file}' created successfully.")

def main():
    # Prompt the user to choose the version
    print("Choose a version to process:")
    print("1. CarDriver")
    print("2. EmergencyCarDriver")
    choice = input("Enter 1 or 2: ")

    if choice == "1":
        csv_file = "../bin/Debug/net8.0/CarDriver.csv"
        geojson_file = "output.geojson"
    elif choice == "2":
        csv_file = "../bin/Debug/net8.0/EmergencyCarDriver.csv"
        geojson_file = "e_output.geojson"
    else:
        print("Invalid choice. Exiting.")
        return

    csv_to_geojson(csv_file, geojson_file)

# Run the script
if __name__ == "__main__":
    main()
