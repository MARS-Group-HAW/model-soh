import requests
import json
from concurrent.futures import ThreadPoolExecutor, as_completed

# API base URL
base_url = "https://tld.iot.hamburg.de/v1.0/Datastreams"

# Coordinates to search for
coordinates = [
    (9.976073965573184, 53.56248769775651),
    (9.981474191841663, 53.56009009055772),
]


# Function to check if two coordinates are close enough by comparing up to 4 decimal places
def coordinates_match(coord1, coord2):
    return round(coord1[0], 4) == round(coord2[0], 4) and round(coord1[1], 4) == round(coord2[1], 4)


# Function to check if an element contains the specified coordinates
def contains_coordinates(element, coordinates_set):
    coordinates_data = element.get("observedArea", {}).get("coordinates", [])
    buffer = None  # Temporary storage for a single number

    for coord in coordinates_data:
        if isinstance(coord, (list, tuple)):
            # Handle nested list of coordinate pairs
            if all(isinstance(c, (list, tuple)) and len(c) == 2 for c in coord):
                for sub_coord in coord:
                    lon, lat = sub_coord
                    if any(coordinates_match((lon, lat), target) for target in coordinates_set):
                        return True
            # Handle single coordinate pair
            elif len(coord) == 2:
                lon, lat = coord
                if any(coordinates_match((lon, lat), target) for target in coordinates_set):
                    return True
            else:
                print(f"Skipping unexpected coordinate format: {coord}")
        elif isinstance(coord, (int, float)):
            # Handle consecutive single numbers
            if buffer is None:
                buffer = coord  # Store the first single number
            else:
                # Form a tuple with the buffered number
                lon, lat = buffer, coord
                buffer = None  # Reset the buffer
                if any(coordinates_match((lon, lat), target) for target in coordinates_set):
                    return True
        else:
            print(f"Skipping invalid coordinate: {coord}")

    # If buffer still contains a number, it means the last number didn't have a pair
    if buffer is not None:
        print(f"Skipping unmatched single number: {buffer}")

    return False


# Function to fetch and process data for a specific range of results
def fetch_and_process(skip, coordinates_set):
    url = (f"{base_url}?$filter=properties/serviceName eq 'HH_STA_traffic_lights' and properties/layerName eq "
           f"'primary_signal'&$expand=Observations($orderby=phenomenonTime "
           f"desc;$top=15)&$orderby=id&$top=1000&$skip={skip}")
    response = requests.get(url, headers={"Accept": "application/json"})
    if response.status_code != 200:
        print(f"Failed to fetch data: {response.status_code}")
        return []

    data = response.json()
    elements = data.get("value", [])

    # Check if any element contains the target coordinates and return matches
    matched_elements = []
    for element in elements:
        if contains_coordinates(element, coordinates_set):
            matched_elements.append(element)

    return matched_elements


def main():
    coordinates_set = set(coordinates)
    output_file = "filtered_data.json"
    matched_elements = []
    request_count = 0  # Counter for the number of requests made
    total_traffic_lights = 0  # Counter for the total number of traffic lights processed
    skip = 0
    step = 1000  # Number of items to skip per request

    with ThreadPoolExecutor(max_workers=5) as executor:
        futures = []
        while True:
            # Submit a fetch task for the current skip value
            future = executor.submit(fetch_and_process, skip, coordinates_set)
            futures.append(future)
            skip += step
            request_count += 1

            # Process results of completed futures
            for future in as_completed(futures):
                result = future.result()
                if result:  # Append matched elements if found
                    matched_elements.extend(result)
                    total_traffic_lights += len(result)
                else:  # Stop if no results are returned
                    print("No more results from the API.")
                    break

            # Break the loop if no new elements are fetched
            if not result:
                break

    # Save results and print stats
    if matched_elements:
        with open(output_file, "w") as f:
            json.dump(matched_elements, f, indent=4)
        print(f"Matches found: {len(matched_elements)}")
        print(f"Data saved to {output_file}")
    else:
        print("No matches found.")

    print(f"Total number of requests made: {request_count}")
    print(f"Total number of traffic lights processed: {total_traffic_lights}")


if __name__ == "__main__":
    main()
