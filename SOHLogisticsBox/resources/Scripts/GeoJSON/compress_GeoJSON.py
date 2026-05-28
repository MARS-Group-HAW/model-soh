import json


"""
This script compresses a GeoJSON file by removing unnecessary whitespace and indentation, 
reducing its file size while maintaining its structure and readability for parsers.

Processing steps:
1. Load the original GeoJSON file.
2. Write a new GeoJSON file in a compact format:
   - No indentation or extra spaces.
   - Uses minimal separators (`,` and `:`).
   - Preserves non-ASCII characters.

Use case:
- Optimizes GeoJSON files for storage and faster transmission without altering the data.

Example:
- Input: "autobahn_und_bundesstrassen_deutschland_elevation_04.geojson"
- Output: "autobahn_und_bundesstrassen_deutschland_elevation_05.geojson"
"""

def compact_geojson(input_file, output_file):
    # Read the original GeoJSON file
    with open(input_file, 'r', encoding='utf-8') as f:
        data = json.load(f)

    # Write the compact version without indentation or extra spaces
    with open(output_file, 'w', encoding='utf-8') as f:
        json.dump(data, f, separators=(',', ':'), ensure_ascii=False)


# Example usage
input_geojson = "InputDirectory/autobahn_und_bundesstrassen_deutschland.geojson"  # Replace with your actual input file
output_geojson = 'OutputDirectory/autobahn_und_bundesstrassen_deutschland_compressed.geojson'  # The compact output file

compact_geojson(input_geojson, output_geojson)
