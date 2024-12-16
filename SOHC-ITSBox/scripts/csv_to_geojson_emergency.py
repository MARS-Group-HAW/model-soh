import csv
import json

def csv_to_geojson():
    # Define the file paths
    csv_file = "../bin/Debug/net8.0/EmergencyCarDriver.csv"
    geojson_file = "e_output.geojson"

    # Initialize an empty GeoJSON structure
    geojson = {
        "type": "FeatureCollection",
        "features": []
    }

    # Read the CSV file and create features
    with open(csv_file, newline='') as csvfile:
        reader = csv.DictReader(csvfile)
        for row in reader:
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

# Run the script
if __name__ == "__main__":
    csv_to_geojson()
