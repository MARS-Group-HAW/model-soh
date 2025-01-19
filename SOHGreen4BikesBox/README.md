# SOHGreen4BikesBox

Green4Bikes is a scenario that models the movement of ``CycleTraveler`` agents, where agents travel from a specific starting point to a selected destination.

They utilize the available bike rental stations and search for cost-efficient routes to the destination, utilizing a rental bike if necessary. If the route is cheaper by bike, the agent moves to the rental station, takes a bike, rides to the next station near the destination, and returns the bike. The remaining distance is covered on foot by the agent.

In detail, the capacities of the stations and the configuration of each rental bike (e.g., e-bike or not) can be individually adjusted.

The default scenario covers the area and data around the district Harburg, Hamburg, Germany, as shown as follows:
![harburg_zentrum](images/harburg.png)

## Model Structure

The following table shows the required layers with input, used by the agents or entities:

| Layer                           | Responsibility                                                                                                                              |
|---------------------------------|---------------------------------------------------------------------------------------------------------------------------------------------|
| ``SpatialGraphMediatorLayer``   | The multimodal spatial graph layer, allowing agents to move across different lanes on edges, restricted by modalities                       |
| ``BicycleRentalLayer``          | The data vector-layer providing slots and areas for parking lots, allowing to occupy the place using car                                    |
| ``CycleTravelerSchedulerLayer`` | The time-scheduled event layer, creating and register `CycleTraveler` at specfic time points or time-intervals                              |
| ``GatewayLayer``                | The data vector-layer providing point-based information about entry and exit stations where travelling agents entities can access this area |

The following table shows the agent and entities active acting within the environment and using the objects:

| Agent or Entity   | Responsibility                                                                                                                                                                                                            |
|-------------------|---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| ``CycleTraveler`` | The agent, travelling from a source to a target, capable `CyclingRentalBike`, `CyclingOwnBike` and `Walking`, allowing to find and use routes with mixtures of thes modalities.                                           |
| ``RentalBicycle`` | The vehicle entity, representing a rental bicycle, as part of the `BicycleRentalLayer`, allowing move on `Cycling` edges of the `SpatialModalityType` restrictions and provide different to attributes to describe a bike |

## Start the model

To start the model, navigate into this directory from a terminal and execute the following command:

```bash
dotnet run
```

This uses the default ``config.json`` configuration where all inputs and outputs are defined.