# Carleton MARS evacuation route optimization

Closed-loop / ranked search for shorter campus clearance (`evac_end_s`) by
choosing lot→exit split fractions. Inspired by scenario 12 and lessons from
heavy SW overload (gridlock).

Existing scenarios **01–12** are unchanged. Candidates live only under:

- `resources/schedules/opt_candidates/`
- `configs/opt_candidates/`
- `results/opt_candidates/` (sims + `leaderboard.csv`; gitignored outputs)

---

## Lessons baked into the search

| Plan | Assignment idea | Effect |
|------|-----------------|--------|
| **01** | P1/P2 → SW; P3–P7 → NE | NE corridor overloaded (esp. P6/P7) |
| **10** | P6 → SW | Moves 900 cars off NE onto SW |
| **12** | P6 → SW + P7 50/50 | Best early one-shot clearance |
| Heavy SW (old jam) | Large SW share (e.g. P5 + half P7 + delays) | Overloads Meadowlands / SW; gridlock |

Takeaway: minimize clearance by **balancing exit demand**, not dumping
everything onto the “faster” exit. Soft SW demand cap ≈ **1800–2000** cars
(P1+P2 + SW shares of P5/P6/P7).

Fixed in this optimizer:

- **P1/P2 → Meadowlands (SW)** always
- **P3/P4 → Brewer (NE)** always
- Search **P5/P6/P7** fractions to SW on `{0, 0.25, 0.5, 0.75, 1.0}` + hill-climb

Exit coordinates:

- **Meadowlands / SW:** `45.3675, -75.7040`
- **Brewer / NE:** `45.387983, -75.690183`

Scenario 12 seed name: `opt_p6sw1_p7sw05_p5sw0`

---

## How to find best clearance (overnight)

### 1. Fast proxy rank (seconds — do this first)

From `CarletonDrivingBox/`:

```bash
python scripts/optimize_exit_assignment.py --optimize --write-top 10
```

This:

1. Scores the full 5³ coarse grid with  
   `proxy = |SW−NE| + SW_overload_penalty + small_distance_to_s12`
2. Hill-climbs neighbors of the s12 seed and the best proxy plan
3. Writes top-K:
   - `resources/schedules/opt_candidates/<name>_schedule.csv`
   - `configs/opt_candidates/config_<name>.json`
4. Updates `results/opt_candidates/leaderboard.csv`

Lower **proxy_score** is better for filtering. It is **not** real clearance.

### 2. Real MARS evaluation (hours each)

**Option A — eval the top 3 proxy plans (recommended overnight):**

```bash
python scripts/optimize_exit_assignment.py --optimize --write-top 10 --eval-top 3
```

**Option B — eval one named candidate:**

```bash
python scripts/optimize_exit_assignment.py --eval-sim opt_p6sw1_p7sw05_p5sw0
```

**Option C — hill-climb with a budget of real sims:**

```bash
python scripts/optimize_exit_assignment.py --optimize --max-evals 5
```

Each eval runs:

```text
dotnet run --project SOHCarletonDrivingBox.csproj -- configs/opt_candidates/config_<name>.json
python scripts/analyze_run.py results/opt_candidates/<name>/
```

and records `evac_end_s` on the leaderboard.

### 3. Pick the winner

Open `results/opt_candidates/leaderboard.csv`. Rank by **`evac_end_s` ascending**
(campus clear / last leave). Use proxy_score only when a row has not been
simulated yet.

Promote a winner to a future scenario **13+** only after review — never
overwrite 01–12.

### Re-analyze without re-simulating

```bash
python scripts/optimize_exit_assignment.py --analyze-only opt_p6sw1_p7sw05_p5sw0
```

---

## Proxy scoring details

| Term | Role |
|------|------|
| `|SW_demand − NE_demand|` | Balance exits (primary) |
| Soft overload if SW > 1800 | Mild penalty |
| Steep overload if SW > 2000 | Strong penalty (jam region) |
| Distance to s12 seed | Small tie-break; keeps search near the known win |

Demand uses lot sizes P1=100 … P7=1100 (3200 total).

---

## CLI cheat sheet

```bash
# Default useful action (same as --optimize --write-top 10)
python scripts/optimize_exit_assignment.py --optimize --write-top 10

# Print top proxy rows without writing
python scripts/optimize_exit_assignment.py --list

# Show s12 seed naming / demand
python scripts/optimize_exit_assignment.py --baseline-check

# Real sims (WARN: hours each)
python scripts/optimize_exit_assignment.py --eval-sim opt_p6sw1_p7sw05_p5sw0
python scripts/optimize_exit_assignment.py --optimize --eval-top 3
python scripts/optimize_exit_assignment.py --optimize --max-evals 5
```

---

## What `SOHBigEventBox` (Barclays Arena) does

Arena box name: **`SOHBigEventBox`** (agents/logic under `SOHModel/BigEvent/`).

It models post-event egress with CSV-driven staged multimodal release and
explicit source→destination rows. There is **no** automated lot→exit
optimizer. Carleton scenarios 10–12 already hand-tune destinations; this
branch formalizes that search with proxy ranking + optional closed-loop sims.
