# Carleton campus car evacuation (MARS / SOH)

Scenario 01: scheduler-based car evacuation on the **blueprint Jupyter drive graph** (`edges_drive.geojson` → `resources/campus_drive_graph.geojson`).

## Prerequisites

- .NET 8
- Python 3 with `matplotlib` (for analysis plots)
- `SOHModel` project reference (sibling folder `../SOHModel`)

## Workflow

```
1. Run simulation
2. Analyze run (CSV + trips geojson)
3. Build heatmap matrix (CSV → DEVS-format matrix)
4. Plot heatmap
```

Optional checks after step 2: `plot_agent_routes.py` for per-lot route maps.

---

## Required SOHModel patch + box-local scheduler

**Full documentation for reviewers:** [`docs/SOHMODEL_PATCHES.md`](docs/SOHMODEL_PATCHES.md)

Rebuild the box after any SOHModel edit (`dotnet build SOHCarletonDrivingBox.csproj`).

| Location | Change | Why |
|----------|--------|-----|
| `SOHModel/Car/Model/CarDriver.cs` | `ID = Guid.NewGuid()` | Scheduler spawns via `new CarDriver(...)`; without unique IDs only one car is kept |
| `SOHModel/Car/Model/CarDriver.cs` | `CurrentEdgeId` null/array safety | Campus `osmid` export for heatmaps |
| `CarletonDrivingBox/CarletonCarDriverSchedulerLayer.cs` | Box-local scheduler | Reads `startLat`/`startLon`/`destLat`/`destLon` from schedule CSV (SemiTruck pattern) |

Stock `CarDriverSchedulerLayer` in SOHModel is **not** patched. Lat/lon schedule support lives in the box.

### Config layout (canonical scheduler pattern)

```json
"agents": [{ "name": "CarDriver", "count": 3200, ... }],
"layers": [
  { "name": "CarLayer", "file": "resources/campus_drive_graph.geojson" },
  { "name": "CarletonCarDriverSchedulerLayer", "file": "resources/car_driver_schedule.csv" }
]
```

Schedule CSV on **scheduler layer only** — not on the `CarDriver` agent.

### Run environment

| Task | Where |
|------|--------|
| `dotnet build` / `dotnet run` | **Windows PowerShell** or WSL with `"/mnt/c/Program Files/dotnet/dotnet.exe"` |
| Python analysis scripts | WSL + `.venv` |

WSL does **not** have `dotnet` installed by default — do not use `sudo snap install dotnet` unless you want a separate Linux toolchain.

## Troubleshooting

Use `"console": false` on long runs if the progress bar appears frozen.

```powershell
taskkill /F /IM dotnet.exe 2>$null
dotnet build SOHCarletonDrivingBox.csproj
dotnet run --project SOHCarletonDrivingBox.csproj -- config.json
```

| Symptom | Likely cause |
|---------|----------------|
| `geometry` null on spawn | Using stock `CarDriverSchedulerLayer` instead of `CarletonCarDriverSchedulerLayer` |
| Empty trips geojson | Scheduler `file` missing on layer, or sim window too short |
| Only 1 completed trip | Missing `Guid.NewGuid()` on `CarDriver` |
| Startup `Sequence contains no elements` | Schedule CSV still on `CarDriver` agent `file` (remove it) |
| OOM on full run | `endPoint` too late or CSV + trips both enabled — use `08:59:30` max |

---

### 1. Run simulation

From this folder:

```powershell
dotnet build SOHCarletonDrivingBox.csproj
dotnet run --project SOHCarletonDrivingBox.csproj
```

Or explicitly:

```powershell
dotnet run --project SOHCarletonDrivingBox.csproj -- config.json
```

Uses `config.json` (~3200 cars, `endPoint` `08:59:30`). All outputs go to **`results/`**:

| File | Purpose |
|------|---------|
| `results/CarDriver.csv` | Per-tick agent state (large on full runs) |
| `CarDriver_trips.geojson` | Completed trips (project root) |

---

### 2. Analyze run (all-in-one)

