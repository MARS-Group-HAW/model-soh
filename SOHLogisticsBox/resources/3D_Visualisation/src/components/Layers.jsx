import { PathLayer, TextLayer, IconLayer } from '@deck.gl/layers';
import { ScenegraphLayer } from '@deck.gl/mesh-layers';
import { Matrix4 } from '@math.gl/core';
import { getHeading, getSpeed, getColorFromSpeed } from '../utils/geoUtils';
import { formatTimestampToTime } from '../utils/timeUtils';

// ------------------ Helpers ------------------
const trunc5 = (n) => Math.trunc(n * 1e5) / 1e5;
const keyLatLon = (lat, lon) => `${trunc5(lat).toFixed(5)},${trunc5(lon).toFixed(5)}`;

let pauseCoords = new Set();
let gasCoords = new Set();

export function setPauseCoordinates(coordsArray) {
    pauseCoords = new Set(coordsArray.map(c => keyLatLon(c.lat, c.lon)));
}
export function setGasCoordinates(coordsArray) {
    gasCoords = new Set(coordsArray.map(c => keyLatLon(c.lat, c.lon)));
}

const MODEL_FIX = new Matrix4();

function getUniqueStatusPositions(points, statusType) {
    const uniqueMap = new Map();
    points
        .filter(p => p.status === statusType)
        .forEach(p => {
            const key = keyLatLon(p.position[1], p.position[0]);
            if (!uniqueMap.has(key)) uniqueMap.set(key, p.position);
        });
    return Array.from(uniqueMap.values());
}

function getDistanceMeters(pos1, pos2) {
    const R = 6371000;
    const lat1 = pos1[1] * Math.PI / 180;
    const lat2 = pos2[1] * Math.PI / 180;
    const dLat = (pos2[1] - pos1[1]) * Math.PI / 180;
    const dLon = (pos2[0] - pos1[0]) * Math.PI / 180;

    const a = Math.sin(dLat / 2) ** 2 +
        Math.cos(lat1) * Math.cos(lat2) *
        Math.sin(dLon / 2) ** 2;
    const c = 2 * Math.atan2(Math.sqrt(a), Math.sqrt(1 - a));
    return R * c;
}
const withinRadius = (a, b, r) => getDistanceMeters(a, b) <= r;

// ------------------ Stau-Parameter ------------------
const JAM = {
    radiusMeters: 120,
    speedThresholdKmh: 5,
    minVehicles: 3
};

// Cluster-Erkennung
function detectTrafficJams(points) {
    const truckMap = new Map();
    points.forEach(p => {
        if (p.speed < JAM.speedThresholdKmh) {
            if (!truckMap.has(p.id)) {
                truckMap.set(p.id, p);
            }
        }
    });
    const eligible = Array.from(truckMap.values());
    if (eligible.length === 0) return [];

    const clusters = [];
    const visited = new Set();

    for (let i = 0; i < eligible.length; i++) {
        const a = eligible[i];
        if (visited.has(a.id)) continue;

        const queue = [a];
        const cluster = [];
        visited.add(a.id);

        while (queue.length) {
            const cur = queue.shift();
            cluster.push(cur);

            for (let j = 0; j < eligible.length; j++) {
                const cand = eligible[j];
                if (visited.has(cand.id)) continue;
                if (withinRadius(cur.position, cand.position, JAM.radiusMeters)) {
                    visited.add(cand.id);
                    queue.push(cand);
                }
            }
        }
        clusters.push(cluster);
    }

    return clusters
        .filter(c => c.length >= JAM.minVehicles)
        .map(c => {
            const avgLon = c.reduce((s, p) => s + p.position[0], 0) / c.length;
            const avgLat = c.reduce((s, p) => s + p.position[1], 0) / c.length;
            return { position: [avgLon, avgLat], size: c.length };
        });
}

const VALID_TRUCK_TYPES = new Set([
    "SmallTruck",
    "MediumLoadTruck",
    "HeavyLoadTruck",
    "ExtendedLoadTruck",
    "LargeCapacityTruck",
    "ExtraCapacityTruck",
    "HighVolumeTruck",
    "MaximumLoadTruck",
    "OverloadTruck"
]);

