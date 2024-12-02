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


# Function to check if two coordinates are close enough
def coordinates_match(coord1, coord2, rel_tol=1e-4):
    return isclose(coord1[0], coord2[0], rel_tol=rel_tol) and isclose(coord1[1], coord2[1], rel_tol=rel_tol)


# Function to check if an element contains the specified coordinates
def contains_coordinates(element, coordinates_set):
    # Access the coordinates inside "observedArea.coordinates"
    coordinates_data = element.get("observedArea", {}).get("coordinates", [])

    for coord in coordinates_data:
        # Ensure that coord is a list or tuple with two elements (lon, lat)
        if isinstance(coord, (list, tuple)) and len(coord) == 2:
            lon, lat = coord
            if any(coordinates_match((lon, lat), coord) for coord in coordinates_set):
                return True
        else:
            print(f"Skipping invalid coordinate: {coord}")

    return False


# Function to fetch and process data for a specific range of results
def fetch_and_process(skip, coordinates_set):
    url = f"{base_url}?$filter=properties/serviceName eq 'HH_STA_traffic_lights' and properties/layerName eq 'primary_signal'&$expand=Observations($orderby=phenomenonTime desc;$top=15)&$orderby=id&$top=1000&$skip={skip}"
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

    # Using ThreadPoolExecutor to fetch data concurrently
    with ThreadPoolExecutor(max_workers=5) as executor:
        futures = []
        skip = 0
        step = 1000  # Number of items to skip per request

        # Launch multiple fetch tasks
        for i in range(5):  # Start with 5 threads
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


if __name__ == "__main__":
    main()
