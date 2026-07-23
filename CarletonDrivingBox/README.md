# SOHCarletonDrivingBox

Car evacuation on the Carleton campus drive network. Cars spawn from parking lots P1–P7 on a schedule, route on `resources/campus_drive_graph.geojson`, and exit at configured destinations.

![Campus parking lots P1–P7 and exits](docs/campus_parking_lots.png)

![Spawn boxes and parking aisles](docs/parking_lot_spawn_boxes.png)

Parking lots **P1–P7** and the two main exits: **Colonel By** (south-west) and **Bronson Avenue** (north-east). Scenario 07 uses an additional emergency exit at Bronson Ave & Raven Rd for P3 and P4. Scenario 08 blocks Bronson & University Dr (Stadium Way stays open). Scenario 09 is the same as 08 with the Raven→Bronson emergency link also available. Scenario 10 keeps baseline timing and the base graph, but sends P6 to the SW Meadowlands point (same as P1/P2) instead of Brewer Park.

| Lot | Real campus area | Egress onto |
|-----|------------------|-------------|
| P1 | OSM Library Rd / CIMS aisles (west strip + entrance + east loop) | Library Rd |
| P2 | Unnamed lot west of Campus Ave (near P2 junction) | Campus Ave |
| P3 | OSM P3 Raven Rd grid (aisles + east egress) | Raven Rd |
| P4 | OSM P4 aisle loop + short curb cut (University Dr straightened) | University Dr |
| P5 | OSM **P5** athletics lot (Stadium Way / Bronson) | Stadium Way |
| P6 | OSM **P6** large western lot (west of P18) | Campus Ave & P6 |
| P7 | OSM **P7** large northern lot | Roundabout |

Footprints come from OpenStreetMap parking polygons; aisles use OSM `highway=service` inside each lot (plus a light inset grid on large lots). Narrow egress funnels connect each lot to its campus-road attach node. Pre-built graphs and parking layers live under `resources/` (no OSM rebuild step for analysis).

---

## ModelDescription

In `Program.cs`, a `ModelDescription` registers the campus-specific agent and layers:

```csharp
var description = new ModelDescription();
description.AddLayer<CarletonCarLayer>("CarLayer");
description.AddAgent<CarletonCarDriver, CarletonCarLayer>();
description.AddEntity<Car>();
description.AddLayer<CarletonCarDriverSchedulerLayer>();
```

| Type | Role |
|------|------|
| `CarletonCarLayer` | Drive graph layer (extends stock `CarLayer`) |
| `CarletonCarDriver` | Car agent for this box (extends stock `CarDriver` behaviour with campus spawn/routing fixes) |
| `CarletonCarDriverSchedulerLayer` | Spawns drivers from a schedule CSV (`startLat`/`startLon`/`destLat`/`destLon`) |
| `Car` | Vehicle entity |

**Why a separate driver type?** Stock `CarDriver` in `SOHModel` is unchanged so other boxes (e.g. `SOHTravellingBox`) keep working. Campus-specific behaviour lives in `CarletonCarDriver.cs` in this project only.

Agent and layer names in the JSON config must match the types above.

---

## Scenarios

Each variant has its own config under `configs/`:

| Config | Description |
|--------|-------------|
| `configs/config_scenario_01.json` | Instant deploy at 06:00; P1/P2 → SW evac (Meadowlands); P3–P7 → NE evac box |
| `configs/config_scenario_02.json` | P6 delayed 1 h |
| `configs/config_scenario_03.json` | P6 delayed 2 h |
| `configs/config_scenario_04.json` | P3 delayed 1 h, P6 delayed 2 h |
| `configs/config_scenario_05.json` | P3 + P4 delayed 1 h, P6 delayed 2 h |
| `configs/config_scenario_06.json` | P3 + P4 delayed 1 h, P6 delayed 1.5 h |
| `configs/config_scenario_07.json` | Baseline timing; alternate graph and P3/P4 exit |
| `configs/config_scenario_08.json` | Bronson & University Dr exit blocked; P1/P2 → SW; P3–P7 → Brewer Park (often Stadium Way) |
| `configs/config_scenario_09.json` | Same as 08 + Raven→Bronson emergency open; P1/P2 → SW Meadowlands; P3–P7 → Brewer Park |
| `configs/config_scenario_10.json` | Baseline timing; base graph; P1/P2/P6 → SW Meadowlands; P3–P5/P7 → Brewer Park |

`config.json` at the project root is a shortcut for scenario 01 (same as `configs/config_scenario_01.json`, writes to `results/scenario_01/`).

Example config layout:

