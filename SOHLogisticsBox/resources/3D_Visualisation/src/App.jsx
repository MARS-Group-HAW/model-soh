import { useEffect, useState } from 'react';
import Stats from 'stats.js';
import { DeckGL } from '@deck.gl/react';
import { MapView } from '@deck.gl/core';
import Map from 'react-map-gl';
import { BitmapLayer } from '@deck.gl/layers';
import { TileLayer } from '@deck.gl/geo-layers';
import { setPauseCoordinates, setGasCoordinates } from './components/Layers';
import Controls from './components/Controls';
import { TruckLayer, TailLayer } from './components/Layers';
import { extractTimedPositions } from './utils/dataUtils';
import { formatTimestampToTime } from './utils/timeUtils';

export default function App() {
    const [trucks, setTrucks] = useState([]);
    const [currentTime, setCurrentTime] = useState(null);
    const [isRunning, setIsRunning] = useState(true);
    const [speedFactor, setSpeedFactor] = useState(1);
    const [initialViewState, setInitialViewState] = useState(null);
    const SHOW_GAS_STATIONS = true;
    const SHOW_PARKING_AREAS = true;
    const SHOW_COLOR_BY_SPEED = true;
    const SHOW_LABELS_PARKING_GAS = true;
    const SHOW_TRAFFIC_JAMS = true;

    const MAPBOX_TOKEN = "pk.eyJ1IjoiZGVubmlzZmlzY2hlcjAxIiwiYSI6ImNtZm51ajJvdDA5NGYya3NiZHdneTU1bjcifQ.K65VKjsWNc9fD-doUKXTpA";


    useEffect(() => {
        const stats = new Stats();
        stats.showPanel(0); // 0: FPS, 1: MS, 2: MB
        document.body.appendChild(stats.dom);

        function animate() {
            stats.begin();
            stats.end();
            requestAnimationFrame(animate);
        }
        requestAnimationFrame(animate);

        return () => {
            document.body.removeChild(stats.dom);
        };
    }, []);

    useEffect(() => {
        fetch('/data/rest_areas.csv')
            .then(r => r.text())
            .then(text => {
                const rows = text.split('\n').slice(1); // Header überspringen
                const coords = rows
                    .map(line => {
                        const [id, lat, lon] = line.split(',');
                        return { lat: parseFloat(lat), lon: parseFloat(lon) };
                    })
                    .filter(c => !isNaN(c.lat) && !isNaN(c.lon));
                setPauseCoordinates(coords);
            });
    }, []);

    // 🆕 Gas stations laden
    useEffect(() => {
        fetch('/data/gas_stations.csv')
            .then(r => r.text())
            .then(text => {
                const rows = text.split('\n').slice(1); // Header überspringen
                const coords = rows
                    .map(line => {
                        const [id, lat, lon] = line.split(',');
                        return { lat: parseFloat(lat), lon: parseFloat(lon) };
                    })
                    .filter(c => !isNaN(c.lat) && !isNaN(c.lon));
                setGasCoordinates(coords);
            });
    }, []);

    // GeoJSON laden
    useEffect(() => {
        fetch('/data/SemiTruckDriver_trips_750.geojson')
            .then(r => r.json())
            .then(data => {
                const trucksData = extractTimedPositions(data);
                setTrucks(trucksData);

                if (trucksData.length > 0 && trucksData[0].positions.length > 0) {
                    setCurrentTime(trucksData[0].positions[0].timestamp);
                    const [lon, lat] = trucksData[0].positions[0].position;
                    setInitialViewState({
                        longitude: lon,
                        latitude: lat,
                        zoom: 13,
                        pitch: 45,
                        bearing: 0
                    });
                }
            })
            .catch(err => console.error('GeoJSON load error:', err));
    }, []);

    // Simulation
    useEffect(() => {
        if (!currentTime || trucks.length === 0 || !isRunning) return;
        const interval = setInterval(() => {
            const lastTime = Math.max(...trucks.map(t => t.positions.at(-1).timestamp));
            setCurrentTime(prev => {
                const next = prev + speedFactor;
                return next >= lastTime ? lastTime : next;
            });
        }, 100);
        return () => clearInterval(interval);
    }, [currentTime, trucks, isRunning, speedFactor]);

    const layers = [
        new TileLayer({
            id: 'osm-tiles',
            data: 'https://tile.openstreetmap.org/{z}/{x}/{y}.png',
            minZoom: 0,
            maxZoom: 19,
            tileSize: 256,
            renderSubLayers: ({ tile, data: image, ...props }) => {
                const bounds = [tile.bbox.west, tile.bbox.south, tile.bbox.east, tile.bbox.north];
                return new BitmapLayer(props, { image, bounds });
            }
        }),
        TailLayer(trucks, currentTime, SHOW_COLOR_BY_SPEED),
        TruckLayer(trucks, currentTime, SHOW_LABELS_PARKING_GAS, SHOW_GAS_STATIONS, SHOW_PARKING_AREAS, SHOW_TRAFFIC_JAMS)
    ];

    const [hoverInfo, setHoverInfo] = useState(null);

    return (
        <div style={{ position: 'relative', height: '100vh' }}>
            {initialViewState && (
                <DeckGL
                    initialViewState={initialViewState}
                    controller
                    layers={[
                        TailLayer(trucks, currentTime, SHOW_COLOR_BY_SPEED),
                        TruckLayer(trucks, currentTime, SHOW_LABELS_PARKING_GAS, SHOW_GAS_STATIONS, SHOW_PARKING_AREAS, SHOW_TRAFFIC_JAMS)
                    ]}
                    views={new MapView({ repeat: true })}
                    onHover={info => setHoverInfo(info.object ? {
                        x: info.x,
                        y: info.y,
                        speed: info.object.speed
                    } : null)}

                >
                    <Map
                        mapStyle="mapbox://styles/mapbox/streets-v12"
                        mapboxAccessToken={MAPBOX_TOKEN}
                        pitch={60}   // Kamera Neigung
                        bearing={-20} // Kamera Richtung
                        onLoad={(event) => {
                            const map = event.target;
                            map.addLayer({
                                id: '3d-buildings',
                                source: 'composite',
                                'source-layer': 'building',
                                filter: ['==', 'extrude', 'true'],
                                type: 'fill-extrusion',
                                minzoom: 15,
                                paint: {
                                    'fill-extrusion-color': '#a7bed3',
                                    'fill-extrusion-height': [
                                        'interpolate',
                                        ['linear'],
                                        ['zoom'],
                                        15, 0,
                                        15.05, ['get', 'height']
                                    ],
                                    'fill-extrusion-base': ['get', 'min_height'],
                                    'fill-extrusion-opacity': 0.6
                                }
                            });
                        }}
                    />

                </DeckGL>
            )}

            <Controls
                isRunning={isRunning}
                setIsRunning={setIsRunning}
                speedFactor={speedFactor}
                setSpeedFactor={setSpeedFactor}
                currentTime={currentTime}
                formatTime={formatTimestampToTime}
            />

            {hoverInfo && (
                <div style={{
                    position: 'absolute',
                    left: hoverInfo.x + 10,
                    top: hoverInfo.y + 10,
                    background: 'rgba(0, 0, 0, 0.7)',
                    color: 'white',
                    padding: '4px 6px',
                    borderRadius: '3px',
                    fontSize: '12px',
                    fontFamily: 'monospace',
                    pointerEvents: 'none',
                    zIndex: 2
                }}>
                    {hoverInfo.speed} km/h
                </div>
            )}


        </div>
    );

}
