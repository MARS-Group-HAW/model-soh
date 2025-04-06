# SOHLogisticsBox

SOHLogisticsBox is an agent-based simulation for optimizing logistics and routing in Germany. It currently uses a
GEOJSON that includes all Highways and Federal Streets in Germany. It extends the Smart Open Hamburg (SOH) project by
adding the semi-truck entity and adding possible constraints such as vehicle height, weight, width, length and
maxIncline. The simulation utilizes OpenStreetMap (OSM) data to determine feasible routes while considering alternative
paths in case of
blockages.
___

## Map Export & Preprocessing

The base map for this project consists of **all highways and federal roads in Germany**. The initial export was done using [**OSMnx**](https://github.com/gboeing/osmnx), including key tags like:

- `maxheight`
- `maxlength`
- `surface`
- `maxweight`
- `maxwidth`
- `incline`

The full export and preprocessing pipeline is implemented in Python and can be found under:
`SOHLogisticsBox/resources/Scripts/GeoJSON/`

### Relevant Scripts

- **`export_Initial_Map.py`**  
  Exports the raw highway and federal road network of Germany using OSMnx and adds useful attributes.  
  The `maxspeed` tag is **explicitly removed** during this step to avoid known runtime errors in MARS.

- **`add_MaxSpeed.py`**  
  Adds the `maxspeed` tag back in after the initial export.  
  This was done **after** the `.geojson` structure had been successfully integrated into MARS.

- **`add_Elevation.py`**  
  Adds elevation data for each coordinate using an external elevation API.

- **`calculate_Incline_By_Elevation.py`**  
  Calculates missing `incline` values for edges where OpenStreetMap doesn't provide them.  
  It uses elevation data and **haversine distance** for a realistic slope approximation.

- **`compress_geojson.py`** *(optional)*  
  Removes unnecessary whitespace from the `.geojson` file to reduce its size by ~50%.
  
- **`calculateEdgeCapacity.py`**  
  Calculates the capacity of each edge in the network based on its **length** and **number of lanes**,  
  using a standardized unit called **Fahrzeugeinheiten (FE)**. Each vehicle type occupies a specific number of FE,  
  derived from its length and the minimum safety distance depending on driving speed.

  For example, a road with 500m length and 2 lanes has a total capacity of 1000 FE.  
  The script adds a `max_capacity_fe` field to each edge in the GeoJSON, which can be used during simulation to track  
  real-time traffic load and perform congestion modeling.

---

### File Information

The current final output is a file named:

`autobahn_und_bundesstrassen_deutschland_attributes_08.geojson`

However, due to its large size, it is **not directly stored in the repository**. Instead, it is **compressed as a `.rar` file**:

> ⚠️ **Important**  
> The file `autobahn_und_bundesstrassen_deutschland_attributes_08.geojson` is stored as  
> **`autobahn_und_bundesstrassen_deutschland.rar`** in the repository.  
> Please extract it **before running the simulation**.

___

## ModelDescription

In the `main` of `Program.cs`, the `ModelDescription` object is defined with a `SemiTruckLayer` where `SemiTrucks` are
controlled by `SemiTruckDrivers`. Also, there is a `SemiTruckSchedulerLayer` to schedule the spawning of `SemiTrucks` in
the
environment:

```c#
var description = new ModelDescription();
description.AddLayer<SemiTruckLayer>();
description.AddLayer<SemiTruckSchedulerLayer>();
description.AddAgent<SemiTruckDriver, SemiTruckLayer>();
description.AddEntity<SemiTruck>();
```

___

## Globals

This section contains the attributes that allow for general configuration of the model. Below is a
brief description of the main attributes.

```json
"globals": {
"deltaT": 1,
"startPoint": "2021-10-11T06:00:00",
"endPoint": "2021-10-11T18:00:00",
"deltaTUnit": "seconds",
"console": true
},
```

### Attribute Description

* `deltaT`: the amount of time increment per simulation step (type: `integer`)
* `startPoint`: the time at which the simulation begins (type: `DateTime`)
* `endPoint`: the time at which the simulation ends (type: `DateTime`)
* `deltaTUnit`: the duration of a time step in the simulation (type: `seconds`)
* `console`: defines if progress bar is displayed in console during simulation run (type: `boolean`)

___

## Layer Mappings

In this section, the layer types are defined in the model logic (and that were added to `description` above) are
configured and populated the .geojson file.

```json
"layers": [
{
"name": "SemiTruckLayer",
"inputs": [
{
"file": "resources/autobahn_und_bundesstrassen_deutschland_attributes_03.geojson",
"inputConfiguration": {
"modalities": ["CarDriving"],
"isBidirectedGraph": true
}
}
]
},
{
"name": "SemiTruckSchedulerLayer",
"file": "resources/semi_truck_scheduler.csv"
}
]

```

### Attribute Description

* `SemiTruckLayer`:
    * `name`:  the name of the layer used for modeling the road network for semi-trucks (type: `string`)
    * `inputs`: a list of input files that provide the geographical and attribute data for this layer (type: `array`)
        * `file`: the file path of the geographic data containing road attributes for highways and federal roads in
          Germany (type: `string`)
        * `inputConfiguration`:  specifies configuration details for the input file (type: `object`)
            * `modalities`: a list of transportation modes supported by this layer in this case "CarDriving" for
              road-based vehicles (type: `array`)
            * `isBidirectedGraph`: defines whether the road network is bidirectional, meaning roads can be used in both
              directions (type: `boolean`)
* `SemiTruckSchedulerLayer`:
    * `name`: the name of the layer responsible for scheduling semi-truck movements (type: `string`)
    * `file`: the file path containing scheduling data for semi-truck operations (type: `string`)

___

## AgentMappings

In this section, the agent types that are defined in the model logic (and that were added to `desciption` above) are
configured:

```json
"agents": [
{
"name": "SemiTruckDriver",
"count": 100,
"file": "resources/semi_truck_initializer.csv",
"outputs": [
{
"kind": "trips",
"outputConfiguration": {
"tripsFields": [
"StableId",
"StartPosition",
"EndPosition",
"DistanceTraveled",
"Duration"
]
}
}
],
"individual": [
{
"name": "ResultTrajectoryEnabled",
"value": true
}
]
}
]

```

### Attribute Description

* `SemiTruckDriver`:
    * `name`: the name of the agent type, representing a truck driver in the simulation (type: `string`)
    * `count`: the max number of agents of this type to be instantiated in the simulation (type: `integer`)
    * `file`: the file path to the data file that initializes the agents with specific attributes and starting
    * `outputs`: specifies the type of data that will be recorded for this agent during the simulation
        * `kind`: the category of output data to be recorded (e.g., "trips" for travel data) (type: `string`)
        * `outputConfiguration`: specifies the specific fields to be logged for trips (type: `object`)
            * `tripsFields`: a list of data fields recorded for each trip taken by the agent (type: `array`)
                * `StableId`: a unique identifier for the agent (type: `string`)
                * `StartPosition`: the initial position of the agent at the start of a trip (type: `coordinate`)
                * `EndPosition`: the final position of the agent at the end of a trip (type: `coordinate`)
                * `DistanceTraveled`: the total distance the agent traveled during the trip (type: `double`)
                * `Duration`: the total time duration of the trip (type: `double`)
* `individual`: a list of properties applied to each individual agent of this type (type: `array`)
    * `name`: the name of the property (e.g., "ResultTrajectoryEnabled", which enables trajectory tracking)  (
      type: `string`)
    * `value`: the value assigned to the property (e.g., true to enable the trajectory result recording) (
      type: `boolean`)

The **`SemiTrucks`** are initialized in **`semi_truck_initializer.csv`** like the following:

| Truck Type          | Start Latitude | Start Longitude | Destination Latitude | Destination Longitude | Drive Mode |
|---------------------|----------------|-----------------|----------------------|-----------------------|------------|
| SmallTruck          | 51.3030184     | 7.2630005       | 52.5476332           | 8.1139535             | 3          |
| MediumLoadTruck     | 51.3030184     | 7.2630005       | 52.5476332           | 8.1139535             | 2          |
| HeavyLoadTruck      | 53.5577323     | 10.2174148      | 52.8067652           | 12.7941436            | 2          |
| ExtraCapacityTruck  | 50.82306       | 10.02379        | 50.49248             | 9.98912               | 3          |
| OverloadTruck       | 50.82306       | 10.02379        | 50.49248             | 9.98912               | 3          |

## EntityMappings

In this section, the entity types that are defined in the model logic (and that were added to `desciption` above) are
configured:

```json
"entities": [
{
"name": "SemiTruck",
"file": "resources/semi_truck.csv"
}
]
```

### Attribute Description

* `entities`: a list of entity definitions that represent physical or abstract objects in the simulation
    * `name`: the name of the entity type, representing a semi-truck in the simulation (type: `string`)
    * `file`: the file path to the CSV file containing the attributes and properties of semi-trucks used in the
      simulation (type: `string`)

## SemiTruck Initialization

The **`semi_truck.csv`** defines different types of **`SemiTrucks`** like the following:

| Type                | Max Acceleration | Max Deceleration | Max Speed (m/s) | Length (m) | Height (m) | Width (m) | Traffic Code | Passenger Capacity | Velocity | Mass (tons) | Max Incline (%) |
|---------------------|------------------|------------------|-----------------|------------|------------|-----------|--------------|--------------------|----------|-------------|-----------------|
| SmallTruck          | 0.5              | 1.2              | 30.83           | 6          | 2.5        | 2.5       | German       | 2                  | 0        | 5.0         | 25              |
| MediumLoadTruck     | 0.5              | 1.2              | 30.83           | 8          | 2.5        | 2.5       | German       | 2                  | 0        | 10.0        | 22              |
| HeavyLoadTruck      | 0.5              | 1.2              | 30.83           | 10         | 2.5        | 2.5       | German       | 2                  | 0        | 15.0        | 18              |
| ExtendedLoadTruck   | 0.5              | 1.2              | 30.83           | 12         | 2.5        | 2.5       | German       | 2                  | 0        | 20.0        | 15              |
| LargeCapacityTruck  | 0.5              | 1.2              | 30.83           | 12         | 2.5        | 2.5       | German       | 2                  | 0        | 25.0        | 13              |
| ExtraCapacityTruck  | 0.5              | 1.2              | 30.83           | 14         | 2.5        | 2.5       | German       | 2                  | 0        | 30.0        | 12              |
| HighVolumeTruck     | 0.5              | 1.2              | 30.83           | 14         | 2.5        | 2.5       | German       | 2                  | 0        | 35.0        | 10              |
| MaximumLoadTruck    | 0.5              | 1.2              | 30.83           | 16         | 2.5        | 2.5       | German       | 2                  | 0        | 40.0        | 8               |
| OverloadTruck       | 0.5              | 1.2              | 30.83           | 16         | 2.5        | 2.5       | German       | 2                  | 0        | 50.0        | 6               |
| UnlimitedTruck      | 0.5              | 1.2              | 30.83           | 1          | 1.0        | 1.0       | German       | 2                  | 0        | 2.0         | 25              |

## Realistic SemiTruck Data based on German Federal Statistics

A second file named **`semi_truck_real_data.csv`** is based on official data from the **Kraftfahrt-Bundesamt Deutschland** (German Federal Motor Transport Authority). To create this file, multiple datasets were merged to extract:

- Origin and destination districts
- Truck weight classifications
- Estimated route distances

Due to mismatches and inconsistencies between the source files, proportional methods (e.g. matching via weight categories) were used to align the data. The resulting file represents the **average daily freight truck traffic** in Germany.

In future development, the simulation could be extended by integrating weekday-specific distributions to reflect fluctuations over the course of a week.

---

## Data Sources & Scripts

All related resources and documentation used to generate the simulation data can be found here:
`SohLogisticBox/resources/Scripts/SemiTruckData/resources`

This folder includes:

- Official statistics from the **Kraftfahrt-Bundesamt**
- Reference handbooks
- A full **NUTS** directory (Nomenclature of Territorial Units for Statistics)

### Processing Workflow

Two Python scripts were used to transform the raw data into a format suitable for simulation:

- **`merge_TruckData.py`**  
  Creates the intermediate file `trucks_per_area.csv`, listing route requirements per district with estimated distances and weights.

- **`truck_Initializer.py`**  
  Converts district-level route data into **coordinate-based truck routes** for the simulation, producing the final `semi_truck_real_data.csv`.

---
## SemiTruck

The **`SemiTruck`** class models semi-truck behavior within the MARS simulation, integrating steering,
passenger handling, and road network navigation. It extends the **`Vehicle`** class, operating in the **`CarDriving`**
modality, allowing it to follow realistic traffic rules and interact dynamically with other vehicles.
The **`SemiTruck`** is connected to the **`StreetLayer`**, representing highways and major roads, and leverages a
spatial graph environment for precise routing, pathfinding, and decision-making.
It is designed for large-scale logistics and freight transport simulations, it accounts for real-world constraints such
as road restrictions or blocked roads.

## SemiTruckDriver

The **`SemiTruckDriver`** class models a semi-truck driver agent in the MARS simulation, handling route
planning, vehicle control, and environmental interactions. It navigates using **`SemiTruckRouteFinder`**, dynamically
adjusting to constraints and blocked roads. Integrated with the **`SemiTruckLayer`**, it registers with the simulation,
manages a **`SemiTruck`**, and ensures realistic freight transport behavior. The driver interacts with road networks,
optimizing truck-specific routes while considering constraints like weight, height, length, width, incline and
restricted roads. Once the destination is reached, the driver exits the simulation, ensuring efficient lifecycle
management within large-scale logistics and mobility simulations.

During each simulation tick, the driver continuously checks the upcoming edges of the route (currently defined as 5km lookahead) of its planned route. If a blocked
road segment is detected within this distance—based on the list of currently closed edges—the driver immediately
triggers a rerouting process. A new bypass route is calculated around the closure, leading to the next valid point
on the original path. This enables the truck to avoid blocked segments and continue toward its destination with
minimal disruption, preserving the overall route context.


## SemiTruckLayer

The **`SemiTruckLayer`** class manages the semi-truck simulation environment, handling the initialization of the
spatial graph, agent creation, and lifecycle management. It defines the `CarDriving` modality, ensuring semi-trucks
operate within the road network. The layer initializes the spatial environment either from a provided mapping or
a specified file, creating a realistic traffic and routing landscape.

During initialization, it spawns and registers `SemiTruckDriver` agents, linking them with the road network and
tracking them in a dedicated dictionary. The layer ensures dynamic agent management, supporting route
adjustments, blocked road handling, and efficient truck movement. Once the simulation completes, the layer unregisters
the agents, maintaining a structured and scalable approach for large-scale freight and mobility simulations.

In addition, the layer supports dynamic road closures based on a `road_closures.csv` file (located in resources), where each entry specifies
an edge ID along with a start and end time for the closure. During simulation, these edges are temporarily removed
from the environment and added to a list of `RemovedEdges`, which is considered during route planning and detour
calculations. After the defined closure period ends, the edges are automatically restored, allowing for realistic
modeling of temporary disruptions such as construction zones or accidents.

## SemiTruckSchedulerLayer

The **`SemiTruckSchedulerLayer`** is responsible for scheduling and deploying semi-truck drivers into the MARS
simulation. It processes scheduling data, extracts key parameters such as start location, destination, drive mode,
and truck type, and dynamically spawns **`SemiTruckDriver`** agents within the **`SemiTruckLayer`**.

The layer ensures data integrity by validating required fields before creating agents. It interacts with the agent
management system, assigning each driver specific routes, vehicle types, and driving behaviors. The scheduled
drivers are then registered and tracked within the simulation environment, ensuring accurate freight transport
modeling. This plays a crucial role in traffic simulation to efficiently manage and time the placing of truck
drivers.

The SemiTrucks in the  **`SemiTruckSchedulerLayer`** are initialized in  **`semi_truck_scheduler.csv`** like the
following:

| Start Time | End Time | Spawn Interval (min) | Spawn Amount | Start Longitude | Start Latitude | Destination Longitude | Destination Latitude | Truck Type    | Drive Mode |
|------------|----------|----------------------|--------------|-----------------|----------------|-----------------------|----------------------|---------------|------------|
| 07:00      | 13:00    | 1000                 | 1            | 10.1355085      | 53.5667347     | 11.4522277            | 53.4306468           | StandardTruck | 2          |
| 08:00      | 14:00    | 1000                 | 1            | 10.1355085      | 53.5667347     | 11.4522277            | 53.4306468           | StandardTruck | 2          |
| 09:00      | 15:00    | 1000                 | 1            | 10.1355085      | 53.5667347     | 11.4522277            | 53.4306468           | StandardTruck | 2          |
| 10:00      | 16:00    | 1000                 | 1            | 10.1355085      | 53.5667347     | 11.4522277            | 53.4306468           | StandardTruck | 2          |

## SemiTruckRouteFinder

The **`SemiTruckRouteFinder`** is a route-planning utility for semi-truck navigation within the MARS
simulation. It determines optimal truck routes while considering road constraints such as height, weight,
width, length, and max incline limits. The route-finding process is tailored to different driving modes, enabling random
navigation, shortest path search, OpenStreetMap (OSM) route following, and adaptive detouring.

The finder integrates directly with the spatial graph environment, ensuring realistic path calculations that
avoid blocked roads, restricted areas, and inaccessible routes. By dynamically adjusting to road attributes and
truck-specific constraints, the **`SemiTruckRouteFinder`** ensures efficient and regulation-compliant routing for
freight transport simulations.

> ℹ️ **Note:**  
> In cases where the target location is not fully reachable due to road closures or constraint violations,  
> the `SemiTruckRouteFinder` will still return a valid route to the **closest possible point** near the destination. 
> This ensures that trucks do not fail silently and can reach at least a partial goal location. 
> Although this does require that the truck can move on the road it starts on.

## Docker & ICC Cloud Deployment

The simulation can also be executed in the **cloud environment of HAW Hamburg** using the **Infrastructure Cloud Computing (ICC)** platform. This is particularly useful for large-scale experiments or performance-intensive runs.

### Docker Setup

A `Dockerfile` is provided in the project directory. It defines the build environment for the simulation, installs required dependencies, and sets up the execution context for MARS-based simulations.

You can find it under:
`SOHLogisticBox/Dockerfile`
This Docker image can be built locally or pushed to a GitLab container registry to be deployed on the ICC.

---

### ICC Deployment

The ICC-specific deployment files are located in:
`SOHLogisticBox/`
These files include:

- **`deployment.yaml`**  
  Defines the job that runs the simulation inside the ICC cluster.

- **`configmap.yaml`**  
  Injects the simulation configuration (e.g., initial truck data, output setup).

- **`icc-config.yaml`**  
  Contains the connection configuration for the ICC cluster (Kubernetes context).

- **`output-pvc.yaml`** & **`pvc-access-pod.yaml`**  
  Setup and access the persistent volume claim (PVC) used to store simulation results.

> **Note:**  
> Running the simulation in the cloud **requires a valid ICC token and access to the Kubernetes cluster** of HAW Hamburg.  
> The credentials and token must be configured in your `icc-config.yaml`. These are **user-specific** and not included in the repository.

---

### Workflow Overview

1. Build or pull the container image (e.g., from GitLab registry).
2. Deploy the simulation via `deployment.yaml` using `kubectl`.
3. Configuration is injected via `configmap.yaml`, and results are stored in the defined PVC.
4. Once completed, results can be accessed via the `pvc-access-pod.yaml` utility pod.

This setup enables automated and scalable simulation runs in the cloud — ideal for batch processing, testing different traffic scenarios, or exploring dynamic routing behaviors in high-load conditions.
