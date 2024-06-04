<h1 align="center">SmartOpenHH | <a href="https://mars.haw-hamburg.de">Website</a></h1>

The SmartOpenHamburg model is an agent-based simulation model for the representation of a digital twin. It calculates multi-modal path dynamics and simulates a defined daily routine of a citizen.

## Quick Start

Start and adjusting the model requires the following steps.

Clone the Git Repo:

```
git clone https://git.haw-hamburg.de/mars/model-soh.git
```
[Program.cs](SOHTravellingBox%2FProgram.cs)
Download and install the SDK for NetCore from the official [website](https://dotnet.microsoft.com/download/dotnet-core/).  Navigate into the cloned directory and make sure that all required dependencies are installed automatically by building the model in the directory where the SOHModel.sln file is located:

```
dotnet build
```

We have prepared a scenario in the project ``SOHTravellingBox`` for the entry with agents, travelling within the area of Hamburg Dammtor, which you can start immediately.

Navigate to the folder and start the model:

```
cd SOHTravellingBox
dotnet run
```

This runs the simulation and creates a file call `HumanTraveler_trips.geojson`. Open [kepler.gl](https://kepler.gl/demo) and import the file via drag & drop. See the trajectories which were computed by the simulation.

---
## SOH modeling introduction

The SOH model provides urban mobility functionality for agents. Agents can therefore use different modalities (transportation devices) to reach their goals in the city. The movement is executed on a graph structure that represents roads, sidewalks or railways.

### Agent types

The model provides two main types of [agents](https://www.mars-group.org/docs/tutorial/soh/agents/) that have a mobility desire (besides pure driver agents that fulfill the role of public transport).

[`Traveller` agents](https://www.mars-group.org/docs/tutorial/soh/agents/traveler) have a start and a goal and they try to reach their goal by using available transportation devices, which we call their movement `capabilities`. They can be easily spawned by an `AgentSchedulerLayer` randomly within an area and find random goals within a target area. They are the simple solution to create mobility demand.

[`Citizen` agents](https://www.mars-group.org/docs/tutorial/soh/agents/citizen) have a daily schedule that cause their mobility demand. The schedule is dependent on their employment status. They can also choose between the modalities that are generally provided in the respective scenario and that are especially available or reasonable for the particular agent and its current location.

![traveler_zones](https://www.mars-group.org/assets/images/harbug_green4bikes-f03fbc7cde934b63b9740a2abb247d31.png)

### Modalities

The model provides a variety of modalities that can be used. We call them `ModalChoice`s.

`Walking` is the main modality and always available.

`CarDriving` requires an own car for the agent (co-driving is not yet implemented) that has to be parked on a parking place. The agent moves to the car, drives to a parking place near by the goal, and then concludes the rest of the way by foot.

`CyclingOwnBike` is quite similar to walking, because the bike can either be parked at the node or in a bike station. Because it can be parked quite everywhere, agents can move from start to goal with the bike (if the bike is available at the start node).

`CyclingRentalBike` is using a rental bike. The agent walks to a near by rental station that has remaining bikes, takes a bike that needs to be returned at another rental station and then finishes the remaining route by foot.

`Train` can be used to drive as a passenger. Therefore the agents searches a reasonable train station near by and exits the train station near the goal. A transfer between lines is possible at stations that provide different lines.

`Ferry` is quite similar to using the train just with ships moving over water.

### Environment

Although there are different modal choices, some of these share the same environment, for instance bikes might also use the streets like cars. We therefore have the `SpatialModalityType` discriminator that describes which lanes can be used by which transportation devices.

For movement we need a [graph](https://www.mars-group.org/docs/tutorial/development/layers#vector-layer) because all transportation devices require it. The graph is stored in the [`SpatialGraphEnvironment`](https://www.mars-group.org/docs/tutorial/development/environments/spatialgraphenv) (`SGE`) that provides route searching capabilities and supervises movement concerning validity constraints like collision detection.

![railroad_graph](README_images/s-bahn-hh.png)

The environment is initialized by graphs that can be imported in either `graphml` or `geojson` format. For multimodal route searching, we require to integrate all relevant graphs in one SGE. So use the `inputs` configuration in the simulation config and add an import configuration to define that edges (later transformed to lanes) of this file can be used by a set of modalities (spatial modality types).

```json
{
      "name": "SpatialGraphMediatorLayer",
      "inputs": [
        {
          "file": "resources/hamburg_rail_station_areas_drive_graph.geojson",
          "inputConfiguration": {
            "modalities": ["Walking"],
            "isBidirectedGraph": true
          }
        },
        {
          "file": "resources/hamburg_u1_north_graph.geojson",
          "inputConfiguration": {
            "modalities": ["TrainDriving"],
            "isBidirectedGraph": true
          }
        }
      ]
    }
```


![walk_drive_graph](README_images/walk_drive_graph.png)

### Handle concept

The usage of transportation devices follows a [handle concept](https://mars.haw-hamburg.de/articles/soh/steering.html) that is a contract between agent and vehicle. If the agent provides the required capabilities, then a vehicle can provide a handle for usage.

![contract](README_images/contract_schema.png)

Every vehicle type defines a steering handle that is provided by the respective vehicle on entrance. The handle takes care about the concrete movement logic and so capsulates the movement behavior by following traffic rules (like driving a car without actively thinking how to do it). The handle requires the agent to have certain capabilities that are required to use the vehicle. These are defined in the respective `ISteeringCapabable`. After leaving a vehicle the handle is invalidated and exchanged with the default `WalkingSteeringHandle`.

![car_steering_handle_concept](README_images/uml_car_steering.png)
