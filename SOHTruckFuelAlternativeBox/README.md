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
| `refuelTimeInMinutes`     | **Legacy, unused.** The refuel duration now emerges from the charging power |
| `mass`                    | Total mass in kg (legacy; ignored when `tareMass` > 0)              |
| `tareMass`                | Unladen mass in kg (tractor + trailer). If > 0: `mass = tareMass + payload` |
| `payload`                 | Default cargo mass in kg; override per run via the `Payload` column in `semi_truck_initializer.csv` |
| `dragCoefficient`, `rollingResistance` | Optional road-load coefficients (defaults 0.6 / 0.006) |
| `chargeTargetSoc`         | Optional SoC (0–1) at which a stop ends; `<= 0` = default (0.8 battery, 1.0 fuel/hydrogen) |
| `maxChargePowerKw`        | Optional vehicle charge/fill power limit; `<= 0` uses the carrier default |
| `FuelStrategy`            | `Linear` or `RoadLoad`                                              |
| `restStrategy`            | `LegalMinimum` (default) or `ChargeWhileResting`, see below         |
| `restPlanningMarginMinutes` | Optional: legal driving time left at which the stop search starts (default 30) |

### Mercedes-Benz eActros 600 (`MercedesBenzEActros600`)

Sources: Mercedes-Benz Trucks eActros 600 specification; ADAC test (Sept 2024).

| Parameter | Value | Basis |
|---|---|---|
| `power` | 400 kW | continuous (600 kW peak) |
| `maxFuelCarrierAmount` | 600 kWh | usable (621 kWh nominal, ~95 % usable per ADAC) |
| `maxChargePowerKw` | 400 kW | CCS2 (MCS 1000 kW not yet modelled) |
| `tareMass` | 18,700 kg | tractor ≈ 11,700 kg + **assumed** 7,000 kg trailer |
| `payload` | 21,300 kg | gives the 40 t gross combination weight of the 500 km claim |
| `maxSpeed` | 25 m/s | 90 km/h (not officially published) |
| `length`/`width`/`height` | 16.5 / 2.55 / 4.0 m | frontal area 10.2 m² |
| `dragCoefficient`, `rollingResistance`, `tank2wheel` | 0.50, 0.005, 0.90 | **literature estimates, not published by Mercedes, not calibrated** |
| `fuelConsumptionPer100km` | 90 kWh | nominal value for the refuel range estimate (ADAC measured ≈ 88) |

### Example truck types (from `resources/semi_truck.csv`)

| Type                 | Carrier    | Capacity    | η     | Cons./100km | Refuel | Strategy |
|----------------------|------------|-------------|-------|-------------|--------|----------|
| SmallTruck           | Fuel       | 100 L       | 0.35  | 17 L        | 5 min  | RoadLoad |
| MediumLoadTruck      | Fuel       | 100 L       | 0.35  | 18 L        | 5 min  | Linear   |
| SmallElectricTruck   | Battery    | 150 kWh     | 0.85  | 70 kWh      | 45 min | RoadLoad |
| SmallHydrogenTruck   | Hydrogen   | 10 kg       | 0.50  | 3.25 kg     | 10 min | RoadLoad |

### Refuel / charging model

Refueling is a process, not a fixed pause: every tick `ChargingProfile` transfers energy at
`min(station power, vehicle power) × taper(SoC)` until the tank is full, and `fuel_consumption` is logged each tick
(negative `consumedAmount` = energy added). Battery power is constant up to 80 % SoC and then tapers linearly to 10 %
of the maximum at 100 % (CC-CV approximation); fuel and hydrogen do not taper. Charging losses are not modelled. A stop ends at `chargeTargetSoc` (default 80 % for batteries, because the CV tail is slow). Without an explicit `chargeTargetSoc`, battery trucks charge only what the remaining route needs (observed consumption × 1.1 plus a 50 km arrival reserve, at most 100 %); if the route needs more than a full battery, the 80 % default applies and another stop follows.

| Carrier  | Default station power | Default vehicle power |
|----------|-----------------------|-----------------------|
| Battery  | 400 kW                | 350 kW                |
| Fuel     | 30 000 kW (~3 L/s)    | 30 000 kW             |
| Hydrogen | 6 000 kW (~0.05 kg/s) | 6 000 kW              |

