import argparse
import json

"""
This script converts a JSON Lines (JSONL) file into a standard GeoJSON FeatureCollection.

Purpose:
- The input file contains one GeoJSON feature per line (in JSONL format).
- This format is useful for streaming or line-by-line processing but not directly usable
  in most GIS tools expecting a standard GeoJSON structure.
- The script wraps all individual features into a single `FeatureCollection` object.

Functionality:
1. Load all features from the input JSONL file (one per line).
2. Combine them into a GeoJSON FeatureCollection.
3. Save the resulting object to a .geojson file for use in GIS tools.

Input:
- A JSONL file where each line is a valid GeoJSON feature.

Output:
- A single .geojson file containing all features as a FeatureCollection.

Example:
- input: "truck_lines.jsonl"
- output: "truck_lines.geojson"
"""


def convert_jsonl_to_geojson(input_path, output_path):
    """Converts a JSONL file with GeoJSON features into a standard GeoJSON FeatureCollection."""
    # Load all features from the input file
    with open(input_path, "r", encoding="utf-8") as infile:
        features = [json.loads(line) for line in infile]

    # Create a valid GeoJSON FeatureCollection
    geojson = {
        "type": "FeatureCollection",
        "features": features
    }

    # Save the FeatureCollection to the output file
    with open(output_path, "w", encoding="utf-8") as outfile:
        json.dump(geojson, outfile, indent=2)

    print(f"GeoJSON gespeichert unter: {output_path}")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(
        description="Convert a JSONL file of truck lines to a GeoJSON file.",
        formatter_class=argparse.RawTextHelpFormatter
    )
    
    parser.add_argument(
        "input_path",
        type=str,
        help="Path to the input JSONL file (e.g., input/truck_lines.jsonl)"
    )

    parser.add_argument(
        "output_path",
        type=str,
        help="Path to the output GeoJSON file (e.g., output/truck_lines.geo.json)"
    )

    args = parser.parse_args()

    convert_jsonl_to_geojson(args.input_path, args.output_path)