// ------------------ Haupt-Layer ------------------
export function TruckLayer(trucks, currentTime, showLabels = true, showGas = true, showParking = true, showJams = true) {
    const lastAngles = new Map(); // speichert letzte Fahrtrichtung pro Truck

    const currentPoints = trucks.map(truck => {
        let point = null;

        for (let i = 0; i < truck.positions.length - 1; i++) {
            const p1 = truck.positions[i];
            const p2 = truck.positions[i + 1];

            if (p1.timestamp <= currentTime && currentTime <= p2.timestamp) {
                const duration = p2.timestamp - p1.timestamp;
                const elapsed = currentTime - p1.timestamp;
                const samePos = p1.position[0] === p2.position[0] && p1.position[1] === p2.position[1];
                const ratio = duration > 0 ? elapsed / duration : 0;

                const lon = samePos ? p1.position[0] : p1.position[0] + (p2.position[0] - p1.position[0]) * ratio;
                const lat = samePos ? p1.position[1] : p1.position[1] + (p2.position[1] - p1.position[1]) * ratio;

                // 🚀 Richtungslogik
                let angle;
                if (samePos) {
                    angle = lastAngles.get(truck.id) ?? 0; // behalte letzte Richtung
                } else {
                    angle = getHeading(p1.position, p2.position);
                    lastAngles.set(truck.id, angle);
                }

                const speed = samePos ? 0 : getSpeed(p1.position, p2.position, p1.timestamp, p2.timestamp);

                // 🚀 Pause/Gas Status
                let status = null;
                let pauseUntil = null;
                let pauseRemaining = null;

                if (samePos) {
                    const key = keyLatLon(lat, lon);
                    let j = i + 1;
                    while (
                        j < truck.positions.length &&
                        truck.positions[j].position[0] === p1.position[0] &&
                        truck.positions[j].position[1] === p1.position[1]
                        ) {
                        j++;
                    }

                    const hasMultiStepStop = j > i + 1;
                    if (hasMultiStepStop) {
                        pauseUntil = truck.positions[j - 1].timestamp;
                        pauseRemaining = pauseUntil - currentTime;

                        if (pauseCoords.has(key)) {
                            status = 'pause';
                            if (pauseRemaining < 600 && gasCoords.has(key)) {
                                status = 'gas';
                            }
                        } else if (gasCoords.has(key)) {
                            status = 'gas';
                        }
                    }
                }

                point = {
                    id: truck.id,
                    truckType: truck.truckType,
                    position: [lon, lat],
                    angle,
                    speed,
                    timestamp: currentTime,
                    status,
                    pauseUntil,
                    pauseRemaining
                };

                break;
            }
        }
        if (!point) return null;
        return point;
    }).filter(Boolean);

    // --- Truck Layer pro Typ ---
    const truckLayers = [];
    const trucksByType = new Map();

    currentPoints.forEach(p => {
        const type = VALID_TRUCK_TYPES.has(p.truckType) ? p.truckType : "DEFAULT";
        if (!trucksByType.has(type)) {
            trucksByType.set(type, []);
        }
        trucksByType.get(type).push(p);
    });

    trucksByType.forEach((points, type) => {
        let modelPath;
        if (type === "DEFAULT") {
            console.warn("Truck ohne gültigen TruckType gefunden – verwende truck.glb");
            modelPath = "/models/truck.glb";
        } else {
            modelPath = `/models/${type}.glb`;
        }

        truckLayers.push(new ScenegraphLayer({
            id: `truck-3d-${type}`,
            data: points,
            scenegraph: modelPath,
            getPosition: d => [d.position[0], d.position[1], 3],
            getOrientation: d => {
                if (d.speed === 0) {
                    // Stillstand → kein +90 Offset
                    return [90, d.angle, 90];
                }
                // Fahrt → mit +90 Offset
                return [90, d.angle + 90, 90];
            },
            sizeScale: 5,
            pickable: true,
            _lighting: 'pbr',
            getModelMatrix: () => MODEL_FIX
        }));
    });

    // --- Gas Stations ---
    let gasLayer = null;
    if (showGas) {
        const uniqueGasPositions = getUniqueStatusPositions(currentPoints, 'gas');
        gasLayer = new ScenegraphLayer({
            id: 'gas-station-3d',
            data: uniqueGasPositions,
            scenegraph: '/models/Gas_station.glb',
            getPosition: pos => [pos[0] + 0.0003, pos[1], 3],
            getOrientation: [0, -45, 0],
            sizeScale: 1,
            pickable: false,
            _lighting: 'pbr',
            getModelMatrix: () => MODEL_FIX
        });
    }

    // --- Parking ---
    let parkingLayer = null;
    if (showParking) {
        const uniquePausePositions = getUniqueStatusPositions(currentPoints, 'pause');
        parkingLayer = new ScenegraphLayer({
            id: 'parking-3d',
            data: uniquePausePositions,
            scenegraph: '/models/Parking.glb',
            getPosition: pos => [pos[0] - 0.0003, pos[1], 2.5],
            getOrientation: [0, 90, 0],
            sizeScale: 5,
            pickable: false,
            _lighting: 'pbr',
            getModelMatrix: () => MODEL_FIX
        });
    }

    // --- Traffic Jams ---
    let jamLayer = null;
    if (showJams) {
        const jamClusters = detectTrafficJams(currentPoints);
        jamLayer = new IconLayer({
            id: 'traffic-jam-icons',
            data: jamClusters,
            iconAtlas: '/symbols/Stau.png',
            iconMapping: { stau: { x: 0, y: 0, width: 4000, height: 4000, mask: false } },
            getIcon: () => 'stau',
            getPosition: d => d.position,
            getSize: () => 64,
            sizeUnits: 'pixels',
            billboard: true,
            pickable: false
        });
    }

    // --- Labels ---
    let textLayer = null;
    if (showLabels) {
        const labelPoints = currentPoints.filter(d => d.status);
        textLayer = new TextLayer({
            id: 'truck-status-labels',
            data: labelPoints,
            getPosition: d => [d.position[0], d.position[1], 10],
            getText: d => {
                if (d.status === 'pause' && d.pauseRemaining > 0) {
                    return `Macht Pause bis ${formatTimestampToTime(d.pauseUntil)}\n(${Math.ceil(d.pauseRemaining / 60)} min uebrig)`;
                }
                if (d.status === 'gas') return `Tankt gerade`;
                return '';
            },
            getSize: 16,
            getColor: [0, 0, 0, 255],
            background: true,
            getBackgroundColor: [255, 255, 255, 0],
            fontFamily: 'monospace',
            sizeUnits: 'pixels',
            billboard: true
        });
    }

    return [
        ...truckLayers,
        ...(gasLayer ? [gasLayer] : []),
        ...(parkingLayer ? [parkingLayer] : []),
        ...(jamLayer ? [jamLayer] : []),
        ...(textLayer ? [textLayer] : [])
    ];
}


