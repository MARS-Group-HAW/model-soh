# Energy models

## Carrier units

| `FuelCarrierType` | Native unit | Energy per unit |
|---|---|---|
| `Fuel` | L | 36 MJ (diesel lower heating value) |
| `Battery` | kWh | 3.6 MJ |
| `Hydrogen` | kg | 120 MJ (lower heating value) |

`FuelCarrierEnergyConverter` converts native amounts to/from Joules. `EnergyStore` applies drivetrain efficiency,
capacity limits and battery recuperation. Strategies compute energy at the wheels, not energy drawn from the store.

## Consumption strategies

`Linear` consumes a distance-proportional amount:

```text
amount = fuelConsumptionPer100km / 100 * distanceKm
```

`RoadLoad` models rolling resistance, aerodynamic drag, gradient and acceleration:

```text
F = Crr*m*g + 0.5*rho*Cd*A*v^2 + m*g*sin(atan(incline/100)) + m*a
wheelPower = F*v
wheelEnergy = wheelPower*deltaT
```

Mass includes payload; frontal area is width times height. Local grade uses the edge's elevation profile, with
the incline tag as fallback. Positive wheel energy is divided by tank-to-wheel efficiency when drawn from storage.
Only batteries recuperate negative wheel energy, multiplied by tank-to-wheel efficiency and a 0.9 recovery factor.

`road_load.energyjoules` is wheel energy. `fuel_consumption.consumedamount` is the net native carrier amount,
including losses or energy added. See [output](output.md).

## Planning and limitations

Planning uses the larger of nominal consumption and trip-average observed wheel energy per km, trusting observations
after 20 km. Reachability additionally checks elevation gains along the whole approach; it is not simply a fixed
nominal range. See [driving behaviour](driving-behaviour.md) for reserve, legal-time and stop-selection rules.

The road-load model is an approximation: rolling resistance and air density are constant, coefficients may be
assumed rather than calibrated, and no idling or auxiliary-load consumption is modelled. Planning does not predict
future acceleration, weather, traffic exactly; an accepted plan can become unsafe and require replacement.
Energy exhaustion is resolved at simulation-tick granularity, and an empty truck cannot continue driving.

Charging power, taper, targets and missing charging losses are documented in [facilities](facilities.md).
