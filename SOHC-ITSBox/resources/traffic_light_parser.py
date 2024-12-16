import json
from datetime import datetime, timedelta
import argparse

def parse_traffic_light_data(input_file, output_file):
    with open(input_file, "r") as file:
        data = json.load(file)
    
    traffic_light_data = {}

    for entry in data:
        # Extract coordinates as a string key
        coordinates = tuple(map(tuple, entry["observedArea"]["coordinates"]))
        traffic_light_data[str(coordinates)] = []

        # Extract observations and sort them by time
        observations = entry.get("Observations", [])
        sorted_observations = sorted(
            observations, 
            key=lambda obs: datetime.fromisoformat(obs["phenomenonTime"].replace("Z", "+00:00"))
        )
        
        # Build a per-second phase list
        for i in range(len(sorted_observations) - 1):
            current = sorted_observations[i]
            next_obs = sorted_observations[i + 1]

            start_time = datetime.fromisoformat(current["phenomenonTime"].replace("Z", "+00:00"))
            end_time = datetime.fromisoformat(next_obs["phenomenonTime"].replace("Z", "+00:00"))
            phase = current["result"]

            # Fill seconds with the current phase
            while start_time < end_time:
                traffic_light_data[str(coordinates)].append(phase)
                start_time += timedelta(seconds=1)

    # Save output to a file
    with open(output_file, "w") as outfile:
        json.dump(traffic_light_data, outfile, indent=4)

def main():
    parser = argparse.ArgumentParser(description="Parse traffic light data and extract phases per second.")
    parser.add_argument("input_file", help="Path to the input JSON file containing traffic light data.")
    parser.add_argument("output_file", help="Path to the output JSON file to save parsed traffic light phases.")
    args = parser.parse_args()

    parse_traffic_light_data(args.input_file, args.output_file)
    print(f"Traffic light phases have been saved to {args.output_file}")

if __name__ == "__main__":
    main()
