#!/bin/bash

# Usage: run inside /resources

set -e  # Exit on error

# Configuration
if [ -f .env ]; then
  export $(grep -v '^#' .env | xargs)
else
  echo ".env file not found"
  exit 1
fi

WORKING_DIR="./output"
mkdir -p "$WORKING_DIR"
DATETIME=$(date +"%Y_%m_%d_%H%M")
OUTPUT_FILE_GEOJSON="truck_lines_${DATETIME}.geo.json"

echo "=== Starting SemiTruckDriver data processing ==="
echo "Timestamp: $(date)"

# Step 1: Drop table semitruckdriver
echo ""
echo "Step 1: Dropping table '${DB_TABLE}'..."
podman exec -e PGPASSWORD=$DB_PASSWORD $CONTAINER_NAME psql -U $DB_USER -d $DB_NAME -c "DROP TABLE IF EXISTS ${DB_NAME}.${DB_TABLE} CASCADE;" || {
     echo "Warning: Failed to drop table. It may not exist yet."
}

# Step 2: Run SOHTruckFuelBox
echo ""
echo "Step 2: Running SOHTruckFuelBox simulation..."

dotnet run --configuration Debug --framework net9.0 || {
    echo "Error: Simulation failed!"
    exit 1
}

# Step 3 & 4: Create truck_lines.geo.json directly using COPY and podman exec
echo ""
echo "Step 3 & 4: Exporting '${DB_TABLE}' data directly to ${WORKING_DIR}/${OUTPUT_FILE_GEOJSON}..."

# We use COPY with a subquery to generate a single FeatureCollection JSON object
# and write it directly to the output file via STDOUT redirection.

podman exec -e PGPASSWORD=$DB_PASSWORD $CONTAINER_NAME psql -U $DB_USER -d $DB_NAME -t -A -c "
    COPY (
        SELECT jsonb_build_object(
            'type', 'FeatureCollection',
            'features', jsonb_agg(feature)
        )
        FROM (
            SELECT jsonb_build_object(
                       'type', 'Feature',
                       'geometry', jsonb_build_object(
                               'type', 'LineString',
                               'coordinates', jsonb_agg(
                                       jsonb_build_array(x, y, 0, EXTRACT(EPOCH FROM datetime)::int)
                                       ORDER BY tick
                                              )
                                   ),
                       'properties', jsonb_build_object(
                               'id', id
                                     )
               ) AS feature
            FROM ${DB_NAME}.${DB_TABLE}
            GROUP BY id
        ) features_subquery
    ) TO STDOUT;
" > "${WORKING_DIR}/${OUTPUT_FILE_GEOJSON}" || {
    echo "Error: Failed to export data from database!"
    exit 1
}

echo "Successfully created ${WORKING_DIR}/${OUTPUT_FILE_GEOJSON}"

# echo ""
# echo "Step 4: Copying output file to working directory..."
# 
# cp /tmp/"${OUTPUT_FILE_JSONL}" ${WORKING_DIR}/"${OUTPUT_FILE_JSONL}" || {
#         echo "Warning: Could not copy from container. File may already be in working directory."
#     }
#     
# echo ""
# echo "Step 5: Converting jsonl to geo.json"
# 
# python3 resources/Scripts/GEOJSON/convert_JSONL_To_GEOJSON.py ${WORKING_DIR}/"${OUTPUT_FILE_JSONL}" ${WORKING_DIR}/"${OUTPUT_FILE_GEOJSON}"
# rm ${WORKING_DIR}/"${OUTPUT_FILE_JSONL}"

echo ""
echo "=== Processing complete ==="
echo "Output file: ${WORKING_DIR}/${OUTPUT_FILE_GEOJSON}"
echo "Finished at: $(date)"
