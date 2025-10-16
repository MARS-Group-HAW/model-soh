#!/bin/bash

# Usage: run inside /resources

set -e  # Exit on error

# Configuration
CONTAINER_NAME="mars_postgis"
DB_NAME="mars_soh_logistics"
DB_USER="mars_soh_logistics"
DB_PASSWORD="admin"
DB_HOST="127.0.0.1"
DB_PORT="5432"
WORKING_DIR="./output"
DATETIME=$(date +"%Y_%m_%d_%H%M")
OUTPUT_FILE_JSONL="truck_lines_${DATETIME}.jsonl"
OUTPUT_FILE_GEOJSON="truck_lines_${DATETIME}.geo.json"


echo "=== Starting SemiTruckDriver data processing ==="
echo "Timestamp: $(date)"

# Step 1: Drop table semitruckdriver
echo ""
echo "Step 1: Dropping table 'semitruckdriver'..."
PGPASSWORD=$DB_PASSWORD psql -h $DB_HOST -p $DB_PORT -U $DB_USER -d $DB_NAME -c "DROP TABLE IF EXISTS semitruckdriver CASCADE;" || {
     echo "Warning: Failed to drop table. It may not exist yet."
}

# Step 2: Run SOHLogistics
echo ""
echo "Step 2: Running SOHLogistics simulation..."
cd ..

MARS_CONFIG_PATH=config.json dotnet run --configuration Debug --framework net9.0 || {
    echo "Error: Simulation failed!"
    exit 1
}

# Step 3: Create truck_lines.jsonl file from semitruckdriver table
echo ""
echo "Step 3: Creating ${OUTPUT_FILE_JSONL} from 'semitruckdriver' table..."

# Execute the last query from convert_to_geojson.sql
PGPASSWORD=$DB_PASSWORD psql -h $DB_HOST -p $DB_PORT -U $DB_USER -d $DB_NAME -t -A -F"," -c "
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
           )
    FROM semitruckdriver
    GROUP BY id;
" -o "/tmp/$OUTPUT_FILE_JSONL" || {
    echo "Error: Failed to export data from database!"
    exit 1
}

echo "Successfully created ${OUTPUT_FILE_JSONL}"

# Step 4: Copy output file to working directory (if using podman container)
echo ""
echo "Step 4: Copying output file to working directory..."

cp /tmp/"${OUTPUT_FILE_JSONL}" ${WORKING_DIR}/"${OUTPUT_FILE_JSONL}" || {
        echo "Warning: Could not copy from container. File may already be in working directory."
    }
    
echo ""
echo "Step 5: Converting jsonl to geo.json"

python3 resources/Scripts/GEOJSON/convert_JSONL_To_GEOJSON.py ${WORKING_DIR}/"${OUTPUT_FILE_JSONL}" ${WORKING_DIR}/"${OUTPUT_FILE_GEOJSON}"
rm ${WORKING_DIR}/"${OUTPUT_FILE_JSONL}"

echo ""
echo "=== Processing complete ==="
echo "Output file: ${WORKING_DIR}/${OUTPUT_FILE_JSONL}"
echo "Finished at: $(date)"
