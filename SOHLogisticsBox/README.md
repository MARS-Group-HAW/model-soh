# SOHLogisticsBox

SOHLogisticsBox is an agent-based simulation for optimizing logistics and routing in Germany. It currently uses a
GEOJSON that includes all Highways and Federal Streets in Germany. It extends the Smart Open Hamburg (SOH) project by
adding the semi-truck entity and adding possible constraints such as vehicle height, weight, width, length and
maxIncline. The simulation utilizes OpenStreetMap (OSM) data to determine feasible routes while considering alternative
paths in case of
blockages.
___

## Map Export

For this project the Map consists of all Highways and Federal Streets in Germany. For this purpose the Map was exported
with osmnx. Furthermore the tags maxheight, maxlength, surface, maxweight, maxwidth and incline were additionally added.
The Map can be exported via the following python code (Colab Script):

```python
%%capture
!pip install osmnx geopandas
import osmnx as ox
import geopandas as gpd
import pandas as pd
additional_tags = [
    "maxheight",
    "maxlength",
    "surface",
    "maxweight",
    "maxwidth",
    "incline",
]
# Add Additional Tags to Tags
ox.settings.useful_tags_way += additional_tags
custom_filter = (
    '["highway"~"motorway|trunk|primary"]'
)
# Download all Federal and Highway Streets in Germany
all_roads = ox.graph_from_place(
    "Germany",
    network_type="drive",
    custom_filter=custom_filter,
    simplify=True
)
# Extract Nodes
gdf_nodes = ox.graph_to_gdfs(all_roads, edges=False, nodes=True)

# Extract Edges
gdf_edges = ox.graph_to_gdfs(all_roads, edges=True, nodes=False)
# Remove maxspeed Tag as it currently crashes MARS simulation (TO BE FIXED)
if "maxspeed" in gdf_edges.columns:
    gdf_edges = gdf_edges.drop(columns=["maxspeed"])
    # Add a type-tag for nodes and edges
gdf_nodes["type"] = "node"
gdf_edges["type"] = "edge"

# Combine GeoDataFrames
gdf_combined = pd.concat([gdf_nodes, gdf_edges], ignore_index=True)
# Save GeoJSON file
gdf_combined.to_file("autobahn_und_bundesstrassen_deutschland_attributes_02.geojson", driver="GeoJSON")
```

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

| Truck Type      | Start Latitude | Start Longitude | Destination Latitude | Destination Longitude | Drive Mode |
|---------------|---------------|----------------|----------------------|----------------------|------------|
| StandardTruck | 51.3030184    | 7.2630005      | 52.5476332           | 8.1139535            | 3          |
| StandardTruck | 51.3030184    | 7.2630005      | 52.5476332           | 8.1139535            | 2          |
| StandardTruck | 53.5577323    | 10.2174148     | 52.8067652           | 12.7941436           | 2          |
| StandardTruck | 50.82306      | 10.02379       | 50.49248             | 9.98912              | 3          |
| HighTruck    | 50.82306      | 10.02379       | 50.49248             | 9.98912              | 3          |


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

The **`semi_truck.csv`** defines different types of **`SemiTrucks`** like the follwing:

| Type              | Max Acceleration | Max Deceleration | Max Speed (m/s) | Length (m) | Height (m) | Width (m) | Traffic Code | Passenger Capacity | Velocity | Mass (tons) | Max Incline (%) |
|------------------|-----------------|------------------|----------------|------------|------------|-----------|--------------|------------------|----------|-------------|----------------|
| StandardTruck    | 0.5             | 1.2              | 20.83          | 12         | 2.5        | 2.5       | German       | 2                | 0        | 15.0        | 15             |
| HighTruck       | 0.5             | 1.2              | 20.83          | 12         | 4.0        | 2.5       | German       | 2                | 0        | 15.0        | 15             |
| LightTruck      | 0.5             | 1.2              | 20.83          | 12         | 2.5        | 2.5       | German       | 2                | 0        | 3.0         | 15             |
| WideTruck       | 0.5             | 1.2              | 20.83          | 12         | 2.5        | 3.5       | German       | 2                | 0        | 3.0         | 15             |
| ShortTruck      | 0.5             | 1.2              | 20.83          | 8          | 2.5        | 2.5       | German       | 2                | 0        | 3.0         | 15             |
| SmallInclineTruck | 0.5           | 1.2              | 20.83          | 8          | 2.5        | 2.5       | German       | 2                | 0        | 3.0         | 8              |

## SemiTruck

