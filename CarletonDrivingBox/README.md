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

### Scenarios 01–06 (DEVS delay parity)

Scenarios differ **only in parking-lot start delays** — same graph, ODs, and car counts as DEVS.

| Layer | Role |
|-------|------|
| `resources/parking_lot_schedules/scenario_XX.csv` | **Source of truth** — same 7-line format as DEVS (`initEventInSec` per lot) |
| `resources/schedule_base.csv` | Scenario-01 deploy windows + lat/lon (one row per lot) |
| `resources/schedules/scenario_XX_schedule.csv` | Generated MARS scheduler CSV (shifted `startTime`/`endTime`) |
| `configs/config_scenario_XX.json` | Run config — points at schedule file + per-scenario `endPoint` / `results/scenario_XX/` |

Regenerate schedules + configs after editing delays:

```bash
python3 scripts/build_mars_schedules.py
```

Run one scenario:

```powershell
dotnet run --project SOHCarletonDrivingBox.csproj -- configs/config_scenario_03.json
```

Run all six (WSL):

```bash
bash scripts/run_scenarios.sh
```

`config.json` at project root remains a shortcut for **scenario_01** (`resources/car_driver_schedule.csv`).

| Scenario | Delayed lots (`initEventInSec`) |
|----------|----------------------------------|
| 01 | none |
| 02 | P6 +3600s |
| 03 | P6 +7200s |
| 04 | P3 +3600s, P6 +7200s |
| 05 | P3 +3600s, P4 +3600s, P6 +7200s |
| 06 | P3 +3600s, P4 +3600s, P6 +5400s |

### Run environment

| Task | Where |
|------|--------|
| `dotnet build` / `dotnet run` | **Windows PowerShell** (recommended) or WSL with Windows `dotnet.exe` |
| Python analysis scripts | **WSL Ubuntu** + project `.venv` |

WSL does **not** have `dotnet` installed by default — do not use `sudo snap install dotnet` unless you want a separate Linux toolchain. Windows PowerShell does **not** have `python` on PATH by default — use WSL for analysis.

---

## Quick start (scenario 01)

Replace `01` with `02` … `06` for other scenarios. All run outputs for a scenario live in **`results/scenario_XX/`**:

| File | Purpose |
|------|---------|
| `CarDriver.csv` | Per-tick agent state |
| `CarDriver_trips.geojson` | Completed trips |
| `summary.csv`, `evac_curve.png`, … | Analysis outputs (after step 2) |
| `heatmap_matrix.csv`, `heatmap_matrix.png` | Heatmap outputs (after step 3) |

### One-time Python setup (WSL Ubuntu)

```bash
cd /mnt/c/Users/doria/Documents/model-soh/CarletonDrivingBox
python3 -m venv .venv
source .venv/bin/activate
pip install matplotlib
```

### 1. Run simulation

**PowerShell (recommended):**

```powershell
cd C:\Users\doria\Documents\model-soh\CarletonDrivingBox
dotnet build SOHCarletonDrivingBox.csproj
dotnet run --project SOHCarletonDrivingBox.csproj -- configs\config_scenario_01.json
```

**WSL Ubuntu** (uses Windows .NET):

```bash
cd /mnt/c/Users/doria/Documents/model-soh/CarletonDrivingBox
"/mnt/c/Program Files/dotnet/dotnet.exe" build SOHCarletonDrivingBox.csproj
"/mnt/c/Program Files/dotnet/dotnet.exe" run --project SOHCarletonDrivingBox.csproj -- configs/config_scenario_01.json
```

If a previous run is stuck, kill dotnet in PowerShell first: `taskkill /F /IM dotnet.exe`

### 2. Analyze run

**WSL Ubuntu** (activate venv first):

```bash
cd /mnt/c/Users/doria/Documents/model-soh/CarletonDrivingBox
source .venv/bin/activate
python3 scripts/analyze_run.py results/scenario_01/CarDriver.csv
```

Trips geojson is read automatically from the same folder (`CarDriver_trips.geojson`). Analysis writes `summary.csv`, `evac_curve.csv`, `evac_curve.png`, and `summary.png` into **`results/scenario_01/`**.

**From PowerShell** (one-liner into WSL):

```powershell
wsl -d Ubuntu-24.04 bash -lc "cd /mnt/c/Users/doria/Documents/model-soh/CarletonDrivingBox && source .venv/bin/activate && python3 scripts/analyze_run.py results/scenario_01/CarDriver.csv"
```

### 3. Heatmap (optional)

