# SmartOpenHH Green4Bikes

Green4Bikes is a model that focuses on the following section of the district Harburg in Hamburg, Germany:

![harburg_zentrum](images/harburg.png)

The `Program.cs` contains the simulation configuration for the model logic defined in a given model of the project
structure. Here, a simulation of the model can be configured and executed. The configuration of a model is defined
within a SimulationConfig object in `Program.cs`. Its main components are `Globals`, `LayerMappings`,
and `AgentMappings`.

___

## ModelDescription

In the `main` of `Program.cs`, a ModelDescription object is defined. It contains references to agent types and layer
types that are defined in the model. Below is an example:

```c#
var description = new ModelDescription();
description.AddLayer<CitizenLayer>();
description.AddAgent<Citizen, CitizenLayer>();
```

**Note:** the layer types and agent types added to `description` need to be referenced in the LayerMappings and
AgentMappings (see below).

___

## Globals

This section contains a set of attributes that allow for general configuration of the model. Below is an example and
brief description of the main attributes.

```C#
Globals = {
    StartPoint = startPoint,
    EndPoint = startPoint + TimeSpan.FromHours(24),
    DeltaTUnit = TimeSpanUnit.Seconds,
    ShowConsoleProgress = true,
    OutputTarget = OutputTargetType.SqLite,
    SqLiteOptions =
    {
        DistinctTable = false
    }
}
```

* `StartPoint`: the time at which the simulation begins (type: `DateTime`)
* `endPoint`: the time at which the simulation ends (type: `DateTime`)
* `DeltaTUnit`: the duration of a time step in the simulation (type: `TimeSpanUnit`)
* `ShowConsoleProgress`: defines if progress bar is displayed in console during simulation run (type: `boolean`)
* `OutputTarget`: defines the medium in which simulation output is stored (type: `OutputTargetType`)
    * `OutputTargetType.None`: create no output
    * `OutputTargetType.Csv`: write output into a .csv file and store it in bin/Debug/netcoreapp3.1
    * `OutputTargetType.SqLite`: write output into a SQLite database
    * `OutputTargetType.PostgreSql`: write output into a PostgreSQL database
    * `OutputTargetType.MongoDB`: write output into a MongoDB database
    * **Note:** when writing results into a database, a database and a valid connection to it need to be set up

For more information, please see [https://mars.haw-hamburg.de](https://mars.haw-hamburg.de).

___

## Layer Mappings

In this section, the layer types that are defined in the model logic (and that were added to `description` above) are
configured and populated with external data.

```c#
LayerMappings =
{
    new LayerMapping
    {
    Name = nameof(CitizenLayer),
	File = Path.Combine(ResourcesConstants.GraphFolder,"harburg_zentrum_walk_graph.graphml")
	}
}
```

For each layer type added to `description`, a LayerMapping needs to be defined. A LayerMapping consists of at least two
keys:

* `Name`: (the name of the layer type, which needs to match the type specified in `description`)
* `File`: (the external data that is to be used to populate the layer)

___

## AgentMappings

In this section, the agent types that are defined in the model logic (and that were added to `desciption` above) are
configured. An initialization file can be specified as well.

```c#
AgentMappings =
{
    new AgentMapping
    {
        Name = nameof(Citizen),
        InstanceCount = 1,
        OutputTarget = OutputTargetType.Csv,
        File = Path.Combine("res", "agent_inits", "CitizenInit10k.csv")
	}
}
```

For each agent type added to `description`, an AgentMapping needs to be defined. An AgentMapping consists of at least
three keys:

* `Name`: the name of the layer type, which needs to match the type specified in `description`)
* `InstanceCount`: the number of agents of this agent type that are to be instantiated
* `File`: the path to the agent type's initialization file (see [SOHResources](../SOHResources/README.md) documentation
  for more information)

The `OutputTarget` key defines the medium into which simulation output data is to be saved. It is optional at the
AgentMapping level. Alternatively, it can be defined globally (see "Globals" above).