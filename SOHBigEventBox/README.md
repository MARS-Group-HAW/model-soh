# SOHBigEventBox


___

## ModelDescription

This model is a simple example of a simulation model that uses the SOH framework. The model simulates the movement of
visitors after a big event at the Barclays arena in Hamburg. The visitors start spawning at the entrances of the arena
and move to their destinations using their preferred mode of transport. The visitors can use i.e. cars, bikes or buses to
reach their destinations. The model uses a simple walking layer to simulate the movement of the visitors.

In the `main` of `Program.cs`, a ModelDescription object is defined. It contains references to agent types and layer
types that are defined in the model. Below is an example:

```c#
var description = new ModelDescription();
description.AddLayer<BaseWalkingLayer>();
description.AddAgent<Visitor, BaseWalkingLayer>();
```

**Note:** the layer types and agent types added to `description` need to be referenced in the LayerMappings and
AgentMappings (see below).

___

## Globals

This section contains a set of attributes that allow for general configuration of the model (see `config.json`). Below is an example and
brief description of the main attributes.

```json
"globals": {
"deltaT": 1,
"startPoint": "2024-07-16T22:30:00",
"endPoint": "2024-07-17T02:30:00",
"deltaTUnit": "seconds",
"console": true
}
```

* `startPoint`: the time at which the end of the event and the simulation begins (type: `DateTime`)
* `endPoint`: the time at which the simulation ends (type: `DateTime`)
* `deltaT`: the duration of a time step in the simulation (type: `TimeSpanUnit`)
* `console`: defines if progress bar is displayed in console during simulation run (type: `boolean`)
* `deltaTUnit`: defines the unit of the time step (type: `TimeSpanUnit`)

For more information, please see [https://mars.haw-hamburg.de](https://mars.haw-hamburg.de).

___

## Layer Mappings

In this section, the layer types that are defined in the model logic (and that were added to `description` above) are
configured and populated with external data.

```json
{
  "name": "BaseWalkingSchedulerLayer",
  "file": "resources/visitor_spawning.csv"
}
```

For each layer type added to `description`, a LayerMapping needs to be defined. A LayerMapping consists of at least two
keys:

* `name`: (the name of the layer type, which needs to match the type specified in `description`)
* `file`: (the external data that is to be used to populate the layer)

For our Big Event Simulation, the `BaseWalkingSchedulerLayer` is populated with the `visitor_spawning.csv` file. This
file contains the information about the visitors that are to be spawned at the beginning of the simulation. If you look 
at the content of the file, you will see that it contains the following columns:
* `startTime`: the time at which the first visitor of a certain row will be spawned
* `endTime`: the time at which the visitors stop being spawned
* `spawningIntervalInMinutes`: the time interval between the spawning of a number (`spawningAmount`) visitors
* `spawningAmount`: the number of visitors to be spawned at each interval
* `usesCar`: probability that the visitor uses a car
* `usesBus`: probability that the visitor uses a bus
* `usesTrain`: probability that the visitor uses a train
* `usesBike`: probability that the visitor uses a bike
* `source`: the spawning source of the visitor (type: `Geometry`)
* `destination`: the destination of the visitor (type: `Geometry`)
* `discriminator`: the discriminator of the visitor

You can play around with the values in the file to see how the simulation output changes. For example, you can increase
the `spawningAmount` to spawn more visitors at each interval, or you can change the `spawningIntervalInMinutes` to
spawn visitors more or less frequently. The source and the destination will calculate the nearest node to the given
coordinates and assign it to the visitor. So if you want to change the location of the simulation you will need another 
file with a different graph. Don't forget to change the new file in the `config.json` layer mappings.

A new ParkingLayer `BarclaysParkingLayer` was added to the model. This layer is used to simulate the parking spaces 
around the arena. When you start the program, the five parking spaces that belong to the arena are filled with cars. 
This specific layer stores every car that is parked in the parking spaces in a list called `ParkedCars`. The main agent
of this simulation, the `Visitor`, then gets a reference to a random car from the list of parked cars if his preferred 
modality is `CarDriving`. 

___

## AgentMappings

In this section, the agent types that are defined in the model logic (and that were added to `description` above) are
configured. An initialization file can be specified as well.

```json
{
      "name": "TrainDriver",
      "outputs": [
        {
          "kind": "trips",
          "outputConfiguration": {
            "tripsFields": ["LoadPercentage"]
          }
        }
      ]
}
```

For each agent type added to `description`, an AgentMapping needs to be defined. An AgentMapping consists of at least
three keys:

* `name`: the name of the agent type, which needs to match the type specified in `description`)
* `outputs`: 
  * `kind`: the kind of output that is to be saved
  * `outputConfiguration`: the configuration of the output
    * `tripsFields`: the fields that are to be saved in the output

Visitors are multi capable agents, which means they can use different modes of transport.
