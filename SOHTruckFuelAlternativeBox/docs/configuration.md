# Scenario and truck configuration

Use [SOHLogisticsBox](../../SOHLogisticsBox/README.md) for shared vehicle geometry, road constraints and routing
concepts. This guide covers the energy extension's inputs.

## Scenario

`config.json` selects the simulation interval, graph inputs, driver count and CSVs, and PostgreSQL agent output.
The usual time step is one second. Keep `agents[].count` consistent with the drivers you intend to spawn; changing
the CSV alone does not necessarily change the configured count.

`Program.cs` registers `SemiTruckLayer`, `SemiTruckDriver` and `SemiTruck`. The scheduler registration is commented
out, so its mapping in the configuration does not enable scheduled departures.

## Truck prototypes: `resources/semi_truck.csv`

Each row defines a type referenced by `TruckType` in the driver initializer. Do not copy a fixed full header from
documentation: the CSV itself is the current schema.

| Column | Meaning |
|---|---|
| `fuelCarrierType` | `Fuel` (L), `Battery` (kWh), or `Hydrogen` (kg) |
| `maxFuelCarrierAmount` | Store capacity in the carrier's unit |
| `tank2wheel` | Tank-to-wheel efficiency, between 0 and 1 |
| `fuelConsumptionPer100km` | Nominal consumption per 100 km; also the planning baseline |
| `FuelStrategy` | `Linear` or `RoadLoad`; see [energy](energy.md) |
| `mass` | Total kg, used when `tareMass` is not positive |
| `tareMass`, `payload` | With positive tare mass, total mass is tare plus payload |
| `dragCoefficient`, `rollingResistance` | Road-load coefficients; defaults 0.6 and 0.006 |
| `chargeTargetSoc` | Optional fixed target in (0, 1]; otherwise route-based/default charging |
| `maxChargePowerKw` | Optional vehicle transfer-power cap; non-positive uses the carrier default |
| `restStrategy` | `LegalMinimum` or `ChargeWhileResting` |
| `restPlanningMarginMinutes` | Optional planning margin, default 30 minutes |
| `refuelTimeInMinutes` | Legacy field; service duration now follows delivered energy and power |

`maxSpeed` is in m/s, dimensions in metres and `power` in kW. Capacity is not initial charge: starting energy is
currently supplied by the `FuelConsumptionTracker` constructor, not a per-driver CSV column. For a temporary
low-energy experiment, change its `EnergyStore` initial-amount argument, not `maxFuelCarrierAmount`; this affects
every truck and can invalidate tests that assume a full store.

The `MercedesBenzEActros600` prototype is based on Mercedes-Benz specifications and an ADAC September 2024 test,
but is not a calibrated manufacturer model. Trailer mass, aerodynamic/rolling coefficients and efficiency include
assumptions. Consult the current CSV for scenario values rather than treating the model name as a capacity guarantee.

## Drivers: `resources/semi_truck_initializer.csv`

```csv
TruckType,StartLat,StartLon,DestLat,DestLon,DriveMode,Payload
MercedesBenzEActros600,53.537079,10.030239,52.375258,13.307587,3,20000
```

`TruckType` must match a prototype. Coordinates are geographic latitude/longitude. `DriveMode` selects the shared
route finder (2: shortest, 3: OSM-based). Optional `Payload` overrides the prototype's cargo mass in kg; absent or
negative uses its default. This is an example, not a promise about the current checked-in route.

## Strategies and facilities

Legal/rest policy and tuning constants are covered in [driving behaviour](driving-behaviour.md).
Facility data is covered in [facilities](facilities.md).
Energy and rest strategies are separate extension points: `IFuelConsumptionStrategy` and `IRestStrategy`.
