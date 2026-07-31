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

**Search is open** — ranking uses overload / near-soft-cap and imbalance (plus a
light P5→SW pattern term). Scenario 12
(`opt_p6sw1_p7sw05_p5sw0`) remains a useful **reference baseline** for
`--baseline-check` and for comparing real clearance, but it is **not** forced
to write / display / eval position #1.

Opt-candidate configs use a **shorter 10000s screening horizon**
(`startPoint` + 10000s; with `2021-10-11T06:00:00` → `endPoint`
`2021-10-11T08:46:40`). That is for screening only: **full clear still needs
`remaining≈0`**. If cars are still jammed at 10k, treat the run as
**failed/incomplete** (extend `endPoint` for a confirmation sim).

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
- Hill-climb probes neighbors of the top proxy rows

Exit coordinates:

- **Meadowlands / SW:** `45.3675, -75.7040`
- **Brewer / NE:** `45.387983, -75.690183`

Scenario 12 reference name: `opt_p6sw1_p7sw05_p5sw0`

---

## How to find best clearance (overnight)

### 1. Fast proxy rank (seconds — filter only)

From `CarletonDrivingBox/`:

```bash
python scripts/optimize_exit_assignment.py --optimize --write-top 10
```

This:

1. Scores the coarse grid (P5∈{0,0.25}) with a filter proxy (see below)
2. Hill-climbs neighbors of the top proxy rows
3. Writes top-K by open proxy rank:
   - `resources/schedules/opt_candidates/<name>_schedule.csv`
   - `configs/opt_candidates/config_<name>.json` (10k screening `endPoint`)
4. Updates `results/opt_candidates/leaderboard.csv`

Lower **proxy_score** is better for filtering. It is **not** real clearance.

### 2. Real MARS evaluation (hours each)

Evaluate top proxy rows (or any named candidate). Screening horizon is 10k s;
confirm full clear with `remaining≈0` (jam at horizon = failed/incomplete).

**Option A — recommended overnight (top proxy rows):**

```bash
python scripts/optimize_exit_assignment.py --optimize --write-top 10 --eval-top 3
```

**Option B — eval one candidate:**

```bash
python scripts/optimize_exit_assignment.py --eval-sim opt_p6sw1_p7sw05_p5sw0
```

**Option C — hill-climb with a budget of real sims (starts at best proxy):**

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
(campus clear / last leave). Require **remaining≈0**; ignore proxy_score once
real sims exist — the proxy is only a shortlist filter.

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
2. **|SW−NE| imbalance** (weaker term)

| Term | Role |
|------|------|
| Soft overload if SW > 1800 | Mild penalty |
| Steep overload if SW > 2000 | Strong penalty (jam region) |
| Near soft-cap if SW > 1550 | Penalty before hard soft-cap (1600/1600 trap) |
| `|SW_demand − NE_demand|` × 0.15 | Weaker balance filter |
| P5→SW fraction × weight | Penalize sending P5 west |

Demand uses lot sizes P1=100 … P7=1100 (3200 total). Distance to the s12
reference may still appear on the leaderboard for comparison but is **not**
used in ranking.

---

## CLI cheat sheet

```bash
# Default useful action (same as --optimize --write-top 10)
python scripts/optimize_exit_assignment.py --optimize --write-top 10

# Print top proxy rows without writing
python scripts/optimize_exit_assignment.py --list

# Show scenario_12 reference naming / demand
python scripts/optimize_exit_assignment.py --baseline-check

# Real sims (WARN: hours each; 10k screening horizon)
python scripts/optimize_exit_assignment.py --eval-sim <name>
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