Battery stations without a known power are assumed to offer Megawatt Charging (1000 kW, `ChargingProfile.MegawattChargingKw`);
the vehicle limit (`maxChargePowerKw`) still applies. Stations from `charging_stations.csv` carry their own power
(`RefuelState.StationPowerKw`); fuel and hydrogen use the carrier default.

#### Charging stations (Deutschlandnetz)
The map data only knows fuel stations (`source_tag = fuel`, no `charging_station` edges), which cannot charge a battery.
Battery trucks therefore search only for `charging_station` edges and otherwise use `resources/charging_stations.csv`;
fuel and hydrogen trucks keep using the `fuel`/`services` edges and `refuel_stations.csv`.

`charging_stations.csv` is generated by `tools/build_charging_stations.py` from the Deutschlandnetz site list
(`resources/deutschlandnetz.json` is the Mobilithek catalog entry whose distribution URL points to the CSV; data by
NOW GmbH / Nationale Leitstelle Ladeinfrastruktur, licence CC BY 4.0). Columns: `id,name,lat,lon,powerKw,chargePoints`.
- 497 sites (377 in operation, 120 under construction - all treated as finished).
- `name` is the operator; the data set has no site names.
- `powerKw` is the power per charge point (`summe_ladeleistung / anzahl_lp`); it is 200 kW for every site, i.e. these
  are HPC points for cars and trucks alike, **not** Megawatt Charging. An empty value means unknown (MCS is assumed).
- The data does not say whether a site has truck-suitable bays.
- A listed station is only used if the road graph can route to it; unreachable ones are skipped.

### Breaks and rests (EU 561/2006, legal minimum)

`DrivingTimeTracker` counts driving time (ticks in which the truck is not stopped by a rest, refuel or accident):

- 45 min break after 4.5 h of driving; it may be split into **15 min followed by 30 min**, never 30 then 15.
- Daily rest (reduced, 9 h) after 9 h of driving. A daily rest also resets the break.
- Every stationary stop, including charging stops, is credited. A charging stop therefore also counts as break or
  rest if it is long enough; the driver is assumed to rest while the truck charges.
- The driver starts looking for a stop `restPlanningMarginMinutes` before the limit, since the facility has to be
  reached in time. No stop is planned if the destination is reached within the legal time. If the nearest facility is
  further away than the margin allows, the limit is exceeded (no violation is logged).
- Not modelled: 10 h driving days, split daily rest, weekly limits.

`restStrategy` selects where the required stop takes place (`IRestStrategy`, analogous to `FuelStrategy`):

| Strategy             | Behaviour |
|----------------------|-----------|
| `LegalMinimum`       | Breaks and rests at rest areas. |
| `ChargeWhileResting` | Battery trucks that cannot reach the destination with the current charge (range at the observed consumption < remaining route + low-fuel threshold) rest at a charging station: the stop lasts at least the required rest and charges to `chargeTargetSoc`, or to 100 % during a daily rest. Otherwise a rest area is used. |

A combined stop is logged as `Refuel` in the `rests` table.

Rest areas come from `source_tag` edges within 100 km ahead, else from `rest_areas.csv`. That list is sparse, so a CSV facility is only used if it is within 10 km of the route ahead; otherwise the driver stops at the roadside on a route node about 5 km ahead (no detour). Charging stations are never replaced by roadside stops.

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

Connection parameters used for logging are read from `SOHTruckFuelAlternativeBox/.env`.
This can be set to the same values as found in the respective `config.json` file.
See `SOHTruckFuelAlternativeBox/.env.example` for more details.

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
- **Charging** has no losses, no station queueing and ends at the target SoC (default 80 % for batteries). Station power is not yet read from data.
- **Rolling resistance** is currently a constant (`Crr` (0.005 for the eActros, default 0.006)). In reality it depends on speed and load --
  see the referenced paper in `RoadUser.cs`.
- **Drag coefficient** defaults to `0.6` for all trucks. Per-type values would improve accuracy.
