# SOHFerryTransferBox

FerryTransfer is a model that focuses on the movement and transfer of people across the Elbe River in Hamburg. Over various intervals and periods, agents are transported from one side of the Elbe to the other using autonomous ferries.

Ferrys can either be defined via a GTFS input and scheduling or explicitly through specific times and destinations to service different stops.
DocWorkers can be created in selected areas with start and destination using an event table dock_worker.csv.

The default scenario covers the area and data around Hamburg Harbour, Germany.

## Model Structure

The following table shows the required layers with input, used by the agents or entities:

| Layer                         | Responsibility                                                                                                                                         |
|-------------------------------|--------------------------------------------------------------------------------------------------------------------------------------------------------|
| ``SpatialGraphMediatorLayer`` | The multimodal spatial graph layer, allowing agents to move across different lanes on edges, restricted by modalities                                  |
| ``FerryLayer``                | The graph layer, providing an isolated graph environment, representing the river for `ShipDriving` modality related objects                            |
| ``FerrySchedulerLayer``       | The time-scheduled event layer, creating and register `FerryDriver` at specfic time points or time-intervals                                           |
| ``FerryStationLayer``         | The data vector-layer providing point-based information about entry and exit stations where `Ferry` entities can be accessed                           |
| ``FerryRouteLayer``           | The layer importing the routes with time-schedules where the `FerryDriver` have to drive and need to wait until starting to drive to the next station. |
| ``DockWorkerLayer``           | The agent-layer spwaning and managing the `DockWorker` agents                                                                                          |
| ``DockWorkerSchedulerLayer``  | The time-schedules event layer, creating and register `DockWorker` at specific time points or time-intervals                                           |


The following table shows the agent and entities active acting within the environment and using the objects:

| Agent or Entity | Responsibility                                                                                                                                            |
|-----------------|-----------------------------------------------------------------------------------------------------------------------------------------------------------|
| ``DockWorker``  | The agent, travelling layer, using the multimodality capabilities to travel from a start to a specific goal using `ShipDriving` and `Walking` modalities. |
| ``FerryDriver`` | The agent, controlling a single `Ferry` to drive the vehicle from one station to another directed by schedules from a `FerryRouteLayer`                   |
| ``Ferry``       | The entity representing the ferry vehicle, actively used by the `FerryDriver` with different attribute for capacity and king                              |

## Start the model

To start the model, navigate into this directory from a terminal and execute the following command:

```bash
dotnet run
```

This uses the default ``config.json`` configuration where all inputs and outputs are defined.