# Carleton MARS evacuation route optimization

Closed-loop / ranked search for shorter campus clearance (`evac_end_s`) by
choosing lot→exit split fractions. Inspired by scenario 12 and lessons from
heavy SW overload (gridlock).

Existing scenarios **01–12** are unchanged. Candidates live only under:

- `resources/schedules/opt_candidates/`
- `configs/opt_candidates/`
- `results/opt_candidates/` (sims + `leaderboard.csv`; gitignored outputs)

---

## Important: proxy is a filter only

**Proxy does NOT guarantee campus clears.** A low `|SW−NE|` score can still jam
(e.g. balanced 1600/1600 with P5→SW overloaded SW approaches and trapped
P3/P4/P5 on the NE side). Only `--eval-sim` / real `evac_end_s` metrics prove
clearance.

**Scenario 12 is the known-good baseline to beat with real sims.** The seed
candidate `opt_p6sw1_p7sw05_p5sw0` is always written and evaluated first.
Use the proxy only to shortlist neighbors around that seed — never treat proxy
rank #1 as a clearance winner.

---

## Lessons baked into the search

| Plan | Assignment idea | Effect |
|------|-----------------|--------|
| **01** | P1/P2 → SW; P3–P7 → NE | NE corridor overloaded (esp. P6/P7) |
| **10** | P6 → SW | Moves 900 cars off NE onto SW |
| **12** | P6 → SW + P7 50/50 | Best early one-shot clearance |
| Heavy SW (old jam) | Large SW share (e.g. P5 + half P7 + delays) | Overloads Meadowlands / SW; gridlock |
| Balanced + P5→SW | ~1600/1600 with `p5_sw≥0.25` | SW approaches loaded; NE lots (P3/P4/P5) stuck |

Takeaway: minimize clearance by **beating scenario 12 in real sims**, not by
dumping demand onto a “balanced” proxy plan. Soft SW demand cap ≈ **1800–2000**
cars (P1+P2 + SW shares of P5/P6/P7); demand near that cap is also penalized.

Fixed in this optimizer:

- **P1/P2 → Meadowlands (SW)** always
- **P3/P4 → Brewer (NE)** always
- Search **P5/P6/P7** fractions to SW; default coarse grid caps **P5→SW ≤ 0.25**
- Hill-climb may still probe neighbors of the s12 seed

Exit coordinates:

- **Meadowlands / SW:** `45.3675, -75.7040`
- **Brewer / NE:** `45.387983, -75.690183`

Scenario 12 seed name: `opt_p6sw1_p7sw05_p5sw0`

---

## How to find best clearance (overnight)

### 1. Fast proxy rank (seconds — filter only)

From `CarletonDrivingBox/`:

```bash
python scripts/optimize_exit_assignment.py --optimize --write-top 10
```

This:

1. Scores the coarse grid (P5∈{0,0.25}) with a filter proxy (see below)
2. Always puts the **s12 seed first** in write / display / eval order
3. Hill-climbs neighbors of the s12 seed and the best proxy alternative
4. Writes top-K:
   - `resources/schedules/opt_candidates/<name>_schedule.csv`
   - `configs/opt_candidates/config_<name>.json`
5. Updates `results/opt_candidates/leaderboard.csv`

Lower **proxy_score** is better for filtering. It is **not** real clearance.

### 2. Real MARS evaluation (hours each)

**Always evaluate the scenario-12 seed first**, then optional neighbors.

**Option A — recommended overnight (s12 first, then next proxy rows):**

```bash
python scripts/optimize_exit_assignment.py --optimize --write-top 10 --eval-top 3
```

`--eval-top` always runs `opt_p6sw1_p7sw05_p5sw0` first, then remaining by the
new rank.

**Option B — eval s12 seed alone (start here):**

```bash
python scripts/optimize_exit_assignment.py --eval-sim opt_p6sw1_p7sw05_p5sw0
```

**Option C — hill-climb with a budget of real sims (starts at s12):**

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
(campus clear / last leave). Ignore proxy_score once real sims exist — the
proxy is only a shortlist filter.

Promote a winner to a future scenario **13+** only after it **beats scenario 12
clearance** in a real sim — never overwrite 01–12.

### Re-analyze without re-simulating

```bash
python scripts/optimize_exit_assignment.py --analyze-only opt_p6sw1_p7sw05_p5sw0
```

---

## Proxy scoring details

Ranking priority (not raw balance alone):

1. **Low overload / near-soft-cap SW penalty** (prefer)
2. **Close to scenario-12 seed** (strong weight)
3. **|SW−NE| imbalance** (weaker term)

| Term | Role |
|------|------|
| Soft overload if SW > 1800 | Mild penalty |
| Steep overload if SW > 2000 | Strong penalty (jam region) |
| Near soft-cap if SW > 1550 | Penalty before hard soft-cap (1600/1600 trap) |
| Distance to s12 seed | Strong preference for known-good baseline |
| `|SW_demand − NE_demand|` × 0.15 | Weaker balance filter |
| P5→SW fraction × weight | Penalize sending P5 west |
| Triple deviation from s12 | Extra hit when P5/P6/P7 all leave the s12 pattern |

Demand uses lot sizes P1=100 … P7=1100 (3200 total).

The s12 seed is **forced to display/write/eval position #1** regardless of
scalar proxy ties.

---

## CLI cheat sheet

```bash
# Default useful action (same as --optimize --write-top 10)
python scripts/optimize_exit_assignment.py --optimize --write-top 10

# Print top proxy rows without writing (s12 always listed first)
python scripts/optimize_exit_assignment.py --list

# Show s12 seed naming / demand
python scripts/optimize_exit_assignment.py --baseline-check

# Real sims (WARN: hours each) — start with s12 seed
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
