# CasablancaBox 🚍

Casablanca citizens simulation that runs the SOH model in four modes:
> * **Bus** – `BusDriver` + `PassengerTraveler` on `CarDriving` + `Walking` graphs
> * **Tram** – `TramDriver` + `PassengerTraveler` on `Tram` track + `Walking` 
> * **Bicycle** – `CycleTraveler` on `Cycling` graphs
> * **Walk** – `HumanTraveler`s on `Walking` only

---
## Area of Interest
The following image illustrate the area of interst that was chosen for the scenario.
![OverviewMap](sidi_maarouf.png)

## Active Agents and Entities

| Agent or Entity       | Responsibility                                                                                                                                                     |
|-----------------------|--------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| ``Citizen``           | Models an individual citizen’s daily life, making mobility decisions (home, work, errands, free time) and moving through the environment via multimodal transport. |
| ``TramDriver``&&``BusDriver``      | Agent operating a single Tram/Bus, moving it between stations according to TramRouteLayer schedules.                                                               |
| ``Bus``               | Models a bus as a road-based vehicle, connected to its operating layer (BusLayer), aware of stations, and supporting passenger and steering interactions.          |
| ``Bicycle``           | Models the physical and functional aspects of a bicycle in the multimodal environment, including weight, type, mass, and integration with parking and steering.    |
| ``Tram``              | Models a tram as a rail-bound, non-colliding vehicle in the multimodal simulation, integrated with stations, steering, and passenger management.                   |

##  Active Layers

| Layer                                         | Responsibility                                                                                                                                                                  |
|-----------------------------------------------|---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| ``SpatialGraphMediatorLayer``                 | A multimodal, lane-resolved spatial graph layer enabling agent traversal across edge lanes under modality-specific constraints.                                                 |
| ``VectorBuildingsLayer``                      | A vector layer containing building Polygon/MultiPolygon geometries annotated with service types, enabling mapping to TripReason activities.                                     |
| ``VectorPoiLayer``                            | A vector layer of Point geometries for relevant services, enabling mapping to TripReason activities.                                                                            |
| ``VectorLandUseLayer``                        | A vector layer containing Polygon/MultiPolygon geometries that represent service areas, enabling mapping to TripReason activities.                                              |
| ``MediatorLayer``                             | An aggregating layer wrapping multiple vector layers, such as VectorPoiLayer, enabling the retrieval of subsequent travel goals conditioned on a specified TripReason activity. |
| ``TramLayer``&&``BusLayer``                   | Manages trams/busses (drivers) within the spatial environment and connects them to the route layer.                                                                             |
| ``TramSchedulerLayer``&&``BusSchedulerLayer`` | Responsible for time-based scheduling and deployment of trams into the simulation, linked with the TramLayer.                                                                   |
| ``TramStationLayer``&&``BusStationLayer``     | Manages tram/bus station data and enables querying stations spatially or by ID.                                                                                                 |
| ``TramRouteLayer`` && ``BusRouteLayer``                        | Organizes tram/bus routes, connects them to stations, and provides lookup and management for routing.                                                                           |
| ``BicycleRentalLayer``                              | Vector layer that delineates parking-lot areas and individual stalls, enabling cars to reserve and occupy spaces in the simulation.                                                                                                                                                               |
| ``CitizenLayer``                              | Holds and manages all citizens as simulation agents, wiring them with dependencies for multimodal decision-making.                                                              |
| ``CitizenSchedulerLayer``                     | Schedules and spawns citizens into the simulation with attributes and starting positions, according to timetable and input data.                                                |

## Requirements
* .NET SDK 8.0+
* The SOHModel project available and referenced.
* GeoJSON/CSV resources under a `resources/` directory (see structure below).
* **Legacy GTFS 1.7.1 NuGet package**: If you keep this package, it will emit an `NU1701` warning because it targets .NET Framework. Remove it if you don’t use GTFS, or suppress the warning.

