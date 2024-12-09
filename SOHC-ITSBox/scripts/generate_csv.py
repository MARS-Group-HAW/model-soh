import json
import csv


# Function to process the JSON file and extract coordinates
def process_json(input_file, output_file):
    # Open the JSON file
    with open(input_file, 'r') as f:
        data = json.load(f)

    # Prepare the CSV file for writing
    with open(output_file, 'w', newline='') as csvfile:
        fieldnames = ['lon', 'lat']
        writer = csv.DictWriter(csvfile, fieldnames=fieldnames)

        writer.writeheader()  # Write the header row

        # Iterate through each datastream
        for datastream in data:
            # Extract the coordinates from the "observedArea"
            coordinates = datastream.get('observedArea', {}).get('coordinates', [])
            for coord in coordinates:
                lon, lat = coord
                # Write the longitude and latitude to the CSV file
                writer.writerow({'lon': lon, 'lat': lat})


# Example usage
input_file = 'traffic_lights_observations.json'
output_file = 'traffic_lights_coordinates.csv'
process_json(input_file, output_file)

print(f"CSV file '{output_file}' has been created.")
