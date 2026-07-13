# SOHModel modifications for Carleton campus evacuation

This document describes changes to the shared **SOHModel** library (`../SOHModel`) and the **box-local layers** in `CarletonDrivingBox`. Carleton-specific scheduler and init-spawn logic stay in the box; shared SOHModel scheduler code stays stock.

---

## Summary

| Location | Change | Why |
|----------|--------|-----|
| `SOHModel/Multimodal/Model/CarDriverSchedulerLayer.cs` | **Untouched (stock HEAD)** | Carleton uses box-local scheduler; no shared-library scheduler patches |
| `CarletonDrivingBox/CarletonCarLayer.cs` | Box-local `CarLayer` subclass | Skip init spawn when agent config has no `file` (scheduler-only setups NRE in stock `CarLayer.InitLayer`) |
| `CarletonDrivingBox/CarletonCarDriverSchedulerLayer.cs` | Box-local scheduler | Reads `startLat`/`startLon`/`destLat`/`destLon`; wires `UnregisterAgent` on goal (prevents OOM) |
| `SOHModel/Car/Model/CarDriver.cs` | `ID = Guid.NewGuid()` in constructor | Scheduler uses `new CarDriver(...)`; without unique IDs only one car survives in `CarLayer.Driver` |
| `SOHModel/Car/Model/CarDriver.cs` | Enhanced `CurrentEdgeId` null/array handling | Campus graph `osmid` attributes can be arrays; prevents CSV export crashes during heatmap runs |
| `CarletonDrivingBox/config.json` | Schedule file on scheduler layer only | Canonical SOH pattern (Bus, Train, HumanTraveler); no schedule CSV on `CarDriver` agent |
| `CarletonDrivingBox/Program.cs` | Registers `CarletonCarLayer` + `CarletonCarDriverSchedulerLayer` | Box-local init spawn guard and lat/lon scheduler wiring |

**Not in SOHModel** (kept in CarletonDrivingBox only):

- `CarDriverSchedulerLayer` lat/lon `ReadPosition()` fallback
- `CarLayer` init-spawn `file` guard (moved to `CarletonCarLayer`)
- `CarLayer.LooksLikeSchedulerCsv` header heuristic
- `CarDriverSchedulerLayer` `TryAdd` / per-spawn `try/catch` / row-level `driveMode` reading

---

## 1. `CarletonCarLayer` skip init spawn without agent file (box-local)

**Symptom:** `NullReferenceException` at `AgentManager.CreateAgentsInternal` during `CarLayer.InitLayer` on CarletonDrivingBox startup.

**Cause:** Canonical scheduler config puts the schedule CSV on the scheduler layer only (no `file` on `CarDriver`). Stock `CarLayer` always calls `SpawnAgents` for every agent mapping; with `count` set but no file, MARS NREs while building agents.

**Fix:** `CarletonCarLayer` filters `AgentInitConfigs` to entries with a non-empty `File` before delegating to stock `CarLayer.InitLayer`:

```csharp
AgentInitConfigs = layerInitData.AgentInitConfigs
    .Where(config => !string.IsNullOrEmpty(config.File))
    .ToList()
```

Registered in `Program.cs` as `"CarLayer"` so existing `config.json` layer names are unchanged. C-ITS and other boxes using stock `CarLayer` are unaffected.

---

## 2. `CarDriver.ID = Guid.NewGuid()` (SOHModel — keep)

**Symptom:** `CarDriver_trips.geojson` has 0–1 features despite thousands of scheduled spawns.

**Cause:** `BusDriver`, `TrainDriver`, and `FerryDriver` all set `ID = Guid.NewGuid()` in their constructors. `CarDriver` did not. The scheduler creates agents with `new CarDriver(...)`, bypassing `AgentManager.SpawnAgents`. Every car received the default GUID `00000000-0000-0000-0000-000000000000`. `CarLayer.Driver` is a `Dictionary<Guid, IAgent>`, so only one car could be stored at a time.

**Fix:**

```csharp
public CarDriver(...)
{
    ID = Guid.NewGuid();
    // ...
}
```

This is a genuine upstream bugfix and should remain in SOHModel.

---

## 3. `CurrentEdgeId` export safety (SOHModel — keep)

**Symptom:** Simulation crashes mid-run while writing `CarDriver.csv`.

**Cause:** Campus graph edges can expose `osmid` as an array or bracketed string. Stock code threw or returned misleading values.

**Fix:** Null-safe getter with array/bracket parsing. Independent of scheduler wiring; kept for heatmap/CSV export stability.

---

## 4. `CarletonCarDriverSchedulerLayer` (box-local)

