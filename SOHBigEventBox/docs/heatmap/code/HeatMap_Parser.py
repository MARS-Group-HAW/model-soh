import json
import folium
from folium.plugins import HeatMap
from datetime import datetime, timedelta
import pytz

# Function to load the GeoJSON file
def load_geojson(file_path):
    """
    Load GeoJSON data from a file.

    :param file_path: Path to the GeoJSON file.
    :return: Parsed JSON data.
    """
    with open(file_path, 'r') as f:
        return json.load(f)

# Function to convert Unix timestamps to datetime
def convert_timestamp_to_datetime(timestamp):
    """
    Convert a Unix timestamp to a timezone-aware datetime object.

    :param timestamp: Unix timestamp (seconds since epoch).
    :return: A datetime object in UTC timezone.
    """
    return datetime.utcfromtimestamp(timestamp).replace(tzinfo=pytz.UTC)

# Function to group timestamps into specific time intervals (e.g., 10 minutes)
def group_by_time_interval(features, interval_minutes=10):
    """
    Group coordinate points into time intervals.

    :param features: List of features from the GeoJSON file.
    :param interval_minutes: Interval size in minutes (default: 10 minutes).
    :return: Dictionary where keys are time intervals and values are coordinate lists.
    """
    time_groups = {}

    for feature in features:
        coordinates = feature['geometry']['coordinates']
        for coord in coordinates:
            timestamp = coord[3]  # The fourth element in the coordinates represents the timestamp
            dt = convert_timestamp_to_datetime(timestamp)

            # Round down to the nearest interval
            interval_time = dt.replace(minute=(dt.minute // interval_minutes) * interval_minutes,
                                       second=0, microsecond=0)

            # Add coordinates to the appropriate interval group
            if interval_time not in time_groups:
                time_groups[interval_time] = []
            time_groups[interval_time].append((coord[1], coord[0]))  # (latitude, longitude)

    return time_groups

# Function to create a time-dependent heatmap
def create_time_dependent_heatmap(time_groups, map_center=[53.5833, 9.9124], zoom_start=13):
    """
    Create a heatmap with layers for each time interval.

    :param time_groups: Dictionary with time intervals as keys and coordinates as values.
    :param map_center: The center of the map [latitude, longitude].
        Default: [53.5833, 9.9124] (specific to the Barclays Arena simulation).
        Update this value if running the simulation for a different area.
    :param zoom_start: Initial zoom level of the map (default: 13).
    :return: None (saves the map as an HTML file).
    """
    m = folium.Map(location=map_center, zoom_start=zoom_start)

    for interval_time, coordinates in sorted(time_groups.items()):
        if coordinates:
            # Layer description with time interval
            layer_name = interval_time.strftime('%Y-%m-%d %H:%M')

            # HeatMap with customized parameters for reduced thresholds
            heatmap_layer = folium.FeatureGroup(name=f"Heatmap {layer_name}")
            HeatMap(
                coordinates,
                radius=5,             # Smaller radius for points
                min_opacity=0.75,     # Lower threshold for visibility
                blur=5,               # Reduced blur for sharper points
                max_zoom=15           # Maximum zoom level for the heatmap
            ).add_to(heatmap_layer)

            heatmap_layer.add_to(m)

    # Enable toggling between different heatmap layers
    folium.LayerControl().add_to(m)

    # Save the map to an HTML file
    m.save('time_dependent_heatmap.html')
    print("Heatmap has been created and saved as 'time_dependent_heatmap.html'.")

# Main function to generate the heatmap
def generate_time_dependent_heatmap(file_path):
    """
    Generate a time-dependent heatmap from a GeoJSON file.

    :param file_path: Path to the GeoJSON file containing visitor trip data.
        Update this path if the input file changes.
    :return: None
    """
    geojson_data = load_geojson(file_path)
    features = geojson_data['features']

    # Group the coordinates by time intervals
    time_groups = group_by_time_interval(features)

    # Debug: Print all identified time intervals and the number of points in each
    print("Detected time intervals:")
    for time_interval in sorted(time_groups.keys()):
        print(time_interval, ":", len(time_groups[time_interval]), "points")

    # Create the time-dependent heatmap
    create_time_dependent_heatmap(time_groups)

# Example call, change the file path as needed
generate_time_dependent_heatmap('resources/Visitor_trips.geojson')