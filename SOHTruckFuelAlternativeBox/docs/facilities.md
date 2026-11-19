# Facilities and charging

## Service model

Trucks stop at a facility, charge or refuel, and rest as required. Facilities are not capacity-limited.

`ChargingProfile` transfers energy each tick at the lesser of station and vehicle power. Battery power is constant
up to 80% SoC, then tapers linearly to 10% of maximum at 100%; fuel and hydrogen do not taper. Charging losses are
not modelled.

An explicit `chargeTargetSoc` fixes the target. Otherwise batteries charge for the remaining route using planning
consumption, a 1.1 distance factor and a 50 km reserve when this fits a full battery; otherwise the default is 80%.
Fuel and hydrogen default to full. During a combined rest, charging may continue towards full without extending
the required rest.

Unknown battery station power assumes megawatt charging, but the truck's `maxChargePowerKw` still caps delivery.

## Station configuration

The optional facility CSV column `powerKw` gives kW per charging point. Missing power uses the carrier default.

## Station data

Battery trucks use `charging_station` graph tags and `resources/charging_stations.csv`. Diesel/hydrogen use
`fuel`/`services` tags and `refuel_stations.csv`; rests use `rest_areas.csv`. A fuel POI is not treated as a charger.
Sites still need a feasible route in the road graph.

`tools/build_charging_stations.py` builds the German charging CSV from Deutschlandnetz data. The source catalog is
`resources/deutschlandnetz.json` (Mobilithek; NOW GmbH / Nationale Leitstelle Ladeinfrastruktur, CC BY 4.0).
Columns are `id,name,lat,lon,powerKw,chargePoints`; `name` contains the operator, and power is per charge point.
The dataset includes sites under construction, treated as operational, and does not guarantee truck-suitable bays.
These are HPC sites, not necessarily megawatt chargers.

If present, `resources/charging_stations_neighbors.csv` is appended by the layer. Generate/update a country's rows
with `tools/build_neighbor_charging_stations.py --country AT --pbf <Geofabrik extract>`; see the script's supported
countries and ID offsets. Its OSM filter selects public DC sites at 150-1500 kW, excludes `hgv=no`, and merges nearby
sites within 100 m. The existing neighbour list covers Austria; neighbour rest/fuel lists are not loaded separately.

For network preprocessing and POI integration, refer to
[SOHLogisticsBox](../../SOHLogisticsBox/README.md#map-export--preprocessing).
