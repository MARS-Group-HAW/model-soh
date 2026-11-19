# Output and analysis

MARS writes agent output using `globals.npgSqlOptions`. Separately, `PostgresDbLogger` writes the tables below using
the `.env` connection. The box flushes custom records on normal shutdown.

**Starting a run truncates all registered custom tables**, so export results before running another scenario.
The logger creates missing tables but does not migrate existing schemas. After an output-record schema change,
export any data you need, then migrate or recreate the affected table before running again. In particular, older
`rests` tables need the UUID `phaseid` column; historical IDs must not be fabricated.

## Tables

| Table | Contents |
|---|---|
| `rests` | `Planned`, `Start`, `End` for `Rest`, `Refuel`, or `RestAndRefuel`, with phase UUID, tick, location, energy and remaining route |
| `truck_trips` | One terminal outcome per finished/failed trip, with energy, location and driving-time counters |
| `fuel_consumption` | Native carrier amounts and energy changes, including charging |
| `road_load` | Forces, wheel power and wheel energy from the `RoadLoad` strategy |
| `vehicle_steering_physics` | Per-tick movement and physics |
| `vehicle_steering_decision` | Per-tick steering decisions |

Terminal outcomes include `Completed`, `ZeroEnergy`, `NoRoute`, `NoLegalStop`, and `NoReachableFuel`.
A truck still active at the simulation horizon does not have a terminal trip row; do not infer completion from
the absence of a failure.

`fuel_consumption.consumedamount < 0` means energy was added. `road_load.energyjoules` is energy at the wheels,
not the storage draw; see [energy](energy.md).

## Stop phases

Every accepted stop plan gets a UUID (`rests.phaseid`), shared by its `Planned`, `Start`, and `End` events.
A replacement plan gets a new ID.
Group by `(agentid, phaseid)`, not stop type: a charging stop may acquire a rest requirement while approaching.

Run [`rest_phases.sql`](rest_phases.sql) against the output database for each agent's chronological JSON list of
phases, planned/start/end ticks and full event rows. A null start or end means there is no corresponding event:
the plan may have been abandoned, interrupted or still active at simulation end. There is no explicit cancellation
event, so these cases cannot be distinguished from the phase alone.

## Trip metrics

Run [`facility_metrics.sql`](facility_metrics.sql) for failed-trip counts and physical stop counts.

Console logs focus on failures and errors, not routine lifecycle events. Stop-search failures include the candidate
count, search window and routed rejection counts. Use structured database events for analysis.