// ------------------ TailLayer ------------------
export function TailLayer(trucks, currentTime, colorBySpeed = true) {
    if (!colorBySpeed) return null;

    const tailLength = 30;
    const tailData = trucks.flatMap(truck => {
        const points = [];
        const idx = truck.positions.findIndex(p => p.timestamp === currentTime);
        const currIdx = idx >= 0 ? idx : truck.positions.findIndex(p => p.timestamp > currentTime);
        if (currIdx > 0) {
            const startIdx = Math.max(0, currIdx - tailLength);
            for (let i = startIdx; i < currIdx; i++) {
                const p1 = truck.positions[i];
                const p2 = truck.positions[i + 1];
                const speed = getSpeed(p1.position, p2.position, p1.timestamp, p2.timestamp);
                const alpha = Math.floor(255 * ((i - startIdx + 1) / tailLength));

                points.push({
                    id: truck.id,
                    path: [p1.position, p2.position],
                    color: [...getColorFromSpeed(speed), alpha]
                });
            }
        }
        return points;
    });

    return new PathLayer({
        id: 'truck-tails',
        data: tailData,
        getPath: d => d.path,
        getColor: d => d.color,
        widthUnits: 'pixels',
        getWidth: 4,
        opacity: 1,
        pickable: false
    });
}
