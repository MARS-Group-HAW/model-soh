import requests
import json
from math import isclose
from concurrent.futures import ThreadPoolExecutor, as_completed

# API base URL
base_url = "https://tld.iot.hamburg.de/v1.0/Datastreams"

# Coordinates to search for
coordinates = [
    (9.976073965573184, 53.56248769775651),
    (9.981474191841663, 53.56009009055772),
]


# Function to check if two coordinates are close enough (less strict than before)
def coordinates_match(coord1, coord2, rel_tol=1e-5, abs_tol=1e-5):
    return isclose(coord1[0], coord2[0], rel_tol=rel_tol, abs_tol=abs_tol) and isclose(coord1[1], coord2[1],
                                                                                       rel_tol=rel_tol, abs_tol=abs_tol)


# Function to check if an element contains the specified coordinates
def contains_coordinates(element, coordinates_set):
    coordinates_data = element.get("observedArea", {}).get("coordinates", [])
    buffer = None  # Temporary storage for a single number

    # Debug: Print out the coordinates in the response
    print(f"Checking element: {element.get('name')}")
    print(f"Observed coordinates: {coordinates_data}")

    for coord in coordinates_data:
        if isinstance(coord, (list, tuple)):
            # Handle nested list of coordinate pairs
            if all(isinstance(c, (list, tuple)) and len(c) == 2 for c in coord):
                print(f"Nested coordinates found: {coord}")
                for sub_coord in coord:
                    lon, lat = sub_coord
                    print(f"Comparing {lon}, {lat} with target coordinates")
                    if any(coordinates_match((lon, lat), target) for target in coordinates_set):
                        return True
            # Handle single coordinate pair
            elif len(coord) == 2:
                lon, lat = coord
                print(f"Comparing {lon}, {lat} with target coordinates")
                if any(coordinates_match((lon, lat), target) for target in coordinates_set):
                    return True
        elif isinstance(coord, (int, float)):
            # Handle consecutive single numbers
            if buffer is None:
                buffer = coord  # Store the first single number
            else:
                # Form a tuple with the buffered number
                lon, lat = buffer, coord
                buffer = None  # Reset the buffer
                print(f"Comparing {lon}, {lat} with target coordinates")
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
    url = f"{base_url}?$filter=properties/serviceName eq 'HH_STA_traffic_lights' and properties/layerName eq 'primary_signal'&$expand=Observations($orderby=phenomenonTime desc;$top=15)&$orderby=id&$top=1000&$skip={skip}"
    response = requests.get(url, headers={"Accept": "application/json"})

    # Debug: Print the status code and raw response for inspection
    print(f"Response status: {response.status_code}")

    if response.status_code != 200:
        print(f"Failed to fetch data: {response.status_code}")
        return []

    data = response.json()

    # Debug: Print the raw JSON response
    print("API Response Data:", json.dumps(data, indent=4))

    elements = data.get("value", [])
    matched_elements = []

    # Check if any element contains the target coordinates and return matches
    for element in elements:
        if contains_coordinates(element, coordinates_set):
            matched_elements.append(element)

    return matched_elements


def main():
    coordinates_set = set(coordinates)
    output_file = "filtered_data.json"
    matched_elements = []
    request_count = 0  # Counter for the number of requests made

    # Using ThreadPoolExecutor to fetch data concurrently
    with ThreadPoolExecutor(max_workers=20) as executor:
        futures = []
        skip = 0
        step = 1000  # Number of items to skip per request

        # Launch multiple fetch tasks
        for i in range(20):  # Start with 5 threads
            futures.append(executor.submit(fetch_and_process, skip, coordinates_set))
            skip += step

        # Wait for threads to complete and collect results
        for future in as_completed(futures):
            result = future.result()
            if result:  # If any result is found, store it
                matched_elements.extend(result)
            request_count += 1  # Increment the request counter after each request

    # After processing all data, save matched elements and print the count
    if matched_elements:
        with open(output_file, "w") as f:
            json.dump(matched_elements, f, indent=4)
        print(f"Matches found: {len(matched_elements)}")
        print(f"Data saved to {output_file}")
    else:
        print("No matches found.")

    print(f"Total number of requests made: {request_count}")
    print(f"Total number of traffic lights processed: {len(matched_elements)}")


if __name__ == "__main__":
    main()