Stock `CarDriverSchedulerLayer` (SOHModel, **unchanged**) reads spawn coordinates from WKT `source`/`destination` columns via `SourceGeometry`/`TargetGeometry`. Carleton's `car_driver_schedule.csv` uses explicit lat/lon columns:

```csv
startTime,endTime,...,startLat,startLon,destLat,destLon,...,driveMode
6:00,6:09,...,45.3814076,-75.7006193,45.3792575,-75.7004525,,,3
```

Rather than patch SOHModel, Carleton follows the **SemiTruck / TrainSchedulerLayer pattern**: a box-local scheduler class reads coordinates from `dataRow.Data` directly and passes **`UnregisterAgent`** (not a no-op) into `CarDriver`.

| Component | Role |
|-----------|------|
| `CarletonCarLayer` | Load road graph; hold active drivers; skip init spawn without agent file |
| `CarletonCarDriverSchedulerLayer` | Spawn cars on the timetable from lat/lon columns; unregister on `GoalReached` |
| `CarDriver` | Drive one car along a route |

### OOM on scenarios 02–06 (`deltaT: 1`)

**Symptom:** `dotnet run` exhausts RAM on longer scenarios while scenario 01 may succeed.

**Cause:** Stock `CarDriverSchedulerLayer` passes an empty `Unregister` delegate into `CarDriver`. When a car reaches its goal, `CarDriver.Tick()` calls `_unregister.Invoke(...)`, which does nothing — the agent stays in MARS's tick list and `CarLayer.Driver` forever. With ~3200 cars over 10k–17k ticks, memory grows without bound.

**Fix (box-local):** Pass a wrapper that removes the driver from `CarLayer.Driver` and calls `UnregisterAgent` (same pattern as `TrainSchedulerLayer` and `SOHBigEventBox` / Barclays Arena multimodal travelers).

**Profiling (optional):**

```powershell
# Terminal 1
dotnet run --project SOHCarletonDrivingBox.csproj -- configs\config_scenario_02.json

# Terminal 2 — watch working set
Get-Process SOHCarletonDrivingBox -ErrorAction SilentlyContinue |
  Select-Object Id, @{n='WS_MB';e={[math]::Round($_.WorkingSet64/1MB)}}
```

Or with `dotnet-counters`: `dotnet tool install -g dotnet-counters` then `dotnet-counters monitor --process-id <pid> System.Runtime`

---

## Required `config.json` layout

Schedule file on **scheduler layer only** (not on the `CarDriver` agent):

```json
{
  "agents": [{
    "name": "CarDriver",
    "count": 3200,
    "outputs": [
      { "kind": "trips", "outputConfiguration": { "tripsFields": ["StableId"] } },
      { "kind": "csv" }
    ],
    "individual": [
      { "value": false, "parameter": "ResultTrajectoryEnabled" }
    ]
  }],
  "layers": [
    { "name": "CarLayer", "file": "resources/campus_drive_graph.geojson" },
    { "name": "CarletonCarDriverSchedulerLayer", "file": "resources/car_driver_schedule.csv" }
  ],
  "entities": [
    { "name": "Car", "file": "resources/car.csv" }
  ]
}
```

**Notes:**

- `"console": false` — `true` updates a progress bar every tick and makes long runs appear frozen; also fails in non-interactive shells.
- `"ResultTrajectoryEnabled": false` — required for large runs; `true` massively slows output.
- Removing `"file"` from `CarletonCarDriverSchedulerLayer` → **zero spawns**.

---

## Run

```powershell
dotnet build SOHCarletonDrivingBox.csproj
dotnet run --project SOHCarletonDrivingBox.csproj
```

**WSL (use Windows .NET):**

```bash
"/mnt/c/Program Files/dotnet/dotnet.exe" build SOHCarletonDrivingBox.csproj
"/mnt/c/Program Files/dotnet/dotnet.exe" run --project SOHCarletonDrivingBox.csproj
```

**Success criteria:** `results/CarDriver.csv` has more than one line; `CarDriver_trips.geojson` contains trip features after cars reach exits.

---

## Impact on other SOH projects

- Stock `CarDriverSchedulerLayer` and `CarLayer` unchanged — BusBox, C-ITS, etc. behave as before.
- `CarDriver` unique-ID fix benefits any scheduler path using `new CarDriver(...)`.
- Carleton lat/lon schedule format and init-spawn guard are isolated in `CarletonDrivingBox`.

---

## Repository paths

| Component | Path |
|-----------|------|
| Simulation box | `CarletonDrivingBox/` |
| Box-local car layer | `CarletonDrivingBox/CarletonCarLayer.cs` |
| Box-local scheduler | `CarletonDrivingBox/CarletonCarDriverSchedulerLayer.cs` |
| Shared library (minimal patch) | `SOHModel/` (`CarDriver.cs` only) |