```bash
cd CarletonDrivingBox
source .venv/bin/activate
bash scripts/analyze_and_heatmap.sh
```

Or step by step (defaults read from `results/`):

```bash
python3 scripts/analyze_run.py
```

Writes to **`results/`**:

| File | Content |
|------|---------|
| `summary.csv` | Expected vs completed, completion rate |
| `evac_curve.csv` | Time (s) vs cars on campus |
| `evac_curve.png` | Evacuation curve plot |
| `summary.png` | Deployed vs completed bar chart |
| `lot_deploy_plan.csv` | Per-lot schedule counts |
| `heatmap_matrix.csv` | DEVS-format congestion matrix (from step 3) |
| `heatmap_matrix.png` | Heatmap plot (from step 4) |
| `agent_routes/` | Per-trip route PNGs (optional) |

**Note:** For scheduler runs, trust **`CarDriver_trips.geojson`** (project root) for completions, not unique IDs in the CSV.

Optional route maps per lot:

```bash
python3 scripts/plot_agent_routes.py --lot P1 --limit 20
```

---

### 3. Build heatmap matrix

Same format as DEVS `heatmap_matrix.csv` (20 sim-road columns, vehicles per 100 m, 1 s steps).

**End time is automatic by default:** scans `CarDriver.csv` for the last second any car is still driving (`CurrentlyCarDriving=true`, `GoalReached=false`) — i.e. when the last car leaves campus. Use `--max-time 6000` only if you want to match the DEVS plot window for side-by-side comparison.

```bash
python3 scripts/build_heatmap_matrix.py
```

Optional fixed cap (DEVS comparison window):

```bash
python3 scripts/build_heatmap_matrix.py --max-time 6000
```

Reads (defaults):

- `results/CarDriver.csv` — active drivers per second
- `resources/campus_drive_graph.geojson` — blueprint graph for edge → sim-road mapping
- `resources/sim_road_lengths.csv` — DEVS segment lengths (normalization)

Writes:

- **`results/heatmap_matrix.csv`**

---

### 4. Visualize heatmap

```bash
python3 scripts/plot_heatmap.py
```

Writes **`results/heatmap_matrix.png`** (same style as DEVS: plasma, vmax=20).

---

## Key files

| Path | Role |
|------|------|
| `config.json` | Production run config |
| `docs/SOHMODEL_PATCHES.md` | **SOHModel changes (for professor / reviewers)** |
| `resources/car_driver_schedule.csv` | Spawn schedule (7 lots) |
| `resources/campus_drive_graph.geojson` | Drive network (from blueprint `Download Graph.ipynb`) |
| `resources/sim_road_lengths.csv` | DEVS sim-road lengths for heatmap |
| `scripts/analyze_run.py` | Post-run analysis |
| `scripts/build_heatmap_matrix.py` | Heatmap CSV |
| `scripts/plot_heatmap.py` | Heatmap PNG |
| `scripts/analyze_and_heatmap.sh` | Steps 2–4 in one command |
| `scripts/plot_agent_routes.py` | Per-trip route PNGs |

## Graph updates

After re-running **`blueprint-geovector/Download Graph.ipynb`**, refresh the MARS graph:

```powershell
Copy-Item `
  "...\blueprint-geovector\GeoVectorBlueprint\Resources\edges_drive.geojson" `
  "resources\campus_drive_graph.geojson" -Force
```

Do **not** use the DEVS `carleton_campus_car_roads.geojson` here — that is a simplified graph for the DEVS model only.

## DEVS comparison

| DEVS | MARS equivalent |
|------|-----------------|
| Scenario log CSV | `CarDriver.csv` |
| `output_data/processed/evac_curve.csv` | `results/evac_curve.csv` |
| `output_data/processed/heatmap_matrix.csv` | `results/heatmap_matrix.csv` |
| `analysis/visualize_processed.py` | `analyze_run.py` + `plot_heatmap.py` |
| Completion count | `CarDriver_trips.geojson` feature count |

Heatmap **values** differ (different engine and graph detail); **columns, units, and plot layout** match DEVS.