```json
{
  "globals": {
    "startPoint": "2021-10-11T06:00:00",
    "endPoint": "2021-10-11T10:01:00",
    "csvOptions": { "outputPath": "results/scenario_01" }
  },
  "agents": [{ "name": "CarletonCarDriver", "count": 3300 }],
  "layers": [
    { "name": "CarLayer", "file": "resources/campus_drive_graph.geojson" },
    { "name": "CarletonCarDriverSchedulerLayer", "file": "resources/schedules/scenario_01_schedule.csv" }
  ],
  "entities": [{ "name": "Car", "file": "resources/car.csv" }]
}
```

The schedule file belongs on **`CarletonCarDriverSchedulerLayer`**, not on the agent.

---

## Resources

| Path | Role |
|------|------|
| `resources/campus_drive_graph.geojson` | Drive network (scenarios 01–06, 10), includes parking aisles |
| `resources/campus_drive_graph_scenario_07.geojson` | Drive network for scenario 07 |
| `resources/campus_drive_graph_scenario_08.geojson` | Drive network for scenario 08 (Bronson & University Dr exit removed; Stadium Way kept) |
| `resources/campus_drive_graph_scenario_09.geojson` | Drive network for scenario 09 (s08 graph + Raven→Bronson emergency link) |
| `resources/parking_lot_spawn_boxes.geojson` | Polygon spawn AOIs for P1–P7 |
| `resources/parking_lot_aisles.geojson` | Parking aisle + narrow egress edges (also merged into drive graphs) |
| `resources/parking_lot_spawn_candidates.json` | Interior aisle nodes; scheduler picks one at random per car |
| `resources/parking_lot_spawns.csv` | Interior spawn lat/lon + box bounds per lot |
| `resources/schedules/scenario_XX_schedule.csv` | Spawn windows and coordinates per lot |
| `resources/schedule_base.csv` | Lot deploy windows and coordinates for scenarios 01–06 |
| `resources/schedule_base_07.csv` | Lot deploy windows and coordinates for scenario 07 |
| `resources/schedule_base_08.csv` | Lot deploy windows and coordinates for scenario 08 |
| `resources/schedule_base_09.csv` | Lot deploy windows and coordinates for scenario 09 |
| `resources/schedule_base_10.csv` | Lot deploy windows and coordinates for scenario 10 |
| `resources/parking_lot_schedules/scenario_XX.csv` | Per-lot delay offsets (`initEventInSec`) |
| `resources/car.csv` | Car entity parameters |
| `resources/sim_road_lengths.csv` | Road segment lengths (heatmap scripts) |

---

## Outputs

Per-scenario results are written to the folder set in `globals.csvOptions.outputPath` (e.g. `results/scenario_01/`):

| File | Content |
|------|---------|
| `CarletonCarDriver.csv` | Per-tick agent state |
| `CarletonCarDriver_trips.geojson` | Completed trip geometries |

---

## Post-run analysis (optional)

### Interactive notebook

`analyze_scenario.ipynb` — pick scenario 1–10, optionally run the simulation, then view summary charts. A DEVS comparison section is stubbed for later.

```bash
python3 -m venv .venv
source .venv/bin/activate          # Windows: .venv\Scripts\activate
pip install -r requirements.txt
python -m ipykernel install --user --name=carleton-driving --display-name="Carleton Driving"
jupyter notebook analyze_scenario.ipynb
```

Set `SCENARIO = 7` and `RUN_SIMULATION = True` (or `False` to only re-analyze existing `results/scenario_XX/` output).

### Scripts

Python 3 scripts under `scripts/`:

| Script | Purpose |
|--------|---------|
| `scripts/analyze_run.py` | Summary stats and evacuation curve for one scenario |
| `scripts/analyze_all_scenarios.py` | Same for all scenarios 01–10 |
| `scripts/analyze_and_heatmap.py` | Analyze + heatmap matrix + plot for one scenario |
| `scripts/build_heatmap_matrix.py` | Road occupancy matrix only |
| `scripts/plot_heatmap.py` | Heatmap image only |
| `scripts/plot_agent_routes.py` | Per-trip route maps (`python scripts/plot_agent_routes.py 01`) |
| `scripts/mars_agent_outputs.py` | Shared paths for agent CSV / trips GeoJSON |
| `scripts/run_all_scenarios.py` | Run simulations 01–10 (`python scripts/run_all_scenarios.py`) |

Pass the path to `CarletonCarDriver.csv` where a script accepts a file argument; trips geojson is resolved from the same folder.

---

## Key files

| Path | Role |
|------|------|
| `CarletonCarDriver.cs` | Campus car agent |
| `CarletonCarLayer.cs` | Campus car layer |
| `CarletonCarDriverSchedulerLayer.cs` | Schedule-based spawning |
| `Program.cs` | Model registration and startup |
| `configs/config_scenario_*.json` | One config per scenario variant |

For more on MARS/SOH configuration, see [mars.haw-hamburg.de](https://mars.haw-hamburg.de).
