import pandas as pd
import numpy as np
import geopandas as gpd
from shapely.geometry import Point
import random
from geopy.distance import geodesic
from collections import defaultdict

"""
This script generates realistic truck routes across Germany based on transport data from the 
German Federal Motor Transport Authority (KBA).

Data sources:
- https://www.kba.de/DE/Statistik/Forschungsdatenzentrum/Datenangebot/Verkehr_europaeischer_Lastkraftfahrzeuge/verkehr_europaeischer_Lkw_node.html

Input files:
1. trucks_per_area.csv (Processed truck transport data)
   - Contains the number of trucks per NUTS3 region, categorized by:
     - Weight classes (1-9)
     - Distance classes (1-21)

2. NUTS3 shapefile (NUTS_RG_01M_2024_4326.shp)
   - Contains geographic boundaries for NUTS3 regions in Germany.

Processing steps:
1. Load NUTS3 boundaries and filter for German regions.
2. Precompute valid destination regions for each NUTS3 area based on distance classes.
3. For each truck entry:
   - Determine the truck type based on weight class.
   - Randomly select a valid starting location within the NUTS3 region.
   - Select a destination region within the given distance class.
   - Generate a valid destination point within the target NUTS3 region.
4. Save generated routes to an output CSV file.

Output file:
- truck_routes.csv
  - Contains the generated truck routes with columns:
    - TruckType: Type of truck based on weight class.
    - StartLat, StartLon: Coordinates of the departure location.
    - DestLat, DestLon: Coordinates of the destination.
    - DriveMode: Fixed value (3), representing shortestRoute.

This dataset can be used for route optimization, traffic simulations, and logistics planning.
"""