The **`SemiTruck`** class models semi-truck behavior within the MARS simulation, integrating steering,
passenger handling, and road network navigation. It extends the **`Vehicle`** class, operating in the **`CarDriving`**
modality, allowing it to follow realistic traffic rules and interact dynamically with other vehicles. The **`SemiTruck`
** is connected to the **`StreetLayer`**, representing highways and major roads, and leverages a spatial graph
environment for precise routing, pathfinding, and decision-making. It is designed for large-scale logistics and freight
transport simulations, it accounts for real-world constraints such as road restrictions or blocked roads.

## SemiTruckDriver

The **`SemiTruckDriver`** class models a semi-truck driver agent in the MARS simulation, handling route
planning, vehicle control, and environmental interactions. It navigates using **`SemiTruckRouteFinder`**, dynamically
adjusting to constraints and blocked roads. Integrated with the **`SemiTruckLayer`**, it registers with the simulation,
manages a **`SemiTruck`**, and ensures realistic freight transport behavior. The driver interacts with road networks,
optimizing truck-specific routes while considering constraints like weight, height, length, width, incline and
restricted roads**. Once the destination is reached, the driver exits the simulation, ensuring efficient lifecycle
management within large-scale logistics and mobility simulations.

## SemiTruckLayer

The **`SemiTruckLayer`** class manages the semi-truck simulation environment, handling the initialization of the
spatial graph, agent creation, and lifecycle management. It defines the CarDriving modality, ensuring semi-trucks
operate within the road network. The layer initializes the spatial environment either from a provided mapping or
a specified file, creating a realistic traffic and routing landscape.
During initialization, it spawns and registers `SemiTruckDriver` agents, linking them with the road network and
tracking them in a dedicated dictionary. The layer ensures dynamic agent management, supporting route
adjustments, blocked road handling, and efficient truck movement. Once the simulation completes, the layer unregisters
the agents, maintaining a structured and scalable approach for large-scale freight and mobility simulations.

## SemiTruckSchedulerLayer

The **`SemiTruckSchedulerLayer`** is responsible for scheduling and deploying semi-truck drivers into the MARS
simulation. It processes scheduling data, extracts key parameters such as start location, destination, drive mode,
and truck type, and dynamically spawns **`SemiTruckDriver`** agents within the **`SemiTruckLayer`**.

The layer ensures data integrity by validating required fields before creating agents. It interacts with the agent
management system, assigning each driver specific routes, vehicle types, and driving behaviors. The scheduled
drivers are then registered and tracked within the simulation environment, ensuring accurate freight transport
modeling. This plays a crucial role in traffic simulation to efficiently manage and time the placing of truck
drivers.

The SemiTrucks in the  **`SemiTruckSchedulerLayer`** are initialized in  **`semi_truck_scheduler.csv`** like the following:

| Start Time | End Time | Spawn Interval (min) | Spawn Amount | Start Longitude | Start Latitude | Destination Longitude | Destination Latitude | Truck Type      | Drive Mode |
|-----------|---------|---------------------|--------------|-----------------|---------------|----------------------|----------------------|---------------|------------|
| 07:00    | 13:00  | 1000                | 1            | 10.1355085      | 53.5667347    | 11.4522277           | 53.4306468           | StandardTruck | 2          |
| 08:00    | 14:00  | 1000                | 1            | 10.1355085      | 53.5667347    | 11.4522277           | 53.4306468           | StandardTruck | 2          |
| 09:00    | 15:00  | 1000                | 1            | 10.1355085      | 53.5667347    | 11.4522277           | 53.4306468           | StandardTruck | 2          |
| 10:00    | 16:00  | 1000                | 1            | 10.1355085      | 53.5667347    | 11.4522277           | 53.4306468           | StandardTruck | 2          |


## SemiTruckRouteFinder

### **SemiTruckRouteFinder in MARS Simulation**

The **`SemiTruckRouteFinder`** is a route-planning utility for semi-truck navigation within the MARS
simulation. It determines optimal truck routes while considering road constraints such as height, weight,
width, length, and max incline limits. The route-finding process is tailored to different driving modes, enabling random
navigation, shortest path search, OpenStreetMap (OSM) route following, and adaptive detouring.

The finder integrates directly with the spatial graph environment, ensuring realistic path calculations that
avoid blocked roads, restricted areas, and inaccessible routes. By dynamically adjusting to road attributes and
truck-specific constraints, the **`SemiTruckRouteFinder`** ensures efficient and regulation-compliant routing for
freight transport simulations.