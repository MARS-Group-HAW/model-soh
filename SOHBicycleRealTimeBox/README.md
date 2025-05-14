# SOHBicycleRealTimeBox

BicycleRealTimeBox is a scenario that models the movement of `CycleTraveler` agents in the area around the Harburg district (Hamburg), similar to the Green4Bikes Box. Agents travel from a specific source to a selected destination.

The key feature of this scenario is the use of real-time data from an integrated sensor network. Instead of relying on fixed resources (such as station capacity or the predefined number of available bikes), the actual data from the sensor network is provided in real time via the integrated vector layer. This real-time data is updated using two different synchronization mechanisms:

1. **Hard Synchronization**  
   The values are fully updated with real-world data at each station.

2. **Soft Synchronization**  
   The values are adjusted by averaging the actual and simulated amounts.

### Synchronization Timing
Synchronization can occur at different times within the simulation:

1. **Directly at the current simulation time**  
   Synchronization occurs in sync with the real-world time. This requires a warm-up phase before future simulation can begin.

2. **Continuously and decoupled from the simulated time**  
   Synchronization runs asynchronously and continuously in the background, independent of the simulation timeline.

## Model Structure

The following table shows the required layers with their inputs, used by the agents or entities:

| Layer                         | Responsibility                                                                                                                     |
|-------------------------------|------------------------------------------------------------------------------------------------------------------------------------|
| `SpatialGraphMediatorLayer`   | The multimodal spatial graph layer, allowing agents to move across different lanes on edges, restricted by modality limitations.   |
| `BicycleRentalLayer`          | The vector data layer providing slots and areas for parking lots, allowing agents to occupy the place using bikes.                 |
| `CycleTravelerSchedulerLayer` | The time-scheduled event layer, creating and registering `CycleTraveler` agents at specific time points or intervals.              |
| `GatewayLayer`                | The vector data layer providing point-based information about entry and exit stations where traveling agents can access this area. |

## Active Agents and Entities

The following table describes the active agents and entities within the environment and their interactions with the objects:

| Agent or Entity | Responsibility                                                                                                                                                                                                  |
|-----------------|-----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| `CycleTraveler` | The agent traveling from a source to a target, capable of using a `CyclingRentalBike`, `CyclingOwnBike`, or `Walking` to find and use routes with a mix of these modalities.                                    |
| `RentalBicycle` | The vehicle entity representing a rental bike, part of the `BicycleRentalLayer`, capable of moving along `Cycling` edges of the `SpatialModalityType` with different attributes (e.g., e-bike or regular bike). |

## Start the Model

To start the model, navigate to this directory from a terminal and execute the following command:

```bash
dotnet run
```

This uses the default `config.json` configuration where all inputs and outputs are defined.

### **Enable Real-Time Synchronization**
To enable real-time synchronization for a vector layer, an MQTT endpoint is defined in the `config.json` file. This allows events from sensors or other sources to be integrated in real time. The MQTT topic can be linked to a specific attribute of the modeled `BicycleRentalStation` entity. A join attribute relationship defines how the field from the MQTT message is mapped.

An example configuration in `config.json` might look like this:

```json
"inputs": [
  {
    "file": "resources/bicycle_rental_layer_complete.geojson",
    "inputConfiguration": {
      "temporalJoinAttribute": "thingId",
      "validTimeAtAttribute": "time",
      "mqttTopicPattern": "v1.1/Datastreams(${DataStreamId})/Observations",
      "mqttBrokerHostName": "iot.hamburg.de"
    }
  }
]
```

In this example:
- `temporalJoinAttribute` defines the attribute used to match the real-time data with the modeled entity.
- `validTimeAtAttribute` sets the time reference for the data.
- `mqttTopicPattern` defines the MQTT topic structure to listen for updates.
- `mqttBrokerHostName` specifies the MQTT broker providing the data stream.

---

The changes include the detailed MQTT configuration and the mapping to the entity attributes. Let me know if you need more adjustments!