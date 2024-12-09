import requests
import json
from shapely.geometry import Point, Polygon
from concurrent.futures import ThreadPoolExecutor, as_completed
from tqdm import tqdm

base_url = "https://tld.iot.hamburg.de/v1.0/Datastreams"

polygon_coords = [
    (9.967878591161167, 53.54190162843912),
    (9.995227259170974, 53.54190162843912),
    (9.995227259170974, 53.566197397982364),
    (9.967878591161167, 53.566197397982364),
    (9.967878591161167, 53.54190162843912)
]

polygon = Polygon(polygon_coords)


def is_within_area(lon, lat, polygon):
    point = Point(lon, lat)
    return polygon.contains(point)


def fetch_and_process(skip):
    url = f"{base_url}?$filter=properties/serviceName eq 'HH_STA_traffic_lights' and properties/layerName eq 'primary_signal'&$expand=Observations($orderby=phenomenonTime desc;$top=15)&$orderby=id&$top=1000&$skip={skip}"
    response = requests.get(url, headers={"Accept": "application/json"})

    if response.status_code != 200:
        print(f"Failed to fetch data: {response.status_code}")
        return []

    data = response.json()

    elements = data.get("value", [])
    matched_elements = []

    for element in elements:
        coordinates_data = element.get("observedArea", {}).get("coordinates", [])
        for coord in coordinates_data:
            if isinstance(coord, (list, tuple)) and len(coord) == 2:
                lon, lat = coord
                if is_within_area(lon, lat, polygon):
                    matched_elements.append(element)
                    break

    return matched_elements


def main():
    output_file = "traffic_lights_observations.json"
    matched_elements = []
    request_count = 0

    with ThreadPoolExecutor(max_workers=20) as executor:
        futures = []
        skip = 0
        step = 1000

        for i in range(20):
            futures.append(executor.submit(fetch_and_process, skip))
            skip += step

        for future in tqdm(as_completed(futures), total=len(futures), desc="Fetching data"):
            result = future.result()
            if result:
                matched_elements.extend(result)
            request_count += 1

    if matched_elements:
        with open(output_file, "w") as f:
            json.dump(matched_elements, f, indent=4)
        print(f"Matches found: {len(matched_elements)}")
        print(f"Data saved to {output_file}")
    else:
        print("No matches found.")


if __name__ == "__main__":
    main()
