# SOHTruckFuelAlternativeBox

SOHTruckFuelAlternativeBox is a research simulation built on top of **SOHLogisticsBox**.
Its purpose is to compare different **propulsion technologies** -- conventional diesel, battery-electric (BEV), and
hydrogen fuel-cell (FCEV) — by running semi-trucks through realistic long-haul routes on the German highway network.

The box reuses the full `SemiTruck` / `SemiTruckDriver` / `SemiTruckLayer` stack from `SOHModel` and extends it with:

- A pluggable **`IFuelConsumptionStrategy`** interface, so fuel/energy consumption models can be swapped per truck type.
- A **`FuelCarrierType`** enum (`Fuel`, `Battery`, `Hydrogen`) that makes the carrier unit explicit.
- A **`FuelCarrierEnergyConverter`** utility for consistent Joule-based conversions across all carrier types.

___

## Fuel Consumption Strategies

### `Linear`

Consumption is proportional to distance, using the per-type `fuelConsumptionPer100km` value:

```
amount_used = (fuelConsumptionPer100km / 100) × distanceDrivenKm
```

Simple and fast. Suitable for large-scale runs where physics fidelity is less important.

### `RoadLoad`

Physics-based model derived from the road load equation:

```
F_total = F_rolling + F_drag + F_gradient + F_accel

F_rolling  = Crr × m × g
F_drag     = 0.5 × rho × Cd × A × v²
F_gradient = m × g × sin(arctan(incline/100))
F_accel    = m × a

Power = F_total × v
Energy = (Power / η_tank2wheel) × delta_t
```

Accounts for real driving conditions: speed, acceleration, road slope, frontal area, and drivetrain efficiency.

**Battery-electric trucks additionally support recuperation** (regenerative braking):
when `F_total < 0` (e.g. downhill/decelerating), energy is fed back into the battery:

```
Energy_recuperated = |Power| × η_tank2wheel × 0.9 × delta_t
```

Range estimation for the `RoadLoad` strategy still uses the linear `fuelConsumptionPer100km` fallback for routing
decisions (see TODO comment in `RoadLoadFuelConsumptionStrategy.cs` for more accurate alternatives).

___

## Energy Carrier Units

The `FuelCarrierType` determines the unit for all tank/consumption values:

| Type        | Unit | Energy per unit        |
|-------------|------|------------------------|
| `Fuel`      | L    | 36 MJ/L (diesel LHV)   |
| `Battery`   | kWh  | 3.6 MJ/kWh             |
| `Hydrogen`  | kg   | 120 MJ/kg (H₂ LHV)     |

Conversions are handled by `FuelCarrierEnergyConverter` (`ToJoules` / `FromJoules`).

___

## SemiTruck CSV Format (`semi_truck.csv`)

Each row defines a truck **type** (prototype) that can be referenced by drivers in the initializer:

```
type,maxAcceleration,maxDeceleration,maxSpeed,length,height,width,trafficCode,
  passengerCapacity,velocity,mass,maxIncline,accidentsPerYear,power,
  fuelCarrierType,maxFuelCarrierAmount,tank2wheel,fuelConsumptionPer100km,
  refuelTimeInMinutes,FuelStrategy
```

| Column                    | Description                                                         |
|---------------------------|---------------------------------------------------------------------|
| `fuelCarrierType`         | `Fuel`, `Battery`, or `Hydrogen`                                    |
| `maxFuelCarrierAmount`    | Tank/battery capacity in the native unit (L / kWh / kg)             |
| `tank2wheel`              | Drivetrain efficiency (0–1); diesel ≈ 0.35, BEV ≈ 0.85, FCEV ≈ 0.50 |
| `fuelConsumptionPer100km` | Nominal consumption per 100 km in the native unit (linear baseline) |
| `refuelTimeInMinutes`     | Stop duration at a refuel/recharge station (full-tank model)        |
| `FuelStrategy`            | `Linear` or `RoadLoad`                                              |

### Example truck types (from `resources/semi_truck.csv`)

| Type                 | Carrier    | Capacity    | η     | Cons./100km | Refuel | Strategy |
|----------------------|------------|-------------|-------|-------------|--------|----------|
| SmallTruck           | Fuel       | 100 L       | 0.35  | 17 L        | 5 min  | RoadLoad |
| MediumLoadTruck      | Fuel       | 100 L       | 0.35  | 18 L        | 5 min  | Linear   |
| SmallElectricTruck   | Battery    | 150 kWh     | 0.85  | 70 kWh      | 45 min | RoadLoad |
| SmallHydrogenTruck   | Hydrogen   | 10 kg       | 0.50  | 3.25 kg     | 10 min | RoadLoad |

