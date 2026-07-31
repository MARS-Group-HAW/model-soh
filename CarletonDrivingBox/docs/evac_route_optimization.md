# Carleton MARS evacuation route optimization

Experiment scaffold for shortening campus clearance by choosing better
lot→exit assignments (inspired by scenario 12 and patterns from
`SOHBigEventBox`).

Existing scenarios **01–12** are unchanged. Candidate schedules from this
work live under `resources/schedules/opt_candidates/` only.

---

## Lesson from scenario 12

Manual route changes already cut clearance in MARS:

| Scenario | Assignment idea | Effect (qualitative) |
|----------|-----------------|----------------------|
| **01** | P1/P2 → Meadowlands (SW); P3–P7 → Brewer (NE) | Baseline: NE corridor overloaded (especially P6/P7) |
| **10** | Same as 01 but **P6 → Meadowlands** | Redirects 900 cars off the NE stack onto SW |
| **11** | Like 01, but **P7 split** 550 Brewer / 550 Meadowlands | Splits the largest lot across exits |
| **12** | **P6 → Meadowlands** + **P7 split** 550/550 | Combines both levers; shortest clearance among early one-shot plans |

Takeaway: clearance is sensitive to **exit load balance**, not only spawn
timing. Destination assignment (which lot uses Meadowlands vs Brewer) is a
high-leverage control variable.

Exit coordinates used in schedules:

- **Meadowlands / SW:** `45.3675, -75.7040`
- **Brewer / NE:** `45.387983, -75.690183`

---

## What `SOHBigEventBox` (Barclays Arena) does

Arena box name: **`SOHBigEventBox`** (agents/logic under `SOHModel/BigEvent/`).

It models post-event egress at Hamburg’s Barclays Arena (~23:00 end,
sim window 22:30–02:30):

1. **Staged multimodal release** — `visitor_spawning_*.csv` drives
   `AgentSchedulerLayer<Visitor>` with `startTime` / `endTime` /
   `spawningIntervalInMinutes` / `spawningAmount` per modality row
   (walk, bus, bike, car). Early trickle before event end, denser pulse
   after 23:00.
2. **Arena entrances → modality → destination** — visitors spawn at arena
   entrance polygons; CSV modality probabilities (`usesCar`, `usesBus`,
   `usesBike`, …) pick mode; destinations are points/polygons in the CSV.
3. **Car path** — `BarclaysParkingLayer` fills named “Parkplatz” lots to
   capacity at init (`ParkedCars`). A car visitor **picks a random parked
   car**, walks/drives via multimodal search to a destination polygon.
   There is **no** lot→exit optimizer and **no** capacity-aware exit split
   beyond what the schedule CSV encodes.
4. **Background traffic** — `Resident` agents add ambient car demand.
5. **Routing** — default multimodal search; bus uses a hand-wired
   station-pair fallback (`FindMultimodalRoute`). Congestion analysis is
   observational (heatmaps), not an automated optimizer.

**Borrow for Carleton:** CSV-driven staged release and explicit
source→destination rows (already mirrored by
`CarletonCarDriverSchedulerLayer` schedules). BigEvent does **not**
automate exit assignment; Carleton scenarios 10–12 already go further by
hand-tuning destinations. This experiment formalizes that search.

---

## Proposed approaches

| ID | Approach | Notes |
|----|----------|--------|
| **(a)** | OD / destination assignment search | Enumerate lot→{Meadowlands, Brewer} (and optional splits) over P5/P6/P7 (and later P3/P4). Keep P1/P2 on SW unless exploring otherwise. |
| **(b)** | Lot release sequencing | Vary start times / intervals (scenarios 02–06 style) jointly with (a). Higher dimensional; defer until destination search plateaus. |
| **(c)** | Exit capacity-aware split (scenario-12 style) | Fix total lot sizes; search split fractions for P6/P7 (and optionally P5) between SW and NE. Closest to the known win. |
| **(d)** | Greedy / hill-climb | Start from scenario 12; mutate one lot’s exit or split; accept if clearance proxy improves. Evaluate with short sims or a proxy (e.g. assigned demand per exit corridor). |

Objective: minimize **campus clearance time** (last agent leave), with
secondary metrics from `scripts/analyze_run.py` (per-lot clearance,
trip-time stats, heatmaps).

---

## Scaffold in this branch

- `scripts/optimize_exit_assignment.py` — reads scenario 01/12 schedule
  patterns, enumerates a small set of P5/P6/P7 exit fractions, writes
  candidate CSVs under `resources/schedules/opt_candidates/` (or
  `--dry-run` to print plans only). Does **not** run multi-hour sims.
- Evaluation is left to existing runners (see below).

---

## Next concrete experiment step

1. Generate candidates:
   ```bash
   python3 scripts/optimize_exit_assignment.py --write
   ```
2. For each written schedule, copy the scenario-12 config pattern, point
   `CarletonCarDriverSchedulerLayer` at the candidate CSV, and set
   `csvOptions.outputPath` to e.g. `results/opt_candidates/<name>/`.
3. Run:
   ```bash
   dotnet run --project SOHCarletonDrivingBox.csproj -- <path-to-candidate-config.json>
   ```
   or adapt `scripts/run_all_scenarios.py` to accept an explicit config
   list.
4. Score with:
   ```bash
   python3 scripts/analyze_run.py results/opt_candidates/<name>/
   ```
5. Rank by clearance; promote the best schedule into a future scenario
   **13+** only after review (do not overwrite 01–12).

Optional proxy before full sims: sum assigned vehicles per exit and
penalize imbalance vs scenario 12’s SW/NE totals as a cheap filter.