---
## Project Layout
*  **Program.cs**             # Entry point for the simulation
* **CasablancaBox.csproj**    # .NET project file
* README.md               # Project documentation
* sidi_maarouf.png        # AOI overview map (Sidi Maârouf)
* **resources/**              # Needed csv and Geojson files
* **config_citizen.json**     # Run config

Warning: Ensure all resources and configs are copied to the build output by adding this to your CasablancaBox.csproj!

---
## Configuration (`config_citizen.json`)
This scenario config defines a **3-hour morning window** for Casablanca citizens with **second-level** simulation ticks and CSV output to `results/`. It wires up multimodal networks (walk, road/cycle, tram track), agent capabilities, schedules, and output formats.

### Run metadata
- **Id:** `casa_citizen`
- **Time window:** `2025-01-01 07:00:00` → `10:00:00`
- **Tick:** `deltaT = 1 second`
- **Console logging:** on
- **Output:** CSV to `results/` (see *Outputs* below).

### Agents
- **Citizen** (`count: 1000`)
    - **Init file:** `resources/citizen_init_10k.csv`
    - **Capabilities:** Bus, Tram, Cycling (plus Walking by default)
    - **Per-agent options:** trajectories enabled (`ResultTrajectoryEnabled = true`)
    - **Outputs:**
        - `trips` with fields: `StableId`, `ActiveCapability`, `RouteMainModality`, `RouteModalities`
        - `default` telemetry, **filtered** to only store ticks where `StoreTickResult == true`.
- **BusDriver**
    - **Trip output:** suffixed `BusDriver`, fields: `StableId`, `Line`, `PassengerAmount` (+ `default`). 
- **TramDriver**
    - **Trip output:** suffixed `TramDriver`, fields: `StableId`, `Line`, `PassengerAmount` (+ `default`).

### Layers (data & networks)
- **Static context:** `VectorBuildingsLayer`, `VectorLanduseLayer`, `VectorPoiLayer` (GeoJSON). 
- **Spatial graph (multimodal mediator):**
    - **Walking:** `casa_sidi_maarouf_walk_graph.geojson`
    - **Tram track:** `casablanca_T1_graph.geojson` (modality: `TrainDriving`)
    - **Road/cycle:** `casa_sidi_maarouf_drive_graph.geojson` (modalities: `CarDriving`, `Cycling`; bidirected; no helper nodes)  
      All three are combined through `SpatialGraphMediatorLayer` for routing across modes. 
- **Shared mobility:** `BicycleRentalLayer` with `bicycle_rental_sidi_maarouf.geojson`. 
- **Transit:**
    - **Bus:** `BusStationLayer` (`stations.geojson`), `BusRouteLayer` (`bus_L300_line.csv`), `BusLayer` (runs on the drive graph), `BusSchedulerLayer` (`bus_driver_schedule_casablanca.csv`).
    - **Tram:** `TramStationLayer` (`casablanca_T1_stations.geojson`), `TramRouteLayer` (`tram_t1_line.csv`), `TramLayer`, `TramSchedulerLayer` (`tram_driver_schedule.csv`). 
- **Agent container:** `CitizenLayer`.

### Entities (vehicle catalogs)
- `RentalBicycle` → `resources/bicycle.csv`
- `Bus` → `resources/bus.csv`
- `Tram` → `resources/tram.csv` 

### Outputs (where & what you get)
- All configured **CSV outputs** are written under `results/`.
- Trip tables per agent class:
    - **Citizen_trips**: `StableId`, `ActiveCapability`, `RouteMainModality`, `RouteModalities`
    - **BusDriver_trips**: `StableId`, `Line`, `PassengerAmount`
    - **TramDriver_trips**: `StableId`, `Line`, `PassengerAmount`
- Additional per-tick **default** outputs are recorded only when `StoreTickResult == true`. 

> **Note:** The tram network is referenced with modality `TrainDriving` in the mediator input. This is intentional in the model’s modality taxonomy and corresponds to rail-based movement used by trams in this scenario.

## Build
```bash
dotnet build
```
## Running the model
```bash
dotnet run
```
## Data Preparation Scripts
The required geospatial datasets for the simulation are generated with the scripts under `Casablanca/resources/scripts`.

| File                               | Responsibility                                                                                 |
|------------------------------------|-----------------------------------------------------------------------------------------------|
| `generate_landuse_and_buildings.py`| Produces land-use and building layers as GeoJSON.                                             |
| `generate_POI.py`                  | Extracts Points of Interest (POIs) for the AOI.                                               |
| `generate_drive_walk_graphs.py`    | Builds driving and walking network graphs for Casablanca Sidi Maârouf.                        |
| `extract_tram_stations.py`         | Extracts tram stations from OSM and builds a tram station layer.                              |
| `generate_random_points_in_aoi.py` | Generates random points inside the AOI polygon (with optional spacing).                       |
| `generate_GraphML.py`              | Exports the AOI networks as GraphML for use in graph-based tools.                             |
| `extract_bus_routes_and_stations.py`| Extracts bus routes and stations from the OSM Overpass API for the AOI.                      |

## Visualization
Agent movements can be visualized in **kepler.gl**. Drag and drop static layers (routes, stations, AOI, …) directly from the `resources/` folder. For temporal playback of movements and interactions, load the trip files `Citizen_trips.geojson`, `TramDriver_trips.geojson`, and `BusDriver_trips.geojson` in kepler.gl and use the time slider.
## Evaluation
Summarize simulated trips and produce ready-to-use tables/charts for the scenario using the following steps:
### 1) Convert Citizen_trips.geojson to CSV
### 2) Run the evaluate.py in resources/scripts/
All results are written next to the input file under resources/eval_outputs/:
* main_mode_counts.csv – trip counts per main mode
* main_mode_share_pct.csv – percent share per main mode
* top_sequences.csv – most common full mode sequences (e.g., Walking>Bus>Walking)
* augmented_trips.csv – original rows plus seq_list (parsed from routemodalities)
* main_mode_share_pct.png – bar chart of main-mode share
* summary.txt – compact human-readable report

## Contact
Yasser Ibourk eMail: yasser.ibourk@haw-hamburg.de
Website: www.mars-group.org