Still in WSL with venv active. Outputs go to the **same folder as the CSV** (`results/scenario_01/`):

```bash
python3 scripts/build_heatmap_matrix.py results/scenario_01/CarDriver.csv
python3 scripts/plot_heatmap.py results/scenario_01/heatmap_matrix.csv
```

Or all analysis + heatmap in one step:

```bash
bash scripts/analyze_and_heatmap.sh results/scenario_01/CarDriver.csv results/scenario_01/CarDriver_trips.geojson
```

Writes `heatmap_matrix.csv` and `heatmap_matrix.png` into `results/scenario_01/`.

### Run all scenarios 01–06

**PowerShell:** run each config in turn, or use WSL:

```bash
bash scripts/run_scenarios.sh
```

(`run_scenarios.sh` uses Windows `dotnet.exe` from WSL.)

---

## Troubleshooting

Use `"console": false` on long runs if the progress bar appears frozen.

```powershell
taskkill /F /IM dotnet.exe 2>$null
dotnet build SOHCarletonDrivingBox.csproj
dotnet run --project SOHCarletonDrivingBox.csproj -- config.json
```

| Symptom | Likely cause |
|---------|----------------|
| `Python was not found` in PowerShell | Use WSL for `python3` (see Quick start); sim stays on PowerShell `dotnet run` |
| `geometry` null on spawn | Using stock `CarDriverSchedulerLayer` instead of `CarletonCarDriverSchedulerLayer` |
| Empty trips geojson | Scheduler `file` missing on layer, or sim window too short |
| Only 1 completed trip | Missing `Guid.NewGuid()` on `CarDriver` |
| Startup `Sequence contains no elements` | Schedule CSV still on `CarDriver` agent `file` (remove it) |
| OOM on full run | `endPoint` too late or CSV + trips both enabled — use `deltaT: 2` (2s ticks) and/or shorter `endPoint` |

---

## Detailed workflow

### 1. Run simulation

See **Quick start** above. Legacy root config:

```powershell
dotnet run --project SOHCarletonDrivingBox.csproj -- config.json
```

Writes to `results/` (no scenario subfolder).

---

### 2. Analyze run

See **Quick start** above. Default (no args) reads `results/CarDriver.csv`:

```bash
source .venv/bin/activate
python3 scripts/analyze_run.py
```

**Note:** For scheduler runs, trust **`CarDriver_trips.geojson`** for completion counts.

Optional route maps per lot:

```bash
python3 scripts/plot_agent_routes.py --lot P1 --limit 20
```

---

### 3. Build heatmap matrix

Same format as DEVS `heatmap_matrix.csv` (20 sim-road columns, vehicles per 100 m, 1 s steps).

**End time is automatic by default:** scans `CarDriver.csv` for the last second any car is still driving (`CurrentlyCarDriving=true`, `GoalReached=false`) — i.e. when the last car leaves campus. Use `--max-time 6000` only if you want to match the DEVS plot window for side-by-side comparison.

```bash
python3 scripts/build_heatmap_matrix.py results/scenario_01/CarDriver.csv
```

Optional fixed cap (DEVS comparison window):

```bash
python3 scripts/build_heatmap_matrix.py results/scenario_01/CarDriver.csv --max-time 6000
```

Reads:

- `results/scenario_XX/CarDriver.csv` — active drivers per second (or pass path as first argument)
- `resources/campus_drive_graph.geojson` — blueprint graph for edge → sim-road mapping
- `resources/sim_road_lengths.csv` — DEVS segment lengths (normalization)

Writes to the **same folder as the CSV**:

- **`results/scenario_XX/heatmap_matrix.csv`**

---

### 4. Visualize heatmap

```bash
python3 scripts/plot_heatmap.py results/scenario_01/heatmap_matrix.csv
```

Writes **`results/scenario_XX/heatmap_matrix.png`** (same style as DEVS: plasma, vmax=20).

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
| Scenario log CSV | `results/scenario_XX/CarDriver.csv` |
| Trips / completions | `results/scenario_XX/CarDriver_trips.geojson` |
| `output_data/processed/evac_curve.csv` | `results/scenario_XX/evac_curve.csv` |
| `output_data/processed/heatmap_matrix.csv` | `results/scenario_XX/heatmap_matrix.csv` |
| `analysis/visualize_processed.py` | `analyze_run.py` + `plot_heatmap.py` |

Heatmap **values** differ (different engine and graph detail); **columns, units, and plot layout** match DEVS.
