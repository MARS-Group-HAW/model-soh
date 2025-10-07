export function extractTimedPositions(geojson) {
    return geojson.features.map((feature, idx) => {
        const coords = feature.geometry.coordinates;
        const truckType = feature.properties?.TruckType || null;

        return {
            id: feature.properties?.id || `truck-${idx}`,
            truckType,
            positions: coords.map(coord => ({
                position: [coord[0], coord[1]],
                timestamp: coord[3]
            }))
        };
    });
}

