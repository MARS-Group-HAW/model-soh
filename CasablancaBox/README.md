# CasablancaBox 🚍

Simulation that runs the SOH model in four modes:

> * **bus** – `BusDriver` + `PassengerTraveler` on `CarDriving` + `Walking` graphs
> * **tram** – `TramDriver` + `PassengerTraveler` on `Tram` track + `Walking` (wire tram as `Ferry` or `TrainDriving`)
> * **bicycle** – `Coming Soon`
> * **walk** – `HumanTraveler`s on `Walking` only

The app auto-picks a `config_*.json` per mode (overridable via `--config=...`) and validates that the required graph modalities are present before the simulation starts.

---
## Area of Interest
The following image illustrate the area of interst that was chosen for the scenario.
![alt text](https://i.ibb.co/5h8yctK5/image.png)

## Entity Types
Coming Soon
## Agent Types
Coming Soon
## Layer Types
Coming Soon
## Requirements
* .NET SDK 8.0+
* The SOHModel project available and referenced.
* GeoJSON/CSV resources under a `resources/` directory (see structure below).
* **Legacy GTFS 1.7.1 NuGet package**: If you keep this package, it will emit an `NU1701` warning because it targets .NET Framework. Remove it if you don’t use GTFS, or suppress the warning.

---

## Project Layout

# CasablancaBox 🚍

A minimal, switchable .NET 8 simulation app that runs the SOH model in four modes:

> * **bus** – `BusDriver` + `PassengerTraveler` on `CarDriving` + `Walking` graphs
> * **tram** – `TramDriver` + `PassengerTraveler` on `Tram` track + `Walking` (wire tram as `Ferry` or `TrainDriving`)
> * **train** – `TrainDriver` + `PassengerTraveler` on `TrainDriving` + `Walking`
> * **walk** – `HumanTraveler`s on `Walking` only

The app auto-picks a `config_*.json` per mode (overridable via `--config=...`) and validates that the required graph modalities are present before the simulation starts.

---

## Requirements

* .NET SDK 8.0+
* The SOHModel project available and referenced.
* GeoJSON/CSV resources under a `resources/` directory (see structure below).
* **Legacy GTFS 1.7.1 NuGet package**: If you keep this package, it will emit an `NU1701` warning because it targets .NET Framework. Remove it if you don’t use GTFS, or suppress the warning (see [Noise control](#noise-control-optional)).

---

## Project Layout

```text
CasablancaBox/
├── CasablancaBox.csproj
├── Program.cs
├── config_bus.json
├── config_tram.json
├── config_train.json
├── config_walking.json
├── resources/
│   ├── casa_sidi_maarouf_walk_graph.geojson
│   ├── casa_sidi_maarouf_drive_graph.geojson
│   ├── tram_graph.geojson            # if tram-as-ferry, edges carry "Ferry" modality
│   ├── train_graph.geojson           # if tram-as-train or train mode, edges carry "TrainDriving"
│   ├── stations.geojson              # bus or tram/train stations
│   ├── routes.geojson                # bus route network if BusLayer needs it
│   ├── bus_300_line.csv              # BusRouteLayer line definition
│   ├── bus_driver_schedule_casablanca.csv
│   ├── passenger_traveler_schedule.csv
│   └── bus.csv / tram.csv / train.csv   # entity catalogs as needed
└── output/
    └── *.geojson, *.csv (simulation outputs)
```
Warning: Ensure all resources and configs are copied to the build output by adding this to your CasablancaBox.csproj!

---

## Build
```bash
dotnet build
```
## Running the model
```bash
dotnet run
```
##### Explicit modes
* dotnet run bus
* dotnet run tram
* dotnet run bicycle
* dotnet run walk
* dotnet run all
##### With flags
* dotnet run --mode=bus --config=config_bus.json --log=Debug

## Visualization
The agents' movement throughout a simulation can be visualized in kepler.gl.
The static resources like the routes or the stations can be added from the \resources folder by into kepler.gl via drag-and-drop.
Additionally, the *_trips.geojson can be added to visualize the agents' movement and interactions over time