> **Note:** `refuelTimeInMinutes` models the full stop as a fixed pause. In reality, charging/refueling rates differ
> significantly between technologies. A BEV takes ~45 min for a fast DC charge; a hydrogen truck ~10 min;
> a diesel truck ~5 min.

___

## Driver Initializer (`semi_truck_initializer.csv`)

Each row spawns one `SemiTruckDriver` at simulation start:

```
TruckType,StartLat,StartLon,DestLat,DestLon,DriveMode
SmallElectricTruck,53.537079,10.030239,52.375258,13.307587,3
```

| Column         | Description                                   |
|----------------|-----------------------------------------------|
| `TruckType`    | Must match a `type` value in `semi_truck.csv` |
| `StartLat/Lon` | Departure coordinate                          |
| `DestLat/Lon`  | Destination coordinate                        |
| `DriveMode`    | 2 = shortest path, 3 = OSM-based route        |

The default scenario sends a `SmallElectricTruck` from Hamburg (Rothenburgsort) to Berlin (Marzahn), roughly 290 km,
demonstrating automatic recharging stops along the way.

___

## Configuration (`config.json`)

```json
{
  "globals": {
    "deltaT": 1,
    "startPoint": "2026-01-08T06:00:00",
    "endPoint":   "2026-01-09T06:00:00",
    "deltaTUnit": "seconds",
    "npgSqlOptions": {  }
  }
}
```

___

## Local Setup

### Prerequisites

- .NET 9 SDK
- Docker (for the PostgreSQL/PostGIS container)
- The road network GeoJSON (not in git, ~220 MB) — see [Road Network File](#road-network-file) below

### 1. Start the database

```bash
cd SOHTruckFuelAlternativeBox
docker compose up -d
```

This starts a PostGIS container (`mars_postgis`) on port 5432.
On first run the database `soh_truck_fuel_alternative` must be created manually:

```bash
docker exec -e PGPASSWORD=admin mars_postgis \
  psql -U mars_soh_logistics -c "CREATE DATABASE soh_truck_fuel_alternative;"
```

Connection parameters are read from `SOHTruckFuelAlternativeBox/.env` (or override via `MARS_CONFIG_PATH`).

### 2. Road network file

The GeoJSON is shared with `SOHLogisticsBox`. Place or symlink it at:

```
SOHTruckFuelAlternativeBox/resources/autobahn_und_bundesstrassen_deutschland_elevation_21.geojson
```

### 3. Run

```bash
cd SOHTruckFuelAlternativeBox
dotnet run
```

Or use the helper script (sets env vars from `.env` and runs):

```bash
./simulate.sh
```

Expected output ends with:
```
Executed iterations <N> lasted <HH:MM:SS>
```

___

## Road Network File

`autobahn_und_bundesstrassen_deutschland_elevation_21.geojson` — the same preprocessed GeoJSON used by
`SOHLogisticsBox`. It contains all German highways and federal roads with elevation data and POI nodes for
fuel stations and rest areas embedded in the graph.

See `SOHLogisticsBox/README.md` (Map Export & Preprocessing) for full details on how it was built.

___

## Data Logging

When a PostgreSQL connection is available, the `RoadLoad` strategy logs per-tick physics data to the
`road_load_entity` table via `PostgresDbLogger`. Fields include velocity, acceleration, mass, frontal area,
road incline, individual force components, total power, and energy transferred.

This data can be used to compare energy efficiency across fuel types under identical route and load conditions.

___

## Known Limitations & TODOs

- **Range estimation with RoadLoad** uses the linear `fuelConsumptionPer100km` as a fallback for routing decisions.
  A more accurate horizon-based or steady-state prediction would improve refueling stop placement (see
  `RoadLoadFuelConsumptionStrategy.cs`).
- **Refueling is modeled as a fixed-duration full stop** (not a rate). A future improvement would model charging as
  `energyAmountPerMinute` to allow partial charges and variable stop lengths.
- **Rolling resistance** is currently a constant (`Crr = 0.006`). In reality it depends on speed and load --
  see the referenced paper in `RoadUser.cs`.
- **Drag coefficient** defaults to `0.6` for all trucks. Per-type values would improve accuracy.
