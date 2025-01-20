# SOHCitizenDailyPlanBox

CitizenDaily provides a scenario that uses the day-plan-dependent `Citizen` agents. Depending on the time of day, the `Citizen` selects a `TripReason` relevant to their precomputed daily plan.

Through the `MediatorLayer`, which aggregates data sources from the `VectorBuildingsLayer`, `VectorPoiLayer`, and `VectorLandUseLayer`, a corresponding destination is chosen for the selected action (e.g., Errands, Freetime, etc.).

The `TripReason` represents the activity carried out at the selected destination based on the data. The `HomeTime` and `Work` activities are fixed and all other activities are freely selectable during each search.

The default scenario covers the area and data around Altona Altstadt, Hamburg, Germany.

## Model Structure

The following table shows the required layers with input, used by the agents or entities:

| Layer                         | Responsibility                                                                                                                                                             |
|-------------------------------|----------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| ``SpatialGraphMediatorLayer`` | The multimodal spatial graph layer, allowing agents to move across different lanes on edges, restricted by modalities                                                      |
| ``BicycleRentalLayer``        | The data vector-layer providing slots and areas for parking lots, allowing to occupy the place using car                                                                   |
| ``BicycleRentalLayer``        | The data vector-layer providing a set of `RentalBicycle`, which can be accessed by agents, used within a walking-bicycle route                                             |
| ``CitizenLayer``              | The agent layer, spawning and managing the `Citizen` agents, created by the pvoided input file                                                                             |
| ``TrafficLightLayer``         | The active-layer with time-series of multiple traffic signs, allowing to update single traffic controller to indicate which edge-to-edge crossing can passed               |
| ``VectorBuildingsLayer``      | The vector-layer, providing (multi-)polygon data of buildings with specified service, which can be mapped to `TripReason` activities                                       |
| ``VectorPoiLayer``            | The vector-layer, providing point data of of relevant services, which can be mapped to `TripReason` activities                                                             |
| ``VectorLandUseLayer``        | The vector-layer, providing (multi-)polygon data, representing areas with services, which can be mapped to `TripReason` activities                                         |
| ``MediatorLayer``             | The aggregating layer, encapsulate the different vector-layer such as `VectorPoiLayer` to allow to retrieve next travelling goals according to given `TripReason` activity |

The following table shows the agent and entities active acting within the environment and using the objects:

| Agent or Entity   | Responsibility                                                                                                                                                                                                               |
|-------------------|------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| ``Citizen``       | The agent, travelling layer, using the multimodality capabilities to travel from a start to a specific goal using `ShipDriving` and `Walking` modalities.                                                                    |
| ``RentalBicycle`` | The vehicle entity, representing a rental bicycle, as part of the `BicycleRentalLayer`, allowing move on `Cycling` edges of the `SpatialModalityType` restrictions and provide different to attributes to describe a bike    |
| ``Car``           | The vehicle entity, representing a car, as part of the `CarParkingLayer`, allowing to move on `CarDriving` edges of the `SpatialModalityType` restricted roads and provide different to attributes to describe this vehicle. |


## Start the model

To start the model, navigate into this directory from a terminal and execute the following command:

```bash
dotnet run
```

This uses the default ``config_altona_altstadt.json`` configuration where all inputs and outputs are defined.