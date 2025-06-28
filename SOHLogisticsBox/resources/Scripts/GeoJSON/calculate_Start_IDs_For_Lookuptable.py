import json

"""
This script extracts all unique start edge IDs from a precomputed routes file.

Purpose:
- Each route consists of a list of edge IDs (`edgeIds`).
- The first edge in each route indicates where an agent enters the motorway.
- By collecting these first edges, we can later identify whether an agent has entered
  a precomputed motorway route and use a lookup table accordingly.

Functionality:
1. Load a list of routes from a JSON file.
2. For each route, extract the first edge ID.
3. Ensure uniqueness (no duplicates).
4. Save the list of start edge IDs to a new JSON file.

Input:
- A JSON file where each entry represents a route with an "edgeIds" field

Output:
- A JSON list of unique first edge IDs from each route

Example:
- input: "all_routes.json"
- output: "start_edge_ids.json"
"""


def extract_start_edge_ids(input_path, output_path):
    """Extracts the first edge ID from each route and saves the unique set to a file."""
    with open(input_path, 'r', encoding='utf-8') as infile:
        data = json.load(infile)

    start_edge_ids = []
    seen = set()

    for route in data:
        if 'edgeIds' in route and route['edgeIds']:
            first_edge = route['edgeIds'][0]
            if first_edge not in seen:
                seen.add(first_edge)
                start_edge_ids.append(first_edge)

    with open(output_path, 'w', encoding='utf-8') as outfile:
        json.dump(start_edge_ids, outfile, indent=2)

    print(f"Extracted {len(start_edge_ids)} unique start edge IDs and saved to '{output_path}'.")


if __name__ == "__main__":
    input_path = 'InputDirectory/all_routes.json'           # Path to input file containing routes
    output_path = 'OutputDirectory/start_edge_ids.json'     # Output file with extracted start edge IDs

    extract_start_edge_ids(input_path, output_path)