class TruckRouteGenerator:
    def __init__(self, input_file, output_file, shapefile):
        self.input_file = input_file
        self.output_file = output_file
        self.shapefile = shapefile  # NUTS3 boundary file

        # Define truck types corresponding to weight classes
        self.truck_types = {
            1: "SmallTruck",
            2: "MediumLoadTruck",
            3: "HeavyLoadTruck",
            4: "ExtendedLoadTruck",
            5: "LargeCapacityTruck",
            6: "ExtraCapacityTruck",
            7: "HighVolumeTruck",
            8: "MaximumLoadTruck",
            9: "OverloadTruck"
        }

        # Define distance class ranges (in km)
        self.distance_classes = {
            1: (0, 50),
            2: (50, 100),
            3: (100, 150),
            4: (150, 200),
            5: (200, 250),
            6: (250, 300),
            7: (300, 350),
            8: (350, 400),
            9: (400, 450),
            10: (450, 500),
            11: (500, 550),
            12: (550, 600),
            13: (600, 650),
            14: (650, 700),
            15: (700, 750),
            16: (750, 800),
            17: (800, 850),
            18: (850, 900),
            19: (900, 950),
            20: (950, 1000),
            21: (1000, 1200)  # Max possible distance in Germany
        }

        # Load NUTS3 region boundaries
        self.nuts3_data = self.load_nuts3_boundaries()

        # Precompute distance-based area mappings
        self.precomputed_area_distances = self.precompute_area_distances()

        # Generate truck routes
        self.generate_routes()

    def load_nuts3_boundaries(self):
        """Load NUTS3 shapefile and filter only German regions."""
        gdf = gpd.read_file(self.shapefile)

        # Check available columns
        print("Available columns:", gdf.columns)

        # Select correct country column
        if "CNTR_CODE" in gdf.columns:
            country_column = "CNTR_CODE"
        elif "NUTS_ID" in gdf.columns:
            country_column = "NUTS_ID"
        else:
            raise KeyError("Could not find a column for country filtering!")

        # Filter Germany (DE)
        gdf = gdf[gdf[country_column].str.startswith("DE")]

        return gdf

    def precompute_area_distances(self):
        """Precompute distance-based area mappings for efficient lookups."""
        area_centroids = {row["NUTS_ID"]: row.geometry.centroid for _, row in self.nuts3_data.iterrows()}
        area_distances = defaultdict(lambda: defaultdict(list))

        for area1, centroid1 in area_centroids.items():
            for area2, centroid2 in area_centroids.items():
                if area1 != area2:
                    distance_km = geodesic((centroid1.y, centroid1.x), (centroid2.y, centroid2.x)).km
                    for dist_class, (min_dist, max_dist) in self.distance_classes.items():
                        if min_dist <= distance_km <= max_dist:
                            area_distances[area1][dist_class].append(area2)

        return area_distances

    def get_random_point_in_nuts3(self, nuts3_code):
        """Generate a random point inside the NUTS3 polygon."""
        area = self.nuts3_data[self.nuts3_data["NUTS_ID"] == nuts3_code]
        if area.empty:
            return None, None

        polygon = area.iloc[0].geometry
        minx, miny, maxx, maxy = polygon.bounds

        for _ in range(10):
            point = Point(random.uniform(minx, maxx), random.uniform(miny, maxy))
            if polygon.contains(point):
                return point.x, point.y

        return None, None

    def generate_routes(self):
        """Generate truck routes with valid start and destination coordinates."""
        df = pd.read_csv(self.input_file, delimiter=";", encoding="utf-8")

        required_columns = ["BE_NUTS3", "FT_ENTF21", "NULAGX", "FT_I"]
        for col in required_columns:
            if col not in df.columns:
                raise KeyError(f"Column '{col}' not found in {self.input_file}")

        df["FT_I"] = df["FT_I"].astype(int)

        routes = []
        for _, row in df.iterrows():
            truck_type = self.truck_types.get(row["NULAGX"], "UnknownTruck")
            num_trucks = row["FT_I"]
            distance_class = row["FT_ENTF21"]
            start_area = row["BE_NUTS3"]

            for _ in range(num_trucks):
                start_lon, start_lat = self.get_random_point_in_nuts3(start_area)
                if start_lon is None or start_lat is None:
                    continue  # Skip invalid start locations

                # Get a valid destination area based on precomputed distances
                possible_dest_areas = self.precomputed_area_distances[start_area].get(distance_class, [])

                if possible_dest_areas:
                    dest_area = random.choice(possible_dest_areas)  # ✅ Randomly select a valid destination
                else:
                    # 🚨 No areas found in this distance class → Pick the farthest available area!
                    max_distance = max(self.precomputed_area_distances[start_area].keys(), default=None)
                    if max_distance:
                        possible_dest_areas = self.precomputed_area_distances[start_area].get(max_distance, [])
                        if possible_dest_areas:
                            dest_area = random.choice(possible_dest_areas)
                        else:
                            continue  # 🚨 No valid target at all → Skip this truck
                    else:
                        continue  # No valid targets at all

                # Generate destination point
                dest_lon, dest_lat = self.get_random_point_in_nuts3(dest_area)
                if dest_lon is None or dest_lat is None:
                    continue  # Skip if invalid

                routes.append([truck_type, start_lat, start_lon, dest_lat, dest_lon, 3])

        df_routes = pd.DataFrame(routes, columns=["TruckType", "StartLat", "StartLon", "DestLat", "DestLon", "DriveMode"])
        df_routes.to_csv(self.output_file, sep=";", index=False, encoding="utf-8")

        print(f"Generated truck routes saved to: {self.output_file}")

# Example Usage
input_file_path = "SOHLogisticsBox/resources/Scripts/SemiTruckData/trucks_per_area.csv"
output_file_path = "SOHLogisticsBox/resources/Scripts/SemiTruckData/truck_routes.csv"
shapefile_path = "SOHLogisticsBox/resources/Scripts/SemiTruckData/resources/NUTS_RG_01M_2024_4326.shp"

# Running the truck route generator
TruckRouteGenerator(input_file_path, output_file_path, shapefile_path)
